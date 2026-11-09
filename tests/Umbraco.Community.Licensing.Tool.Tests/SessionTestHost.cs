namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Builds temp home and current folders and runs whole sessions through <see cref="ToolApp.Run(string[], ToolEnvironment, Func{ToolEnvironment, int})"/>.</summary>
internal sealed class SessionTestHost : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ucl-session-" + Guid.NewGuid());

    public SessionTestHost()
    {
        Home = Path.Combine(_root, "home");
        Cwd = Path.Combine(_root, "work", "cwd");
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Cwd);
    }

    public string Home { get; }
    public string Cwd { get; set; }
    public string HomeInstallation => Path.Combine(Home, ".ucl");

    /// <summary>The iteration count the host's sessions and passphrase checks use.</summary>
    public const int Iterations = 1000;

    /// <summary>
    /// Makes <paramref name="folder"/> hold a real, migrated installation with the idle lock setting
    /// <paramref name="idleLock"/> (null leaves the setting out, as a damaged installation would be) and,
    /// when <paramref name="passphrase"/> is given, a passphrase check for it at <see cref="Iterations"/>.
    /// </summary>
    public static void MakeInstallation(string folder, string? idleLock = "never", string? passphrase = null)
    {
        var installation = Installation.Holds(folder) ? Installation.OpenExisting(folder) : Installation.Create(folder);
        using var db = installation.OpenDb();
        if (idleLock is not null && !db.Settings.Any(x => x.Name == Data.SettingNames.IdleLock))
            db.Settings.Add(new Data.Setting { Name = Data.SettingNames.IdleLock, Value = idleLock });
        if (passphrase is not null && !db.Settings.Any(x => x.Name == Data.SettingNames.PassphraseHash))
            db.Settings.AddRange(PassphraseCheck.Create(System.Text.Encoding.ASCII.GetBytes(passphrase), Iterations).ToSettings());
        db.SaveChanges();
    }

    /// <summary>A passphrase the setup helpers type: 28 characters of words, valid under the passphrase rule.</summary>
    public const string SetupPassphrase = "correct horse battery staple";

    public int Run(
        ScriptedConsole console,
        ActionTable? actions = null,
        Startup.Offer? offer = null,
        FakeTimeProvider? clock = null,
        Func<Installation, TimeSpan?>? readIdleTime = null) =>
        ToolApp.Run(
            [],
            new ToolEnvironment(console, clock ?? new FakeTimeProvider(), Home, Cwd, Iterations),
            env => MenuLoop.Run(env, actions ?? ActionTable.Default(), offer ?? Startup.Offer.Stubs, readIdleTime));

    /// <summary>
    /// Makes an installation in <see cref="HomeInstallation"/> through a whole session: the startup offer, real setup
    /// with the proposed answers except those given, then Quit. Later sections use this to start from the same state
    /// a vendor does. Returns the folder.
    /// </summary>
    public string SetUpInstallation(bool shared = false, bool standby = true, string idleLock = "", string prefix = "", string passphrase = SetupPassphrase)
    {
        var console = new ScriptedConsole().SetUp(shared: shared, standby: standby, idleLock: idleLock, prefix: prefix, passphrase: passphrase).Line("0");
        var exit = Run(console, offer: Startup.Offer.Default);
        if (exit != 0 || console.Err.Length > 0)
            throw new InvalidOperationException($"Setup did not complete (exit {exit}): {console.Err}{console.Out}");
        return HomeInstallation;
    }

    /// <summary>A transcript's lines, without line endings.</summary>
    public static string[] Lines(string text) => text.Split(["\r\n", "\n"], StringSplitOptions.None);

    /// <summary>How many times <paramref name="text"/> appears; a scripted answer has no echoed line ending, so output after a prompt shares its line.</summary>
    public static int Count(string transcript, string text) =>
        transcript.Split(text).Length - 1;

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

/// <summary>Scripted answers for guided setup at the startup offer.</summary>
internal static class SetupScript
{
    /// <summary>The startup offer's "Set up a new installation", then its folder question (empty takes <c>.ucl</c>).</summary>
    public static ScriptedConsole ChooseSetUp(this ScriptedConsole console, string folder = "") =>
        console.Line("1").Line(folder);

    /// <summary>The questions after the passphrase, in order: prefix, mode, standby, idle lock. Empty answers take the proposed values.</summary>
    public static ScriptedConsole AnswerRest(
        this ScriptedConsole console, string prefix = "", bool shared = false, bool standby = true, string idleLock = "") =>
        console
            .Line(prefix)
            .Line(shared ? "2" : "")
            .Line(standby ? "" : "n")
            .Line(idleLock);

    /// <summary>
    /// The whole of setup at the startup offer: folder, passphrase twice, then the rest. A null
    /// <paramref name="passphraseAgain"/> repeats the passphrase.
    /// </summary>
    public static ScriptedConsole SetUp(
        this ScriptedConsole console,
        string folder = "",
        string passphrase = SessionTestHost.SetupPassphrase,
        string? passphraseAgain = null,
        string prefix = "",
        bool shared = false,
        bool standby = true,
        string idleLock = "") =>
        console
            .ChooseSetUp(folder)
            .Hidden(passphrase)
            .Hidden(passphraseAgain ?? passphrase)
            .AnswerRest(prefix, shared, standby, idleLock);
}
