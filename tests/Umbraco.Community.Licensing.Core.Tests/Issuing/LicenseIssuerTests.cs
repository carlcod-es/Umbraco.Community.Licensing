using System.Reflection;
using Umbraco.Community.Licensing.Core.Tests.Signing;
using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Issuing;

public class LicenseIssuerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, 734, TimeSpan.Zero);

    private readonly SigningPrivateKey _key = SigningPrivateKey.FromPem(ExampleKeys.PrivateKeyPem);

    public void Dispose() => _key.Dispose();

    private static LicenseIssuer Issuer(KeyPrefix? prefix = null, IRandomSource? random = null, DateTimeOffset? now = null) =>
        new(prefix, new FixedTimeProvider(now ?? Now), random ?? new FixedRandomSource("8F3AKM7RXB", "7Q2D"));

    private static LicenseRequest Typical() => new()
    {
        Product = "acme.commerce",
        Expiry = LicenseExpiry.On(new DateOnly(2027, 3, 1)),
        DisplayName = "Commerce Standard",
        VendorTag = "CUST-0042",
        Features = FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m),
    };

    private static LicenseRequest Minimal() => new() { Product = "acme.seo-toolkit", Expiry = LicenseExpiry.Perpetual };

    private static LicensePayload ReadPayload(string keyString)
    {
        var envelope = KeyEnvelope.Read(keyString);
        Assert.Equal(KeyEnvelopeState.Read, envelope.State);
        Assert.True(PayloadReader.TryRead(envelope.Payload!, out var payload));
        return payload!;
    }

    [Fact]
    public void TypicalIssue_ReturnsKeyReferenceIdentifierAndIssueTime()
    {
        var issued = Issuer().Issue(Typical(), _key);

        Assert.False(string.IsNullOrEmpty(issued.KeyString));
        Assert.Equal("LIC-8F3AK-M7RXB", issued.Reference);
        Assert.Equal("LIC-8F3AK-M7RXB-7Q2D", issued.KeyIdentifier);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), issued.Issued);

        var payload = ReadPayload(issued.KeyString);
        Assert.Equal("acme.commerce", payload.Product);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero), payload.Expires);
        Assert.Equal("Commerce Standard", payload.DisplayName);
        Assert.Equal("CUST-0042", payload.VendorTag);
        Assert.Equal(
            [FeatureInput.Switch("ecommerce", true), FeatureInput.OfNumber("max-orders", 500m)],
            payload.Features);
    }

    private static LicenseRequest WithFeatures(FeatureList features) => Minimal() with { Features = features };

    [Fact]
    public void Definitions_WrongType_NamesTheFeature()
    {
        var defs = FeatureDefinitions.None.Number("max-orders");
        var request = WithFeatures(FeatureList.Empty.With("max-orders", "500"));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, defs));

        var problem = Assert.Single(ex.Problems);
        Assert.Equal("features.max-orders", problem.Field);
        Assert.Contains("number", problem.Message);
        Assert.Contains("text", problem.Message);
    }

    [Fact]
    public void Definitions_MisspelledName_NamesTheFeature()
    {
        var defs = FeatureDefinitions.None.Number("max-orders");
        var request = WithFeatures(FeatureList.Empty.With("max-order", 500m));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, defs));

        Assert.Equal("features.max-order", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void Definitions_DefinedFeatureLeftOut_IsAccepted()
    {
        var defs = FeatureDefinitions.None.Switch("ecommerce").Number("max-orders").Switch("ai-assist");
        var request = WithFeatures(FeatureList.Empty.With("ecommerce", true));

        var issued = Issuer().Issue(request, _key, defs);

        Assert.Equal([FeatureInput.Switch("ecommerce", true)], ReadPayload(issued.KeyString).Features);
    }

    [Fact]
    public void Definitions_Empty_RejectsAnyFeature()
    {
        var request = WithFeatures(FeatureList.Empty.With("ecommerce", true));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, FeatureDefinitions.None));

        Assert.Equal("features.ecommerce", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void Definitions_EmptyWithNoFeatures_IsAccepted() =>
        Assert.NotNull(Issuer().Issue(Minimal(), _key, FeatureDefinitions.None));

    [Fact]
    public void Definitions_NotPassed_FeatureRulesAlone()
    {
        var request = WithFeatures(FeatureList.Empty.With("max-orders", "500").With("max-order", 500m));

        Assert.NotNull(Issuer().Issue(request, _key));
        Assert.NotNull(Issuer().Issue(request, _key, null));
    }

    [Fact]
    public void Definitions_Matching_IsAccepted()
    {
        var defs = FeatureDefinitions.None.Switch("ecommerce").Number("max-orders");

        Assert.NotNull(Issuer().Issue(Typical(), _key, defs));
    }

    [Fact]
    public void Definitions_Problems_JoinTheOtherProblemsInOneException()
    {
        var defs = FeatureDefinitions.None.Number("max-orders");
        var request = Typical() with { VendorTag = "bad tag", Expiry = null };

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, defs));

        Assert.Equal(
            ["expires", "features.ecommerce", "vendorTag"],
            ex.Problems.Select(p => p.Field).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Definitions_InvalidFeatureName_IsReportedOnceByTheContentRules()
    {
        var request = WithFeatures(FeatureList.Empty.With("Bad Name", true));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, FeatureDefinitions.None));

        var problem = Assert.Single(ex.Problems);
        Assert.Equal("features", problem.Field);
    }

    [Fact]
    public void Definitions_RepeatedUndefinedName_IsReportedOncePerName()
    {
        var request = WithFeatures(FeatureList.Of([
            new LicenseFeature("ecommerce", new SwitchFeature(true)),
            new LicenseFeature("ecommerce", new SwitchFeature(true)),
        ]));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, FeatureDefinitions.None));

        Assert.Single(ex.Problems, p => p.Field == "features.ecommerce");
    }

    [Theory]
    [InlineData("")]
    [InlineData("LONG")]
    public void Definitions_InvalidText_NamesTheFeature(string kind)
    {
        var defs = FeatureDefinitions.None.Text("region");
        var text = kind == "LONG" ? new string('x', 257) : string.Empty;
        var request = WithFeatures(FeatureList.Empty.With("region", text));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, defs));

        Assert.Equal("features.region", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void Definitions_Null_FeatureRulesStillReject()
    {
        var request = WithFeatures(FeatureList.Empty.With("ecommerce", false));

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key, null));

        Assert.Equal("features.ecommerce", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void MinimalIssue_ProducesAKeyString()
    {
        var issued = Issuer().Issue(Minimal(), _key);

        var payload = ReadPayload(issued.KeyString);
        Assert.Equal("acme.seo-toolkit", payload.Product);
        Assert.Null(payload.Expires);
        Assert.Null(payload.DisplayName);
        Assert.Null(payload.VendorTag);
        Assert.Empty(payload.Features);
    }

    [Fact]
    public void SeveralProblems_OneExceptionListsAllAndNothingIsGenerated()
    {
        var random = new FixedRandomSource("8F3AKM7RXB", "7Q2D");
        var request = new LicenseRequest
        {
            Product = "acme.commerce",
            VendorTag = "INV 2026 04",
            Features = FeatureList.Empty.With("max-orders", -200m),
        };

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer(random: random).Issue(request, _key));

        Assert.Equal(["expires", "features.max-orders", "vendorTag"], ex.Problems.Select(p => p.Field).Order(StringComparer.Ordinal));
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void ProblemsFromEveryRule_AreCollectedTogether()
    {
        var request = new LicenseRequest
        {
            Product = "Bad",
            Reference = "LIC-8F3AK-M7RXO",
            Expiry = null,
            DisplayName = "",
            VendorTag = "",
            Features = FeatureList.Empty.With("Bad Name", true),
        };

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        Assert.Equal(
            ["displayName", "expires", "features", "product", "reference", "vendorTag"],
            ex.Problems.Select(p => p.Field).Order(StringComparer.Ordinal));
        Assert.Contains("[reference]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullFeatureValue_IsAProgrammingError_NotAProblem()
    {
        var request = Minimal() with { Features = FeatureList.Of([new LicenseFeature("pro", null!)]) };

        Assert.Throws<InvalidOperationException>(() => Issuer().Issue(request, _key));
    }

    [Fact]
    public void NullArguments_ThrowArgumentNullException()
    {
        var issuer = Issuer();

        Assert.Throws<ArgumentNullException>(() => issuer.Issue((LicenseRequest)null!, _key));
        Assert.Throws<ArgumentNullException>(() => issuer.Issue(Minimal(), null!));
        Assert.Throws<ArgumentNullException>(() => issuer.Issue(Minimal() with { Features = null! }, _key));
    }

    [Fact]
    public void NullSigningKey_WithInvalidRequest_StillThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Issuer().Issue(new LicenseRequest(), null!));
    }

    [Fact]
    public void FirstIssue_GeneratesAReferenceFromTheRandomSource()
    {
        var issued = Issuer(random: new FixedRandomSource("23456789AB", "CDEF")).Issue(Minimal(), _key);

        Assert.Equal("LIC-23456-789AB", issued.Reference);
        Assert.Equal("LIC-23456-789AB-CDEF", issued.KeyIdentifier);
    }

    [Fact]
    public void DefaultRandomSource_GeneratesAValidReferenceAndKeyPart()
    {
        var issued = new LicenseIssuer(timeProvider: new FixedTimeProvider(Now)).Issue(Minimal(), _key);

        Assert.Matches(@"^LIC-[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{5}$", issued.Reference);
        Assert.StartsWith(issued.Reference + "-", issued.KeyIdentifier, StringComparison.Ordinal);
        Assert.StartsWith(issued.KeyIdentifier + ".", issued.KeyString, StringComparison.Ordinal);
    }

    [Fact]
    public void Reissue_KeepsTheReference_NewKeyPartEachTime()
    {
        var random = new FixedRandomSource("8F3AKM7RXB", "7Q2D", "9ZZZ");
        var issuer = Issuer(random: random);
        var request = Minimal() with { Reference = "LIC-8F3AK-M7RXB" };

        var first = issuer.Issue(request, _key);
        var second = issuer.Issue(request, _key);

        Assert.Equal("LIC-8F3AK-M7RXB", first.Reference);
        Assert.Equal("LIC-8F3AK-M7RXB", second.Reference);
        Assert.Equal("LIC-8F3AK-M7RXB-7Q2D", first.KeyIdentifier);
        Assert.Equal("LIC-8F3AK-M7RXB-9ZZZ", second.KeyIdentifier);
        Assert.Equal(0, random.ReferenceCalls);
    }

    [Theory]
    [InlineData("lic-8f3ak m7rxb")]
    [InlineData("8F3AK-M7RXB")]
    public void GivenReference_IsAcceptedLoosely(string reference)
    {
        var issued = Issuer().Issue(Minimal() with { Reference = reference }, _key);

        Assert.Equal("LIC-8F3AK-M7RXB", issued.Reference);
    }

    [Theory]
    [InlineData("LIC-8F3AK-M7RXB")]
    [InlineData("ACME-8F3AK-M7RXB-7Q2D")]
    [InlineData("8F3AK-M7RXO")]
    [InlineData("8F3AK-M7RX")]
    [InlineData("8F3AK-M7RXBK")]
    public void InvalidReference_NamesReference(string reference)
    {
        var ex = Assert.Throws<LicenseIssueException>(() =>
            Issuer(KeyPrefix.Of("ACME")).Issue(Minimal() with { Reference = reference }, _key));

        Assert.Equal("reference", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void IssueTime_IsTheClockTruncatedToTheSecond()
    {
        var issued = Issuer(now: new DateTimeOffset(2026, 10, 1, 8, 0, 0, 734, TimeSpan.Zero)).Issue(Minimal(), _key);

        var expected = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, issued.Issued);
        Assert.Equal(TimeSpan.Zero, issued.Issued.Offset);
        Assert.Equal(expected, ReadPayload(issued.KeyString).Issued);
    }

    [Fact]
    public void IssueTime_FromANonUtcClock_IsUtc()
    {
        var issued = Issuer(now: new DateTimeOffset(2026, 10, 1, 10, 0, 0, 999, TimeSpan.FromHours(2))).Issue(Minimal(), _key);

        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), issued.Issued);
        Assert.Equal(TimeSpan.Zero, issued.Issued.Offset);
    }

    [Fact]
    public void PastExpiry_IsReportedAgainstTheIssuersClock()
    {
        var ex = Assert.Throws<LicenseIssueException>(() =>
            Issuer().Issue(Minimal() with { Expiry = LicenseExpiry.On(new DateOnly(2026, 9, 30)) }, _key));

        Assert.Equal("expires", Assert.Single(ex.Problems).Field);
    }

    [Fact]
    public void Issue_ReadsTheClockOnce_SoAnExpiryEqualToTheCurrentSecondIsNeverBeforeTheIssueTime()
    {
        var clock = new AdvancingTimeProvider(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), TimeSpan.FromSeconds(1));
        var issuer = new LicenseIssuer(null, clock, new FixedRandomSource("8F3AKM7RXB", "7Q2D"));

        var issued = issuer.Issue(Minimal() with { Expiry = LicenseExpiry.At(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)) }, _key);

        Assert.Equal(1, clock.Reads);
        var payload = ReadPayload(issued.KeyString);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), payload.Expires);
        Assert.True(payload.Expires >= payload.Issued);
        Assert.Equal(payload.Issued, issued.Issued);
    }

    [Fact]
    public void Issue_WithProblems_ReadsTheClockOnce()
    {
        var clock = new AdvancingTimeProvider(Now, TimeSpan.FromSeconds(1));
        var issuer = new LicenseIssuer(null, clock, new FixedRandomSource("8F3AKM7RXB", "7Q2D"));

        Assert.Throws<LicenseIssueException>(() => issuer.Issue(Minimal() with { Product = null }, _key));

        Assert.Equal(1, clock.Reads);
    }

    [Fact]
    public void Issue_NullFeatures_ThrowsNamingTheFeaturesMember()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Issuer().Issue(Minimal() with { Features = null! }, _key));

        Assert.Equal("request.Features", ex.ParamName);
    }

    [Fact]
    public void PasteableKey_IsOneLinePrintableAsciiStartingWithTheIdentifier()
    {
        var request = Typical() with { DisplayName = "Café Ångström 日本" };

        var issued = Issuer().Issue(request, _key);

        Assert.All(issued.KeyString, c => Assert.InRange(c, '!', '~'));
        Assert.StartsWith(issued.KeyIdentifier + ".", issued.KeyString, StringComparison.Ordinal);
        Assert.Equal(request.DisplayName, ReadPayload(issued.KeyString).DisplayName);
    }

    [Fact]
    public void IssueThenRead_VerifiesWithThePublicKeyAndPayloadRoundTrips()
    {
        using var pair = SigningKeyPair.Create();
        var trusted = TrustedSigningKeys.Create(pair.PublicKey);
        var issued = Issuer().Issue(Typical(), pair.PrivateKey);

        var envelope = KeyEnvelope.Read(issued.KeyString);

        Assert.Equal(KeyEnvelopeState.Read, envelope.State);
        Assert.Equal(issued.KeyIdentifier, envelope.Identifier);
        Assert.Equal(64, envelope.Signature!.Length);
        Assert.True(trusted.Verify(pair.SigningKeyId, envelope.SigningInput, envelope.Signature));
        Assert.False(TrustedSigningKeys.Create(SigningPublicKey.Parse(ExampleKeys.PublicKey))
            .Verify(ExampleKeys.SigningKeyId, envelope.SigningInput, envelope.Signature));

        Assert.True(PayloadReader.TryRead(envelope.Payload!, out var read));
        Assert.Equal(pair.SigningKeyId, read!.SigningKeyId);
        Assert.Equal(11, read.SigningKeyId.Length);
        Assert.Equal(issued.Issued, read.Issued);
        Assert.Equal(PayloadWriter.Write(read), envelope.Payload);
    }

    [Fact]
    public void DefaultPrefix_IsLic()
    {
        var issued = new LicenseIssuer(null, new FixedTimeProvider(Now), new FixedRandomSource("8F3AKM7RXB", "7Q2D")).Issue(Minimal(), _key);

        Assert.Equal("LIC-8F3AK-M7RXB-7Q2D", issued.KeyIdentifier);
    }

    [Fact]
    public void VendorPrefix_AppliesToReferenceAndIdentifier()
    {
        var issued = Issuer(KeyPrefix.Of("ACME")).Issue(Minimal(), _key);

        Assert.Equal("ACME-8F3AK-M7RXB", issued.Reference);
        Assert.Equal("ACME-8F3AK-M7RXB-7Q2D", issued.KeyIdentifier);
        Assert.StartsWith("ACME-8F3AK-M7RXB-7Q2D.", issued.KeyString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("acme 8f3ak m7rxb")]
    [InlineData("8F3AK-M7RXB")]
    public void VendorPrefix_ReferenceAcceptedWithOrWithoutPrefix(string reference)
    {
        var issued = Issuer(KeyPrefix.Of("ACME")).Issue(Minimal() with { Reference = reference }, _key);

        Assert.Equal("ACME-8F3AK-M7RXB", issued.Reference);
    }

    [Fact]
    public void NoPrefix_OmitsThePrefix()
    {
        var issued = Issuer(KeyPrefix.None).Issue(Minimal(), _key);

        Assert.Equal("8F3AK-M7RXB", issued.Reference);
        Assert.Equal("8F3AK-M7RXB-7Q2D", issued.KeyIdentifier);
        Assert.StartsWith("8F3AK-M7RXB-7Q2D.", issued.KeyString, StringComparison.Ordinal);
    }

    [Fact]
    public void NoPrefix_RejectsAPrefixedReference()
    {
        Assert.Throws<LicenseIssueException>(() =>
            Issuer(KeyPrefix.None).Issue(Minimal() with { Reference = "LIC-8F3AK-M7RXB" }, _key));
    }

    [Fact]
    public void IssuedLicenseToString_OmitsTheKeyString()
    {
        var issued = Issuer().Issue(Minimal(), _key);

        var text = issued.ToString();

        Assert.DoesNotContain(issued.KeyString, text, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyString", text, StringComparison.Ordinal);
        Assert.Contains(issued.KeyIdentifier, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Issuer_RetainsNoKeyRequestOrKeyString()
    {
        var fields = typeof(LicenseIssuer).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.All(fields, f => Assert.True(
            f.FieldType == typeof(KeyPrefix) || f.FieldType == typeof(TimeProvider) || f.FieldType == typeof(IRandomSource),
            f.Name));
    }

    [Fact]
    public void Exception_ListsProblemsInMessage()
    {
        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(new LicenseRequest(), _key));

        Assert.Contains(ex.Problems, p => p.Field == "product");
        Assert.Contains("[product]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyTooLong_RejectedNamingFeaturesAndTheExcess()
    {
        var request = Minimal() with { Features = KeyLengthSupport.TextFeatures(50, KeyLengthSupport.Emoji(256)) };
        var length = KeyLengthSupport.KeyLength(_key.SigningKeyId, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), request.Product!, null, null, request.Features);

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        var problem = Assert.Single(ex.Problems);
        Assert.Equal("features", problem.Field);
        var over = (length - 32_767).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(length > 32_767);
        Assert.Contains($"({over} characters over the limit of 32,767)", problem.Message);
    }

    [Fact]
    public void KeyTooLong_OtherProblemsReportedWithoutALengthProblem()
    {
        var request = Minimal() with
        {
            Product = "Not Valid",
            Features = KeyLengthSupport.TextFeatures(50, KeyLengthSupport.Emoji(256)),
        };

        var ex = Assert.Throws<LicenseIssueException>(() => Issuer().Issue(request, _key));

        Assert.DoesNotContain(ex.Problems, p => p.Message.Contains("too large for a key"));
        Assert.Contains(ex.Problems, p => p.Field == "product");
    }

    [Fact]
    public void KeyLength_Exactly32767_IsIssued_OneMoreIsRejected()
    {
        // Emoji take the key close to the limit; single letters then walk the length to the exact value.
        var baseValue = KeyLengthSupport.Emoji(256);
        var features = KeyLengthSupport.TextFeatures(22, baseValue).With("pad", new string('a', 256));
        var padded = features;
        var length = 0;
        for (var letters = 1; letters <= 256; letters++)
        {
            padded = features.With("pad2", new string('a', letters));
            length = KeyLengthSupport.KeyLength(_key.SigningKeyId, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), "acme.x", null, null, padded);
            if (length >= 32_767)
            {
                break;
            }
        }

        Assert.Equal(32_767, length);
        var issued = Issuer().Issue(new LicenseRequest { Product = "acme.x", Expiry = LicenseExpiry.Perpetual, Features = padded }, _key);
        Assert.Equal(32_767, issued.KeyString.Length);

        var over = padded.With("pad3", "a");
        var ex = Assert.Throws<LicenseIssueException>(
            () => Issuer().Issue(new LicenseRequest { Product = "acme.x", Expiry = LicenseExpiry.Perpetual, Features = over }, _key));
        Assert.Equal("features", Assert.Single(ex.Problems).Field);
    }

    // ADR-0007 worst case: 22,212 characters with a 16-character prefix. The default LIC prefix is 13 characters shorter, so
    // the same license measures 22,199.
    [Theory]
    [InlineData(null, 22_199)]
    [InlineData("ACMECOMMERCE2026", 22_212)]
    public void LargeLicenseFits_EveryFieldAtItsLimit_IssuedAndReadBack(string? prefix, int expectedLength)
    {
        var product = "a" + new string('b', 31) + "." + new string('c', 31);
        var display = new string('D', 64);
        var tag = new string('T', 64);
        var value = string.Concat(Enumerable.Repeat("aB3dE5gH7j", 26))[..256];
        var features = KeyLengthSupport.TextFeatures(50, value);
        var request = new LicenseRequest
        {
            Product = product,
            Expiry = LicenseExpiry.On(new DateOnly(2027, 3, 1)),
            DisplayName = display,
            VendorTag = tag,
            Features = features,
        };

        var issued = Issuer(prefix is null ? null : KeyPrefix.Of(prefix)).Issue(request, _key);

        Assert.True(issued.KeyString.Length <= 32_767);
        Assert.Equal(expectedLength, issued.KeyString.Length);
        var envelope = KeyEnvelope.Read(issued.KeyString);
        Assert.Equal(KeyEnvelopeState.Read, envelope.State);
        var payload = ReadPayload(issued.KeyString);
        Assert.Equal(product, payload.Product);
        Assert.Equal(display, payload.DisplayName);
        Assert.Equal(tag, payload.VendorTag);
        Assert.Equal(features.ToInputs(), payload.Features);
    }

    private sealed class FixedRandomSource(string reference, params string[] keyParts) : IRandomSource
    {
        private int _next;

        public int ReferenceCalls { get; private set; }

        public int KeyPartCalls { get; private set; }

        public int Calls => ReferenceCalls + KeyPartCalls;

        public string NextReference()
        {
            ReferenceCalls++;
            return reference;
        }

        public string NextKeyPart()
        {
            KeyPartCalls++;
            return keyParts[Math.Min(_next++, keyParts.Length - 1)];
        }
    }
}
