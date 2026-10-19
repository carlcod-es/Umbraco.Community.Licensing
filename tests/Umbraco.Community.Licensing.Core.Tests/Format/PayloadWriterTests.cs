using System.Text;
using System.Text.Json;
using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class PayloadWriterTests
{
    private static readonly DateTimeOffset Issued = new(2026, 9, 1, 8, 5, 12, TimeSpan.Zero);

    private static LicensePayload Minimal(params FeatureInput[] features) =>
        new("0F8NSYaNR_Q", "acme.commerce", Issued, null, null, null, features);

    private static string Json(LicensePayload payload) => Encoding.UTF8.GetString(PayloadWriter.Write(payload));

    [Fact]
    public void Write_Adr0007Sample_ProducesExactJson()
    {
        var payload = new LicensePayload(
            "Nb_sm4Fxh5c",
            "acme.commerce",
            Issued,
            new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero),
            "Commerce Pro",
            "CUST-0042",
            [
                FeatureInput.Switch("ecommerce", true),
                FeatureInput.OfNumber("max-orders", 2500m),
                FeatureInput.OfText("licensed-domains", "example.com,shop.example.com"),
            ]);

        Assert.Equal(
            "{\"k\":\"Nb_sm4Fxh5c\",\"p\":\"acme.commerce\",\"i\":1788249912,"
            + "\"e\":1803945599,\"n\":\"Commerce Pro\",\"t\":\"CUST-0042\","
            + "\"f\":{\"ecommerce\":true,\"max-orders\":2500,\"licensed-domains\":\"example.com,shop.example.com\"}}",
            Json(payload));
    }

    [Fact]
    public void Write_Minimal_OmitsOptionalFieldsAndEmptyFeatures()
    {
        Assert.Equal(
            "{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912}",
            Json(Minimal()));
    }

    [Fact]
    public void Write_EachOptionalFieldAlone_IsWrittenOthersOmitted()
    {
        var expires = Minimal() with { Expires = new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero) };
        var name = Minimal() with { DisplayName = "Pro" };
        var tag = Minimal() with { VendorTag = "T-1" };
        var features = Minimal(FeatureInput.Switch("a", true));
        const string head = "{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912";

        Assert.Equal(head + ",\"e\":1798859045}", Json(expires));
        Assert.Equal(head + ",\"n\":\"Pro\"}", Json(name));
        Assert.Equal(head + ",\"t\":\"T-1\"}", Json(tag));
        Assert.Equal(head + ",\"f\":{\"a\":true}}", Json(features));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void Write_Numbers_AreNormalisedAndCultureInvariant(string culture)
    {
        using var scope = new CultureScope(culture);

        var json = Json(Minimal(
            FeatureInput.OfNumber("a", 2.50m),
            FeatureInput.OfNumber("b", 2500m),
            FeatureInput.OfNumber("c", 0.1234m),
            FeatureInput.OfNumber("d", 0m)));

        Assert.EndsWith("\"f\":{\"a\":2.5,\"b\":2500,\"c\":0.1234,\"d\":0}}", json);
    }

    [Theory]
    [InlineData("\u00e9", new byte[] { 0xC3, 0xA9 })]
    [InlineData("\u6771", new byte[] { 0xE6, 0x9D, 0xB1 })]
    [InlineData("\U0001F600", new byte[] { 0xF0, 0x9F, 0x98, 0x80 })]
    public void Write_NonAsciiText_IsRawUtf8(string text, byte[] utf8)
    {
        var bytes = PayloadWriter.Write(Minimal(FeatureInput.OfText("note", text)) with { DisplayName = text });

        Assert.StartsWith("{\"k\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\",\"i\":1788249912,\"n\":\"", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.True(bytes.AsSpan().IndexOf(utf8) >= 0);
        Assert.DoesNotContain("\\u", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(2, Occurrences(bytes, utf8));
        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal(text, doc.RootElement.GetProperty("n").GetString());
        Assert.Equal(text, doc.RootElement.GetProperty("f").GetProperty("note").GetString());
    }

    [Fact]
    public void Write_MixedNonAscii_IsRawAndRoundTrips()
    {
        const string name = "Caf\u00e9 \u4e2d\u6587 \U0001F600";
        const string text = "\u00fcber, \u00e5ngstr\u00f6m";
        var payload = Minimal(FeatureInput.OfText("note", text)) with { DisplayName = name };

        var bytes = PayloadWriter.Write(payload);

        Assert.Contains(name, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Contains(text, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal(name, doc.RootElement.GetProperty("n").GetString());
        Assert.Equal(text, doc.RootElement.GetProperty("f").GetProperty("note").GetString());
    }

    [Fact]
    public void Write_QuoteAndBackslash_AreEscaped()
    {
        var json = Json(Minimal(FeatureInput.OfText("note", "a\"b\\c")) with { DisplayName = "say \"hi\" \\o/" });

        Assert.Contains("\"n\":\"say \\\"hi\\\" \\\\o/\"", json, StringComparison.Ordinal);
        Assert.Contains("\"note\":\"a\\\"b\\\\c\"", json, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("say \"hi\" \\o/", doc.RootElement.GetProperty("n").GetString());
        Assert.Equal("a\"b\\c", doc.RootElement.GetProperty("f").GetProperty("note").GetString());
    }

    [Fact]
    public void Write_HtmlSensitiveCharacters_AreNotEscaped()
    {
        var json = Json(Minimal(FeatureInput.OfText("note", "a<b>c&d'e+f")) with { DisplayName = "<>&'+" });

        Assert.Contains("\"n\":\"<>&'+\"", json, StringComparison.Ordinal);
        Assert.Contains("\"note\":\"a<b>c&d'e+f\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ControlCharacters_AreEscapedAsUpperCaseUnicodeEscapes()
    {
        // The content rules refuse control characters, so the issuer never passes one; the writer still escapes them.
        var json = Json(Minimal(FeatureInput.OfText("note", "a\u0000b\nc\u001Fd")));

        Assert.Contains("\"note\":\"a\\u0000b\\u000Ac\\u001Fd\"", json, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("a\u0000b\nc\u001Fd", doc.RootElement.GetProperty("f").GetProperty("note").GetString());
    }

    [Fact]
    public void Write_DelAndLineSeparators_AreRaw()
    {
        // Only U+0000 to U+001F are escaped besides quote and backslash.
        var bytes = PayloadWriter.Write(Minimal() with { DisplayName = "a\u007Fb\u2028c" });

        Assert.Contains("a\u007Fb\u2028c", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_UnpairedSurrogate_Throws() =>
        Assert.Throws<InvalidOperationException>(() => PayloadWriter.Write(Minimal() with { DisplayName = "a\uD800b" }));

    [Fact]
    public void Write_AllEscapesInOneValue_StaysValidJson()
    {
        var text = string.Concat(Enumerable.Range(0, 0x20).Select(i => (char)i)) + "\"\\" + "\u00e9\u6771\U0001F600<>&'+";
        var bytes = PayloadWriter.Write(Minimal(FeatureInput.OfText("note", text)));

        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal(text, doc.RootElement.GetProperty("f").GetProperty("note").GetString());
    }

    private static int Occurrences(byte[] haystack, byte[] needle)
    {
        var count = 0;
        var span = haystack.AsSpan();
        int at;
        while ((at = span.IndexOf(needle)) >= 0)
        {
            count++;
            span = span[(at + needle.Length)..];
        }

        return count;
    }

    [Fact]
    public void Write_NonUtcDate_IsWrittenAsUtcInstant()
    {
        var payload = Minimal() with
        {
            Issued = new DateTimeOffset(2026, 9, 1, 10, 5, 12, TimeSpan.FromHours(2)),
            Expires = new DateTimeOffset(2027, 1, 1, 1, 0, 0, TimeSpan.FromHours(5.5)),
        };

        var json = Json(payload);

        Assert.Contains("\"i\":1788249912,", json);
        Assert.Contains("\"e\":1798745400", json);
    }

    [Fact]
    public void Write_FeaturesKeepListOrder()
    {
        var json = Json(Minimal(FeatureInput.Switch("zeta", true), FeatureInput.Switch("alpha", true)));

        Assert.EndsWith("\"f\":{\"zeta\":true,\"alpha\":true}}", json);
    }

    [Fact]
    public void Write_DateWithFraction_Throws()
    {
        var payload = Minimal() with { Issued = Issued.AddTicks(1) };

        Assert.Throws<InvalidOperationException>(() => PayloadWriter.Write(payload));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.23456)]
    public void Write_NumberFailingTheRule_Throws(double value)
    {
        var payload = Minimal(FeatureInput.OfNumber("a", (decimal)value));

        Assert.Throws<InvalidOperationException>(() => PayloadWriter.Write(payload));
    }

    [Fact]
    public void Write_OffSwitch_Throws() =>
        Assert.Throws<InvalidOperationException>(() => PayloadWriter.Write(Minimal(FeatureInput.Switch("a", false))));

    [Fact]
    public void Write_MissingText_Throws() =>
        Assert.Throws<InvalidOperationException>(() => PayloadWriter.Write(Minimal(FeatureInput.OfText("a", null))));

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(253402300799L, "253402300799")]
    public void Write_DateAtTheEdges_IsWrittenAsPlainInteger(long seconds, string expected)
    {
        var payload = Minimal() with { Issued = DateTimeOffset.FromUnixTimeSeconds(seconds) };

        Assert.Contains("\"i\":" + expected + "}", Json(payload), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_DateBefore1970_Throws() =>
        Assert.Throws<InvalidOperationException>(() => PayloadWriter.Write(Minimal() with { Issued = DateTimeOffset.FromUnixTimeSeconds(-1) }));
}
