using System.CommandLine;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>An input a command needs: its option name, the value given (if any) and the prompt to ask with.</summary>
internal readonly record struct RequiredInput(string OptionName, string? Given, string Prompt);

/// <summary>
/// What a command sees when it runs: the environment and the parsed global options.
/// </summary>
internal sealed class ToolContext(
    ToolEnvironment environment,
    ParseResult parseResult,
    GlobalOptions globals,
    Action<ToolContext> runSetup)
{
    public ToolEnvironment Environment { get; } = environment;

    public ToolConsole Console => Environment.Console;

    /// <summary>The parse result, for reading the command's own options.</summary>
    public ParseResult ParseResult { get; } = parseResult;

    /// <summary><c>--folder</c> as given; <see cref="UseInstallation"/> resolves it.</summary>
    public string? Folder { get; } = parseResult.GetValue(globals.Folder);

    public bool PassphraseStdin { get; } = parseResult.GetValue(globals.PassphraseStdin);

    public bool NoInput { get; } = parseResult.GetValue(globals.NoInput);

    /// <summary>Whether a person can be asked: stdin not redirected and <c>--no-input</c> absent.</summary>
    public bool Interactive => Console.IsInteractive(NoInput);

    /// <summary>
    /// Obtains a required input: <paramref name="given"/> when present; otherwise, when interactive, asks
    /// with <paramref name="prompt"/> on stderr; otherwise refuses naming <paramref name="optionName"/>.
    /// Values are trimmed; an empty or whitespace-only value is missing.
    /// </summary>
    /// <exception cref="ToolRefusal">The input is missing and could not be asked for.</exception>
    public string Require(string optionName, string? given, string prompt) =>
        RequireAll(new RequiredInput(optionName, given, prompt))[0];

    /// <summary>
    /// Obtains several required inputs, in order. Given values are used as they are; each missing one is
    /// asked for in turn when interactive. Refuses once, naming every input still missing.
    /// </summary>
    /// <exception cref="ToolRefusal">One or more inputs are missing and could not be asked for.</exception>
    public string[] RequireAll(params RequiredInput[] inputs)
    {
        var values = new string?[inputs.Length];
        var missing = new List<string>();
        for (var i = 0; i < inputs.Length; i++)
        {
            var value = inputs[i].Given?.Trim();
            if (string.IsNullOrEmpty(value) && Interactive)
            {
                value = Console.Prompt($"{inputs[i].Prompt}: ")?.Trim();
            }

            if (string.IsNullOrEmpty(value))
            {
                missing.Add(inputs[i].OptionName);
            }

            values[i] = value;
        }

        if (missing.Count > 0)
        {
            throw new ToolRefusal(missing.Count == 1
                ? $"Missing {missing[0]}. Give it on the command line."
                : $"Missing {string.Join(", ", missing)}. Give them on the command line.");
        }

        return values!;
    }

    /// <summary>
    /// Reads a secret without echo. The only route to a hidden prompt, so it can never run when nobody is
    /// present.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not interactive: the caller should have refused instead.</exception>
    public string? PromptHidden(string text) =>
        Interactive
            ? Console.PromptHidden(text)
            : throw new InvalidOperationException("A hidden prompt needs an interactive session.");

    /// <summary>
    /// The step every installation-using command starts with: resolves the vendor folder, writes
    /// <c>Using &lt;path&gt;</c> to stderr and returns the full path. Resolution is the first of
    /// <c>--folder</c>, the current directory, <c>&lt;home&gt;/.ucl</c>; a folder is an installation when it
    /// holds <c>ucl.db</c>. Parents are not searched and nothing is remembered between runs.
    /// </summary>
    /// <exception cref="ToolRefusal">No installation was found (or setup, if offered, did not create one).</exception>
    public string UseInstallation()
    {
        var folder = ResolveInstallation();
        Console.Error.WriteLine($"Using {folder}");
        return folder;
    }

    private string ResolveInstallation()
    {
        if (Folder is not null)
        {
            if (string.IsNullOrWhiteSpace(Folder))
            {
                throw new ToolRefusal("--folder needs a path.");
            }

            var named = Path.GetFullPath(Folder.Trim(), Environment.CurrentDirectory);
            return Installation.Exists(named)
                ? named
                : throw new ToolRefusal(
                    $"No installation in {named} (named by --folder): {Installation.DatabaseFileName} not found there.");
        }

        var candidates = new[]
        {
            Path.GetFullPath(Environment.CurrentDirectory),
            Path.GetFullPath(Path.Combine(Environment.Home, Installation.HomeFolderName)),
        }.Distinct(StringComparer.Ordinal).ToArray();

        var found = candidates.FirstOrDefault(Installation.Exists);
        if (found is not null)
        {
            return found;
        }

        var message = $"No installation found. {Installation.DatabaseFileName} is not in:"
            + string.Concat(candidates.Select(c => $"{System.Environment.NewLine}  {c}"))
            + $"{System.Environment.NewLine}Run 'ucl setup' to create one, or name a folder with --folder.";
        if (!Interactive)
        {
            throw new ToolRefusal(message);
        }

        Console.Error.WriteLine(message);
        var answer = Console.Prompt("Run setup now? [y/N] ")?.Trim();
        if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolRefusal("Setup not run. Nothing changed.");
        }

        runSetup(this);
        return candidates.FirstOrDefault(Installation.Exists)
            ?? throw new ToolRefusal("Setup did not create an installation. Nothing changed.");
    }
}
