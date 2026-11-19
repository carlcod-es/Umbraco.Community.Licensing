using System.Globalization;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Licensing;

/// <summary>One known feature as the dashboard shows it. All wording is built on the server.</summary>
/// <param name="Name">The feature name.</param>
/// <param name="Grants">What it grants: "on", the number, the domains list, or "not included". For a lapsed license it ends "(no longer in effect)".</param>
/// <param name="Included">True when the license holds the feature (on, a number or a text).</param>
/// <param name="InEffect">True only when the license is Valid and holds the feature.</param>
public sealed record AdminFeatureStatus(string Name, string Grants, bool Included, bool InEffect);

/// <param name="Used">Submissions taken this month.</param>
/// <param name="Limit">The monthly limit, or null when there is none in effect.</param>
public sealed record AdminUsageStatus(int Used, int? Limit, bool OverLimit, string Text);

/// <param name="Host">The host checked, without its port.</param>
/// <param name="Checked">True only when the domain check ran, which it does only for a Valid license. When false, <c>Covered</c> says nothing.</param>
/// <param name="Covered">True when the host is covered (or any domain is, with no licensed domains). Meaningful only when <c>Checked</c>.</param>
public sealed record AdminDomainStatus(string Host, bool Checked, bool Covered, string Text);

/// <summary>
/// The licensing dashboard's data, for administrators. Holds no key string. Wording is built here; the client only lays it out.
/// </summary>
/// <param name="State">The state name.</param>
/// <param name="FirstAction">What to do first, or null when Valid.</param>
/// <param name="KeyIdentifier">The key identifier, verified or only claimed; null when there is none.</param>
/// <param name="KeyIdentifierVerified">False when the identifier is only claimed by an unverified key.</param>
/// <param name="ClaimedProduct">The product the key names, only when it is not this one.</param>
/// <param name="Perpetual">True for a verified license with no expiry.</param>
/// <param name="ExpiryDate">The expiry date (yyyy-MM-dd, UTC), or null when perpetual or there is no verified license.</param>
/// <param name="Expiry">The expiry wording: "Perpetual", "Expires 2099-12-31", "Expired 2026-01-31"; null with no verified license.</param>
/// <param name="ExpiringSoonText">The expiring-soon wording with the date, or null.</param>
/// <param name="Features">The three known features, in a fixed order.</param>
/// <param name="SettingName">The setting that holds the key.</param>
/// <param name="EnvironmentVariable">The same setting as an environment variable.</param>
/// <param name="KeySourceName">Where the key was read from.</param>
public sealed record AdminLicenseStatus(
    string State,
    string? FirstAction,
    string? KeyIdentifier,
    bool KeyIdentifierVerified,
    string? ClaimedProduct,
    bool Perpetual,
    string? ExpiryDate,
    string? Expiry,
    bool ExpiringSoon,
    string? ExpiringSoonText,
    IReadOnlyList<AdminFeatureStatus> Features,
    AdminUsageStatus Usage,
    AdminDomainStatus Domain,
    string SettingName,
    string EnvironmentVariable,
    string KeySourceName)
{
    private static readonly string[] KnownFeatures =
    [
        ContactFormProduct.FileUploadsFeature,
        ContactFormProduct.MaxSubmissionsPerMonthFeature,
        ContactFormProduct.LicensedDomainsFeature,
    ];

    public static AdminLicenseStatus From(LicenseSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var result = snapshot.Result;
        var license = result.License;
        var valid = snapshot.State == LicenseState.Valid;

        var verified = license is not null;
        var identifier = license?.KeyIdentifier ?? result.ClaimedKeyIdentifier;

        var claimed = license?.Product ?? result.ClaimedProduct;
        if (string.Equals(claimed, ContactFormProduct.ProductId, StringComparison.Ordinal))
        {
            claimed = null;
        }

        string? expiryDate = null;
        string? expiry = null;
        if (license is not null)
        {
            if (license.Expires is { } expires)
            {
                expiryDate = Date(expires);
                expiry = valid ? $"Expires {expiryDate}" : $"Expired {expiryDate}";
            }
            else
            {
                expiry = "Perpetual";
            }
        }

        var soon = valid && snapshot.ExpiringSoon;
        var soonText = soon && expiryDate is not null ? $"This license expires on {expiryDate} (UTC)." : null;

        return new AdminLicenseStatus(
            snapshot.State.ToString(),
            FirstActions.For(snapshot.State),
            identifier,
            verified,
            claimed,
            license is { IsPerpetual: true },
            expiryDate,
            expiry,
            soon,
            soonText,
            [.. KnownFeatures.Select(name => Feature(name, license, valid))],
            BuildUsage(snapshot),
            BuildDomain(snapshot),
            ContactFormOptions.LicenseKeySettingName,
            ContactFormOptions.LicenseKeyEnvironmentVariable,
            snapshot.KeySourceName);
    }

    private static string Date(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static AdminFeatureStatus Feature(string name, VerifiedLicense? license, bool valid)
    {
        var value = license?.Features.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        var grants = value switch
        {
            SwitchFeature { IsOn: true } => "on",
            NumberFeature n => n.Value.ToString("0.############################", CultureInfo.InvariantCulture),
            TextFeature t => t.Value,
            _ => null,
        };

        if (grants is null)
        {
            return new AdminFeatureStatus(name, "not included", false, false);
        }

        return valid
            ? new AdminFeatureStatus(name, grants, true, true)
            : new AdminFeatureStatus(name, $"{grants} (no longer in effect)", true, false);
    }

    private static string Submissions(int count) => count == 1 ? "1 submission" : $"{count} submissions";

    private static AdminUsageStatus BuildUsage(LicenseSnapshot s)
    {
        var text = s.MonthlyLimit is { } limit
            ? $"{s.UsedThisMonth} of {limit} submissions used this month" + (s.OverLimit ? ", over the limit" : "")
            : $"{Submissions(s.UsedThisMonth)} this month, no limit in effect";
        return new AdminUsageStatus(s.UsedThisMonth, s.MonthlyLimit, s.OverLimit, text);
    }

    private static AdminDomainStatus BuildDomain(LicenseSnapshot s)
    {
        var host = s.HostChecked;
        var text = s.State != LicenseState.Valid
            ? $"Not checked: the license is not valid. Host: {host}"
            : s.LicensedDomains is null
                ? $"Any domain is covered. Host: {host}"
                : s.DomainCovered
                    ? $"{host} is covered by the licensed domains ({string.Join(", ", s.LicensedDomains)})."
                    : $"The license does not cover {host}. Licensed domains: {string.Join(", ", s.LicensedDomains)}.";
        return new AdminDomainStatus(host, s.State == LicenseState.Valid, s.DomainCovered, text);
    }
}
