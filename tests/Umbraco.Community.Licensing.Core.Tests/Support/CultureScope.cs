using System.Globalization;

namespace Umbraco.Community.Licensing.Core.Tests.Support;

/// <summary>Sets the current and UI culture for the scope and restores both on dispose.</summary>
/// <remarks>Create the scope at the top of the test method: culture does not flow out of an awaited helper.</remarks>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _previousUICulture = CultureInfo.CurrentUICulture;

    public CultureScope(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUICulture;
    }
}
