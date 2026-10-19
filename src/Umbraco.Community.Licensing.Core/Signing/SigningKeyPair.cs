namespace Umbraco.Community.Licensing.Core;

/// <summary>A newly created signing key pair. Disposing it disposes the private key.</summary>
public sealed class SigningKeyPair : IDisposable
{
    private SigningKeyPair(SigningPrivateKey privateKey)
    {
        PrivateKey = privateKey;
    }

    /// <summary>The private key. Keep it with the vendor.</summary>
    public SigningPrivateKey PrivateKey { get; }

    /// <summary>The public key to give to products.</summary>
    public SigningPublicKey PublicKey => PrivateKey.PublicKey;

    /// <summary>The signing key ID shared by both halves.</summary>
    public string SigningKeyId => PrivateKey.SigningKeyId;

    /// <summary>Creates a new, independent ECDSA P-256 key pair.</summary>
    /// <returns>The new pair.</returns>
    public static SigningKeyPair Create() => new(SigningPrivateKey.Generate());

    /// <inheritdoc/>
    public void Dispose() => PrivateKey.Dispose();
}
