using System.Globalization;
using System.Text;

namespace Umbraco.Community.Licensing.Core;

/// <summary>A feature as the content rules see it: a name and a typed value. Task 5.1's public types map onto this.</summary>
internal readonly record struct FeatureInput
{
    private FeatureInput(string? name, FeatureKind kind, bool isOn, decimal number, string? text)
    {
        Name = name;
        Kind = kind;
        IsOn = isOn;
        Number = number;
        Text = text;
    }

    public string? Name { get; }

    public FeatureKind Kind { get; }

    public bool IsOn { get; }

    public decimal Number { get; }

    public string? Text { get; }

    public static FeatureInput Switch(string? name, bool isOn) => new(name, FeatureKind.Switch, isOn, 0m, null);

    public static FeatureInput OfNumber(string? name, decimal value) => new(name, FeatureKind.Number, false, value, null);

    public static FeatureInput OfText(string? name, string? value) => new(name, FeatureKind.Text, false, 0m, value);
}

internal enum FeatureKind
{
    Switch,
    Number,
    Text,
}

/// <summary>
/// The issue rules for product ID, display name, vendor tag and features (PDR-0017 limits). Used by the
/// issuer to reject and by the strict reader to report not supported, so issue and read never disagree.
/// Culture-invariant and ordinal; lengths count Unicode scalar values; every problem is reported.
/// </summary>
internal static class ContentRules
{
    public const int MaxProductLength = 64;

    public const int MaxFeatureNameLength = 64;

    public const int MaxFeatures = 50;

    public const int MaxDisplayNameLength = 64;

    public const int MaxVendorTagLength = 64;

    public const int MaxFeatureTextLength = 256;

    /// <summary>Checks every content field except the expiry and reference. Null display name or vendor tag means not given.</summary>
    public static List<LicenseProblem> Check(string? product, string? displayName, string? vendorTag, IReadOnlyList<FeatureInput> features)
    {
        var problems = new List<LicenseProblem>();
        CheckProduct(product, problems);
        if (displayName is not null)
        {
            CheckDisplayName(displayName, problems);
        }

        if (vendorTag is not null)
        {
            CheckVendorTag(vendorTag, problems);
        }

        CheckFeatures(features, problems);
        return problems;
    }

    /// <summary>Two dot-separated parts of lowercase letters, digits and hyphens, each starting with a letter; at most 64 characters.</summary>
    public static void CheckProduct(string? product, List<LicenseProblem> problems)
    {
        if (product is null)
        {
            problems.Add(new LicenseProblem("product", "A product ID is required."));
            return;
        }

        var dot = product.IndexOf('.', StringComparison.Ordinal);
        var valid = dot > 0
            && IsNamePart(product.AsSpan(0, dot))
            && IsNamePart(product.AsSpan(dot + 1));

        if (!valid)
        {
            problems.Add(new LicenseProblem("product", "A product ID must be two dot-separated parts, each of lowercase a-z, digits and hyphens, starting with a letter (for example acme.commerce)."));
        }
        else if (product.Length > MaxProductLength)
        {
            problems.Add(new LicenseProblem("product", $"A product ID may have at most {MaxProductLength} characters."));
        }
    }

    /// <summary>1 to 64 characters, no leading or trailing whitespace, no line breaks or control characters.</summary>
    public static void CheckDisplayName(string displayName, List<LicenseProblem> problems) =>
        CheckText("displayName", "A display name", displayName, MaxDisplayNameLength, problems);

