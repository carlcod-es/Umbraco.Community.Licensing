using System.Text;
using Umbraco.Community.Licensing.Core.Tests.Issuing;
using Umbraco.Community.Licensing.Core.Tests.Signing;
using Umbraco.Community.Licensing.Core.Tests.Support;
using static Umbraco.Community.Licensing.Core.Tests.Evaluation.EvaluationSupport;

namespace Umbraco.Community.Licensing.Core.Tests.Evaluation;

public class LicenseEvaluatorTests
{
    private const string Identifier = "LIC-8F3AK-M7RXB-7Q2D";

    // Evaluate one key for one product

    [Theory]
    [InlineData("Hunter2!")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D.eyJzaWdu")]
    public void GarbageNeverThrows_HostileStrings_ReturnAResult(string key)
    {
        var result = Evaluator().Evaluate(key);

        Assert.False(result.IsLicensed);
    }

    [Fact]
    public void GarbageNeverThrows_CutOffKey_ReturnsAResult()
    {
        var key = Issue();

        for (var length = 0; length < key.Length; length += 7)
        {
            Assert.False(Evaluator().Evaluate(key[..length]).IsLicensed);
        }
    }

    [Fact]
    public void GarbageNeverThrows_OneCharacterChanged_ReturnsAResultThatIsNotValid()
    {
        var key = Issue();
        var evaluator = Evaluator();

        for (var i = 0; i < key.Length; i += 11)
        {
            var changed = key[..i] + (key[i] == 'A' ? 'B' : 'A') + key[(i + 1)..];
            Assert.False(evaluator.Evaluate(changed).IsLicensed);
        }
    }

    [Fact]
    public void GarbageNeverThrows_OneMillionRandomCharacters_ReturnsUnreadable()
    {
        var random = new Random(20261012);
        var text = new string(Enumerable.Range(0, 1_000_000).Select(_ => (char)random.Next(0x20, 0x2FFF)).ToArray());

        var result = Evaluator().Evaluate(text);

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Null(result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void InvalidProductId_RaisesAtSetup()
    {
        Assert.Throws<ArgumentException>(() => new LicenseEvaluator("Acme Commerce", Trusted));
        Assert.Throws<ArgumentNullException>(() => new LicenseEvaluator(null!, Trusted));
        Assert.Throws<ArgumentException>(() => new LicenseEvaluator("", Trusted));
    }

    [Fact]
    public void EmptyTrustedSet_RaisesAtSetup()
    {
        Assert.Throws<ArgumentException>(() => new LicenseEvaluator(Product, TrustedSigningKeys.Empty));
        Assert.Throws<ArgumentNullException>(() => new LicenseEvaluator(Product, null!));
    }

    [Fact]
    public void ProductId_IsTheOneGivenAtSetup() => Assert.Equal(Product, Evaluator().ProductId);

    [Fact]
    public void Evaluate_WithTheSystemClock_Works()
    {
        var evaluator = new LicenseEvaluator(Product, Trusted);

        Assert.Equal(LicenseState.Missing, evaluator.Evaluate(null).State);
    }

    // Missing key

    [Fact]
    public void NoKey_IsMissing() => Assert.Equal(LicenseState.Missing, Evaluator().Evaluate(null).State);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" \t\n ")]
    public void EmptyPlaceholder_IsMissing(string placeholder) =>
        Assert.Equal(LicenseState.Missing, Evaluator().Evaluate(placeholder).State);

    // One state, first failing check wins

    [Fact]
    public void WrongProductBeforeSignature_UntrustedSigner_IsWrongProduct()
    {
        var key = Issue(Request("zenith.commerce-shipping"), key: Stranger);

        var result = Evaluator().Evaluate(key);

        Assert.Equal(LicenseState.WrongProduct, result.State);
        Assert.Equal("zenith.commerce-shipping", result.ClaimedProduct);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void UnknownSigningKey_IsSigningKeyNotRecognised()
    {
        var result = Evaluator().Evaluate(Issue(key: Stranger));

        Assert.Equal(LicenseState.SigningKeyNotRecognised, result.State);
        Assert.Equal(Product, result.ClaimedProduct);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void EditedContents_AreNotVerified()
    {
        var edited = WithPayload(Issue(), EditedPayload());

        var result = Evaluator().Evaluate(edited);

        Assert.Equal(LicenseState.NotVerified, result.State);
    }

    [Fact]
    public void VerifiedButNotReadableByThisProduct_UnknownField_IsNotSupportedNotUnreadable()
    {
        var key = SignRaw(RawPayload(extra: ",\"z\":1"));

        var result = Evaluator().Evaluate(key);

        Assert.Equal(LicenseState.NotSupported, result.State);
        Assert.Equal(Product, result.ClaimedProduct);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
        Assert.Null(result.License);
    }

    [Fact]
    public void Valid_TrustedUnexpiredKey_IsLicensed()
    {
        var result = Evaluator().Evaluate(Issue());

        Assert.Equal(LicenseState.Valid, result.State);
        Assert.True(result.IsLicensed);
    }

    [Fact]
    public void FirstFailingCheckWins_UnreadablePayloadBeatsEverythingAfterIt()
    {
        // Verified payload that routing cannot read: no product.
        var key = SignRaw("{\"k\":\"" + ExampleKeys.SigningKeyId + "\",\"i\":1}");

        Assert.Equal(LicenseState.Unreadable, Evaluator().Evaluate(key).State);
    }

    [Fact]
    public void ClaimedProductBreakingTheProductIdRule_IsWrongProductWithNoClaimedProduct()
    {
        var product = new string('a', 2_500) + "\n" + new string('b', 2_499);
        var json = RawPayload(product: product.Replace("\n", "\\n"));
        var key = KeyEnvelope.Compose(Identifier, Encoding.UTF8.GetBytes(json), Stranger.Sign);

        var result = Evaluator().Evaluate(key);

        Assert.Equal(5_000, product.Length);
        Assert.Equal(LicenseState.WrongProduct, result.State);
        Assert.Null(result.ClaimedProduct);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
    }

    [Theory]
    [InlineData("Acme.Commerce", LicenseState.WrongProduct)]
    [InlineData("acme", LicenseState.WrongProduct)]
    [InlineData("acme.", LicenseState.WrongProduct)]
    [InlineData("1acme.commerce", LicenseState.WrongProduct)]
    [InlineData("acme.commerce.extra", LicenseState.WrongProduct)]
    [InlineData("acme commerce", LicenseState.WrongProduct)]
    [InlineData("acme.\\uD800x", LicenseState.Unreadable)]
    public void ClaimedProductBreakingTheProductIdRule_IsNotReportedAndNeverThrows(string escaped, LicenseState expected)
    {
        var key = KeyEnvelope.Compose(Identifier, Encoding.UTF8.GetBytes(RawPayload(product: escaped)), Key.Sign);

        var result = Evaluator().Evaluate(key);

        Assert.Equal(expected, result.State);
        Assert.Null(result.ClaimedProduct);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void ClaimedProductAtTheLengthLimit_IsReportedAt64AndNotAt65(int length, bool reported)
    {
        // A well-formed two-part ID (dot included in the count): only its length decides, per PDR-0026.
        var product = new string('a', 31) + "." + new string('b', length - 32);
        var key = KeyEnvelope.Compose(Identifier, Encoding.UTF8.GetBytes(RawPayload(product: product)), Key.Sign);

        var result = Evaluator().Evaluate(key);

        Assert.Equal(length, product.Length);
        Assert.Equal(LicenseState.WrongProduct, result.State);
        Assert.Equal(reported ? product : null, result.ClaimedProduct);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void ClaimedProductMeetingTheProductIdRule_IsStillReported()
    {
        var key = Issue(Request("acme.forms"), key: Stranger);

        var result = Evaluator().Evaluate(key);

        Assert.Equal(LicenseState.WrongProduct, result.State);
        Assert.Equal("acme.forms", result.ClaimedProduct);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void FirstFailingCheckWins_ExpiredKeyForAnotherProduct_IsWrongProduct()
    {
        var key = Issue(Request("zenith.commerce-shipping", LicenseExpiry.On(new DateOnly(2026, 11, 15))));

        Assert.Equal(LicenseState.WrongProduct, Evaluator(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)).Evaluate(key).State);
    }

    [Fact]
    public void FirstFailingCheckWins_NotSupportedKeyThatIsAlsoPastItsExpiry_IsNotSupported()
    {
        var key = SignRaw(RawPayload(extra: ",\"e\":" + IssueTime.AddDays(1).ToUnixTimeSeconds() + ",\"z\":1"));

        Assert.Equal(LicenseState.NotSupported, Evaluator(Later).Evaluate(key).State);
    }

    private static string EditedPayload() =>
        RawPayload(extra: ",\"e\":" + new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), features: "\"max-orders\":999999");

    // Reading a supplied string

    [Fact]
    public void WrappedByAnEmailClient_IsValid()
    {
        var key = Issue();
        var wrapped = $"{key[..60]}\r\n{key[60..130]}\n  {key[130..]}\n";

        Assert.Equal(LicenseState.Valid, Evaluator().Evaluate(wrapped).State);
    }

    [Fact]
    public void CutOff_IsUnreadableClaimingTheIdentifier()
    {
        var result = Evaluator().Evaluate("LIC-8F3AK-M7RXB-7Q2D.eyJzaWdu");

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
        Assert.Null(result.ClaimedProduct);
    }

    [Fact]
    public void NoIdentifier_IsUnreadableWithNoClaims()
    {
        foreach (var text in new[] { "LIC-8F3AK-M7R", "Hunter2!", Issue().ToLowerInvariant() })
        {
            var result = Evaluator().Evaluate(text);

            Assert.Equal(LicenseState.Unreadable, result.State);
            Assert.Null(result.ClaimedKeyIdentifier);
            Assert.Null(result.ClaimedProduct);
        }
    }

    [Fact]
    public void OtherPrefixAccepted_IsValidWithThatIdentifier()
    {
        var key = Issue(prefix: KeyPrefix.Of("ACME"));

        var result = Evaluator().Evaluate(key);

        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal("ACME-8F3AK-M7RXB-7Q2D", result.License!.KeyIdentifier);
        Assert.Equal("ACME-8F3AK-M7RXB", result.License.Reference);
    }

    [Fact]
    public void NoPrefix_IsValid()
    {
        var result = Evaluator().Evaluate(Issue(prefix: KeyPrefix.None));

        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal("8F3AK-M7RXB-7Q2D", result.License!.KeyIdentifier);
        Assert.Equal("8F3AK-M7RXB", result.License.Reference);
    }

    [Fact]
    public void RelabelledKey_IsNotVerifiedClaimingTheNewPrefix()
    {
        var key = Issue(prefix: KeyPrefix.Of("ACME"));
        var relabelled = "BETA" + key["ACME".Length..];

        var result = Evaluator().Evaluate(relabelled);

        Assert.Equal(LicenseState.NotVerified, result.State);
        Assert.Equal("BETA-8F3AK-M7RXB-7Q2D", result.ClaimedKeyIdentifier);
        Assert.Equal(Product, result.ClaimedProduct);
    }

    [Fact]
    public void IdentifierTypo_IsUnreadableWithNoClaims()
    {
        var key = Issue();
        var typo = "LIC-8F3AK-M7RXB-7Q2O" + key[Identifier.Length..];

        var result = Evaluator().Evaluate(typo);

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Null(result.ClaimedKeyIdentifier);
        Assert.Null(result.ClaimedProduct);
    }

    [Fact]
    public void TooLong_IsUnreadableClaimingTheIdentifier()
    {
        var result = Evaluator().Evaluate(Identifier + "." + new string('A', 32_800));

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
        Assert.Null(result.ClaimedProduct);
    }

    // Strict reading of verified contents

    [Fact]
    public void FaultyVendorTool_RepeatedFeatureName_IsNotSupportedWithClaims()
    {
        var key = SignRaw(RawPayload(features: "\"ecommerce\":true,\"ecommerce\":true"));

        var result = Evaluator().Evaluate(key);

        Assert.Equal(LicenseState.NotSupported, result.State);
        Assert.Equal(Product, result.ClaimedProduct);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
        Assert.Null(result.License);
    }

    [Fact]
    public void UnknownField_IsNotSupported() =>
        Assert.Equal(LicenseState.NotSupported, Evaluator().Evaluate(SignRaw(RawPayload(extra: ",\"unknown\":\"x\""))).State);

    [Fact]
    public void OverALimit_FiftyOneFeatures_IsNotSupported()
    {
        var features = string.Join(",", Enumerable.Range(0, 51).Select(i => $"\"f{i}\":true"));

        Assert.Equal(LicenseState.NotSupported, Evaluator().Evaluate(SignRaw(RawPayload(features: features))).State);
    }

    [Fact]
    public void OverALimit_FeatureNameOf65Characters_IsNotSupported()
    {
        var features = $"\"{new string('a', 65)}\":true";

        Assert.Equal(LicenseState.NotSupported, Evaluator().Evaluate(SignRaw(RawPayload(features: features))).State);
    }

    [Theory]
    [InlineData("\"a\":false")]
    [InlineData("\"a\":null")]
    [InlineData("\"a\":[1]")]
    [InlineData("\"a\":{\"b\":1}")]
    [InlineData("\"a\":1e5")]
    public void UnsupportedFeatureValue_IsNotSupported(string features) =>
        Assert.Equal(LicenseState.NotSupported, Evaluator().Evaluate(SignRaw(RawPayload(features: features))).State);

    [Fact]
    public void UnknownFeatureNames_AreAccepted()
    {
        var result = Evaluator().Evaluate(SignRaw(RawPayload(extra: ",\"e\":" + new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), features: "\"never-heard-of-it\":true")));

        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal("never-heard-of-it", Assert.Single(result.License!.Features).Name);
    }

    // Expiry boundary

    private static string ExpiringKey() =>
        Issue(Request(expiry: LicenseExpiry.At(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero))));

    [Fact]
    public void LastSecond_IsValid()
    {
        var clock = new DateTimeOffset(2027, 3, 1, 12, 0, 0, 900, TimeSpan.Zero);

        Assert.Equal(LicenseState.Valid, Evaluator(clock).Evaluate(ExpiringKey()).State);
    }

    [Fact]
    public void ExactlyAtTheExpirySecond_IsValid() =>
        Assert.Equal(LicenseState.Valid, Evaluator(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero)).Evaluate(ExpiringKey()).State);

    [Fact]
    public void NextSecond_IsExpired() =>
        Assert.Equal(LicenseState.Expired, Evaluator(new DateTimeOffset(2027, 3, 1, 12, 0, 1, TimeSpan.Zero)).Evaluate(ExpiringKey()).State);

    [Fact]
    public void PerpetualKey_NeverExpires()
    {
        var key = Issue(Request(expiry: LicenseExpiry.Perpetual));

        var result = Evaluator(new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero)).Evaluate(key);

        Assert.Equal(LicenseState.Valid, result.State);
        Assert.True(result.License!.IsPerpetual);
    }

