namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The folders a new installation may be put in when the vendor is asked (setup and restore at the startup offer,
/// PDR-0052): <c>.ucl</c> in the home folder, proposed, and the current directory, both as full paths. An
/// installation never sits directly in the home folder, so when the current directory is the home folder only
/// <c>.ucl</c> is offered; a current directory that is <c>.ucl</c> itself is offered once.
/// </summary>
internal static class FolderOffer
{
    internal const string HomeId = ".ucl";
    internal const string CurrentId = "current";

    private sealed record Option(Choice Choice, string Path);

    /// <summary>The folders offered, in order, as the question's choices; the first is proposed.</summary>
    public static IReadOnlyList<Choice> Choices(ToolEnvironment environment) =>
        Options(environment).Select(o => o.Choice).ToList();

    /// <summary>Asks which folder, <c>.ucl</c> proposed, and returns its full path. <c>/cancel</c> throws <see cref="ActionCancelledException"/>.</summary>
    public static string Ask(Session session)
    {
        var options = Options(session.Environment);
        var picked = session.Questions.AskChoice("Folder for the installation", options.Select(o => o.Choice).ToList(), HomeId);
        return options.First(o => o.Choice.Id == picked.Id).Path;
    }

    private static List<Option> Options(ToolEnvironment environment)
    {
        var home = Path.GetFullPath(Path.Combine(environment.Home, ".ucl"));
        var options = new List<Option> { new(new Choice(HomeId, $"{home} (.ucl in your home folder)"), home) };

        var current = Path.GetFullPath(environment.CurrentDirectory);
        if (!Startup.SamePath(current, environment.Home) && !Startup.SamePath(current, home))
            options.Add(new Option(new Choice(CurrentId, $"{current} (the current folder)"), current));
        return options;
    }
}
