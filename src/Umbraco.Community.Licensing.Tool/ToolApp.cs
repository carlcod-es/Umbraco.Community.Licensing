using System.Reflection;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>Entry point of <c>ucl</c>: arguments, person-present check, then the session (ADR-0020).</summary>
internal static class ToolApp
{
    internal const string NotPersonMessage =
        "ucl is run by a person at a terminal: neither its input nor its output may be redirected.";

    internal const string NoCommandMessage =
        "ucl takes no command other than --help and --version; it is used from its menu. Run ucl with no arguments.";

    public static int Run(string[] args, ToolEnvironment environment) => Run(args, environment, Session);

    /// <summary>
    /// Runs with the given session entry, which opens after the argument and person-present checks.
    /// Tests pass a stub menu here.
    /// </summary>
    internal static int Run(string[] args, ToolEnvironment environment, Func<ToolEnvironment, int> session)
    {
        var console = environment.Console;

        if (args.Length == 1 && args[0] is ("--help" or "-h" or "-?"))
        {
            console.WriteLine(HelpText);
            return 0;
        }

        if (args.Length == 1 && args[0] == "--version")
        {
            console.WriteLine(Version);
            return 0;
        }

        if (args.Length > 0)
        {
            console.WriteError(NoCommandMessage);
            return 2;
        }

        if (!console.PersonPresent)
        {
            console.WriteError(NotPersonMessage);
            return 1;
        }

        try
        {
            return session(environment);
        }
        catch (EndOfInputException)
        {
            return 1;
        }
        catch (InstallationRefusedException ex)
        {
            // An installation that cannot be used is refused before the session opens: stderr, nothing on stdout (ADR-0013).
            console.WriteError(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            // Backstop (PDR-0053): by here the menu loop has disposed its session, so the passphrase is zeroed.
            // The catch is what makes that certain: an unhandled exception need not run finally blocks.
            console.WriteLine($"The session ended: {Failure.OneLine(ex)}");
            return 1;
        }
    }

    /// <summary>Startup, then the menus, with every entry a stub until its section replaces it.</summary>
    private static int Session(ToolEnvironment environment) =>
        MenuLoop.Run(environment, ActionTable.Default(), Startup.Offer.Stubs);

    /// <summary>The informational version without any <c>+</c> build metadata.</summary>
    internal static string Version
    {
        get
        {
            var version = typeof(ToolApp).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(ToolApp).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var plus = version.IndexOf('+');
            return plus < 0 ? version : version[..plus];
        }
    }

    internal static string HelpText =>
        """
        ucl - manage products, customers and license keys, and issue offline licenses.

        Run ucl with no arguments, at a terminal, to open the menu. Answer each question at the
        prompt; type /cancel at any question to cancel the action and return to the menu.

        Menu:
          1 Products      add, rename, add a feature, list
          2 Licenses      issue, reissue, renew, resend, find and list, show, customer details
          3 Signing keys  status, add a standby key, switch, trusted keys, release confirmed,
                          reissues sent, export by signing key
          4 Backup        export, restore into the current folder
          5 Settings      the folder in use, the prefix, the idle lock, a new installation in the
                          current folder
          0 Quit          (in a submenu, 0 is Back)

        The installation is the folder holding ucl.db: the current folder when it holds one,
        otherwise .ucl in your home folder. When neither does, ucl offers to set one up, restore
        one from a backup, or quit.

        Options (the only arguments ucl accepts):
          --help, -h, -?  show this help
          --version       show the version
        """;
}
