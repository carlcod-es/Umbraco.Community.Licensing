namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// The signed payload as a value (ADR-0002). Produced by the issuer for <see cref="PayloadWriter"/> and by the
/// strict reader after verification. Reference and key part are not here: they live only in the identifier (ADR-0001).
/// </summary>
/// <param name="SigningKeyId">The 11-character signing key ID.</param>
/// <param name="Product">The product ID.</param>
/// <param name="Issued">The issue time, UTC, whole seconds.</param>
/// <param name="Expires">The expiry time, UTC, whole seconds; null means perpetual.</param>
/// <param name="DisplayName">The display name; null means none.</param>
/// <param name="VendorTag">The vendor tag; null means none.</param>
/// <param name="Features">The features in order; empty means none.</param>
internal sealed record LicensePayload(
    string SigningKeyId,
    string Product,
    DateTimeOffset Issued,
    DateTimeOffset? Expires,
    string? DisplayName,
    string? VendorTag,
    IReadOnlyList<FeatureInput> Features);