    [Fact]
    public void ExpiryEarlierThanIssue_EvaluatesAsExpired()
    {
        var key = SignRaw(RawPayload(extra: ",\"e\":" + (IssueTime.ToUnixTimeSeconds() - 1000)));

        Assert.Equal(LicenseState.Expired, Evaluator().Evaluate(key).State);
    }

    [Fact]
    public void Clock_IsReadOncePerEvaluation()
    {
        var clock = new CountingTimeProvider(Later);
        var evaluator = new LicenseEvaluator(Product, Trusted, clock);

        evaluator.Evaluate(Issue());

        Assert.Equal(1, clock.Reads);
    }

    // What a result reports

    [Fact]
    public void UnverifiedClaimsStayClaims_EditedKey_ReportsClaimsOnly()
    {
        var edited = WithPayload(Issue(), EditedPayload());

        var result = Evaluator().Evaluate(edited);

        Assert.Equal(LicenseState.NotVerified, result.State);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
        Assert.Equal(Product, result.ClaimedProduct);
        Assert.Null(result.License);
        Assert.False(result.IsLicensed);
    }

    [Fact]
    public void UnverifiedClaimsStayClaims_EveryFailedState_HasNoLicense()
    {
        var evaluator = Evaluator();
        var results = new[]
        {
            evaluator.Evaluate(null),
            evaluator.Evaluate("Hunter2!"),
            evaluator.Evaluate(Issue(Request("other.product"))),
            evaluator.Evaluate(Issue(key: Stranger)),
            evaluator.Evaluate(WithPayload(Issue(), EditedPayload())),
            evaluator.Evaluate(SignRaw(RawPayload(extra: ",\"z\":1"))),
        };

        Assert.All(results, r => Assert.Null(r.License));
        Assert.Null(results[0].ClaimedProduct);
        Assert.Null(results[0].ClaimedKeyIdentifier);
    }

