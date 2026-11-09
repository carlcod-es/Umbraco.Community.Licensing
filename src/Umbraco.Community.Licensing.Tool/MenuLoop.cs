namespace Umbraco.Community.Licensing.Tool;

/// <summary>The menu session after startup (ADR-0020): show a menu, take a number, run the entry, show the menu again.</summary>
internal static class MenuLoop
{
    /// <summary>
    /// Runs startup and then the menus; returns the exit code. Quit ends with 0. <paramref name="readIdleTime"/>
    /// is the seam for the installation's idle lock setting (null keeps the session's default, the installation's Settings).
    /// </summary>
    public static int Run(ToolEnvironment environment, ActionTable actions, Startup.Offer offer, Func<Installation, TimeSpan?>? readIdleTime = null)
    {
        // Quit, end of input and any exception leave through here: the lock is zeroed in every case (ADR-0021).
        using var session = new Session(environment);
        if (readIdleTime is not null)
            session.ReadIdleTime = readIdleTime;
        if (!Startup.Begin(session, offer))
            return 0;

        Show(session, actions, "Main menu", Menus.Main, "Quit");
        return 0;
    }

    /// <summary>
    /// Shows a menu until the vendor leaves it with 0. Entries that are actions run and return here,
    /// completed or cancelled; entries that are submenus open and return here on Back.
    /// </summary>
    private static void Show(Session session, ActionTable actions, string title, IReadOnlyList<MenuNode> nodes, string exitLabel)
    {
        while (true)
        {
            var picked = Choose(session, title, nodes, exitLabel);
            if (picked is null)
                return;

            var node = nodes[picked.Value - 1];
            if (node.Children is not null)
            {
                Show(session, actions, node.Label, node.Children, "Back");
                continue;
            }

            try
            {
                actions.Run(node.Key!, node.Label, session);
            }
            catch (ActionCancelledException)
            {
                session.Console.WriteLine("Cancelled.");
            }
            catch (Exception ex) when (Failure.IsContinuable(ex))
            {
                // Nothing was stored (each action writes in one step at the end) and the lock is untouched (PDR-0053).
                // The line names the submenu and the entry, for example "Backup - Export failed: <message>".
                var action = title == "Main menu" ? node.Label : $"{title} - {node.Label}";
                session.Console.WriteLine($"{action} failed: {Failure.OneLine(ex)}");
            }
        }
    }

    /// <summary>
    /// Shows the entries numbered from 1 and the exit entry as 0, and asks for a number until one is
    /// accepted. Returns the entry number, or null for 0. A refused answer names the rule and shows the menu again.
    /// </summary>
    internal static int? Choose(Session session, string title, IReadOnlyList<MenuNode> nodes, string exitLabel)
    {
        var console = session.Console;
        while (true)
        {
            console.WriteLine();
            console.WriteLine(title);
            for (var i = 0; i < nodes.Count; i++)
                console.WriteLine($"  {i + 1} {nodes[i].Label}");
            console.WriteLine($"  0 {exitLabel}");

            console.Write($"Choose (a number from 0 to {nodes.Count}): ");
            var answer = (console.ReadLine() ?? throw new EndOfInputException()).Trim();

            if (answer == "0")
                return null;
            if (int.TryParse(answer, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
                && number >= 1 && number <= nodes.Count)
                return number;

            console.WriteLine($"Answer a number from 0 to {nodes.Count}.");
        }
    }
}
