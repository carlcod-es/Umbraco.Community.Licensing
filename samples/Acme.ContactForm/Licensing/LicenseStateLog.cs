using System.Globalization;
using Microsoft.Extensions.Logging;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Licensing;

/// <summary>
/// Writes one line each time the license condition changes, so an operator can see when a key expires, nears expiry or is
/// replaced without a line on every request. The condition is the state, plus expiring soon when the state is Valid.
/// Valid logs at Information, Valid and expiring soon at Warning (with the expiry date), any other state at Error.
/// Logs the state and the key identifier only, never the key. Register as a singleton.
/// </summary>
public sealed class LicenseStateLog(ILogger<LicenseStateLog> logger)
{
    private readonly Lock _gate = new();
    private (LicenseState State, bool ExpiringSoon)? _last;

    /// <summary>Compares the snapshot's condition with the last one logged and logs when it differs, including the first report.</summary>
    public void Report(LicenseSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var result = snapshot.Result;
        var expiringSoon = result.State == LicenseState.Valid && snapshot.ExpiringSoon;
        var condition = (result.State, expiringSoon);

        // The identifier is the verified one, else the one the key claims, else none. Never the key text.
        var identifier = result.License is { } license
            ? license.KeyIdentifier
            : result.ClaimedKeyIdentifier is { } claimed ? "claimed " + claimed : "none";

        // Logged inside the lock so two threads reporting the same change write once, and in order.
        lock (_gate)
        {
            if (_last == condition)
            {
                return;
            }

            _last = condition;

            if (result.State != LicenseState.Valid)
            {
                logger.LogError("The Acme Contact Form license state is now {LicenseState} (key {KeyIdentifier}).", result.State, identifier);
            }
            else if (expiringSoon)
            {
                var expires = snapshot.ExpiresUtc?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "unknown";
                logger.LogWarning("The Acme Contact Form license state is {LicenseState} but expires soon, on {ExpiryDate} (key {KeyIdentifier}).", result.State, expires, identifier);
            }
            else
            {
                logger.LogInformation("The Acme Contact Form license state is {LicenseState} (key {KeyIdentifier}).", result.State, identifier);
            }
        }
    }
}
