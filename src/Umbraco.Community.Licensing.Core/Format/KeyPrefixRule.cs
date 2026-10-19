namespace Umbraco.Community.Licensing.Core;

/// <summary>The rule for a key identifier prefix: 1 to 16 characters of <c>A</c>-<c>Z</c> and <c>0</c>-<c>9</c>. <see cref="IsValid"/> checks a stated prefix, so null is not valid; "no prefix" is null only in <c>KeyIdentifierFormat</c> and <c>ReferenceParser</c>.</summary>
internal static class KeyPrefixRule
{
    public const int MaxLength = 16;

    public static bool IsValid(string? prefix)
    {
        if (prefix is null || prefix.Length is 0 or > MaxLength)
        {
            return false;
        }

        foreach (var c in prefix)
        {
            if (!(c is >= 'A' and <= 'Z' || c is >= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }
}
