namespace Umbraco.Community.Licensing.Tool;

/// <summary>Reads a typed key prefix by the prefix rule (PDR-0023): the one parse and the one rule text, for setup and <c>prefix set</c>.</summary>
internal static class PrefixInput
{
    /// <summary>The prefix rule, as shown when an answer breaks it.</summary>
    public const string RuleText = "A key prefix must be 1 to 16 characters of A-Z and 0-9 (capital letters), or 'none'.";

    /// <summary>
    /// Reads <paramref name="answer"/> (already trimmed, not empty): <c>none</c> in any case is no prefix.
    /// </summary>
    /// <param name="prefix">The prefix to store; empty means none.</param>
    /// <returns>False when the answer breaks the prefix rule.</returns>
    public static bool TryParse(string answer, out string prefix)
    {
        if (string.Equals(answer, "none", StringComparison.OrdinalIgnoreCase))
        {
            prefix = KeyPrefix.None.Value;
            return true;
        }

        try
        {
            prefix = KeyPrefix.Of(answer).Value;
            return true;
        }
        catch (ArgumentException)
        {
            prefix = "";
            return false;
        }
    }

    /// <summary>A prefix as shown to the vendor: itself, or <c>none</c>.</summary>
    public static string Show(string prefix) => prefix.Length == 0 ? "none" : prefix;
}
