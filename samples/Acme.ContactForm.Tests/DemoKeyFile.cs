global using Acme.ContactForm.Demo;

using System.Globalization;
using System.Text.Json;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>Writes the demo key file and helps read it. The reader is the package's <see cref="DemoKeys"/>.</summary>
internal static class DemoKeyFile
{
    /// <summary>Evaluation date for entries without a demo date; after every issue date, before the expiring entries lapse.</summary>
    public static readonly DateTimeOffset FixedClock = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string SourcePath => Path.Combine(DemoKeyFiles.SamplesRoot, "Acme.ContactForm", "Demo", "demo-keys.json");

    public static IReadOnlyList<DemoKeyEntry> LoadEmbedded() => DemoKeys.LoadEmbedded();

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
