using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The backup file format (ADR-0015): a zip with exactly two entries, <see cref="ManifestEntry"/> and <see cref="DatabaseEntry"/>,
/// names with <c>/</c> only. Export writes it and restore reads it.
/// </summary>
internal static class BackupFormat
{
    public const int Version = 1;
    public const string ManifestEntry = "manifest.json";
    public const string DatabaseEntry = "ucl.db";

    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The time a backup was taken as shown by export and restore: UTC, to the second.</summary>
    public static string Describe(DateTime takenUtc) =>
        takenUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
}

/// <summary>
/// The manifest of a backup. <paramref name="TakenUtc"/> is ISO 8601 UTC; <paramref name="LastMigration"/> is the highest
/// <c>MigrationId</c> in the snapshot's history.
/// </summary>
internal sealed record BackupManifest(
    [property: JsonPropertyName("format")] int Format,
    [property: JsonPropertyName("toolVersion")] string ToolVersion,
    [property: JsonPropertyName("lastMigration")] string LastMigration,
    [property: JsonPropertyName("takenUtc")] DateTime TakenUtc);
