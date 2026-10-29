namespace Umbraco.Community.Licensing.Tool.Tests;

public class FolderResolutionTests
{
    private static string Make(TempFolder root, string name) =>
        ScriptedTool.MakeInstallation(Path.Combine(root.Path, name));

    [Fact]
    public void Folder_option_wins()
    {
        using var root = new TempFolder();
        var cwd = Make(root, "cwd");
        Make(root, "home/.ucl");
        var named = Make(root, "named");

        var result = Installation.Resolve(named, cwd, Path.Combine(root.Path, "home"));

        Assert.Equal(named, result.Folder);
        Assert.Equal([named], result.Candidates);
    }

    [Fact]
    public void Folder_option_relative_to_current_directory_and_missing_installation_reported()
    {
        using var root = new TempFolder();

        var result = Installation.Resolve("sub", root.Path, "");

        Assert.Null(result.Folder);
        Assert.True(result.NamedByOption);
        Assert.Equal([Path.Combine(root.Path, "sub")], result.Candidates);
    }

    [Fact]
    public void Current_folder_with_database_beats_home()
    {
        using var root = new TempFolder();
        var cwd = Make(root, "cwd");
        Make(root, "home/.ucl");

        var result = Installation.Resolve(null, cwd, Path.Combine(root.Path, "home"));

        Assert.Equal(cwd, result.Folder);
    }

    [Fact]
    public void Home_fallback_when_current_folder_has_none()
    {
        using var root = new TempFolder();
        Directory.CreateDirectory(Path.Combine(root.Path, "cwd"));
        var home = Make(root, "home/.ucl");

        var result = Installation.Resolve(null, Path.Combine(root.Path, "cwd"), Path.Combine(root.Path, "home"));

        Assert.Equal(home, result.Folder);
    }

    [Fact]
    public void None_lists_candidates_checked()
    {
        using var root = new TempFolder();
        var cwd = Path.Combine(root.Path, "cwd");

        var result = Installation.Resolve(null, cwd, Path.Combine(root.Path, "home"));

        Assert.Null(result.Folder);
        Assert.False(result.HomeUnknown);
        Assert.Equal([cwd, Path.Combine(root.Path, "home", ".ucl")], result.Candidates);
    }

    [Fact]
    public void Candidates_are_de_duplicated_ignoring_trailing_separators()
    {
        using var root = new TempFolder();
        var home = Path.Combine(root.Path, "home");
        var cwd = Path.Combine(home, ".ucl") + Path.DirectorySeparatorChar;

        var result = Installation.Resolve(null, cwd, home);

        Assert.Single(result.Candidates);
    }

    [Fact]
    public void Path_comparer_ignores_case_only_on_windows()
    {
        var equal = Installation.PathComparer.Equals("/a/B", "/a/b");
        Assert.Equal(OperatingSystem.IsWindows(), equal);
        Assert.True(Installation.PathComparer.Equals("/a/b/", "/a/b"));
    }

    [Fact]
    public void Empty_home_drops_the_home_candidate_and_never_uses_the_working_directory()
    {
        using var root = new TempFolder();
        var cwd = Path.Combine(root.Path, "cwd");

        var result = Installation.Resolve(null, cwd, "");

        Assert.Null(result.Folder);
        Assert.True(result.HomeUnknown);
        Assert.Equal([cwd], result.Candidates);
    }

    [Fact]
    public void Empty_home_refusal_says_the_home_folder_could_not_be_determined()
    {
        using var tool = new ScriptedTool();
        var environment = tool.Environment with { Home = "" };

        var exit = ToolApp.Run(["folder", "show"], environment);

        Assert.Equal(1, exit);
        Assert.Contains("home folder could not be determined", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }
}
