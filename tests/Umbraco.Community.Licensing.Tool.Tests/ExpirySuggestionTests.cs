using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class ExpirySuggestionTests
{
    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, null, System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();

    private static IssueEntry Entry(IssueKind kind, string issued, string? expires) => new()
    {
        Reference = "LIC-TEST",
        Kind = kind,
        IssuedUtc = At(issued),
        ExpiresUtc = expires is null ? null : At(expires),
        FeaturesJson = "{}",
        KeyIdentifier = "k",
        SigningKeyId = "s",
    };

    private static DateTimeOffset? Renew(params IssueEntry[] entries) => ExpirySuggestion.Suggest(entries, isRenewal: true);

    [Fact]
    public void Renewal_of_a_one_year_license()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z"));

        Assert.Equal(At("2027-11-30T23:59:59Z"), suggested);
    }

    [Fact]
    public void Second_renewal_after_an_early_renewal()
    {
        var suggested = Renew(
            Entry(IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z"),
            Entry(IssueKind.Renewal, "2026-11-20T00:00:00Z", "2027-11-30T23:59:59Z"));

        Assert.Equal(At("2028-11-30T23:59:59Z"), suggested);
    }

    [Fact]
    public void Add_on_before_renewal()
    {
        var suggested = Renew(
            Entry(IssueKind.Issue, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z"),
            Entry(IssueKind.Reissue, "2026-09-01T00:00:00Z", "2027-01-15T10:00:00Z"));

        Assert.Equal(At("2028-01-15T10:00:00Z"), suggested);
    }

    [Fact]
    public void Renewal_after_a_lapse()
    {
        var suggested = Renew(
            Entry(IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z"),
            Entry(IssueKind.Renewal, "2027-02-01T09:00:00Z", "2028-02-01T09:00:00Z"));

        Assert.Equal(At("2029-02-01T09:00:00Z"), suggested);
    }

    [Fact]
    public void Feature_change()
    {
        var suggested = ExpirySuggestion.Suggest(
            [Entry(IssueKind.Issue, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z")],
            isRenewal: false);

        Assert.Equal(At("2026-11-30T23:59:59Z"), suggested);
    }

    [Fact]
    public void Perpetual_license()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2026-01-15T10:00:00Z", null));

        Assert.Null(suggested);
    }

    [Fact]
    public void Never_reissued()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2026-01-15T10:00:00Z", "2026-07-15T10:00:00Z"));

        Assert.Equal(At("2027-01-15T10:00:00Z"), suggested);
    }

    [Fact]
    public void Term_not_a_whole_number_of_months()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2026-03-10T09:00:00Z", "2026-04-09T09:00:00Z"));

        Assert.Equal(At("2026-05-09T09:00:00Z"), suggested);
    }

    [Fact]
    public void Date_expiry_issued_during_the_day()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2025-11-30T14:23:11Z", "2026-11-30T23:59:59Z"));

        Assert.Equal(At("2027-11-30T23:59:59Z"), suggested);
    }

    [Fact]
    public void Term_in_whole_days()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2026-03-10T14:00:00Z", "2026-04-09T23:59:59Z"));

        Assert.Equal(At("2026-05-09T23:59:59Z"), suggested);
    }

    [Fact]
    public void Term_within_one_day()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2026-03-10T09:00:00Z", "2026-03-10T11:00:00Z"));

        Assert.Equal(At("2026-03-10T11:00:00Z"), suggested);
    }

    [Fact]
    public void Term_ending_before_it_starts_suggests_the_current_expiry()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2026-06-10T09:00:00Z", "2026-03-10T09:00:00Z"));

        Assert.Equal(At("2026-03-10T09:00:00Z"), suggested);
    }

    [Fact]
    public void Resend_does_not_change_the_term()
    {
        var suggested = Renew(
            Entry(IssueKind.Issue, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z"),
            Entry(IssueKind.Resend, "2026-03-01T00:00:00Z", "2027-01-15T10:00:00Z"));

        Assert.Equal(At("2028-01-15T10:00:00Z"), suggested);
    }

    [Fact]
    public void Renewal_after_a_perpetual_entry_starts_at_the_renewals_issue_time()
    {
        // Perpetual, then renewed to a time-limited expiry: term = issue time to expiry (one year), not unbounded.
        var suggested = Renew(
            Entry(IssueKind.Issue, "2025-01-01T00:00:00Z", null),
            Entry(IssueKind.Renewal, "2026-03-05T08:00:00Z", "2027-03-05T08:00:00Z"));

        Assert.Equal(At("2028-03-05T08:00:00Z"), suggested);
    }

    [Fact]
    public void Time_limited_license_whose_last_renewal_was_perpetual_suggests_the_current_expiry()
    {
        var suggested = Renew(
            Entry(IssueKind.Issue, "2025-01-01T00:00:00Z", "2026-01-01T00:00:00Z"),
            Entry(IssueKind.Renewal, "2026-01-01T00:00:00Z", null),
            Entry(IssueKind.Reissue, "2026-02-01T00:00:00Z", "2027-01-01T00:00:00Z"));

        Assert.Equal(At("2027-01-01T00:00:00Z"), suggested);
    }

    [Fact]
    public void Month_end_term_counts_as_one_whole_month()
    {
        // 2026-01-31 to 2026-02-28 is one calendar month (31 clamps to 28), so one more month is added.
        var suggested = Renew(Entry(IssueKind.Issue, "2026-01-31T12:00:00Z", "2026-02-28T12:00:00Z"));

        Assert.Equal(At("2026-03-28T12:00:00Z"), suggested);
    }

    [Fact]
    public void Month_end_renewal_clamps_to_the_shorter_month()
    {
        // One-month term, current expiry 2026-01-31: 31 Jan + 1 month clamps to 28 Feb.
        var suggested = Renew(
            Entry(IssueKind.Issue, "2025-12-31T12:00:00Z", "2026-01-31T12:00:00Z"));

        Assert.Equal(At("2026-02-28T12:00:00Z"), suggested);
    }

    [Fact]
    public void Month_end_days_clamp_only_when_the_start_day_is_past_the_end_month()
    {
        // 2026-01-30 to 2026-02-28: day 30 clamps to 28 in February, so it is one month.
        Assert.Equal(At("2026-03-28T00:00:00Z"), Renew(Entry(IssueKind.Issue, "2026-01-30T00:00:00Z", "2026-02-28T00:00:00Z")));

        // 2026-02-28 to 2026-03-31: day 28 does not reach 31; exact time (31 days) is added.
        Assert.Equal(At("2026-05-01T00:00:00Z"), Renew(Entry(IssueKind.Issue, "2026-02-28T00:00:00Z", "2026-03-31T00:00:00Z")));
    }

    [Fact]
    public void Leap_day_term_of_a_year()
    {
        // 2024-02-29 to 2025-02-28 is twelve months; added to 2025-02-28 it gives 2026-02-28.
        var suggested = Renew(Entry(IssueKind.Issue, "2024-02-29T00:00:00Z", "2025-02-28T00:00:00Z"));

        Assert.Equal(At("2026-02-28T00:00:00Z"), suggested);
    }

    [Fact]
    public void Renewal_landing_in_February_of_a_leap_year()
    {
        // Six-month term; current expiry 2023-08-31 plus six months clamps to 2024-02-29 (leap day).
        var suggested = Renew(
            Entry(IssueKind.Issue, "2023-02-28T00:00:00Z", "2023-08-28T00:00:00Z"),
            Entry(IssueKind.Reissue, "2023-03-01T00:00:00Z", "2023-08-31T00:00:00Z"));

        Assert.Equal(At("2024-02-29T00:00:00Z"), suggested);
    }

    [Fact]
    public void One_year_term_ending_on_a_non_leap_february_28_renews_to_the_leap_year_28()
    {
        var suggested = Renew(Entry(IssueKind.Issue, "2026-02-28T00:00:00Z", "2027-02-28T00:00:00Z"));

        Assert.Equal(At("2028-02-28T00:00:00Z"), suggested);
    }
}
