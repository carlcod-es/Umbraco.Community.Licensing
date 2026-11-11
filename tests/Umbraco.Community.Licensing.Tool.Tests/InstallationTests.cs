using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>The installation database (ADR-0013): schema, pragmas, refusals, and the idle lock setting (ADR-0022).</summary>
public class InstallationTests : IDisposable
{
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static ScriptedConsole Script(params string[] answers)
    {
        var console = new ScriptedConsole();
        foreach (var a in answers)
            console.Line(a);
        return console;
    }

    /// <summary>A plain connection, as any other tool would use (PDR-0046); no pooling, so nothing keeps the file open.</summary>
    private static SqliteConnection Plain(string folder)
    {
        var connection = new SqliteConnection($"Data Source={Path.Combine(folder, Startup.DatabaseFileName)};Pooling=False");
        connection.Open();
        return connection;
    }

    private static List<string> Query(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
            rows.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        return rows;
    }

    private static readonly Dictionary<string, string[]> Schema = new()
    {
        ["Settings"] = ["Name", "Value"],
        ["Products"] = ["ProductId", "DisplayName", "CreatedUtc"],
        ["FeatureDefinitions"] = ["ProductId", "Name", "Type", "AddedUtc"],
        ["SigningKeys"] = ["SigningKeyId", "ProductId", "PublicKey", "EncryptedPrivateKeyPem", "Role", "CreatedUtc", "ReplacedUtc", "ReplacedReason", "ReissuesSentUtc"],
        ["KeyReleases"] = ["SigningKeyId", "ProductId", "ConfirmedUtc"],
        ["Licenses"] = ["Reference", "ProductId", "CustomerEmail", "CustomerEmailKey", "CreatedUtc"],
        ["IssueEntries"] = ["Id", "Reference", "Kind", "IssuedUtc", "ExpiresUtc", "DisplayName", "VendorTag", "FeaturesJson", "OrderRef", "OrderRefKey", "KeyIdentifier", "SigningKeyId"],
    };

