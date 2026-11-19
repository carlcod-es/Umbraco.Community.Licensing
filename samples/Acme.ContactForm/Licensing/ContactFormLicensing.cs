using Acme.ContactForm.Submissions;
using Microsoft.AspNetCore.Http;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Licensing;

/// <summary>What the product knows about its license right now. Holds no key text.</summary>
/// <param name="HostChecked">The host without its port.</param>
/// <param name="DomainCovered">True when the license is not Valid or names no domains; otherwise whether the host is listed.</param>
/// <param name="LicensedDomains">The listed domains, or null when not Valid or the feature is absent.</param>
/// <param name="DaysLeft">Whole days to expiry, rounded down; null when not Valid or perpetual.</param>
public sealed record LicenseSnapshot(
    LicenseState State,
    LicenseResult Result,
    string KeySourceName,
    bool UploadsGranted,
    int? MonthlyLimit,
    int UsedThisMonth,
    bool OverLimit,
    string HostChecked,
    bool DomainCovered,
    IReadOnlyList<string>? LicensedDomains,
    bool ExpiringSoon,
    DateTimeOffset? ExpiresUtc,
    int? DaysLeft);

/// <summary>
/// The licensing core to copy: reads the key, evaluates it and turns the result into the answers the product acts on.
/// Evaluated on every call, never cached. Register as a singleton.
/// </summary>
/// <param name="stateLog">Told the snapshot on every evaluation; it logs a change of condition once.</param>
public sealed class ContactFormLicensing(
    ContactFormProduct product,
    ILicenseKeySource keySource,
    ISubmissionStore submissions,
    LicenseStateLog stateLog)
{
    public const int ExpiringSoonDays = 30;

    /// <param name="host">The request host, possibly with a port (<c>localhost:44321</c>, <c>[::1]:5000</c>).</param>
    public LicenseSnapshot Check(string host)
    {
        var reading = keySource.Read();
        var result = product.Evaluator.Evaluate(reading.Key);
        var valid = result.State == LicenseState.Valid;
        var hostChecked = new HostString(host ?? "").Host;

        var uploads = valid && result.HasSwitch(ContactFormProduct.FileUploadsFeature);

        int? limit = null;
        if (valid && result.GetNumber(ContactFormProduct.MaxSubmissionsPerMonthFeature) is { } number)
        {
            limit = (int)Math.Clamp(Math.Floor(number), int.MinValue, int.MaxValue);
        }

        var used = submissions.CountThisMonth();

        IReadOnlyList<string>? domains = null;
        if (valid && result.GetText(ContactFormProduct.LicensedDomainsFeature) is { } text)
        {
            domains = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        var covered = domains is null || domains.Any(d => string.Equals(d, hostChecked, StringComparison.OrdinalIgnoreCase));

        DateTimeOffset? expires = valid ? result.License?.Expires : null;
        int? daysLeft = expires is { } e ? (int)Math.Floor((e - product.Clock.GetUtcNow()).TotalDays) : null;

        var snapshot = new LicenseSnapshot(
            result.State,
            result,
            reading.SourceName,
            uploads,
            limit,
            used,
            limit is { } l && used > l,
            hostChecked,
            covered,
            domains,
            daysLeft is <= ExpiringSoonDays,
            expires,
            daysLeft);

        stateLog.Report(snapshot);
        return snapshot;
    }
}
