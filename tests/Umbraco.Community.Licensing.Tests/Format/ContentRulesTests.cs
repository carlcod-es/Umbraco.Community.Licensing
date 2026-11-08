using Umbraco.Community.Licensing.Tests.Support;

namespace Umbraco.Community.Licensing.Tests.Format;

public class ContentRulesTests
{
    public static TheoryData<string> Cultures => ["en-US", "tr-TR", "de-DE"];

    private static List<LicenseProblem> Run(Action<List<LicenseProblem>> check)
    {
        var problems = new List<LicenseProblem>();
        check(problems);
        return problems;
    }

    private static string[] Fields(List<LicenseProblem> problems) => problems.Select(p => p.Field).ToArray();

    private static readonly string LineSep = ((char)0x2028).ToString();
    private static readonly string ParaSep = ((char)0x2029).ToString();
    private static readonly string Nel = ((char)0x85).ToString();
    private static readonly string Nul = ((char)0).ToString();
    private static readonly string High = ((char)0xD800).ToString();
    private static readonly string Low = ((char)0xDC00).ToString();
    private static readonly string Nbsp = ((char)0xA0).ToString();
    private static readonly string Kelvin = ((char)0x212A).ToString();

    // A 4-byte letter (CJK Extension B), so length tests count surrogate pairs without using refused emoji.
    private static string Astral(int count) => string.Concat(Enumerable.Repeat("\U00020000", count));

    private static string Product(int length) => "a." + new string('b', length - 2);