    /// <summary>1 to 64 characters from <c>A-Z a-z 0-9 - _ . # /</c>.</summary>
    public static void CheckVendorTag(string vendorTag, List<LicenseProblem> problems)
    {
        var valid = vendorTag.Length is >= 1 and <= MaxVendorTagLength;
        foreach (var c in vendorTag)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '#' or '/'))
            {
                valid = false;
                break;
            }
        }

        if (!valid)
        {
            problems.Add(new LicenseProblem("vendorTag", $"A vendor tag must be 1 to {MaxVendorTagLength} characters from A-Z a-z 0-9 - _ . # /."));
        }
    }

    /// <summary>At most 50 features; valid names, each once; each value valid for its type.</summary>
    public static void CheckFeatures(IReadOnlyList<FeatureInput> features, List<LicenseProblem> problems)
    {
        if (features.Count > MaxFeatures)
        {
            problems.Add(new LicenseProblem("features", $"A license may have at most {MaxFeatures} features."));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reportedRepeats = new HashSet<string>(StringComparer.Ordinal);

        foreach (var feature in features)
        {
            if (!IsValidFeatureName(feature.Name))
            {
                problems.Add(new LicenseProblem("features", $"The feature name {Quote(feature.Name)} is not valid: a feature name must be 1 to {MaxFeatureNameLength} characters of lowercase a-z, digits and hyphens, starting with a letter."));
                continue;
            }

            var name = feature.Name!;
            if (!seen.Add(name) && reportedRepeats.Add(name))
            {
                problems.Add(new LicenseProblem("features", $"The feature name {name} appears more than once."));
            }

            CheckFeatureValue(feature, problems);
        }
    }

    /// <summary>1 to 64 characters of lowercase a-z, digits and hyphens, starting with a letter.</summary>
    public static bool IsValidFeatureName(string? name) =>
        name is not null && name.Length <= MaxFeatureNameLength && IsNamePart(name);

    private const int MaxQuotedLength = 32;

    /// <summary>The name for a message: <c>(none)</c> when null or empty, otherwise cut to 32 characters without splitting a surrogate pair.</summary>
    private static string Quote(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "(none)";
        }

        if (name.Length <= MaxQuotedLength)
        {
            return $"\"{name}\"";
        }

        var length = char.IsHighSurrogate(name[MaxQuotedLength - 1]) ? MaxQuotedLength - 1 : MaxQuotedLength;
        return $"\"{name[..length]}...\"";
    }

    private static void CheckFeatureValue(FeatureInput feature, List<LicenseProblem> problems)
    {
        var field = $"features.{feature.Name}";
        switch (feature.Kind)
        {
            case FeatureKind.Switch:
                if (!feature.IsOn)
                {
                    problems.Add(new LicenseProblem(field, "A switch is granted by being present; false is not allowed."));
                }

                break;

            case FeatureKind.Number:
                if (!NumberRule.TryNormalise(feature.Number, out _, out var reason))
                {
                    problems.Add(new LicenseProblem(field, reason!));
                }

                break;

            default:
                CheckText(field, "A text value", feature.Text, MaxFeatureTextLength, problems);
                break;
        }
    }

    private static bool IsNamePart(ReadOnlySpan<char> part)
    {
        if (part.IsEmpty || !char.IsAsciiLetterLower(part[0]))
        {
            return false;
        }

        foreach (var c in part)
        {
            if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static void CheckText(string field, string subject, string? value, int maxLength, List<LicenseProblem> problems)
    {
        if (value is null)
        {
            problems.Add(new LicenseProblem(field, $"{subject} is required."));
            return;
        }

        var hasSurrogateError = !TryScan(value, out var scalars, out var hasControl);
        if (hasSurrogateError)
        {
            problems.Add(new LicenseProblem(field, $"{subject} contains an unpaired surrogate."));
            return;
        }

        if (scalars is 0 || scalars > maxLength)
        {
            problems.Add(new LicenseProblem(field, $"{subject} must be 1 to {maxLength} characters."));
        }

        if (hasControl)
        {
            problems.Add(new LicenseProblem(field, $"{subject} must not contain line breaks or control characters."));
        }

        if (scalars > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])))
        {
            problems.Add(new LicenseProblem(field, $"{subject} must not start or end with whitespace."));
        }
    }

    // Control characters: category Cc plus U+2028 and U+2029 (ADR-0002).
    private static bool IsControl(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control || rune.Value is 0x2028 or 0x2029;

    // Counts scalar values and flags control characters; false when the text has an unpaired surrogate.
    private static bool TryScan(string value, out int scalars, out bool hasControl)
    {
        scalars = 0;
        hasControl = false;
        var i = 0;
        while (i < value.Length)
        {
            if (Rune.DecodeFromUtf16(value.AsSpan(i), out var rune, out var consumed) != System.Buffers.OperationStatus.Done)
            {
                return false;
            }

            scalars++;
            hasControl |= IsControl(rune);
            i += consumed;
        }

        return true;
    }
}
