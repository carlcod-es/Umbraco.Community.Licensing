using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The Signing keys menu, so far: status, adding a standby key and switching (vendor-tool-signing-keys). Status needs no
/// passphrase. Adding a standby key and switching ask their questions first, then the passphrase (ADR-0021), then write in one transaction.
/// </summary>
internal static class SigningKeyActions
{
    internal const string NothingToAdd = "Every signing key already has a standby key, so there is nothing to add.";
    internal const string AllHaveStandby = "Every signing key listed has a standby key.";
    internal const string SharedHasStandby = "The shared signing key has a standby key.";

    internal static string AlreadyHasStandby(string what) => $"{what} already has a standby key, so none is added.";

    public static void Register(ActionTable table)
    {
        table.Register("keys.status", Status);
        table.Register("keys.standby", AddStandby);
        table.Register("keys.switch", Switch);
    }

    /// <summary>The products a key signs for: its product in per-product mode, every product in shared mode.</summary>
    private static List<string> ProductsOf(SigningKeyRecord key, bool perProduct, IReadOnlyList<string> allProducts) =>
        perProduct ? (key.ProductId is null ? [] : [key.ProductId]) : [.. allProducts];

    internal static void Status(Session session)
    {
        var (perProduct, _) = ProductActions.ReadKeyMode(session);
        var onlyWithout = session.Questions.AskYesNo("Only those without a standby key?", proposed: false);

        List<string> products;
        Dictionary<string, string> names;
        List<SigningKeyRecord> keys;
        HashSet<(string, string)> released;
        using (var db = session.OpenDb())
        {
            names = db.Products.AsNoTracking().ToDictionary(p => p.ProductId, p => p.DisplayName);
            products = [.. names.Keys.Order(StringComparer.Ordinal)];
            keys = db.SigningKeys.AsNoTracking().Where(k => k.Role != KeyRole.Replaced).ToList();
            released = db.KeyReleases.AsNoTracking().ToList().Select(r => (r.SigningKeyId, r.ProductId)).ToHashSet();
        }

        var console = session.Console;
        var shown = 0;

        if (perProduct)
        {
            foreach (var product in products)
            {
                var (active, standby) = ActiveKeys.Roles(keys, product);
                if (onlyWithout && standby is not null)
                    continue;

                shown++;
                console.WriteLine($"{product} - {names[product]}");
                WriteKeys(console, active, standby, released, perProduct, products);
            }

            if (products.Count == 0)
                console.WriteLine(ProductActions.NoProducts);
            else if (shown == 0)
                console.WriteLine(AllHaveStandby);
        }
        else
        {
            var (active, standby) = ActiveKeys.Roles(keys, null);
            if (onlyWithout && standby is not null)
            {
                console.WriteLine(SharedHasStandby);
                return;
            }

            console.WriteLine("Shared signing key");
            WriteKeys(console, active, standby, released, perProduct, products);
            console.WriteLine($"  Products: {(products.Count == 0 ? "none" : string.Join(", ", products))}");
        }
    }

    private static void WriteKeys(
        ToolConsole console,
        SigningKeyRecord active,
        SigningKeyRecord? standby,
        HashSet<(string, string)> released,
        bool perProduct,
        IReadOnlyList<string> products)
    {
        console.WriteLine($"  Signing key: {active.SigningKeyId}");
        if (standby is null)
        {
            console.WriteLine("  Standby key: none");
            return;
        }

        var lacking = ProductsOf(standby, perProduct, products).Where(p => !released.Contains((standby.SigningKeyId, p))).ToList();
        console.WriteLine(lacking.Count == 0
            ? $"  Standby key: {standby.SigningKeyId}"
            : $"  Standby key: {standby.SigningKeyId} - not yet in a release for {string.Join(", ", lacking)}");
    }

