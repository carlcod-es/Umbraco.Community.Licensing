using System.Globalization;

namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// The feature number rule (ADR-0002): zero or positive, at most 4 decimal places, at most 15 digits in
/// total, written without trailing fractional zeros. One grammar serves the issuer (which emits the
/// canonical text) and the strict reader (which checks the raw token against it).
/// </summary>
internal static class NumberRule
{
    public const int MaxDecimalPlaces = 4;

    public const int MaxDigits = 15;

    /// <summary>
    /// Checks a number and produces the canonical text the payload carries (<c>2.50</c> becomes <c>2.5</c>).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="text">The canonical invariant text when accepted.</param>
    /// <param name="reason">Why the number was rejected, otherwise null.</param>
    public static bool TryNormalise(decimal value, out string text, out string? reason)
    {
        text = string.Empty;

        if (value < 0)
        {
            reason = "A number must be zero or positive.";
            return false;
        }

        // Dividing by 1.000...0 (28 places) drops trailing fractional zeros without changing the value.
        var stripped = value / 1.0000000000000000000000000000m;
        if (stripped == 0)
        {
            stripped = 0m;
        }

        var candidate = stripped.ToString(CultureInfo.InvariantCulture);
        if (!IsCanonical(candidate))
        {
            reason = stripped.Scale > MaxDecimalPlaces
                ? $"A number may have at most {MaxDecimalPlaces} decimal places."
                : $"A number may have at most {MaxDigits} digits in total.";
            return false;
        }

        text = candidate;
        reason = null;
        return true;
    }

    /// <summary>Checks raw text against the grammar: <c>0</c> or <c>[1-9][0-9]*</c>, optional <c>.</c> and 1 to 4 digits, at most 15 digits in total.</summary>
    public static bool IsCanonical(ReadOnlySpan<char> text)
    {
        var i = 0;
        if (text.IsEmpty || !char.IsAsciiDigit(text[0]))
        {
            return false;
        }

        if (text[0] == '0')
        {
            i = 1;
        }
        else
        {
            while (i < text.Length && char.IsAsciiDigit(text[i]))
            {
                i++;
            }
        }

        var digits = i;
        if (i < text.Length)
        {
            if (text[i] != '.')
            {
                return false;
            }

            var fraction = text[(i + 1)..];
            if (fraction.Length is 0 or > MaxDecimalPlaces)
            {
                return false;
            }

            foreach (var c in fraction)
            {
                if (!char.IsAsciiDigit(c))
                {
                    return false;
                }
            }

            digits += fraction.Length;
        }

        return digits <= MaxDigits;
    }

    /// <summary>Checks raw text against the grammar, then parses it as a <see cref="decimal"/> (never <see cref="double"/>).</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out decimal value)
    {
        value = 0m;
        return IsCanonical(text)
            && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}
