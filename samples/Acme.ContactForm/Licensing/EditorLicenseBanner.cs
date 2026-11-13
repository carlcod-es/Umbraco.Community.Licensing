using System.Globalization;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Licensing;

/// <summary>
/// The case names an <see cref="EditorLicenseBanner"/> reports. None contains a license state name, so the banner data
/// can be checked for state names being absent.
/// </summary>
public static class BannerCases
{
    public const string NeedsAttention = "NeedsAttention";
    public const string ExpiringSoon = "ExpiringSoon";
    public const string OverMonthlyLimit = "OverMonthlyLimit";
    public const string DomainNotCovered = "DomainNotCovered";
}

/// <summary>
/// The editor's banner data. Holds no key string, identifier, state, claimed product or feature value other than the expiry
/// date and the usage numbers. Wording is built here; the client only lays it out.
/// </summary>
/// <param name="Cases">The <see cref="BannerCases"/> that apply, in a fixed order.</param>
/// <param name="Message">One sentence per case, joined, each ending "Contact your administrator."</param>
/// <param name="ExpiryDate">The expiry date (yyyy-MM-dd, UTC) when expiring soon; otherwise null.</param>
/// <param name="Used">Submissions this month when over the limit; otherwise null.</param>
/// <param name="Limit">The monthly limit when over it; otherwise null.</param>
public sealed record EditorLicenseBanner(
    IReadOnlyList<string> Cases,
    string Message,
    string? ExpiryDate,
    int? Used,
    int? Limit)
{
    private const string Contact = "Contact your administrator.";

    /// <summary>The banner, or null when the license is Valid, not expiring soon, within its limit and covering the domain.</summary>
    public static EditorLicenseBanner? From(LicenseSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.State != LicenseState.Valid)
        {
            return new EditorLicenseBanner(
                [BannerCases.NeedsAttention],
                $"The contact form's license needs attention. {Contact}",
                null,
                null,
                null);
        }

        var cases = new List<string>();
        var messages = new List<string>();
        string? expiryDate = null;
        int? used = null;
        int? limit = null;

        if (snapshot.ExpiringSoon && snapshot.ExpiresUtc is { } expires)
        {
            expiryDate = expires.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            cases.Add(BannerCases.ExpiringSoon);
            messages.Add($"The contact form's license expires on {expiryDate}. {Contact}");
        }

        if (snapshot.OverLimit && snapshot.MonthlyLimit is { } max)
        {
            used = snapshot.UsedThisMonth;
            limit = max;
            cases.Add(BannerCases.OverMonthlyLimit);
            messages.Add($"The contact form is over its monthly submission limit ({used} of {max} used). {Contact}");
        }

        if (!snapshot.DomainCovered)
        {
            cases.Add(BannerCases.DomainNotCovered);
            var host = snapshot.HostChecked.Length > 0 ? snapshot.HostChecked : "this site's host";
            messages.Add($"The contact form's license does not cover {host}. {Contact}");
        }

        return cases.Count == 0
            ? null
            : new EditorLicenseBanner(cases, string.Join(' ', messages), expiryDate, used, limit);
    }
}
