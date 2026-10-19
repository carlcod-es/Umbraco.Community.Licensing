namespace Umbraco.Community.Licensing.Core.Tests.Support;

/// <summary>A <see cref="TimeProvider"/> that moves forward by a step on every <see cref="GetUtcNow"/> call and counts the calls.</summary>
public sealed class AdvancingTimeProvider(DateTimeOffset start, TimeSpan step) : TimeProvider
{
    private DateTimeOffset _next = start.ToUniversalTime();

    public int Reads { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        Reads++;
        var current = _next;
        _next += step;
        return current;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
