namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>A clock that moves only when told to and fires the timers made with <see cref="CreateTimer"/> (ADR-0021).</summary>
internal sealed class FakeTimeProvider(DateTimeOffset? start = null) : TimeProvider
{
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = start ?? new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Moves the clock on, firing each due timer in due order (a periodic one as often as it falls due).</summary>
    public void Advance(TimeSpan by)
    {
        var target = _now + by;
        while (true)
        {
            var next = _timers.Where(t => t.Due is not null && t.Due <= target).MinBy(t => t.Due);
            if (next is null)
                break;
            _now = next.Due!.Value;
            next.Fire();
        }

        _now = target;
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            return true;
        }

        public void Fire()
        {
            Due = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : Due + _period;
            callback(state);
        }

        public void Dispose()
        {
            Due = null;
            owner._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
