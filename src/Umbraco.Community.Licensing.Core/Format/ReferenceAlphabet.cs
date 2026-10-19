namespace Umbraco.Community.Licensing.Core;

/// <summary>The 31 characters allowed in a reference and a key part: uppercase letters and digits without <c>0 O 1 I L</c>.</summary>
internal static class ReferenceAlphabet
{
    public const string Characters = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public const int ReferenceLength = 10;

    public const int KeyPartLength = 4;

    public static bool Contains(char c) => Characters.Contains(c, StringComparison.Ordinal);

    public static bool IsValid(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (!Contains(c))
            {
                return false;
            }
        }

        return true;
    }
}
