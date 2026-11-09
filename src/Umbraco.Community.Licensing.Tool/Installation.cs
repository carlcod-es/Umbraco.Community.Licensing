using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// An installation: the folder holding <c>ucl.db</c> (ADR-0013). <see cref="OpenExisting"/> opens, checks and migrates a
/// database that is there; <see cref="Create"/> makes a new one and only setup calls it. The object holds no connection;
/// each action takes its own short-lived <see cref="ToolDbContext"/> from <see cref="OpenDb"/> and disposes it, so a
/// failed action leaves nothing open.
/// </summary>
internal sealed class Installation
{
    private static readonly string[] DatabaseFiles =
        [Startup.DatabaseFileName, Startup.DatabaseFileName + "-journal", Startup.DatabaseFileName + "-wal", Startup.DatabaseFileName + "-shm"];

    private readonly DbContextOptions<ToolDbContext> _options;
    private Action? _undoCreate;

    private Installation(string folder, DbContextOptions<ToolDbContext> options)
    {
        Folder = folder;
        _options = options;
    }

    /// <summary>The installation folder as a full path.</summary>
    public string Folder { get; }

    /// <summary>True when <paramref name="folder"/> holds an installation: it has a <c>ucl.db</c>.</summary>
    public static bool Holds(string folder) => File.Exists(Path.Combine(folder, Startup.DatabaseFileName));

    /// <summary>
    /// Opens the database of an installation that is there (<see cref="InstallationRefusedException"/> when it is not),
    /// refuses a file that is not a ucl database or holds a migration this tool does not know, and applies pending
    /// migrations, each in a transaction; writes nothing when none is pending. Nothing is written to a file that is refused as foreign.
    /// </summary>
    public static Installation OpenExisting(string folder)
    {
        var full = Path.GetFullPath(folder);
        if (!Holds(full))
            throw new InstallationRefusedException($"There is no installation in {full}.");

        var options = Options(full);
        try
        {
            using var db = new ToolDbContext(options);

            var known = db.Database.GetMigrations().ToList();
            // Read the history with a plain read-only connection first: a 0-byte or foreign SQLite file must not get the schema.
            var applied = ReadAppliedMigrations(Path.Combine(full, Startup.DatabaseFileName));
            if (!applied.Contains(known[0]))
                throw new InvalidOperationException("Not a ucl database.");

            if (applied.Any(m => !known.Contains(m)))
                throw new InstallationRefusedException(
                    $"The installation in {full} was made or changed by a newer version of ucl and cannot be opened by this one. Please update the tool.");

            // Only when one is missing from the list read above: Migrate() writes to the file even with nothing to apply.
            if (known.Any(m => !applied.Contains(m)))
                db.Database.Migrate();
        }
        catch (InstallationRefusedException)
        {
            throw;
        }
        catch (Exception)
        {
            // No technical detail (PDR-0053); the vendor's next step is the same whatever the cause.
            throw new InstallationRefusedException(
                $"ucl cannot open the database in {full}. It may not be a ucl database, may be damaged, or may be in use by another program.");
        }

        return new Installation(full, options);
    }

