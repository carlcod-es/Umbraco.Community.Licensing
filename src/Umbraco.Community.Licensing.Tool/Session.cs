using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The state one menu session shares with its actions. The current installation folder can change
/// during the session (setup or restore into another folder, ADR-0022) through <see cref="UseFolder"/>.
/// Owns the <see cref="SessionLock"/>, which every completed answer restarts and which is zeroed when the
/// session is disposed. Holds the open <see cref="Installation"/> (ADR-0013); actions take a short-lived database context from <see cref="OpenDb"/>.
/// </summary>
internal sealed class Session : IDisposable
{
    public Session(ToolEnvironment environment)
    {
        Environment = environment;
        Questions = new Questions(environment.Console);
        Lock = new SessionLock(environment.TimeProvider);
        environment.Console.AnswerRead += Lock.Touch;
    }

    public ToolEnvironment Environment { get; }

    public ToolConsole Console => Environment.Console;

    public Questions Questions { get; }

    /// <summary>The held passphrase and its idle lock (ADR-0021).</summary>
    public SessionLock Lock { get; }

    /// <summary>
    /// Reads the idle time (null is <c>never</c>) of an installation just opened: by default its <c>Settings</c>
    /// row (ADR-0022). Tests replace it to set an idle time without writing the setting.
    /// </summary>
    public Func<Installation, TimeSpan?> ReadIdleTime { get; set; } = installation => installation.ReadIdleTime();

    /// <summary>The installation in use; null before one is found or created.</summary>
    public Installation? Installation { get; private set; }

    /// <summary>
    /// A new database context for one action. The action disposes it in a <c>using</c> when it ends, completed or
    /// failed, so nothing stays open after a failure and the session carries on (PDR-0053).
    /// </summary>
    public ToolDbContext OpenDb() =>
        (Installation ?? throw new InvalidOperationException("No installation is in use.")).OpenDb();

    /// <summary>The installation folder in use, as a full path; null before one is found or created.</summary>
    public string? Folder { get; private set; }

    /// <summary>The note that <c>.ucl</c> in the home folder also holds an installation and is not used, or null.</summary>
    public string? UnusedNote { get; private set; }

    /// <summary>
    /// Moves the session to an installation folder and states it, and takes that installation's idle
    /// time (ADR-0022). The held passphrase is zeroed and dropped, since it belongs to the installation
    /// left (a restore, PDR-0052), unless <paramref name="keepPassphrase"/> is true: setup into a new
    /// folder keeps the passphrase it has just handed to <see cref="Lock"/>.
    /// <paramref name="beforeUsing"/> writes what must come before the <c>Using</c> line (setup's closing message); it runs
    /// only once the folder is known to be usable, and when it fails the session is left as it was.
    /// </summary>
    public void UseFolder(string folder, string? unusedNote = null, bool keepPassphrase = false, Action? beforeUsing = null)
    {
        // Open (an installation that is there) and migrate first: a folder that is refused leaves the session, its passphrase and its idle time as they were.
        var installation = Installation.OpenExisting(folder);
        var idleTime = ReadIdleTime(installation);
        beforeUsing?.Invoke();
        if (!keepPassphrase)
            Lock.Release();
        Lock.SetIdleTime(idleTime);
        Installation = installation;
        Folder = installation.Folder;
        UnusedNote = unusedNote;
        WriteFolderInUse();
    }

    /// <summary>Writes <c>Using &lt;full path&gt;</c> and the unused-installation note when there is one.</summary>
    public void WriteFolderInUse()
    {
        Console.WriteLine($"Using {Folder}");
        if (UnusedNote is not null)
            Console.WriteLine(UnusedNote);
    }

    /// <summary>Zeroes and drops the held passphrase and stops listening for answers.</summary>
    public void Dispose()
    {
        Environment.Console.AnswerRead -= Lock.Touch;
        Lock.Dispose();
    }
}
