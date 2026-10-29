namespace Umbraco.Community.Licensing.Tool;

/// <summary>What makes a folder an installation (ADR-0013).</summary>
internal static class Installation
{
    /// <summary>The database file whose presence marks a folder as an installation.</summary>
    public const string DatabaseFileName = "ucl.db";

    /// <summary>The folder under the user's home that setup uses by default.</summary>
    public const string HomeFolderName = ".ucl";

    /// <summary>An installation exists when <c>ucl.db</c> exists in the folder.</summary>
    public static bool Exists(string folder) => File.Exists(Path.Combine(folder, DatabaseFileName));
}
