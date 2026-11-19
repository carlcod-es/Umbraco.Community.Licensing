using Acme.ContactForm.Submissions;

namespace Acme.ContactForm.Tests;

public class SubmissionStoreTests
{
    private static DateTimeOffset Utc(int y, int m, int d, int h = 0, int min = 0, int s = 0) =>
        new(y, m, d, h, min, s, TimeSpan.Zero);

    [Fact]
    public void New_month()
    {
        var clock = new TestClock(Utc(2026, 3, 10, 12));
        var store = new InMemorySubmissionStore(clock);
        store.Add("A", "a@example.com", "one");
        store.Add("B", "b@example.com", "two");
        Assert.Equal(2, store.CountThisMonth());
        Assert.Equal(2, store.ListThisMonth().Count);

        clock.Set(Utc(2026, 4, 1));

        Assert.Equal(0, store.CountThisMonth());
        Assert.Empty(store.ListThisMonth());
    }

    [Fact]
    public void New_month_at_the_UTC_boundary()
    {
        var clock = new TestClock(Utc(2026, 3, 31, 23, 59, 59));
        var store = new InMemorySubmissionStore(clock);
        store.Add("A", "a@example.com", "last second");
        Assert.Equal(1, store.CountThisMonth());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(Utc(2026, 4, 1), clock.GetUtcNow());
        Assert.Equal(0, store.CountThisMonth());

        store.Add("B", "b@example.com", "first second");
        Assert.Equal(1, store.CountThisMonth());
        Assert.Equal("B", Assert.Single(store.ListThisMonth()).Name);
    }

    [Fact]
    public void Same_month_in_a_different_year_is_not_this_month()
    {
        var clock = new TestClock(Utc(2025, 3, 5));
        var store = new InMemorySubmissionStore(clock);
        store.Add("A", "a@example.com", "old");
        clock.Set(Utc(2026, 3, 5));
        Assert.Equal(0, store.CountThisMonth());
    }

    [Fact]
    public void Upload_kept_as_name_and_size()
    {
        var store = new InMemorySubmissionStore(new TestClock(Utc(2026, 3, 10)));
        store.Add("A", "a@example.com", "with", "report.pdf", 2048);
        store.Add("B", "b@example.com", "without");

        var list = store.ListThisMonth();
        Assert.Equal("report.pdf", list[0].UploadFileName);
        Assert.Equal(2048, list[0].UploadSizeBytes);
        Assert.Null(list[1].UploadFileName);
        Assert.Null(list[1].UploadSizeBytes);
        Assert.Equal(Utc(2026, 3, 10), list[0].ReceivedUtc);
    }
}
