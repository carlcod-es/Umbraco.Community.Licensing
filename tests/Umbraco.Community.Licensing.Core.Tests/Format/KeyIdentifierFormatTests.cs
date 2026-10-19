namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class KeyIdentifierReferenceOfTests
{
    [Theory]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D", "8F3AKM7RXB")]
    [InlineData("8F3AK-M7RXB-7Q2D", "8F3AKM7RXB")]
    [InlineData("A-8F3AK-M7RXB-7Q2D", "8F3AKM7RXB")]
    [InlineData("ABCDEFGHIJ123456-8F3AK-M7RXB-7Q2D", "8F3AKM7RXB")]
    public void ReferenceOf_ReturnsTenCharactersWithoutHyphensOrPrefix(string identifier, string reference)
    {
        Assert.Matches(KeyIdentifierFormat.IdentifierPattern, identifier);
        Assert.Equal(reference, KeyIdentifierFormat.ReferenceOf(identifier));
    }
}

public class KeyIdentifierTryReferenceOfDisplayTests
{
    [Theory]
    [InlineData("LIC-8F3AK-M7RXB")]
    [InlineData("8F3AK-M7RXB")]
    [InlineData("A-8F3AK-M7RXB")]
    [InlineData("ABCDEFGHIJ123456-8F3AK-M7RXB")]
    public void Valid_ReturnsTenCharacters(string display)
    {
        Assert.True(KeyIdentifierFormat.TryReferenceOfDisplay(display, out var reference));
        Assert.Equal("8F3AKM7RXB", reference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("8F3AKM7RXB")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D")]
    [InlineData("-8F3AK-M7RXB")]
    [InlineData("ABCDEFGHIJ1234567-8F3AK-M7RXB")]
    [InlineData("lic-8F3AK-M7RXB")]
    [InlineData("LIC-8F3AK-M7RX0")]
    [InlineData("LIC-8F3AK-M7RXB\n")]
    [InlineData("LIC-8F3AK- M7RXB")]
    public void Malformed_ReturnsFalseAndEmpty(string? display)
    {
        Assert.False(KeyIdentifierFormat.TryReferenceOfDisplay(display, out var reference));
        Assert.Equal(string.Empty, reference);
    }
}
