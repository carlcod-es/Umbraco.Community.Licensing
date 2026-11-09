using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// An installation: the folder holding <c>ucl.db</c> (ADR-0013). <see cref="Open"/> is the one place a database is
/// opened, checked and migrated. The object holds no connection; each action takes its own short-lived
/// <see cref="ToolDbContext"/> from <see cref="OpenDb"/> and disposes it, so a failed action leaves nothing open.
/// </summary>
internal sealed class Installation
{
    private readonly DbContextOptions<ToolDbContext> _options;

    private Installation(string folder, DbContextOptions<ToolDbContext> options)
    {
        Folder = folder;
        _options = options;
    }

    /// <summary>The installation folder as a full path.</summary>
    public string Folder { get; }

    /// <summary>
    /// Opens the database in <paramref name="folder"/> (creating the folder and database when missing, which setup
    /// relies on), refuses one holding a migration this tool does not know, and applies pending migrations, each in a
    /// transaction. Throws <see cref="InstallationRefusedException"/> when the database cannot be used.
    /// </summary>
    public static Installation Open(string folder)
    {
        var full = Path.GetFullPath(folder);
        var options = new DbContextOptionsBuilder<ToolDbContext>()
            .UseSqlite(ConnectionString(full))
            .AddInterceptors(SqlitePragmas.Instance)
            .Options;

        try
        {
            Directory.CreateDirectory(full);
            using var db = new ToolDbContext(options);

            var known = db.Database.GetMigrations().ToHashSet();
            var unknown = db.Database.GetAppliedMigrations().Where(m => !known.Contains(m)).ToList();
            if (unknown.Count > 0)
                throw new InstallationRefusedException(
                    $"The installation in {full} was made or changed by a newer version of ucl and cannot be opened by this one. Please update the tool.");

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

        if (value is not null && value.Equals("never", StringComparison.OrdinalIgnoreCase))
            return null;
        if (value is not null && value.All(char.IsAsciiDigit)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes is >= 1 and <= 1440)
            return TimeSpan.FromMinutes(minutes);

        throw new InstallationRefusedException(
            $"The idle lock setting in {Folder} is missing or invalid, so the installation looks damaged. Restore it from a backup.");
    }

    private static string ConnectionString(string folder) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(folder, Startup.DatabaseFileName),
            Mode = SqliteOpenMode.ReadWriteCreate,
            // No pooling: a closed context releases the file at once (Windows cannot delete or copy an open file).
            Pooling = false,
            ForeignKeys = true,
        }.ToString();
}
