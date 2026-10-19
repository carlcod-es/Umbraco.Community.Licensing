using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class ReferenceAlphabetTests
{
    [Fact]
    public void Alphabet_HasThirtyOneCharactersWithoutConfusableOnes()
    {
        Assert.Equal(31, ReferenceAlphabet.Characters.Length);
        Assert.Equal(31, ReferenceAlphabet.Characters.Distinct().Count());
        foreach (var c in "01OIL")
        {
            Assert.DoesNotContain(c, ReferenceAlphabet.Characters);
        }
    }

    [Fact]
    public void Alphabet_MatchesIdentifierPatternClass()
    {
        var accepted = Enumerable.Range(0, 128).Select(i => (char)i)
            .Where(c => KeyIdentifierFormat.IdentifierPattern.IsMatch($"{c}{c}{c}{c}{c}-{c}{c}{c}{c}{c}-{c}{c}{c}{c}")
                && !char.IsAsciiLetterLower(c))
            .ToArray();

        Assert.Equal(ReferenceAlphabet.Characters.Order().ToArray(), accepted.Order().ToArray());
    }
}

public class SecureRandomSourceTests
{
    [Fact]
    public void Generated_ValuesUseOnlyAlphabetAndHaveRightLength()
    {
        for (var i = 0; i < 500; i++)
        {
            var reference = SecureRandomSource.Instance.NextReference();
            var keyPart = SecureRandomSource.Instance.NextKeyPart();

            Assert.Equal(10, reference.Length);
            Assert.Equal(4, keyPart.Length);
            Assert.True(ReferenceAlphabet.IsValid(reference));
            Assert.True(ReferenceAlphabet.IsValid(keyPart));
        }
    }

    [Fact]
    public void Generated_CoversWholeAlphabet()
    {
        var seen = new HashSet<char>();
        for (var i = 0; i < 2000; i++)
        {
            seen.UnionWith(SecureRandomSource.Instance.NextReference());
        }

        Assert.Equal(31, seen.Count);
    }

    [Fact]
    public void Generated_ValuesDiffer()
    {
        Assert.NotEqual(SecureRandomSource.Instance.NextReference(), SecureRandomSource.Instance.NextReference());
    }

}

public class KeyPrefixRuleTests
{
    [Theory]
    [InlineData("LIC")]
    [InlineData("A")]
    [InlineData("ACME2026")]
    [InlineData("ACMECOMMERCE2026")]
    public void Valid_prefix(string prefix) => Assert.True(KeyPrefixRule.IsValid(prefix));

    [Theory]
    [InlineData("acme")]
    [InlineData("ACME_SHOP")]
    [InlineData("ACME-SHOP")]
    [InlineData("")]
    [InlineData("ACMECOMMERCE20261")]
    [InlineData("ACME SHOP")]
    [InlineData("ÄCME")]
    [InlineData(null)]
    public void Invalid_prefix(string? prefix) => Assert.False(KeyPrefixRule.IsValid(prefix));
}

public class KeyIdentifierFormatTests
{
    [Theory]
    [InlineData("LIC", "LIC-8F3AK-M7RXB", "LIC-8F3AK-M7RXB-7Q2D")]
    [InlineData("ACME", "ACME-8F3AK-M7RXB", "ACME-8F3AK-M7RXB-7Q2D")]
    [InlineData(null, "8F3AK-M7RXB", "8F3AK-M7RXB-7Q2D")]
    public void Display_forms(string? prefix, string reference, string identifier)
    {
        Assert.Equal(reference, KeyIdentifierFormat.DisplayReference(prefix, "8F3AKM7RXB"));
        Assert.Equal(identifier, KeyIdentifierFormat.DisplayIdentifier(prefix, "8F3AKM7RXB", "7Q2D"));
    }

    [Theory]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D")]
    [InlineData("ACME-8F3AK-M7RXB-7Q2D")]
    [InlineData("ACMECOMMERCE2026-8F3AK-M7RXB-7Q2D")]
    [InlineData("8F3AK-M7RXB-7Q2D")]
    public void Pattern_matches(string text) => Assert.Matches(KeyIdentifierFormat.IdentifierPattern, text);