    internal static void AddStandby(Session session)
    {
        var (perProduct, _) = ProductActions.ReadKeyMode(session);

        // Active keys without a standby: per-product by product, in shared mode the shared key. A damaged scope fails here.
        List<SigningKeyRecord> without = [];
        bool hasProducts;
        using (var db = session.OpenDb())
        {
            hasProducts = db.Products.Any();
            var keys = db.SigningKeys.AsNoTracking().Where(k => k.Role != KeyRole.Replaced).ToList();
            var scopes = perProduct
                ? db.Products.AsNoTracking().Select(p => p.ProductId).ToList().Order(StringComparer.Ordinal).Select(p => (string?)p).ToList()
                : [(string?)null];
            foreach (var scopeId in scopes)
            {
                var (active, standby) = ActiveKeys.Roles(keys, scopeId);
                if (standby is null)
                    without.Add(active);
            }
        }

        if (perProduct && without.Count == 0 && !hasProducts)
        {
            session.Console.WriteLine(ProductActions.NoProducts);
            return;
        }

        if (without.Count == 0)
        {
            session.Console.WriteLine(NothingToAdd);
            return;
        }

        var choice = session.Questions.AskChoice(
            perProduct ? "Product" : "Signing key",
            [.. without.Select(k => new Choice(k.ProductId ?? k.SigningKeyId, k.ProductId is null ? $"The shared signing key {k.SigningKeyId}" : $"{k.ProductId} - signing key {k.SigningKeyId}"))]);
        var target = without.Single(k => (k.ProductId ?? k.SigningKeyId) == choice.Id);
        var scope = target.ProductId is null ? "The shared signing key" : $"The signing key of {target.ProductId}";

        // The passphrase is asked after the last question. The callback only makes and encrypts the key (ADR-0014).
        var iterations = session.Environment.Pbkdf2Iterations;
        var made = KeyProtection.WithPassphrase(session, passphrase => KeyProtection.CreateEncrypted(passphrase, iterations));

        var now = session.Environment.TimeProvider.GetUtcNow().UtcDateTime;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            CommitStandby(db, target, made, now, scope);
            transaction.Commit();
        }

