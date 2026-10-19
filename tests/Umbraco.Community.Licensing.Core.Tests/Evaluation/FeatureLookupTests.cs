using Umbraco.Community.Licensing.Core.Tests.Support;
using static Umbraco.Community.Licensing.Core.Tests.Evaluation.EvaluationSupport;

namespace Umbraco.Community.Licensing.Core.Tests.Evaluation;

public class FeatureLookupTests
{
    private static LicenseResult Valid(FeatureList features) =>
        Evaluator().Evaluate(Issue(Request(features: features)));

    [Fact]
    public void Switch_DifferentCaseOfName_IsGranted()
    {
        var result = Valid(FeatureList.Empty.With("ecommerce", true));

        Assert.True(result.HasSwitch("Ecommerce"));
        Assert.True(result.HasSwitch("ecommerce"));
    }

    [Fact]
    public void NumberExact_DiscountRate_ReturnsExactlyAsIssued()
    {
        var result = Valid(FeatureList.Empty.With("discount-rate", 0.15m));

        Assert.Equal(0.15m, result.GetNumber("discount-rate"));
        Assert.Equal("0.15", result.GetNumber("discount-rate")!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TextUnchanged_Domains_ReturnsTheValueAsIssued()
    {
        var result = Valid(FeatureList.Empty.With("licensed-domains", "example.com,shop.example.com"));

        Assert.Equal("example.com,shop.example.com", result.GetText("licensed-domains"));
    }

    [Fact]
    public void WrongType_NumberStoredAsText_NumberLookupIsNotGranted()
    {
        var result = Valid(FeatureList.Empty.With("max-orders", "500"));

        Assert.Null(result.GetNumber("max-orders"));
        Assert.Equal("500", result.GetText("max-orders"));
    }

    [Fact]
    public void WrongType_EveryMismatch_IsNotGranted()
    {
        var result = Valid(FeatureList.Empty.With("sw", true).With("num", 5m).With("txt", "x"));

        Assert.Null(result.GetNumber("sw"));
        Assert.Null(result.GetText("sw"));
        Assert.False(result.HasSwitch("num"));
        Assert.Null(result.GetText("num"));
        Assert.False(result.HasSwitch("txt"));
        Assert.Null(result.GetNumber("txt"));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public void SwitchLookupOnText_TextNamedLikeASwitch_IsNotGranted(string text)
    {
        var result = Valid(FeatureList.Empty.With("pro", text));

        Assert.False(result.HasSwitch("pro"));
    }

    [Fact]
    public void ExpiredKeyGrantsNothing_AllLookups_AreNotGranted()
    {
        var key = Issue(Request(features: FeatureList.Empty.With("ecommerce", true).With("n", 1m).With("t", "x")));

        var result = Evaluator(new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero)).Evaluate(key);

        Assert.Equal(LicenseState.Expired, result.State);
        Assert.False(result.HasSwitch("ecommerce"));
        Assert.Null(result.GetNumber("n"));
        Assert.Null(result.GetText("t"));
    }

    [Fact]
    public void NotValidStates_AllLookups_AreNotGranted()
    {
        var results = new[]
        {
            Evaluator().Evaluate(null),
            Evaluator().Evaluate("Hunter2!"),
            Evaluator(product: "other.product").Evaluate(Issue()),
            Evaluator().Evaluate(Issue(key: Stranger)),
        };

        foreach (var result in results)
        {
            Assert.False(result.HasSwitch("ecommerce"));
            Assert.Null(result.GetNumber("max-orders"));
            Assert.Null(result.GetText("max-orders"));
        }
    }

    [Fact]
    public void CultureIndependent_TurkishCulture_UppercaseInvoicingIsGranted()
    {
        using var culture = new CultureScope("tr-TR");
        var result = Valid(FeatureList.Empty.With("invoicing", true).With("title", "t").With("limit", 3m));

        Assert.True(result.HasSwitch("INVOICING"));
        Assert.Equal("t", result.GetText("TITLE"));
        Assert.Equal(3m, result.GetNumber("LIMIT"));
    }

    [Fact]
    public void CultureIndependent_TurkishCulture_DottedAndDotlessIAreNotFolded()
    {
        using var culture = new CultureScope("tr-TR");
        var result = Valid(FeatureList.Empty.With("invoicing", true));

        Assert.False(result.HasSwitch("İNVOICING"));
        Assert.False(result.HasSwitch("ınvoicing"));
    }

    [Fact]
    public void LookAlikeName_CyrillicO_IsNotGranted()
    {
        var result = Valid(FeatureList.Empty.With("licensed-domains", "example.com"));

        Assert.Null(result.GetText("licensed-dоmains"));
    }

    [Fact]
    public void LookAlikeName_KelvinSignForK_IsNotGranted()
    {
        var result = Valid(FeatureList.Empty.With("kiosk", true));

        Assert.True(result.HasSwitch("KIOSK"));
        Assert.False(result.HasSwitch("Kiosk"));
    }

    [Fact]
    public void AbsentName_AllLookups_AreNotGranted()
    {
        var result = Valid(FeatureList.Empty.With("ecommerce", true));

        Assert.False(result.HasSwitch("missing"));
        Assert.Null(result.GetNumber("missing"));
        Assert.Null(result.GetText("missing"));
        Assert.False(result.HasSwitch(string.Empty));
    }

    [Fact]
    public void NullName_Throws()
    {
        var result = Valid(FeatureList.Empty.With("ecommerce", true));

        Assert.Throws<ArgumentNullException>(() => result.HasSwitch(null!));
        Assert.Throws<ArgumentNullException>(() => result.GetNumber(null!));
        Assert.Throws<ArgumentNullException>(() => result.GetText(null!));
        Assert.Throws<ArgumentNullException>(() => Evaluator().Evaluate(null).HasSwitch(null!));
    }

    [Fact]
    public void NoPresenceOrUntypedLookup_PublicSurface_HasOnlyTheThreeTypedLookups()
    {
        var lookups = typeof(LicenseResult).GetMethods()
            .Where(m => m.DeclaringType == typeof(LicenseResult) && m.GetParameters() is [{ ParameterType: var p }] && p == typeof(string))
            .Select(m => m.Name)
            .Order()
            .ToArray();

        Assert.Equal(["GetNumber", "GetText", "HasSwitch"], lookups);
    }
}
