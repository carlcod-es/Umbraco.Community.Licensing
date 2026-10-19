namespace Umbraco.Community.Licensing.Core.Tests.Signing;

public class TrustedSigningKeysTests
{
    private static readonly byte[] Data = "arbitrary bytes"u8.ToArray();

    [Fact]
    public void SeveralKeys_SignaturesFromEitherPrivateKeyVerify()
    {
        using var a = SigningKeyPair.Create();
        using var b = SigningKeyPair.Create();

        var trusted = TrustedSigningKeys.Create(a.PublicKey, b.PublicKey);

        Assert.Equal(2, trusted.SigningKeyIds.Count);
        Assert.True(trusted.Verify(a.SigningKeyId, Data, a.PrivateKey.Sign(Data)));
        Assert.True(trusted.Verify(b.SigningKeyId, Data, b.PrivateKey.Sign(Data)));
    }

    [Fact]
    public void SameKeyTwice_HeldOnceNoError()
    {
        using var a = SigningKeyPair.Create();
        var set = TrustedSigningKeys.Empty.With(a.PublicKey);

        var again = set.With(SigningPublicKey.Parse(a.PublicKey.Export()));

        Assert.Equal([a.SigningKeyId], again.SigningKeyIds);
        Assert.Equal([a.SigningKeyId], TrustedSigningKeys.Create(a.PublicKey, a.PublicKey).SigningKeyIds);
    }

    [Fact]
    public void DifferentKeyUnderHeldId_ThrowsAndSetUnchanged()
    {
        using var a = SigningKeyPair.Create();
        using var b = SigningKeyPair.Create();
        var forced = new SigningPublicKey(a.SigningKeyId, b.PublicKey.Spki);
        var set = TrustedSigningKeys.Create(a.PublicKey);

        Assert.Throws<ArgumentException>(() => set.With(forced));
        Assert.Throws<ArgumentException>(() => TrustedSigningKeys.Create(a.PublicKey, forced));

        Assert.Equal([a.SigningKeyId], set.SigningKeyIds);
        Assert.True(set.Verify(a.SigningKeyId, Data, a.PrivateKey.Sign(Data)));
        Assert.False(set.Verify(a.SigningKeyId, Data, b.PrivateKey.Sign(Data)));
    }

    [Fact]
    public void Empty_HoldsNothingAndVerifiesNothing()
    {
        using var a = SigningKeyPair.Create();

        Assert.Empty(TrustedSigningKeys.Empty.SigningKeyIds);
        Assert.False(TrustedSigningKeys.Empty.Contains(a.SigningKeyId));
        Assert.False(TrustedSigningKeys.Empty.Verify(a.SigningKeyId, Data, a.PrivateKey.Sign(Data)));
        Assert.Empty(TrustedSigningKeys.Create().SigningKeyIds);
    }

    [Fact]
    public void With_IsImmutable()
    {
        using var a = SigningKeyPair.Create();
        var empty = TrustedSigningKeys.Empty;

        var one = empty.With(a.PublicKey);

        Assert.Empty(empty.SigningKeyIds);
        Assert.True(one.Contains(a.SigningKeyId));
    }

    [Fact]
    public void Without_RemovesKeyAndIgnoresUnheldId()
    {
        using var a = SigningKeyPair.Create();
        using var b = SigningKeyPair.Create();
        var set = TrustedSigningKeys.Create(a.PublicKey, b.PublicKey);

        var withoutA = set.Without(a.SigningKeyId);

        Assert.False(withoutA.Contains(a.SigningKeyId));
        Assert.True(withoutA.Contains(b.SigningKeyId));
        Assert.False(withoutA.Verify(a.SigningKeyId, Data, a.PrivateKey.Sign(Data)));
        Assert.True(set.Contains(a.SigningKeyId));
        Assert.Equal(set.SigningKeyIds, set.Without("AAAAAAAAAAA").SigningKeyIds);
    }

    [Fact]
    public void NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TrustedSigningKeys.Empty.With(null!));
        Assert.Throws<ArgumentNullException>(() => TrustedSigningKeys.Empty.Without(null!));
        Assert.Throws<ArgumentNullException>(() => TrustedSigningKeys.Empty.Contains(null!));
        Assert.Throws<ArgumentNullException>(() => TrustedSigningKeys.Create(default(IEnumerable<SigningPublicKey>)!));
        Assert.Throws<ArgumentNullException>(() => TrustedSigningKeys.Create(new SigningPublicKey[] { null! }));
    }
}
