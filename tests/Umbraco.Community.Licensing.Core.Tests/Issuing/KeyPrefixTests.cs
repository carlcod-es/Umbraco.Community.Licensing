namespace Umbraco.Community.Licensing.Core.Tests.Issuing;

public class KeyPrefixTests
{
    [Fact]
    public void Default_IsLic() => Assert.Equal("LIC", KeyPrefix.Default.Value);

    [Fact]
    public void None_ValueIsEmpty() => Assert.Equal(string.Empty, KeyPrefix.None.Value);

    [Fact]
    public void LongestPrefix_IsAccepted()
    {
        var prefix = KeyPrefix.Of("ACMECOMMERCE2026");

        Assert.Equal("ACMECOMMERCE2026", prefix.Value);
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("ACME_SHOP")]
    [InlineData("ACME-SHOP")]
    [InlineData("")]
    [InlineData("ACMECOMMERCE20261")]
    public void InvalidPrefix_Throws(string prefix) => Assert.Throws<ArgumentException>(() => KeyPrefix.Of(prefix));

    [Fact]
    public void NullPrefix_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => KeyPrefix.Of(null!));

    [Fact]
    public void Of_EqualsByValue() => Assert.Equal(KeyPrefix.Default, KeyPrefix.Of("LIC"));

    [Fact]
    public void ToFormatPrefix_MapsNoneToNullAndOthersToTheirValue()
    {
        Assert.Null(KeyPrefix.None.ToFormatPrefix());
        Assert.Equal("LIC", KeyPrefix.Default.ToFormatPrefix());
        Assert.Equal("ACME", KeyPrefix.Of("ACME").ToFormatPrefix());
    }
}
