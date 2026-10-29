namespace Umbraco.Community.Licensing.Tool.Tests;

public class FolderTests
{
    private static readonly string NewLine = System.Environment.NewLine;

    private static void AssertUsing(ScriptedTool tool, string folder)
    {
        Assert.Contains($"Using {folder}{NewLine}", tool.Stderr);
        Assert.DoesNotContain("Using", tool.Stdout);
    }

    [Fact]
    public void Current_folder_used()
    {
        using var tool = new ScriptedTool();
        var cwd = ScriptedTool.MakeInstallation(tool.Environment.CurrentDirectory);

        var exit = tool.Run("folder", "show");

        Assert.Equal(0, exit);
        Assert.Equal(cwd + NewLine, tool.Stdout);
        AssertUsing(tool, cwd);
    }

    [Fact]
    public void Home_installation_used_elsewhere()
    {
        using var tool = new ScriptedTool();
        Directory.CreateDirectory(tool.Environment.CurrentDirectory);
        var home = ScriptedTool.MakeInstallation(tool.HomeInstallation);

        var exit = tool.Run("folder", "show");

        Assert.Equal(0, exit);
        Assert.Equal(home + NewLine, tool.Stdout);
        AssertUsing(tool, home);
    }

    [Fact]
    public void Current_folder_beats_home_installation()
    {
        using var tool = new ScriptedTool();
        var cwd = ScriptedTool.MakeInstallation(tool.Environment.CurrentDirectory);
        ScriptedTool.MakeInstallation(tool.HomeInstallation);

        Assert.Equal(0, tool.Run("folder", "show"));
        Assert.Equal(cwd + NewLine, tool.Stdout);
    }

    [Fact]
    public void Parent_of_current_folder_is_not_searched()
    {
        using var tool = new ScriptedTool();
        ScriptedTool.MakeInstallation(tool.Environment.CurrentDirectory);
        var child = Path.Combine(tool.Environment.CurrentDirectory, "child");
        Directory.CreateDirectory(child);
        var inChild = tool.Environment with { CurrentDirectory = child };

        var exit = ToolApp.Run(["folder", "show"], inChild);

        Assert.Equal(1, exit);
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void Folder_named_on_the_command_wins()
    {
        using var tool = new ScriptedTool();
        ScriptedTool.MakeInstallation(tool.Environment.CurrentDirectory);
        ScriptedTool.MakeInstallation(tool.HomeInstallation);
        var named = ScriptedTool.MakeInstallation(Path.Combine(Path.GetDirectoryName(tool.Environment.Home)!, "named"));

        var exit = tool.Run("folder", "show", "--folder", named);

        Assert.Equal(0, exit);
        Assert.Equal(named + NewLine, tool.Stdout);
        AssertUsing(tool, named);
    }

    [Fact]
    public void Relative_folder_is_resolved_against_the_environment_current_directory()
    {
        using var tool = new ScriptedTool();
        var expected = ScriptedTool.MakeInstallation(Path.Combine(tool.Environment.CurrentDirectory, "vendor"));
        // The current directory itself is not an installation; --folder reaches the subfolder.

        var exit = tool.Run("folder", "show", "--folder", "vendor");

        Assert.Equal(0, exit);
        Assert.Equal(expected + NewLine, tool.Stdout);
        AssertUsing(tool, expected);
    }

    [Fact]
    public void Named_folder_without_an_installation_is_refused_naming_only_that_folder()
    {
        using var tool = new ScriptedTool();
        ScriptedTool.MakeInstallation(tool.Environment.CurrentDirectory);
        ScriptedTool.MakeInstallation(tool.HomeInstallation);
        var empty = Path.GetFullPath(Path.Combine(tool.Environment.CurrentDirectory, "empty"));

        var exit = tool.Run("folder", "show", "--folder", "empty");

        Assert.Equal(1, exit);
        Assert.Contains(empty, tool.Stderr);
        Assert.DoesNotContain(tool.HomeInstallation, tool.Stderr);
        Assert.DoesNotContain("Using", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void No_installation_found_declined()
    {
        var setupCalls = 0;
        using var tool = new ScriptedTool(stdin: "n\n", stdinRedirected: false, setup: _ => setupCalls++);

        var exit = tool.Run("folder", "show");

        Assert.Equal(1, exit);
        Assert.Equal(0, setupCalls);
        Assert.Contains(Path.GetFullPath(tool.Environment.CurrentDirectory), tool.Stderr);
        Assert.Contains(Path.GetFullPath(tool.HomeInstallation), tool.Stderr);
        Assert.Contains("Run setup now? [y/N]", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }

    [Theory]
    [InlineData("")]
    [InlineData("maybe")]
    public void No_installation_found_anything_but_yes_declines(string answer)
    {
        var setupCalls = 0;
        using var tool = new ScriptedTool(stdin: answer + "\n", stdinRedirected: false, setup: _ => setupCalls++);

        Assert.Equal(1, tool.Run("folder", "show"));
        Assert.Equal(0, setupCalls);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("YES")]
    public void No_installation_found_accepted_starts_setup_then_carries_on(string answer)
    {
        var setupCalls = 0;
        using var tool = new ScriptedTool(
            stdin: answer + "\n",
            stdinRedirected: false,
            setup: context =>
            {
                setupCalls++;
                ScriptedTool.MakeInstallation(Path.Combine(context.Environment.Home, Installation.HomeFolderName));
            });

        var exit = tool.Run("folder", "show");

        Assert.Equal(0, exit);
        Assert.Equal(1, setupCalls);
        var home = Path.GetFullPath(tool.HomeInstallation);
        Assert.Equal(home + NewLine, tool.Stdout);
        AssertUsing(tool, home);
    }

    [Fact]
    public void No_installation_found_accepted_but_setup_unavailable_refuses()
    {
        using var tool = new ScriptedTool(stdin: "y\n", stdinRedirected: false);

        var exit = tool.Run("folder", "show");

        Assert.Equal(1, exit);
        Assert.Contains("not available", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void No_installation_found_with_nobody_present()
    {
        var setupCalls = 0;
        using var tool = new ScriptedTool(stdin: "y\n", stdinRedirected: true, setup: _ => setupCalls++);

        var exit = tool.Run("folder", "show");

        Assert.Equal(1, exit);
        Assert.Equal(0, setupCalls);
        Assert.Contains(Path.GetFullPath(tool.Environment.CurrentDirectory), tool.Stderr);
        Assert.Contains(Path.GetFullPath(tool.HomeInstallation), tool.Stderr);
        Assert.DoesNotContain("Run setup now?", tool.Stderr);
        Assert.Equal("", tool.Stdout);
        Assert.Equal("y", tool.Environment.Console.ReadLine());
    }

    [Fact]
    public void No_installation_found_under_no_input_at_a_terminal_offers_nothing()
    {
        using var tool = new ScriptedTool(stdin: "y\n", stdinRedirected: false);

        Assert.Equal(1, tool.Run("folder", "show", "--no-input"));
        Assert.DoesNotContain("Run setup now?", tool.Stderr);
        Assert.Equal("y", tool.Environment.Console.ReadLine());
    }
}
