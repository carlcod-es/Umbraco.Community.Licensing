using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The passphrase check stored at setup: PBKDF2-HMAC-SHA256 over the passphrase with a random salt, the
/// iteration count stored beside them so a later change of the default does not lock the vendor out.
/// </summary>
/// <param name="Salt">16 random bytes.</param>
/// <param name="Iterations">The iteration count used to make <paramref name="Hash"/>.</param>
/// <param name="Hash">The derived 32 bytes.</param>
internal sealed record PassphraseCheck(byte[] Salt, int Iterations, byte[] Hash)
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>Makes a check for <paramref name="passphrase"/> with a fresh salt.</summary>
    public static PassphraseCheck Create(string passphrase, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return new PassphraseCheck(salt, iterations, Derive(KeyProtection.Normalize(passphrase), salt, iterations));
    }

    /// <summary>Whether <paramref name="passphrase"/> matches, using the stored salt and iterations; fixed-time compare.</summary>
    public bool Verify(string passphrase) =>
        CryptographicOperations.FixedTimeEquals(Derive(KeyProtection.Normalize(passphrase), Salt, Iterations), Hash);

    private static byte[] Derive(string passphrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, HashSize);
}

/// <summary>
/// Passphrase check storage and private-key encryption (ADR-0014). Nothing here keeps a passphrase or a
/// decrypted key: callers hold them for one command.
/// </summary>
internal static class KeyProtection
{
    /// <summary>
    /// The one place a passphrase is normalised to Unicode NFC (Q34), so the same text gives the same bytes
    /// from any source or OS. Check, encrypt and decrypt all go through it; it never trims.
    /// </summary>
    internal static string Normalize(string passphrase) => passphrase.Normalize(NormalizationForm.FormC);

    /// <summary>Settings row: base64 salt of the passphrase check.</summary>
    public const string SaltSetting = "PassphraseCheckSalt";

    /// <summary>Settings row: iteration count of the passphrase check.</summary>
    public const string IterationsSetting = "PassphraseCheckIterations";

    /// <summary>Settings row: base64 hash of the passphrase check.</summary>
    public const string HashSetting = "PassphraseCheckHash";

    /// <summary>
    /// Adds the passphrase check rows to <paramref name="db"/> for the caller to save (setup saves them with
    /// the rest of its work). Iterations come from <paramref name="iterations"/>, normally
    /// <see cref="ToolEnvironment.Pbkdf2Iterations"/>.
    /// </summary>
    public static void StoreCheck(ToolDbContext db, string passphrase, int iterations)
    {
        var check = PassphraseCheck.Create(passphrase, iterations);
        db.Settings.AddRange(
            new Setting { Name = SaltSetting, Value = Convert.ToBase64String(check.Salt) },
            new Setting { Name = IterationsSetting, Value = iterations.ToString(CultureInfo.InvariantCulture) },
            new Setting { Name = HashSetting, Value = Convert.ToBase64String(check.Hash) });
    }

    /// <summary>The stored check; null when the installation has none or an unreadable one.</summary>
    public static PassphraseCheck? LoadCheck(ToolDbContext db)
    {
        var values = db.Settings
            .Where(s => s.Name == SaltSetting || s.Name == IterationsSetting || s.Name == HashSetting)
            .ToDictionary(s => s.Name, s => s.Value);
        if (!values.TryGetValue(SaltSetting, out var salt)
            || !values.TryGetValue(IterationsSetting, out var iterations)
            || !values.TryGetValue(HashSetting, out var hash)
            || !int.TryParse(iterations, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            || count < 1)
        {
            return null;
        }

        try
        {
            return new PassphraseCheck(Convert.FromBase64String(salt), count, Convert.FromBase64String(hash));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Encrypts <paramref name="key"/> as an encrypted PKCS#8 PEM (PBES2, AES-256-CBC, PBKDF2-HMAC-SHA256 at
    /// <paramref name="iterations"/>), which other tools open with the passphrase. The count is written into the PEM.
    /// </summary>
    public static string Encrypt(SigningPrivateKey key, string passphrase, int iterations)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(key.ExportPem());
        return ecdsa.ExportEncryptedPkcs8PrivateKeyPem(
            Normalize(passphrase),
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterations));
    }

    /// <summary>
    /// Decrypts a PEM made by <see cref="Encrypt"/>; the iteration count is read from the PEM. Dispose the key
    /// when the command ends.
    /// </summary>
    /// <exception cref="CryptographicException">Wrong passphrase or damaged PEM.</exception>
    public static SigningPrivateKey Decrypt(string encryptedPem, string passphrase)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromEncryptedPem(encryptedPem, Normalize(passphrase));
        return SigningPrivateKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());
    }
}
