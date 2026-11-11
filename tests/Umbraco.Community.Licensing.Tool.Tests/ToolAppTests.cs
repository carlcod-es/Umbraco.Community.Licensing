using System.Xml.Linq;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class ToolAppTests
{
    private static ToolEnvironment Env(ScriptedConsole console, string? home = null, string? cwd = null) =>
        new(console, new FakeTimeProvider(), home ?? Path.Combine(Path.GetTempPath(), "ucl-home-" + Guid.NewGuid()),
            cwd ?? Path.Combine(Path.GetTempPath(), "ucl-cwd-" + Guid.NewGuid()), 1000);

    private static int RunWithSessionCounter(string[] args, ScriptedConsole console, out int sessions)
    {
        var count = 0;
        var exit = ToolApp.Run(args, Env(console), _ => { count++; return 0; });
        sessions = count;
        return exit;
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void HelpWithNobodyPresent_WritesHelpAndSucceeds(string flag)
    {
        var console = new ScriptedConsole(inputRedirected: true, outputRedirected: true);

        var exit = RunWithSessionCounter([flag], console, out var sessions);

        Assert.Equal(0, exit);
        Assert.Equal(0, sessions);
        Assert.Contains("Products", console.Out);
        Assert.Contains("Signing keys", console.Out);
        Assert.Contains("Quit", console.Out);
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void VersionWithNobodyPresent_WritesVersionEqualToVersionProps()
    {
        var console = new ScriptedConsole(inputRedirected: true, outputRedirected: true);

        var exit = ToolApp.Run(["--version"], Env(console));

        Assert.Equal(0, exit);
        Assert.Equal(VersionFromProps(), console.Out.Trim());
        Assert.DoesNotContain('+', console.Out);
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void NobodyPresent_RefusesOnStderrAndReadsNothing()
    {
        var home = Path.Combine(Path.GetTempPath(), "ucl-home-" + Guid.NewGuid());
        var cwd = Path.Combine(Path.GetTempPath(), "ucl-cwd-" + Guid.NewGuid());
        var console = new ScriptedConsole(inputRedirected: true, outputRedirected: true);
        var sessions = 0;

        var exit = ToolApp.Run([], Env(console, home, cwd), _ => { sessions++; return 0; });

        Assert.Equal(1, exit);
        Assert.Equal(0, sessions);
        Assert.Contains("person at a terminal", console.Err);
        Assert.Equal("", console.Out);
        Assert.False(Directory.Exists(home));
        Assert.False(Directory.Exists(cwd));
    }

    [Fact]
    public void OutputRedirected_InputATerminal_RefusesWithExitOne()
    {
        var console = new ScriptedConsole(inputRedirected: false, outputRedirected: true);

        var exit = RunWithSessionCounter([], console, out var sessions);

        Assert.Equal(1, exit);
        Assert.Equal(0, sessions);
        Assert.Contains("person at a terminal", console.Err);
        Assert.Equal("", console.Out);
    }

    [Fact]
    public void PersonPresent_OpensTheSession()
    {
        var console = new ScriptedConsole();

        var exit = RunWithSessionCounter([], console, out var sessions);

        Assert.Equal(0, exit);
        Assert.Equal(1, sessions);
    }

    [Theory]
    [InlineData("license", "issue", "acme.commerce")]
    [InlineData("--folder", "x")]
    [InlineData("--passphrase-stdin")]
    [InlineData("--help", "extra")]
    [InlineData("--version", "extra")]
    public void CommandGiven_RefusesWithExitTwoAndChangesNothing(params string[] args)
    {
        var home = Path.Combine(Path.GetTempPath(), "ucl-home-" + Guid.NewGuid());
        var cwd = Path.Combine(Path.GetTempPath(), "ucl-cwd-" + Guid.NewGuid());
        var console = new ScriptedConsole();
        var sessions = 0;

        var exit = ToolApp.Run(args, Env(console, home, cwd), _ => { sessions++; return 0; });

        Assert.Equal(2, exit);
        Assert.Equal(0, sessions);
        Assert.Contains("no command other than --help and --version", console.Err);
        Assert.Contains("menu", console.Err);
        Assert.Equal("", console.Out);
        Assert.False(Directory.Exists(home));
        Assert.False(Directory.Exists(cwd));
    }

    [Fact]
    public void CommandGiven_WithNobodyPresent_StillRefusesWithExitTwo()
    {
        var console = new ScriptedConsole(inputRedirected: true, outputRedirected: true);

        Assert.Equal(2, ToolApp.Run(["license"], Env(console)));
    }

    [Fact]
    public void EndOfInputInSession_ExitsWithOne()
    {
        var console = new ScriptedConsole();

        var exit = ToolApp.Run([], Env(console), env =>
        {
            new Questions(env.Console).AskText("Name", "text", _ => null);
            return 0;
        });

        Assert.Equal(1, exit);
    }

    private static string VersionFromProps()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Version.props")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var doc = XDocument.Load(Path.Combine(dir.FullName, "Version.props"));
        return doc.Descendants("Version").Single().Value.Trim();
    }
}
