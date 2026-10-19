namespace Umbraco.Community.Licensing.Core;

/// <summary>How a request states its expiry. Task 5.1's <c>LicenseExpiry</c> maps onto this.</summary>
internal readonly record struct ExpiryStatement
{
    private ExpiryStatement(ExpiryKind kind, DateOnly date, DateTimeOffset instant)
    {
        Kind = kind;
        Date = date;
        Instant = instant;
    }

    public ExpiryKind Kind { get; }

    public DateOnly Date { get; }

    public DateTimeOffset Instant { get; }

    /// <summary>Nothing was stated; the default value.</summary>
    public static ExpiryStatement NotStated => default;

    public static ExpiryStatement Perpetual => new(ExpiryKind.Perpetual, default, default);

    public static ExpiryStatement OnDate(DateOnly date) => new(ExpiryKind.Date, date, default);

    public static ExpiryStatement AtInstant(DateTimeOffset instant) => new(ExpiryKind.Instant, default, instant);
}

internal enum ExpiryKind
{
    NotStated = 0,
    Date,
    Instant,
    Perpetual,
}

/// <summary>Resolves a stated expiry to the UTC second it is signed as (ADR-0002), against the current instant.</summary>
internal static class ExpiryRule
{
    private const string Field = "expires";

    /// <summary>
    /// A date alone is 23:59:59 UTC that day; a date and time keeps its instant in UTC; a fraction of a
    /// second or an instant earlier than the current second is a problem. Equal to the current second is accepted.
    /// </summary>
    /// <param name="statement">The stated expiry.</param>
    /// <param name="now">The current instant, read once by the caller; truncated to the second here.</param>
    /// <param name="problems">Receives the problem, naming <c>expires</c>.</param>
    /// <param name="expires">The UTC instant, or null for perpetual (also null when a problem was added).</param>
    /// <returns>True when no problem was added.</returns>
    public static bool TryResolve(ExpiryStatement statement, DateTimeOffset now, List<LicenseProblem> problems, out DateTimeOffset? expires)
    {
        expires = null;

        switch (statement.Kind)
        {
            case ExpiryKind.Perpetual:
                return true;

            case ExpiryKind.Date:
                var endOfDay = new DateTimeOffset(statement.Date.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc));
                return Accept(endOfDay, now, problems, out expires);

            case ExpiryKind.Instant:
                var instant = statement.Instant.ToUniversalTime();
                if (instant.Ticks % TimeSpan.TicksPerSecond != 0)
                {
                    problems.Add(new LicenseProblem(Field, "The expiry must be to the second, with no fraction of a second."));
                    return false;
                }

                return Accept(instant, now, problems, out expires);

            default:
                problems.Add(new LicenseProblem(Field, "An expiry must be stated: a date, a UTC date and time, or perpetual."));
                return false;
        }
    }

    private static bool Accept(DateTimeOffset instant, DateTimeOffset now, List<LicenseProblem> problems, out DateTimeOffset? expires)
    {
        var currentSecond = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);

        if (instant < currentSecond)
        {
            expires = null;
            problems.Add(new LicenseProblem(Field, "The expiry is earlier than the current time."));
            return false;
        }

        expires = instant;
        return true;
    }
}
