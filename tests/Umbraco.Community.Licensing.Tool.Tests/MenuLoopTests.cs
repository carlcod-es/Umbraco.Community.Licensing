namespace Umbraco.Community.Licensing.Tool.Tests;

public class MenuLoopTests : IDisposable
{
    private readonly SessionTestHost _host = new();

    public MenuLoopTests() => SessionTestHost.MakeInstallation(_host.HomeInstallation);

    public void Dispose() => _host.Dispose();

    private static ScriptedConsole Script(params string[] answers)
    {
        var console = new ScriptedConsole();
        foreach (var a in answers)
            console.Line(a);
        return console;
    }

    /// <summary>The numbered entries of the menu titled <paramref name="title"/>, first time it is shown.</summary>
    private static string[] Entries(string transcript, string title)
    {
        var lines = SessionTestHost.Lines(transcript);
        var start = Array.IndexOf(lines, title);
        Assert.True(start >= 0, $"Menu '{title}' not shown.");
        return lines.Skip(start + 1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal)).ToArray();
    }

    [Fact]
    public void MainMenu_ListsEntriesInDesignOrder_AndQuitExitsZero()
    {
        var console = Script("0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        Assert.Equal(
            ["  1 Products", "  2 Licenses", "  3 Signing keys", "  4 Backup", "  5 Settings", "  0 Quit"],
            Entries(console.Out, "Main menu"));
        Assert.Equal("", console.Err);
    }

    public static TheoryData<string, string, string[]> Submenus => new()
    {
        { "1", "Products", ["  1 Add a product", "  2 Rename a product", "  3 Add a feature", "  4 List products", "  0 Back"] },
        {
            "2", "Licenses",
            [
                "  1 Issue", "  2 Reissue", "  3 Renew", "  4 Resend", "  5 Find and list", "  6 Show a license",
                "  7 Customer details", "  0 Back",
            ]
        },
        {
            "3", "Signing keys",
            [
                "  1 Status", "  2 Add a standby key", "  3 Switch", "  4 Trusted keys", "  5 Release confirmed",
                "  6 Reissues sent", "  7 Export by signing key", "  0 Back",
            ]
        },
        { "4", "Backup", ["  1 Export", "  2 Restore into the current folder", "  0 Back"] },
        {
            "5", "Settings",
            [
                "  1 Folder in use", "  2 Prefix", "  3 Idle lock", "  4 New installation in the current folder",
                "  0 Back",
            ]
        },
    };

    [Theory]
    [MemberData(nameof(Submenus))]
    public void Submenu_ListsEntriesInDesignOrder_AndZeroIsBack(string number, string title, string[] expected)
    {
        var console = Script(number, "0", "0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        Assert.Equal(expected, Entries(console.Out, title));
        // Back returned to the main menu, which was then left with Quit.
        Assert.Equal(2, SessionTestHost.Lines(console.Out).Count(l => l == "Main menu"));
    }

    [Fact]
    public void NewInstallationSubmenu_ListsSetUpAndRestore()
    {
        var console = Script("5", "4", "0", "0", "0");

        _host.Run(console);

        Assert.Equal(
            ["  1 Set up", "  2 Restore from a backup", "  0 Back"],
            Entries(console.Out, "New installation in the current folder"));
    }

    [Fact]
    public void EveryEntryOfTheTree_HasAnActionKey_AndKeysAreUnique()
    {
        var keys = Menus.Keys().ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal(7 + 4 + 7 + 2 + 3 + 2, keys.Count);
    }

    [Fact]
    public void RefusedAnswer_ShowsTheMenuAgain()
    {
        var console = Script("9", "abc", "", "-1", "0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        var lines = SessionTestHost.Lines(console.Out);
        Assert.Equal(5, lines.Count(l => l == "Main menu"));
        Assert.Equal(4, SessionTestHost.Count(console.Out, "Answer a number from 0 to 5."));
    }

    [Fact]
    public void StubAction_SaysNotAvailable_AndReturnsToItsMenu()
    {
        var console = Script("1", "4", "0", "0");

        var exit = _host.Run(console);

        Assert.Equal(0, exit);
        Assert.Contains("List products is not available yet.", console.Out);
        Assert.Equal(2, SessionTestHost.Lines(console.Out).Count(l => l == "Products"));
    }

    [Fact]
    public void CompletedAction_ReturnsToTheMenuItWasChosenFrom_InSeveralSubmenus()
    {
        var ran = new List<string>();
        var actions = new ActionTable();
        actions.Register("products.add", _ => ran.Add("products.add"));
        actions.Register("keys.status", _ => ran.Add("keys.status"));
        actions.Register(Menus.SetUpHere, _ => ran.Add("setup"));
        var console = Script("1", "1", "0", "3", "1", "0", "5", "4", "1", "0", "0", "0");

        var exit = _host.Run(console, actions);

        Assert.Equal(0, exit);
        Assert.Equal(["products.add", "keys.status", "setup"], ran);
        var lines = SessionTestHost.Lines(console.Out);
        Assert.Equal(2, lines.Count(l => l == "Products"));
        Assert.Equal(2, lines.Count(l => l == "Signing keys"));
        Assert.Equal(2, lines.Count(l => l == "New installation in the current folder"));
        Assert.Equal(4, lines.Count(l => l == "Main menu"));
    }

    [Fact]
    public void CancelledAction_ReturnsToTheMenuItWasChosenFrom_InSeveralSubmenus()
    {
        var actions = new ActionTable();
        actions.Register("licenses.issue", _ => throw new ActionCancelledException());
        actions.Register(Menus.RestoreHere, _ => throw new ActionCancelledException());
        var console = Script("2", "1", "0", "5", "4", "2", "0", "0", "0");

        var exit = _host.Run(console, actions);

        Assert.Equal(0, exit);
        var lines = SessionTestHost.Lines(console.Out);
        Assert.Equal(2, lines.Count(l => l == "Licenses"));
        Assert.Equal(2, lines.Count(l => l == "New installation in the current folder"));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Cancelled."));
    }

    [Fact]
    public void ActionCancelledByTheVendor_AtItsQuestion_ReturnsToTheMenu()
    {
        var actions = new ActionTable();
        actions.Register("products.add", s => s.Questions.AskText("Product ID", "letters", _ => null));
        var console = Script("1", "1", "/cancel", "0", "0");

        var exit = _host.Run(console, actions);

        Assert.Equal(0, exit);
        Assert.Equal(2, SessionTestHost.Lines(console.Out).Count(l => l == "Products"));
    }

    [Fact]
    public void MenuAnswers_RaiseAnswerRead()
    {
        var console = Script("1", "0", "0");
        var answers = 0;
        console.AnswerRead += () => answers++;

        _host.Run(console);

        Assert.Equal(3, answers);
    }

    [Fact]
    public void EndOfInput_AtAMenu_ExitsOne()
    {
        var console = Script("1");

        var exit = _host.Run(console);

        Assert.Equal(1, exit);
    }
}
