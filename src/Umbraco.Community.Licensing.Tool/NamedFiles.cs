namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Path questions for files the vendor names (ADR-0019). Relative paths resolve against
/// <see cref="ToolEnvironment.CurrentDirectory"/>, never the process directory, and every path is
/// shown in full. A typed path that is <c>~</c> alone or starts with <c>~/</c> (or <c>~\</c> on Windows) is read
/// from <see cref="ToolEnvironment.Home"/>; every other <c>~</c> is part of the name (PDR-0061). Nothing here
/// replaces a file, and a folder is created only after the vendor says yes to creating it (PDR-0062).
/// </summary>
/// <param name="environment">Supplies the current directory the answers resolve against.</param>
/// <param name="questions">Asks the path questions, so <c>/cancel</c> and re-asking behave as everywhere else.</param>
/// <param name="betweenCheckAndWrite">Test seam, called with the path after an output path passes its check and before it is opened.</param>
internal sealed class NamedFiles(ToolEnvironment environment, Questions questions, Action<string>? betweenCheckAndWrite = null)
{
    private const string Accepts = "a file path";

    /// <summary>
    /// Asks for a file to write, until the path is not an existing file or folder and its parent folder
    /// exists. When the parent folder is missing, names it in full and asks <c>Create it?</c>: yes creates it
    /// with every missing level, no asks for the path again, <c>/cancel</c> ends the action with nothing created.
    /// Returns the full path; no file is written.
    /// </summary>
    public string AskOutputPath(string question)
    {
        while (true)
        {
            var path = Resolve(questions.AskText(question, Accepts, rule: CheckOutput).Value);
            var parent = Path.GetDirectoryName(path);
            if (parent is null || Directory.Exists(parent))
                return path;

            environment.Console.WriteLine($"The folder {parent} does not exist.");
            if (questions.AskYesNo("Create it?", proposed: true))
            {
                Directory.CreateDirectory(parent);
                return path;
            }
        }
    }

    /// <summary>
    /// Asks for a file to write and writes it, opened with <see cref="FileMode.CreateNew"/> so an
    /// existing file is never replaced. When the file appears between the check and the open, the file
    /// is left as it is, the tool says so showing the full path, and the question is asked again.
    /// If <paramref name="write"/> throws, the file this call created is removed and the exception propagates.
    /// Returns the full path written.
    /// </summary>
    public string AskAndWrite(string question, Action<Stream> write)
    {
        while (true)
        {
            var path = AskOutputPath(question);
            betweenCheckAndWrite?.Invoke(path);
            if (TryWriteNew(path, write))
                return path;

            environment.Console.WriteLine(Directory.Exists(path) ? FolderMessage(path) : ExistsMessage(path));
        }
    }

    /// <summary>
    /// Creates <paramref name="path"/> with <see cref="FileMode.CreateNew"/> and passes the stream to
    /// <paramref name="write"/>. Returns false, changing nothing, when the file already exists or the path is now a folder.
    /// </summary>
    public static bool TryWriteNew(string path, Action<Stream> write)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(path) || Directory.Exists(path))
        {
            return false;
        }
        catch (UnauthorizedAccessException) when (Directory.Exists(path))
        {
            return false; // became a folder between the check and the open
        }

        try
        {
            using (stream)
                write(stream);
        }
        catch
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }

        return true;
    }

    /// <summary>Asks for a file to read, until it exists and is not a folder. Returns the full path.</summary>
    public string AskImportPath(string question) =>
        Resolve(questions.AskText(question, Accepts, rule: CheckImport).Value);

    private string Resolve(string answer) => Path.GetFullPath(ExpandHome(answer), environment.CurrentDirectory);

    /// <summary>Replaces a leading <c>~</c> that stands for the home folder; the home folder is never read from an environment variable (ADR-0017).</summary>
    private string ExpandHome(string answer)
    {
        if (answer == "~")
            return environment.Home;
        if (answer.StartsWith("~/", StringComparison.Ordinal) || (OperatingSystem.IsWindows() && answer.StartsWith("~\\", StringComparison.Ordinal)))
            return environment.Home + answer[1..];
        return answer;
    }

    private string? CheckOutput(string answer)
    {
        if (!TryFull(answer, out var full, out var broken))
            return broken;

        if (File.Exists(full))
            return ExistsMessage(full);
        if (Directory.Exists(full))
            return FolderMessage(full);
        return null;
    }

    private string? CheckImport(string answer)
    {
        if (!TryFull(answer, out var full, out var broken))
            return broken;

        if (File.Exists(full))
            return null;
        if (Directory.Exists(full))
            return $"{full} is a folder, not a file. Give the path of the file.";
        return $"The file {full} does not exist. Give the path again.";
    }

    private bool TryFull(string answer, out string full, out string? broken)
    {
        full = "";
        broken = null;
        if (answer.Length == 0)
        {
            broken = "A path is required.";
            return false;
        }

        try
        {
            full = Resolve(answer);
            return true;
        }
        catch (ArgumentException)
        {
            broken = $"'{answer}' is not a valid path.";
            return false;
        }
    }

    private static string FolderMessage(string full) => $"{full} is a folder. Give a file name.";

    private static string ExistsMessage(string full) =>
        $"The file {full} already exists and is not replaced. Give another name.";
}
