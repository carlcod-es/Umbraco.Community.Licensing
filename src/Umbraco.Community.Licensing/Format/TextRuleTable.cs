using System.Text;

namespace Umbraco.Community.Licensing;

/// <summary>
/// The characters a text feature value may contain, by kind (PDR-0042): letters, marks, decimal digits and
/// punctuation, less default-ignorable code points, plus printable ASCII. The generated half holds the
/// non-ASCII ranges for a fixed Unicode version, so the answer does not depend on the host runtime (ADR-0018).
/// </summary>
internal static partial class TextRuleTable
{
    /// <summary>True when <paramref name="rune"/> is allowed in a text value. Does not allocate.</summary>
    internal static bool IsAllowed(Rune rune)
    {
        var value = rune.Value;
        if (value < 0x80)
        {
            return value is >= 0x20 and <= 0x7E;
        }

        var ranges = Ranges;
        var low = 0;
        var high = (ranges.Length / 2) - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (value < ranges[mid * 2])
            {
                high = mid - 1;
            }
            else if (value > ranges[(mid * 2) + 1])
            {
                low = mid + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
