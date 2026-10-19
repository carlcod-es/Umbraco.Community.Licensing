using System.Text;
using Umbraco.Community.Licensing.Core.Tests.Evaluation;
using Umbraco.Community.Licensing.Core.Tests.Signing;
using Umbraco.Community.Licensing.Core.Tests.Support;
using static Umbraco.Community.Licensing.Core.Tests.Evaluation.EvaluationSupport;
using static Umbraco.Community.Licensing.Core.Tests.Examples.ExampleKeyStrings;

namespace Umbraco.Community.Licensing.Core.Tests.Examples;

/// <summary>
/// Evaluates the committed key strings of docs/license-examples.md with the example public key at the documented time
/// and asserts the documented outcome (ADR-0005).
/// </summary>
public class ExampleTests
{
    private const string Acme = "acme.commerce";

    private const string Zenith = "zenith.commerce-shipping";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);

    private static LicenseResult Evaluate(string key, DateTimeOffset? at = null, string product = Acme) =>
        new LicenseEvaluator(product, Trusted, new FixedTimeProvider(at ?? Now)).Evaluate(key);

    private static FeatureList Orders(decimal maxOrders) =>
        FeatureList.Empty.With("ecommerce", true).With("max-orders", maxOrders);

    private static void AssertLicense(
        LicenseResult result, LicenseState state, string identifier, string product, DateTimeOffset issued,
        DateTimeOffset? expires, FeatureList? features = null, string? displayName = null, string? vendorTag = null)
    {
        Assert.Equal(state, result.State);
        var license = result.License!;
        Assert.NotNull(license);
        Assert.Equal(identifier, license.KeyIdentifier);
        Assert.Equal(identifier[..identifier.LastIndexOf('-')], license.Reference);
        Assert.Equal(product, license.Product);
        Assert.Equal(ExampleKeys.SigningKeyId, license.SigningKeyId);
        Assert.Equal(issued, license.Issued);
        Assert.Equal(expires, license.Expires);
        Assert.Equal(displayName, license.DisplayName);
        Assert.Equal(vendorTag, license.VendorTag);
        Assert.Equal(features ?? FeatureList.Empty, license.Features);
    }

    private static void AssertFailed(
        LicenseResult result, LicenseState state, string? identifier = null, string? product = null)
    {
        Assert.Equal(state, result.State);
        Assert.False(result.IsLicensed);
        Assert.Null(result.License);
        Assert.Equal(identifier, result.ClaimedKeyIdentifier);
        Assert.Equal(product, result.ClaimedProduct);
    }

    private static void AssertTypicalValid(LicenseResult result, string identifier = "LIC-8F3AK-M7RXB-7Q2D", DateTimeOffset? issued = null) =>
        AssertLicense(
            result, LicenseState.Valid, identifier, Acme, issued ?? Now, Utc(2027, 3, 1, 23, 59, 59), Orders(500m),
            "Commerce Standard", "CUST-0042");

    [Fact]
    public void Example1_Minimal_ValidPerpetualAndNothingGranted()
    {
        var result = Evaluate(Example1, product: "acme.seo-toolkit");

        AssertLicense(result, LicenseState.Valid, "LIC-4HN7T-QW2ZC-J3WM", "acme.seo-toolkit", Now, null);
        Assert.True(result.License!.IsPerpetual);
        Assert.True(result.IsLicensed);
        Assert.False(result.HasSwitch("ecommerce"));
        Assert.Null(result.GetNumber("max-orders"));
        Assert.Null(result.GetText("licensed-domains"));
    }

    [Fact]
    public void Example2_Typical_ValidThroughExpirySecond()
    {
        var result = Evaluate(Example2);

        AssertTypicalValid(result);
        Assert.True(result.HasSwitch("ecommerce"));
        Assert.Equal(500m, result.GetNumber("max-orders"));
        AssertTypicalValid(Evaluate(Example2, Utc(2027, 3, 1, 23, 59, 59)));
    }

    [Fact]
    public void Example2_Typical_ExpiredFromTheNextSecond()
    {
        var result = Evaluate(Example2, Utc(2027, 3, 2));

        AssertLicense(
            result, LicenseState.Expired, "LIC-8F3AK-M7RXB-7Q2D", Acme, Now, Utc(2027, 3, 1, 23, 59, 59), Orders(500m),
            "Commerce Standard", "CUST-0042");
        Assert.False(result.IsLicensed);
        Assert.False(result.HasSwitch("ecommerce"));
    }

    [Fact]
    public void Example3_ExpiryWithATime_ValidAtTheSecondExpiredAfter()
    {
        AssertLicense(
            Evaluate(Example3, Utc(2026, 10, 31, 12)), LicenseState.Valid, "LIC-6TWQ4-NJ8KD-3XFA", Acme, Now,
            Utc(2026, 10, 31, 12));
        AssertLicense(
            Evaluate(Example3, Utc(2026, 10, 31, 12, 0, 1)), LicenseState.Expired, "LIC-6TWQ4-NJ8KD-3XFA", Acme, Now,
            Utc(2026, 10, 31, 12));
    }

    [Fact]
    public void Example4_RenewalInstalledLate_ExpiredNamingKey1ThenValidWithKey2()
    {
        var expired = Evaluate(Example4Key1, Utc(2026, 10, 16, 8));

        AssertLicense(
            expired, LicenseState.Expired, "LIC-8F3AK-M7RXB-7Q2D", Acme, Utc(2025, 10, 15, 9), Utc(2026, 10, 15, 23, 59, 59),
            Orders(500m));
        Assert.Equal("LIC-8F3AK-M7RXB-7Q2D", expired.License!.KeyIdentifier);

        AssertLicense(
            Evaluate(Example4Key2, Utc(2026, 10, 20, 8)), LicenseState.Valid, "LIC-8F3AK-M7RXB-K9PX", Acme, Now,
            Utc(2027, 10, 15, 23, 59, 59), Orders(500m));
    }

    [Fact]
    public void Example5_AddOnByReissue_EachKeyReportsItsOwnFeatures()
    {
        var key1 = Evaluate(Example5Key1);
        var key2 = Evaluate(Example5Key2);
        var key3 = Evaluate(Example5Key3);
        var expires = Utc(2027, 3, 1, 23, 59, 59);

        AssertLicense(key1, LicenseState.Valid, "LIC-8F3AK-M7RXB-7Q2D", Acme, Utc(2026, 3, 1, 8), expires, Orders(500m));
        Assert.True(key1.HasSwitch("ecommerce"));
        Assert.Equal(500m, key1.GetNumber("max-orders"));
        Assert.False(key1.HasSwitch("ai-assist"));

        AssertLicense(key2, LicenseState.Valid, "LIC-8F3AK-M7RXB-K9PX", Acme, Utc(2026, 6, 15, 8), expires, Orders(1500m));
        Assert.Equal(1500m, key2.GetNumber("max-orders"));

        AssertLicense(
            key3, LicenseState.Valid, "LIC-8F3AK-M7RXB-H8RC", Acme, Utc(2026, 9, 1, 8), expires,
            Orders(1500m).With("ai-assist", true));
        Assert.True(key3.HasSwitch("ecommerce"));
        Assert.Equal(1500m, key3.GetNumber("max-orders"));
        Assert.True(key3.HasSwitch("ai-assist"));
    }

    [Fact]
    public void Example6_DomainsAsText_TheTextIsReportedAsIssued()
    {
        var result = Evaluate(Example6);

        AssertLicense(
            result, LicenseState.Valid, "LIC-9KCHP-W3TRM-5YBN", Acme, Now, Utc(2027, 3, 1, 23, 59, 59),
            FeatureList.Empty.With("licensed-domains", "example.com,shop.example.com"));
        Assert.Equal("example.com,shop.example.com", result.GetText("licensed-domains"));
    }

    [Fact]
    public void Example7_ExpiryTypo_BothKeysValidWithTheirOwnExpiryAndTheSameReference()
    {
        var at = Utc(2026, 10, 1, 9);
        var typo = Evaluate(Example7KeyA, at);
        var corrected = Evaluate(Example7KeyB, at);

        AssertLicense(typo, LicenseState.Valid, "LIC-M4Q7Z-B2NHS-T7VA", Acme, Now, Utc(2099, 3, 1, 23, 59, 59));
        AssertLicense(corrected, LicenseState.Valid, "LIC-M4Q7Z-B2NHS-D3KE", Acme, at, Utc(2027, 3, 1, 23, 59, 59));
        Assert.Equal(typo.License!.Reference, corrected.License!.Reference);
        Assert.Equal("LIC-M4Q7Z-B2NHS", corrected.License.Reference);
    }

    [Fact]
    public void Example11_Prod_ValidWithTheTypicalContents() =>
        AssertTypicalValid(Evaluate(Example2));

    [Fact]
    public void Example11_Staging_CharacterDroppedFromThePayload_UnreadableClaimingTheIdentifier() =>
        AssertFailed(Evaluate(Example11Staging), LicenseState.Unreadable, "LIC-8F3AK-M7RXB-K9PX");

    [Fact]
    public void Example11_Dev_KeyForAnotherProduct_WrongProductClaimingIdentifierAndProduct() =>
        AssertFailed(Evaluate(Example13), LicenseState.WrongProduct, "LIC-5E3FT-BPZ7Y-2MHS", Zenith);

    [Fact]
    public void Example11_Test_CutOffByEmail_UnreadableClaimingTheIdentifier() =>
        AssertFailed(Evaluate(Example11Test), LicenseState.Unreadable, "LIC-2V5C9-HKD3P-X3BN");

    [Fact]
    public void Example11_Uat_SignedByAnotherKey_SigningKeyNotRecognised() =>
        AssertFailed(Evaluate(Example11Uat), LicenseState.SigningKeyNotRecognised, "LIC-8F3AK-M7RXB-H8RC", Acme);

    [Fact]
    public void Example11_Demo_EditedPayload_NotVerifiedAndTheEditedClaimsAppearNowhere()
    {
        var result = Evaluate(Example11Demo);

        AssertFailed(result, LicenseState.NotVerified, "LIC-8F3AK-M7RXB-P6TY", Acme);
        Assert.Null(result.GetNumber("max-orders"));
        Assert.False(result.HasSwitch("ecommerce"));
        Assert.DoesNotContain("999999", result.ToString());
        Assert.DoesNotContain("2099", result.ToString());
    }

    [Fact]
    public void Example11_New_EmptySetting_Missing() =>
        AssertFailed(Evaluate(""), LicenseState.Missing);

    [Fact]
    public void Example11_Copy_KeyFollowedByForty000CharactersOfLog_UnreadableClaimingTheIdentifier()
    {
        var log = new StringBuilder();
        while (log.Length < 40_000)
        {
            log.Append("2026-10-01 08:00:00 info Licensing: starting the application host on port 5000\n");
        }

        var result = Evaluate(Example2 + "\n" + log.ToString(0, 40_000));

        AssertFailed(result, LicenseState.Unreadable, "LIC-8F3AK-M7RXB-7Q2D");
    }

    [Fact]
    public void Example11_Typo_IdentifierHandEdited_UnreadableWithNoClaims() =>
        AssertFailed(Evaluate(Example11Typo), LicenseState.Unreadable);

    [Fact]
    public void Example11_Old_UnknownFieldFromANewerTool_NotSupportedClaimingIdentifierAndProduct() =>
        AssertFailed(Evaluate(Example11Old), LicenseState.NotSupported, "LIC-8F3AK-M7RXB-W4TN", Acme);

    [Fact]
    public void Example11_OnlyProdIsLicensed()
    {
        string[] others =
        [
            Example11Staging, Example13, Example11Test, Example11Uat, Example11Demo, "", Example11Typo, Example11Old,
        ];

        Assert.True(Evaluate(Example2).IsLicensed);
        Assert.All(others, key => Assert.False(Evaluate(key).IsLicensed));
    }

    [Fact]
    public void Example11_WrappedAcrossLinesWithTrailingNewline_StillValid()
    {
        var wrapped = new StringBuilder();
        for (var i = 0; i < Example2.Length; i += 76)
        {
            wrapped.Append(Example2.AsSpan(i, Math.Min(76, Example2.Length - i))).Append('\n');
        }

        AssertTypicalValid(Evaluate(wrapped.ToString()));
    }

    [Fact]
    public void Example13_SeparateAddOnPackage_ValidForItsOwnProductOnly()
    {
        AssertLicense(
            Evaluate(Example13, product: Zenith), LicenseState.Valid, "LIC-5E3FT-BPZ7Y-2MHS", Zenith, Now,
            Utc(2027, 3, 1, 23, 59, 59));
        AssertFailed(Evaluate(Example13), LicenseState.WrongProduct, "LIC-5E3FT-BPZ7Y-2MHS", Zenith);
    }

    [Fact]
    public void Example16_RoutineRotation_Example1StaysValidWithBothKeysTrusted()
    {
        var both = TrustedSigningKeys.Create(
            SigningPublicKey.Parse(ExampleKeys.PublicKey), SigningPublicKey.Parse(ExampleKeys.SecondPublicKey));

        var result = new LicenseEvaluator("acme.seo-toolkit", both, new FixedTimeProvider(Now)).Evaluate(Example1);

        AssertLicense(result, LicenseState.Valid, "LIC-4HN7T-QW2ZC-J3WM", "acme.seo-toolkit", Now, null);
    }

    [Fact]
    public void Example17_AcmePrefix_ValidForAcmeAndWrongProductForZenith()
    {
        var result = Evaluate(Example17Acme);

        AssertTypicalValid(result, "ACME-8F3AK-M7RXB-7Q2D");
        Assert.Equal("ACME-8F3AK-M7RXB", result.License!.Reference);
        AssertFailed(Evaluate(Example17Acme, product: Zenith), LicenseState.WrongProduct, "ACME-8F3AK-M7RXB-7Q2D", Acme);
    }

    [Fact]
    public void Example17_NoPrefix_ValidWithoutBeingToldAPrefix()
    {
        var result = Evaluate(Example17None, product: Zenith);

        AssertLicense(result, LicenseState.Valid, "8F3AK-M7RXB-7Q2D", Zenith, Now, Utc(2027, 3, 1, 23, 59, 59));
        Assert.Equal("8F3AK-M7RXB", result.License!.Reference);
    }

    [Fact]
    public void Example17_AcmeKeyRelabelledBeta_NotVerified() =>
        AssertFailed(Evaluate(Example17Beta), LicenseState.NotVerified, "BETA-8F3AK-M7RXB-7Q2D", Acme);

    [Fact]
    public void Example17_ReissueAfterMovingToAcmePrefix_KeepsTheReferencePartAndIsValid()
    {
        var before = Evaluate(Example2);
        var request = ReissueRequest.From(before.License!);
        var issuer = new LicenseIssuer(
            KeyPrefix.Of("ACME"), new FixedTimeProvider(Now), new FixedIdentifierSource("8F3AKM7RXB", "K9PX"));

        var reissued = issuer.Issue(request, Key);
        var result = Evaluate(reissued.KeyString);

        Assert.StartsWith("ACME-8F3AK-M7RXB-", reissued.KeyIdentifier);
        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal(reissued.KeyIdentifier, result.License!.KeyIdentifier);
    }

    [Fact]
    public void SecondThrowawayKey_ParsesAndMatchesItsPrivateKey()
    {
        var publicKey = SigningPublicKey.Parse(ExampleKeys.SecondPublicKey);
        using var privateKey = SigningPrivateKey.FromPem(ExampleKeys.SecondPrivateKeyPem);

        Assert.Equal(ExampleKeys.SecondSigningKeyId, publicKey.SigningKeyId);
        Assert.Equal(publicKey, privateKey.PublicKey);
        Assert.NotEqual(ExampleKeys.SigningKeyId, publicKey.SigningKeyId);
    }
}
