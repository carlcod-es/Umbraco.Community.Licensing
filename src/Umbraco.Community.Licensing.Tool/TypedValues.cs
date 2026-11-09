using System.Globalization;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>Typed-value checks for the menu prompts, delegating to the library's content rules where it has them.</summary>
internal static class TypedValues
{
    internal const int PassphraseMinLength = 15;
    internal const int PassphraseMaxLength = 256;

    internal const string PassphraseTooShort = "The passphrase must be at least 15 characters.";
    internal const string PassphraseTooLong = "The passphrase can be at most 256 characters.";
    internal const string PassphraseCharacters =
        "The passphrase can use only letters A to Z, digits, spaces and the symbols on a standard keyboard.";
    internal const string PassphraseEdgeSpace = "The passphrase cannot start or end with a space.";

    internal const string PrefixRule = "A prefix is 1 to 16 characters of A to Z and 0 to 9, or none.";
    internal const string IdleLockRule = "The idle lock is a whole number of minutes from 1 to 1440, or never.";

    /// <summary>The idle lock setting for no lock; stored in lower case.</summary>
    internal const string Never = "never";

    /// <summary>The words that mean no prefix, in any case.</summary>
    internal const string NoPrefixWord = "none";

    /// <summary>Checks a display name with the library's rules; an empty list means it is valid.</summary>
    public static IReadOnlyList<LicenseProblem> CheckDisplayName(string displayName)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckDisplayName(displayName, problems);
        return problems;
    }

    /// <summary>
    /// The passphrase rule (ADR-0019), as a <see cref="HiddenRule"/>: each character U+0020 to U+007E, 15 to 256
    /// of them, the first and last not a space. Returns the rule broken, or null. <paramref name="length"/> is the
    /// true count typed, which can exceed the characters held. The passphrase never becomes a string.
    /// </summary>
    public static string? CheckPassphrase(ReadOnlySpan<char> held, int length)
    {
        foreach (var c in held)
        {
            if (c is < ' ' or > '~')
                return PassphraseCharacters;
        }

        if (length < PassphraseMinLength)
            return PassphraseTooShort;
        if (length > PassphraseMaxLength)
            return PassphraseTooLong;
        if (held[0] == ' ' || held[^1] == ' ')
            return PassphraseEdgeSpace;
        return null;
    }

    /// <summary>
    /// Reads a prefix answer: <c>none</c> in any case is no prefix, anything else must pass the library's prefix rule.
    /// Returns the rule broken, or null; <paramref name="prefix"/> is the prefix when it passes.
    /// </summary>
    public static string? CheckPrefix(string text, out KeyPrefix prefix)
    {
        prefix = KeyPrefix.None;
        if (string.Equals(text, NoPrefixWord, StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            prefix = KeyPrefix.Of(text);
            return null;
        }
        catch (ArgumentException)
        {
            return PrefixRule;
        }
    }

    /// <summary>The prefix rule as a <see cref="Questions.AskText"/> rule.</summary>
    public static string? CheckPrefix(string text) => CheckPrefix(text, out _);

    /// <summary>
    /// The idle lock rule (ADR-0019): a whole number from 1 to 1440 written with ASCII digits, or <c>never</c> in any
    /// case. <paramref name="idleTime"/> is the time, or null for never.
    /// </summary>
    public static bool TryParseIdleLock(string text, out TimeSpan? idleTime)
    {
        idleTime = null;
        if (string.Equals(text, Never, StringComparison.OrdinalIgnoreCase))
            return true;
        if (text.Length > 0 && text.All(char.IsAsciiDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes is >= 1 and <= 1440)
        {
            idleTime = TimeSpan.FromMinutes(minutes);
            return true;
        }

        return false;
    }

    /// <summary>The idle lock rule as a <see cref="Questions.AskText"/> rule.</summary>
    public static string? CheckIdleLock(string text) => TryParseIdleLock(text, out _) ? null : IdleLockRule;

    /// <summary>The stored form of an idle time: whole minutes as ASCII digits, or <c>never</c>.</summary>
    public static string IdleLockSetting(TimeSpan? idleTime) =>
        idleTime is { } t ? ((int)t.TotalMinutes).ToString(CultureInfo.InvariantCulture) : Never;
}
