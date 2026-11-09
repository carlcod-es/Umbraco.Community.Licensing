using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The Signing keys menu: Trusted keys, Release confirmed and Reissues sent (vendor-tool-signing-keys, PDR-0035, PDR-0051). None
/// needs the passphrase. The two that write do it in one transaction that re-checks what the vendor was shown.
/// </summary>
internal static class TrustedKeyActions
{
    /// <summary>Starts a comment line in the printed list; every other line is one public key line, as <c>SigningPublicKey.Parse</c> reads it.</summary>
    internal const string CommentPrefix = "// ";

    internal const string ReleaseDeclined = "Nothing was changed: no key was marked as in a release.";
    internal const string ReissuesDeclined = "Nothing was changed: the reissues were not marked as sent.";
    internal const string NoLeakedKeys = "No signing key replaced for a leak is waiting for its reissues to be sent.";

    internal static string AlreadyInRelease(string product) => $"Every key in the list is already in a release for {product}.";

    public static void Register(ActionTable table)
    {
        table.Register("keys.trusted", Trusted);
        table.Register("keys.release", ReleaseConfirmed);
        table.Register("keys.reissues", ReissuesSent);
    }

    /// <summary>
    /// The keys a release of the scope must trust, in print order: active, standby, then the replaced keys, oldest first. Replaced keys
    /// are listed except one replaced for a leak whose reissues are confirmed sent. Zero or several active keys, or several standby
    /// keys, fail as a damaged installation.
    /// </summary>
    internal static List<SigningKeyRecord> ListFor(ToolDbContext db, string? scope)
    {
        var inScope = db.SigningKeys.Where(k => k.ProductId == scope).ToList();
        var (active, standby) = ActiveKeys.Roles(inScope.Where(k => k.Role != KeyRole.Replaced), scope);

        List<SigningKeyRecord> list = [active];
        if (standby is not null)
            list.Add(standby);
        list.AddRange(inScope
            .Where(k => k.Role == KeyRole.Replaced && !(k.ReplacedReason == ReplacedReason.Leak && k.ReissuesSentUtc is not null))
            .OrderBy(k => k.ReplacedUtc).ThenBy(k => k.SigningKeyId, StringComparer.Ordinal));
        return list;
    }

    /// <summary>The role named in the comment line before a key.</summary>
    internal static string RoleOf(SigningKeyRecord key) => key.Role switch
    {
        KeyRole.Active => "active",
        KeyRole.Standby => "standby",
        _ when key.ReplacedReason == ReplacedReason.Leak => "replaced (leak) - LEAKED, stays listed until the reissues are sent",
        _ => $"replaced ({key.ReplacedReason?.ToString().ToLowerInvariant()})",
    };

    private static void WriteList(ToolConsole console, string product, string? scope, List<SigningKeyRecord> keys)
    {
        console.WriteLine($"{CommentPrefix}Trusted public keys for {product}{(scope is null ? " (the shared signing keys)" : "")}");
        foreach (var key in keys)
        {
            console.WriteLine(CommentPrefix + RoleOf(key));
            console.WriteLine(key.PublicKey);
        }
    }

    /// <summary>
    /// Asks for the product (also in shared mode, where each product has its own release) and reads its list. Null when the
    /// installation has no products. The keys are the product's in per-product mode, the shared keys in shared mode.
    /// </summary>
    private static (string Product, string? Scope, List<SigningKeyRecord> Keys)? ChooseProduct(Session session)
    {
        var (perProduct, _) = ProductActions.ReadKeyMode(session);
        using var db = session.OpenDb();
        var names = db.Products.AsNoTracking().ToList().OrderBy(p => p.ProductId, StringComparer.Ordinal).ToList();
        if (names.Count == 0)
        {
            session.Console.WriteLine(ProductActions.NoProducts);
            return null;
        }

        var product = session.Questions.AskChoice("Product", [.. names.Select(p => new Choice(p.ProductId, $"{p.ProductId} - {p.DisplayName}"))]).Id;
        var scope = perProduct ? product : null;
        return (product, scope, ListFor(db, scope));
    }

    internal static void Trusted(Session session)
    {
        if (ChooseProduct(session) is not var (product, scope, keys))
            return;

        WriteList(session.Console, product, scope, keys);
    }

