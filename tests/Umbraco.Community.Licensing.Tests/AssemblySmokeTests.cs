namespace Umbraco.Community.Licensing.Tests;

public class AssemblySmokeTests
{
    [Fact]
    public void LibraryAssembly_Loads_WithExpectedName()
    {
        var assembly = typeof(LicenseProblem).Assembly;

        Assert.Equal("Umbraco.Community.Licensing", assembly.GetName().Name);
    }
}
