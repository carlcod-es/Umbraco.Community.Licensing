using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>One entry of Demo/demo-keys.json: the key and what evaluating it must show.</summary>
public sealed record DemoKeyEntry
{
    public string Name { get; init; } = "";

    public string Description { get; init; } = "";

    /// <summary>The key string, or null for the "No key" entry.</summary>
    public string? Key { get; init; }

    /// <summary>The date the entry is evaluated at (ISO 8601 UTC); null means any date after the issue date.</summary>
    public string? DemoDate { get; init; }

    public string State { get; init; } = "";

    /// <summary>The verified product, or the claimed one for <c>WrongProduct</c>; null where there is none.</summary>
    public string? Product { get; init; }

    public bool ProductVerified { get; init; }

    /// <summary>The expiry instant (ISO 8601 UTC); null for perpetual or where there is no verified license.</summary>
    public string? Expires { get; init; }

    /// <summary>Feature name to value: true/false for a switch, a number, or text.</summary>
    public Dictionary<string, JsonElement> Features { get; init; } = [];
}

/// <summary>Reads and writes the demo key file. The reader uses the package assembly's embedded copy.</summary>
internal static class DemoKeyFile
{
    public const string ResourceName = "Acme.ContactForm.Demo.demo-keys.json";

    /// <summary>Evaluation date for entries without a demo date; after every issue date, before the expiring entries lapse.</summary>
    public static readonly DateTimeOffset FixedClock = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string SourcePath => Path.Combine(DemoKeyFiles.SamplesRoot, "Acme.ContactForm", "Demo", "demo-keys.json");

    public static IReadOnlyList<DemoKeyEntry> LoadEmbedded()
    {
        var assembly = Assembly.Load("Acme.ContactForm");
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The package assembly has no embedded resource {ResourceName}.");
        return JsonSerializer.Deserialize<List<DemoKeyEntry>>(stream, Json)
            ?? throw new InvalidOperationException("demo-keys.json is empty.");
    }

    public static string FormatInstant(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseInstant(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public static JsonElement ToJson(FeatureValue value) => value switch
    {
        SwitchFeature s => JsonSerializer.SerializeToElement(s.IsOn),
        NumberFeature n => JsonSerializer.SerializeToElement(n.Value),
        TextFeature t => JsonSerializer.SerializeToElement(t.Value),
        _ => throw new InvalidOperationException("Unknown feature value."),
    };

    /// <summary>A clock that always reads one instant.</summary>
    public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
