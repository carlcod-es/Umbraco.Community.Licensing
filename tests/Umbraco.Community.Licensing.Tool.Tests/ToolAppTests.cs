namespace Umbraco.Community.Licensing.Tool.Tests;

public class ToolAppTests
{
    [Fact]
    public void Missing_input_with_nobody_present_stdin_redirected()
    {
        using var tool = new ScriptedTool(stdinRedirected: true);

        var exit = tool.Run("stub-issue");

        Assert.Equal(1, exit);
        Assert.Contains("--expires", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void Missing_input_with_nobody_present_no_input_at_a_terminal()
    {
        using var tool = new ScriptedTool(stdin: "2027-03-01\n", stdinRedirected: false);

        var exit = tool.Run("stub-issue", "--no-input");

        Assert.Equal(1, exit);
        Assert.Contains("--expires", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void Prompted_by_hand()
    {
        using var tool = new ScriptedTool(stdin: "2027-03-01\n", stdinRedirected: false);

        var exit = tool.Run("stub-issue");

        Assert.Equal(0, exit);
        Assert.Contains("Expiry", tool.Stderr);
        Assert.Equal("2027-03-01" + System.Environment.NewLine, tool.Stdout);
    }

    [Fact]
    public void Empty_answer_at_a_prompt_is_a_refusal()
    {
        using var tool = new ScriptedTool(stdin: "\n", stdinRedirected: false);

        Assert.Equal(1, tool.Run("stub-issue"));
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void Given_input_asks_nothing()
    {
        using var tool = new ScriptedTool(stdinRedirected: true);

        var exit = tool.Run("stub-issue", "--expires", "perpetual");

        Assert.Equal(0, exit);
        Assert.Equal("perpetual" + System.Environment.NewLine, tool.Stdout);
        Assert.Equal("", tool.Stderr);
    }

    [Fact]
    public void Global_options_are_accepted_after_a_subcommand()
    {
        using var tool = new ScriptedTool();

        var exit = tool.Run("stub-issue", "--expires", "perpetual", "--folder", "x", "--passphrase-stdin", "--no-input");

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Unknown_option_is_a_usage_error()
    {
        using var tool = new ScriptedTool();

        Assert.Equal(2, tool.Run("--bogus"));
        Assert.Equal("", tool.Stdout);
        Assert.Contains("--bogus", tool.Stderr);
    }

    [Fact]
    public void Unknown_command_is_a_usage_error()
    {
        using var tool = new ScriptedTool();
        Assert.Equal(2, tool.Run("nope"));
    }

    [Fact]
    public void Version_prints_the_bare_Version_props_version_to_stdout()
    {
        using var tool = new ScriptedTool();

        var exit = tool.Run("--version");

        Assert.Equal(0, exit);
        Assert.Equal(VersionTests.RepoVersion, tool.Stdout.Trim());
    }

    [Fact]
    public void Help_does_not_list_the_stub_only_when_not_registered()
    {
        using var tool = new ScriptedTool();

        // The stub is registered only through the test overload; production has no such command.
        var exit = ToolApp.Run(["--help"], tool.Environment);

        Assert.Equal(0, exit);
        Assert.DoesNotContain("stub-issue", tool.Stdout);
    }

    [Fact]
    public void Help_shows_ucl_as_the_usage_name_and_lists_folder()
    {
        using var tool = new ScriptedTool();

        var exit = tool.Run("--help");

        Assert.Equal(0, exit);
        Assert.Contains("ucl", tool.Stdout);
        Assert.Contains("folder", tool.Stdout);
        Assert.DoesNotContain("Umbraco.Community.Licensing.Tool ", tool.Stdout);
    }

    [Fact]
    public void Hidden_read_does_not_echo_and_honours_backspace()
    {
        using var tool = new ScriptedTool(stdinRedirected: false, hiddenKeys: "abx\bc\n");

        var exit = tool.Run("stub-hidden");

        Assert.Equal(0, exit);
        Assert.Equal("abc" + System.Environment.NewLine, tool.Stdout);
        Assert.StartsWith("Passphrase: ", tool.Stderr);
        Assert.DoesNotContain("abc", tool.Stderr);
    }

    [Fact]
    public void Hidden_read_never_falls_back_to_stdin_when_redirected()
    {
        using var tool = new ScriptedTool(stdin: "from-stdin\n", stdinRedirected: true);

        // System.CommandLine's default handler reports the programming error and exits 1.
        Assert.Equal(1, tool.Run("stub-hidden"));
        Assert.Contains("interactive session", tool.Stderr);
        Assert.Throws<InvalidOperationException>(() => tool.Environment.Console.PromptHidden("x", noInput: false));
        Assert.Equal("", tool.Stdout);
        Assert.DoesNotContain("Passphrase: ", tool.Stderr);
    }

    [Fact]
    public void Hidden_read_is_refused_under_no_input_at_a_terminal()
    {
        // No keys are scripted and no prompt may be written: the guard fires first.
        using var tool = new ScriptedTool(stdinRedirected: false);

        Assert.Equal(1, tool.Run("stub-hidden", "--no-input"));
        Assert.Contains("interactive session", tool.Stderr);
        Assert.Equal("", tool.Stdout);
        Assert.DoesNotContain("Passphrase: ", tool.Stderr);
    }

    [Fact]
    public void Several_missing_inputs_with_nobody_present_are_refused_once_naming_each()
    {
        using var tool = new ScriptedTool(stdinRedirected: true);

        var exit = tool.Run("stub-two");

        Assert.Equal(1, exit);
        Assert.Contains("--product", tool.Stderr);
        Assert.Contains("--customer", tool.Stderr);
        Assert.Single(tool.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void Only_the_missing_input_is_named_when_the_other_is_given()
    {
        using var tool = new ScriptedTool(stdinRedirected: true);

        Assert.Equal(1, tool.Run("stub-two", "--product", "p"));
        Assert.Contains("--customer", tool.Stderr);
        Assert.DoesNotContain("--product", tool.Stderr);
    }

    [Fact]
    public void Several_missing_inputs_are_prompted_in_turn_by_hand()
    {
        using var tool = new ScriptedTool(stdin: " widget \nacme\n", stdinRedirected: false);

        var exit = tool.Run("stub-two");

        Assert.Equal(0, exit);
        Assert.Equal("widget,acme" + System.Environment.NewLine, tool.Stdout);
        Assert.Contains("Product", tool.Stderr);
        Assert.Contains("Customer", tool.Stderr);
    }

    [Fact]
    public void Given_and_prompted_values_are_trimmed_alike()
    {
        using var given = new ScriptedTool();
        Assert.Equal(0, given.Run("stub-issue", "--expires", "  perpetual "));
        Assert.Equal("perpetual" + System.Environment.NewLine, given.Stdout);

        using var prompted = new ScriptedTool(stdin: "  perpetual \n", stdinRedirected: false);
        Assert.Equal(0, prompted.Run("stub-issue"));
        Assert.Equal("perpetual" + System.Environment.NewLine, prompted.Stdout);
    }

    [Fact]
    public void Whitespace_only_given_value_is_missing()
    {
        using var tool = new ScriptedTool();

        Assert.Equal(1, tool.Run("stub-issue", "--expires", "   "));
        Assert.Contains("--expires", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }
}