        session.Console.WriteLine($"Added standby key {made.SigningKeyId} to {scope.ToLowerInvariant()}.");
        session.Console.WriteLine(target.ProductId is null
            ? "It is not yet in a release for any product."
            : $"It is not yet in a release for {target.ProductId}.");
    }

    /// <summary>The write of Add a standby key, inside the caller's transaction: re-checks what the questions were based on, then adds the key.</summary>
    internal static void CommitStandby(ToolDbContext db, SigningKeyRecord target, KeyProtection.NewKey made, DateTime now, string scope)
    {
        if (!db.SigningKeys.Any(k => k.SigningKeyId == target.SigningKeyId && k.Role == KeyRole.Active))
            throw new InvalidOperationException($"{scope} is no longer the active key, so no standby key is added.");
        if (db.SigningKeys.Any(k => k.Role == KeyRole.Standby && k.ProductId == target.ProductId))
            throw new InvalidOperationException(AlreadyHasStandby(scope));

        // A new standby key has no KeyReleases rows: it is in a release for no product yet.
        db.SigningKeys.Add(new SigningKeyRecord
        {
            SigningKeyId = made.SigningKeyId,
            ProductId = target.ProductId,
            PublicKey = made.PublicKey,
            EncryptedPrivateKeyPem = made.EncryptedPrivateKeyPem,
            Role = KeyRole.Standby,
            CreatedUtc = now,
        });
        db.SaveChanges();
    }

    internal const string SwitchDeclined = "Nothing was changed: the signing key was not switched.";

    private static readonly Choice[] Reasons =
    [
        new("routine", "routine - a planned rotation"),
        new("loss", "loss - the private key was lost"),
        new("leak", "leak - the private key may have been exposed"),
    ];

    /// <summary>
    /// Switch (PDR-0035, PDR-0047, PDR-0050, PDR-0051): the standby becomes active and a new standby is made, or, with no
    /// standby, a new key pair becomes active. The warning and confirmation come before the passphrase (ADR-0021).
    /// </summary>
    internal static void Switch(Session session)
    {
        var (perProduct, _) = ProductActions.ReadKeyMode(session);

        List<string> products;
        List<SigningKeyRecord> keys;
        HashSet<(string, string)> released;
        using (var db = session.OpenDb())
        {
            products = [.. db.Products.AsNoTracking().Select(p => p.ProductId).ToList().Order(StringComparer.Ordinal)];
            keys = db.SigningKeys.AsNoTracking().Where(k => k.Role != KeyRole.Replaced).ToList();
            released = db.KeyReleases.AsNoTracking().ToList().Select(r => (r.SigningKeyId, r.ProductId)).ToHashSet();
        }

        // The key to switch: a product's in per-product mode; the shared key is the only entry in shared mode, shown so the vendor sees which key.
        string? productId;
        if (perProduct)
        {
            if (products.Count == 0)
            {
                session.Console.WriteLine(ProductActions.NoProducts);
                return;
            }

            productId = session.Questions.AskChoice("Product", [.. products.Select(p => new Choice(p, $"{p} - signing key {ActiveKeys.Roles(keys, p).Active.SigningKeyId}"))]).Id;
        }
        else
        {
            var shared = ActiveKeys.Roles(keys, null).Active;
            session.Questions.AskChoice("Signing key", [new Choice(shared.SigningKeyId, $"The shared signing key {shared.SigningKeyId}")]);
            productId = null;
        }

        var (active, standby) = ActiveKeys.Roles(keys, productId);
        var scope = productId is null ? "The shared signing key" : $"The signing key of {productId}";
        var reason = Enum.Parse<ReplacedReason>(session.Questions.AskChoice("Reason", Reasons).Id, ignoreCase: true);

        // The products the key becoming active signs for, and those it is not yet in a release for. A new key pair is in none.
        // Shared mode with no products has nothing to name, so no warning and no confirmation (the spec asks for one only when products lack the release).
        List<string> signsFor = productId is null ? products : [productId];
        var lacking = standby is null ? signsFor : [.. signsFor.Where(p => !released.Contains((standby.SigningKeyId, p)))];
        if (lacking.Count > 0)
        {
            var becoming = standby is null ? "A new signing key" : $"The standby key {standby.SigningKeyId}";
            session.Console.WriteLine($"{becoming} is not yet in a release for {string.Join(", ", lacking)}.");
            session.Console.WriteLine("Sites on releases that do not trust it will report keys signed by it as signed by a key not recognised.");
            if (!session.Questions.AskYesNo("Switch anyway?", proposed: false))
            {
                session.Console.WriteLine(SwitchDeclined);
                return;
            }
        }

        // The passphrase is asked after the last question and the confirmation. The callback only makes and encrypts the key (ADR-0014).
        var iterations = session.Environment.Pbkdf2Iterations;
        var made = KeyProtection.WithPassphrase(session, passphrase => KeyProtection.CreateEncrypted(passphrase, iterations));

        var now = session.Environment.TimeProvider.GetUtcNow().UtcDateTime;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            CommitSwitch(db, active, standby, made, reason, now, scope);
            transaction.Commit();
        }

        var console = session.Console;
        if (standby is not null)
        {
            console.WriteLine($"{scope} is now {standby.SigningKeyId}; {active.SigningKeyId} is replaced ({reason.ToString().ToLowerInvariant()}).");
            console.WriteLine($"Added standby key {made.SigningKeyId}, which has no release marks yet.");
        }
        else
        {
            console.WriteLine($"{scope} is now {made.SigningKeyId}, a new key pair; {active.SigningKeyId} is replaced ({reason.ToString().ToLowerInvariant()}).");
            console.WriteLine("It has no release marks yet, and there is no standby key (Add a standby key makes one).");
        }
    }

    /// <summary>
    /// The write of Switch, inside the caller's transaction: re-checks that the active and standby keys read before the questions
    /// still hold those roles, then replaces the active key and checks one active key is left.
    /// </summary>
    internal static void CommitSwitch(
        ToolDbContext db,
        SigningKeyRecord active,
        SigningKeyRecord? standby,
        KeyProtection.NewKey made,
        ReplacedReason reason,
        DateTime now,
        string scope)
    {
        var current = db.SigningKeys.Where(k => k.Role != KeyRole.Replaced && k.ProductId == active.ProductId).ToList();
        var oldActive = current.SingleOrDefault(k => k.SigningKeyId == active.SigningKeyId && k.Role == KeyRole.Active)
            ?? throw new InvalidOperationException($"{scope} is no longer the active key, so the signing key is not switched.");
        var standbyNow = current.Where(k => k.Role == KeyRole.Standby).ToList();
        if (standbyNow.Count != (standby is null ? 0 : 1) || (standby is not null && standbyNow[0].SigningKeyId != standby.SigningKeyId))
            throw new InvalidOperationException($"{scope} has a different standby key than when you chose, so the signing key is not switched.");

        oldActive.Role = KeyRole.Replaced;
        oldActive.ReplacedUtc = now;
        oldActive.ReplacedReason = reason;

        // A new standby, and a new key pair that becomes active, have no KeyReleases rows: in a release for no product yet.
        var fresh = new SigningKeyRecord
        {
            SigningKeyId = made.SigningKeyId,
            ProductId = active.ProductId,
            PublicKey = made.PublicKey,
            EncryptedPrivateKeyPem = made.EncryptedPrivateKeyPem,
            Role = standby is null ? KeyRole.Active : KeyRole.Standby,
            CreatedUtc = now,
        };
        if (standbyNow.Count == 1)
            standbyNow[0].Role = KeyRole.Active;
        db.SigningKeys.Add(fresh);
        db.SaveChanges();

        // A unique index cannot hold this, SQLite NULLs being distinct: check it here, so a failure rolls the whole write back.
        if (db.SigningKeys.Count(k => k.Role == KeyRole.Active && k.ProductId == active.ProductId) != 1)
            throw new InvalidOperationException($"{scope} would not have exactly one active key, so the signing key is not switched.");
    }
}
