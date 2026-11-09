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

    /// <summary>
    /// Makes <paramref name="folder"/> hold a real, migrated installation with the idle lock setting
    /// <paramref name="idleLock"/> (null leaves the setting out, as a damaged installation would be).
    /// </summary>
    public static void MakeInstallation(string folder, string? idleLock = "never")
    {
        var installation = Installation.Open(folder);
        if (idleLock is null)
            return;
        using var db = installation.OpenDb();
        if (db.Settings.Any(x => x.Name == Data.SettingNames.IdleLock))
            return;
        db.Settings.Add(new Data.Setting { Name = Data.SettingNames.IdleLock, Value = idleLock });
        db.SaveChanges();
    }

    public int Run(ScriptedConsole console, ActionTable? actions = null, Startup.Offer? offer = null, FakeTimeProvider? clock = null) =>
        ToolApp.Run(
            [],
            new ToolEnvironment(console, clock ?? new FakeTimeProvider(), Home, Cwd, 1000),
            env => MenuLoop.Run(env, actions ?? ActionTable.Default(), offer ?? Startup.Offer.Stubs));

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
