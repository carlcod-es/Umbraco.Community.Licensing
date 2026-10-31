using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>The expiry suggested when a license is reissued: the current expiry plus the length of the last term, measured on UTC dates.</summary>
internal static class ExpirySuggestion
{
    /// <summary>
    /// Suggests the expiry for a reissue. Pure: no clock, no database.
    /// </summary>
    /// <param name="entries">The license's issue entries in issue order (oldest first); the last one holds the current expiry.</param>
    /// <param name="isRenewal">True for a renewal; any other reissue suggests the current expiry.</param>
    /// <returns>The suggested expiry in UTC; null = perpetual.</returns>
    public static DateTimeOffset? Suggest(IReadOnlyList<IssueEntry> entries, bool isRenewal)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("A license has at least one issue entry.", nameof(entries));
        }

        var current = entries[^1].ExpiresUtc;
        if (current is null || !isRenewal)
        {
            return current;
        }

        var (start, end) = LastTerm(entries);

        // No usable term (the last renewal made it perpetual): nothing to add. The vendor types the expiry.
        if (end is null)
        {
            return current;
        }

        // Measured on UTC dates; the current expiry keeps its time of day. A term that starts and ends on the same
        // date (or runs backwards) has no length, so the suggestion stays at the current expiry.
        var startDate = DateOnly.FromDateTime(start.UtcDateTime);
        var endDate = DateOnly.FromDateTime(end.Value.UtcDateTime);
        if (endDate <= startDate)
        {
            return current;
        }

        return WholeMonths(startDate, endDate) is { } months
            ? AddMonths(current.Value, months)
            : current.Value.AddDays(endDate.DayNumber - startDate.DayNumber);
    }

    /// <summary>The length the last renewal granted, or the first issue's length when never renewed.</summary>
    private static (DateTimeOffset Start, DateTimeOffset? End) LastTerm(IReadOnlyList<IssueEntry> entries)
    {
        var renewal = -1;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Kind == IssueKind.Renewal)
            {
                renewal = i;
                break;
            }
        }

        if (renewal < 0)
        {
            return (entries[0].IssuedUtc, entries[0].ExpiresUtc);
        }

        var r = entries[renewal];

        // A renewal is never the first entry. A previous entry that was perpetual (null expiry) has no expiry to
        // run on from, so the term starts at the renewal's issue time.
        var before = renewal > 0 ? entries[renewal - 1].ExpiresUtc : null;
        // The later of the previous expiry and the renewal's issue time.
        var start = before is { } p && p > r.IssuedUtc ? p : r.IssuedUtc;
        return (start, r.ExpiresUtc);
    }

    /// <summary>
    /// The number of calendar months from <paramref name="start"/> to <paramref name="end"/> when it is a whole
    /// number (same day of month, a day past the end of a month counting as its last day); otherwise null.
    /// </summary>
    private static int? WholeMonths(DateOnly start, DateOnly end)
    {
        var sameDay = Math.Min(start.Day, DateTime.DaysInMonth(end.Year, end.Month)) == end.Day;
        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
        return sameDay && months > 0 ? months : null;
    }

    /// <summary>Adds calendar months, clamping to the end of a shorter month (31 Jan + 1 month = 28 Feb).</summary>
    private static DateTimeOffset AddMonths(DateTimeOffset value, int months) =>
        new(value.UtcDateTime.AddMonths(months), TimeSpan.Zero);
}
