using System.CommandLine;
using Umbraco.Community.Licensing.Tool.Data;

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
    /// <summary>The environment variable that supplies the passphrase to scripts.</summary>
    public const string PassphraseVariable = "UCL_PASSPHRASE";

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
    public string PromptHidden(string text) => Console.PromptHidden(text, NoInput);

    /// <summary>
    /// Reads the passphrase, first found of: <c>--passphrase-stdin</c> (first line of stdin, read as UTF-8; an
    /// empty line or end of input is refused, never falling through), the <c>UCL_PASSPHRASE</c> variable, a
    /// hidden prompt when interactive (ADR-0012). It is never trimmed (a line ending is removed by the line
    /// read only); an empty value counts as missing. Not cached: the caller holds it for one command.
    /// </summary>
    /// <exception cref="ToolRefusal">
    /// <c>--passphrase-stdin</c> at a terminal (reading a line would echo it) or with no passphrase on stdin,
    /// or no source supplied one and nobody can be asked.
    /// </exception>
    public string ReadPassphrase()
    {
        if (PassphraseStdin)
        {
            if (!Console.InputRedirected)
            {
                throw new ToolRefusal(
                    "--passphrase-stdin needs the passphrase piped on stdin. At a terminal, leave it out to be asked.");
            }

            var line = Console.ReadLine();
            return string.IsNullOrEmpty(line)
                ? throw new ToolRefusal(
                    "--passphrase-stdin was given but no passphrase was on stdin. Nothing changed.")
                : line;
        }

        var variable = Environment.GetVariable(PassphraseVariable);
        if (!string.IsNullOrEmpty(variable))
        {
            return variable;
        }

        if (Interactive)
        {
            var typed = PromptHidden("Passphrase: ");
            if (typed.Length > 0)
            {
                return typed;
            }
        }

        throw new ToolRefusal(
            $"Passphrase needed. Pipe it with --passphrase-stdin or set {PassphraseVariable}. Nothing changed.");
    }

    /// <summary>
    /// Unlocks for an action that creates or uses a private key: reads the passphrase once and checks it
    /// against the check stored at setup, so a mistyped passphrase can never encrypt a new key. Call it before
    /// anything else changes.
    /// </summary>
    /// <returns>The verified passphrase; hold it for this command only.</returns>
    /// <exception cref="ToolRefusal">No passphrase, a wrong one, or the installation holds no passphrase check.</exception>
    public string ReadVerifiedPassphrase(ToolDbContext db)
    {
        var check = KeyProtection.LoadCheck(db)
            ?? throw new ToolRefusal("This installation has no usable passphrase check. Nothing changed.");
        var passphrase = ReadPassphrase();
        return check.Verify(passphrase)
            ? passphrase
            : throw new ToolRefusal("Wrong passphrase. Nothing changed.");
    }

    /// <summary>
    /// Resolves the vendor folder (writing <c>Using &lt;path&gt;</c>) and opens its database. The one way a
    /// command opens the database: <see cref="Installation.Open"/> creates <c>ucl.db</c> when missing, so it
    /// must only receive a resolved folder. Dispose the result.
    /// </summary>
    /// <exception cref="ToolRefusal">No installation, or a database made by a newer tool.</exception>
    public ToolDbContext OpenDatabase() => Installation.Open(UseInstallation());

    /// <summary>
    /// The step every installation-using command starts with: resolves the vendor folder, writes
    /// <c>Using &lt;path&gt;</c> to stderr and returns the full path. Resolution is the first of
    /// <c>--folder</c>, the current directory, <c>&lt;home&gt;/.ucl</c>; a folder is an installation when it
    /// holds <c>ucl.db</c>. Parents are not searched and nothing is remembered between runs.
    /// </summary>
    /// <exception cref="ToolRefusal">No installation was found; also after an accepted setup, which does not run the action.</exception>
    public string UseInstallation()
    {
        var folder = ResolveInstallation();
        Console.Error.WriteLine($"Using {folder}");
        return folder;
    }

    private string ResolveInstallation()
    {
        if (Folder is not null && string.IsNullOrWhiteSpace(Folder))
        {
            throw new ToolRefusal("--folder needs a path.");
        }

        var resolution = Installation.Resolve(Folder, Environment.CurrentDirectory, Environment.Home);
        if (resolution.Folder is not null)
        {
            return resolution.Folder;
        }

        if (resolution.NamedByOption)
        {
            throw new ToolRefusal(
                $"No installation in {resolution.Candidates[0]} (named by --folder): {Installation.DatabaseFileName} not found there.");
        }

        var candidates = resolution.Candidates;
        var message = $"No installation found. {Installation.DatabaseFileName} is not in:"
            + string.Concat(candidates.Select(c => $"{System.Environment.NewLine}  {c}"))
            + (resolution.HomeUnknown
                ? $"{System.Environment.NewLine}The home folder could not be determined, so {Installation.HomeFolderName} under it was not checked."
                : "")
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

        // Setup runs in full (or refuses); the action that found no installation does not run (Q32).
        runSetup(this);
        throw new ToolRefusal("Setup finished. Run the command again.");
    }
}
