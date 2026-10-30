using System.CommandLine;
using System.Globalization;
using System.Text;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Commands;

/// <summary>
/// <c>ucl setup</c>: creates an installation, only with a person answering at a terminal (PDR-0030). Every
/// answer is collected before anything is written.
/// </summary>
internal static class SetupCommand
{
    /// <summary>The shortest passphrase accepted, in Unicode code points (NIST SP 800-63B rev 4).</summary>
    public const int MinimumPassphraseLength = 15;

    private const string RefusedNothingCreated = "Nothing was created.";

    public static Command Build(ToolHost host)
    {
        var command = new Command("setup", "Create a vendor installation (a person at a terminal answers the questions).");
        command.Validators.Add(result =>
        {
            // Setup takes no answer from options: the folder is asked, the passphrase typed.
            if (result.GetResult(host.Globals.Folder) is not null)
            {
                result.AddError("setup does not take --folder: it asks for the folder.");
            }

            if (result.GetResult(host.Globals.PassphraseStdin) is not null)
            {
                result.AddError("setup does not take --passphrase-stdin: the passphrase is typed twice at the terminal.");
            }
        });
        host.SetAction(command, context =>
        {
            Run(context);
            return ExitCodes.Done;
        });
        return command;
    }

    /// <summary>Runs setup in full or refuses. Also what an accepted setup offer runs.</summary>
    /// <exception cref="ToolRefusal">Nobody to ask, input ended, or the folder already holds an installation.</exception>
    public static void Run(ToolContext context)
    {
        if (!context.Interactive)
        {
            throw new ToolRefusal($"Setup is run by a person at a terminal, answering its questions. {RefusedNothingCreated}");
        }

        var console = context.Console;
        console.Error.WriteLine("Setting up a vendor installation. Nothing is written until every question is answered.");

        var folder = AskFolder(context);
        if (Installation.Exists(folder))
        {
            throw new ToolRefusal($"{folder} already holds an installation. Setup changed nothing there.");
        }

        var passphrase = AskPassphrase(context);
        var prefix = AskPrefix(context);
        var shared = AskSigningKeyMode(context);
        var standby = AskStandby(context, shared);

        Write(context, folder, passphrase, prefix, shared, standby);
        WriteClosingMessage(context, folder, shared);
    }

    private static string AskFolder(ToolContext context)
    {
        var console = context.Console;
        var environment = context.Environment;
        var current = Path.GetFullPath(environment.CurrentDirectory);
        var offers = new List<(string Path, string Label)>();
        if (!string.IsNullOrWhiteSpace(environment.Home))
        {
            var proposed = Path.GetFullPath(Path.Combine(environment.Home, Installation.HomeFolderName));
            offers.Add((proposed, Installation.PathComparer.Equals(proposed, current) ? "proposed, also the current folder" : "proposed"));
        }
        else
        {
            console.Error.WriteLine($"The home folder could not be determined, so {Installation.HomeFolderName} under it is not offered.");
        }

        if (!offers.Any(o => Installation.PathComparer.Equals(o.Path, current)))
        {
            offers.Add((current, "current folder"));
        }

        console.Error.WriteLine("Where should the installation live?");
        for (var i = 0; i < offers.Count; i++)
        {
            console.Error.WriteLine($"  {i + 1}) {offers[i].Path} ({offers[i].Label})");
        }

        if (!string.IsNullOrWhiteSpace(environment.Home) && Installation.PathComparer.Equals(current, environment.Home))
        {
            console.Error.WriteLine(
                $"Warning: the current folder is your home folder. The installation's files ({Installation.DatabaseFileName} and its journal files) would sit loose in it.");
        }

        while (true)
        {
            var answer = Ask(context, $"Folder [1-{offers.Count}, default 1]: ").Trim();
            if (answer.Length == 0)
            {
                return offers[0].Path;
            }

            if (int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number >= 1 && number <= offers.Count)
            {
                return offers[number - 1].Path;
            }

            console.Error.WriteLine($"Answer with a number from 1 to {offers.Count}.");
        }
    }

    private static string AskPassphrase(ToolContext context)
    {
        var console = context.Console;
        console.Error.WriteLine(
            $"Passphrase: protects the private signing keys. At least {MinimumPassphraseLength} characters; keep it in a password manager. If it is lost the keys cannot be recovered.");
        while (true)
        {
            var first = context.PromptHidden("Passphrase: ");
            if (KeyProtection.Normalize(first).EnumerateRunes().Count() < MinimumPassphraseLength)
            {
                console.Error.WriteLine($"The passphrase must be at least {MinimumPassphraseLength} characters. Try again.");
                continue;
            }

            var second = context.PromptHidden("Passphrase again: ");
            if (!string.Equals(KeyProtection.Normalize(first), KeyProtection.Normalize(second), StringComparison.Ordinal))
            {
                console.Error.WriteLine("The two passphrases differ. Try again.");
                continue;
            }

            return first;
        }
    }

