namespace Umbraco.Community.Licensing.Core;

/// <summary>What a vendor asks to issue. Nothing is checked here; the issuer lists every problem together.</summary>
public sealed record LicenseRequest
{
    /// <summary>The product ID.</summary>
    public string? Product { get; init; }

    /// <summary>The customer reference; generated when not given.</summary>
    public string? Reference { get; init; }

    /// <summary>The expiry; null means not stated, which is rejected at issue.</summary>
    public LicenseExpiry? Expiry { get; init; }

    /// <summary>The display name; null means not given, an explicit empty string is reported at issue.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The vendor tag; null means not given, an explicit empty string is reported at issue.</summary>
    public string? VendorTag { get; init; }

    /// <summary>The features, in order.</summary>
    public FeatureList Features { get; init; } = FeatureList.Empty;
}