    [Fact]
    public void NewFolder_GetsTheSchema_AndNothingElse()
    {
        var folder = Path.Combine(_host.Home, "new", "folder");

        Installation.Create(folder);

        using var connection = Plain(folder);
        var tables = Query(connection, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%'");
        Assert.Equal(Schema.Keys.Order(), tables.Order());
        foreach (var (table, columns) in Schema)
            Assert.Equal(columns.Order(), Query(connection, $"SELECT name FROM pragma_table_info('{table}')").Order());
        Assert.Single(Query(connection, "SELECT MigrationId FROM __EFMigrationsHistory"));
    }

    [Fact]
    public void OpenExisting_WritesNothing_BytesAndLastWriteTimeAreIdentical_AndNoJournalRemains()
    {
        var folder = Path.Combine(_host.Home, "untouched");
        SessionTestHost.MakeInstallation(folder);
        var path = Path.Combine(folder, Startup.DatabaseFileName);
        var bytes = File.ReadAllBytes(path);
        var written = File.GetLastWriteTimeUtc(path);
        Thread.Sleep(50);

        var installation = Installation.OpenExisting(folder);
        installation.ReadIdleTime();

        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.False(File.Exists(path + "-journal"));
    }

    [Fact]
    public void ASessionThatQuitsAtOnce_LeavesTheDatabaseBytesIdentical()
    {
        var home = _host.SetUpInstallation();
        var path = Path.Combine(home, Startup.DatabaseFileName);
        var bytes = File.ReadAllBytes(path);
        var written = File.GetLastWriteTimeUtc(path);
        Thread.Sleep(50);

        var console = new ScriptedConsole().Line("0");
        Assert.Equal(0, _host.Run(console));

        Assert.Equal("", console.Err);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void OpenExisting_OnAReadOnlyFile_WithNothingPending_Opens()
    {
        var folder = Path.Combine(_host.Home, "readonly");
        SessionTestHost.MakeInstallation(folder);
        var path = Path.Combine(folder, Startup.DatabaseFileName);
        var attributes = File.GetAttributes(path);
        File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
        try
        {
            var installation = Installation.OpenExisting(folder);

            Assert.Equal(Path.GetFullPath(folder), installation.Folder);
        }
        finally
        {
            File.SetAttributes(path, attributes);
        }
    }

    [Fact]
    public void Keys_AreAsDesigned()
    {
        var folder = Path.Combine(_host.Home, "keys");
        Installation.Create(folder);

        using var connection = Plain(folder);
        string Pk(string table) => string.Join("+", Query(connection, $"SELECT name FROM pragma_table_info('{table}') WHERE pk > 0 ORDER BY pk"));
        Assert.Equal("Name", Pk("Settings"));
        Assert.Equal("ProductId", Pk("Products"));
        Assert.Equal("ProductId+Name", Pk("FeatureDefinitions"));
        Assert.Equal("SigningKeyId", Pk("SigningKeys"));
        Assert.Equal("SigningKeyId+ProductId", Pk("KeyReleases"));
        Assert.Equal("Reference", Pk("Licenses"));
        Assert.Equal("Id", Pk("IssueEntries"));

        var indexed = Query(connection,
            "SELECT m.name || '.' || c.name FROM sqlite_master m, pragma_index_list(m.name) i, pragma_index_info(i.name) c WHERE m.type = 'table'");
        Assert.Contains("Licenses.CustomerEmailKey", indexed);
        Assert.Contains("IssueEntries.OrderRefKey", indexed);
    }

    [Fact]
    public void ReopeningAnInstallation_ChangesNothing()
    {
        var folder = Path.Combine(_host.Home, "again");
        Installation.Create(folder);
        Installation.OpenExisting(folder);
        Installation.OpenExisting(folder);

        using var connection = Plain(folder);
        Assert.Single(Query(connection, "SELECT MigrationId FROM __EFMigrationsHistory"));
    }

    [Fact]
    public void ConnectionsAnActionGets_HaveSecureDeleteOn_AndJournalModeDelete()
    {
        var installation = Installation.Create(Path.Combine(_host.Home, "pragmas"));

        // Two contexts in turn: each opens its own connection, and each must have the pragmas.
        for (var i = 0; i < 2; i++)
        {
            using var db = installation.OpenDb();
            db.Database.OpenConnection();
            using var connection = (SqliteConnection)db.Database.GetDbConnection();
            Assert.Equal(["1"], Query(connection, "PRAGMA secure_delete"));
            Assert.Equal(["delete"], Query(connection, "PRAGMA journal_mode"));
        }
    }

    [Fact]
    public void Timestamps_AreReadBackAsUtc()
    {
        var installation = Installation.Create(Path.Combine(_host.Home, "utc"));
        var created = new DateTime(2026, 11, 9, 8, 30, 0, DateTimeKind.Utc);
        using (var db = installation.OpenDb())
        {
            db.Products.Add(new Data.Product { ProductId = "p", DisplayName = "P", CreatedUtc = created });
            db.SaveChanges();
        }

        using var read = installation.OpenDb();
        var product = read.Products.Single();
        Assert.Equal(created, product.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, product.CreatedUtc.Kind);
    }

    private static byte[] Snapshot(string folder) => File.ReadAllBytes(Path.Combine(folder, Startup.DatabaseFileName));

    [Fact]
    public void OpenExisting_WithNoDatabase_IsRefused_AndCreatesNothing()
    {
        var folder = Path.Combine(_host.Home, "absent");

        var ex = Assert.Throws<InstallationRefusedException>(() => Installation.OpenExisting(folder));

        Assert.Contains(folder, ex.Message);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void OpenExisting_ZeroByteFile_IsRefused_AndLeftUntouched()
    {
        Directory.CreateDirectory(_host.Cwd);
        var file = Path.Combine(_host.Cwd, Startup.DatabaseFileName);
        File.WriteAllBytes(file, []);

        Assert.Throws<InstallationRefusedException>(() => Installation.OpenExisting(_host.Cwd));

        Assert.Equal(0, new FileInfo(file).Length);
    }

    [Fact]
    public void OpenExisting_ForeignSqliteDatabase_IsRefused_WithoutGettingTheSchema()
    {
        Directory.CreateDirectory(_host.Cwd);
        using (var connection = Plain(_host.Cwd))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Other (Id INTEGER PRIMARY KEY, Note TEXT)";
            command.ExecuteNonQuery();
        }

        var before = Snapshot(_host.Cwd);
        Assert.Throws<InstallationRefusedException>(() => Installation.OpenExisting(_host.Cwd));

        Assert.Equal(before, Snapshot(_host.Cwd));
        using var check = Plain(_host.Cwd);
        Assert.Equal(["Other"], Query(check, "SELECT name FROM sqlite_master WHERE type = 'table'"));
    }

    [Fact]
    public void Create_WhereAnInstallationIs_ThrowsAndChangesNothing()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var before = Snapshot(_host.Cwd);

        Assert.Throws<InstallationExistsException>(() => Installation.Create(_host.Cwd, db => db.Settings.Add(new Data.Setting { Name = "X", Value = "y" })));

        Assert.Equal(before, Snapshot(_host.Cwd));
    }

    [Fact]
    public void Create_AcceptsAFolderWithOtherFiles_AndLeavesThemAlone()
    {
        File.WriteAllText(Path.Combine(_host.Cwd, "notes.txt"), "mine");

        Installation.Create(_host.Cwd);

        Assert.Equal("mine", File.ReadAllText(Path.Combine(_host.Cwd, "notes.txt")));
        Assert.True(Installation.Holds(_host.Cwd));
    }

    [Fact]
    public void Create_WhenPopulatingFails_RemovesTheFoldersItMade()
    {
        var folder = Path.Combine(_host.Home, "made", "here");

        Assert.Throws<InvalidOperationException>(() => Installation.Create(folder, _ => throw new InvalidOperationException("boom")));

        Assert.False(Directory.Exists(Path.Combine(_host.Home, "made")));
    }

    [Fact]
    public void Create_WhenPopulatingFails_InAFolderThatExisted_RemovesOnlyTheDatabase()
    {
        File.WriteAllText(Path.Combine(_host.Cwd, "notes.txt"), "mine");

        Assert.Throws<InvalidOperationException>(() => Installation.Create(_host.Cwd, _ => throw new InvalidOperationException("boom")));

        Assert.Equal(["notes.txt"], Directory.GetFileSystemEntries(_host.Cwd).Select(Path.GetFileName));
    }

    [Fact]
    public void Create_WritesThePopulatedRowsInOneTransaction_AndUndoCreateRemovesWhatItMade()
    {
        var folder = Path.Combine(_host.Home, "undo");

        var installation = Installation.Create(folder, db =>
        {
            db.Settings.Add(new Data.Setting { Name = "A", Value = "1" });
            db.Settings.Add(new Data.Setting { Name = "B", Value = "2" });
            db.SaveChanges();
        });

        using (var db = installation.OpenDb())
            Assert.Equal(2, db.Settings.Count());

        installation.UndoCreate();
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void Timestamps_NotMarkedUtc_AreRefusedOnWrite_NotShiftedByTheMachineOffset()
    {
        var installation = Installation.Create(Path.Combine(_host.Home, "kinds"));

        foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Local })
        {
            using var db = installation.OpenDb();
            db.Products.Add(new Data.Product { ProductId = "p", DisplayName = "P", CreatedUtc = new DateTime(2026, 11, 9, 8, 30, 0, kind) });
            var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        using var read = installation.OpenDb();
        Assert.Empty(read.Products);
    }

    [Fact]
    public void DatabaseWithAnUnknownMigration_IsRefusedOnStderr_BeforeTheSessionOpens()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        using (var connection = Plain(_host.Cwd))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_FromTheFuture', '99.0.0')";
            command.ExecuteNonQuery();
        }

