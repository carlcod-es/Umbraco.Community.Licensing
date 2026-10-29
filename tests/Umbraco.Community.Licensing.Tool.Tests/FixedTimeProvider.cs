namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>A clock that reads a fixed instant until moved.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
