using System.Security.Cryptography;

namespace Umbraco.Community.Licensing.Core;

/// <summary>Source of the random reference and key part. A seam so tests can fix both.</summary>
internal interface IRandomSource
{
    /// <summary>Returns a new reference: 10 characters from <see cref="ReferenceAlphabet"/>, no prefix, no hyphen.</summary>
    string NextReference();

    /// <summary>Returns a new key part: 4 characters from <see cref="ReferenceAlphabet"/>.</summary>
    string NextKeyPart();
}

/// <summary>Cryptographically secure source; every character is chosen uniformly from the alphabet.</summary>
internal sealed class SecureRandomSource : IRandomSource
{
    public static SecureRandomSource Instance { get; } = new();

    public string NextReference() => Next(ReferenceAlphabet.ReferenceLength);

    public string NextKeyPart() => Next(ReferenceAlphabet.KeyPartLength);

    // GetItems selects by rejection sampling, so there is no modulo bias over 31 characters.
    private static string Next(int length) =>
        new(RandomNumberGenerator.GetItems<char>(ReferenceAlphabet.Characters, length));
}
