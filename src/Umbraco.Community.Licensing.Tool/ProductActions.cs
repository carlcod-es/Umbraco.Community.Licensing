using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

using FeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

/// <summary>
/// The Products menu: add a product, rename it, add a feature, list products (vendor-tool-products). Every action asks
/// all its questions first, then writes in one transaction, so a cancelled or failed action stores nothing. Only adding
/// a product in per-product mode needs the passphrase (ADR-0021); it is asked after the last question.
/// </summary>
internal static class ProductActions
{
    internal const string NoProducts = "There are no products yet. Add one first (Products > Add a product).";

    internal static string DuplicateProduct(string productId) => $"The product ID {productId} is already in use. A product ID is unique and never changes.";

    internal static string FeatureDefined(string name, FeatureType type) =>
        $"The feature {name} is already defined as {TypeWord(type)}. A feature definition is never removed, renamed or retyped.";

    internal static string TypeWord(FeatureType type) => type.ToString().ToLowerInvariant();

    public static void Register(ActionTable table)
    {
        table.Register("products.add", Add);
        table.Register("products.rename", Rename);
        table.Register("products.feature", AddFeature);
        table.Register("products.list", List);
    }

    internal static void Add(Session session)
    {
        var productId = session.Questions.AskText(
            "Product ID",
            TypedValues.ProductIdAccepts,
            text => TypedValues.CheckProductId(text) ?? (Exists(session, text) ? DuplicateProduct(text) : null)).Value;
        var displayName = session.Questions.AskText("Display name", TypedValues.DisplayNameAccepts, TypedValues.CheckDisplayNameText).Value;

        var (perProduct, standby) = ReadKeyMode(session);

        // The passphrase is asked after the last question. The callback only makes and encrypts keys: no question and
        // no console inside it (it runs under the lock), and only encrypted keys come out (ADR-0014).
        var environment = session.Environment;
        var keys = perProduct
            ? KeyProtection.WithPassphrase(session, passphrase =>
            {
                var made = new List<KeyProtection.NewKey> { KeyProtection.CreateEncrypted(passphrase, environment.Pbkdf2Iterations) };
                if (standby)
                    made.Add(KeyProtection.CreateEncrypted(passphrase, environment.Pbkdf2Iterations));
                return made;
            })
            : [];

        var now = environment.TimeProvider.GetUtcNow().UtcDateTime;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            // The checks run inside the transaction: a product ID is taken once, and a product has one active key (a unique index cannot hold this, SQLite NULLs being distinct).
            if (db.Products.Any(p => p.ProductId == productId))
                throw new InvalidOperationException(DuplicateProduct(productId));
            if (db.SigningKeys.Any(k => k.ProductId == productId))
                throw new InvalidOperationException($"The product ID {productId} already has signing keys, so the product is not added.");

            db.Products.Add(new Product { ProductId = productId, DisplayName = displayName, CreatedUtc = now });
            for (var i = 0; i < keys.Count; i++)
            {
                db.SigningKeys.Add(new SigningKeyRecord
                {
                    SigningKeyId = keys[i].SigningKeyId,
                    ProductId = productId,
                    PublicKey = keys[i].PublicKey,
                    EncryptedPrivateKeyPem = keys[i].EncryptedPrivateKeyPem,
                    Role = i == 0 ? KeyRole.Active : KeyRole.Standby,
                    CreatedUtc = now,
                });
            }

            db.SaveChanges();
            transaction.Commit();
        }

