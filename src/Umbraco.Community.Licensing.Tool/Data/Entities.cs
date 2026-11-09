namespace Umbraco.Community.Licensing.Tool.Data;

// The tables of design.md "Database and folder" (ADR-0013). Rules every entity follows (PDR-0046):
//  - Every timestamp is UTC, stored as text by SQLite ("yyyy-MM-dd HH:mm:ss.fffffff"); see ToolDbContext.
//  - Enums are stored as lower-case text (for example "standby"), readable with a plain SQLite connection.
//  - No column holds a key string, an absolute path or anything specific to an operating system.

/// <summary>A named setting: prefix, signing-key mode, standby on, idle lock, passphrase check (<see cref="SettingNames"/>).</summary>
internal sealed class Setting
{
    public required string Name { get; set; }
    public required string Value { get; set; }
}

/// <summary>The names of the <see cref="Setting"/> rows the tool reads or writes.</summary>
internal static class SettingNames
{
    /// <summary>Idle lock: whole minutes as ASCII digits, or <c>never</c> (design.md, Typed values).</summary>
    public const string IdleLock = "IdleLock";
}

internal sealed class Product
{
    public required string ProductId { get; set; }
    public required string DisplayName { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>The type of a feature definition.</summary>
internal enum FeatureType { Switch, Number, Text }

/// <summary>A feature a product defines. Key: <see cref="ProductId"/> and <see cref="Name"/>.</summary>
internal sealed class FeatureDefinition
{
    public required string ProductId { get; set; }
    public required string Name { get; set; }
    public FeatureType Type { get; set; }
    public DateTime AddedUtc { get; set; }
}

/// <summary>The role of a signing key.</summary>
internal enum KeyRole { Active, Standby, Replaced }

/// <summary>Why a signing key was replaced.</summary>
internal enum ReplacedReason { Routine, Loss, Leak }

/// <summary>A signing key pair. The private key is held only as an encrypted PEM (ADR-0014), with <c>\n</c> line endings.</summary>
internal sealed class SigningKeyRecord
{
    public required string SigningKeyId { get; set; }

    /// <summary>The product the key is for; null in shared mode.</summary>
    public string? ProductId { get; set; }

    /// <summary>The public key's export line.</summary>
    public required string PublicKey { get; set; }

    public required string EncryptedPrivateKeyPem { get; set; }
    public KeyRole Role { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ReplacedUtc { get; set; }
    public ReplacedReason? ReplacedReason { get; set; }
    public DateTime? ReissuesSentUtc { get; set; }
}

/// <summary>A key is in a release for a product when its row exists (ADR-0022). Key: <see cref="SigningKeyId"/> and <see cref="ProductId"/>.</summary>
internal sealed class KeyRelease
{
    public required string SigningKeyId { get; set; }
    public required string ProductId { get; set; }
    public DateTime ConfirmedUtc { get; set; }
}

/// <summary>A license, identified by its 10-character reference. <see cref="CustomerEmailKey"/> is the match key (ADR-0019), set and cleared with the email.</summary>
internal sealed class LicenseRecord
{
    public required string Reference { get; set; }
    public required string ProductId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerEmailKey { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>What an issue entry records.</summary>
internal enum IssueKind { Issue, Reissue, Renewal, Resend }

/// <summary>
/// One signing of a license. <see cref="KeyIdentifier"/> keeps the prefix the entry was issued with.
/// <see cref="FeaturesJson"/> is JSON text; <see cref="OrderRefKey"/> is the match key, set and cleared with <see cref="OrderRef"/>.
/// </summary>
internal sealed class IssueEntry
{
    public long Id { get; set; }
    public required string Reference { get; set; }
    public IssueKind Kind { get; set; }
    public DateTime IssuedUtc { get; set; }

    /// <summary>Null is perpetual.</summary>
    public DateTime? ExpiresUtc { get; set; }

    public string? DisplayName { get; set; }
    public string? VendorTag { get; set; }
    public required string FeaturesJson { get; set; }
    public string? OrderRef { get; set; }
    public string? OrderRefKey { get; set; }
    public required string KeyIdentifier { get; set; }
    public required string SigningKeyId { get; set; }
}
