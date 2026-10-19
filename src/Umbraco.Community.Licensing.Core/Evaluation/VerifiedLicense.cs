namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// A license whose signature the evaluator has verified: every value here is a verified fact from the key.
/// The evaluator produces it; it holds no key text, so the default <c>ToString</c> cannot leak it.
/// </summary>
/// <param name="Product">The product ID.</param>
/// <param name="Reference">The reference in display form with its prefix, such as <c>LIC-8F3AK-M7RXB</c>.</param>
/// <param name="KeyIdentifier">The key identifier in display form, such as <c>LIC-8F3AK-M7RXB-7Q2D</c>.</param>
/// <param name="SigningKeyId">The ID of the key that signed the license.</param>
/// <param name="Issued">When the license was issued.</param>
/// <param name="Expires">When the license expires, or null when it is perpetual.</param>
/// <param name="DisplayName">The display name, or null.</param>
/// <param name="VendorTag">The vendor tag, or null.</param>
/// <param name="Features">The features, in the order of the key.</param>
public sealed record VerifiedLicense(
    string Product,
    string Reference,
    string KeyIdentifier,
    string SigningKeyId,
    DateTimeOffset Issued,
    DateTimeOffset? Expires,
    string? DisplayName,
    string? VendorTag,
    FeatureList Features)
{
    /// <summary>True when the license has no expiry.</summary>
    public bool IsPerpetual => Expires is null;

    /// <summary>The key identifier only, so logging a license never prints other values or any part of the key string.</summary>
    /// <returns>For example <c>VerifiedLicense LIC-8F3AK-M7RXB-7Q2D</c>.</returns>
    public override string ToString() => $"VerifiedLicense {KeyIdentifier}";
}
