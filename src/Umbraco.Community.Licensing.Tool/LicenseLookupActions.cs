using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Licenses &gt; Find and list, Show a license and Customer details (vendor-tool-licenses). They read and correct the records and never sign, so
/// they never ask for the passphrase. Nothing here deletes a license or an issue entry; only the customer fields are cleared, and a value
/// is cleared together with its match key (ADR-0019), each change in one transaction.
/// </summary>
internal static class LicenseLookupActions
{
    internal const string NoMatch = "No licenses match.";
    internal const string NoEmail = "This license has no customer email. Nothing changed.";
    internal const string Declined = "Nothing cleared.";
    internal const string NoOrderReferences = "No issue entry of this license holds an order reference. Nothing changed.";

    private const string None = "(none)";

    private static readonly IReadOnlyList<Choice> CustomerChoices =
    [
        new("change-email", "Change the customer email"),
        new("clear-email", "Clear the customer email"),
        new("correct-order-reference", "Correct the latest order reference"),
        new("clear-order-references", "Clear every order reference"),
    ];

    /// <summary>Entries newest first: issue time, then ID for entries made at the same instant.</summary>
    private static List<IssueEntry> NewestFirst(IEnumerable<IssueEntry> entries) =>
        [.. entries.OrderByDescending(e => e.IssuedUtc).ThenByDescending(e => e.Id)];

    private static string Time(DateTime utc) => utc.ToString(ReissueActions.ExpiryFormat, CultureInfo.InvariantCulture);

    private static string Expiry(DateTime? expiresUtc) => expiresUtc is { } e ? Time(e) : TypedValues.PerpetualWord;

    private static string Display(KeyPrefix prefix, string reference) =>
        KeyIdentifierFormat.DisplayReference(prefix.Value.Length == 0 ? null : prefix.Value, reference);

    /// <summary>
    /// Four optional filters, each skipped by an empty answer; all empty lists every license. Rows are in reference order (ordinal). The typed
    /// filters pass their input rule before they are turned into a match key, which cannot take every text.
    /// </summary>
    internal static void Find(Session session)
    {
        var questions = session.Questions;
        var console = session.Console;
        var prefix = LicenseActions.ReadPrefix(session);

        string? productId = null;
        List<Choice> products;
        using (var db = session.OpenDb())
            products = [.. db.Products.AsNoTracking().Select(p => new { p.ProductId, p.DisplayName }).ToList()
                .OrderBy(p => p.ProductId, StringComparer.Ordinal).Select(p => new Choice(p.ProductId, $"{p.ProductId} - {p.DisplayName}"))];
        if (products.Count > 0)
            productId = questions.AskChoiceOrSkip("Product", products)?.Id;

        var license = LicenseQuestion.AskOrSkip(session);
        var orderRef = LicenseActions.AskOptional(questions, "Order reference", TypedValues.OrderRefAccepts, TypedValues.CheckOrderReference);
        var email = LicenseActions.AskOptional(questions, "Customer email", TypedValues.EmailAccepts, TypedValues.CheckCustomerEmail);
        var orderRefKey = orderRef is null ? null : TypedValues.MatchKey(orderRef);
        var emailKey = email is null ? null : TypedValues.MatchKey(email);

        List<(LicenseRecord License, IssueEntry Latest)> rows = [];
        using (var db = session.OpenDb())
        {
            var licenses = db.Licenses.AsNoTracking().AsQueryable();
            if (productId is not null)
                licenses = licenses.Where(l => l.ProductId == productId);
            if (license is not null)
                licenses = licenses.Where(l => l.Reference == license.Reference);
            if (emailKey is not null)
                licenses = licenses.Where(l => l.CustomerEmailKey == emailKey);
            if (orderRefKey is not null)
                licenses = licenses.Where(l => db.IssueEntries.Any(e => e.Reference == l.Reference && e.OrderRefKey == orderRefKey));

            var found = licenses.ToList().OrderBy(l => l.Reference, StringComparer.Ordinal).ToList();
            var references = found.Select(l => l.Reference).ToList();
            var entries = db.IssueEntries.AsNoTracking().Where(e => references.Contains(e.Reference)).ToList()
                .GroupBy(e => e.Reference)
                .ToDictionary(g => g.Key, g => NewestFirst(g)[0]);
            foreach (var l in found)
            {
                var latest = entries.GetValueOrDefault(l.Reference)
                    ?? throw new InstallationRefusedException(ActiveKeys.Damaged($"The license {l.Reference} has no issue entry"));
                rows.Add((l, latest));
            }
        }

        if (rows.Count == 0)
        {
            console.WriteLine(NoMatch);
            return;
        }

        foreach (var (l, latest) in rows)
        {
            console.WriteLine(
                $"{Display(prefix, l.Reference)}  {l.ProductId}  expires {Expiry(latest.ExpiresUtc)}  email {l.CustomerEmail ?? None}  order reference {latest.OrderRef ?? None}");
        }

        console.WriteLine($"{rows.Count} {(rows.Count == 1 ? "license" : "licenses")}.");
    }

