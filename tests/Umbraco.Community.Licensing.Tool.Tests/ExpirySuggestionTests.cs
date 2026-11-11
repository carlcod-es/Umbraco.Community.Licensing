using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class ExpirySuggestionTests
{
    private static DateTime Utc(string iso) =>
        DateTime.SpecifyKind(DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);

    private static IssueEntry Entry(long id, IssueKind kind, string issued, string? expires) => new()
    {
        Id = id,
        Reference = "LIC-AAAAA-BBBBB",
        Kind = kind,
        IssuedUtc = Utc(issued),
        ExpiresUtc = expires is null ? null : Utc(expires),
        FeaturesJson = "[]",
        KeyIdentifier = "LIC",
        SigningKeyId = "k1",
    };

    private static DateTime? Renew(DateTime? current, params IssueEntry[] entries) =>
        ExpirySuggestion.Suggest(entries, current, IssueKind.Renewal);

    [Fact]
    public void RenewalOfAOneYearLicense()
    {
        var first = Entry(1, IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        Assert.Equal(Utc("2027-11-30T23:59:59Z"), Renew(first.ExpiresUtc, first));
    }

    [Fact]
    public void SecondRenewalAfterAnEarlyRenewal()
    {
        var first = Entry(1, IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        var renewal = Entry(2, IssueKind.Renewal, "2026-11-20T08:00:00Z", "2027-11-30T23:59:59Z");
        Assert.Equal(Utc("2028-11-30T23:59:59Z"), Renew(renewal.ExpiresUtc, first, renewal));
    }

    [Fact]
    public void AddOnBeforeRenewal()
    {
        var first = Entry(1, IssueKind.Issue, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z");
        var addOn = Entry(2, IssueKind.Reissue, "2026-09-01T10:00:00Z", "2027-01-15T10:00:00Z");
        Assert.Equal(Utc("2028-01-15T10:00:00Z"), Renew(addOn.ExpiresUtc, first, addOn));
    }

    [Fact]
    public void RenewalAfterALapse()
    {
        var first = Entry(1, IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        var renewal = Entry(2, IssueKind.Renewal, "2027-02-01T09:00:00Z", "2028-02-01T09:00:00Z");
        Assert.Equal(Utc("2029-02-01T09:00:00Z"), Renew(renewal.ExpiresUtc, first, renewal));
    }

    [Fact]
    public void FeatureChange()
    {
        var first = Entry(1, IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        Assert.Equal(first.ExpiresUtc, ExpirySuggestion.Suggest([first], first.ExpiresUtc, IssueKind.Reissue));
    }

    [Fact]
    public void PerpetualLicense()
    {
        var first = Entry(1, IssueKind.Issue, "2026-01-15T10:00:00Z", null);
        Assert.Null(Renew(null, first));
    }

    [Fact]
    public void NeverReissued()
    {
        var first = Entry(1, IssueKind.Issue, "2026-01-15T10:00:00Z", "2026-07-15T10:00:00Z");
        Assert.Equal(Utc("2027-01-15T10:00:00Z"), Renew(first.ExpiresUtc, first));
    }

    [Fact]
    public void TermNotAWholeNumberOfMonths()
    {
        var first = Entry(1, IssueKind.Issue, "2026-03-10T09:00:00Z", "2026-04-09T09:00:00Z");
        Assert.Equal(Utc("2026-05-09T09:00:00Z"), Renew(first.ExpiresUtc, first));
    }

    [Fact]
    public void TermWithDifferentTimeOfDay_UsesExactTime()
    {
        var first = Entry(1, IssueKind.Issue, "2026-03-10T09:00:00Z", "2027-03-10T10:00:00Z");
        Assert.Equal(Utc("2028-03-09T11:00:00Z"), Renew(first.ExpiresUtc, first));
    }

    [Fact]
    public void MonthEnd_Jan31ToFeb28_IsOneMonth_ClampedToMonthEnd()
    {
        var first = Entry(1, IssueKind.Issue, "2026-01-31T12:00:00Z", "2026-02-28T12:00:00Z");
        Assert.Equal(Utc("2026-03-28T12:00:00Z"), Renew(first.ExpiresUtc, first));
        Assert.Equal(Utc("2026-04-30T12:00:00Z"), Renew(Utc("2026-03-31T12:00:00Z"), first));
    }

    [Fact]
    public void LeapDay_Feb29ToFeb28_IsOneYear()
    {
        var first = Entry(1, IssueKind.Issue, "2024-02-29T00:00:00Z", "2025-02-28T00:00:00Z");
        Assert.Equal(Utc("2026-02-28T00:00:00Z"), Renew(first.ExpiresUtc, first));
    }

    [Fact]
    public void LeapDay_CurrentExpiryOnFeb29_AddsYearClamped()
    {
        var first = Entry(1, IssueKind.Issue, "2024-02-29T00:00:00Z", "2025-02-28T00:00:00Z");
        Assert.Equal(Utc("2029-02-28T00:00:00Z"), Renew(Utc("2028-02-29T00:00:00Z"), first));
    }

    [Fact]
    public void Feb28ToLeapDay_IsNotWholeMonths_UsesExactTime()
    {
        var first = Entry(1, IssueKind.Issue, "2023-02-28T00:00:00Z", "2024-02-29T00:00:00Z");
        Assert.Equal(Utc("2025-03-01T00:00:00Z"), Renew(first.ExpiresUtc, first));
    }

    [Fact]
    public void Feb28ToMar31_IsNotOneMonth_UsesExactTime()
    {
        var first = Entry(1, IssueKind.Issue, "2026-02-28T12:00:00Z", "2026-03-31T12:00:00Z");
        Assert.Equal(Utc("2026-05-01T12:00:00Z"), Renew(first.ExpiresUtc, first));
    }

    [Fact]
    public void NoEntries_KeepsCurrentExpiry()
    {
        var current = Utc("2026-07-15T10:00:00Z");
        Assert.Equal(current, Renew(current));
    }

    [Fact]
    public void TermWithoutEnd_KeepsCurrentExpiry()
    {
        var first = Entry(1, IssueKind.Issue, "2026-01-15T10:00:00Z", null);
        var current = Utc("2026-07-15T10:00:00Z");
        Assert.Equal(current, Renew(current, first));
    }

    [Fact]
    public void TermEndingBeforeItsStart_KeepsCurrentExpiry()
    {
        var first = Entry(1, IssueKind.Issue, "2026-01-15T10:00:00Z", "2026-01-15T10:00:00Z");
        Assert.Equal(first.ExpiresUtc, Renew(first.ExpiresUtc, first));
        var backwards = Entry(2, IssueKind.Issue, "2026-03-15T10:00:00Z", "2026-02-15T10:00:00Z");
        Assert.Equal(backwards.ExpiresUtc, Renew(backwards.ExpiresUtc, backwards));
    }

    [Fact]
    public void RenewalIsTheFirstEntry_TermRunsFromItsIssue()
    {
        var renewal = Entry(1, IssueKind.Renewal, "2026-01-15T10:00:00Z", "2026-07-15T10:00:00Z");
        Assert.Equal(Utc("2027-01-15T10:00:00Z"), Renew(renewal.ExpiresUtc, renewal));
    }

    [Fact]
    public void EntriesAreOrderedByIssueTime_NotById()
    {
        var first = Entry(5, IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        var renewal = Entry(2, IssueKind.Renewal, "2026-11-20T08:00:00Z", "2027-11-30T23:59:59Z");
        Assert.Equal(Utc("2028-11-30T23:59:59Z"), Renew(renewal.ExpiresUtc, renewal, first));
    }
}
