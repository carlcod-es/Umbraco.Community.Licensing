using System.CommandLine;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>The recursive options available to every command.</summary>
internal sealed class GlobalOptions
{
    public Option<string?> Folder { get; } = new("--folder")
    {
        Description = "The vendor folder to use.",
        Recursive = true,
    };

    public Option<bool> PassphraseStdin { get; } = new("--passphrase-stdin")
    {
        Description = "Read the passphrase from the first line of standard input.",
        Recursive = true,
    };

    public Option<bool> NoInput { get; } = new("--no-input")
    {
        Description = "Never prompt, even at a terminal; a missing input is a refusal.",
        Recursive = true,
    };

    public void AddTo(Command root)
    {
        root.Options.Add(Folder);
        root.Options.Add(PassphraseStdin);
        root.Options.Add(NoInput);
    }
}
