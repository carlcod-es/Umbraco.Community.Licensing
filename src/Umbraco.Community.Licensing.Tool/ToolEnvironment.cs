namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Everything a session needs from outside, so a whole session runs in-process in tests (ADR-0020).
/// No environment variables are read (ADR-0017).
/// </summary>
/// <param name="Console">The only console access.</param>
/// <param name="TimeProvider">Time and timers, including the idle lock's.</param>
/// <param name="Home">The user's home folder; <c>.ucl</c> under it is the fallback installation.</param>
/// <param name="CurrentDirectory">The folder searched first for <c>ucl.db</c>.</param>
/// <param name="Pbkdf2Iterations">PBKDF2 iteration count for new keys and passphrase checks.</param>
internal sealed record ToolEnvironment(
    ToolConsole Console,
    TimeProvider TimeProvider,
    string Home,
    string CurrentDirectory,
    int Pbkdf2Iterations)
{
    /// <summary>PBKDF2 iterations used in production (ADR-0014).</summary>
    public const int DefaultPbkdf2Iterations = 600_000;

    /// <summary>The real process: the terminal, system time, the user profile folder and the working directory.</summary>
    public static ToolEnvironment FromProcess() => new(
        new TerminalConsole(),
        TimeProvider.System,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Directory.GetCurrentDirectory(),
        DefaultPbkdf2Iterations);
}
