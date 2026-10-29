namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Everything a command reads from the outside world, so tests run commands with a scripted one.
/// </summary>
/// <param name="Console">Standard streams and prompts.</param>
/// <param name="TimeProvider">The clock.</param>
/// <param name="Home">The user's home folder.</param>
/// <param name="CurrentDirectory">The current directory.</param>
/// <param name="GetVariable">Looks up an environment variable (e.g. <c>UCL_PASSPHRASE</c>); null when unset.</param>
/// <param name="Pbkdf2Iterations">PBKDF2 iteration count for passphrase-derived keys.</param>
internal sealed record ToolEnvironment(
    ToolConsole Console,
    TimeProvider TimeProvider,
    string Home,
    string CurrentDirectory,
    Func<string, string?> GetVariable,
    int Pbkdf2Iterations)
{
    /// <summary>Production PBKDF2 iteration count.</summary>
    public const int ProductionPbkdf2Iterations = 600_000;

    /// <summary>The running process.</summary>
    public static ToolEnvironment FromProcess() => new(
        ToolConsole.FromProcess(),
        TimeProvider.System,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Directory.GetCurrentDirectory(),
        Environment.GetEnvironmentVariable,
        ProductionPbkdf2Iterations);
}