        var console = Script("0");
        var exit = _host.Run(console);

        Assert.Equal(1, exit);
        Assert.Contains("update the tool", console.Err);
        Assert.Contains(Path.GetFullPath(_host.Cwd), console.Err);
        Assert.Equal("", console.Out);
    }

    [Fact]
    public void DatabaseThatCannotBeOpened_IsRefusedOnStderr_BeforeTheSessionOpens()
    {
        Directory.CreateDirectory(_host.Cwd);
        File.WriteAllBytes(Path.Combine(_host.Cwd, Startup.DatabaseFileName), Enumerable.Range(0, 4096).Select(i => (byte)(i * 7 + 3)).ToArray());

        var console = Script("0");
        var exit = _host.Run(console);

        Assert.Equal(1, exit);
        Assert.Contains("cannot open the database", console.Err);
        Assert.DoesNotContain("Exception", console.Err);
        Assert.Equal("", console.Out);
    }

    [Fact]
    public void FolderRefusedMidSession_IsAnActionFailure_AndTheSessionKeepsItsFolder()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var broken = Path.Combine(_host.Home, "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, Startup.DatabaseFileName), "not a database, just text that is long enough to be read as a header");
        string? after = null;
        var actions = new ActionTable();
        actions.Register("settings.prefix", s =>
        {
            try { s.UseFolder(broken); }
            finally { after = s.Folder; }
        });

        var console = Script("5", "2", "0", "0");
        var exit = _host.Run(console, actions);

        Assert.Equal(0, exit);
        Assert.Equal(Path.GetFullPath(_host.Cwd), after);
        Assert.Contains("failed: ucl cannot open the database", console.Out);
        Assert.Equal("", console.Err);
    }

    [Theory]
    [InlineData("15", 15)]
    [InlineData("1", 1)]
    [InlineData("1440", 1440)]
    public void IdleLockMinutes_ReachTheSessionLock(string stored, int minutes)
    {
        SessionTestHost.MakeInstallation(_host.Cwd, stored);
        TimeSpan? seen = null;
        var actions = new ActionTable();
        actions.Register("products.add", s => seen = s.Lock.IdleTime);

        _host.Run(Script("1", "1", "0", "0"), actions);

        Assert.Equal(TimeSpan.FromMinutes(minutes), seen);
    }

    [Theory]
    [InlineData("never")]
    [InlineData("NEVER")]
    public void IdleLockNever_ReachesTheSessionLock_AsNull(string stored)
    {
        SessionTestHost.MakeInstallation(_host.Cwd, stored);
        TimeSpan? seen = TimeSpan.FromMinutes(99);
        var actions = new ActionTable();
        actions.Register("products.add", s => seen = s.Lock.IdleTime);

        _host.Run(Script("1", "1", "0", "0"), actions);

        Assert.Null(seen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("1441")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData(" 5")]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("٣")] // an Arabic-Indic digit is not an ASCII digit
    public void IdleLockMissingOrInvalid_IsRefusedLikeAnUnopenableDatabase(string? stored)
    {
        SessionTestHost.MakeInstallation(_host.Cwd, stored);

        var console = Script("0");
        var exit = _host.Run(console);

        Assert.Equal(1, exit);
        Assert.Contains("idle lock setting", console.Err);
        Assert.Equal("", console.Out);
    }

    [Fact]
    public void OpenDb_WithNoInstallation_IsAnInvalidOperation()
    {
        var console = new ScriptedConsole();
        using var session = new Session(new ToolEnvironment(console, new FakeTimeProvider(), _host.Home, _host.Cwd, 1000));

        Assert.Throws<InvalidOperationException>(() => session.OpenDb());
    }

    [Fact]
    public void ActionContext_IsUsableAfterAFailedAction()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var actions = new ActionTable();
        var count = -1;
        actions.Register("products.add", s =>
        {
            using var db = s.OpenDb();
            db.Products.Add(new Data.Product { ProductId = "a", DisplayName = "A", CreatedUtc = DateTime.UtcNow });
            db.SaveChanges();
            db.Products.Add(new Data.Product { ProductId = "a", DisplayName = "again", CreatedUtc = DateTime.UtcNow }); // duplicate key
            db.SaveChanges();
        });
        actions.Register("products.list", s =>
        {
            using var db = s.OpenDb();
            count = db.Products.Count();
        });

        var console = Script("1", "1", "4", "0", "0");
        var exit = _host.Run(console, actions);

        Assert.Equal(0, exit);
        Assert.Contains("Products - Add a product failed:", console.Out);
        Assert.Equal(1, count);
    }
}
