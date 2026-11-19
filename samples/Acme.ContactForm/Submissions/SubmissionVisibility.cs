using Acme.ContactForm.Licensing;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Submissions;

/// <summary>
/// Which of this month's submissions the license covers. Pure: it reads the snapshot and a count, and changes nothing.
/// The domain result is not an input.
/// </summary>
public static class SubmissionVisibility
{
    /// <summary>
    /// One flag per submission in arrival order, true when hidden. Not Valid hides every one. Valid with a monthly limit of N
    /// hides the (N+1)th onwards. Otherwise none.
    /// </summary>
    public static IReadOnlyList<bool> HiddenFlags(LicenseSnapshot snapshot, int count)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var shown = snapshot.State != LicenseState.Valid
            ? 0
            : snapshot.MonthlyLimit is { } limit ? Math.Max(limit, 0) : int.MaxValue;

        return [.. Enumerable.Range(0, count).Select(i => i >= shown)];
    }
}