    /// <summary>
    /// Creates the installation in <paramref name="folder"/>: the folder when missing (a folder with other files is
    /// accepted and its files left alone), the database and its schema, then <paramref name="populate"/>, whose
    /// <c>SaveChanges</c> is the one transaction writing the first rows. When anything fails, only what this call
    /// created is removed (the folders it made, else just <c>ucl.db</c> and its side files) and the exception goes on.
    /// Throws <see cref="InstallationExistsException"/>, changing nothing, when <c>ucl.db</c> is already there.
    /// <see cref="UndoCreate"/> removes the same afterwards. Only setup calls this.
    /// </summary>
    public static Installation Create(string folder, Action<ToolDbContext>? populate = null)
    {
        var full = Path.GetFullPath(folder);
        var firstCreated = FirstMissingFolder(full);
        var databasePath = Path.Combine(full, Startup.DatabaseFileName);
        var madeDatabase = false;
        void Undo() => RemoveCreated(full, firstCreated, removeDatabase: madeDatabase);

        try
        {
            Directory.CreateDirectory(full);
            try
            {
                // CreateNew is atomic: an installation made since the caller looked is never overwritten or removed.
                using (new FileStream(databasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }

                madeDatabase = true;
            }
            catch (IOException) when (File.Exists(databasePath))
            {
                throw new InstallationExistsException(full);
            }

            var options = Options(full);
            using (var db = new ToolDbContext(options))
            {
                db.Database.Migrate();
                populate?.Invoke(db);
            }

            return new Installation(full, options) { _undoCreate = Undo };
        }
        catch
        {
            Undo();
            throw;
        }
    }

    /// <summary>Removes what <see cref="Create"/> made, when startup cannot use the new installation. No effect on any other installation.</summary>
    public void UndoCreate() => _undoCreate?.Invoke();

    /// <summary>
    /// A new context on the installation's database, for one action. Dispose it (<c>using</c>) when the action
    /// ends, whether it completed or failed. Every connection it opens has the pragmas of <see cref="SqlitePragmas"/>.
    /// </summary>
    public ToolDbContext OpenDb() => new(_options);

    /// <summary>
    /// Reads the idle lock setting (null is <c>never</c>). A missing or malformed value means a damaged
    /// installation (setup always writes it) and is refused like a database that cannot be opened.
    /// </summary>
    public TimeSpan? ReadIdleTime()
    {
        string? value;
        try
        {
            using var db = OpenDb();
            value = db.Settings.AsNoTracking().Where(s => s.Name == SettingNames.IdleLock).Select(s => s.Value).SingleOrDefault();
        }
        catch (Exception)
        {
            throw new InstallationRefusedException($"ucl cannot read the settings in {Folder}.");
        }

        if (value is not null && TypedValues.TryParseIdleLock(value, out var idleTime))
            return idleTime;

        throw new InstallationRefusedException(DamagedIdleLockMessage(Folder));
    }

    /// <summary>The refusal for a missing or invalid idle lock setting; a backup cannot be restored in place, so it names another folder.</summary>
    internal static string DamagedIdleLockMessage(string? folder) =>
        $"The idle lock setting in {folder} is missing or invalid, so the installation looks damaged. Restore a backup into another folder.";

    private static List<string> ReadAppliedMigrations(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory";
        using var reader = command.ExecuteReader();
        var migrations = new List<string>();
        while (reader.Read())
            migrations.Add(reader.GetString(0));
        return migrations;
    }

    /// <summary>The outermost folder on the way to <paramref name="folder"/> that does not exist yet, or null when it does.</summary>
    private static string? FirstMissingFolder(string folder)
    {
        string? missing = null;
        for (var current = folder; current is not null && !Directory.Exists(current); current = Path.GetDirectoryName(current))
            missing = current;
        return missing;
    }

    private static void RemoveCreated(string folder, string? firstCreated, bool removeDatabase)
    {
        try
        {
            if (removeDatabase)
            {
                foreach (var name in DatabaseFiles)
                    File.Delete(Path.Combine(folder, name));
            }

            // Folders this setup made, innermost first; one that holds anything else stays.
            for (var current = firstCreated is null ? null : folder; current is not null; current = Path.GetDirectoryName(current))
            {
                Directory.Delete(current);
                if (string.Equals(current, firstCreated, StringComparison.Ordinal))
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the failure being reported is the one that matters.
        }
    }

    private static DbContextOptions<ToolDbContext> Options(string folder) =>
        new DbContextOptionsBuilder<ToolDbContext>()
            .UseSqlite(ConnectionString(folder))
            .AddInterceptors(SqlitePragmas.Instance)
            .Options;

    private static string ConnectionString(string folder) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(folder, Startup.DatabaseFileName),
            Mode = SqliteOpenMode.ReadWrite,
            // No pooling: a closed context releases the file at once (Windows cannot delete or copy an open file).
            Pooling = false,
            ForeignKeys = true,
        }.ToString();
}
