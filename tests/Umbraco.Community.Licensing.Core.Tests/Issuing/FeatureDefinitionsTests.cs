namespace Umbraco.Community.Licensing.Core.Tests.Issuing;

public class FeatureDefinitionsTests
{
    [Fact]
    public void None_IsEmpty() => Assert.Empty(FeatureDefinitions.None);

    [Fact]
    public void Adding_ReturnsNewInstanceAndKeepsOrderAndTypes()
    {
        var none = FeatureDefinitions.None;
        var defs = none.Switch("ecommerce").Number("max-orders").Text("region");

        Assert.Empty(none);
        Assert.Equal(3, defs.Count);
        Assert.Equal(
            [
                new KeyValuePair<string, FeatureType>("ecommerce", FeatureType.Switch),
                new KeyValuePair<string, FeatureType>("max-orders", FeatureType.Number),
                new KeyValuePair<string, FeatureType>("region", FeatureType.Text),
            ],
            defs);
    }

    [Fact]
    public void Adding_LeavesTheOriginalUnchanged()
    {
        var one = FeatureDefinitions.None.Switch("a");
        var two = one.Number("b");

        Assert.NotSame(one, two);
        Assert.Single(one);
        Assert.Equal(2, two.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Max Orders")]
    [InlineData("Max-orders")]
    [InlineData("1abc")]
    [InlineData("a_b")]
    [InlineData("a b")]
    public void InvalidName_RaisesArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => FeatureDefinitions.None.Switch(name));
        Assert.Throws<ArgumentException>(() => FeatureDefinitions.None.Number(name));
        Assert.Throws<ArgumentException>(() => FeatureDefinitions.None.Text(name));
    }

    [Fact]
    public void NameOverSixtyFourCharacters_RaisesArgumentException() =>
        Assert.Throws<ArgumentException>(() => FeatureDefinitions.None.Switch(new string('a', 65)));

    [Fact]
    public void NameOfSixtyFourCharacters_IsAccepted() =>
        Assert.Single(FeatureDefinitions.None.Switch(new string('a', 64)));

    [Fact]
    public void RepeatedName_RaisesArgumentException_EvenWithAnotherType()
    {
        var defs = FeatureDefinitions.None.Number("max-orders");

        Assert.Throws<ArgumentException>(() => defs.Number("max-orders"));
        Assert.Throws<ArgumentException>(() => defs.Text("max-orders"));
        Assert.Throws<ArgumentException>(() => defs.Switch("max-orders"));
    }

    [Fact]
    public void NullName_RaisesArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => FeatureDefinitions.None.Switch(null!));
        Assert.Throws<ArgumentNullException>(() => FeatureDefinitions.None.Number(null!));
        Assert.Throws<ArgumentNullException>(() => FeatureDefinitions.None.Text(null!));
    }
}