    // Product ID

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ValidProductId_IsAccepted(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var product in new[] { "acme.commerce", "zenith.commerce-shipping", "i.i", "a1.b-2" })
        {
            Assert.Empty(Run(p => ContentRules.CheckProduct(product, p)));
        }
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void InvalidProductId_NamesProduct(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var product in new[] { "commerce", "Acme.Commerce", "acme.commerce.pro", "acme.9shop", "acme.", ".shop", "", "acme.shöp", "İ.shop", null })
        {
            var problems = Run(p => ContentRules.CheckProduct(product, p));
            Assert.Equal(["product"], Fields(problems));
        }
    }

    [Fact]
    public void ProductIdTooLong_RejectedAt65AcceptedAt64()
    {
        Assert.Empty(Run(p => ContentRules.CheckProduct(Product(64), p)));
        Assert.Equal(["product"], Fields(Run(p => ContentRules.CheckProduct(Product(65), p))));
    }

    // Display name

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ValidDisplayName_IsAccepted(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var name in new[] { "Commerce Pro", "Commerce Pro – Édition", "x", new string('x', 64), "\U00020000 pack", Astral(64) })
        {
            Assert.Empty(Run(p => ContentRules.CheckDisplayName(name, p)));
        }
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void InvalidDisplayName_NamesDisplayName(string culture)
    {
        using var scope = new CultureScope(culture);

        var invalid = new[]
        {
            "", " Pro", "Pro ", "a\nb", "a\r\nb", "a\tb", "a" + Nul + "b", "a" + LineSep + "b", "a" + ParaSep + "b", "a" + Nel + "b",
            new string('x', 65), Astral(65), Nbsp + "Pro", "ab" + High, Low + "ab", "a" + High + "b",
        };
        foreach (var name in invalid)
        {
            Assert.Equal(["displayName"], Fields(Run(p => ContentRules.CheckDisplayName(name, p))).Distinct().ToArray());
        }
    }

    [Fact]
    public void DisplayNameLength_CountsScalarValuesNotCodeUnits()
    {
        // 64 four-byte letters are 128 UTF-16 code units but 64 characters.
        Assert.Empty(Run(p => ContentRules.CheckDisplayName(Astral(64), p)));
        Assert.Single(Run(p => ContentRules.CheckDisplayName("ab" + High, p)));
    }

    // Text rule (PDR-0042, ADR-0018)

    private static List<LicenseProblem> DisplayName(string name) => Run(p => ContentRules.CheckDisplayName(name, p));

    private static List<LicenseProblem> TextValue(string name, string value) =>
        Run(p => ContentRules.CheckFeatures([FeatureInput.OfText(name, value)], p));

    private static string Cp(int value) => char.ConvertFromUtf32(value);

    [Theory]
    [MemberData(nameof(Cultures))]
    public void TextRule_AnyScriptAccepted(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var name in new[] { "Ακρόπολη Pro", "コマース Pro", "Łódź Edition", "Commerce Pro – Édition" })
        {
            Assert.Empty(DisplayName(name));
            Assert.Empty(TextValue("t", name));
        }
    }

    [Theory]
    [InlineData("eu+uk")]
    [InlineData("a=b")]
    [InlineData("<none>")]
    [InlineData("$100 | ~50%")]
    public void TextRule_PrintableAsciiUnchanged(string value)
    {
        Assert.Empty(TextValue("t", value));
        Assert.Empty(DisplayName(value));
    }

    [Fact]
    public void TextRule_EveryPrintableAsciiCharacterAccepted()
    {
        for (var c = 0x20; c <= 0x7E; c++)
        {
            var value = "a" + (char)c + "b";
            Assert.Empty(TextValue("t", value));
            Assert.Empty(DisplayName(value));
        }
    }

    [Theory]
    [InlineData("Pro ⭐")]
    [InlineData("Pro™")]
    [InlineData("Pro ©")]
    [InlineData("Pro €")]
    [InlineData("Pro ±")]
    [InlineData("Pro \U0001F600")]
    public void TextRule_EmojiAndSymbolsRefused(string name)
    {
        Assert.Equal(["displayName"], Fields(DisplayName(name)));
    }

    [Theory]
    [InlineData("1\uFE0F\u20E3")]
    [InlineData("Pro\uFE0F")]
    [InlineData("\U0001F44D\U0001F3FB")]
    [InlineData("Pro\u20E3")]
    public void TextRule_EmojiBuildingBlocksRefused(string name)
    {
        Assert.Equal(["displayName"], Fields(DisplayName(name)));
    }

    [Theory]
    [InlineData(0x200B)]
    [InlineData(0x200C)]
    [InlineData(0x200D)]
    [InlineData(0x202E)]
    [InlineData(0x00AD)]
    public void TextRule_InvisibleCharactersRefused(int codePoint)
    {
        var problems = TextValue("licensed-domains", "a" + Cp(codePoint) + "b");

        Assert.Equal(["features.licensed-domains"], Fields(problems));
    }

    [Theory]
    [InlineData(0x3164)]
    [InlineData(0x034F)]
    public void TextRule_InvisibleLettersAndMarksRefused(int codePoint)
    {
        Assert.Equal(["displayName"], Fields(DisplayName("Pro" + Cp(codePoint) + "x")));
    }

    [Theory]
    [InlineData(0x00A0)]
    [InlineData(0x3000)]
    [InlineData(0x2002)]
    [InlineData(0x2003)]
    public void TextRule_OtherSpacesRefused(int codePoint)
    {
        var problems = DisplayName("Commerce" + Cp(codePoint) + "Pro");

        Assert.Equal(["displayName"], Fields(problems));
        Assert.Contains("not allowed", Assert.Single(problems).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextRule_OtherSpaceAtEnd_ReportsBothProblems()
    {
        var problems = DisplayName("Pro" + Nbsp);

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Message.Contains("not allowed", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Message.Contains("start or end with whitespace", StringComparison.Ordinal));
    }

    [Fact]
    public void TextRule_RepeatedAsciiSpacesAccepted()
    {
        Assert.Empty(DisplayName("Commerce  Pro"));
        Assert.Empty(TextValue("t", "Commerce  Pro"));
    }

    [Theory]
    [InlineData("①")]
    [InlineData("m²")]
    [InlineData("½")]
    [InlineData("Ⅻ")]
    public void TextRule_NumbersThatAreNotDigitsRefused(string value)
    {
        Assert.Equal(["features.t"], Fields(TextValue("t", value)));
    }

    [Theory]
    [InlineData(0xE000)]
    [InlineData(0x0378)]
    [InlineData(0x10940)]
    [InlineData(0xF0000)]
    public void TextRule_UnassignedAndPrivateUseCharactersRefused(int codePoint)
    {
        Assert.Equal(["displayName"], Fields(DisplayName("Pro" + Cp(codePoint))));
    }

    [Fact]
    public void TextRule_BothFormsOfAnAccentedLetterAccepted()
    {
        Assert.Empty(DisplayName("Caf\u00e9"));
        Assert.Empty(DisplayName("Cafe\u0301"));
        Assert.Empty(TextValue("t", "Caf\u00e9"));
        Assert.Empty(TextValue("t", "Cafe\u0301"));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void TextRule_SameAnswerOnEveryMachine(string culture)
    {
        // The answer comes from fixed ranges and a generated table, so it does not move with the culture.
        using var scope = new CultureScope(culture);

        Assert.Empty(DisplayName("コマース Pro"));
        var problem = Assert.Single(DisplayName("Pro ⭐"));
        Assert.Equal(
            "A display name contains a character that is not allowed: U+2B50 at position 5. Allowed: printable ASCII, and letters, combining marks, digits and punctuation from any script.",
            problem.Message);
    }

    [Fact]
    public void TextRule_FourByteLetterCountsAsOneCharacter()
    {
        Assert.Empty(DisplayName(Astral(1)));
        Assert.Empty(DisplayName(Astral(64)));
        Assert.Equal(["displayName"], Fields(DisplayName(Astral(65))));
    }

    [Fact]
    public void TextRule_ControlCharacter_ReportsOnlyTheControlMessage()
    {
        var problem = Assert.Single(DisplayName("a" + Nel + "b"));

        Assert.Contains("control characters", problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not allowed", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x1F)]
    [InlineData(0x7F)]
    [InlineData(0x9F)]
    [InlineData(0x2028)]
    [InlineData(0x2029)]
    public void TextRule_ControlRanges_AreControl(int codePoint)
    {
        var problem = Assert.Single(DisplayName("a" + Cp(codePoint) + "b"));

        Assert.Contains("control characters", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextRule_Message_NamesCodePointAndPosition_NotTheCharacter()
    {
        var problem = Assert.Single(TextValue("t", "ab" + Cp(0x1F600) + "d"));

        Assert.Equal("features.t", problem.Field);
        Assert.Equal(
            "A text value contains a character that is not allowed: U+1F600 at position 3. Allowed: printable ASCII, and letters, combining marks, digits and punctuation from any script.",
            problem.Message);
        Assert.DoesNotContain(Cp(0x1F600), problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextRule_Message_PositionCountsScalarValues()
    {
        var problem = Assert.Single(DisplayName(Astral(2) + "\u2B50"));

        Assert.Contains("U+2B50 at position 3.", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextRule_Message_CountsOtherRefusedCharacters()
    {
        var one = Assert.Single(DisplayName("a\u2B50b\u2122"));
        var two = Assert.Single(DisplayName("a\u2B50b\u2122c\u00A9"));
        var repeated = Assert.Single(DisplayName("\u2B50\u2B50"));

        Assert.Contains("U+2B50 at position 2 (and 1 other).", one.Message, StringComparison.Ordinal);
        Assert.Contains("U+2B50 at position 2 (and 2 others).", two.Message, StringComparison.Ordinal);
        Assert.Contains("U+2B50 at position 1 (and 1 other).", repeated.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2122", one.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextRule_OneProblemPerField()
    {
        var problems = ContentRules.Check(
            "acme.commerce",
            "\u2B50",
            null,
            [FeatureInput.OfText("a", "\u2B50\u2122"), FeatureInput.OfText("b", "\u200B")]);

        Assert.Equal(["displayName", "features.a", "features.b"], Fields(problems));
    }

    [Fact]
    public void FeatureRules_TextValueBreakingTheTextRule_NamesFeature()
    {
        Assert.Equal(["features.licensed-domains"], Fields(TextValue("licensed-domains", "shop.example.com ✓")));
    }

    [Fact]
    public void DisplayNameRule_InvalidDisplayName_EmojiNamesDisplayName()
    {
        Assert.Equal(["displayName"], Fields(DisplayName("Pro ⭐")));
    }

    [Fact]
    public void FeatureRules_ValidFeatures_MixedCaseDomainsAccepted()
    {
        Assert.Empty(TextValue("licensed-domains", "a.com,A.com"));
    }

    // Vendor tag

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ValidVendorTag_IsAccepted(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var tag in new[] { "#1001", "SHOP-2026-000123", "pi_3NkX9a2eZvKYlo2C1", "INV/2026/04", "a.b", "I", new string('i', 64) })
        {
            Assert.Empty(Run(p => ContentRules.CheckVendorTag(tag, p)));
        }
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void InvalidVendorTag_NamesVendorTag(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var tag in new[] { "INV 2026 04", "jane@acme.com", "", new string('x', 65), "tag\n", "é", "ı", "a" + Kelvin + "b" })
        {
            Assert.Equal(["vendorTag"], Fields(Run(p => ContentRules.CheckVendorTag(tag, p))));
        }
    }

    // Features

    private static FeatureInput[] Many(int count) =>
        Enumerable.Range(0, count).Select(i => FeatureInput.Switch($"f{(char)('a' + i / 26)}{(char)('a' + i % 26)}", true)).ToArray();

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ValidFeatures_AreAccepted(string culture)
    {
        using var scope = new CultureScope(culture);

        FeatureInput[] features =
        [
            FeatureInput.Switch("ecommerce", true),
            FeatureInput.OfNumber("max-orders", 2500m),
            FeatureInput.OfNumber("discount-rate", 0.15m),
            FeatureInput.OfText("licensed-domains", "a.com,A.com"),
        ];

        Assert.Empty(Run(p => ContentRules.CheckFeatures(features, p)));
        Assert.Empty(Run(p => ContentRules.CheckFeatures([], p)));
    }

    [Fact]
    public void ExplicitFalse_NamesFeature()
    {
        var problems = Run(p => ContentRules.CheckFeatures([FeatureInput.Switch("pro", false)], p));

        Assert.Equal(["features.pro"], Fields(problems));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void InvalidNumbers_NameFeature(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var value in new[] { -200m, 2.12345m, 1234567890123456m, 123456789012.3456m, -0.0001m })
        {
            var problems = Run(p => ContentRules.CheckFeatures([FeatureInput.OfNumber("max-orders", value)], p));
            Assert.Equal(["features.max-orders"], Fields(problems));
        }
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void InvalidNames_NameFeatures(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var name in new[] { "Max Orders", "9lives", "", null, "İstanbul", new string('a', 65) })
        {
            var problems = Run(p => ContentRules.CheckFeatures([FeatureInput.Switch(name, true)], p));
            Assert.Equal(["features"], Fields(problems));
        }

        Assert.Empty(Run(p => ContentRules.CheckFeatures([FeatureInput.Switch(new string('a', 64), true)], p)));
    }

    [Fact]
    public void InvalidName_MessageNamesTheFeature()
    {
        var problem = Assert.Single(Run(p => ContentRules.CheckFeatures([FeatureInput.Switch("Max Orders", true)], p)));

        Assert.Equal("features", problem.Field);
        Assert.Contains("\"Max Orders\"", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void InvalidName_NullOrEmpty_MessageSaysNone(string? name)
    {
        var problem = Assert.Single(Run(p => ContentRules.CheckFeatures([FeatureInput.Switch(name, true)], p)));

        Assert.Contains("(none)", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidName_LongName_IsTruncatedWithoutSplittingASurrogatePair()
    {
        var name = new string('A', 31) + "\U0001F600" + new string('A', 100);

        var problem = Assert.Single(Run(p => ContentRules.CheckFeatures([FeatureInput.Switch(name, true)], p)));

        Assert.DoesNotContain(new string('A', 100), problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\U0001F600", problem.Message, StringComparison.Ordinal);
        Assert.Contains(new string('A', 31) + "...", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedName_NamesFeaturesOncePerName()
    {
        FeatureInput[] features =
        [
            FeatureInput.OfNumber("max-orders", 1m),
            FeatureInput.OfNumber("max-orders", 2m),
            FeatureInput.OfNumber("max-orders", 3m),
        ];

        Assert.Equal(["features"], Fields(Run(p => ContentRules.CheckFeatures(features, p))));
    }

    [Fact]
    public void TooManyFeatures_RejectedAt51AcceptedAt50()
    {
        Assert.Empty(Run(p => ContentRules.CheckFeatures(Many(50), p)));
        Assert.Equal(["features"], Fields(Run(p => ContentRules.CheckFeatures(Many(51), p))));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void InvalidText_NamesFeature(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var text in new[] { "", " example.com", "example.com ", "a\nb", "a" + LineSep + "b", new string('x', 257), "ab" + High, null })
        {
            var problems = Run(p => ContentRules.CheckFeatures([FeatureInput.OfText("licensed-domain", text)], p));
            Assert.Equal(["features.licensed-domain"], Fields(problems).Distinct().ToArray());
        }
    }

    [Fact]
    public void TextAtLimit_IsAccepted()
    {
        Assert.Empty(Run(p => ContentRules.CheckFeatures([FeatureInput.OfText("t", new string('x', 256))], p)));
        Assert.Empty(Run(p => ContentRules.CheckFeatures([FeatureInput.OfText("t", Astral(256))], p)));
        Assert.Equal(["features.t"], Fields(Run(p => ContentRules.CheckFeatures([FeatureInput.OfText("t", Astral(257))], p))));
    }

    [Fact]
    public void PersonalDataInText_IsAccepted()
    {
        Assert.Empty(Run(p => ContentRules.CheckFeatures([FeatureInput.OfText("owner", "Jane Smith")], p)));
    }

    // Example 14 and "Several problems"

    [Theory]
    [MemberData(nameof(Cultures))]
    public void SeveralProblems_AreAllReported(string culture)
    {
        using var scope = new CultureScope(culture);

        var problems = ContentRules.Check(
            "commerce",
            " Pro",
            "INV 2026 04",
            [FeatureInput.OfNumber("max-orders", -200m), FeatureInput.Switch("pro", false), FeatureInput.Switch("Max Orders", true)]);

        Assert.Equal(
            ["product", "displayName", "vendorTag", "features.max-orders", "features.pro", "features"],
            Fields(problems));
    }

    [Fact]
    public void Example14_ContentRowsAreRejectedWithTheirFields()
    {
        var expected = new (string Field, ContentCase Case)[]
        {
            ("product", new("commerce", null, null, [])),
            ("product", new("Acme.Commerce", null, null, [])),
            ("product", new(Product(65), null, null, [])),
            ("displayName", new("acme.commerce", " Pro", null, [])),
            ("displayName", new("acme.commerce", "a\nb", null, [])),
            ("displayName", new("acme.commerce", new string('x', 65), null, [])),
            ("vendorTag", new("acme.commerce", null, "INV 2026 04", [])),
            ("vendorTag", new("acme.commerce", null, "jane@acme.com", [])),
            ("vendorTag", new("acme.commerce", null, "", [])),
            ("vendorTag", new("acme.commerce", null, new string('x', 65), [])),
            ("features.pro", new("acme.commerce", null, null, [FeatureInput.Switch("pro", false)])),
            ("features.max-orders", new("acme.commerce", null, null, [FeatureInput.OfNumber("max-orders", -200m)])),
            ("features.storage-gb", new("acme.commerce", null, null, [FeatureInput.OfNumber("storage-gb", 2.12345m)])),
            ("features", new("acme.commerce", null, null, [FeatureInput.Switch("max-orders", true), FeatureInput.Switch("max-orders", true)])),
            ("features", new("acme.commerce", null, null, [FeatureInput.OfNumber("Max Orders", 500m)])),
            ("features", new("acme.commerce", null, null, Many(51))),
            ("features.licensed-domain", new("acme.commerce", null, null, [FeatureInput.OfText("licensed-domain", "")])),
            ("features.licensed-domain", new("acme.commerce", null, null, [FeatureInput.OfText("licensed-domain", " example.com")])),
            ("features.licensed-domain", new("acme.commerce", null, null, [FeatureInput.OfText("licensed-domain", "a\nb")])),
            ("features.licensed-domain", new("acme.commerce", null, null, [FeatureInput.OfText("licensed-domain", new string('x', 257))])),
        };

        foreach (var (field, c) in expected)
        {
            var problems = ContentRules.Check(c.Product, c.DisplayName, c.VendorTag, c.Features);
            Assert.Equal([field], Fields(problems));
        }
    }

    [Fact]
    public void Example14_AllowedRowsAreAccepted()
    {
        FeatureInput[] features =
        [
            FeatureInput.OfText("licensed-domains", "a.com,A.com"),
            FeatureInput.OfNumber("discount-rate", 0.15m),
        ];

        Assert.Empty(ContentRules.Check("acme.commerce", null, null, features));
    }

    private sealed record ContentCase(string? Product, string? DisplayName, string? VendorTag, IReadOnlyList<FeatureInput> Features);
}
