using System.Text;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Settings &gt; New installation in the current folder (vendor-tool-setup: Second installation, New installation in the current folder), as whole sessions.</summary>
public class NewInstallationTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Earlier = "an earlier held passphrase";
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private int Run(ScriptedConsole console, ActionTable? actions = null, Func<Installation, TimeSpan?>? readIdleTime = null, FakeTimeProvider? clock = null) =>
        _host.Run(console, actions, Startup.Offer.Default, clock: clock, readIdleTime: readIdleTime);

    /// <summary>Every table of the installation, as text: what "unchanged" compares. The file bytes are compared too where it matters (opening an installation writes nothing).</summary>
    private static string Content(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return System.Text.Json.JsonSerializer.Serialize(new object[]
        {
            db.Settings.AsNoTracking().OrderBy(x => x.Name).ToList(),
            db.Products.AsNoTracking().OrderBy(x => x.ProductId).ToList(),
            db.SigningKeys.AsNoTracking().OrderBy(x => x.SigningKeyId).ToList(),
            db.Licenses.AsNoTracking().OrderBy(x => x.Reference).ToList(),
        });
    }

    private static Dictionary<string, string> Settings(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return db.Settings.AsNoTracking().ToDictionary(s => s.Name, s => s.Value);
    }

    // Main menu 5 is Settings; there 1 is Folder in use and 4 the new installation, whose 1 is Set up and 2 Restore.
    private static ScriptedConsole OpenNew(ScriptedConsole c, string choice) => c.Line("5").Line("4").Line(choice);
    // From the New installation submenu: back to Settings, back to the main menu, quit.
    private static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0").Line("0");

    /// <summary>The answers after the passphrase: prefix NEW, the rest proposed.</summary>
    private static ScriptedConsole AnswerSetUp(ScriptedConsole c, string passphrase = Words) =>
        c.Hidden(passphrase).Hidden(passphrase).AnswerRest(prefix: "NEW");

    /// <summary>Products &gt; Add a product and Products &gt; Rename stand in for actions that hold and read the passphrase.</summary>
    private static ActionTable Stubs(Action<Session> hold, Action<Session> read)
    {
        var actions = ActionTable.Default();
        actions.Register("products.add", hold);
        actions.Register("products.rename", read);
        return actions;
    }

    private static byte[]? HeldBytes(Session session)
    {
        byte[]? bytes = null;
        session.Lock.TryUse(p => bytes = p.ToArray());
        return bytes;
    }

    [Fact]
    public void SecondInstallation_InAnotherFolder_IsIndependent_AndEachSessionSeesOnlyItsOwn()
    {
        var home = _host.SetUpInstallation(prefix: "AAA");
        var homeContent = Content(home);
        var homeSettings = Settings(home);
        var homeBytes = File.ReadAllBytes(Path.Combine(home, Startup.DatabaseFileName));

        var setup = Quit(AnswerSetUp(OpenNew(new ScriptedConsole(), "1")));
        Assert.Equal(0, Run(setup));
        Assert.Equal("", setup.Err);

        Assert.True(Installation.Holds(_host.Cwd));
        Assert.Equal("NEW", Settings(_host.Cwd)[SettingNames.Prefix]);
        Assert.Equal(homeContent, Content(home));
        Assert.Equal(homeSettings, Settings(home));
        Assert.Equal(homeBytes, File.ReadAllBytes(Path.Combine(home, Startup.DatabaseFileName)));

        // A later session in the folder sees only its own data (Settings > Prefix shows NEW); one elsewhere uses ~/.ucl.
        var inCwd = Quit(new ScriptedConsole().Line("5").Line("2").Line(""));
        Assert.Equal(0, Run(inCwd));
        Assert.Contains($"Using {Path.GetFullPath(_host.Cwd)}", inCwd.Out);
        Assert.Contains("Prefix: NEW", inCwd.Out);

        _host.Cwd = Path.Combine(Path.GetDirectoryName(_host.Cwd)!, "elsewhere");
        Directory.CreateDirectory(_host.Cwd);
        var elsewhere = Quit(new ScriptedConsole().Line("5").Line("2").Line(""));
        Assert.Equal(0, Run(elsewhere));
        Assert.Contains($"Using {home}", elsewhere.Out);
        Assert.Contains("Prefix: AAA", elsewhere.Out);
        Assert.DoesNotContain("Prefix: NEW", elsewhere.Out);
    }

    [Fact]
    public void NewInstallationFromSettings_StatesTheNewPath_ContinuesWithIt_HoldsThePassphraseJustSet_AndLeavesHomeUnchanged()
    {
        var home = _host.SetUpInstallation(prefix: "AAA");
        var homeContent = Content(home);
        byte[]? held = null;
        var actions = Stubs(_ => { }, s => held = HeldBytes(s));

        // Set up, Back, Settings > Folder in use (1), Back, then Products > Rename, which reads the lock.
        var console = AnswerSetUp(OpenNew(new ScriptedConsole(), "1")).Line("0").Line("1").Line("0").Line("1").Line("2").Line("0").Line("0");
        Assert.Equal(0, Run(console, actions));

        var cwd = Path.GetFullPath(_host.Cwd);
        Assert.Equal("", console.Err);
        Assert.DoesNotContain("Folder for the installation", console.Out); // the current folder is the only one: not asked
        Assert.Equal(homeContent, Content(home));

        // The closing message, then Using <new path> with the unused note, then Settings again.
        var closing = console.Out.IndexOf("Setup is complete", StringComparison.Ordinal);
        var usingNew = console.Out.IndexOf($"Using {cwd}", StringComparison.Ordinal);
        var settingsAgain = console.Out.IndexOf("Settings", usingNew, StringComparison.Ordinal);
        Assert.True(closing >= 0 && closing < usingNew && usingNew < settingsAgain);
        Assert.Contains($"  {cwd}", console.Out[closing..usingNew]);
        Assert.Equal(2, SessionTestHost.Count(console.Out, $"Using {cwd}")); // stated at the move and by Folder in use
        Assert.Equal(2, SessionTestHost.Count(console.Out, Startup.UnusedNote(home)));
        Assert.Equal(Encoding.ASCII.GetBytes(Words), held);
    }

    [Fact]
    public void NewInstallationFromSettings_ReplacesAnEarlierHeldPassphrase_ZeroingIt()
    {
        _host.SetUpInstallation();
        var earlier = Encoding.ASCII.GetBytes(Earlier);
        byte[]? held = null;
        var actions = Stubs(s => s.Lock.Hold(earlier), s => held = HeldBytes(s));

        var console = new ScriptedConsole().Line("1").Line("1").Line("0");
        AnswerSetUp(OpenNew(console, "1")).Line("0").Line("0").Line("1").Line("2").Line("0").Line("0");
        Assert.Equal(0, Run(console, actions));

        Assert.Equal(Encoding.ASCII.GetBytes(Words), held);
        Assert.All(earlier, b => Assert.Equal(0, b));
    }

    [Fact]
    public void CancelledSetUpFromSettings_CreatesNothing_KeepsTheEarlierInstallation_AndTheEarlierHeldPassphrase()
    {
        var home = _host.SetUpInstallation(prefix: "AAA");
        var earlier = Encoding.ASCII.GetBytes(Earlier);
        byte[]? held = null;
        var actions = Stubs(s => s.Lock.Hold(earlier), s => held = HeldBytes(s));

        var console = new ScriptedConsole().Line("1").Line("1").Line("0");
        OpenNew(console, "1").Hidden(Words).Hidden(Words).Line("/cancel"); // cancelled at the prefix
        console.Line("0").Line("0").Line("1").Line("2").Line("0").Line("0");
        Assert.Equal(0, Run(console, actions));

        Assert.Contains("Cancelled.", console.Out);
        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
        Assert.DoesNotContain("Setup is complete", console.Out);
        Assert.Equal(Encoding.ASCII.GetBytes(Earlier), held);
        Assert.Equal(1, SessionTestHost.Count(console.Out, $"Using {home}"));
        Assert.DoesNotContain($"Using {Path.GetFullPath(_host.Cwd)}", console.Out);
    }

    [Fact]
    public void CancelledSetUpFromSettings_AfterTheIdleTimePassedInsideSetUp_ZeroesTheEarlierPassphrase_AndTheNextActionHasNone()
    {
        var idle = TimeSpan.FromMinutes(15);
        var clock = new FakeTimeProvider();
        _host.SetUpInstallation(prefix: "AAA");
        var earlier = GC.AllocateArray<byte>(Earlier.Length, pinned: true);
        Encoding.ASCII.GetBytes(Earlier, earlier);
        byte[]? held = [1];
        var actions = Stubs(s => s.Lock.Hold(earlier), s => held = HeldBytes(s));

        // Products > Add holds the earlier passphrase; then the vendor leaves the terminal for the idle time at the prefix and cancels.
        var console = new ScriptedConsole(clock: clock).Line("1").Line("1").Line("0");
        OpenNew(console, "1").Hidden(Words).Hidden(Words).Line("/cancel", idle);
        console.Line("0").Line("0").Line("1").Line("2").Line("0").Line("0");
        Assert.Equal(0, Run(console, actions, _ => idle, clock));

        Assert.Contains("Cancelled.", console.Out);
        Assert.All(earlier, b => Assert.Equal(0, b));
        Assert.Null(held);
    }

    [Fact]
    public void SetUpFromSettings_TheSessionRefusesTheNewDatabase_IsAFailedSetUp_NothingLeft_EarlierKept_ClosingNotPrinted()
    {
        var home = _host.SetUpInstallation();
        var cwd = Path.GetFullPath(_host.Cwd);
        var earlier = Encoding.ASCII.GetBytes(Earlier);
        byte[]? held = null;
        var actions = Stubs(s => s.Lock.Hold(earlier), s => held = HeldBytes(s));

        var console = new ScriptedConsole().Line("1").Line("1").Line("0");
        AnswerSetUp(OpenNew(console, "1")).Line("0").Line("0").Line("1").Line("2").Line("0").Line("0");
        // Reading the idle time of the new installation fails, as for a database the session cannot use.
        var exit = Run(console, actions, i => Startup.SamePath(i.Folder, cwd)
            ? throw new InstallationRefusedException("The idle lock setting looks damaged.")
            : TimeSpan.FromMinutes(15));

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        Assert.Contains("New installation in the current folder - Set up failed: The idle lock setting looks damaged.", console.Out);
        Assert.DoesNotContain("Setup is complete", console.Out);
        Assert.DoesNotContain($"Using {cwd}", console.Out);
        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
        Assert.Equal(Encoding.ASCII.GetBytes(Earlier), held);
        Assert.Equal(1, SessionTestHost.Count(console.Out, $"Using {home}"));
        Assert.True(SessionTestHost.Lines(console.Out).Count(l => l == "New installation in the current folder") >= 2);
    }

    [Fact]
    public void CurrentFolderIsTheHomeFolder_IsRefused_NothingChanges_AndNothingIsAsked()
    {
        var home = _host.SetUpInstallation();
        var homeContent = Content(home);
        _host.Cwd = _host.Home;

        foreach (var choice in new[] { "1", "2" })
        {
            var console = Quit(new ScriptedConsole().Line("5").Line("4").Line(choice));
            Assert.Equal(0, Run(console));

            Assert.Equal("", console.Err);
            Assert.Contains(SettingsActions.HomeFolderRefused, console.Out);
            Assert.Contains("cannot sit loose in the home folder", console.Out);
            Assert.DoesNotContain("not available yet", console.Out);
            Assert.Empty(console.HiddenGiven);
            Assert.DoesNotContain("Choose a passphrase", console.Out);
            Assert.True(SessionTestHost.Lines(console.Out).Count(l => l == "New installation in the current folder") >= 2); // the menu is shown again
            Assert.Equal([".ucl"], Directory.GetFileSystemEntries(_host.Home).Select(Path.GetFileName));
            Assert.Equal(homeContent, Content(home));
        }
    }

    [Fact]
    public void CurrentFolderAlreadyHoldsAnInstallation_IsRefusedNamingIt_NothingChanges()
    {
        // The session uses the installation in the current folder; the vendor asks for a new one there.
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: "another passphrase entirely");
        var cwd = Path.GetFullPath(_host.Cwd);
        var before = Content(cwd);
        var settings = Settings(cwd);

        foreach (var choice in new[] { "1", "2" })
        {
            var console = Quit(new ScriptedConsole().Line("5").Line("4").Line(choice));
            Assert.Equal(0, Run(console));

            Assert.Equal("", console.Err);
            Assert.Contains(SettingsActions.HoldsInstallationRefused(cwd), console.Out);
            Assert.DoesNotContain("not available yet", console.Out);
            Assert.Empty(console.HiddenGiven);
            Assert.Equal(before, Content(cwd));
            Assert.Equal(settings, Settings(cwd));
            Assert.False(Directory.Exists(Path.Combine(_host.Home, ".ucl")));
        }
    }

    [Fact]
    public void RestoreFromSettings_IsAStub_ChangesNothing_AndKeepsTheEarlierPassphrase()
    {
        var home = _host.SetUpInstallation();
        var earlier = Encoding.ASCII.GetBytes(Earlier);
        byte[]? held = null;
        var actions = Stubs(s => s.Lock.Hold(earlier), s => held = HeldBytes(s));

        var console = new ScriptedConsole().Line("1").Line("1").Line("0");
        OpenNew(console, "2").Line("0").Line("0").Line("1").Line("2").Line("0").Line("0");
        Assert.Equal(0, Run(console, actions));

        Assert.Contains("Restore from a backup is not available yet.", console.Out);
        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
        Assert.Equal(Encoding.ASCII.GetBytes(Earlier), held);
        Assert.Equal(1, SessionTestHost.Count(console.Out, $"Using {home}"));
        Assert.Equal("", console.Err);
    }
}
