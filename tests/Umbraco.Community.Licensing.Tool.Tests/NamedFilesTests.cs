using System.Text;

namespace Umbraco.Community.Licensing.Tool.Tests;

public sealed class NamedFilesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ucl-named-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string P(params string[] parts) => Path.Combine([_dir, .. parts]);

    private (NamedFiles N, ScriptedConsole C) Make(ScriptedConsole c, Action<string>? between = null)
    {
        var env = new ToolEnvironment(c, new FakeTimeProvider(), "h", _dir, 1);
        return (new NamedFiles(env, new Questions(c), between), c);
    }

    private static void Text(string s, Stream o) => o.Write(Encoding.ASCII.GetBytes(s));

    [Fact]
    public void Output_ExistingFile_SaysSoWithFullPathAsksAgain_FileUnchanged()
    {
        var existing = P("a.zip");
        File.WriteAllText(existing, "old");
        var (n, c) = Make(new ScriptedConsole().Line(existing).Line(P("b.zip")));

        var path = n.AskAndWrite("Out", s => Text("new", s));

        Assert.Equal(P("b.zip"), path);
        Assert.Equal("old", File.ReadAllText(existing));
        Assert.Equal("new", File.ReadAllText(path));
        Assert.Contains($"The file {existing} already exists", c.Out);
        Assert.Equal(2, CountOf(c.Out, "Out ("));
    }

    [Fact]
    public void Output_FolderPath_SaysSoWithFullPathAsksAgain()
    {
        Directory.CreateDirectory(P("sub"));
        var (n, c) = Make(new ScriptedConsole().Line(P("sub")).Line(P("ok.zip")));

        Assert.Equal(P("ok.zip"), n.AskOutputPath("Out"));
        Assert.Contains($"{P("sub")} is a folder", c.Out);
    }

    [Fact]
    public void Output_MissingParentFolder_SaysSoWithFullPathAsksAgain_CreatesNoFolder()
    {
        var (n, c) = Make(new ScriptedConsole().Line(P("Dropbx", "x.zip")).Line(P("x.zip")));

        Assert.Equal(P("x.zip"), n.AskOutputPath("Out"));
        Assert.Contains($"The folder {P("Dropbx")} does not exist", c.Out);
        Assert.False(Directory.Exists(P("Dropbx")));
    }

    [Fact]
    public void Output_RelativePath_ResolvesAgainstCurrentDirectory()
    {
        var (n, _) = Make(new ScriptedConsole().Line("rel.zip"));

        var path = n.AskAndWrite("Out", s => Text("x", s));

        Assert.Equal(P("rel.zip"), path);
        Assert.True(File.Exists(P("rel.zip")));
    }

    [Fact]
    public void Output_AcceptedPath_WrittenWithCreateNew()
    {
        var (n, _) = Make(new ScriptedConsole().Line(P("k.txt")));

        n.AskAndWrite("Out", s => Text("KEY", s));

        Assert.Equal("KEY", File.ReadAllText(P("k.txt")));
    }

    [Fact]
    public void Output_FileCreatedBetweenCheckAndWrite_NotReplacedAndAskedAgain()
    {
        var raced = P("race.zip");
        var created = false;
        var (n, c) = Make(
            new ScriptedConsole().Line(raced).Line(P("next.zip")),
            p =>
            {
                if (!created)
                {
                    created = true;
                    File.WriteAllText(p, "theirs");
                }
            });

        var path = n.AskAndWrite("Out", s => Text("mine", s));

        Assert.Equal("theirs", File.ReadAllText(raced));
        Assert.Equal(P("next.zip"), path);
        Assert.Equal("mine", File.ReadAllText(path));
        Assert.Contains($"The file {raced} already exists", c.Out);
    }

    [Fact]
    public void Output_PathBecomesAFolderBetweenCheckAndWrite_SaysSoAndAsksAgain()
    {
        var raced = P("race.txt");
        var created = false;
        var (n, c) = Make(
            new ScriptedConsole().Line(raced).Line(P("next.txt")),
            p =>
            {
                if (!created)
                {
                    created = true;
                    Directory.CreateDirectory(p);
                }
            });

        var path = n.AskAndWrite("Out", s => Text("mine", s));

        Assert.True(Directory.Exists(raced));
        Assert.Equal(P("next.txt"), path);
        Assert.Equal("mine", File.ReadAllText(path));
        Assert.Contains($"{raced} is a folder", c.Out);
    }

    [Fact]
    public void TryWriteNew_ExistingFile_ReturnsFalseAndLeavesIt()
    {
        File.WriteAllText(P("e"), "old");

        Assert.False(NamedFiles.TryWriteNew(P("e"), s => Text("new", s)));
        Assert.Equal("old", File.ReadAllText(P("e")));
    }

    [Fact]
    public void Write_Throws_RemovesTheFileItCreated()
    {
        Assert.Throws<InvalidOperationException>(() =>
            NamedFiles.TryWriteNew(P("t"), _ => throw new InvalidOperationException()));
        Assert.False(File.Exists(P("t")));
    }

    [Fact]
    public void Output_Cancel_ThrowsAndWritesNothing()
    {
        var (n, _) = Make(new ScriptedConsole().Line("/cancel"));

        Assert.Throws<ActionCancelledException>(() => n.AskAndWrite("Out", s => Text("x", s)));
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public void Import_MissingFile_SaysSoWithFullPathAsksAgain_CreatesNothing()
    {
        File.WriteAllText(P("good.zip"), "z");
        var (n, c) = Make(new ScriptedConsole().Line(P("gone.zip")).Line(P("good.zip")));

        Assert.Equal(P("good.zip"), n.AskImportPath("In"));
        Assert.Contains($"The file {P("gone.zip")} does not exist", c.Out);
        Assert.False(File.Exists(P("gone.zip")));
    }

    [Fact]
    public void Import_FolderPath_SaysSoWithFullPathAsksAgain()
    {
        Directory.CreateDirectory(P("sub"));
        File.WriteAllText(P("good.zip"), "z");
        var (n, c) = Make(new ScriptedConsole().Line(P("sub")).Line("good.zip"));

        Assert.Equal(P("good.zip"), n.AskImportPath("In"));
        Assert.Contains($"{P("sub")} is a folder", c.Out);
    }

    [Fact]
    public void Import_ExistingFile_Accepted()
    {
        File.WriteAllText(P("a.zip"), "z");
        var (n, _) = Make(new ScriptedConsole().Line(P("a.zip")));

        Assert.Equal(P("a.zip"), n.AskImportPath("In"));
    }

    [Fact]
    public void Import_Cancel_Throws()
    {
        var (n, _) = Make(new ScriptedConsole().Line("/cancel"));

        Assert.Throws<ActionCancelledException>(() => n.AskImportPath("In"));
    }

    [Fact]
    public void EmptyAnswer_IsRefused()
    {
        var (n, c) = Make(new ScriptedConsole().Line("").Line(P("a")));

        n.AskOutputPath("Out");
        Assert.Contains("A path is required.", c.Out);
    }

    private static int CountOf(string text, string s) =>
        (text.Length - text.Replace(s, "").Length) / s.Length;
}
