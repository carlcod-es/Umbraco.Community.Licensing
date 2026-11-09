using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Settings &gt; Prefix and Settings &gt; Idle lock. Neither asks for the passphrase (PDR-0023, PDR-0030): the
/// current value is shown and proposed, a changed value is written in one short transaction, and an unchanged
/// one writes nothing. Stored forms are those setup writes (<see cref="SettingNames"/>).
/// </summary>
internal static class SettingsActions
{
    internal const string PrefixChanged =
        "New issues and reissues use the new prefix. Existing licenses keep theirs until reissued.";

    internal const string IdleLockChanged = "The new idle lock applies from the next session.";

    internal const string Unchanged = "Unchanged.";

    private const string MissingPrefix =
        "The prefix setting is missing, so the installation looks damaged. Restore a backup into another folder.";

    public static void Register(ActionTable table)
    {
        table.Register("settings.prefix", ChangePrefix);
        table.Register("settings.idle", ChangeIdleLock);
        table.Register(Menus.SetUpHere, session => NewInstallation(session, s => Setup.Run(s, s.Environment.CurrentDirectory), setUp: true, "Set up"));
        table.Register(Menus.RestoreHere, session => NewInstallation(session, NotAvailable, setUp: false, "Restore from a backup"));
    }

    internal const string HomeFolderRefused =
        "An installation cannot sit loose in the home folder. Start the tool in another folder to make one there. Nothing changes.";

    internal static string HoldsInstallationRefused(string folder) =>
        $"{folder} already holds an installation. A new installation there is refused and nothing changes.";

    /// <summary>The refusal for a current folder that cannot take a new installation, or null when it can.</summary>
    internal static string? RefuseCurrentFolder(ToolEnvironment environment)
    {
        var current = Path.GetFullPath(environment.CurrentDirectory);
        if (Startup.SamePath(current, environment.Home))
            return HomeFolderRefused;
        return Installation.Holds(current) ? HoldsInstallationRefused(current) : null;
    }

    /// <summary>
    /// Settings &gt; New installation in the current folder. The refusals come first, before any question, and change
    /// nothing. The passphrase the session already holds is set aside inside the session lock while setup or restore runs, so the idle lock still covers it: a cancelled or failed
    /// one gives it back (the session still uses the earlier installation), a completed one zeroes it, since the session
    /// now holds the one just set (setup) or none (restore, ADR-0022). A refused new database is a failed setup (PDR-0053).
    /// </summary>
    internal static void NewInstallation(Session session, Func<Session, Provisioned?> make, bool setUp, string entry)
    {
        if (RefuseCurrentFolder(session.Environment) is { } refusal)
        {
            session.Console.WriteLine(refusal);
            return;
        }

        var folder = Path.GetFullPath(session.Environment.CurrentDirectory);
        var home = Path.GetFullPath(Path.Combine(session.Environment.Home, ".ucl"));
        var note = !Startup.SamePath(folder, home) && Installation.Holds(home) ? Startup.UnusedNote(home) : null;

        var moved = false;
        try
        {
            session.Lock.SetAside();
            var provisioned = make(session);
            if (provisioned is null)
                return;
            moved = Startup.UseProvisioned(session, provisioned, note, keepPassphrase: setUp, $"{Menus.NewInstallationLabel} - {entry}");
        }
        finally
        {
            if (moved)
                session.Lock.Discard();
            else
                session.Lock.Restore();
        }
    }

    private static Provisioned? NotAvailable(Session session)
    {
        session.Console.WriteLine("Restore from a backup is not available yet.");
        return null;
    }

    internal static void ChangePrefix(Session session)
    {
        var stored = Read(session, SettingNames.Prefix) ?? throw new InstallationRefusedException(MissingPrefix);
        var shown = stored.Length == 0 ? TypedValues.NoPrefixWord : stored;
        session.Console.WriteLine($"Prefix: {shown}");

        var answer = session.Questions.AskText(
            "New prefix",
            TypedValues.PrefixAccepts,
            TypedValues.CheckPrefix,
            proposed: shown);
        TypedValues.CheckPrefix(answer.Value, out var prefix);

        if (prefix.Value == stored)
        {
            session.Console.WriteLine(Unchanged);
            return;
        }

        Write(session, SettingNames.Prefix, prefix.Value);
        session.Console.WriteLine($"Prefix is now {(prefix.Value.Length == 0 ? TypedValues.NoPrefixWord : prefix.Value)}. {PrefixChanged}");
    }

    internal static void ChangeIdleLock(Session session)
    {
        var stored = Read(session, SettingNames.IdleLock) ?? throw new InstallationRefusedException(Installation.DamagedIdleLockMessage(session.Folder));
        if (!TypedValues.TryParseIdleLock(stored, out var current))
            throw new InstallationRefusedException(Installation.DamagedIdleLockMessage(session.Folder));

        var shown = TypedValues.IdleLockSetting(current);
        session.Console.WriteLine(current is { } t ? $"Idle lock: {(int)t.TotalMinutes} minutes" : "Idle lock: never");

        var answer = session.Questions.AskText(
            "New idle lock",
            TypedValues.IdleLockAccepts,
            TypedValues.CheckIdleLock,
            proposed: shown);
        TypedValues.TryParseIdleLock(answer.Value, out var idleTime);
        var setting = TypedValues.IdleLockSetting(idleTime);

        if (setting == shown)
        {
            session.Console.WriteLine(Unchanged);
            return;
        }

        // The session lock keeps its time: a change applies from the next session (spec "Idle lock").
        Write(session, SettingNames.IdleLock, setting);
        session.Console.WriteLine($"Idle lock is now {(idleTime is { } n ? $"{(int)n.TotalMinutes} minutes" : "never")}. {IdleLockChanged}");
    }

    private static string? Read(Session session, string name)
    {
        using var db = session.OpenDb();
        return db.Settings.AsNoTracking().Where(s => s.Name == name).Select(s => s.Value).SingleOrDefault();
    }

    private static void Write(Session session, string name, string value)
    {
        using var db = session.OpenDb();
        var row = db.Settings.SingleOrDefault(s => s.Name == name);
        if (row is null)
            db.Settings.Add(new Setting { Name = name, Value = value });
        else
            row.Value = value;
        db.SaveChanges();
    }
}
