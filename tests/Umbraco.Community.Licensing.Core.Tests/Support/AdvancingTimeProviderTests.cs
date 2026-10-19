namespace Umbraco.Community.Licensing.Core.Tests.Support;

public class AdvancingTimeProviderTests
{
    [Fact]
    public void GetUtcNow_EachCall_ReturnsTheCurrentInstantThenAdvancesAndCounts()
    {
        var start = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(2));
        var provider = new AdvancingTimeProvider(start, TimeSpan.FromSeconds(1));

        var first = provider.GetUtcNow();
        var second = provider.GetUtcNow();

        Assert.Equal(start, first);
        Assert.Equal(TimeSpan.Zero, first.Offset);
        Assert.Equal(start.AddSeconds(1), second);
        Assert.Equal(2, provider.Reads);
        Assert.Equal(TimeZoneInfo.Utc, provider.LocalTimeZone);
    }
}
