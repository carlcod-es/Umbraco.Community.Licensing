using System.Text;
using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class PayloadReaderTests
{
    private const string Key = "\"k\":\"0F8NSYaNR_Q\"";
    private const string Product = "\"p\":\"acme.commerce\"";
    private const string Issued = "\"i\":1788249912";

    private static readonly DateTimeOffset IssuedAt = new(2026, 9, 1, 8, 5, 12, TimeSpan.Zero);

    private static string Obj(params string[] fields) => "{" + string.Join(',', fields) + "}";

    private static string Valid(params string[] extra) => Obj([Key, Product, Issued, .. extra]);

    private static string WithFeatures(string features) => Valid("\"f\":" + features);

    private static bool TryRead(string json, out LicensePayload? payload, out string? failure) =>
        PayloadReader.TryRead(Encoding.UTF8.GetBytes(json), out payload, out failure);

    private static LicensePayload ReadOk(string json)
    {
        Assert.True(TryRead(json, out var payload, out var failure), failure);
        Assert.Null(failure);
        return payload!;
    }

    private static void AssertRefused(string json)
    {
        Assert.False(TryRead(json, out var payload, out var failure));
        Assert.Null(payload);
        Assert.False(string.IsNullOrEmpty(failure));
    }

    // Accepted ----------------------------------------------------------------------------------------------

    [Fact]
    public void TryRead_Adr0007Sample_ReadsEveryField()
    {
        var payload = ReadOk(
            "{\"k\":\"Nb_sm4Fxh5c\",\"p\":\"acme.commerce\",\"i\":1788249912,"
            + "\"e\":1803945599,\"n\":\"Commerce Pro\",\"t\":\"CUST-0042\","
            + "\"f\":{\"ecommerce\":true,\"max-orders\":2500,\"licensed-domains\":\"example.com,shop.example.com\"}}");

        Assert.Equal("Nb_sm4Fxh5c", payload.SigningKeyId);
        Assert.Equal("acme.commerce", payload.Product);
        Assert.Equal(IssuedAt, payload.Issued);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero), payload.Expires);
        Assert.Equal("Commerce Pro", payload.DisplayName);
        Assert.Equal("CUST-0042", payload.VendorTag);
        Assert.Equal(
            [
                FeatureInput.Switch("ecommerce", true),
                FeatureInput.OfNumber("max-orders", 2500m),
                FeatureInput.OfText("licensed-domains", "example.com,shop.example.com"),
            ],
            payload.Features);
    }

    [Fact]
    public void TryRead_Minimal_HasPerpetualNoOptionalFieldsNoFeatures()
    {
        var payload = ReadOk(Valid());

        Assert.Null(payload.Expires);
        Assert.Null(payload.DisplayName);
        Assert.Null(payload.VendorTag);
        Assert.Empty(payload.Features);
        Assert.Equal(IssuedAt, payload.Issued);
    }

    [Fact]
    public void TryRead_EmptyFeaturesObject_ReadsAsNoFeatures() => Assert.Empty(ReadOk(WithFeatures("{}")).Features);

    [Fact]
    public void TryRead_FieldOrderIsFree()
    {
        var payload = ReadOk(Obj("\"f\":{\"a\":true}", Issued, Product, Key));

        Assert.Equal("acme.commerce", payload.Product);
        Assert.Single(payload.Features);
    }

    [Fact]
    public void TryRead_WhitespaceBetweenTokens_IsAccepted() =>
        ReadOk(" {\n \"k\" : \"0F8NSYaNR_Q\" ,\"p\":\"acme.commerce\",\"i\":1788249912}\n");

    [Fact]
    public void TryRead_UnknownFeatureName_IsAccepted()
    {
        var payload = ReadOk(WithFeatures("{\"some-future-flag\":true,\"zzz9\":\"x\"}"));

        Assert.Equal(2, payload.Features.Count);
    }

    [Fact]
    public void TryRead_EscapedAllowedCharacters_AreAccepted()
    {
        var payload = ReadOk(Valid("\"t\":\"\\u0041-\\u0030\\/\\u0023\"", "\"n\":\"\\u0043af\\u00e9 \\ud83d\\ude00\""));

        Assert.Equal("A-0/#", payload.VendorTag);
        Assert.Equal("Caf\u00e9 \U0001F600", payload.DisplayName);
    }

    [Fact]
    public void TryRead_EscapedFeatureName_IsDecoded()
    {
        var payload = ReadOk(WithFeatures("{\"\\u0065commerce\":true}"));

        Assert.Equal("ecommerce", payload.Features[0].Name);
    }

    [Fact]
    public void TryRead_NonNormalisedNumber_ReadsAsCanonicalValue()
    {
        var feature = ReadOk(WithFeatures("{\"max-orders\":2.50}")).Features[0];

        Assert.Equal(FeatureKind.Number, feature.Kind);
        Assert.Equal(2.5m, feature.Number);
        Assert.Equal("2.5", feature.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("0.0", "0")]
    [InlineData("7", "7")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("1.2000", "1.2")]
    [InlineData("999999999999999", "999999999999999")]
    [InlineData("99999999999.9999", "99999999999.9999")]
    [InlineData("123456789012345", "123456789012345")]
    public void TryRead_NumbersWithinGrammar_AreAccepted(string raw, string expected)
    {
        var number = ReadOk(WithFeatures("{\"n\":" + raw + "}")).Features[0].Number;

        Assert.Equal(expected, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryRead_FiftyFeaturesAnd64CharacterName_AreAccepted()
    {
        var many = string.Join(',', Enumerable.Range(0, 50).Select(i => $"\"f{i}\":true"));
        Assert.Equal(50, ReadOk(WithFeatures("{" + many + "}")).Features.Count);

        var name = "a" + new string('b', 63);
        Assert.Equal(name, ReadOk(WithFeatures("{\"" + name + "\":true}")).Features[0].Name);
    }

    [Fact]
    public void TryRead_TextAndDisplayNameAtLimitInScalarValues_AreAccepted()
    {
        var emoji64 = string.Concat(Enumerable.Repeat("\\ud83d\\ude00", 64));
        var emoji256 = string.Concat(Enumerable.Repeat("\\ud83d\\ude00", 256));

        ReadOk(Valid("\"n\":\"" + emoji64 + "\""));
        ReadOk(WithFeatures("{\"t\":\"" + emoji256 + "\"}"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void TryRead_UnderOtherCultures_ReadsDatesAndNumbersInvariantly(string culture)
    {
        using var scope = new CultureScope(culture);

        var payload = ReadOk(WithFeatures("{\"n\":1234.5,\"i\":1}").Replace(Issued, "\"i\":1798761599", StringComparison.Ordinal));

        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero), payload.Issued);
        Assert.Equal(TimeSpan.Zero, payload.Issued.Offset);
        Assert.Equal(1234.5m, payload.Features[0].Number);
        Assert.Equal(1m, payload.Features[1].Number);
    }

    // Round trip --------------------------------------------------------------------------------------------

    [Fact]
    public void RoundTrip_Adr0007Sample_ReadsBackEqual()
    {
        var written = new LicensePayload(
            "0F8NSYaNR_Q",
            "acme.commerce",
            IssuedAt,
            new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero),
            "Commerce Pro \u00e9\U0001F600",
            "CUST-0042",
            [
                FeatureInput.Switch("ecommerce", true),
                FeatureInput.OfNumber("max-orders", 2500m),
                FeatureInput.OfNumber("ratio", 0.0125m),
                FeatureInput.OfText("licensed-domains", "example.com,shop.example.com"),
            ]);

        Assert.True(PayloadReader.TryRead(PayloadWriter.Write(written), out var read));
        AssertEqual(written, read!);
    }

    [Fact]
    public void RoundTrip_Minimal_ReadsBackEqual()
    {
        var written = new LicensePayload("0F8NSYaNR_Q", "acme.commerce", IssuedAt, null, null, null, []);

        Assert.True(PayloadReader.TryRead(PayloadWriter.Write(written), out var read));
        AssertEqual(written, read!);
    }

    [Fact]
    public void RoundTrip_QuotesAndBackslashes_ReadsBackEqual()
    {
        var written = new LicensePayload(
            "0F8NSYaNR_Q",
            "acme.commerce",
            IssuedAt,
            null,
            "say \"hi\" \\o/",
            "A/#",
            [FeatureInput.OfText("note", "\\uD800 \" \\")]);

        Assert.True(PayloadReader.TryRead(PayloadWriter.Write(written), out var read));
        AssertEqual(written, read!);
    }

    private static void AssertEqual(LicensePayload expected, LicensePayload actual)
    {
        Assert.Equal(expected.SigningKeyId, actual.SigningKeyId);
        Assert.Equal(expected.Product, actual.Product);
        Assert.Equal(expected.Issued, actual.Issued);
        Assert.Equal(expected.Expires, actual.Expires);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.VendorTag, actual.VendorTag);
        Assert.Equal(expected.Features, actual.Features);
    }

    // Refused: spec scenarios -------------------------------------------------------------------------------

    [Fact]
    public void TryRead_FaultyVendorTool_RepeatedFeatureName_IsRefused()
    {
        Assert.False(TryRead(WithFeatures("{\"ecommerce\":true,\"max-orders\":5,\"ecommerce\":true}"), out _, out var failure));
        Assert.Contains("ecommerce", failure, StringComparison.Ordinal);
        Assert.Contains("more than once", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_UnknownField_IsRefused()
    {
        Assert.False(TryRead(Valid("\"licensee\":\"x\""), out _, out var failure));
        Assert.Contains("licensee", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_OverALimit_51Features_IsRefused()
    {
        var many = string.Join(',', Enumerable.Range(0, 51).Select(i => $"\"f{i}\":true"));

        Assert.False(TryRead(WithFeatures("{" + many + "}"), out _, out var failure));
        Assert.Contains("at most 50", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_OverALimit_FeatureName65Characters_IsRefused() =>
        AssertRefused(WithFeatures("{\"a" + new string('b', 64) + "\":true}"));

    // Refused: ADR-0002 strict-read cases -------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"x\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("true")]
    public void TryRead_NonObject_IsRefused(string json) => AssertRefused(json);

    [Theory]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912")]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.comm")]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912,}")]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912} x")]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912}{}")]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912/*c*/}")]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912,\"f\":{\"a\":01}}")]
    public void TryRead_MalformedOrTruncatedJson_IsRefused(string json) => AssertRefused(json);

    [Fact]
    public void TryRead_InvalidUtf8_IsRefused()
    {
        byte[] bytes = [.. "{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912,\"n\":\"a"u8, 0xC3, 0x28, .. "\"}"u8];

        Assert.False(PayloadReader.TryRead(bytes, out var payload, out var failure));
        Assert.Null(payload);
        Assert.Contains("UTF-8", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"k\":\"0F8NSYaNR_Q\"")]
    [InlineData("\"p\":\"acme.commerce\"")]
    [InlineData("\"i\":1788249912")]
    [InlineData("\"e\":1803945599")]
    [InlineData("\"n\":\"Commerce Pro\"")]
    [InlineData("\"t\":\"CUST-0042\"")]
    [InlineData("\"f\":{\"a\":true}")]
    [InlineData("\"f\":{}")]
    public void TryRead_RepeatedField_IsRefused(string field)
    {
        var required = new[] { Key, Product, Issued }.Contains(field);
        var json = required ? Valid(field) : Valid(field, field);

        Assert.False(TryRead(json, out _, out var failure));
        Assert.Contains("repeated", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_RepeatedFieldWithDifferentValue_IsRefused() =>
        AssertRefused(Valid("\"p\":\"other.product\""));

    [Fact]
    public void TryRead_RepeatedFieldViaEscapedName_IsRefused() =>
        AssertRefused(Valid("\"\\u0070roduct\":\"acme.commerce\""));

    [Fact]
    public void TryRead_RepeatedFeatureNameViaEscape_IsRefused() =>
        AssertRefused(WithFeatures("{\"ecommerce\":true,\"\\u0065commerce\":true}"));

    [Fact]
    public void TryRead_RepeatedFeatureNameWithDifferentValueType_IsRefused() =>
        AssertRefused(WithFeatures("{\"n\":1,\"n\":\"x\"}"));

    [Fact]
    public void TryRead_FeatureNamesDifferingOnlyInCase_AreRefusedByNameRule()
    {
        // Names are lowercase-only, so repeats are compared ordinally; the upper-case spelling fails the name rule.
        Assert.False(TryRead(WithFeatures("{\"ecommerce\":true,\"Ecommerce\":true}"), out _, out var failure));
        Assert.Contains("not valid", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("k")]
    [InlineData("p")]
    [InlineData("i")]
    public void TryRead_MissingRequiredField_IsRefused(string missing)
    {
        var fields = new[] { Key, Product, Issued }.Where(f => !f.StartsWith($"\"{missing}\"", StringComparison.Ordinal));

        AssertRefused(Obj([.. fields]));
    }

    [Fact]
    public void TryRead_EmptyObject_IsRefused() => AssertRefused("{}");

    [Theory]
    [InlineData("\"k\":7,\"p\":\"acme.commerce\",\"i\":1788249912")]
    [InlineData("\"k\":\"0F8NSYaNR_Q\",\"p\":null,\"i\":1788249912")]
    [InlineData("\"k\":\"0F8NSYaNR_Q\",\"p\":[\"a.b\"],\"i\":1788249912")]
    [InlineData("\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":\"1788249912\"")]
    [InlineData("\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":null")]
    public void TryRead_WrongTypeForRequiredField_IsRefused(string fields) => AssertRefused("{" + fields + "}");

    [Theory]
    [InlineData("0F8NSYaNR_")]
    [InlineData("0F8NSYaNR_Qx")]
    [InlineData("")]
    public void TryRead_SigningKeyIdNot11Characters_IsRefused(string keyId) =>
        AssertRefused(Obj($"\"k\":\"{keyId}\"", Product, Issued));

    // Dates are Unix-second integers (ADR-0007) -------------------------------------------------------------

    [Theory]
    [InlineData("\"2026-09-01T08:05:12Z\"")]
    [InlineData("\"1788249912\"")]
    [InlineData("\"\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void TryRead_DateNotANumber_IsRefused(string token)
    {
        AssertRefused(Obj(Key, Product, "\"i\":" + token));
        AssertRefused(Valid("\"e\":" + token));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1788249912.0")]
    [InlineData("1788249912.5")]
    [InlineData("-1")]
    [InlineData("-0")]
    [InlineData("1e3")]
    [InlineData("1E3")]
    [InlineData("1e+3")]
    [InlineData("1.788249912e9")]
    [InlineData("01")]
    [InlineData("00")]
    [InlineData("0123")]
    [InlineData("253402300800")]
    [InlineData("999999999999")]
    [InlineData("1000000000000")]
    [InlineData("99999999999999999999")]
    public void TryRead_DateOutsideTheGrammar_IsRefused(string raw)
    {
        foreach (var culture in new[] { "en-US", "tr-TR", "de-DE" })
        {
            using var scope = new CultureScope(culture);

            Assert.False(TryRead(Obj(Key, Product, "\"i\":" + raw), out var payload, out _));
            Assert.Null(payload);
            AssertRefused(Valid("\"e\":" + raw));
        }
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1", 1L)]
    [InlineData("1788249912", 1788249912L)]
    [InlineData("253402300799", 253402300799L)]
    public void TryRead_DateWithinTheGrammar_IsAccepted(string raw, long seconds)
    {
        var expected = DateTimeOffset.FromUnixTimeSeconds(seconds);

        Assert.Equal(expected, ReadOk(Obj(Key, Product, "\"i\":" + raw)).Issued);
        Assert.Equal(expected, ReadOk(Valid("\"e\":" + raw)).Expires);
    }

    [Fact]
    public void TryRead_LatestDate_IsEndOf9999()
    {
        var payload = ReadOk(Obj(Key, Product, "\"i\":253402300799"));

        Assert.Equal(new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero), payload.Issued);
        Assert.Equal(TimeSpan.Zero, payload.Issued.Offset);
    }

    [Theory]
    [InlineData("signingKeyId")]
    [InlineData("product")]
    [InlineData("issued")]
    [InlineData("expires")]
    [InlineData("displayName")]
    [InlineData("vendorTag")]
    [InlineData("features")]
    public void TryRead_Adr0002FieldName_IsRefusedAsUnknown(string name)
    {
        Assert.False(TryRead(Valid("\"" + name + "\":1"), out var payload, out var failure));
        Assert.Null(payload);
        Assert.Contains(name, failure, StringComparison.Ordinal);
        Assert.Contains("not known", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_Adr0002ShapedPayload_IsRefused() =>
        AssertRefused(
            "{\"signingKeyId\":\"0F8NSYaNR_Q\",\"product\":\"acme.commerce\",\"issued\":\"2026-09-01T08:05:12Z\","
            + "\"features\":{\"ecommerce\":true}}");

    [Fact]
    public void TryRead_EscapedFieldName_IsDecodedBeforeMatching()
    {
        var payload = ReadOk(Obj(Key, "\"\\u0070\":\"acme.commerce\"", Issued));

        Assert.Equal("acme.commerce", payload.Product);
    }

    [Fact]
    public void TryRead_ExpiresNull_IsRefused()
    {
        Assert.False(TryRead(Valid("\"e\":null"), out _, out var failure));
        Assert.Contains("field e", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"n\":null")]
    [InlineData("\"n\":\"\"")]
    [InlineData("\"t\":null")]
    [InlineData("\"t\":\"\"")]
    [InlineData("\"n\":5")]
    [InlineData("\"t\":true")]
    [InlineData("\"e\":\"5\"")]
    public void TryRead_NullEmptyOrWrongTypeOptionalField_IsRefused(string field) => AssertRefused(Valid(field));

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[{\"a\":true}]")]
    [InlineData("\"ecommerce\"")]
    [InlineData("true")]
    [InlineData("5")]
    public void TryRead_FeaturesNotAnObject_IsRefused(string value)
    {
        Assert.False(TryRead(WithFeatures(value), out _, out var failure));
        Assert.Contains("field f", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"x\":true}")]
    [InlineData("[]")]
    [InlineData("[1]")]
    public void TryRead_UnsupportedFeatureTokenType_IsRefused(string token)
    {
        Assert.False(TryRead(WithFeatures("{\"ecommerce\":" + token + "}"), out _, out var failure));
        Assert.Contains("unsupported value type", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-0")]
    [InlineData("1e2")]
    [InlineData("1E2")]
    [InlineData("1e-2")]
    [InlineData("1.5e0")]
    [InlineData("0.00001")]
    [InlineData("1.23456")]
    [InlineData("1000000000000000")]
    [InlineData("123456789012.2345")]
    [InlineData("1234567890123456")]
    [InlineData("99999999999999999999999999999")]
    [InlineData("0.1234567890123456789")]
    [InlineData("100000000000000000000000000000000000")]
    public void TryRead_NumberBreakingGrammar_IsRefused(string raw)
    {
        foreach (var culture in new[] { "en-US", "tr-TR", "de-DE" })
        {
            using var scope = new CultureScope(culture);

            Assert.False(TryRead(WithFeatures("{\"n\":" + raw + "}"), out _, out var failure));
            Assert.Contains("number rule", failure, StringComparison.Ordinal);
        }
    }

    // Content rules and limits (ADR-0002, PDR-0017) -----------------------------------------------------------

    [Theory]
    [InlineData("acme")]
    [InlineData("Acme.commerce")]
    [InlineData("acme.Commerce")]
    [InlineData("1acme.commerce")]
    [InlineData("acme.commerce.pro")]
    [InlineData(".commerce")]
    [InlineData("acme.")]
    [InlineData("acme_x.commerce")]
    [InlineData("")]
    public void TryRead_ProductNotMatchingRule_IsRefused(string product) =>
        AssertRefused(Obj(Key, $"\"p\":\"{product}\"", Issued));

    [Fact]
    public void TryRead_Product64And65Characters()
    {
        var at64 = "a" + new string('b', 31) + "." + "c" + new string('d', 30);
        Assert.Equal(64, at64.Length);
        ReadOk(Obj(Key, $"\"p\":\"{at64}\"", Issued));

        AssertRefused(Obj(Key, $"\"p\":\"{at64}x\"", Issued));
    }

    [Theory]
    [InlineData(" Pro")]
    [InlineData("Pro ")]
    [InlineData("Pro\\n")]
    [InlineData("A\\nB")]
    [InlineData("A\\tB")]
    [InlineData("A\\u0000B")]
    [InlineData("A\\u007fB")]
    [InlineData("A\\u2028B")]
    [InlineData("A\\u2029B")]
    [InlineData("\\u00a0Pro")]
    [InlineData("Pro\\u3000")]
    public void TryRead_DisplayNameBreakingRule_IsRefused(string value) => AssertRefused(Valid($"\"n\":\"{value}\""));

    [Fact]
    public void TryRead_DisplayName65Scalars_IsRefused()
    {
        ReadOk(Valid("\"n\":\"" + new string('x', 64) + "\""));
        AssertRefused(Valid("\"n\":\"" + new string('x', 65) + "\""));
        AssertRefused(Valid("\"n\":\"" + string.Concat(Enumerable.Repeat("\\ud83d\\ude00", 65)) + "\""));
    }

    [Theory]
    [InlineData("CUST 0042")]
    [InlineData("CUST\\n")]
    [InlineData("CUST-0042\\n")]
    [InlineData("\\nCUST")]
    [InlineData("CUST!")]
    [InlineData("CUST@x")]
    [InlineData("CUST:1")]
    [InlineData("caf\\u00e9")]
    [InlineData("\\uff21")]
    [InlineData("CUST\\u0000")]
    public void TryRead_VendorTagOutsideAllowedSet_IsRefused(string value) => AssertRefused(Valid($"\"t\":\"{value}\""));

    [Fact]
    public void TryRead_VendorTag64And65Characters()
    {
        ReadOk(Valid("\"t\":\"" + new string('A', 64) + "\""));
        AssertRefused(Valid("\"t\":\"" + new string('A', 65) + "\""));
    }

    [Theory]
    [InlineData("Ecommerce")]
    [InlineData("1abc")]
    [InlineData("-abc")]
    [InlineData("ab_c")]
    [InlineData("ab c")]
    [InlineData("ab.c")]
    [InlineData("")]
    [InlineData("caf\\u00e9")]
    public void TryRead_FeatureNameBreakingRule_IsRefused(string name) => AssertRefused(WithFeatures("{\"" + name + "\":true}"));

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\" x\"")]
    [InlineData("\"x \"")]
    [InlineData("\"a\\nb\"")]
    [InlineData("\"a\\u0001b\"")]
    [InlineData("\"a\\u2028b\"")]
    public void TryRead_FeatureTextBreakingRule_IsRefused(string token) => AssertRefused(WithFeatures("{\"t\":" + token + "}"));

    [Fact]
    public void TryRead_FeatureText257Scalars_IsRefused()
    {
        ReadOk(WithFeatures("{\"t\":\"" + new string('x', 256) + "\"}"));
        AssertRefused(WithFeatures("{\"t\":\"" + new string('x', 257) + "\"}"));
    }

    // Lone surrogates take their own path ------------------------------------------------------------------

    [Theory]
    [InlineData("\"n\":\"a\\uD800b\"")]
    [InlineData("\"n\":\"\\uDC00\"")]
    [InlineData("\"n\":\"\\uD800\"")]
    [InlineData("\"n\":\"\\uD83D\\u0041\"")]
    [InlineData("\"n\":\"\\uDE00\\uD83D\"")]
    [InlineData("\"t\":\"\\uD800\"")]
    [InlineData("\"f\":{\"t\":\"\\uD800\"}")]
    [InlineData("\"f\":{\"a\\uD800\":true}")]
    [InlineData("\"\\uD800\":1")]
    public void TryRead_EscapedLoneSurrogate_IsRefusedByItsOwnCheck(string field)
    {
        Assert.False(TryRead(Valid(field), out var payload, out var failure));
        Assert.Null(payload);
        Assert.Contains("surrogate", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_EscapedBackslashBeforeU_IsNotASurrogateEscape()
    {
        // The JSON text \\uD800 is a backslash followed by "uD800", not an escape.
        var payload = ReadOk(Valid("\"n\":\"a\\\\uD800\""));

        Assert.Equal("a\\uD800", payload.DisplayName);
    }

    [Fact]
    public void TryRead_ValidEscapedSurrogatePair_IsAccepted() =>
        Assert.Equal("\U0001F600", ReadOk(Valid("\"n\":\"\\uD83D\\uDE00\"")).DisplayName);

    // Depth -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void TryRead_DeeplyNestedValue_IsRefusedWithoutThrowing(int depth)
    {
        AssertRefused(WithFeatures("{\"a\":" + new string('[', depth) + new string(']', depth) + "}"));
        AssertRefused(Valid("\"x\":" + new string('[', depth)));
        AssertRefused(new string('[', depth));
    }

    [Fact]
    public void TryRead_Failure_LeavesResultNull()
    {
        Assert.False(PayloadReader.TryRead("[]"u8, out var payload));
        Assert.Null(payload);
    }
}
