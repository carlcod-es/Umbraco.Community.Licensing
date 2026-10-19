using System.Globalization;

namespace Umbraco.Community.Licensing.Core.Tests.Support;

public class CultureScopeTests
{
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void Scope_WhileActive_SetsCurrentAndUICulture(string name)
    {
        using var scope = new CultureScope(name);

        Assert.Equal(name, CultureInfo.CurrentCulture.Name);
        Assert.Equal(name, CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void Scope_AfterDispose_RestoresPreviousCultures()
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;

        using (new CultureScope("tr-TR"))
        {
        }

        Assert.Same(culture, CultureInfo.CurrentCulture);
        Assert.Same(uiCulture, CultureInfo.CurrentUICulture);
    }
}
