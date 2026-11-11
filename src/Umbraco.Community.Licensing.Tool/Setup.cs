using System.Security.Cryptography;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Guided setup (design.md "Menu session"): asks every question first, then in one step creates the folder and
/// database, writes the settings and, in shared mode, the shared signing key, and hands the passphrase to the
/// session's lock (PDR-0050). Nothing is created before the last answer, so a cancelled setup leaves nothing.
/// </summary>
internal static class Setup
{
    internal const string PassphraseQuestion = "Choose a passphrase";
    internal const string PassphraseAccepts = "15 to 256 characters: letters A to Z, digits, spaces and standard keyboard symbols; no space at the start or end";
    internal const string AgainQuestion = "Type it again";
    internal const string PassphrasesDiffer = "The two passphrases differ. Start again.";

    internal const string DefaultPrefix = "LIC";
    internal const string DefaultIdleLock = "15";

    internal static string RefusedFolderMessage(string folder) =>
        $"{folder} already holds an installation. Setup refuses it and changes nothing.";

    /// <summary>
    /// Runs setup. <paramref name="fixedFolder"/> is the only folder when given (Settings, new installation in the
    /// current folder): the folder question is skipped. Returns the new installation's folder with the undo for it,
    /// or null when the folder already holds an installation (refused, nothing changed). <c>/cancel</c> throws
    /// <see cref="ActionCancelledException"/>. A passphrase this setup handed to the lock is released again when
    /// setup fails after that point; any other failure goes on to the caller with nothing created or held.
    /// </summary>
    public static Provisioned? Run(Session session, string? fixedFolder = null)
    {
        var console = session.Console;
        var folder = Path.GetFullPath(fixedFolder ?? FolderOffer.Ask(session));
        if (Installation.Holds(folder))
        {
            console.WriteLine(RefusedFolderMessage(folder));
            return null;
        }

        byte[]? passphrase = null;
        var held = false;
        try
        {
            passphrase = AskPassphrase(session);

            var prefix = AskPrefix(session);
            var shared = session.Questions.AskChoice(
                "Signing-key mode",
                [
                    new Choice(SigningKeyModes.PerProduct, "One signing key per product (recommended)"),
                    new Choice(SigningKeyModes.Shared, "One signing key shared by all products"),
                ],
                SigningKeyModes.PerProduct).Id == SigningKeyModes.Shared;
            var standby = session.Questions.AskYesNo("Create a standby key with each signing key (recommended)", proposed: true);
            var idleText = session.Questions.AskText(
                "Idle lock in minutes",
                TypedValues.IdleLockAccepts,
                TypedValues.CheckIdleLock,
                proposed: DefaultIdleLock).Value;
            TypedValues.TryParseIdleLock(idleText, out var idleTime);

            // The last answer is in. From here on, one step.
            var environment = session.Environment;
            var keys = shared ? NewSharedKeys(passphrase, environment, standby) : [];
            var now = environment.TimeProvider.GetUtcNow().UtcDateTime;

            Installation installation;
            try
            {
                installation = Installation.Create(folder, db => Write(db, passphrase, environment, prefix, shared, standby, idleTime, keys, now));
            }
            catch (InstallationExistsException)
            {
                // Someone made an installation there since the folder was checked.
                console.WriteLine(RefusedFolderMessage(folder));
                return null;
            }

            try
            {
                session.Lock.Hold(passphrase);
                held = true;
            }
            catch
            {
                installation.UndoCreate();
                throw;
            }

            // The closing message waits until the session has taken the folder (Session.UseFolder), so a folder it refuses is never announced.
            return new Provisioned(
                installation.Folder,
                installation.UndoCreate,
                () => WriteClosingMessage(console, installation.Folder, shared, standby));
        }
        catch
        {
            if (held)
                session.Lock.Release();
            throw;
        }
        finally
        {
            if (!held && passphrase is not null)
                CryptographicOperations.ZeroMemory(passphrase);
        }
    }

