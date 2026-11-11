using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The stored passphrase check: PBKDF2-HMAC-SHA256 over the passphrase bytes with a 16-byte salt and
/// the iteration count it was made with (ADR-0014), kept as the three <c>Settings</c> rows
/// <see cref="SettingNames.PassphraseSalt"/>, <see cref="SettingNames.PassphraseIterations"/> and
/// <see cref="SettingNames.PassphraseHash"/>.
/// </summary>
internal sealed record PassphraseCheck(byte[] Salt, int Iterations, byte[] Hash)
{
    internal const int SaltSize = 16;
    internal const int HashSize = 32;

    /// <summary>Makes a check for a passphrase with a new random salt.</summary>
    public static PassphraseCheck Create(ReadOnlySpan<byte> passphrase, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return new PassphraseCheck(salt, iterations, Derive(passphrase, salt, iterations));
    }

    /// <summary>True when the passphrase gives the stored hash; compared in fixed time.</summary>
    public bool Matches(ReadOnlySpan<byte> passphrase)
    {
        var derived = Derive(passphrase, Salt, Iterations);
        try
        {
            return CryptographicOperations.FixedTimeEquals(derived, Hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derived);
        }
    }

    /// <summary>The three rows to store.</summary>
    public IEnumerable<Setting> ToSettings() =>
    [
        new Setting { Name = SettingNames.PassphraseSalt, Value = Convert.ToBase64String(Salt) },
        new Setting { Name = SettingNames.PassphraseIterations, Value = Iterations.ToString(CultureInfo.InvariantCulture) },
        new Setting { Name = SettingNames.PassphraseHash, Value = Convert.ToBase64String(Hash) },
    ];

    /// <summary>Reads the check from the database; null when any row is missing or malformed.</summary>
    public static PassphraseCheck? Read(ToolDbContext db)
    {
        var names = new[] { SettingNames.PassphraseSalt, SettingNames.PassphraseIterations, SettingNames.PassphraseHash };
        var rows = db.Settings.AsNoTracking().Where(s => names.Contains(s.Name)).ToDictionary(s => s.Name, s => s.Value);
        if (!rows.TryGetValue(SettingNames.PassphraseSalt, out var salt)
            || !rows.TryGetValue(SettingNames.PassphraseIterations, out var iterations)
            || !rows.TryGetValue(SettingNames.PassphraseHash, out var hash))
            return null;

        Span<byte> saltBytes = stackalloc byte[SaltSize];
        Span<byte> hashBytes = stackalloc byte[HashSize];
        if (!Convert.TryFromBase64String(salt, saltBytes, out var saltLength) || saltLength != SaltSize
            || !Convert.TryFromBase64String(hash, hashBytes, out var hashLength) || hashLength != HashSize
            || !int.TryParse(iterations, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
            return null;

        return new PassphraseCheck(saltBytes.ToArray(), count, hashBytes.ToArray());
    }

    private static byte[] Derive(ReadOnlySpan<byte> passphrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, HashSize);
}

/// <summary>
/// Encrypting and decrypting private signing keys, and unlocking the session for them (ADR-0014, ADR-0021).
/// Every step takes the passphrase as bytes, from <see cref="SessionLock"/> or the hidden prompt; it never becomes a string.
/// </summary>
internal static class KeyProtection
{
    /// <summary>Tries the vendor has to type the passphrase right, per action (PDR-0049).</summary>
    internal const int MaxTries = 3;

    internal const string WrongPassphrase = "That passphrase is wrong.";

    internal const string MissingCheckMessage =
        "The installation has no passphrase check, so it looks damaged. Restore a backup into another folder.";

    /// <summary>A new key pair: its signing key ID, public key export line and private key encrypted under the passphrase.</summary>
    internal sealed record NewKey(string SigningKeyId, string PublicKey, string EncryptedPrivateKeyPem);

    /// <summary>
    /// Creates a signing key pair and encrypts its private key (PBES2, AES-256-CBC, PBKDF2-HMAC-SHA256) with the
    /// passphrase, as a PEM with <c>\n</c> line endings. Check the passphrase first (<see cref="WithPassphrase"/>).
    /// </summary>
    public static NewKey CreateEncrypted(ReadOnlySpan<byte> passphrase, int iterations)
    {
        using var pair = SigningKeyPair.Create();
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pair.PrivateKey.ExportPem());
        var pem = ecdsa.ExportEncryptedPkcs8PrivateKeyPem(
            passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterations));
        return new NewKey(pair.SigningKeyId, pair.PublicKey.Export(), pem.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Decrypts a stored private key. The caller disposes the key (a <c>using</c>) before its action returns.
    /// Throws <see cref="CryptographicException"/> when the passphrase does not open the PEM.
    /// </summary>
    public static SigningPrivateKey Decrypt(string encryptedPem, ReadOnlySpan<byte> passphrase)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromEncryptedPem(encryptedPem, passphrase);
        return SigningPrivateKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());
    }

    /// <summary>
    /// Runs <paramref name="use"/> with the session's passphrase, asking for it first when none is held (ADR-0021).
    /// Actions call this after their own questions, before signing or creating a key, so a refused or cancelled
    /// unlock keeps the answers already given. A typed passphrase is checked against the stored check before it
    /// is held; a wrong one is said to be wrong and asked again, three tries in all, then the action is cancelled
    /// (<see cref="ActionCancelledException"/>). Every rejected answer is zeroed. An installation without a check
    /// is damaged and fails as an ordinary action failure.
    /// </summary>
    public static void WithPassphrase(Session session, PassphraseUse use)
    {
        if (session.Lock.TryUse(use))
            return;

        var check = ReadCheck(session);
        var held = false;
        for (var attempt = 1; attempt <= MaxTries && !held; attempt++)
        {
            var typed = session.Questions.AskHidden("Passphrase", "the installation's passphrase", (_, _) => null);
            var taken = false;
            try
            {
                if (check.Matches(typed))
                {
                    session.Lock.Hold(typed); // takes the array: it is zeroed when the lock releases it
                    taken = true;
                    held = true;
                }
                else
                {
                    session.Console.WriteLine(WrongPassphrase);
                }
            }
            finally
            {
                if (!taken)
                    CryptographicOperations.ZeroMemory(typed);
            }
        }

        if (!held)
            throw new ActionCancelledException();

        // Only an idle time shorter than this very call could have locked it again.
        if (!session.Lock.TryUse(use))
            throw new InvalidOperationException("The passphrase was forgotten before it could be used; try the action again.");
    }

    /// <summary>
    /// <see cref="WithPassphrase(Session, PassphraseUse)"/> for a callback that returns a value, so the caller needs no
    /// captured local. The callback gets the passphrase bytes for the call only, as there.
    /// </summary>
    public static T WithPassphrase<T>(Session session, Func<ReadOnlySpan<byte>, T> use)
    {
        T result = default!;
        PassphraseUse inner = passphrase => { result = use(passphrase); };
        WithPassphrase(session, inner);
        return result;
    }

    private static PassphraseCheck ReadCheck(Session session)
    {
        using var db = session.OpenDb();
        return PassphraseCheck.Read(db) ?? throw new InstallationRefusedException(MissingCheckMessage);
    }
}
