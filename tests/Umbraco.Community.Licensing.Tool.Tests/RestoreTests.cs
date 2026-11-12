using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// Restore from a backup (vendor-tool-backup: Files read by the tool, Backup import; ADR-0015, ADR-0023, PDR-0057), as whole sessions.
/// Backups are made by Backup &gt; Export in a session of a source installation in a home of its own, then restored into the
/// home of <see cref="_target"/>.
/// </summary>
public class RestoreTests : IDisposable
{
    private const string Pass = "a source passphrase 2026";
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Product = "source.product";
    private const string Expiry = "2027-03-01T23:59:59Z";
    private static readonly DateTime Taken = Utc(2026, 11, 8, 9, 30, 15);

    private readonly SessionTestHost _source = new();
    private readonly SessionTestHost _target = new();
    private string? _backup;

    public void Dispose()
    {
        _source.Dispose();
        _target.Dispose();
    }

    private static ScriptedConsole Script(params string[] answers)
    {
        var console = new ScriptedConsole();
        foreach (var a in answers)
            console.Line(a);
        return console;
    }

    private static ScriptedConsole Run(SessionTestHost host, ScriptedConsole console, FakeTimeProvider? clock = null, Startup.Offer? offer = null)
    {
        var exit = host.Run(console, offer: offer ?? Startup.Offer.Default, clock: clock);
        Assert.True(exit == 0, console.Err + console.Out);
        return console;
    }

    /// <summary>Issue (Licenses 2, Issue 1) of the source product, which has no features, up to and with the summary and no key file; <paramref name="passphrase"/> when the session holds none.</summary>
    private static ScriptedConsole Issue(ScriptedConsole c, string orderRef, params string[] passphrases) =>
        Issue(c, orderRef, passphrases, default);

    private static ScriptedConsole Issue(ScriptedConsole c, string orderRef, string passphrase, TimeSpan advance) =>
        Issue(c, orderRef, [passphrase], advance);

    private static ScriptedConsole Issue(ScriptedConsole c, string orderRef, TimeSpan advance) =>
        Issue(c, orderRef, [], advance);

    private static ScriptedConsole Issue(ScriptedConsole c, string orderRef, string[] passphrases, TimeSpan advance)
    {
        c.Line("2", advance).Line("1").Line("1").Line(Expiry).Line("").Line("").Line(orderRef).Line("owner@example.com").Line("");
        foreach (var passphrase in passphrases)
            c.Hidden(passphrase);
        return c.Line("").Line("0");
    }

    /// <summary>
    /// A source installation with idle lock <paramref name="idle"/> minutes, one product and one license, and a backup of it taken
    /// through Backup &gt; Export. Returns the zip's path.
    /// </summary>
    private string MakeBackup(string idle = "", string passphrase = Pass)
    {
        _source.SetUpInstallation(idleLock: idle, passphrase: passphrase);
        Run(_source, Script("1", "1", Product, "Source Product").Hidden(passphrase).Line("0").Line("0"));
        var issue = Issue(new ScriptedConsole(), "ORD-1", passphrase).Line("0");
        Run(_source, issue);

        var zip = Path.Combine(_source.Cwd, "backup.zip");
        Run(_source, Script("4", "1", zip, "0", "0"), ClockAt(Taken));
        _backup = zip;
        return zip;
    }

    private string Backup => _backup ?? MakeBackup();

    private static string Content(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return JsonSerializer.Serialize(new object[]
        {
            db.Settings.AsNoTracking().OrderBy(x => x.Name).ToList(),
            db.Products.AsNoTracking().OrderBy(x => x.ProductId).ToList(),
            db.SigningKeys.AsNoTracking().OrderBy(x => x.SigningKeyId).ToList(),
            db.Licenses.AsNoTracking().OrderBy(x => x.Reference).ToList(),
            db.IssueEntries.AsNoTracking().OrderBy(x => x.Id).ToList(),
        });
    }

    private static int Entries(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return db.IssueEntries.Count();
    }

    // Startup offer: 1 Set up, 2 Restore. Then the backup path, the folder (empty takes .ucl), the question.
    private static ScriptedConsole OfferRestore(string zip, string folder = "", string answer = "y") => Script("2", zip, folder, answer);

