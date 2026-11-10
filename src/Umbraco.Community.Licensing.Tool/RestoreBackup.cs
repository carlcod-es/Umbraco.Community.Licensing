using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Restore from a backup (vendor-tool-backup: Backup import; ADR-0015, ADR-0023, PDR-0057). Asks for the backup, checks it, then
/// (at the startup offer only) the folder, shows the date and the warning, asks whether to restore, and only then makes the
/// installation. A refusal or a no ends the restore with nothing created and returns null to where it was started. The caller
/// moves the session to the result (<see cref="Startup.UseProvisioned"/>), which opens the database and applies pending migrations.
/// </summary>
internal static class RestoreBackup
{
    private const string TempSuffix = ".restore-tmp";
    private const long ManifestLimit = 64 * 1024;

    internal static string NotABackupMessage(string file) =>
        $"{file} is not a backup made by ucl. Nothing is created.";

    internal static string NotCompleteMessage(string file) =>
        $"{file} is not a complete backup: its database is damaged. Nothing is created.";

    internal static string NewerToolMessage(string file, string backupVersion, string runningVersion) =>
        $"{file} was taken by ucl {backupVersion}, a newer version than the {runningVersion} running. Update ucl to restore it. Nothing is created.";

    internal static string HoldsInstallationMessage(string folder) =>
        $"{folder} already holds an installation. Move or rename that folder first. Nothing changes.";

    internal const string Warning = "Licenses issued after that date are not in the backup.";

    internal const string Declined = "Nothing was restored.";

    /// <summary>A refusal that ends the restore: the message is shown as it is and nothing stays made.</summary>
    private sealed class RefusedException(string message) : Exception(message);

    /// <summary>
    /// Runs restore. <paramref name="fixedFolder"/> is the only folder when given (Backup menu, Settings: the current folder, whose
    /// refusals the caller has already made): the folder question is skipped. Returns the new installation's folder with the undo
    /// for it, or null when restore ended without making one. <c>/cancel</c> throws <see cref="ActionCancelledException"/>.
    /// </summary>
    public static Provisioned? Run(Session session, string? fixedFolder = null) =>
        Run(session, fixedFolder, ToolApp.Version, KnownMigrations());

    /// <summary>The same with the running version and the tool's migrations given, so tests can stand in for another tool.</summary>
    internal static Provisioned? Run(Session session, string? fixedFolder, string runningVersion, IReadOnlyCollection<string> knownMigrations)
    {
        var console = session.Console;
        var file = new NamedFiles(session.Environment, session.Questions).AskImportPath("Backup file to restore");

        BackupManifest manifest;
        try
        {
            manifest = ReadManifest(file, runningVersion, knownMigrations);
        }
        catch (RefusedException ex)
        {
            console.WriteLine(ex.Message);
            return null;
        }

        var folder = Path.GetFullPath(fixedFolder ?? FolderOffer.Ask(session));
        if (Installation.Holds(folder))
        {
            console.WriteLine(HoldsInstallationMessage(folder));
            return null;
        }

        console.WriteLine($"This backup was taken on {BackupFormat.Describe(manifest.TakenUtc)}.");
        console.WriteLine(Warning);
        console.WriteLine($"It will be restored into {folder}.");
        if (!session.Questions.AskYesNo("Restore this backup?", proposed: false))
        {
            console.WriteLine(Declined);
            return null;
        }

        try
        {
            return Import(file, folder, runningVersion, knownMigrations);
        }
        catch (RefusedException ex)
        {
            console.WriteLine(ex.Message);
            return null;
        }
    }

    /// <summary>The migrations this tool knows, in order.</summary>
    internal static IReadOnlyList<string> KnownMigrations()
    {
        using var db = new ToolDbContext(new DbContextOptionsBuilder<ToolDbContext>().UseSqlite("Data Source=:memory:").Options);
        return db.Database.GetMigrations().ToList();
    }

    /// <summary>
    /// True when <paramref name="backupVersion"/> has higher SemVer precedence than <paramref name="runningVersion"/> (ADR-0023).
    /// A running version that is not SemVer cannot be compared, so only the migration check applies then.
    /// </summary>
    internal static bool IsNewer(SemVer backupVersion, string runningVersion) =>
        SemVer.TryParse(runningVersion, out var running) && backupVersion.CompareTo(running) > 0;

