namespace Umbraco.Community.Licensing.Tool.Tests;

public class StartupTests : IDisposable
{
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static ScriptedConsole Script(params string[] answers)
    {
        var console = new ScriptedConsole();
        foreach (var a in answers)
            console.Line(a);
        return console;
    }

    private static string Note(string homeInstallation) => Startup.UnusedNote(homeInstallation);

    [Fact]
    public void CurrentFolderUsed_StatesItsPath_AndNotesTheUnusedHomeInstallation_AlsoInSettings()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        SessionTestHost.MakeInstallation(_host.HomeInstallation);
        var console = Script("5", "1", "0", "0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        var lines = SessionTestHost.Lines(console.Out);
        Assert.Equal($"Using {_host.Cwd}", lines[0]);
        Assert.Equal(Note(_host.HomeInstallation), lines[1]);
        // Settings > Folder in use shows the same two lines again.
        Assert.Equal(2, SessionTestHost.Count(console.Out, $"Using {_host.Cwd}"));
        Assert.Equal(2, SessionTestHost.Count(console.Out, Note(_host.HomeInstallation)));
        Assert.DoesNotContain("~", console.Out);
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void CurrentFolderUsed_WithNoHomeInstallation_HasNoNote()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var console = Script("0");

        _host.Run(console);

        Assert.StartsWith($"Using {_host.Cwd}{Environment.NewLine}", console.Out);
        Assert.DoesNotContain("Note:", console.Out);
    }

    [Fact]
    public void HomeInstallationUsedElsewhere_StatesItsPath_WithNoNote_AlsoInSettings()
    {
        SessionTestHost.MakeInstallation(_host.HomeInstallation);
        var console = Script("5", "1", "0", "0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        var lines = SessionTestHost.Lines(console.Out);
        Assert.Equal($"Using {_host.HomeInstallation}", lines[0]);
        Assert.Equal(2, SessionTestHost.Count(console.Out, $"Using {_host.HomeInstallation}"));
        Assert.DoesNotContain("Note:", console.Out);
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void StartedInTheHomeFolder_UsesTheHomeInstallation_NotTheHomeFolderItself()
    {
        SessionTestHost.MakeInstallation(_host.HomeInstallation);
        File.WriteAllText(Path.Combine(_host.Home, Startup.DatabaseFileName), "");
        _host.Cwd = _host.Home;
        var console = Script("0");

        _host.Run(console);

        Assert.StartsWith($"Using {_host.HomeInstallation}{Environment.NewLine}", console.Out);
        Assert.DoesNotContain("Note:", console.Out);
    }

    [Fact]
    public void NoInstallationFound_NamesBothFolders_AndOffersSetUpRestoreQuit()
    {
        var console = Script("0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        Assert.Contains($"  {_host.Cwd}", console.Out);
        Assert.Contains($"  {_host.HomeInstallation}", console.Out);
        Assert.DoesNotContain("~", console.Out);
        var lines = SessionTestHost.Lines(console.Out);
        var start = Array.IndexOf(lines, "What now?");
        Assert.Equal(
            ["  1 Set up a new installation", "  2 Restore from a backup", "  0 Quit"],
            lines.Skip(start + 1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal)));
        Assert.DoesNotContain("Main menu", console.Out);
    }

    [Fact]
    public void NoInstallationFound_InTheHomeFolder_NamesOnlyTheHomeInstallationFolder()
    {
        _host.Cwd = _host.Home;
        var console = Script("0");

        _host.Run(console);

        Assert.Contains($"  {_host.HomeInstallation}", console.Out);
        Assert.DoesNotContain($"  {_host.Home}{Environment.NewLine}", console.Out);
    }

    [Fact]
    public void QuitAtTheStartupOffer_ExitsZero_AndCreatesNothing()
    {
        var console = Script("0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        Assert.Empty(Directory.GetFileSystemEntries(_host.Home));
        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void StubbedSetUpAndRestore_ShowTheOfferAgain_AndCreateNothing()
    {
        var console = Script("1", "2", "9", "0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        Assert.Contains("Set up a new installation is not available yet.", console.Out);
        Assert.Contains("Restore from a backup is not available yet.", console.Out);
        Assert.Equal(4, SessionTestHost.Lines(console.Out).Count(l => l == "What now?"));
        Assert.Empty(Directory.GetFileSystemEntries(_host.Home));
    }

    [Fact]
    public void SetUpAtTheOffer_ThatReturnsAFolder_StatesItAndShowsTheMainMenu()
    {
        var created = Path.Combine(_host.Home, ".ucl");
        var offer = new Startup.Offer(_ => { SessionTestHost.MakeInstallation(created); return created; }, Startup.Offer.Stubs.Restore);
        var console = Script("1", "0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        var lines = SessionTestHost.Lines(console.Out);
        Assert.Contains($"Using {created}", console.Out);
        Assert.Contains("Main menu", lines);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public void OfferAction_Cancelled_SaysSo_AndShowsTheOfferAgain(string choice)
    {
        var offer = new Startup.Offer(
            _ => throw new ActionCancelledException(),
            _ => throw new ActionCancelledException());
        var console = Script(choice, "0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Cancelled."));
        Assert.Equal(2, SessionTestHost.Lines(console.Out).Count(l => l == "What now?"));
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void SessionFolder_CanChange_AfterStart_AndSettingsShowsTheNewOne()
    {
        SessionTestHost.MakeInstallation(_host.HomeInstallation);
        var other = Path.Combine(_host.Home, "other");
        SessionTestHost.MakeInstallation(other);
        var actions = ActionTable.Default();
        actions.Register("settings.prefix", s => s.UseFolder(other));
        var console = Script("5", "2", "1", "0", "0");

        _host.Run(console, actions);

        Assert.Equal(2, SessionTestHost.Count(console.Out, $"Using {other}"));
    }
}
