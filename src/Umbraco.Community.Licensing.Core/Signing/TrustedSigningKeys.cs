using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace Umbraco.Community.Licensing.Core;

/// <summary>An immutable set of trusted public keys, addressed by signing key ID.</summary>
public sealed class TrustedSigningKeys
{
    private const int SignatureLength = 64;

    private readonly Dictionary<string, SigningPublicKey> _keys;

    private TrustedSigningKeys(Dictionary<string, SigningPublicKey> keys)
    {
        _keys = keys;
        SigningKeyIds = new ReadOnlyCollection<string>([.. keys.Keys.Order(StringComparer.Ordinal)]);
    }

    /// <summary>A set holding no keys.</summary>
    public static TrustedSigningKeys Empty { get; } = new([]);

    /// <summary>The IDs held, in ordinal order.</summary>
    public IReadOnlyCollection<string> SigningKeyIds { get; }

    /// <summary>Creates a set from <paramref name="keys"/>. Repeating a key is allowed.</summary>
    /// <param name="keys">The keys to trust.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> or one of its items is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two different keys share one signing key ID.</exception>
    public static TrustedSigningKeys Create(params IEnumerable<SigningPublicKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var set = Empty;
        foreach (var key in keys)
        {
            set = set.With(key);
        }

        return set;
    }

    /// <summary>Returns a set that also trusts <paramref name="key"/>. Adding a key already held returns an equal set.</summary>
    /// <param name="key">The key to add.</param>
    /// <returns>The new set; this set is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A different key is already held under the same signing key ID.</exception>
    public TrustedSigningKeys With(SigningPublicKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_keys.TryGetValue(key.SigningKeyId, out var held))
        {
            return held.Equals(key)
                ? this
                : throw new ArgumentException(
                    $"A different key is already trusted under signing key ID '{key.SigningKeyId}'.", nameof(key));
        }

        var copy = new Dictionary<string, SigningPublicKey>(_keys) { [key.SigningKeyId] = key };
        return new TrustedSigningKeys(copy);
    }

    /// <summary>Returns a set without the key under <paramref name="signingKeyId"/>. An ID not held leaves the set unchanged.</summary>
    /// <param name="signingKeyId">The ID to withdraw.</param>
    /// <returns>The new set; this set is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signingKeyId"/> is <see langword="null"/>.</exception>
    public TrustedSigningKeys Without(string signingKeyId)
    {
        ArgumentNullException.ThrowIfNull(signingKeyId);

        if (!_keys.ContainsKey(signingKeyId))
        {
            return this;
        }

        var copy = new Dictionary<string, SigningPublicKey>(_keys);
        copy.Remove(signingKeyId);
        return new TrustedSigningKeys(copy);
    }

    /// <summary>Whether a key is held under <paramref name="signingKeyId"/>.</summary>
    /// <param name="signingKeyId">The ID to look for.</param>
    /// <returns><see langword="true"/> if held.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signingKeyId"/> is <see langword="null"/>.</exception>
    public bool Contains(string signingKeyId)
    {
        ArgumentNullException.ThrowIfNull(signingKeyId);
        return _keys.ContainsKey(signingKeyId);
    }

    /// <summary>
    /// Verifies a 64-byte P1363 ECDSA P-256/SHA-256 signature with the key held under
    /// <paramref name="signingKeyId"/>. False for a null or unknown ID or any bad signature; never throws on signature content.
    /// </summary>
    internal bool Verify(string signingKeyId, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        if (signingKeyId is null || signature.Length != SignatureLength || !_keys.TryGetValue(signingKeyId, out var key))
        {
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(key.Spki, out _);
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
