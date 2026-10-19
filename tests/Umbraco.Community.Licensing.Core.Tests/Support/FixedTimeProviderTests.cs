namespace Umbraco.Community.Licensing.Core.Tests.Support;

public class FixedTimeProviderTests
{
    [Fact]
    public void GetUtcNow_AfterConstruction_ReturnsSetInstantAsUtc()
    {
        var instant = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.FromHours(2));
        var provider = new FixedTimeProvider(instant);

        var now = provider.GetUtcNow();

        Assert.Equal(instant, now);
        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.Equal(TimeZoneInfo.Utc, provider.LocalTimeZone);
    }

    [Fact]
    public void GetUtcNow_AfterSetUtcNow_ReturnsNewInstant()
    {
        var provider = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var next = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        provider.SetUtcNow(next);

        Assert.Equal(next, provider.GetUtcNow());
    }
}
