using System.Security.Cryptography;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>Receives the held passphrase's ASCII bytes for the duration of the call; they must not be copied out or kept.</summary>
internal delegate void PassphraseUse(ReadOnlySpan<byte> passphrase);

/// <summary>
/// Holds the session passphrase as pinned ASCII bytes, never a string, and forgets it when the idle
/// time passes (ADR-0021, ADR-0022). The idle time is a <see cref="TimeSpan"/>, or null for
/// <c>never</c> (no timer, no comparison). Every completed answer, a menu choice included, calls
/// <see cref="Touch"/>, which records the time and restarts a <see cref="TimeProvider"/> timer. When
/// the timer fires the bytes are zeroed and dropped; every use also compares the time since the last
/// answer with the idle time and locks first when it has passed, so a late timer never hands out the
/// passphrase. The timer runs on another thread, so everything touching the bytes is behind one lock.
/// </summary>
internal sealed class SessionLock(TimeProvider time) : IDisposable
{
    private readonly object _gate = new();
    private ITimer? _timer;
    private byte[]? _held;
    private TimeSpan? _idleTime;
    private DateTimeOffset _lastAnswer = time.GetUtcNow();
    private bool _disposed;

    /// <summary>The idle time in force; null is <c>never</c>.</summary>
    public TimeSpan? IdleTime
    {
        get { lock (_gate) return _idleTime; }
    }

    /// <summary>
    /// Sets the idle time of the installation the session now uses (when the session opens, and when
    /// it moves to another installation), counting from now. Null is <c>never</c>: any timer stops.
    /// </summary>
    public void SetIdleTime(TimeSpan? idleTime)
    {
        if (idleTime is { } t && t <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idleTime), "The idle time must be positive, or null for never.");

        lock (_gate)
        {
            _idleTime = idleTime;
            _lastAnswer = time.GetUtcNow();
            Rearm();
        }
    }

    /// <summary>
    /// Takes ownership of the passphrase's pinned ASCII bytes (from <see cref="Questions.AskHidden"/>
    /// or setup) and zeroes any passphrase held before. The array is the one held and later zeroed;
    /// it is not copied. The idle time counts from now.
    /// </summary>
    public void Hold(byte[] passphrase)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        lock (_gate)
        {
            if (!ReferenceEquals(_held, passphrase))
                ZeroHeld();
            if (_disposed)
            {
                CryptographicOperations.ZeroMemory(passphrase);
                return;
            }

            _held = passphrase;
            _lastAnswer = time.GetUtcNow();
            Rearm();
        }
    }

    /// <summary>True when a passphrase is held and the idle time has not passed (locks first when it has).</summary>
    public bool IsHeld
    {
        get
        {
            lock (_gate)
            {
                LockIfIdle();
                return _held is not null;
            }
        }
    }

    /// <summary>
    /// Passes the held bytes to <paramref name="use"/> without copying them to a string, under the lock
    /// so the timer cannot zero them mid-use. Returns false, calling nothing, when none is held or the
    /// idle time has passed; the caller then asks for the passphrase.
    /// </summary>
    public bool TryUse(PassphraseUse use)
    {
        ArgumentNullException.ThrowIfNull(use);
        lock (_gate)
        {
            LockIfIdle();
            if (_held is null)
                return false;
            use(_held);
            return true;
        }
    }

    /// <summary>A completed answer: records its time and restarts the idle timer.</summary>
    public void Touch()
    {
        lock (_gate)
        {
            // An answer after the idle time, before the timer ran, finds the lock already due.
            LockIfIdle();
            _lastAnswer = time.GetUtcNow();
            Rearm();
        }
    }

    /// <summary>Zeroes and drops the held passphrase (Quit, end of input, a move to a restored installation). The lock stays usable.</summary>
    public void Release()
    {
        lock (_gate)
        {
            ZeroHeld();
            Rearm();
        }
    }

    /// <summary>Zeroes and drops the held passphrase and disposes the timer; the lock then holds nothing for good.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            ZeroHeld();
            _timer?.Dispose();
            _timer = null;
        }
    }

    // The timer runs only while a passphrase is held and the idle time is not never.
    private void Rearm()
    {
        if (_disposed)
            return;
        if (_held is null || _idleTime is null)
        {
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        _timer ??= time.CreateTimer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(_idleTime.Value, Timeout.InfiniteTimeSpan);
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            if (_disposed || _held is null || _idleTime is not { } idle)
                return;

            var remaining = idle - (time.GetUtcNow() - _lastAnswer);
            if (remaining <= TimeSpan.Zero)
                ZeroHeld();
            else
                _timer?.Change(remaining, Timeout.InfiniteTimeSpan); // a stale fire: an answer came since it was set
        }
    }

    private void LockIfIdle()
    {
        if (_held is not null && _idleTime is { } idle && time.GetUtcNow() - _lastAnswer >= idle)
            ZeroHeld();
    }

    private void ZeroHeld()
    {
        if (_held is null)
            return;
        CryptographicOperations.ZeroMemory(_held);
        _held = null;
    }
}
