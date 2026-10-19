using System.Buffers.Text;
using System.Security.Cryptography;

namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// A public signing key: the half a product holds to verify licenses. Exports to one line of
/// text, <c>&lt;signing key ID&gt;.&lt;base64url SPKI&gt;</c>, and carries no private material.
/// </summary>
public sealed class SigningPublicKey : IEquatable<SigningPublicKey>
{
    private const int P256SpkiLength = 91;

    /// <summary>DER SPKI header for a named-curve P-256 key, up to and including the uncompressed-point marker 0x04.</summary>
    private static ReadOnlySpan<byte> P256SpkiPrefix =>
    [
        0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01,
        0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07, 0x03, 0x42, 0x00, 0x04,
    ];

    private readonly byte[] _spki;

    internal SigningPublicKey(byte[] spki)
        : this(DeriveId(spki), spki)
    {
    }

    /// <summary>Lets tests force an ID that is not the derived one. Production code uses the derived-ID constructor.</summary>
    internal SigningPublicKey(string signingKeyId, byte[] spki)
    {
        SigningKeyId = signingKeyId;
        _spki = spki;
    }

    /// <summary>The 11-character signing key ID, derived from this key alone.</summary>
    public string SigningKeyId { get; }

    /// <summary>The key as DER SubjectPublicKeyInfo bytes. Callers must not modify the array.</summary>
    internal byte[] Spki => _spki;

    /// <summary>Imports a public key from the text produced by <see cref="Export"/>.</summary>
    /// <param name="text">The exported key text.</param>
    /// <returns>The imported key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The text is not a P-256 public key in the export format, or its carried ID does not match the ID computed from the key.</exception>
    public static SigningPublicKey Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var trimmed = text.AsSpan().Trim();
        var dot = trimmed.IndexOf('.');
        if (dot < 0)
        {
            throw new FormatException("The text is not a signing public key: expected '<signing key ID>.<base64url key>'.");
        }

        var carriedId = trimmed[..dot];
        byte[] spki;
        try
        {
            spki = Base64Url.DecodeFromChars(trimmed[(dot + 1)..]);
        }
        catch (FormatException)
        {
            throw new FormatException("The text is not a signing public key: the key part is not base64url.");
        }

        var key = FromSpki(spki);
        if (!carriedId.SequenceEqual(key.SigningKeyId))
        {
            throw new FormatException(
                $"Signing key ID mismatch: the text carries '{carriedId}' but the key's ID is '{key.SigningKeyId}'.");
        }

        return key;
    }

    /// <summary>Exports the key as one line: <c>&lt;signing key ID&gt;.&lt;base64url SPKI&gt;</c>.</summary>
    /// <returns>The key text.</returns>
    public string Export() => SigningKeyId + "." + Base64Url.EncodeToString(_spki);

    /// <inheritdoc/>
    public bool Equals(SigningPublicKey? other) =>
        other is not null && _spki.AsSpan().SequenceEqual(other._spki);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SigningPublicKey);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_spki);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => Export();

    /// <summary>First 8 bytes of SHA-256 over the SPKI DER, base64url.</summary>
    internal static string DeriveId(ReadOnlySpan<byte> spki) =>
        Base64Url.EncodeToString(SHA256.HashData(spki)[..8]);

    /// <summary>Builds a key from SPKI bytes, requiring a P-256 key in canonical encoding.</summary>
    internal static SigningPublicKey FromSpki(byte[] spki)
    {
        // Checked before any platform import so curve detection is identical on every OS and
        // explicit-parameter or other-curve keys never reach a platform-specific code path.
        if (!spki.AsSpan().StartsWith(P256SpkiPrefix) || spki.Length != P256SpkiLength)
        {
            throw new FormatException("The key is not an uncompressed ECDSA P-256 public key in SubjectPublicKeyInfo encoding.");
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(spki, out var read);
            var canonical = ecdsa.ExportSubjectPublicKeyInfo();
            if (read != spki.Length || !canonical.AsSpan().SequenceEqual(spki))
            {
                throw new FormatException("The key is not in the canonical P-256 public key encoding.");
            }

            return new SigningPublicKey(canonical);
        }
        catch (CryptographicException ex)
        {
            throw new FormatException("The text is not an ECDSA P-256 public key.", ex);
        }
    }
}
