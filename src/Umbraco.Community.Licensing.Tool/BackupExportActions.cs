using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Backup &gt; Export (vendor-tool-backup, ADR-0015): a zip of a consistent snapshot of <c>ucl.db</c> and a manifest, written to a file
/// the vendor names. Needs no passphrase and does not touch the held one; private keys stay as the encrypted PEMs the database holds.
/// </summary>
internal static class BackupExportActions
{
    private const string TempSuffix = ".backup-tmp";

    public static void Register(ActionTable table) => table.Register("backup.export", session => Export(session));

    /// <param name="afterSnapshot">Test seam: runs once the snapshot exists, before the zip is written, to force a failure there.</param>
    internal static void Export(Session session, Action? afterSnapshot = null)
    {
        var installation = session.Installation ?? throw new InvalidOperationException("No installation is in use.");
        var files = new NamedFiles(session.Environment, session.Questions);
        var takenUtc = default(DateTime);

        var path = files.AskAndWrite("File to back up to", stream => takenUtc = WriteBackup(installation, session, stream, afterSnapshot));

        session.Console.WriteLine($"Backup written to {path}, taken {BackupFormat.Describe(takenUtc)}.");
    }

    /// <summary>Writes the zip and returns the time the snapshot was taken.</summary>
    internal static DateTime WriteBackup(Installation installation, Session session, Stream output, Action? afterSnapshot = null)
    {
        // In the installation folder so nothing is written outside it (ADR-0013); never named ucl.db.
        var temp = Path.Combine(installation.Folder, $"{Startup.DatabaseFileName}.{Guid.NewGuid():N}{TempSuffix}");
        try
        {
            var takenUtc = session.Environment.TimeProvider.GetUtcNow().UtcDateTime;
            Snapshot(installation, temp);
            afterSnapshot?.Invoke();
            var manifest = new BackupManifest(BackupFormat.Version, ToolApp.Version, LastMigration(temp), takenUtc);

            using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            var manifestEntry = zip.CreateEntry(BackupFormat.ManifestEntry);
            using (var stream = manifestEntry.Open())
                JsonSerializer.Serialize(stream, manifest, BackupFormat.Json);
            var databaseEntry = zip.CreateEntry(BackupFormat.DatabaseEntry);
            using (var stream = databaseEntry.Open())
            using (var source = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read))
                source.CopyTo(stream);
            return takenUtc;
        }
        finally
        {
            DeleteQuietly(temp);
        }
    }

    /// <summary>A consistent copy of the live database in one statement, taken while no action runs. The connection is closed before this returns.</summary>
    private static void Snapshot(Installation installation, string target)
    {
        using var db = installation.OpenDb(); // opens with the tool's pragmas
        var connection = db.Database.GetDbConnection();
        connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"VACUUM INTO '{target.Replace("'", "''")}'";
            command.ExecuteNonQuery();
        }
        finally
        {
            connection.Close();
        }
    }

    private static string LastMigration(string snapshot)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = snapshot, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1";
        return (string?)command.ExecuteScalar() ?? throw new InvalidOperationException("The snapshot has no migration history.");
    }

    private static void DeleteQuietly(string file)
    {
        foreach (var name in new[] { file, file + "-journal" })
        {
            try { File.Delete(name); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