    [Fact]
    public void ExpiredReportsWhatLapsed_FeaturesExpiryAndIdentifierAsFacts()
    {
        var key = Issue(Request(
            expiry: LicenseExpiry.On(new DateOnly(2026, 12, 31)),
            features: FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m)));

        var result = Evaluator(new DateTimeOffset(2027, 1, 5, 0, 0, 0, TimeSpan.Zero)).Evaluate(key);

        Assert.Equal(LicenseState.Expired, result.State);
        Assert.False(result.IsLicensed);
        var license = result.License!;
        Assert.Equal(Identifier, license.KeyIdentifier);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero), license.Expires);
        Assert.Equal(FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m), license.Features);
        Assert.Null(result.ClaimedProduct);
        Assert.Null(result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void Valid_ReportsEveryFact()
    {
        var license = Evaluator().Evaluate(Issue()).License!;

        Assert.Equal(Product, license.Product);
        Assert.Equal("LIC-8F3AK-M7RXB", license.Reference);
        Assert.Equal(Identifier, license.KeyIdentifier);
        Assert.Equal(ExampleKeys.SigningKeyId, license.SigningKeyId);
        Assert.Equal(IssueTime, license.Issued);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero), license.Expires);
        Assert.Equal("Commerce Standard", license.DisplayName);
        Assert.Equal("CUST-0042", license.VendorTag);
        Assert.Equal(2, license.Features.Count);
        Assert.Equal(new NumberFeature(500m), license.Features[1].Value);
    }

    [Fact]
    public void NoKeyTextInResults_ToStringHoldsNoPayloadOrSignature()
    {
        var evaluator = Evaluator();
        var valid = Issue();
        var edited = WithPayload(valid, PayloadJson(valid).Replace("500", "501", StringComparison.Ordinal));
        var keys = new[]
        {
            valid,
            edited,
            Issue(key: Stranger),
            Issue(Request("other.product")),
            SignRaw(RawPayload(extra: ",\"z\":1")),
            Issue(Request(expiry: LicenseExpiry.On(new DateOnly(2026, 10, 15)))),
        };

        foreach (var key in keys)
        {
            var result = evaluator.Evaluate(key);
            var parts = Segments(key);
            var texts = new List<string> { result.ToString() };
            if (result.License is { } license)
            {
                texts.Add(license.ToString());
            }

            foreach (var text in texts)
            {
                Assert.DoesNotContain(parts[1], text, StringComparison.Ordinal);
                Assert.DoesNotContain(parts[2], text, StringComparison.Ordinal);
                Assert.Contains(parts[0], text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void ToString_NamesStateAndIdentifierOnly()
    {
        var evaluator = Evaluator();

        Assert.Equal("Missing", evaluator.Evaluate(null).ToString());
        Assert.Equal("Unreadable", evaluator.Evaluate("Hunter2!").ToString());
        Assert.Equal($"Valid {Identifier}", evaluator.Evaluate(Issue()).ToString());
        Assert.Equal($"WrongProduct (claimed {Identifier})", evaluator.Evaluate(Issue(Request("other.product"))).ToString());
        Assert.Equal($"Expired {Identifier}", Evaluator(new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero)).Evaluate(Issue()).ToString());
        Assert.Equal($"NotSupported (claimed {Identifier})", evaluator.Evaluate(SignRaw(RawPayload(extra: ",\"unknown\":\"x\""))).ToString());
        Assert.Equal($"NotVerified (claimed {Identifier})", evaluator.Evaluate(WithPayload(Issue(), EditedPayload())).ToString());
        Assert.Equal($"VerifiedLicense {Identifier}", evaluator.Evaluate(Issue()).License!.ToString());
    }

    // Large license fits (evaluation half), license-generation

    [Fact]
    public void LargeLicenseFits_EvaluatesAsValid()
    {
        var product = "a" + new string('b', 31) + "." + new string('c', 31);
        var value = string.Concat(Enumerable.Repeat("aB3dE5gH7j", 26))[..256];
        var request = new LicenseRequest
        {
            Product = product,
            Expiry = LicenseExpiry.On(new DateOnly(2027, 3, 1)),
            DisplayName = new string('D', 64),
            VendorTag = new string('T', 64),
            Features = KeyLengthSupport.TextFeatures(50, value),
        };
        var key = Issue(request);

        var result = Evaluator(product: product).Evaluate(key);

        Assert.True(key.Length <= 32_767);
        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal(50, result.License!.Features.Count);
        Assert.Equal(new TextFeature(value), result.License.Features[49].Value);
    }

    // End to end

    [Fact]
    public void EndToEnd_EvaluateReissueEvaluate_SameReferenceNewKeyPart()
    {
        var evaluator = Evaluator();
        var first = evaluator.Evaluate(Issue());
        var issuer = new LicenseIssuer(timeProvider: new FixedTimeProvider(IssueTime.AddDays(1)), prefix: null);
        var request = ReissueRequest.From(first.License!) with { Features = first.License!.Features.With("max-orders", 1500m) };

        var reissued = issuer.Issue(request, Key);
        var second = evaluator.Evaluate(reissued.KeyString);

        Assert.Equal(LicenseState.Valid, second.State);
        Assert.Equal(first.License.Reference, second.License!.Reference);
        Assert.NotEqual(first.License.KeyIdentifier, second.License.KeyIdentifier);
        Assert.Equal(new NumberFeature(1500m), second.License.Features[1].Value);
        Assert.Equal(first.License.Expires, second.License.Expires);
        Assert.Equal(first.License.DisplayName, second.License.DisplayName);
    }

    // Behaviour around the contract

    [Fact]
    public void Evaluate_TwiceOnTheSameKey_GivesEqualFacts()
    {
        var key = Issue();

        Assert.Equal(Evaluator().Evaluate(key).License, Evaluator().Evaluate(key).License);
    }

    [Fact]
    public void ResultIsASnapshot_KeptValidResultStaysValid()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var evaluator = new LicenseEvaluator(Product, Trusted, clock);
        var key = ExpiringKey();
        var kept = evaluator.Evaluate(key);

        clock.SetUtcNow(new DateTimeOffset(2027, 3, 1, 12, 0, 1, TimeSpan.Zero));

        Assert.Equal(LicenseState.Valid, kept.State);
        Assert.Equal(LicenseState.Expired, evaluator.Evaluate(key).State);
    }

    [Fact]
    public void SafetyNet_ThrowingClock_ReturnsUnreadableInsteadOfThrowing()
    {
        var evaluator = new LicenseEvaluator(Product, Trusted, new ThrowingTimeProvider());

        var result = evaluator.Evaluate(Issue());

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Null(result.License);
    }

    [Fact]
    public async Task Evaluate_FromManyThreads_GivesTheSameAnswer()
    {
        var evaluator = Evaluator();
        var key = Issue();

        var states = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => evaluator.Evaluate(key).State)));

        Assert.All(states, s => Assert.Equal(LicenseState.Valid, s));
    }

    private sealed class CountingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public int Reads { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return utcNow;
        }
    }

    private sealed class ThrowingTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("clock failure");
    }
}