        var console = session.Console;
        console.WriteLine($"Added {productId} - {displayName}.");
        if (!perProduct)
            console.WriteLine("It uses the shared signing key.");
        else
            console.WriteLine(standby ? "Created its signing key and its standby key." : "Created its signing key.");
        console.WriteLine("Add its features with Products > Add a feature.");
    }

    internal static void Rename(Session session)
    {
        var product = AskProduct(session);
        if (product is null)
            return;

        session.Console.WriteLine($"Display name: {product.DisplayName}");
        var displayName = session.Questions.AskText(
            "New display name", TypedValues.DisplayNameAccepts, TypedValues.CheckDisplayNameText, proposed: product.DisplayName).Value;

        if (displayName == product.DisplayName)
        {
            session.Console.WriteLine(SettingsActions.Unchanged);
            return;
        }

        using (var db = session.OpenDb())
        {
            var row = db.Products.SingleOrDefault(p => p.ProductId == product.ProductId)
                ?? throw new InvalidOperationException($"The product {product.ProductId} is not in the installation.");
            row.DisplayName = displayName;
            db.SaveChanges();
        }

        session.Console.WriteLine($"{product.ProductId} is now {displayName}.");
    }

    internal static void AddFeature(Session session)
    {
        var product = AskProduct(session);
        if (product is null)
            return;

        var existing = ReadFeatures(session, product.ProductId);
        PrintFeatures(session.Console, existing);

        var name = session.Questions.AskText(
            "Feature name",
            TypedValues.FeatureNameAccepts,
            text => TypedValues.CheckFeatureName(text)
                ?? (existing.TryGetValue(text, out var type) ? FeatureDefined(text, type) : null)).Value;
        var type = Enum.Parse<FeatureType>(session.Questions.AskChoice(
            "Feature type",
            [
                new Choice("switch", "switch: on or off"),
                new Choice("number", "number"),
                new Choice("text", "text"),
            ]).Id, ignoreCase: true);

        var now = session.Environment.TimeProvider.GetUtcNow().UtcDateTime;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            var current = db.FeatureDefinitions.SingleOrDefault(f => f.ProductId == product.ProductId && f.Name == name);
            if (current is not null)
                throw new InvalidOperationException(FeatureDefined(name, current.Type));

            db.FeatureDefinitions.Add(new FeatureDefinition { ProductId = product.ProductId, Name = name, Type = type, AddedUtc = now });
            db.SaveChanges();
            transaction.Commit();
        }

        session.Console.WriteLine($"Added {TypeWord(type)} feature {name} to {product.ProductId}.");
    }

    internal static void List(Session session)
    {
        using var db = session.OpenDb();
        var products = db.Products.AsNoTracking().ToList().OrderBy(p => p.ProductId, StringComparer.Ordinal).ToList();
        if (products.Count == 0)
        {
            session.Console.WriteLine(NoProducts);
            return;
        }

        var features = db.FeatureDefinitions.AsNoTracking().ToList()
            .GroupBy(f => f.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Name, StringComparer.Ordinal).ToList());
        var counts = db.Licenses.AsNoTracking().GroupBy(l => l.ProductId).Select(g => new { g.Key, Count = g.Count() }).ToDictionary(x => x.Key, x => x.Count);

        foreach (var product in products)
        {
            session.Console.WriteLine($"{product.ProductId} - {product.DisplayName}");
            var own = features.GetValueOrDefault(product.ProductId) ?? [];
            session.Console.WriteLine(own.Count == 0
                ? "  Features: none"
                : $"  Features: {string.Join(", ", own.Select(f => $"{f.Name} ({TypeWord(f.Type)})"))}");
            session.Console.WriteLine($"  Licenses: {counts.GetValueOrDefault(product.ProductId)}");
        }
    }

    /// <summary>Lists the products and asks for one; null (after saying so) when there are none.</summary>
    private static Product? AskProduct(Session session)
    {
        List<Product> products;
        using (var db = session.OpenDb())
            products = db.Products.AsNoTracking().ToList().OrderBy(p => p.ProductId, StringComparer.Ordinal).ToList();

        if (products.Count == 0)
        {
            session.Console.WriteLine(NoProducts);
            return null;
        }

        var choice = session.Questions.AskChoice("Product", [.. products.Select(p => new Choice(p.ProductId, $"{p.ProductId} - {p.DisplayName}"))]);
        return products.Single(p => p.ProductId == choice.Id);
    }

    private static bool Exists(Session session, string productId)
    {
        using var db = session.OpenDb();
        return db.Products.AsNoTracking().Any(p => p.ProductId == productId);
    }

    private static Dictionary<string, FeatureType> ReadFeatures(Session session, string productId)
    {
        using var db = session.OpenDb();
        return db.FeatureDefinitions.AsNoTracking().Where(f => f.ProductId == productId).ToDictionary(f => f.Name, f => f.Type, StringComparer.Ordinal);
    }

    private static void PrintFeatures(ToolConsole console, Dictionary<string, FeatureType> features)
    {
        if (features.Count == 0)
        {
            console.WriteLine("Features so far: none");
            return;
        }

        console.WriteLine($"Features so far: {string.Join(", ", features.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key} ({TypeWord(f.Value)})"))}");
    }

    /// <summary>Per-product (true) or shared mode, and whether standby keys are on, from the settings setup wrote.</summary>
    private static (bool PerProduct, bool Standby) ReadKeyMode(Session session)
    {
        using var db = session.OpenDb();
        var names = new[] { SettingNames.SigningKeyMode, SettingNames.StandbyKeys };
        var rows = db.Settings.AsNoTracking().Where(s => names.Contains(s.Name)).ToDictionary(s => s.Name, s => s.Value);
        if (!rows.TryGetValue(SettingNames.SigningKeyMode, out var mode)
            || mode is not (SigningKeyModes.PerProduct or SigningKeyModes.Shared)
            || !rows.TryGetValue(SettingNames.StandbyKeys, out var standby)
            || standby is not ("true" or "false"))
            throw new InstallationRefusedException("The signing-key settings are missing or damaged, so the installation looks damaged. Restore a backup into another folder.");

        return (mode == SigningKeyModes.PerProduct, standby == "true");
    }
}
