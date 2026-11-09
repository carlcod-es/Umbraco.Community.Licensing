using System.Globalization;
using System.Text;

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
    internal const string PrefixAccepts = "LIC, your own (1 to 16 characters of A to Z and 0 to 9) or none";
    internal const string IdleLockAccepts = "a whole number of minutes from 1 to 1440, or never";
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

    internal const string ProductIdAccepts = "two dot-separated parts of a to z, 0 to 9 and hyphens, each starting with a letter, at most 64 characters, like acme.commerce";
    internal const string DisplayNameAccepts = "1 to 64 characters: printable ASCII, and letters, combining marks, digits and punctuation from any script";
    internal const string FeatureNameAccepts = "1 to 64 characters of a to z, 0 to 9 and hyphens, starting with a letter";

    /// <summary>The display name rule as a <see cref="Questions.AskText"/> rule: every problem found, on one line, or null.</summary>
    public static string? CheckDisplayNameText(string displayName) => Join(CheckDisplayName(displayName));

    /// <summary>The library's product ID rule as a <see cref="Questions.AskText"/> rule.</summary>
    public static string? CheckProductId(string productId)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckProduct(productId, problems);
        return Join(problems);
    }

    /// <summary>The library's feature name rule as a <see cref="Questions.AskText"/> rule.</summary>
    public static string? CheckFeatureName(string name)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckFeatures([FeatureInput.Switch(name, true)], problems);
        return Join(problems);
    }

    private static string? Join(IReadOnlyList<LicenseProblem> problems) =>
        problems.Count == 0 ? null : string.Join(" ", problems.Select(p => p.Message));

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

    internal const int EmailMinLength = 3;
    internal const int EmailMaxLength = 254;
    internal const int OrderRefMaxLength = 64;

    internal const string EmailAccepts = "3 to 254 characters, with exactly one @ that has text before and after it, and no spaces";
    internal const string OrderRefAccepts = "1 to 64 characters: printable ASCII, and letters, combining marks, digits and punctuation from any script";
    internal const string EmailRule = "A customer email must be 3 to 254 characters, with exactly one @ that has text before and after it, and no spaces.";

    /// <summary>
    /// The customer email rule (ADR-0019): the library's text check for field <c>customerEmail</c> at 254, then at
    /// least 3 characters, exactly one <c>@</c> with text before and after it, and no space. Returns the rule broken
    /// naming the customer email, or null.
    /// </summary>
    public static string? CheckCustomerEmail(string email)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckText("customerEmail", "A customer email", email, EmailMaxLength, problems);
        if (problems.Count > 0)
            return $"{Join(problems)} {EmailRule}"; // the text rule's message alone would not say the @ and space rules

        var at = email.IndexOf('@');
        var valid = email.Length >= EmailMinLength
            && at > 0
            && at < email.Length - 1
            && email.IndexOf('@', at + 1) < 0
            && !email.Contains(' ');
        return valid ? null : EmailRule;
    }

    /// <summary>The order reference rule (ADR-0019): the library's text check for field <c>orderRef</c> at 64. Returns the rule broken, or null.</summary>
    public static string? CheckOrderReference(string orderRef)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckText("orderRef", "An order reference", orderRef, OrderRefMaxLength, problems);
        return Join(problems);
    }

    internal const string ExpiryAccepts = "a date like 2027-03-01, a UTC date and time like 2027-03-01T23:59:59Z, or perpetual";
    internal const string ExpiryRuleMessage =
        "The expiry must be a real date like 2027-03-01 (valid to 23:59:59 UTC that day), a UTC date and time to the second like 2027-03-01T23:59:59Z, or perpetual.";
    internal const string VendorTagAccepts = "1 to 64 characters from A-Z a-z 0-9 - _ . # /";
    internal const string NumberAccepts = "a number, zero or more, with at most 4 decimal places and 15 digits in total";
    internal const string SwitchAccepts = "on or off";
    internal const string SwitchRule = "A switch is answered on or off.";
    internal const string PerpetualWord = "perpetual";

    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>An expiry answer: the library's value and the UTC instant it resolves to (null is perpetual).</summary>
    internal sealed record ExpiryAnswer(LicenseExpiry Expiry, DateTime? ExpiresUtc);

    /// <summary>
    /// Reads an expiry answer (PDR-0007): <c>perpetual</c> in any case, a real date <c>2027-03-01</c> (the end of that day, UTC)
    /// or a UTC date and time to the second <c>2027-03-01T23:59:59Z</c>; then the library's expiry rule against
    /// <paramref name="now"/> (not earlier than the current second). Returns the rule broken, or null with <paramref name="answer"/> set.
    /// </summary>
    public static string? ReadExpiry(string text, DateTimeOffset now, out ExpiryAnswer? answer)
    {
        answer = null;
        LicenseExpiry expiry;
        ExpiryStatement statement;
        if (string.Equals(text, PerpetualWord, StringComparison.OrdinalIgnoreCase))
        {
            expiry = LicenseExpiry.Perpetual;
            statement = ExpiryStatement.Perpetual;
        }
        else if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            expiry = LicenseExpiry.On(date);
            statement = ExpiryStatement.OnDate(date);
        }
        else if (DateTimeOffset.TryParseExact(
            text, InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant))
        {
            expiry = LicenseExpiry.At(instant);
            statement = ExpiryStatement.AtInstant(instant);
        }
        else
        {
            return ExpiryRuleMessage;
        }

        var problems = new List<LicenseProblem>();
        if (!ExpiryRule.TryResolve(statement, now, problems, out var expires))
            return Join(problems);

        answer = new ExpiryAnswer(expiry, expires?.UtcDateTime);
        return null;
    }

    /// <summary>The expiry rule as a <see cref="Questions.AskText"/> rule.</summary>
    public static string? CheckExpiry(string text, DateTimeOffset now) => ReadExpiry(text, now, out _);

    /// <summary>The library's vendor tag rule as a <see cref="Questions.AskText"/> rule.</summary>
    public static string? CheckVendorTag(string vendorTag)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckVendorTag(vendorTag, problems);
        return Join(problems);
    }

    /// <summary>Reads <c>on</c> or <c>off</c> in any case. Returns the rule broken, or null with <paramref name="on"/> set.</summary>
    public static string? ReadSwitch(string text, out bool on)
    {
        on = string.Equals(text, "on", StringComparison.OrdinalIgnoreCase);
        return on || string.Equals(text, "off", StringComparison.OrdinalIgnoreCase) ? null : SwitchRule;
    }

    /// <summary>
    /// Reads a number feature: ASCII digits with an optional decimal point, then the library's number rule (zero or more, at most
    /// 4 decimal places, 15 digits in all). Returns the rule broken, or null with <paramref name="value"/> set.
    /// </summary>
    public static string? ReadNumber(string text, out decimal value)
    {
        value = 0m;
        if (text.Length == 0 || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
            || !text.All(c => char.IsAsciiDigit(c) || c is '.' or '-' or '+'))
            return $"'{(text.Length > 32 ? text[..32] + "..." : text)}' is not a number. A number is {NumberAccepts}.";

        return NumberRule.TryNormalise(value, out _, out var reason) ? null : $"{reason} A number is {NumberAccepts}.";
    }

    /// <summary>A text feature value by the library's rule (<c>features.&lt;name&gt;</c>, 1 to 256 characters). Returns the rule broken, or null.</summary>
    public static string? CheckTextFeature(string name, string text)
    {
        var problems = new List<LicenseProblem>();
        ContentRules.CheckFeatures([FeatureInput.OfText(name, text)], problems);
        return Join(problems);
    }

    /// <summary>
    /// The match key for finding by customer email or order reference (ADR-0019): normalised to form C, then lower
    /// case in the invariant culture. Compared whole, for equality; spaces are kept. The stored value stays as typed.
    /// </summary>
    public static string MatchKey(string value) => value.Normalize(NormalizationForm.FormC).ToLowerInvariant();
}
