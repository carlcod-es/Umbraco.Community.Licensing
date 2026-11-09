using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

using FeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

/// <summary>
/// Licenses &gt; Reissue, Renew and Resend (vendor-tool-licenses): a new key for an existing license, from its record. The license is chosen by
/// <see cref="LicenseQuestion"/>; product and reference are kept (PDR-0021); the questions come first, then the summary, the passphrase, the
/// signing and one transaction writing a new issue entry (design.md, Menu session).
/// </summary>
internal static class ReissueActions
{
    internal static void Reissue(Session session) => Run(session, IssueKind.Reissue);

    internal static void Renew(Session session) => Run(session, IssueKind.Renewal);

    internal static void Resend(Session session) => Run(session, IssueKind.Resend);

    private static void Run(Session session, IssueKind kind)
    {
        var console = session.Console;
        var questions = session.Questions;
        var clock = session.Environment.TimeProvider;

        var license = LicenseQuestion.Ask(session);
        var prefix = LicenseActions.ReadPrefix(session);

        Product product;
        List<IssueEntry> entries;
        using (var db = session.OpenDb())
        {
            product = db.Products.AsNoTracking().SingleOrDefault(p => p.ProductId == license.ProductId)
                ?? throw new InstallationRefusedException(ActiveKeys.Damaged($"The license {license.Reference} belongs to a product that is not recorded"));
            entries = db.IssueEntries.AsNoTracking().Where(e => e.Reference == license.Reference).ToList();
        }

        // The latest entry holds the current values; the same order the expiry suggestion reads them in.
        var current = entries.OrderBy(e => e.IssuedUtc).ThenBy(e => e.Id).LastOrDefault()
            ?? throw new InstallationRefusedException(ActiveKeys.Damaged($"The license {license.Reference} has no issue entry"));
        var definitions = ProductActions.ReadFeatures(session, product.ProductId);
        var signingKey = ActiveKeys.Find(session, product.ProductId);
        var display = KeyIdentifierFormat.DisplayReference(prefix.Value.Length == 0 ? null : prefix.Value, license.Reference);
        var currentFeatures = LicenseActions.FeaturesJson.Read(current.FeaturesJson);
        console.WriteLine($"{display} for {product.ProductId}: current values are shown; Enter keeps them.");

        TypedValues.ExpiryAnswer? expiry = null;
        string? displayName = current.DisplayName;
        string? vendorTag = current.VendorTag;
        var features = currentFeatures;
        string? orderRef = null;
        var customerEmail = license.CustomerEmail;

        if (kind == IssueKind.Resend)
        {
            // Nothing is asked: the expiry is the stored one, which the library refuses once it has passed.
            var stored = current.ExpiresUtc is { } e ? e.ToString(ExpiryFormat, CultureInfo.InvariantCulture) : TypedValues.PerpetualWord;
            var broken = TypedValues.ReadExpiry(stored, clock.GetUtcNow(), out expiry);
            if (broken is not null)
            {
                console.WriteLine($"This license cannot be resent. {broken} Renew or reissue it with a later expiry instead. Nothing is stored.");
                return;
            }
        }
        else
        {
            var proposed = ExpirySuggestion.Suggest(entries, current.ExpiresUtc, kind);
            expiry = AskExpiry(session, proposed);
            if (kind == IssueKind.Reissue)
            {
                displayName = LicenseActions.AskCurrent(questions, "Display name", TypedValues.DisplayNameAccepts, TypedValues.CheckDisplayNameText, current.DisplayName, allowRemove: true);
                vendorTag = LicenseActions.AskCurrent(questions, "Vendor tag", TypedValues.VendorTagAccepts, TypedValues.CheckVendorTag, current.VendorTag, allowRemove: true);
                features = LicenseActions.AskFeatures(questions, definitions, currentFeatures);
            }

            orderRef = LicenseActions.AskOptional(questions, "Order reference", TypedValues.OrderRefAccepts, TypedValues.CheckOrderReference);
            customerEmail = LicenseActions.AskCurrent(questions, "Customer email", TypedValues.EmailAccepts, TypedValues.CheckCustomerEmail, license.CustomerEmail);
        }

        var libraryDefinitions = LicenseActions.ToLibrary(definitions);
        if (!LicenseActions.CheckBeforeSummary(console, product.ProductId, displayName, vendorTag, features, libraryDefinitions))
            return;

        LicenseActions.WriteSummary(console, product, display, expiry!.ExpiresUtc, displayName, vendorTag, features, customerEmail, orderRef);
        if (!questions.AskYesNo(LicenseActions.SignQuestion, proposed: true))
        {
            console.WriteLine(LicenseActions.NotSigned);
            return;
        }

        var request = new LicenseRequest
        {
            Product = product.ProductId,
            Reference = license.Reference,
            Expiry = expiry.Expiry,
            DisplayName = displayName,
            VendorTag = vendorTag,
            Features = features,
        };
        var issued = LicenseActions.Sign(session, request, prefix, libraryDefinitions, signingKey);

        if (!KeyIdentifierFormat.TryReferenceOfDisplay(issued.Reference, out var reference) || reference != license.Reference)
            throw new InvalidOperationException("The reissued license does not keep the reference.");

        var issuedUtc = issued.Issued.UtcDateTime;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            if (customerEmail != license.CustomerEmail)
            {
                var stored = db.Licenses.Single(l => l.Reference == license.Reference);
                stored.CustomerEmail = customerEmail;
                stored.CustomerEmailKey = customerEmail is null ? null : TypedValues.MatchKey(customerEmail); // typed, so it passed the email rule
            }

            db.IssueEntries.Add(new IssueEntry
            {
                Reference = license.Reference,
                Kind = kind,
                IssuedUtc = issuedUtc,
                ExpiresUtc = expiry.ExpiresUtc,
                DisplayName = displayName,
                VendorTag = vendorTag,
                FeaturesJson = LicenseActions.FeaturesJson.Write(features),
                OrderRef = orderRef,
                OrderRefKey = orderRef is null ? null : TypedValues.MatchKey(orderRef),
                KeyIdentifier = issued.KeyIdentifier,
                SigningKeyId = signingKey.SigningKeyId,
            });
            db.SaveChanges();
            transaction.Commit();
        }

        LicenseActions.ShowKey(session, issued);
    }

    internal const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// Asks the expiry with the suggestion as the proposed answer. A proposed answer is not run through the rule by the question, and a
    /// suggestion can lie in the past (after a lapse), so the answer is read here and the question asked again when the library refuses it.
    /// </summary>
    private static TypedValues.ExpiryAnswer AskExpiry(Session session, DateTime? proposed)
    {
        var clock = session.Environment.TimeProvider;
        var proposedText = proposed is { } p ? p.ToString(ExpiryFormat, CultureInfo.InvariantCulture) : TypedValues.PerpetualWord;
        while (true)
        {
            var answer = session.Questions.AskText("Expiry", TypedValues.ExpiryAccepts, text => TypedValues.CheckExpiry(text, clock.GetUtcNow()), proposedText);
            var broken = TypedValues.ReadExpiry(answer.Value, clock.GetUtcNow(), out var expiry);
            if (broken is null)
                return expiry!;
            session.Console.WriteLine(broken);
        }
    }
}
