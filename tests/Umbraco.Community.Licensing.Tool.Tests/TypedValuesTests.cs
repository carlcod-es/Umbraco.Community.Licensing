namespace Umbraco.Community.Licensing.Tool.Tests;

public class TypedValuesTests
{
    [Fact]
    public void CheckDisplayName_ValidName_GivesNoProblems() =>
        Assert.Empty(TypedValues.CheckDisplayName("Acme Ltd"));

    [Fact]
    public void CheckDisplayName_NameWithEmoji_GivesProblem() =>
        Assert.NotEmpty(TypedValues.CheckDisplayName("Acme \U0001F600"));
}
