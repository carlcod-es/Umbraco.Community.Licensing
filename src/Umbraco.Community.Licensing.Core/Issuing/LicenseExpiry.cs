namespace Umbraco.Community.Licensing.Core;

/// <summary>How a request states when a license expires. A request with no expiry (null) is rejected at issue; perpetual must be stated.</summary>
public abstract record LicenseExpiry
{
    private LicenseExpiry()
    {
    }

    /// <summary>Perpetual: the license does not expire.</summary>
    public static LicenseExpiry Perpetual { get; } = new PerpetualExpiry();

    /// <summary>Expires at 23:59:59 UTC on <paramref name="date"/>.</summary>
    /// <param name="date">The last day the license is valid.</param>
    /// <returns>The expiry.</returns>
    public static LicenseExpiry On(DateOnly date) => new DateExpiry(date);

    /// <summary>Expires at <paramref name="instant"/>, kept as given. A fraction of a second is rejected at issue.</summary>
    /// <param name="instant">The expiry instant.</param>
    /// <returns>The expiry.</returns>
    public static LicenseExpiry At(DateTimeOffset instant) => new InstantExpiry(instant);

    /// <summary>Maps a request's expiry to the statement the format code resolves; null (not stated) stays not stated.</summary>
    internal static ExpiryStatement ToStatement(LicenseExpiry? expiry) => expiry switch
    {
        null => ExpiryStatement.NotStated,
        DateExpiry d => ExpiryStatement.OnDate(d.Date),
        InstantExpiry i => ExpiryStatement.AtInstant(i.Instant),
        _ => ExpiryStatement.Perpetual,
    };

    private sealed record PerpetualExpiry : LicenseExpiry;

    private sealed record DateExpiry(DateOnly Date) : LicenseExpiry;

    private sealed record InstantExpiry(DateTimeOffset Instant) : LicenseExpiry;
}
