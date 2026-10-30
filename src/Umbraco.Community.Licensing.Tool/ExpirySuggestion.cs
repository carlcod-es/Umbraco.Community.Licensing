using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>The expiry suggested when a license is reissued: the current expiry plus the length of the last term.</summary>
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

        // No usable term (the last renewal made it perpetual, or it ran backwards): nothing to add. The vendor types the expiry.
        if (end is null || end <= start)
        {
            return current;
        }

        return WholeMonths(start, end.Value) is { } months
            ? AddMonths(current.Value, months)
            : current.Value + (end.Value - start);
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
        var start = before is { } p && p > r.IssuedUtc ? p : r.IssuedUtc;
        return (start, r.ExpiresUtc);
    }

    /// <summary>
    /// The number of calendar months from <paramref name="start"/> to <paramref name="end"/> when it is a whole
    /// number (same time of day, same day of month, a day past the end of a month counting as its last day);
    /// otherwise null. Compared in UTC.
    /// </summary>
    private static int? WholeMonths(DateTimeOffset start, DateTimeOffset end)
    {
        var s = start.UtcDateTime;
        var e = end.UtcDateTime;
        if (s.TimeOfDay != e.TimeOfDay)
        {
            return null;
        }

        var sameDay = Math.Min(s.Day, DateTime.DaysInMonth(e.Year, e.Month)) == e.Day;
        var months = ((e.Year - s.Year) * 12) + e.Month - s.Month;
        return sameDay && months > 0 ? months : null;
    }

    /// <summary>Adds calendar months, clamping to the end of a shorter month (31 Jan + 1 month = 28 Feb).</summary>
    private static DateTimeOffset AddMonths(DateTimeOffset value, int months) =>
        new(value.UtcDateTime.AddMonths(months), TimeSpan.Zero);
}