    private static void AssertNothingMade(SessionTestHost host)
    {
        Assert.False(Directory.Exists(host.HomeInstallation));
        Assert.Empty(Directory.GetFileSystemEntries(host.Cwd));
    }

    private static string FullOf(string path) => Path.GetFullPath(path);

    private static void Rewrite(string zipPath, Action<JsonObject> change)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        var entry = zip.GetEntry("manifest.json")!;
        JsonObject manifest;
        using (var stream = entry.Open())
            manifest = JsonNode.Parse(stream)!.AsObject();
        entry.Delete();
        change(manifest);
        var rewritten = zip.CreateEntry("manifest.json");
        using var writer = new StreamWriter(rewritten.Open());
        writer.Write(manifest.ToJsonString());
    }

    private string Craft(string name, params (string Name, byte[] Content)[] entries)
    {
        var path = Path.Combine(_target.Cwd, name);
        using var file = new FileStream(path, FileMode.CreateNew);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            using var stream = zip.CreateEntry(entryName).Open();
            stream.Write(content);
        }

        return path;
    }

    /// <summary>Sets the uncompressed size of the first entry to 100 in its local and central headers, so a manifest that inflates to 200 KB declares a small one. The BCL reader stops at the declared size, so the entry reads as cut short and is refused (the bounded read in RestoreBackup is a second guard the BCL does not let a test reach).</summary>
    private static string LyingHeader(string zipPath)
    {
        var bytes = File.ReadAllBytes(zipPath);
        PatchSize(bytes, 0x04034b50, 22);
        PatchSize(bytes, 0x02014b50, 24);
        File.WriteAllBytes(zipPath, bytes);
        return zipPath;

        static void PatchSize(byte[] zip, uint signature, int offset)
        {
            for (var i = 0; i + offset + 4 <= zip.Length; i++)
            {
                if (BitConverter.ToUInt32(zip, i) != signature)
                    continue;
                BitConverter.GetBytes(100u).CopyTo(zip, i + offset);
                return;
            }

            throw new InvalidOperationException("Header not found.");
        }
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Manifest(string toolVersion = "0.1.0-beta.7", string migration = "x", int format = 1, string taken = "2026-11-08T09:30:15Z") =>
        $$"""{"format":{{format}},"toolVersion":"{{toolVersion}}","lastMigration":"{{migration}}","takenUtc":"{{taken}}"}""";

    // --- Import file not found

    [Fact]
    public void ImportFileNotFound_SaysSoWithTheFullPath_AsksAgain_AFolderPathToo_AndNothingIsCreated()
    {
        var missing = Path.Combine(_target.Home, "Backups", "ucl-2026-11-01.zip");
        var folder = _target.Cwd;
        var console = Script("2", missing, folder, "/cancel", "0");

        Run(_target, console);

        Assert.Contains($"The file {FullOf(missing)} does not exist. Give the path again.", console.Out);
        Assert.Contains($"{FullOf(folder)} is a folder, not a file.", console.Out);
        Assert.Equal(3, SessionTestHost.Count(console.Out, "Backup file to restore"));
        Assert.False(Directory.Exists(Path.Combine(_target.Home, "Backups")));
        AssertNothingMade(_target);
    }

    [Fact]
    public void ImportPathTypedWithTilde_IsReadFromHome_AndRestores()
    {
        var zip = Backup;
        File.Copy(zip, Path.Combine(_target.Home, "b.zip"));

        var console = Run(_target, OfferRestore("~/b.zip").Line("0"));

        Assert.Equal(Content(_source.HomeInstallation), Content(_target.HomeInstallation));
        Assert.DoesNotContain("does not exist", console.Out);
    }

    [Fact]
    public void ImportPathWithTildeInAName_IsReadFromTheCurrentFolder_NotHome()
    {
        var zip = Backup;
        File.Copy(zip, Path.Combine(_target.Home, "~b.zip"));
        File.Copy(zip, Path.Combine(_target.Cwd, "~b.zip"));

        Run(_target, OfferRestore("~b.zip").Line("0"));

        Assert.Equal(Content(_source.HomeInstallation), Content(_target.HomeInstallation));
    }

    // --- Restore to a new machine / another operating system

    [Fact]
    public void RestoreToANewMachine_FromTheStartupOffer_ShowsTheDateAndWarning_CreatesTheInstallation_ThenTheMainMenu()
    {
        var zip = Backup;
        var sourceContent = Content(_source.HomeInstallation);

        var console = Run(_target, OfferRestore(zip).Line("0"));

        var home = FullOf(_target.HomeInstallation);
        var offered = console.Out.IndexOf("Backup file to restore", StringComparison.Ordinal);
        var date = console.Out.IndexOf("This backup was taken on 2026-11-08 09:30:15 UTC.", StringComparison.Ordinal);
        var warning = console.Out.IndexOf(RestoreBackup.Warning, StringComparison.Ordinal);
        var question = console.Out.IndexOf("Restore this backup? [y/N]", StringComparison.Ordinal);
        var usingLine = console.Out.IndexOf($"Using {home}", StringComparison.Ordinal);
        var menu = console.Out.IndexOf("Main menu", usingLine, StringComparison.Ordinal);
        Assert.True(offered >= 0 && offered < date && date < warning && warning < question && question < usingLine && usingLine < menu, console.Out);
        Assert.Equal("", console.Err);
        Assert.Empty(console.HiddenGiven);
        Assert.Equal(sourceContent, Content(_target.HomeInstallation));
        Assert.Equal(["ucl.db"], Directory.GetFiles(_target.HomeInstallation).Select(Path.GetFileName));
        Assert.Empty(Directory.GetFileSystemEntries(_target.Cwd));
    }

    [Fact]
    public void RestoreOnAnotherOperatingSystem_EveryProductLicenseAndKeyIsThere_AndAnIssueSucceedsWithTheBackupsPassphrase()
    {
        var zip = Backup;
        using (var archive = ZipFile.OpenRead(zip))
            Assert.All(archive.Entries, e => Assert.DoesNotContain('\\', e.FullName)); // entry names use /, whatever system wrote them
        var sourceContent = Content(_source.HomeInstallation);
        var before = Entries(_source.HomeInstallation);

        // Restored into a different home, then an issue: the session holds no passphrase, so it is asked.
        var console = OfferRestore(zip);
        Issue(console, "ORD-2", Pass);
        Run(_target, console.Line("0"));

        Assert.Equal("", console.Err);
        Assert.DoesNotContain(KeyProtection.WrongPassphrase, console.Out);
        Assert.Single(console.HiddenGiven);
        Assert.Equal(before + 1, Entries(_target.HomeInstallation));
        Assert.Equal(1, Entries(_source.HomeInstallation));
        Assert.NotEqual(sourceContent, Content(_target.HomeInstallation)); // the issue is in the restored installation only
        Assert.Equal(sourceContent, Content(_source.HomeInstallation));
        LicenseKit.AssertZeroed(console);
        Assert.False(string.IsNullOrEmpty(KeyStringOf(console)));
    }

    // --- Folder at the startup offer

    [Fact]
    public void RestoreAtTheOffer_CanChooseTheCurrentFolder_AndCreatesNothingInHome()
    {
        var zip = Backup;

        var console = Run(_target, OfferRestore(zip, folder: "2").Line("0"));

        Assert.Contains($"Using {FullOf(_target.Cwd)}", console.Out);
        Assert.True(Installation.Holds(_target.Cwd));
        Assert.False(Directory.Exists(_target.HomeInstallation));
    }

    [Fact]
    public void RestoreAtTheOffer_WhenTheCurrentFolderIsHome_OffersOnlyTheHomeInstallationFolder()
    {
        var zip = Backup;
        _target.Cwd = _target.Home;

        var console = Run(_target, OfferRestore(zip).Line("0"));

        Assert.DoesNotContain("(the current folder)", console.Out);
        Assert.Contains($"Using {FullOf(_target.HomeInstallation)}", console.Out);
        Assert.Equal([".ucl"], Directory.GetFileSystemEntries(_target.Home).Select(Path.GetFileName));
    }

    // --- Folder already holds an installation

    [Fact]
    public void FolderAlreadyHoldsAnInstallation_AtTheOffer_IsRefusedSayingToMoveIt_AndNothingChanges()
    {
        var zip = Backup;
        string? bytesBefore = null;
        // An installation appears in ~/.ucl after the offer was made (so the folder question proposes a folder that now holds one).
        var offer = new Startup.Offer(Startup.Offer.Stubs.SetUp, s =>
        {
            SessionTestHost.MakeInstallation(_target.HomeInstallation);
            bytesBefore = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_target.HomeInstallation, "ucl.db")));
            return RestoreBackup.Run(s);
        });
        var home = FullOf(_target.HomeInstallation);

        var console = Run(_target, Script("2", zip, "", "0"), offer: offer);

        Assert.Contains(RestoreBackup.HoldsInstallationMessage(home), console.Out);
        Assert.Contains("Move or rename that folder first", console.Out);
        Assert.DoesNotContain("Restore this backup?", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "What now?")); // the offer again
        Assert.Equal(bytesBefore, Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_target.HomeInstallation, "ucl.db"))));
        Assert.Equal(["ucl.db"], Directory.GetFiles(_target.HomeInstallation).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("backup")]
    [InlineData("settings")]
    public void FolderAlreadyHoldsAnInstallation_FromBackupAndSettings_IsRefusedBeforeAnyQuestion_AndNothingChanges(string from)
    {
        _ = Backup;
        SessionTestHost.MakeInstallation(_target.Cwd, passphrase: "another passphrase entirely");
        var before = Content(_target.Cwd);
        var cwd = FullOf(_target.Cwd);

        var console = from == "backup"
            ? Script("4", "2", "0", "0")
            : Script("5", "4", "2", "0", "0", "0");
        Run(_target, console);

        Assert.Contains(RestoreBackup.HoldsInstallationMessage(cwd), console.Out);
        Assert.DoesNotContain("Backup file to restore", console.Out);
        Assert.Equal(before, Content(cwd));
        Assert.Equal(["ucl.db"], Directory.GetFiles(_target.Cwd).Select(Path.GetFileName));
        Assert.False(Directory.Exists(_target.HomeInstallation));
    }

    [Theory]
    [InlineData("backup")]
    [InlineData("settings")]
    public void RestoreInTheHomeFolder_IsRefusedBeforeAnyQuestion_AndNothingChanges(string from)
    {
        _ = Backup;
        SessionTestHost.MakeInstallation(_target.HomeInstallation);
        _target.Cwd = _target.Home;

        var console = from == "backup" ? Script("4", "2", "0", "0") : Script("5", "4", "2", "0", "0", "0");
        Run(_target, console);

        Assert.Contains(SettingsActions.HomeFolderRefused, console.Out);
        Assert.DoesNotContain("Backup file to restore", console.Out);
        Assert.Equal([".ucl"], Directory.GetFileSystemEntries(_target.Home).Select(Path.GetFileName));
    }

    // --- Not a backup

    [Theory]
    [InlineData("not-a-zip")]
    [InlineData("no-manifest")]
    [InlineData("unparseable-manifest")]
    [InlineData("manifest-not-an-object")]
    [InlineData("unknown-format")]
    [InlineData("no-database")]
    [InlineData("version-not-semver")]
    [InlineData("no-version")]
    [InlineData("no-last-migration")]
    [InlineData("no-taken")]
    [InlineData("oversized-manifest")]
    [InlineData("lying-manifest-header")]
    [InlineData("taken-without-z")]
    [InlineData("taken-with-offset")]
    public void NotABackup_IsRefusedSayingSo_NothingIsCreated_AndTheOfferIsShownAgain(string kind)
    {
        var migration = RestoreBackup.KnownMigrations()[^1];
        var db = Bytes("not read");
        var zip = kind switch
        {
            "not-a-zip" => Path.Combine(_target.Cwd, "x.zip").Also(p => File.WriteAllText(p, "this is text")),
            "no-manifest" => Craft("x.zip", ("ucl.db", db)),
            "unparseable-manifest" => Craft("x.zip", ("manifest.json", Bytes("{ not json")), ("ucl.db", db)),
            "manifest-not-an-object" => Craft("x.zip", ("manifest.json", Bytes("[1,2]")), ("ucl.db", db)),
            "unknown-format" => Craft("x.zip", ("manifest.json", Bytes(Manifest(format: 2, migration: migration))), ("ucl.db", db)),
            "no-database" => Craft("x.zip", ("manifest.json", Bytes(Manifest(migration: migration)))),
            "version-not-semver" => Craft("x.zip", ("manifest.json", Bytes(Manifest(toolVersion: "1.0", migration: migration))), ("ucl.db", db)),
            "no-version" => Craft("x.zip", ("manifest.json", Bytes($$"""{"format":1,"lastMigration":"{{migration}}","takenUtc":"2026-11-08T09:30:15Z"}""")), ("ucl.db", db)),
            "no-last-migration" => Craft("x.zip", ("manifest.json", Bytes("""{"format":1,"toolVersion":"0.1.0","takenUtc":"2026-11-08T09:30:15Z"}""")), ("ucl.db", db)),
            "no-taken" => Craft("x.zip", ("manifest.json", Bytes($$"""{"format":1,"toolVersion":"0.1.0","lastMigration":"{{migration}}"}""")), ("ucl.db", db)),
            "oversized-manifest" => Craft("x.zip", ("manifest.json", new byte[200 * 1024]), ("ucl.db", db)),
            "lying-manifest-header" => LyingHeader(Craft("x.zip", ("manifest.json", Bytes(Manifest(migration: migration) + new string(' ', 200 * 1024))), ("ucl.db", db))),
            "taken-without-z" => Craft("x.zip", ("manifest.json", Bytes(Manifest(migration: migration, taken: "2026-11-08T09:30:15"))), ("ucl.db", db)),
            _ => Craft("x.zip", ("manifest.json", Bytes(Manifest(migration: migration, taken: "2026-11-08T09:30:15+02:00"))), ("ucl.db", db)),
        };
        var entriesBefore = Directory.GetFileSystemEntries(_target.Cwd).Order().ToList();

        var console = Run(_target, Script("2", zip, "0"));

        Assert.Contains($"{FullOf(zip)} is not a backup made by ucl. Nothing is created.", console.Out);
        Assert.DoesNotContain("Folder for the installation", console.Out);
        Assert.DoesNotContain("Restore this backup?", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "What now?"));
        Assert.False(Directory.Exists(_target.HomeInstallation));
        Assert.Equal(entriesBefore, Directory.GetFileSystemEntries(_target.Cwd).Order().ToList());
    }

    [Fact]
    public void NotABackup_FromTheBackupMenu_ReturnsToTheBackupMenu_AndTheCurrentFolderIsUnchanged()
    {
        _target.SetUpInstallation();
        var zip = Craft("x.zip", ("ucl.db", Bytes("x")));

        var console = Run(_target, Script("4", "2", zip, "0", "0"));

        Assert.Contains("is not a backup made by ucl", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "  2 Restore into the current folder")); // the Backup menu again
        Assert.False(Installation.Holds(_target.Cwd));
    }

    [Fact]
    public void ADatabaseThatFailsTheIntegrityCheck_IsRefusedAsNotComplete_AndNothingIsLeftBehind()
    {
        var zip = Backup;
        var damaged = Path.Combine(_target.Cwd, "damaged.zip");
        File.Copy(zip, damaged);
        using (var archive = ZipFile.Open(damaged, ZipArchiveMode.Update))
        {
            archive.GetEntry("ucl.db")!.Delete();
            using var stream = archive.CreateEntry("ucl.db").Open();
            stream.Write(Bytes("this is not a database, just text that is long enough to look like a file"));
        }

        var entriesBefore = Directory.GetFileSystemEntries(_target.Cwd).Order().ToList();
        var console = Run(_target, OfferRestore(damaged).Line("0"));

        Assert.Contains($"{FullOf(damaged)} is not a complete backup", console.Out);
        Assert.False(Directory.Exists(_target.HomeInstallation));
        Assert.Equal(entriesBefore, Directory.GetFileSystemEntries(_target.Cwd).Order().ToList());
    }

    [Fact]
    public void ASqliteFileThatIsNotAUclDatabase_PassesTheIntegrityCheckButIsRefusedOnOpening_AndNothingIsLeftBehind()
    {
        // A valid SQLite file with a forged manifest: restore makes it, the session cannot use it, and the undo removes it.
        var foreign = Path.Combine(_target.Cwd, "foreign.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={foreign};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Other (Id INTEGER PRIMARY KEY); INSERT INTO Other VALUES (1);";
            command.ExecuteNonQuery();
        }

        var zip = Craft("forged.zip", ("manifest.json", Bytes(Manifest(migration: RestoreBackup.KnownMigrations()[^1]))), ("ucl.db", File.ReadAllBytes(foreign)));
        File.Delete(foreign);
        var entriesBefore = Directory.GetFileSystemEntries(_target.Cwd).Order().ToList();

        var console = Run(_target, OfferRestore(zip).Line("0"));

        Assert.Contains("failed:", console.Out);
        Assert.Contains("ucl cannot open the database", console.Out);
        Assert.DoesNotContain("Using ", console.Out);
        Assert.False(Directory.Exists(_target.HomeInstallation));
        Assert.Equal(entriesBefore, Directory.GetFileSystemEntries(_target.Cwd).Order().ToList());
    }

    [Fact]
    public void ADamagedDatabaseInTheCurrentFolderRestore_LeavesTheFolderOtherFilesAlone_AndNoTempFile()
    {
        var zip = Backup;
        var damaged = Path.Combine(_target.Cwd, "damaged.zip");
        File.Copy(zip, damaged);
        using (var archive = ZipFile.Open(damaged, ZipArchiveMode.Update))
        {
            archive.GetEntry("ucl.db")!.Delete();
            using var stream = archive.CreateEntry("ucl.db").Open();
            stream.Write(Bytes("garbage garbage garbage garbage garbage garbage garbage garbage"));
        }

        _target.SetUpInstallation(); // an installation in use, in ~/.ucl
        var console = Run(_target, Script("4", "2", damaged, "y", "0", "0"));

        Assert.Contains("is not a complete backup", console.Out);
        Assert.Equal(["damaged.zip"], Directory.GetFileSystemEntries(_target.Cwd).Select(Path.GetFileName));
    }

    [Fact]
    public void OtherEntriesInTheZip_AreIgnored_AndNothingIsExtractedByAnEntrysOwnPath()
    {
        var zip = Backup;
        var withExtras = Path.Combine(_target.Cwd, "extras.zip");
        File.Copy(zip, withExtras);
        using (var archive = ZipFile.Open(withExtras, ZipArchiveMode.Update))
        {
            using (var stream = archive.CreateEntry("../escaped.txt").Open())
                stream.Write(Bytes("x"));
            using (var stream = archive.CreateEntry("sub/ucl.db").Open())
                stream.Write(Bytes("y"));
        }

        var console = Run(_target, OfferRestore(withExtras).Line("0"));

        Assert.Contains($"Using {FullOf(_target.HomeInstallation)}", console.Out);
        Assert.False(File.Exists(Path.Combine(_target.Cwd, "..", "escaped.txt")));
        Assert.False(File.Exists(Path.Combine(_target.HomeInstallation, "escaped.txt")));
        Assert.Equal(["ucl.db"], Directory.GetFiles(_target.HomeInstallation).Select(Path.GetFileName));
        Assert.Equal(Content(_source.HomeInstallation), Content(_target.HomeInstallation));
    }

    // --- Backup from a newer tool version

    [Fact]
    public void BackupFromANewerToolVersion_AKnownLastMigration_IsRefused_NamingBothVersions_AndNothingIsCreated()
    {
        var newer = Path.Combine(_target.Cwd, "newer.zip");
        File.Copy(Backup, newer);
        Rewrite(newer, m => m["toolVersion"] = "99.0.0");

        var console = Run(_target, Script("2", newer, "0"));

        Assert.Contains($"{FullOf(newer)} was taken by ucl 99.0.0, a newer version than the {ToolApp.Version} running. Update ucl to restore it.", console.Out);
        Assert.DoesNotContain("Restore this backup?", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "What now?"));
        Assert.False(Directory.Exists(_target.HomeInstallation));
    }

    [Fact]
    public void BackupFromANewerToolVersion_AnUnknownLastMigration_IsRefused_NamingBothVersions_AndNothingIsCreated()
    {
        var newer = Path.Combine(_target.Cwd, "newer.zip");
        File.Copy(Backup, newer);
        // The same version twice is faithful to ADR-0023: an unknown last migration says the backup is from a newer tool, and the backup's own version is all there is to name.
        Rewrite(newer, m => m["lastMigration"] = "99991231235959_FromTheFuture");

        var console = Run(_target, Script("2", newer, "0"));

        Assert.Contains($"was taken by ucl {ToolApp.Version}, a newer version than the {ToolApp.Version} running. Update ucl to restore it.", console.Out);
        Assert.False(Directory.Exists(_target.HomeInstallation));
    }

    [Fact]
    public void BackupFromANewerVersion_InAnotherEntryPoint_ReturnsToTheMenuItWasStartedFrom()
    {
        _target.SetUpInstallation();
        var newer = Path.Combine(_target.Cwd, "newer.zip");
        File.Copy(Backup, newer);
        Rewrite(newer, m => m["toolVersion"] = "0.1.0");

        var console = Run(_target, Script("5", "4", "2", newer, "0", "0", "0"));

        Assert.Contains("a newer version than the", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "  2 Restore from a backup")); // the New installation submenu again
        Assert.False(Installation.Holds(_target.Cwd));
    }

    [Theory]
    [InlineData("0.0.1")]
    [InlineData("0.1.0-beta.6")]
    [InlineData("0.1.0-alpha.99+other.build")]
    public void BackupFromAnOlderToolVersion_IsRestored(string version)
    {
        var older = Path.Combine(_target.Cwd, "older.zip");
        File.Copy(Backup, older);
        Rewrite(older, m => m["toolVersion"] = version);

        var console = Run(_target, OfferRestore(older).Line("0"));

        Assert.Contains($"Using {FullOf(_target.HomeInstallation)}", console.Out);
    }

    // --- Restore declined

    [Fact]
    public void RestoreDeclined_FromTheStartupOffer_CreatesNothing_AndTheOfferIsShownAgain()
    {
        var zip = Backup;
        var entriesBefore = Directory.GetFileSystemEntries(_target.Cwd).Order().ToList();

        var console = Run(_target, OfferRestore(zip, answer: "").Line("0")); // Enter takes the proposed no

        Assert.Contains("Restore this backup? [y/N]", console.Out);
        Assert.Contains(RestoreBackup.Declined, console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "What now?"));
        Assert.DoesNotContain("Using ", console.Out);
        Assert.False(Directory.Exists(_target.HomeInstallation));
        Assert.Equal(entriesBefore, Directory.GetFileSystemEntries(_target.Cwd).Order().ToList());
    }

    [Theory]
    [InlineData("n")]
    [InlineData("/cancel")]
    public void RestoreDeclined_AtTheOffer_NoOrCancel_CreatesNothing(string answer)
    {
        var zip = Backup;

        var console = Run(_target, OfferRestore(zip, folder: "2", answer: answer).Line("0"));

        Assert.Equal(2, SessionTestHost.Count(console.Out, "What now?"));
        Assert.False(Installation.Holds(_target.Cwd));
        Assert.False(Directory.Exists(_target.HomeInstallation));
    }

    [Theory]
    [InlineData("n")]
    [InlineData("/cancel")]
    public void RestoreDeclined_FromBackup_LeavesTheCurrentFolderUnchanged_AndShowsTheBackupMenu(string answer)
    {
        var zip = Backup;
        var home = _target.SetUpInstallation();
        var before = Content(home);

        var console = Run(_target, Script("4", "2", zip, answer, "0", "0"));

        Assert.Equal(2, SessionTestHost.Count(console.Out, "  2 Restore into the current folder"));
        Assert.Equal(1, SessionTestHost.Count(console.Out, $"Using {home}")); // not moved
        Assert.False(Installation.Holds(_target.Cwd));
        Assert.Empty(Directory.GetFileSystemEntries(_target.Cwd));
        Assert.Equal(before, Content(home));
    }

    // --- Into the current folder from Backup and Settings

    [Theory]
    [InlineData("backup")]
    [InlineData("settings")]
    public void RestoreIntoTheCurrentFolder_FromBackupAndFromSettings_AsksNoFolder_ShowsTheDate_AndContinuesWithIt(string from)
    {
        var zip = Backup;
        var home = _target.SetUpInstallation();
        var homeBefore = Content(home);
        var cwd = FullOf(_target.Cwd);

        var console = from == "backup"
            ? Script("4", "2", zip, "y", "0", "0")
            : Script("5", "4", "2", zip, "y", "0", "0", "0");
        Run(_target, console);

        Assert.DoesNotContain("Folder for the installation", console.Out);
        Assert.Contains("This backup was taken on 2026-11-08 09:30:15 UTC.", console.Out);
        Assert.Contains($"Using {cwd}", console.Out);
        Assert.Contains(Startup.UnusedNote(home), console.Out);
        Assert.Equal(Content(_source.HomeInstallation), Content(cwd));
        Assert.Equal(homeBefore, Content(home));
        Assert.Equal(["ucl.db"], Directory.GetFiles(_target.Cwd).Select(Path.GetFileName));
    }

    [Fact]
    public void RestoreIntoTheCurrentFolder_LeavesTheFoldersOtherFilesAlone()
    {
        var zip = Backup;
        _target.SetUpInstallation();
        File.WriteAllText(Path.Combine(_target.Cwd, "notes.txt"), "mine");

        Run(_target, Script("4", "2", zip, "y", "0", "0"));

        Assert.Equal("mine", File.ReadAllText(Path.Combine(_target.Cwd, "notes.txt")));
        Assert.Equal(["notes.txt", "ucl.db"], Directory.GetFiles(_target.Cwd).Select(Path.GetFileName).Order());
    }

    // --- Passphrase and idle time of the restored installation

    [Fact]
    public void RestoredInstallationAsksForItsPassphrase_FromSettings_AfterAnIssueInHome_AndUsesItsOwnIdleTime()
    {
        var zip = MakeBackup(idle: "2"); // a different installation: idle lock 2 minutes, its own passphrase

        // The current folder is not home; ~/.ucl holds an installation with a product and an idle lock of 15 minutes.
        using var kit = new LicenseKit(shared: false);
        var clock = ClockAt(Utc(2026, 11, 8, 9, 0));
        var console = new ScriptedConsole(clock: clock);

        // An issue in ~/.ucl: the passphrase is asked once and held.
        console.Line("2").Line("1").Line("1").Line(Expiry).Line("").Line("").Line("on").Line("500").Line("").Line("").Line("ORD-A").Line("a@example.com")
            .Line("").Hidden(Words).Line("").Line("0");
        // Settings > New installation in the current folder > Restore from a backup, then back to the main menu.
        console.Line("5").Line("4").Line("2").Line(zip).Line("y").Line("0").Line("0");
        // The next issue asks again: the earlier passphrase is wrong, the backup's works.
        Issue(console, "ORD-B", Words, Pass);
        // One minute on: the restored installation's 2 minute idle lock has not passed, so no passphrase is asked.
        Issue(console, "ORD-C", TimeSpan.FromMinutes(1));
        // Three minutes on: past 2 minutes, well within the 15 of the installation left, so it is asked.
        Issue(console, "ORD-D", Pass, TimeSpan.FromMinutes(3));
        console.Line("0");

        var exit = kit.Host.Run(console, offer: Startup.Offer.Default, clock: clock);

        Assert.True(exit == 0, console.Err + console.Out);
        var cwd = FullOf(kit.Host.Cwd);
        Assert.Contains($"Using {cwd}", console.Out);
        Assert.Equal(1, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
        Assert.Equal(4, console.HiddenGiven.Count); // Words in home; Words (wrong) and Pass after the restore; Pass after the idle lock
        LicenseKit.AssertZeroed(console);
        Assert.Equal(1, Entries(kit.Host.HomeInstallation)); // ORD-A
        Assert.Equal(1 + 3, Entries(cwd)); // the backup's license and ORD-B, ORD-C, ORD-D
    }
}

file static class Extensions
{
    public static string Also(this string value, Action<string> action)
    {
        action(value);
        return value;
    }
}
