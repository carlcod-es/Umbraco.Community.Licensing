using System.Globalization;
using System.Text;

namespace Umbraco.Community.Licensing.Tests.Format;

public class TextRuleTableTests
{
    private static bool Allowed(int value) => TextRuleTable.IsAllowed(new Rune(value));

    [Fact]
    public void UnicodeVersion_is_16()
    {
        Assert.Equal("16.0.0", TextRuleTable.UnicodeVersion);
    }

    [Fact]
    public void Ranges_are_sorted_non_overlapping_and_merged()
    {
        var r = TextRuleTable.Ranges;
        Assert.Equal(0, r.Length % 2);
        Assert.Equal(791, r.Length / 2);
        Assert.True(r[0] >= 0x80);
        Assert.True(r[^1] <= 0x10FFFF);
        for (var i = 0; i < r.Length; i += 2)
        {
            Assert.True(r[i] <= r[i + 1], $"range {i / 2} start > end");
            if (i > 0)
            {
                Assert.True(r[i] > r[i - 1] + 1, $"range {i / 2} overlaps or touches the previous range");
            }
        }
    }

    [Fact]
    public void Printable_ascii_is_allowed_and_other_ascii_refused()
    {
        for (var v = 0x20; v <= 0x7E; v++)
        {
            Assert.True(Allowed(v), $"U+{v:X4}");
        }

        for (var v = 0; v < 0x20; v++)
        {
            Assert.False(Allowed(v), $"U+{v:X4}");
        }

        Assert.False(Allowed(0x7F));
    }

    [Theory]
    [InlineData(0x00E9)]
    [InlineData(0x30B3)]
    [InlineData(0x20000)]
    public void Spot_allowed(int value) => Assert.True(Allowed(value));

    [Theory]
    [InlineData(0x2B50)]
    [InlineData(0x20E3)]
    [InlineData(0x00A0)]
    [InlineData(0x3164)]
    [InlineData(0xFE0F)]
    [InlineData(0xE000)]
    [InlineData(0x0378)]
    [InlineData(0x10940)]
    public void Spot_refused(int value) => Assert.False(Allowed(value));

    [Fact]
    public void Every_code_point_matches_runtime_categories_less_default_ignorables()
    {
        if (Rune.GetUnicodeCategory(new Rune(0x1C89)) == UnicodeCategory.OtherNotAssigned
            || Rune.GetUnicodeCategory(new Rune(0x10940)) != UnicodeCategory.OtherNotAssigned)
        {
            Assert.Skip("Runtime Unicode data is not 16.0 (probe U+1C89 assigned, U+10940 unassigned); cross-check skipped.");
        }

        var mismatches = new List<string>();
        var count = 0;
        for (var v = 0; v <= 0x10FFFF; v++)
        {
            if (v is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            var expected = v < 0x80 ? v is >= 0x20 and <= 0x7E : CategoryAllowed(Rune.GetUnicodeCategory(new Rune(v))) && !IsIgnorable(v);
            if (Allowed(v) != expected)
            {
                count++;
                if (mismatches.Count < 10)
                {
                    mismatches.Add($"U+{v:X4} expected {expected}");
                }
            }
        }

        Assert.True(count == 0, $"{count} mismatches, first: {string.Join(", ", mismatches)}");
    }

    private static bool CategoryAllowed(UnicodeCategory c) => c is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.NonSpacingMark
        or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.DecimalDigitNumber
        or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation
        or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation
        or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation;

    private static bool IsIgnorable(int v) =>
        v is 0x034F or 0x115F or 0x1160 or 0x17B4 or 0x17B5 or (>= 0x180B and <= 0x180D) or 0x180F or 0x3164
            or (>= 0xFE00 and <= 0xFE0F) or 0xFFA0 or (>= 0xE0100 and <= 0xE01EF);
}
