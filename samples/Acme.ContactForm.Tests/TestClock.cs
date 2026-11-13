namespace Acme.ContactForm.Tests;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public void Set(DateTimeOffset now) => _now = now;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}