    /// <summary>
    /// Reads and checks the manifest, looking only at the two named entries; the zip's other entries are ignored and no entry is
    /// extracted by its own path. Anything that is not a backup, or is from a newer tool, is a <see cref="RefusedException"/>.
    /// </summary>
    private static BackupManifest ReadManifest(string file, string runningVersion, IReadOnlyCollection<string> knownMigrations)
    {
        using var zip = OpenZip(file);
        var manifestEntry = zip.GetEntry(BackupFormat.ManifestEntry);
        if (manifestEntry is null || zip.GetEntry(BackupFormat.DatabaseEntry) is null || manifestEntry.Length > ManifestLimit)
            throw new RefusedException(NotABackupMessage(file));

        BackupManifest? manifest;
        try
        {
            using var stream = manifestEntry.Open();
            manifest = JsonSerializer.Deserialize<BackupManifest>(stream);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            throw new RefusedException(NotABackupMessage(file));
        }

        if (manifest is null
            || manifest.Format != BackupFormat.Version
            || string.IsNullOrEmpty(manifest.LastMigration)
            || manifest.TakenUtc == default
            || !SemVer.TryParse(manifest.ToolVersion, out var backupVersion))
            throw new RefusedException(NotABackupMessage(file));

        if (IsNewer(backupVersion, runningVersion) || !knownMigrations.Contains(manifest.LastMigration))
            throw new RefusedException(NewerToolMessage(file, manifest.ToolVersion, runningVersion));

        return manifest;
    }

    private static ZipArchive OpenZip(string file)
    {
        try
        {
            return ZipFile.OpenRead(file);
        }
        catch (InvalidDataException)
        {
            throw new RefusedException(NotABackupMessage(file));
        }
    }

    /// <summary>
    /// Makes the installation: extracts <c>ucl.db</c> to a temp name in the folder, checks it, and moves it to <c>ucl.db</c> without
    /// overwriting. The folder is created only when missing (the home <c>.ucl</c>, as setup does). Everything made is removed on any
    /// failure or refusal; a chosen folder's other files are left alone.
    /// </summary>
    private static Provisioned Import(string file, string folder, string runningVersion, IReadOnlyCollection<string> knownMigrations)
    {
        var firstCreated = Installation.FirstMissingFolder(folder);
        var databasePath = Path.Combine(folder, Startup.DatabaseFileName);
        var temp = Path.Combine(folder, $"{Startup.DatabaseFileName}.{Guid.NewGuid():N}{TempSuffix}");
        var madeDatabase = false;
        var undo = () => Installation.RemoveCreated(folder, firstCreated, removeDatabase: madeDatabase);

        try
        {
            Directory.CreateDirectory(folder);

            // The zip is read again: what is extracted is what was checked just now.
            ReadManifest(file, runningVersion, knownMigrations);
            using (var zip = OpenZip(file))
            {
                var entry = zip.GetEntry(BackupFormat.DatabaseEntry) ?? throw new RefusedException(NotABackupMessage(file));
                try
                {
                    using var source = entry.Open();
                    using var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    source.CopyTo(target);
                }
                catch (InvalidDataException)
                {
                    throw new RefusedException(NotCompleteMessage(file));
                }
            }

            if (!IntegrityOk(temp))
                throw new RefusedException(NotCompleteMessage(file));

            try
            {
                // Not overwriting: an installation made here meanwhile is left as it is.
                File.Move(temp, databasePath, overwrite: false);
            }
            catch (IOException) when (File.Exists(databasePath))
            {
                File.Delete(temp);
                undo = () => Installation.RemoveCreated(folder, firstCreated, removeDatabase: false);
                throw new RefusedException(HoldsInstallationMessage(folder));
            }

            madeDatabase = true;
            return new Provisioned(folder, () => undo());
        }
        catch
        {
            DeleteQuietly(temp);
            undo();
            throw;
        }
    }

    /// <summary><c>PRAGMA integrity_check</c> on the extracted file; a file SQLite cannot read counts as failing.</summary>
    private static bool IntegrityOk(string database)
    {
        try
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";
            using var reader = command.ExecuteReader();
            return reader.Read() && reader.GetString(0) == "ok" && !reader.Read();
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static void DeleteQuietly(string file)
    {
        try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
