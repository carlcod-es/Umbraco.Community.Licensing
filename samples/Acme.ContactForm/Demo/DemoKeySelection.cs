using Acme.ContactForm.Licensing;

namespace Acme.ContactForm.Demo;

/// <summary>
/// The demo key the picker chose, or none. Held in memory only and never written to configuration, so a restart goes back
/// to the configured key. Register as a singleton; safe to read and change from any thread.
/// </summary>
public sealed class DemoKeySelection
{
    /// <summary>The select request's value that goes back to the configured key (the entry's own name works too).</summary>
    public const string ConfiguredId = "configured";

    private volatile DemoKeyEntry? _chosen;

    public DemoKeySelection() : this(DemoKeys.LoadEmbedded())
    {
    }

    public DemoKeySelection(IReadOnlyList<DemoKeyEntry> entries) => Entries = entries;

    public IReadOnlyList<DemoKeyEntry> Entries { get; }

    /// <summary>The chosen entry, or null when the configured key is in force.</summary>
    public DemoKeyEntry? Chosen => _chosen;

    /// <summary>Chooses an entry by name, or the configured key by <see cref="ConfiguredId"/>. False when the name is unknown.</summary>
    public bool TrySelect(string? name)
    {
        if (string.Equals(name, ConfiguredId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ConfiguredLicenseKeySource.Name, StringComparison.Ordinal))
        {
            _chosen = null;
            return true;
        }

        var entry = Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        if (entry is null)
        {
            return false;
        }

        _chosen = entry;
        return true;
    }
}

/// <summary>Wraps the configured key source; while an entry is chosen it supplies that entry's key instead.</summary>
public sealed class DemoKeySource(ILicenseKeySource configured, DemoKeySelection selection) : ILicenseKeySource
{
    public const string NamePrefix = "Demo selection: ";

    public LicenseKeyReading Read() =>
        selection.Chosen is { } entry
            ? new LicenseKeyReading(entry.Key, NamePrefix + entry.Name)
            : configured.Read();
}

/// <summary>
/// The licensing clock in the demo site: the chosen entry's demo date when it has one, the system clock otherwise. Never
/// register it as <see cref="TimeProvider"/>: it is given to the product (and so the evaluator) only.
/// </summary>
public sealed class DemoClock(DemoKeySelection selection) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => selection.Chosen?.DemoInstant ?? System.GetUtcNow();
}
