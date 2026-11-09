using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>The expiry proposed as the answer to the expiry question when a license is reissued.</summary>
internal static class ExpirySuggestion
{
    /// <summary>
    /// Renewal of a time-limited license: the current expiry plus the last term (calendar months when the
    /// term was a whole number of months, exact time otherwise). Perpetual stays perpetual; any other
    /// reissue keeps the current expiry. Null is perpetual.
    /// </summary>
    public static DateTime? Suggest(IReadOnlyCollection<IssueEntry> entries, DateTime? currentExpiryUtc, IssueKind reissueKind)
    {
        if (currentExpiryUtc is not { } current)
        {
            return null;
        }

        if (reissueKind != IssueKind.Renewal)
        {
            return current;
        }

        // Issue time, then Id: Ids alone are not time order if the clock or an import disagrees.
        var ordered = entries.OrderBy(e => e.IssuedUtc).ThenBy(e => e.Id).ToList();
        if (ordered.Count == 0)
        {
            return current;
        }

        var lastRenewal = ordered.FindLastIndex(e => e.Kind == IssueKind.Renewal);
        DateTime start;
        DateTime? end;
        if (lastRenewal < 0)
        {
            start = ordered[0].IssuedUtc;
            end = ordered[0].ExpiresUtc;
        }
        else
        {
            var renewal = ordered[lastRenewal];
            start = renewal.IssuedUtc;
            if (lastRenewal > 0 && ordered[lastRenewal - 1].ExpiresUtc is { } previousExpiry && previousExpiry > start)
            {
                start = previousExpiry;
            }

            end = renewal.ExpiresUtc;
        }

        if (end is not { } termEnd || termEnd <= start)
        {
            return current;
        }

        return WholeMonths(start, termEnd) is { } months
            ? current.AddMonths(months)
            : current + (termEnd - start);
    }

    /// <summary>The month count when both share day of month and time of day, a day past a month's end counting as its last day.</summary>
    private static int? WholeMonths(DateTime start, DateTime end)
    {
        if (start.TimeOfDay != end.TimeOfDay)
        {
            return null;
        }

        var sameDay = start.Day == end.Day
            || Math.Min(start.Day, DateTime.DaysInMonth(end.Year, end.Month)) == end.Day;
        return sameDay ? ((end.Year - start.Year) * 12) + end.Month - start.Month : null;
    }
}
