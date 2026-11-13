using Microsoft.Extensions.Options;

namespace Acme.ContactForm.Licensing;

/// <summary>The key currently in force, and where it came from (<c>Key</c> is null when none is set).</summary>
public readonly record struct LicenseKeyReading(string? Key, string SourceName);

/// <summary>Where the product gets its license key. Read on every use, so a changed key is picked up without a restart.</summary>
public interface ILicenseKeySource
{
    LicenseKeyReading Read();
}

/// <summary>Reads the key from the site's configuration through the options monitor, never caching it.</summary>
public sealed class ConfiguredLicenseKeySource(IOptionsMonitor<ContactFormOptions> options) : ILicenseKeySource
{
    public const string Name = "Configured key";

    public LicenseKeyReading Read()
    {
        var key = options.CurrentValue.LicenseKey;
        return new(string.IsNullOrWhiteSpace(key) ? null : key, Name);
    }
}
