namespace Umbraco.Community.Licensing.Tool;

/// <summary>The outcome of looking for an installation folder (Database and folder, ADR-0013).</summary>
/// <param name="Folder">The folder to use, or null when none holds an installation.</param>
/// <param name="UnusedHomeInstallation">The home installation that is not used because the current folder is, or null.</param>
/// <param name="Checked">The folders looked in, in full, for the no-installation message.</param>
internal sealed record FolderResolution(string? Folder, string? UnusedHomeInstallation, IReadOnlyList<string> Checked);

/// <summary>
/// A new installation made at the startup offer: its folder, and the undo that removes what the setup or restore
/// created when the session cannot then use it (null when there is nothing to remove), and what to write before the
/// session states the folder (setup's closing message), which is written only once the session can use the folder.
/// </summary>
internal sealed record Provisioned(string Folder, Action? Undo = null, Action? Announce = null)
{
    public static implicit operator Provisioned(string folder) => new(folder);
}

/// <summary>Session startup after the argument and person checks: find the folder, then offer setup when there is none.</summary>
internal static class Startup
{
    internal const string DatabaseFileName = "ucl.db";

    /// <summary>Creates a new installation and returns it, or null when it did not happen (a refusal, a no, or a stub).</summary>
    internal delegate Provisioned? Provision(Session session);

    /// <summary>The startup offer's set up and restore.</summary>
    internal sealed record Offer(Provision SetUp, Provision Restore)
    {
        public static Offer Stubs { get; } = new(
            s => NotAvailable(s, "Set up a new installation"),
            s => NotAvailable(s, "Restore from a backup"));

        /// <summary>What the tool runs with: guided setup and restore.</summary>
        public static Offer Default { get; } = new(s => Setup.Run(s), s => RestoreBackup.Run(s));

        private static Provisioned? NotAvailable(Session session, string label)
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
            Provisioned? provisioned;
            try
            {
                provisioned = setUp ? offer.SetUp(session) : offer.Restore(session);
            }
            catch (ActionCancelledException)
            {
                console.WriteLine("Cancelled.");
                continue;
            }
            catch (Exception ex) when (Failure.IsContinuable(ex))
            {
                // The offer is shown again; setup has removed what it made and released what it held (PDR-0053).
                console.WriteLine($"{choices[picked.Value - 1].Label} failed: {Failure.OneLine(ex)}");
                continue;
            }

            if (provisioned is null)
                continue;

            // Setup keeps the passphrase it just set; a restore forgets any (ADR-0022).
            if (UseProvisioned(session, provisioned, null, keepPassphrase: setUp, choices[picked.Value - 1].Label))
                return true;
        }
    }

    /// <summary>
    /// Moves the session to a new installation (setup or restore just made). One that the session cannot use is treated as
    /// a failed setup or restore: the failure line, the lock released, what was made removed. Returns false then.
    /// </summary>
    internal static bool UseProvisioned(Session session, Provisioned provisioned, string? unusedNote, bool keepPassphrase, string label)
    {
        try
        {
            session.UseFolder(Path.GetFullPath(provisioned.Folder), unusedNote, keepPassphrase, provisioned.Announce);
            return true;
        }
        catch (Exception ex) when (Failure.IsContinuable(ex))
        {
            session.Lock.Release();
            provisioned.Undo?.Invoke();
            session.Console.WriteLine($"{label} failed: {Failure.OneLine(ex)}");
            return false;
        }
    }

    private static bool HoldsInstallation(string folder) => Installation.Holds(folder);

    /// <summary>
    /// True when both paths name the same folder: compared in full, with links followed (macOS <c>/var</c> is
    /// <c>/private/var</c>), ignoring case except on Linux. A case-sensitive volume elsewhere is not told apart.
    /// </summary>
    internal static bool SamePath(string a, string b) =>
        string.Equals(
            Canonical(a),
            Canonical(b),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private static string Canonical(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        try
        {
            var root = Path.GetPathRoot(full) ?? "";
            var current = root;
            var parts = full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                current = Path.Combine(current, part);
                var info = new DirectoryInfo(current);
                if (info.Exists && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                    current = Canonical(target.FullName); // a link's target can itself pass through links
            }

            return current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return full;
        }
    }
}
