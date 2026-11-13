using Microsoft.Extensions.Logging;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Licensing;

/// <summary>
/// Writes one warning each time the license state changes, so an operator can see when a key expires or is replaced
/// without a line on every request. Logs the state and the key identifier only, never the key. Register as a singleton.
/// </summary>
public sealed class LicenseStateLog(ILogger<LicenseStateLog> logger)
{
    private readonly Lock _gate = new();
    private LicenseState? _last;

    /// <summary>Compares the state with the last one logged and warns when it differs, including the first report.</summary>
    public void Report(LicenseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // The identifier is the verified one, else the one the key claims, else none. Never the key text.
        var identifier = result.License is { } license
            ? license.KeyIdentifier
            : result.ClaimedKeyIdentifier is { } claimed ? "claimed " + claimed : "none";

        // Logged inside the lock so two threads reporting the same change write once, and in order.
        lock (_gate)
        {
            if (_last == result.State)
            {
                return;
            }

            _last = result.State;
            logger.LogWarning("The Acme Contact Form license state is now {LicenseState} (key {KeyIdentifier}).", result.State, identifier);
        }
    }
}
