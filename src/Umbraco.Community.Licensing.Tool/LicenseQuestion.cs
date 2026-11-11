using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The license question (design.md, Choosing a license; ADR-0022), one helper for every action that asks for a license: a reference typed
/// under the prefix rule (PDR-0023), or a whole key string signed by one of the installation's keys. It needs no passphrase and neither
/// stores nor writes the key string.
/// </summary>
internal static class LicenseQuestion
{
    internal const string Question = "License";

    internal const string NotIssuedHere = "That key was not issued by this installation.";

    internal static string NoRecord(string display) => $"There is no record of the license {display}.";

    internal static string NotReferenceOrKey(string referenceRule) =>
        $"That is not a license reference or a key string. {referenceRule} A key string is pasted whole.";

    /// <summary>What the question accepts, for its hint: a reference written the way the current prefix gives it, or a key string.</summary>
    internal static string Accepts(KeyPrefix prefix) =>
        $"a license reference like {KeyIdentifierFormat.DisplayReference(prefix.Value.Length == 0 ? null : prefix.Value, "8F3AKM7RXB")}, or a whole key string";

    /// <summary>
    /// Asks until the answer chooses a license with a record, and returns that record. A refusal names its reason and asks again;
    /// <c>/cancel</c> ends the action as at any question.
    /// </summary>
    public static LicenseRecord Ask(Session session) => AskCore(session, optional: false)!;

    /// <summary>As <see cref="Ask"/>, but an empty answer skips the question (null): the license filter of Find and list.</summary>
    public static LicenseRecord? AskOrSkip(Session session) => AskCore(session, optional: true);

    private static LicenseRecord? AskCore(Session session, bool optional)
    {
        var prefix = LicenseActions.ReadPrefix(session);
        LicenseRecord? chosen = null;
        session.Questions.AskText(
            Question,
            optional ? Accepts(prefix) + ", or Enter to leave it out" : Accepts(prefix),
            text => optional && text.Length == 0 ? null : Choose(session, prefix, text, out chosen));
        return chosen;
    }

    /// <summary>The rule broken, or null with <paramref name="record"/> set.</summary>
    private static string? Choose(Session session, KeyPrefix prefix, string text, out LicenseRecord? record)
    {
        record = null;
        var formatPrefix = prefix.Value.Length == 0 ? null : prefix.Value;

        string reference;
        if (ReferenceParser.TryParse(text, formatPrefix, out var typed, out var problem))
        {
            reference = typed;
        }
        else
        {
            var read = ReadKey(session, text);
            if (read.Refusal is not null)
                return read.Refusal(problem!.Message);
            reference = read.Reference!;
        }

        using var db = session.OpenDb();
        record = db.Licenses.AsNoTracking().SingleOrDefault(l => l.Reference == reference);
        return record is null ? NoRecord(KeyIdentifierFormat.DisplayReference(formatPrefix, reference)) : null;
    }

    private sealed record KeyRead(string? Reference, Func<string, string>? Refusal);

    /// <summary>Evaluates a key string against the products' keys: once against the first product, and once against the product it claims.</summary>
    private static KeyRead ReadKey(Session session, string keyString)
    {
        var (perProduct, _) = ProductActions.ReadKeyMode(session);
        List<string> products;
        List<SigningKeyRecord> keys;
        using (var db = session.OpenDb())
        {
            products = db.Products.AsNoTracking().Select(p => p.ProductId).ToList().Order(StringComparer.Ordinal).ToList();
            keys = db.SigningKeys.AsNoTracking().ToList();
        }

        var notHere = new KeyRead(null, _ => NotIssuedHere);
        if (products.Count == 0)
            return notHere;

        // Every key the installation holds for a product, whatever its role or why it was replaced; shared keys serve every product.
        LicenseResult? Evaluate(string productId)
        {
            var held = keys.Where(k => perProduct ? k.ProductId == productId : k.ProductId is null).Select(k => SigningPublicKey.Parse(k.PublicKey));
            var trusted = TrustedSigningKeys.Create(held);
            return trusted.SigningKeyIds.Count == 0 ? null : new LicenseEvaluator(productId, trusted, session.Environment.TimeProvider).Evaluate(keyString);
        }

        var result = Evaluate(products[0]);
        if (result is { State: LicenseState.WrongProduct, ClaimedProduct: { } claimed })
        {
            if (!products.Contains(claimed, StringComparer.Ordinal))
                return notHere;
            result = Evaluate(claimed);
        }

        switch (result?.State)
        {
            case LicenseState.Valid or LicenseState.Expired when result.License is not null
                && KeyIdentifierFormat.TryReferenceOfDisplay(result.License.Reference, out var reference):
                return new KeyRead(reference, null);
            case null or LicenseState.WrongProduct or LicenseState.SigningKeyNotRecognised:
                return notHere;
            default:
                return new KeyRead(null, NotReferenceOrKey);
        }
    }
}
