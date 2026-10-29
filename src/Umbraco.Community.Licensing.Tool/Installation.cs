using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The outcome of looking for an installation.
/// </summary>
/// <param name="Folder">The installation found (full path); null when none.</param>
/// <param name="Candidates">The folders checked, in order, de-duplicated.</param>
/// <param name="NamedByOption">The only candidate came from <c>--folder</c>.</param>
/// <param name="HomeUnknown">The home folder was empty, so <c>&lt;home&gt;/.ucl</c> was not a candidate.</param>
internal sealed record FolderResolution(
    string? Folder,
    IReadOnlyList<string> Candidates,
    bool NamedByOption,
    bool HomeUnknown);

/// <summary>What makes a folder an installation, and how to open one (ADR-0013).</summary>
internal static class Installation
{
    /// <summary>The database file whose presence marks a folder as an installation.</summary>
    public const string DatabaseFileName = "ucl.db";

    /// <summary>The folder under the user's home that setup uses by default.</summary>
    public const string HomeFolderName = ".ucl";

    /// <summary>
    /// Compares folder paths: full path, trailing separators ignored; ignoring case on Windows only.
    /// </summary>
    public static IEqualityComparer<string> PathComparer { get; } = new FolderPathComparer();

    /// <summary>An installation exists when <c>ucl.db</c> exists in the folder.</summary>
    public static bool Exists(string folder) => File.Exists(Path.Combine(folder, DatabaseFileName));

    /// <summary>
    /// Finds the installation: <paramref name="folderOption"/> if given (relative to
    /// <paramref name="currentDirectory"/>), else the current directory, else <c>&lt;home&gt;/.ucl</c>.
    /// Parents are not searched. An empty <paramref name="home"/> drops the home candidate.
    /// </summary>
    public static FolderResolution Resolve(string? folderOption, string currentDirectory, string home)
    {
        if (folderOption is not null)
        {
            var named = Path.GetFullPath(folderOption.Trim(), currentDirectory);
            return new FolderResolution(Exists(named) ? named : null, [named], NamedByOption: true, HomeUnknown: false);
        }

        var candidates = new List<string> { Path.GetFullPath(currentDirectory) };
        var homeUnknown = string.IsNullOrWhiteSpace(home);
        if (!homeUnknown)
        {
            candidates.Add(Path.GetFullPath(Path.Combine(home, HomeFolderName)));
        }

        var distinct = candidates.Distinct(PathComparer).ToArray();
        return new FolderResolution(distinct.FirstOrDefault(Exists), distinct, NamedByOption: false, homeUnknown);
    }

    /// <summary>
    /// Creates the folder when missing, then an empty <c>ucl.db</c> atomically: it never replaces or opens
    /// an existing one, so two creators cannot both succeed. SQLite accepts the empty file as a new database;
    /// <see cref="Open"/> then migrates it.
    /// </summary>
    /// <exception cref="ToolRefusal">The folder already holds an installation (checked and created in one step).</exception>
    public static void CreateNew(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, DatabaseFileName);
        try
        {
            using var created = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(path))
        {
            throw new ToolRefusal($"{folder} already holds an installation. Setup changed nothing there.");
        }
    }

    /// <summary>
    /// Opens the installation in <paramref name="folder"/>, creating <c>ucl.db</c> when missing. Refuses,
    /// changing nothing, when the database holds a migration this tool does not know; otherwise applies
    /// pending migrations. Dispose the context to release the file.
    /// </summary>
    /// <exception cref="ToolRefusal">The database was created by a newer tool.</exception>
    public static ToolDbContext Open(string folder)
    {
        Directory.CreateDirectory(folder);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(folder, DatabaseFileName),
            // Pooled handles keep the file open after the context is disposed, which blocks deleting,
            // moving or copying the folder.
            Pooling = false,
        }.ToString();

        var options = new DbContextOptionsBuilder<ToolDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(new ConnectionPragmas())
            .Options;

        var db = new ToolDbContext(options);
        try
        {
            var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var unknown = db.Database.GetAppliedMigrations().Where(m => !known.Contains(m)).ToArray();
            if (unknown.Length > 0)
            {
                throw new ToolRefusal(
                    $"This database was made by a newer version of the tool (unknown migration {string.Join(", ", unknown)}). "
                    + "Please update the tool. Nothing changed.");
            }

            db.Database.Migrate();
            return db;
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    private sealed class FolderPathComparer : IEqualityComparer<string>
    {
        private static readonly StringComparer Inner =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private static string Normalise(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        public bool Equals(string? x, string? y) =>
            x is null || y is null ? x is null && y is null : Inner.Equals(Normalise(x), Normalise(y));

        public int GetHashCode(string obj) => Inner.GetHashCode(Normalise(obj));
    }
}
