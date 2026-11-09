namespace Umbraco.Community.Licensing.Tool;

/// <summary>The outcome of looking for an installation folder (Database and folder, ADR-0013).</summary>
/// <param name="Folder">The folder to use, or null when none holds an installation.</param>
/// <param name="UnusedHomeInstallation">The home installation that is not used because the current folder is, or null.</param>
/// <param name="Checked">The folders looked in, in full, for the no-installation message.</param>
internal sealed record FolderResolution(string? Folder, string? UnusedHomeInstallation, IReadOnlyList<string> Checked);

/// <summary>Session startup after the argument and person checks: find the folder, then offer setup when there is none.</summary>
internal static class Startup
{
    internal const string DatabaseFileName = "ucl.db";

    /// <summary>Creates a new installation and returns its folder, or null when it did not happen (a stub until sections 6 and 10).</summary>
    internal delegate string? Provision(Session session);

    /// <summary>The startup offer's set up and restore, replaced by sections 6.1 and 10.2.</summary>
    internal sealed record Offer(Provision SetUp, Provision Restore)
    {
        public static Offer Stubs { get; } = new(
            s => NotAvailable(s, "Set up a new installation"),
            s => NotAvailable(s, "Restore from a backup"));

        private static string? NotAvailable(Session session, string label)
        {
            session.Console.WriteLine($"{label} is not available yet.");
            return null;
        }
    }

    public static FolderResolution Resolve(ToolEnvironment environment)
    {
        var current = Path.GetFullPath(environment.CurrentDirectory);
        var homeInstallation = Path.GetFullPath(Path.Combine(environment.Home, ".ucl"));
        // An installation never sits directly in the home folder (PDR-0052).
        var currentIsCandidate = !SamePath(current, environment.Home);
        var checkedFolders = currentIsCandidate && !SamePath(current, homeInstallation)
            ? new[] { current, homeInstallation }
            : [homeInstallation];

        if (currentIsCandidate && HoldsInstallation(current))
        {
            var unused = !SamePath(current, homeInstallation) && HoldsInstallation(homeInstallation)
                ? homeInstallation
                : null;
            return new FolderResolution(current, unused, checkedFolders);
        }

        return HoldsInstallation(homeInstallation)
            ? new FolderResolution(homeInstallation, null, checkedFolders)
            : new FolderResolution(null, null, checkedFolders);
    }

    internal static string UnusedNote(string homeInstallation) =>
        $"Note: {homeInstallation} also holds an installation; it is not used in this session.";

    /// <summary>
    /// Finds the installation and states it, or makes the startup offer. Returns false when the vendor
    /// quits at the offer. Using a folder opens its database first; one that cannot be opened is refused
    /// (<see cref="InstallationRefusedException"/>, stderr, exit 1) before <c>Using</c> is written.
    /// </summary>
    public static bool Begin(Session session, Offer offer)
    {
        var resolution = Resolve(session.Environment);
        if (resolution.Folder is not null)
        {
            session.UseFolder(
                resolution.Folder,
                resolution.UnusedHomeInstallation is null ? null : UnusedNote(resolution.UnusedHomeInstallation));
            return true;
        }

        var console = session.Console;
        console.WriteLine("No installation found. Looked in:");
        foreach (var folder in resolution.Checked)
            console.WriteLine($"  {folder}");

        var choices = new[]
        {
            new MenuNode("Set up a new installation"),
            new MenuNode("Restore from a backup"),
        };
        while (true)
        {
            var picked = MenuLoop.Choose(session, "What now?", choices, "Quit");
            if (picked is null)
                return false;

            var setUp = picked == 1;
            string? folder;
            try
            {
                folder = setUp ? offer.SetUp(session) : offer.Restore(session);
            }
            catch (ActionCancelledException)
            {
                console.WriteLine("Cancelled.");
                continue;
            }
            catch (Exception ex) when (Failure.IsContinuable(ex))
            {
                // The offer is shown again; nothing was stored and the lock is untouched (PDR-0053).
                console.WriteLine($"{choices[picked.Value - 1].Label} failed: {Failure.OneLine(ex)}");
                continue;
            }

            if (folder is not null)
            {
                // Setup keeps the passphrase it just set; a restore forgets any (ADR-0022).
                session.UseFolder(Path.GetFullPath(folder), keepPassphrase: setUp);
                return true;
            }
        }
    }

    private static bool HoldsInstallation(string folder) => File.Exists(Path.Combine(folder, DatabaseFileName));

    private static bool SamePath(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