    [Theory]
    [InlineData("lic-8F3AK-M7RXB-7Q2D")]
    [InlineData("LIC-8f3ak-m7rxb-7q2d")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2DX")]
    [InlineData("LIC-8F3AK-M7RX-7Q2D")]
    [InlineData("acme-8F3AK-M7RXB-7Q2D")]
    [InlineData("ACMECOMMERCE20261-8F3AK-M7RXB-7Q2D")]
    [InlineData("LIC-8F3AO-M7RXB-7Q2D")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D\n")]
    [InlineData("-8F3AK-M7RXB-7Q2D")]
    [InlineData("")]
    public void Pattern_rejects(string text) => Assert.DoesNotMatch(KeyIdentifierFormat.IdentifierPattern, text);
}

public class ReferenceParserTests
{
    [Theory]
    [InlineData("lic-8f3ak m7rxb", "LIC", "8F3AKM7RXB")]
    [InlineData("8F3AK-M7RXB", "LIC", "8F3AKM7RXB")]
    [InlineData("acme 8f3ak m7rxb", "ACME", "8F3AKM7RXB")]
    [InlineData("8F3AK-M7RXB", "ACME", "8F3AKM7RXB")]
    [InlineData("8F3AK-M7RXB", null, "8F3AKM7RXB")]
    [InlineData("8f3akm7rxb", null, "8F3AKM7RXB")]
    public void Reference_accepted_loosely(string input, string? prefix, string expected)
    {
        Assert.True(ReferenceParser.TryParse(input, prefix, out var reference, out var problem));
        Assert.Equal(expected, reference);
        Assert.Null(problem);
    }

    [Fact]
    public void Reference_with_a_vendor_prefix_displays_with_that_prefix()
    {
        ReferenceParser.TryParse("acme 8f3ak m7rxb", "ACME", out var reference, out _);

        Assert.Equal("ACME-8F3AK-M7RXB", KeyIdentifierFormat.DisplayReference("ACME", reference));
    }

    [Fact]
    public void Reference_accepted_loosely_under_turkish_culture()
    {
        using var scope = new CultureScope("tr-TR");

        Assert.True(ReferenceParser.TryParse("lic-8f3ak-m7rxb", "LIC", out var reference, out _));
        Assert.Equal("8F3AKM7RXB", reference);
    }

    [Theory]
    [InlineData("LIC-8F3AK-M7RXB", "ACME")]
    [InlineData("ACME-8F3AK-M7RXB-7Q2D", "ACME")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D", "LIC")]
    [InlineData("LIC-8F3AK-M7RXB", null)]
    [InlineData("ACME-8F3AK-M7RXB", null)]
    public void Reference_with_another_prefix_or_a_key_part(string input, string? prefix)
    {
        AssertRejected(input, prefix);
    }

    [Theory]
    [InlineData("8F3AO-M7RXB")]
    [InlineData("8F3AK-M7RX")]
    [InlineData("8F3AK-M7RXBB")]
    [InlineData("8F3A0-M7RXB")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("8F3AK\tM7RXB")]
    [InlineData("8F3AK_M7RXB")]
    [InlineData("8F3AK-M7RXB\0")]
    [InlineData("\08F3AK-M7RXB")]
    [InlineData("8F3AK\0M7RXB")]
    public void Invalid_reference(string? input)
    {
        AssertRejected(input, "LIC");
        AssertRejected(input, null);
    }

    private static void AssertRejected(string? input, string? prefix)
    {
        Assert.False(ReferenceParser.TryParse(input, prefix, out var reference, out var problem));
        Assert.Equal(string.Empty, reference);
        Assert.NotNull(problem);
        Assert.Equal("reference", problem.Field);
        Assert.False(string.IsNullOrWhiteSpace(problem.Message));
    }
}
