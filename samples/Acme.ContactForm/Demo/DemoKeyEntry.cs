using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.ContactForm.Demo;

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

    /// <summary><see cref="DemoDate"/> as an instant; null when the entry has none.</summary>
    [JsonIgnore]
    public DateTimeOffset? DemoInstant =>
        DemoDate is { } text
            ? DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
            : null;
}

/// <summary>Reads the demo key set embedded in this assembly. The one reader the picker and the tests share.</summary>
public static class DemoKeys
{
    public const string ResourceName = "Acme.ContactForm.Demo.demo-keys.json";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<DemoKeyEntry> LoadEmbedded()
    {
        using var stream = typeof(DemoKeys).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The package assembly has no embedded resource {ResourceName}.");
        return JsonSerializer.Deserialize<List<DemoKeyEntry>>(stream, Json)
            ?? throw new InvalidOperationException("demo-keys.json is empty.");
    }
}
