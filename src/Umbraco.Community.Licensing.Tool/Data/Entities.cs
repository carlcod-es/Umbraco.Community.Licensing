namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>Role of a signing key (stored as text).</summary>
internal enum SigningKeyRole
{
    Active,
    Standby,
    Replaced,
}

/// <summary>Why a signing key was replaced (stored as text).</summary>
internal enum ReplacementReason
{
    Routine,
    Loss,
    Leak,
}

/// <summary>What an issue entry records (stored as text).</summary>
internal enum IssueKind
{
    Issue,
    Reissue,
    Renewal,
    Resend,
}

/// <summary>Value type of a feature definition (stored as text).</summary>
internal enum FeatureType
{
    Switch,
    Number,
    Text,
}

/// <summary>A named setting: prefix, signing-key mode, standby on, passphrase check.</summary>
internal sealed class Setting
{
    public required string Name { get; set; }

    public required string Value { get; set; }
}

internal sealed class Product
{
    public required string ProductId { get; set; }

    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public List<FeatureDefinition> Features { get; } = [];
}

internal sealed class FeatureDefinition
{
    public required string ProductId { get; set; }

    public required string Name { get; set; }

    public FeatureType Type { get; set; }

    public DateTimeOffset AddedUtc { get; set; }
}

internal sealed class SigningKey
{
    public required string SigningKeyId { get; set; }

    /// <summary>Null for the shared key.</summary>
    public string? ProductId { get; set; }

    /// <summary>The public key export line.</summary>
    public required string PublicKey { get; set; }

    /// <summary>Encrypted PKCS#8 PEM; never a usable key without the passphrase.</summary>
    public required string EncryptedPrivateKeyPem { get; set; }

    public SigningKeyRole Role { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ReplacedUtc { get; set; }

    public ReplacementReason? ReplacedReason { get; set; }

    public DateTimeOffset? InReleaseUtc { get; set; }

    public DateTimeOffset? ReissuesSentUtc { get; set; }
}

internal sealed class License
{
    /// <summary>The 10-character license reference.</summary>
    public required string Reference { get; set; }

    public required string ProductId { get; set; }

    public string? CustomerEmail { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public List<IssueEntry> Entries { get; } = [];
}

internal sealed class IssueEntry
{
    public long Id { get; set; }

    public required string Reference { get; set; }

    public IssueKind Kind { get; set; }

    public DateTimeOffset IssuedUtc { get; set; }

    /// <summary>Null = perpetual.</summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    public string? DisplayName { get; set; }

    public string? VendorTag { get; set; }

    public required string FeaturesJson { get; set; }

    public string? OrderRef { get; set; }

    /// <summary>The prefix-bearing key identifier this entry was issued with. Not the key string.</summary>
    public required string KeyIdentifier { get; set; }

    public required string SigningKeyId { get; set; }
}