    /// <summary>The passphrase typed twice, hidden; the rule is checked on the first entry. Returns the pinned bytes, which the caller owns.</summary>
    private static byte[] AskPassphrase(Session session)
    {
        while (true)
        {
            var first = session.Questions.AskHidden(PassphraseQuestion, PassphraseAccepts, TypedValues.CheckPassphrase);
            byte[] second;
            try
            {
                second = session.Questions.AskHidden(AgainQuestion, "the same passphrase", (_, _) => null);
            }
            catch
            {
                CryptographicOperations.ZeroMemory(first);
                throw;
            }

            var same = CryptographicOperations.FixedTimeEquals(first, second);
            CryptographicOperations.ZeroMemory(second);
            if (same)
                return first;

            CryptographicOperations.ZeroMemory(first);
            session.Console.WriteLine(PassphrasesDiffer);
        }
    }

    private static KeyPrefix AskPrefix(Session session)
    {
        var answer = session.Questions.AskText(
            "Key prefix",
            TypedValues.PrefixAccepts,
            TypedValues.CheckPrefix,
            proposed: DefaultPrefix);
        TypedValues.CheckPrefix(answer.Value, out var prefix);
        return prefix;
    }

    private static KeyProtection.NewKey[] NewSharedKeys(byte[] passphrase, ToolEnvironment environment, bool standby)
    {
        var keys = new List<KeyProtection.NewKey> { KeyProtection.CreateEncrypted(passphrase, environment.Pbkdf2Iterations) };
        if (standby)
            keys.Add(KeyProtection.CreateEncrypted(passphrase, environment.Pbkdf2Iterations));
        return [.. keys];
    }

    /// <summary>The first rows, written by one <c>SaveChanges</c>, so one transaction: the settings with the passphrase check, and the shared key and its standby.</summary>
    private static void Write(
        ToolDbContext db,
        byte[] passphrase,
        ToolEnvironment environment,
        KeyPrefix prefix,
        bool shared,
        bool standby,
        TimeSpan? idleTime,
        KeyProtection.NewKey[] keys,
        DateTime now)
    {
        db.Settings.AddRange(
            new Setting { Name = SettingNames.Prefix, Value = prefix.Value },
            new Setting { Name = SettingNames.SigningKeyMode, Value = shared ? SigningKeyModes.Shared : SigningKeyModes.PerProduct },
            new Setting { Name = SettingNames.StandbyKeys, Value = standby ? "true" : "false" },
            new Setting { Name = SettingNames.IdleLock, Value = TypedValues.IdleLockSetting(idleTime) });
        db.Settings.AddRange(PassphraseCheck.Create(passphrase, environment.Pbkdf2Iterations).ToSettings());

        for (var i = 0; i < keys.Length; i++)
        {
            db.SigningKeys.Add(new SigningKeyRecord
            {
                SigningKeyId = keys[i].SigningKeyId,
                ProductId = null,
                PublicKey = keys[i].PublicKey,
                EncryptedPrivateKeyPem = keys[i].EncryptedPrivateKeyPem,
                Role = i == 0 ? KeyRole.Active : KeyRole.Standby,
                CreatedUtc = now,
            });
        }

        db.SaveChanges();
    }

    private static void WriteClosingMessage(ToolConsole console, string folder, bool shared, bool standby)
    {
        console.WriteLine();
        console.WriteLine("Setup is complete. The installation is in this folder:");
        console.WriteLine($"  {folder}");
        console.WriteLine();
        console.WriteLine("It holds:");
        console.WriteLine("  - your private signing keys, encrypted with your passphrase");
        console.WriteLine("  - your license records");
        console.WriteLine("  - customer emails and order references, which are not encrypted");
        console.WriteLine();
        console.WriteLine("Back this folder up. Keep it, and its backups, out of folders that sync to");
        console.WriteLine("cloud storage, such as OneDrive, iCloud Drive or Dropbox (examples,");
        console.WriteLine("not a complete list of such services).");
        console.WriteLine("Keep your passphrase in a password manager, apart from your backups:");
        console.WriteLine("without it the private signing keys cannot be recovered.");
        if (shared)
            console.WriteLine(standby ? "The shared signing key and its standby key are created." : "The shared signing key is created.");
        console.WriteLine();
        console.WriteLine("Next steps:");
        if (shared)
        {
            console.WriteLine("  1. Products > Add a product (the shared signing key is already created)");
        }
        else
        {
            console.WriteLine("  1. Products > Add a product (this creates its signing key under the");
            console.WriteLine("     passphrase you just set; it is not asked again in this session");
            console.WriteLine("     unless the idle lock has passed)");
        }
        console.WriteLine("  2. Signing keys > Trusted keys (prints the public key lines your");
        console.WriteLine("     release must trust)");
        console.WriteLine();
    }
}
