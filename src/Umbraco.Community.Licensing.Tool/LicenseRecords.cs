using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>The filters of <c>license list</c>, as typed; null = not filtered. All given filters must match.</summary>
/// <param name="Product">The product ID, compared as stored.</param>
/// <param name="Reference">Read under the prefix rule (PDR-0023).</param>
/// <param name="OrderRef">Matches the order reference of any issue entry.</param>
/// <param name="Email">The whole customer email, case ignored.</param>
internal sealed record LicenseFilter(string? Product, string? Reference, string? OrderRef, string? Email);

/// <summary>What <c>license customer</c> was asked for, as typed.</summary>
/// <param name="OrderRef">The corrected order reference of the latest issue entry.</param>
/// <param name="ClearOrderRefs">Clear the order reference of every issue entry.</param>
/// <param name="Email">The new customer email.</param>
/// <param name="ClearEmail">Clear the customer email.</param>
internal sealed record CustomerChange(string? OrderRef, bool ClearOrderRefs, string? Email, bool ClearEmail);

/// <summary>Finding, showing and editing the customer details of licenses.</summary>
internal static class LicenseRecords
{
    /// <summary>A reference as shown: the installation's prefix, then the 10 characters in two groups of 5.</summary>
    public static string DisplayReference(InstallationSettings settings, string reference)
    {
        var groups = $"{reference[..5]}-{reference[5..]}";
        return settings.Prefix.Length == 0 ? groups : $"{settings.Prefix}-{groups}";
    }

    /// <summary>
    /// The licenses matching every given filter, in the order they were first issued (reference breaks ties).
    /// Filtering is done in memory: a vendor's licenses are few, and it keeps the email comparison independent
    /// of the database's collation. An empty filter value is not a filter that matches everything but one that
    /// matches nothing, so a script never lists (or acts on) every license by an unset variable.
    /// </summary>
    public static IReadOnlyList<License> List(ToolDbContext db, InstallationSettings settings, LicenseFilter filter)
    {
        var reference = filter.Reference is null ? null : Issuing.NormaliseReference(settings, filter.Reference);
        var email = filter.Email?.Trim();
        var orderRef = filter.OrderRef?.Trim();
        var product = filter.Product?.Trim();
        return [.. db.Licenses.Include(l => l.Entries).AsEnumerable()
            .Where(l => product is null || l.ProductId == product)
            .Where(l => reference is null || l.Reference == reference)
            .Where(l => email is null || (email.Length > 0 && string.Equals(l.CustomerEmail, email, StringComparison.OrdinalIgnoreCase)))
            .Where(l => orderRef is null || (orderRef.Length > 0 && l.Entries.Any(e => e.OrderRef == orderRef)))
            .OrderBy(l => l.CreatedUtc)
            .ThenBy(l => l.Reference, StringComparer.Ordinal)];
    }

    /// <summary>One tab-separated row per license: reference, product, current expiry, email, latest order reference.</summary>
    public static string FormatRows(InstallationSettings settings, IEnumerable<License> licenses)
    {
        var text = new StringBuilder();
        foreach (var license in licenses)
        {
            var latest = Latest(license);
            text.AppendJoin(
                '\t',
                DisplayReference(settings, license.Reference),
                license.ProductId,
                Issuing.ExpiryText(latest.ExpiresUtc),
                license.CustomerEmail ?? "",
                latest.OrderRef ?? "");
            text.AppendLine();
        }

        return text.ToString();
    }

    /// <summary>
    /// One license with every issue entry, newest first:
    /// <code>
    /// LIC-8F3AK-M7RXB
    ///   Product: acme.commerce
    ///   Customer email: owner@example.com    (none when empty)
    ///   Issue entries: 3
    ///   #3 renewal
    ///     Issued: ...  Expires: ...  Display name / Vendor tag / Features / Order reference (each "none" when empty)
    ///     Key identifier: ...  Signing key ID: ...
    /// </code>
    /// </summary>
    public static string Format(InstallationSettings settings, License license)
    {
        var entries = license.Entries.OrderBy(e => e.Id).ToList();
        var text = new StringBuilder();
        text.AppendLine(DisplayReference(settings, license.Reference));
        text.AppendLine($"  Product: {license.ProductId}");
        text.AppendLine($"  Customer email: {license.CustomerEmail ?? "none"}");
        text.AppendLine($"  Issue entries: {entries.Count}");
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            text.AppendLine($"  #{i + 1} {entry.Kind.ToString().ToLowerInvariant()}");
            text.AppendLine($"    Issued: {Issuing.ExpiryText(entry.IssuedUtc)}");
            text.AppendLine($"    Expires: {Issuing.ExpiryText(entry.ExpiresUtc)}");
            text.AppendLine($"    Display name: {entry.DisplayName ?? "none"}");
            text.AppendLine($"    Vendor tag: {entry.VendorTag ?? "none"}");
            text.AppendLine($"    Features: {FeaturesText(entry.FeaturesJson)}");
            text.AppendLine($"    Order reference: {entry.OrderRef ?? "none"}");
            text.AppendLine($"    Key identifier: {entry.KeyIdentifier}");
            text.AppendLine($"    Signing key ID: {entry.SigningKeyId}");
        }

        return text.ToString();
    }

    /// <summary>
    /// Applies <paramref name="change"/> in one save. Only the latest issue entry's order reference can be
    /// corrected; clearing touches every entry. Nothing else is stored, so the earlier values leave the row.
    /// </summary>
    /// <returns>What was done, as a phrase.</returns>
    /// <exception cref="ToolRefusal">Nothing to change, or an empty value.</exception>
    public static string ApplyCustomerChange(ToolDbContext db, License license, CustomerChange change)
    {
        var email = change.Email?.Trim();
        var orderRef = change.OrderRef?.Trim();
        if (email is { Length: 0 })
        {
            throw new ToolRefusal("--email needs a value; use --clear-email to clear it. Nothing changed.");
        }

        if (orderRef is { Length: 0 })
        {
            throw new ToolRefusal("--order-ref needs a value; use --clear-order-refs to clear them. Nothing changed.");
        }

        var done = new List<string>();
        if (email is not null)
        {
            license.CustomerEmail = email;
            done.Add("customer email set");
        }

        if (change.ClearEmail)
        {
            license.CustomerEmail = null;
            done.Add("customer email cleared");
        }

        if (orderRef is not null)
        {
            Latest(license).OrderRef = orderRef;
            done.Add("latest order reference corrected");
        }

        if (change.ClearOrderRefs)
        {
            foreach (var entry in license.Entries)
            {
                entry.OrderRef = null;
            }

            done.Add("order references cleared");
        }

        if (done.Count == 0)
        {
            throw new ToolRefusal("Nothing to change. Give --order-ref, --clear-order-refs, --email or --clear-email. Nothing changed.");
        }

        db.SaveChanges();
        return string.Join(", ", done);
    }

    private static IssueEntry Latest(License license) => license.Entries.OrderBy(e => e.Id).Last();

    private static string FeaturesText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var parts = document.RootElement.EnumerateArray().Select(item =>
        {
            var value = item.GetProperty("value");
            var shown = item.GetProperty("type").GetString() switch
            {
                "switch" => value.GetBoolean() ? "on" : "off",
                "number" => value.GetDecimal().ToString(CultureInfo.InvariantCulture),
                _ => value.GetString(),
            };
            return $"{item.GetProperty("name").GetString()}={shown}";
        }).ToList();
        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }
}
