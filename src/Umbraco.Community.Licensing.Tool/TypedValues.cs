
namespace Umbraco.Community.Licensing.Tool;

/// <summary>Typed-value checks for the menu prompts, delegating to the library's content rules.</summary>
internal static class TypedValues
{
    /// <summary>Checks a display name with the library's rules; an empty list means it is valid.</summary>
    public static IReadOnlyList<LicenseProblem> CheckDisplayName(string displayName)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckDisplayName(displayName, problems);
        return problems;
    }
}