    private static string AskPrefix(ToolContext context)
    {
        while (true)
        {
            var answer = Ask(
                context,
                "Key prefix for license references [LIC]: your own, or 'none' for no prefix: ").Trim();
            if (answer.Length == 0)
            {
                return KeyPrefix.Default.Value;
            }

            if (PrefixInput.TryParse(answer, out var prefix))
            {
                return prefix;
            }

            context.Console.Error.WriteLine($"{PrefixInput.RuleText} Try again.");
        }
    }

    private static bool AskSigningKeyMode(ToolContext context)
    {
        context.Console.Error.WriteLine("Signing keys:");
        context.Console.Error.WriteLine("  1) one signing key per product (proposed)");
        context.Console.Error.WriteLine("  2) one signing key shared across all products");
        while (true)
        {
            var answer = Ask(context, "Signing keys [1-2, default 1]: ").Trim();
            switch (answer)
            {
                case "" or "1":
                    return false;
                case "2":
                    return true;
                default:
                    context.Console.Error.WriteLine("Answer 1 or 2.");
                    break;
            }
        }
    }

    private static bool AskStandby(ToolContext context, bool shared)
    {
        var subject = shared ? "the shared signing key" : "each signing key";
        while (true)
        {
            var answer = Ask(context, $"Create a standby key with {subject} (recommended)? [Y/n]: ").Trim();
            if (answer.Length == 0 || answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (answer.Equals("n", StringComparison.OrdinalIgnoreCase) || answer.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            context.Console.Error.WriteLine("Answer y or n.");
        }
    }

    /// <summary>Asks one question; end of input refuses, so no question loops forever.</summary>
    private static string Ask(ToolContext context, string prompt) =>
        context.Console.Prompt(prompt)
        ?? throw new ToolRefusal($"Input ended before setup was complete. {RefusedNothingCreated}");

    private static void Write(ToolContext context, string folder, string passphrase, string prefix, bool shared, bool standby)
    {
        var folderCreated = !Directory.Exists(folder);
        var databaseCreated = false;
        try
        {
            // Atomic: an installation made by someone else while the questions were answered is refused,
            // and is never deleted by the cleanup below.
            Installation.CreateNew(folder);
            databaseCreated = true;
            WriteInstallation(context, folder, passphrase, prefix, shared, standby);
        }
        catch
        {
            RemoveWhatThisRunCreated(folder, folderCreated, databaseCreated);
            throw;
        }
    }

    private static void WriteInstallation(ToolContext context, string folder, string passphrase, string prefix, bool shared, bool standby)
    {
        var iterations = context.Environment.Pbkdf2Iterations;
        var now = context.Environment.TimeProvider.GetUtcNow();
        using var db = Installation.Open(folder);
        new InstallationSettings(prefix, shared, standby).AddTo(db);
        KeyProtection.StoreCheck(db, passphrase, iterations);
        if (shared)
        {
            SigningKeyStore.Add(db, null, SigningKeyRole.Active, passphrase, iterations, now);
            if (standby)
            {
                SigningKeyStore.Add(db, null, SigningKeyRole.Standby, passphrase, iterations, now);
            }
        }

        // One SaveChanges is one transaction: settings, check and keys all land or none do.
        db.SaveChanges();
    }

    /// <summary>Removes the database only when this run created it (and its journal files), then the folder when this run made it and it is empty.</summary>
    private static void RemoveWhatThisRunCreated(string folder, bool folderCreated, bool databaseCreated)
    {
        try
        {
            if (databaseCreated)
            {
                foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
                {
                    File.Delete(Path.Combine(folder, Installation.DatabaseFileName + suffix));
                }
            }

            if (folderCreated && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the original failure is the one to report.
        }
    }

    private static void WriteClosingMessage(ToolContext context, string folder, bool shared)
    {
        var text = new StringBuilder();
        text.AppendLine();
        text.AppendLine($"Setup finished. The installation is in {folder}");
        text.AppendLine("It holds:");
        text.AppendLine("  - the private signing keys, encrypted with your passphrase;");
        text.AppendLine("  - the license records;");
        text.AppendLine("  - customer emails and order references, which are not encrypted.");
        text.AppendLine("Back this folder up. Keep it out of folders synced to cloud storage.");
        text.AppendLine("Keep the passphrase in a password manager, apart from the backups. If it is lost the private keys cannot be recovered.");
        text.AppendLine();
        text.AppendLine("Next steps:");
        text.AppendLine("  1. Add a product:");
        text.AppendLine("       ucl product add <product-id> --name <name>");
        if (!shared)
        {
            text.AppendLine("     This creates the product's signing key (and its standby key when standby keys are on) and asks for the passphrase.");
        }

        text.AppendLine("  2. Print the public key lines the product's release must trust:");
        text.AppendLine("       ucl key trusted <product-id>");
        context.Console.Error.Write(text.ToString());
    }
}
