using System.Reflection;
using Umbraco.Community.Licensing.Core.Tests.Signing;
using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Issuing;

public class ReissueTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, 734, TimeSpan.Zero);

    private readonly SigningPrivateKey _key = SigningPrivateKey.FromPem(ExampleKeys.PrivateKeyPem);

    public void Dispose() => _key.Dispose();

    private static LicenseIssuer Issuer(KeyPrefix? prefix = null, DateTimeOffset? now = null) =>
        new(prefix, new FixedTimeProvider(now ?? Now), new FixedRandomSource("ZZZZZZZZZZ", "9XYZ"));

    private static VerifiedLicense License(
        DateTimeOffset? expires = null,
        string reference = "LIC-8F3AK-M7RXB",
        FeatureList? features = null) => new(
            "acme.commerce",
            reference,
            reference + "-7Q2D",
            "key-1",
            new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            expires,
            "Commerce Standard",
            "CUST-0042",
            features ?? FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m));

    private static readonly DateTimeOffset FutureExpiry = new(2027, 3, 1, 23, 59, 59, TimeSpan.Zero);

    private static LicensePayload ReadPayload(string keyString)
    {
        var envelope = KeyEnvelope.Read(keyString);
        Assert.Equal(KeyEnvelopeState.Read, envelope.State);
        Assert.True(PayloadReader.TryRead(envelope.Payload!, out var payload));
        return payload!;
    }

    [Fact]
    public void AddAFeature_SameReferenceNewKeyPartNewIssueTime()
    {
        var license = License(FutureExpiry, features: FeatureList.Empty.With("max-orders", 500m));
        var request = ReissueRequest.From(license) with { Features = license.Features.With("max-orders", 1500m) };

        var issued = Issuer().Issue(request, _key);

        Assert.Equal("LIC-8F3AK-M7RXB", issued.Reference);
        Assert.Equal("LIC-8F3AK-M7RXB-9XYZ", issued.KeyIdentifier);
        Assert.NotEqual(license.KeyIdentifier, issued.KeyIdentifier);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), issued.Issued);
        var payload = ReadPayload(issued.KeyString);
        Assert.Equal([FeatureInput.OfNumber("max-orders", 1500m)], payload.Features);
        Assert.Equal(FutureExpiry, payload.Expires);
    }

    [Fact]
    public void PrefilledPastExpiry_IssuedUnchanged_IsRejectedNamingExpires()
    {
        var request = ReissueRequest.From(License(new DateTimeOffset(2026, 9, 1, 23, 59, 59, TimeSpan.Zero)));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        Assert.Equal("expires", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void PrefilledPastExpiry_WithNewExpiry_IsIssued()
    {
        var request = ReissueRequest.From(License(new DateTimeOffset(2026, 9, 1, 23, 59, 59, TimeSpan.Zero)))
            with
        { Expiry = LicenseExpiry.On(new DateOnly(2027, 9, 1)) };

        var issued = Issuer().Issue(request, _key);

        Assert.Equal(new DateTimeOffset(2027, 9, 1, 23, 59, 59, TimeSpan.Zero), ReadPayload(issued.KeyString).Expires);
    }

    [Fact]
    public void PerpetualCarriedOver_ExpiryIsStatedPerpetual()
    {
        var license = License(expires: null);

        var request = ReissueRequest.From(license);

        Assert.True(license.IsPerpetual);
        Assert.Same(LicenseExpiry.Perpetual, request.Expiry);
        Assert.Null(ReadPayload(Issuer().Issue(request, _key).KeyString).Expires);
    }

    [Fact]
    public void NotPerpetual_ExpiryIsTheInstant()
    {
        var request = ReissueRequest.From(License(FutureExpiry));

        Assert.Equal(LicenseExpiry.At(FutureExpiry), request.Expiry);
    }

    [Fact]
    public void ProductAndReferenceKept_HaveNoSetterAndOtherFieldsCanChange()
    {
        var request = ReissueRequest.From(License(FutureExpiry));

        Assert.Equal("acme.commerce", request.Product);
        Assert.Equal("LIC-8F3AK-M7RXB", request.Reference);
        Assert.Null(typeof(ReissueRequest).GetProperty(nameof(ReissueRequest.Product))!.SetMethod);
        Assert.Null(typeof(ReissueRequest).GetProperty(nameof(ReissueRequest.Reference))!.SetMethod);
        foreach (var name in new[] { "Expiry", "DisplayName", "VendorTag", "Features" })
        {
            Assert.NotNull(typeof(ReissueRequest).GetProperty(name)!.SetMethod);
        }

        var changed = request with
        {
            Expiry = LicenseExpiry.Perpetual,
            DisplayName = "Commerce Pro",
            VendorTag = "CUST-0043",
            Features = FeatureList.Empty.With("ai-assist", true),
        };
        var payload = ReadPayload(Issuer().Issue(changed, _key).KeyString);
        Assert.Equal("acme.commerce", payload.Product);
        Assert.Null(payload.Expires);
        Assert.Equal("Commerce Pro", payload.DisplayName);
        Assert.Equal("CUST-0043", payload.VendorTag);
        Assert.Equal([FeatureInput.Switch("ai-assist", true)], payload.Features);
    }

    [Fact]
    public void ProductAndReferenceKept_ConstructorIsNotPublic() =>
        Assert.Empty(typeof(ReissueRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    [Fact]
    public void PrefixChangedByTheVendor_NewIdentifierStartsWithTheIssuersPrefix()
    {
        var request = ReissueRequest.From(License(FutureExpiry));

        var issued = Issuer(KeyPrefix.Of("ACME")).Issue(request, _key);

        Assert.StartsWith("ACME-8F3AK-M7RXB-", issued.KeyIdentifier, StringComparison.Ordinal);
        Assert.Equal("ACME-8F3AK-M7RXB", issued.Reference);
        Assert.StartsWith("ACME-8F3AK-M7RXB-", issued.KeyString, StringComparison.Ordinal);
        Assert.Null(typeof(ReissueRequest).GetProperty(nameof(ReissueRequest.Reference))!.SetMethod);
    }

    [Fact]
    public void NoPrefixIssuer_ReissuesWithoutPrefix()
    {
        var issued = Issuer(KeyPrefix.None).Issue(ReissueRequest.From(License(FutureExpiry)), _key);

        Assert.Equal("8F3AK-M7RXB", issued.Reference);
        Assert.StartsWith("8F3AK-M7RXB-", issued.KeyIdentifier, StringComparison.Ordinal);
    }

    [Fact]
    public void UnprefixedReference_IsKeptUnderTheIssuersPrefix()
    {
        var issued = Issuer().Issue(ReissueRequest.From(License(FutureExpiry, reference: "8F3AK-M7RXB")), _key);

        Assert.Equal("LIC-8F3AK-M7RXB", issued.Reference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("LIC-8F3AK-M7RX")]
    [InlineData("LIC-8F3AK-M7RXO")]
    [InlineData("lic-8F3AK-M7RXB")]
    [InlineData("LIC-8F3AKM7RXB")]
    [InlineData("ABCDEFGHIJ1234567-8F3AK-M7RXB")]
    [InlineData("LIC_-8F3AK-M7RXB")]
    public void MalformedReference_NamesReferenceInTheSingleException(string reference)
    {
        var request = ReissueRequest.From(License(FutureExpiry, reference: reference));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        Assert.Equal("reference", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void MalformedReference_JoinsOtherProblems()
    {
        var request = ReissueRequest.From(License(FutureExpiry, reference: "nope")) with { VendorTag = "bad tag" };

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        Assert.Equal(["reference", "vendorTag"], ex.Problems.Select(p => p.Field).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Definitions_AreStillApplied()
    {
        var request = ReissueRequest.From(License(FutureExpiry));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, FeatureDefinitions.None.Switch("ecommerce")));
        Assert.Equal("features.max-orders", Assert.Single(ex.Problems).Field);

        Assert.NotNull(Issuer().Issue(request, _key, FeatureDefinitions.None.Switch("ecommerce").Number("max-orders")));
    }

    [Fact]
    public void ExpiryNotStated_NamesExpires()
    {
        var request = ReissueRequest.From(License(FutureExpiry)) with { Expiry = null };

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        Assert.Equal("expires", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void From_ReadsNoClock_TheIssuersClockAloneDecidesTheExpiry()
    {
        var request = ReissueRequest.From(License(new DateTimeOffset(2026, 9, 1, 23, 59, 59, TimeSpan.Zero)));

        Assert.NotNull(Issuer(now: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)).Issue(request, _key));
        Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));
    }

    [Fact]
    public void From_Null_Throws() => Assert.Throws<ArgumentNullException>(() => ReissueRequest.From(null!));

    [Fact]
    public void Issue_NullArguments_Throw()
    {
        var request = ReissueRequest.From(License(FutureExpiry));

        Assert.Throws<ArgumentNullException>(() => Issuer().Issue((ReissueRequest)null!, _key));
        Assert.Throws<ArgumentNullException>(() => Issuer().Issue(request, null!));
        Assert.Throws<ArgumentNullException>(() => Issuer().Issue(request with { Features = null! }, _key));
    }

    [Fact]
    public void ReissuedKey_ReadsBackWithTheSameProductAndReference()
    {
        var request = ReissueRequest.From(License(FutureExpiry));

        var issued = Issuer().Issue(request, _key);

        var envelope = KeyEnvelope.Read(issued.KeyString);
        Assert.Equal(KeyEnvelopeState.Read, envelope.State);
        Assert.Equal(issued.KeyIdentifier, envelope.Identifier);
        var payload = ReadPayload(issued.KeyString);
        Assert.True(TrustedSigningKeys.Create(_key.PublicKey).Verify(payload.SigningKeyId, envelope.SigningInput, envelope.Signature));
        Assert.Equal(request.Product, payload.Product);
        Assert.Equal("8F3AKM7RXB", KeyIdentifierFormat.ReferenceOf(envelope.Identifier!));
    }

    [Fact]
    public void KeyTooLong_ReissueWithAddedTextFeatures_IsRejectedNamingFeatures()
    {
        var license = License(FutureExpiry, features: KeyLengthSupport.TextFeatures(10, "short"));
        var request = ReissueRequest.From(license) with
        {
            Features = KeyLengthSupport.TextFeatures(50, KeyLengthSupport.Emoji(256)),
        };

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        var problem = Assert.Single(ex.Problems);
        Assert.Equal("features", problem.Field);
        Assert.Contains("characters over the limit of 32,767", problem.Message);
    }

    private sealed class FixedRandomSource(string reference, string keyPart) : IRandomSource
    {
        public string NextReference() => reference;

        public string NextKeyPart() => keyPart;
    }
}
