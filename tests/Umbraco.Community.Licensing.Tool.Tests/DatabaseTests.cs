using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class DatabaseTests
{
    private static readonly string[] Tables =
        ["Settings", "Products", "FeatureDefinitions", "SigningKeys", "Licenses", "IssueEntries"];

    private static string DbPath(TempFolder folder) => Path.Combine(folder.Path, Installation.DatabaseFileName);

    private static SqliteConnection Raw(TempFolder folder)
    {
        var connection = new SqliteConnection($"Data Source={DbPath(folder)};Pooling=False");
        connection.Open();
        return connection;
    }

    private static T Scalar<T>(SqliteConnection connection, string sql) =>
        (T)Convert.ChangeType(new SqliteCommand(sql, connection).ExecuteScalar()!, typeof(T));

    private static string[] Columns(SqliteConnection connection, string table)
    {
        using var command = new SqliteCommand($"SELECT name FROM pragma_table_info('{table}')", connection);
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return [.. names];
    }

    [Fact]
    public void A_new_folder_gets_the_schema()
    {
        using var folder = new TempFolder();

        Installation.Open(folder.Path).Dispose();

        Assert.True(File.Exists(DbPath(folder)));
        using var connection = Raw(folder);
        foreach (var table in Tables)
        {
            Assert.Equal(1, Scalar<int>(connection, $"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='{table}'"));
        }

        Assert.Equal(["Name", "Value"], Columns(connection, "Settings"));
        Assert.Equal(["ProductId", "DisplayName", "CreatedUtc"], Columns(connection, "Products"));
        Assert.Equal(["ProductId", "Name", "Type", "AddedUtc"], Columns(connection, "FeatureDefinitions"));
        Assert.Equal(
            ["SigningKeyId", "ProductId", "PublicKey", "EncryptedPrivateKeyPem", "Role", "CreatedUtc", "ReplacedUtc", "ReplacedReason", "InReleaseUtc", "ReissuesSentUtc"],
            Columns(connection, "SigningKeys"));
        Assert.Equal(["Reference", "ProductId", "CustomerEmail", "CreatedUtc"], Columns(connection, "Licenses"));
        Assert.Equal(
            ["Id", "Reference", "Kind", "IssuedUtc", "ExpiresUtc", "DisplayName", "VendorTag", "FeaturesJson", "OrderRef", "KeyIdentifier", "SigningKeyId"],
            Columns(connection, "IssueEntries"));
        Assert.Equal(1, Scalar<int>(connection, "SELECT count(*) FROM sqlite_master WHERE type='index' AND tbl_name='IssueEntries' AND sql LIKE '%OrderRef%'"));
    }

    [Fact]
    public void No_column_holds_a_key_string()
    {
        using var folder = new TempFolder();
        Installation.Open(folder.Path).Dispose();
        using var connection = Raw(folder);

        foreach (var table in Tables)
        {
            foreach (var column in Columns(connection, table))
            {
                // PublicKey (export line) and EncryptedPrivateKeyPem are the only key material; a key string is the issued license.
                Assert.DoesNotContain("KeyString", column, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("LicenseKey", column, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Pragmas_read_back_as_set_and_after_reopening()
    {
        using var folder = new TempFolder();

        for (var i = 0; i < 2; i++)
        {
            using var db = Installation.Open(folder.Path);
            var connection = db.Database.GetDbConnection();
            db.Database.OpenConnection();
            using var secure = connection.CreateCommand();
            secure.CommandText = "PRAGMA secure_delete";
            Assert.Equal(1L, secure.ExecuteScalar());
            using var journal = connection.CreateCommand();
            journal.CommandText = "PRAGMA journal_mode";
            Assert.Equal("delete", journal.ExecuteScalar());
        }
    }

    [Fact]
    public void Unknown_migration_is_refused_and_nothing_changes()
    {
        using var folder = new TempFolder();
        Installation.Open(folder.Path).Dispose();
        using (var connection = Raw(folder))
        {
            new SqliteCommand(
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29990101000000_FromTheFuture', '99.0.0')",
                connection).ExecuteNonQuery();
        }

        var before = File.ReadAllBytes(DbPath(folder));

        var refusal = Assert.Throws<ToolRefusal>(() => Installation.Open(folder.Path));

        Assert.Contains("update the tool", refusal.Message);
        Assert.Equal(before, File.ReadAllBytes(DbPath(folder)));
    }

    [Fact]
    public void Unknown_migration_on_a_database_that_also_lacks_known_ones_is_refused_before_migrating()
    {
        using var folder = new TempFolder();
        using (var connection = Raw(folder))
        {
            new SqliteCommand(
                "CREATE TABLE __EFMigrationsHistory (MigrationId TEXT NOT NULL PRIMARY KEY, ProductVersion TEXT NOT NULL);"
                + "INSERT INTO __EFMigrationsHistory VALUES ('29990101000000_FromTheFuture', '99.0.0')",
                connection).ExecuteNonQuery();
        }

        var refusal = Assert.Throws<ToolRefusal>(() => Installation.Open(folder.Path));

        Assert.Contains("update the tool", refusal.Message);
        using var check = Raw(folder);
        Assert.Equal(0, Scalar<int>(check, "SELECT count(*) FROM sqlite_master WHERE name='Settings'"));
    }

    [Fact]
    public void Reopen_applies_nothing_new_and_keeps_data()
    {
        using var folder = new TempFolder();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.FromHours(2)));
        using (var db = Installation.Open(folder.Path))
        {
            db.Products.Add(new Product { ProductId = "acme", DisplayName = "Acme", CreatedUtc = clock.GetUtcNow() });
            db.SaveChanges();
        }

        string[] historyBefore;
        using (var connection = Raw(folder))
        {
            historyBefore = ReadHistory(connection);
        }

        using (var db = Installation.Open(folder.Path))
        {
            var product = db.Products.Single();
            Assert.Equal("Acme", product.DisplayName);
            Assert.Equal(clock.GetUtcNow(), product.CreatedUtc);
            Assert.Equal(TimeSpan.Zero, product.CreatedUtc.Offset);
        }

        using var after = Raw(folder);
        Assert.Equal(historyBefore, ReadHistory(after));
        Assert.Equal("2026-10-29T10:00:00.0000000Z", Scalar<string>(after, "SELECT CreatedUtc FROM Products"));
    }

    private static string[] ReadHistory(SqliteConnection connection)
    {
        using var command = new SqliteCommand("SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId", connection);
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return [.. ids];
    }
}
