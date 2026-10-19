namespace Umbraco.Community.Licensing.Core;

/// <summary>Reads a reference a vendor supplied, against the configured prefix.</summary>
internal static class ReferenceParser
{
    /// <summary>
    /// Ignores case (ASCII), hyphens and spaces, then accepts 10 alphabet characters, or the
    /// configured prefix followed by 10. With a null prefix only the 10-character form is accepted.
    /// No prefix is passed as null, never an empty string.
    /// </summary>
    /// <param name="input">The supplied text.</param>
    /// <param name="prefix">The configured prefix, or null for none.</param>
    /// <param name="reference">The 10 canonical characters, without prefix or hyphen, when accepted.</param>
    /// <param name="problem">The rejection, naming <c>reference</c>, otherwise null.</param>
    public static bool TryParse(string? input, string? prefix, out string reference, out LicenseProblem? problem)
    {
        reference = string.Empty;
        var normalised = Normalise(input);
        var bare = normalised;

        if (prefix is not null
            && normalised.Length == prefix.Length + ReferenceAlphabet.ReferenceLength
            && normalised.StartsWith(prefix, StringComparison.Ordinal))
        {
            bare = normalised[prefix.Length..];
        }

        if (bare.Length == ReferenceAlphabet.ReferenceLength && ReferenceAlphabet.IsValid(bare))
        {
            reference = bare;
            problem = null;
            return true;
        }

        problem = new LicenseProblem("reference", Describe(prefix));
        return false;
    }

    private static string Normalise(string? input)
    {
        if (input is null)
        {
            return string.Empty;
        }

        var kept = 0;
        foreach (var c in input)
        {
            if (c is not ('-' or ' '))
            {
                kept++;
            }
        }

        return string.Create(kept, input, static (span, source) =>
        {
            var count = 0;
            foreach (var c in source)
            {
                if (c is '-' or ' ')
                {
                    continue;
                }

                span[count++] = c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
            }
        });
    }

    private static string Describe(string? prefix) => prefix is null
        ? "The reference must be 10 characters from the reference alphabet (no 0, O, 1, I or L); hyphens, spaces and case are ignored."
        : $"The reference must be 10 characters from the reference alphabet (no 0, O, 1, I or L), optionally preceded by the prefix {prefix}; hyphens, spaces and case are ignored.";
}