    internal static void ReleaseConfirmed(Session session)
    {
        if (ChooseProduct(session) is not var (product, scope, keys))
            return;

        var console = session.Console;
        WriteList(console, product, scope, keys);

        HashSet<string> released;
        using (var db = session.OpenDb())
            released = [.. db.KeyReleases.AsNoTracking().Where(r => r.ProductId == product).Select(r => r.SigningKeyId)];

        var lacking = keys.Where(k => !released.Contains(k.SigningKeyId)).ToList();
        if (lacking.Count == 0)
        {
            console.WriteLine(AlreadyInRelease(product));
            return;
        }

        if (!session.Questions.AskYesNo($"Does a release of {product} trust this list?", proposed: false))
        {
            console.WriteLine(ReleaseDeclined);
            return;
        }

        var now = session.Environment.TimeProvider.GetUtcNow().UtcDateTime;
        int marked;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            marked = CommitRelease(db, product, scope, [.. keys.Select(k => k.SigningKeyId)], now);
            transaction.Commit();
        }

        console.WriteLine($"Marked {marked} {(marked == 1 ? "key" : "keys")} as in a release for {product}.");
    }

    /// <summary>
    /// The write of Release confirmed, inside the caller's transaction: re-checks that the product still exists and its list is the one
    /// shown, then inserts a row for each listed key that lacks one for this product. Returns the rows inserted.
    /// </summary>
    internal static int CommitRelease(ToolDbContext db, string product, string? scope, IReadOnlyCollection<string> shown, DateTime now)
    {
        if (!db.Products.Any(p => p.ProductId == product))
            throw new InvalidOperationException($"The product {product} no longer exists, so no key is marked.");

        var current = ListFor(db, scope);
        if (!current.Select(k => k.SigningKeyId).ToHashSet(StringComparer.Ordinal).SetEquals(shown))
            throw new InvalidOperationException($"The trusted keys of {product} changed since the list was shown, so no key is marked.");

        var released = db.KeyReleases.Where(r => r.ProductId == product).Select(r => r.SigningKeyId).ToHashSet();
        var added = 0;
        foreach (var key in current.Where(k => !released.Contains(k.SigningKeyId)))
        {
            db.KeyReleases.Add(new KeyRelease { SigningKeyId = key.SigningKeyId, ProductId = product, ConfirmedUtc = now });
            added++;
        }

        db.SaveChanges();
        return added;
    }

    internal static void ReissuesSent(Session session)
    {
        var (perProduct, _) = ProductActions.ReadKeyMode(session);

        List<SigningKeyRecord> waiting;
        using (var db = session.OpenDb())
        {
            waiting = db.SigningKeys.AsNoTracking()
                .Where(k => k.Role == KeyRole.Replaced && k.ReplacedReason == ReplacedReason.Leak && k.ReissuesSentUtc == null)
                .ToList()
                .OrderBy(k => k.ReplacedUtc).ThenBy(k => k.SigningKeyId, StringComparer.Ordinal).ToList();
        }

        var console = session.Console;
        if (waiting.Count == 0)
        {
            console.WriteLine(NoLeakedKeys);
            return;
        }

        string Scope(SigningKeyRecord k) => perProduct ? $"the signing key of {k.ProductId}" : "the shared signing key";
        var key = session.Questions.AskChoice(
            "Signing key replaced for a leak",
            [.. waiting.Select(k => new Choice(k.SigningKeyId, $"{k.SigningKeyId} - replaced {k.ReplacedUtc:yyyy-MM-dd}, {Scope(k)}"))]);
        var chosen = waiting.Single(k => k.SigningKeyId == key.Id);

        if (!session.Questions.AskYesNo($"Are the reissues of every license signed by {chosen.SigningKeyId} sent?", proposed: false))
        {
            console.WriteLine(ReissuesDeclined);
            return;
        }

        var now = session.Environment.TimeProvider.GetUtcNow().UtcDateTime;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            CommitReissuesSent(db, chosen.SigningKeyId, now);
            transaction.Commit();
        }

        console.WriteLine($"Reissues for {chosen.SigningKeyId} are marked as sent; it is no longer in the trusted keys of {Scope(chosen)}.");
    }

    /// <summary>The write of Reissues sent, inside the caller's transaction: re-checks the key is still replaced for a leak with its reissues not yet sent.</summary>
    internal static void CommitReissuesSent(ToolDbContext db, string signingKeyId, DateTime now)
    {
        var key = db.SigningKeys.SingleOrDefault(k => k.SigningKeyId == signingKeyId && k.Role == KeyRole.Replaced && k.ReplacedReason == ReplacedReason.Leak && k.ReissuesSentUtc == null)
            ?? throw new InvalidOperationException($"The signing key {signingKeyId} is no longer replaced for a leak with its reissues not sent, so nothing is marked.");

        key.ReissuesSentUtc = now;
        db.SaveChanges();
    }
}
