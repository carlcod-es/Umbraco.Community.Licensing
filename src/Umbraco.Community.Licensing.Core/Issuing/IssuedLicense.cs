namespace Umbraco.Community.Licensing.Core;

/// <summary>A license the issuer produced. The library keeps no copy; the vendor stores what it needs.</summary>
/// <param name="KeyString">The key string to give the customer: one line of printable ASCII, no whitespace.</param>
/// <param name="Reference">The license reference in display form with the issuer's prefix, such as <c>LIC-8F3AK-M7RXB</c>.</param>
/// <param name="KeyIdentifier">The key identifier in display form, such as <c>LIC-8F3AK-M7RXB-7Q2D</c>.</param>
/// <param name="Issued">The issue time, UTC, whole seconds.</param>
public sealed record IssuedLicense(string KeyString, string Reference, string KeyIdentifier, DateTimeOffset Issued)
{
    /// <summary>Describes the license without the key string, so logging an instance cannot leak the key.</summary>
    /// <returns>The reference, key identifier and issue time.</returns>
    public override string ToString() => $"IssuedLicense {{ Reference = {Reference}, KeyIdentifier = {KeyIdentifier}, Issued = {Issued:O} }}";
}
