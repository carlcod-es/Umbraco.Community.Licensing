using System.Security.Cryptography;

namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// A private signing key (ECDSA P-256). Held by the vendor only; exports to and imports from a
/// PKCS#8 PEM. Dispose to release the underlying key.
/// </summary>
public sealed class SigningPrivateKey : IDisposable
{
    private readonly ECDsa _ecdsa;

    private SigningPrivateKey(ECDsa ecdsa)
    {
        _ecdsa = ecdsa;
        PublicKey = SigningPublicKey.FromSpki(ecdsa.ExportSubjectPublicKeyInfo());
    }

    /// <summary>The public key matching this private key.</summary>
    public SigningPublicKey PublicKey { get; }

    /// <summary>The signing key ID, the same as <see cref="PublicKey"/> reports.</summary>
    public string SigningKeyId => PublicKey.SigningKeyId;

    /// <summary>
    /// Imports a private key from an unencrypted PKCS#8 PEM (<c>BEGIN PRIVATE KEY</c>). The text must hold exactly one PEM
    /// block; text around that block (whitespace, comments) is ignored, but a second <c>-----BEGIN</c> block anywhere
    /// after the first is rejected.
    /// </summary>
    /// <param name="pem">The PEM text.</param>
    /// <returns>The imported key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pem"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The text is not an ECDSA P-256 private key in PKCS#8 PEM, or holds more than one PEM block.</exception>
    public static SigningPrivateKey FromPem(string pem)
    {
        ArgumentNullException.ThrowIfNull(pem);

        if (!PemEncoding.TryFind(pem, out var fields) || !pem.AsSpan()[fields.Label].SequenceEqual("PRIVATE KEY"))
        {
            throw new FormatException("The text is not a PKCS#8 PEM private key ('-----BEGIN PRIVATE KEY-----').");
        }

        if (pem.AsSpan(fields.Location.End.Value).Contains("-----BEGIN", StringComparison.Ordinal))
        {
            throw new FormatException("The text holds more than one PEM block; expected exactly one private key.");
        }

        byte[] der;
        try
        {
            der = Convert.FromBase64String(pem[fields.Base64Data].ToString());
        }
        catch (FormatException ex)
        {
            throw new FormatException("The PEM body is not valid base64.", ex);
        }

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportPkcs8PrivateKey(der, out _);
            return new SigningPrivateKey(ecdsa);
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException or ArgumentException)
        {
            ecdsa.Dispose();
            throw new FormatException("The PEM is not an ECDSA P-256 private key.", ex);
        }
        catch (FormatException)
        {
            ecdsa.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    /// <summary>Exports the key as an unencrypted PKCS#8 PEM.</summary>
    /// <returns>The PEM text.</returns>
    public string ExportPem() => _ecdsa.ExportPkcs8PrivateKeyPem();

    /// <inheritdoc/>
    public void Dispose() => _ecdsa.Dispose();

    /// <summary>Creates a new random P-256 key.</summary>
    internal static SigningPrivateKey Generate()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            return new SigningPrivateKey(ecdsa);
        }
        catch
        {
            ecdsa.Dispose();
            throw;
        }
    }

    /// <summary>Signs <paramref name="data"/>: ECDSA P-256 over SHA-256, 64-byte IEEE P1363 (r then s).</summary>
    internal byte[] Sign(ReadOnlySpan<byte> data) =>
        _ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
}
