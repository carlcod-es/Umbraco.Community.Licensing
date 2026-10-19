using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class NumberRuleTests
{
    public static TheoryData<string> Cultures => ["en-US", "tr-TR", "de-DE"];

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Normalise_RemovesTrailingZerosAndUsesInvariantText(string culture)
    {
        using var scope = new CultureScope(culture);

        (decimal Value, string Text)[] cases =
        [
            (2.50m, "2.5"), (2.5m, "2.5"), (2500m, "2500"), (0.15m, "0.15"), (0m, "0"), (0.00m, "0"),
            (-0.0m, "0"), (10.0000m, "10"), (100.1000m, "100.1"), (0.0001m, "0.0001"), (2.500000m, "2.5"),
            (12345678901.2345m, "12345678901.2345"), (999999999999999m, "999999999999999"),
        ];
        foreach (var (value, text) in cases)
        {
            Assert.True(NumberRule.TryNormalise(value, out var actual, out var reason));
            Assert.Null(reason);
            Assert.Equal(text, actual);
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2.12345")]
    [InlineData("1234567890123456")]
    [InlineData("123456789012.3456")]
    [InlineData("1000000000000000")]
    public void Normalise_RejectsInvalid(string input)
    {
        var value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(NumberRule.TryNormalise(value, out var text, out var reason));
        Assert.Empty(text);
        Assert.NotNull(reason);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("2.5")]
    [InlineData("0.0001")]
    [InlineData("2.50")]
    [InlineData("12345678901.2345")]
    [InlineData("999999999999999")]
    public void Grammar_AcceptsValid(string text)
    {
        Assert.True(NumberRule.IsCanonical(text));
        Assert.True(NumberRule.TryParse(text, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("01")]
    [InlineData("00")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("1.23456")]
    [InlineData("1e3")]
    [InlineData("1E3")]
    [InlineData("1,5")]
    [InlineData("1 ")]
    [InlineData("١٢")]
    [InlineData("1234567890123456")]
    [InlineData("123456789012.3456")]
    [InlineData("0.12345678901234")]
    public void Grammar_RejectsInvalid(string text)
    {
        Assert.False(NumberRule.IsCanonical(text));
        Assert.False(NumberRule.TryParse(text, out _));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Normalise_ThenParse_RoundTrips(string culture)
    {
        using var scope = new CultureScope(culture);

        foreach (var value in new[] { 0m, 0.15m, 2.5m, 2500m, 12345678901.2345m })
        {
            Assert.True(NumberRule.TryNormalise(value, out var text, out _));
            Assert.True(NumberRule.TryParse(text, out var parsed));
            Assert.Equal(value, parsed);
        }
    }
}