    /// <summary>One license: its header, then every issue entry, newest first.</summary>
    internal static void Show(Session session)
    {
        var license = LicenseQuestion.Ask(session);
        var prefix = LicenseActions.ReadPrefix(session);
        var console = session.Console;

        List<IssueEntry> entries;
        using (var db = session.OpenDb())
            entries = NewestFirst(db.IssueEntries.AsNoTracking().Where(e => e.Reference == license.Reference).ToList());

        console.WriteLine($"{Display(prefix, license.Reference)}");
        console.WriteLine($"  Product: {license.ProductId}");
        console.WriteLine($"  Customer email: {license.CustomerEmail ?? None}");
        console.WriteLine($"  Issue entries, newest first: {entries.Count}");
        foreach (var e in entries)
        {
            console.WriteLine($"  {Time(e.IssuedUtc)}  {e.Kind.ToString().ToLowerInvariant()}");
            console.WriteLine($"    Expiry: {Expiry(e.ExpiresUtc)}");
            console.WriteLine($"    Features: {LicenseActions.DescribeAll(LicenseActions.FeaturesJson.Read(e.FeaturesJson))}");
            console.WriteLine($"    Order reference: {e.OrderRef ?? None}");
            console.WriteLine($"    Key identifier: {e.KeyIdentifier}");
            console.WriteLine($"    Signing key: {e.SigningKeyId}");
        }
    }

    /// <summary>The license, then one change to its customer fields.</summary>
    internal static void CustomerDetails(Session session)
    {
        var license = LicenseQuestion.Ask(session);
        var prefix = LicenseActions.ReadPrefix(session);
        var display = Display(prefix, license.Reference);
        var console = session.Console;
        var questions = session.Questions;

        var choice = questions.AskChoice("Customer details", CustomerChoices).Id;
        switch (choice)
        {
            case "change-email":
                console.WriteLine($"Customer email: {license.CustomerEmail ?? None}");
                var email = questions.AskText(
                    "New customer email", TypedValues.EmailAccepts, TypedValues.CheckCustomerEmail, proposed: license.CustomerEmail).Value;
                if (email == license.CustomerEmail)
                {
                    console.WriteLine(SettingsActions.Unchanged);
                    return;
                }

                SetEmail(session, license.Reference, email);
                console.WriteLine($"The customer email of {display} is now {email}.");
                break;

            case "clear-email":
                if (license.CustomerEmail is null)
                {
                    console.WriteLine(NoEmail);
                    return;
                }

                console.WriteLine($"License: {display}");
                console.WriteLine($"Customer email to clear: {license.CustomerEmail}");
                if (!questions.AskYesNo("Clear this customer email? It cannot be undone.", proposed: false))
                {
                    console.WriteLine(Declined);
                    return;
                }

                SetEmail(session, license.Reference, null);
                console.WriteLine($"The customer email of {display} is cleared.");
                break;

            case "correct-order-reference":
                CorrectLatestOrderReference(session, license, display);
                break;

            default:
                ClearOrderReferences(session, license, display);
                break;
        }
    }

    private static void SetEmail(Session session, string reference, string? email)
    {
        using var db = session.OpenDb();
        using var transaction = db.Database.BeginTransaction();
        var stored = db.Licenses.Single(l => l.Reference == reference);
        stored.CustomerEmail = email;
        stored.CustomerEmailKey = email is null ? null : TypedValues.MatchKey(email); // typed, so it passed the email rule
        db.SaveChanges();
        transaction.Commit();
    }

    private static void CorrectLatestOrderReference(Session session, LicenseRecord license, string display)
    {
        var console = session.Console;
        IssueEntry latest;
        using (var db = session.OpenDb())
            latest = NewestFirst(db.IssueEntries.AsNoTracking().Where(e => e.Reference == license.Reference).ToList()).FirstOrDefault()
                ?? throw new InstallationRefusedException(ActiveKeys.Damaged($"The license {license.Reference} has no issue entry"));

        console.WriteLine($"Order reference of the latest issue entry: {latest.OrderRef ?? None}");
        var orderRef = session.Questions.AskText(
            "New order reference", TypedValues.OrderRefAccepts, TypedValues.CheckOrderReference, proposed: latest.OrderRef).Value;
        if (orderRef == latest.OrderRef)
        {
            console.WriteLine(SettingsActions.Unchanged);
            return;
        }

        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            var stored = db.IssueEntries.Single(e => e.Id == latest.Id);
            stored.OrderRef = orderRef;
            stored.OrderRefKey = TypedValues.MatchKey(orderRef); // typed, so it passed the order reference rule
            db.SaveChanges();
            transaction.Commit();
        }

        console.WriteLine($"The order reference of the latest issue entry of {display} is now {orderRef}.");
    }

    private static void ClearOrderReferences(Session session, LicenseRecord license, string display)
    {
        var console = session.Console;
        List<IssueEntry> shown;
        using (var db = session.OpenDb())
            shown = NewestFirst(db.IssueEntries.AsNoTracking()
                .Where(e => e.Reference == license.Reference && (e.OrderRef != null || e.OrderRefKey != null)).ToList()).ToList();

        if (shown.Count == 0)
        {
            console.WriteLine(NoOrderReferences);
            return;
        }

        // Entries without an order reference are left out: only the values to be cleared are listed.
        console.WriteLine($"License: {display}");
        console.WriteLine("Order references to clear:");
        foreach (var e in shown.Where(e => e.OrderRef is not null))
            console.WriteLine($"  {Time(e.IssuedUtc)}  {e.Kind.ToString().ToLowerInvariant()}  {e.OrderRef}");
        if (!session.Questions.AskYesNo("Clear these order references? They cannot be undone.", proposed: false))
        {
            console.WriteLine(Declined);
            return;
        }

        int cleared;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            var held = db.IssueEntries.Where(e => e.Reference == license.Reference && (e.OrderRef != null || e.OrderRefKey != null)).ToList();
            foreach (var entry in held)
            {
                entry.OrderRef = null;
                entry.OrderRefKey = null;
            }

            db.SaveChanges();
            transaction.Commit();
            cleared = held.Count;
        }

        session.Console.WriteLine(cleared == 0
            ? NoOrderReferences
            : $"Cleared the order {(cleared == 1 ? "reference" : "references")} of {cleared} issue {(cleared == 1 ? "entry" : "entries")} of {display}.");
    }
}
