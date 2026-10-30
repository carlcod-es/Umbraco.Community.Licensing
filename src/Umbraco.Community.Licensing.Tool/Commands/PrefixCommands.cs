using System.CommandLine;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Commands;

/// <summary>The <c>prefix</c> command group: show, set. Neither needs the passphrase.</summary>
internal static class PrefixCommands
{
    public static Command Build(ToolHost host) =>
        new("prefix", "Show or change the key prefix of new licenses.")
        {
            BuildShow(host),
            BuildSet(host),
        };

    private static Command BuildShow(ToolHost host)
    {
        var command = new Command("show", "Show the key prefix (none when there is none).");
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            context.Console.Out.WriteLine(PrefixInput.Show(InstallationSettings.Load(db).Prefix));
            return ExitCodes.Done;
        });
        return command;
    }

    private static Command BuildSet(ToolHost host)
    {
        // Optional to the parser: a missing prefix is prompted or refused naming it, not a parse error.
        var prefix = new Argument<string?>("prefix")
        {
            Description = "LIC, your own prefix (1 to 16 characters of A-Z and 0-9) or none.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var command = new Command("set", "Change the key prefix. Needs no passphrase; no license record changes.") { prefix };
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var typed = context.Require("<prefix>", context.ParseResult.GetValue(prefix), "Key prefix (LIC, your own, or none)");
            if (!PrefixInput.TryParse(typed, out var chosen))
            {
                throw new ToolRefusal($"\"{typed}\" is not a key prefix. {PrefixInput.RuleText} The prefix is unchanged.");
            }

            var previous = InstallationSettings.SetPrefix(db, chosen);
            var console = context.Console;
            if (previous == chosen)
            {
                console.Error.WriteLine($"The prefix is already {PrefixInput.Show(chosen)}. Nothing changed.");
                return ExitCodes.Done;
            }

            var added = chosen.Length == 0 ? "no prefix" : $"{chosen}-";
            console.Error.WriteLine($"Prefix changed from {PrefixInput.Show(previous)} to {PrefixInput.Show(chosen)}.");
            console.Error.WriteLine($"New issues and reissues use {added}. Existing licenses keep their prefix until reissued. No record was changed.");
            console.Error.WriteLine(chosen.Length == 0
                ? "References are typed as 8F3AK-M7RXB; an earlier prefix is no longer read."
                : $"References are typed as {chosen}-8F3AK-M7RXB or 8F3AK-M7RXB; an earlier prefix is no longer read.");
            return ExitCodes.Done;
        });
        return command;
    }
}
