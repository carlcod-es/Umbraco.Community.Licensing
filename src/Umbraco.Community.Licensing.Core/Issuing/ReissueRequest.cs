namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// A request to issue a license again from a verified one. The product and reference are kept and cannot be
/// changed (no setter, so <c>with { Reference = ... }</c> does not compile); a new reference is a fresh <see cref="LicenseRequest"/>.
/// Nothing is checked here; issuing applies every issue rule.
/// </summary>
public sealed record ReissueRequest
{
    internal ReissueRequest(string product, string reference)
    {
        Product = product;
        Reference = reference;
    }

    /// <summary>The product ID of the license, kept.</summary>
    public string Product { get; }

    /// <summary>The reference as read from the license, with its prefix (<c>LIC-8F3AK-M7RXB</c>); the issuer keeps its 10 characters under its own prefix.</summary>
    public string Reference { get; }

    /// <summary>The expiry, stated explicitly when built from a license; null means not stated, which is rejected at issue.</summary>
    public LicenseExpiry? Expiry { get; init; }

    /// <summary>The display name; null means not given, an explicit empty string is reported at issue.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The vendor tag; null means not given, an explicit empty string is reported at issue.</summary>
    public string? VendorTag { get; init; }

    /// <summary>The features, in order.</summary>
    public FeatureList Features { get; init; } = FeatureList.Empty;

    /// <summary>
    /// Builds a request holding everything in <paramref name="license"/>. Reads no clock and checks nothing, so an
    /// expired license gives a request that is rejected at issue until its expiry is changed. A perpetual license
    /// gives <see cref="LicenseExpiry.Perpetual"/>.
    /// </summary>
    /// <param name="license">The verified license.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="license"/> is null.</exception>
    public static ReissueRequest From(VerifiedLicense license)
    {
        ArgumentNullException.ThrowIfNull(license);
        return new ReissueRequest(license.Product, license.Reference)
        {
            Expiry = license.Expires is { } expires ? LicenseExpiry.At(expires) : LicenseExpiry.Perpetual,
            DisplayName = license.DisplayName,
            VendorTag = license.VendorTag,
            Features = license.Features,
        };
    }
}
