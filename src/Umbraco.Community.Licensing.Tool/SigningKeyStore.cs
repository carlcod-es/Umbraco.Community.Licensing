using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Creates and looks up the stored signing keys: the one place a key pair becomes a <see cref="SigningKey"/> row
/// and the one place the active key is chosen by signing-key mode (ADR-0014).
/// </summary>
internal static class SigningKeyStore
{
    /// <summary>
    /// Makes a key pair and adds its row (private key encrypted under <paramref name="passphrase"/>) to
    /// <paramref name="db"/> for the caller to save with the rest of its work.
    /// </summary>
    /// <param name="productId">The product the key belongs to; null for the shared key.</param>
    public static SigningKey Add(
        ToolDbContext db,
        string? productId,
        SigningKeyRole role,
        string passphrase,
        int iterations,
        DateTimeOffset now)
    {
        using var pair = SigningKeyPair.Create();
        var key = new SigningKey
        {
            SigningKeyId = pair.SigningKeyId,
            ProductId = productId,
            PublicKey = pair.PublicKey.Export(),
            EncryptedPrivateKeyPem = KeyProtection.Encrypt(pair.PrivateKey, passphrase, iterations),
            Role = role,
            CreatedUtc = now,
        };
        db.SigningKeys.Add(key);
        return key;
    }

    /// <summary>
    /// The active key a product signs with: the product's own in per-product mode, the shared key (no product)
    /// in shared mode.
    /// </summary>
    /// <exception cref="ToolRefusal">The installation holds no active key for it.</exception>
    public static SigningKey Active(ToolDbContext db, InstallationSettings settings, string productId)
    {
        var owner = settings.Shared ? null : productId;
        return db.SigningKeys.FirstOrDefault(k => k.ProductId == owner && k.Role == SigningKeyRole.Active)
            ?? throw new ToolRefusal(settings.Shared
                ? "The installation has no active shared signing key. Nothing changed."
                : $"The product {productId} has no active signing key. Nothing changed.");
    }
}
