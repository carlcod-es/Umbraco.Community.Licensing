using System.CommandLine;
using System.Text;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Commands;

/// <summary>The <c>product</c> command group: add, rename, feature add, list.</summary>
internal static class ProductCommands
{
    private const string ProductIdName = "<product-id>";

    public static Command Build(ToolHost host)
    {
        var feature = new Command("feature", "Work with a product's feature definitions.") { BuildFeatureAdd(host) };
        return new Command("product", "Work with products and their feature definitions.")
        {
            BuildAdd(host),
            BuildRename(host),
            feature,
            BuildList(host),
        };
    }

    // Positional arguments are optional to the parser: a missing one is prompted or refused naming it, not a parse error.
    private static Argument<string?> ProductIdArgument() =>
        new("product-id") { Description = "The product ID, for example acme.commerce.", Arity = ArgumentArity.ZeroOrOne };

    private static Option<string?> NameOption() => new("--name") { Description = "The display name." };

    private static Command BuildAdd(ToolHost host)
    {
        var id = ProductIdArgument();
        var name = NameOption();
        var command = new Command(
            "add",
            "Add a product. In per-product signing-key mode this creates its signing key (and standby key) and needs the passphrase.")
        {
            id,
            name,
        };
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var values = context.RequireAll(
                new RequiredInput(ProductIdName, context.ParseResult.GetValue(id), "Product ID"),
                new RequiredInput("--name", context.ParseResult.GetValue(name), "Display name"));
            Add(context, db, values[0], values[1]);
            return ExitCodes.Done;
        });
        return command;
    }

    private static void Add(ToolContext context, ToolDbContext db, string productId, string displayName)
    {
        ProductRules.CheckProductId(productId);
        var settings = InstallationSettings.Load(db);
        if (db.Products.Any(p => p.ProductId == productId))
        {
            throw new ToolRefusal($"The product {productId} already exists. Nothing changed.");
        }

        var now = context.Environment.TimeProvider.GetUtcNow();
        db.Products.Add(new Product { ProductId = productId, DisplayName = displayName, CreatedUtc = now });

        if (!settings.Shared)
        {
            // Everything is checked; only now is the passphrase asked for, and a wrong one adds nothing.
            var passphrase = context.ReadVerifiedPassphrase(db);
            var iterations = context.Environment.Pbkdf2Iterations;
            AddKey(db, productId, SigningKeyRole.Active, passphrase, iterations, now);
            if (settings.StandbyKeys)
            {
                AddKey(db, productId, SigningKeyRole.Standby, passphrase, iterations, now);
            }
        }

        // One SaveChanges is one transaction: the product and its keys land together or not at all.
        db.SaveChanges();
        context.Console.Error.WriteLine(settings.Shared
            ? $"Added product {productId}. It uses the shared signing key."
            : $"Added product {productId} with its own signing key{(settings.StandbyKeys ? " and standby key" : "")}.");
    }

    private static void AddKey(ToolDbContext db, string productId, SigningKeyRole role, string passphrase, int iterations, DateTimeOffset now)
    {
        using var pair = SigningKeyPair.Create();
        db.SigningKeys.Add(new SigningKey
        {
            SigningKeyId = pair.SigningKeyId,
            ProductId = productId,
            PublicKey = pair.PublicKey.Export(),
            EncryptedPrivateKeyPem = KeyProtection.Encrypt(pair.PrivateKey, passphrase, iterations),
            Role = role,
            CreatedUtc = now,
        });
    }

    private static Command BuildRename(ToolHost host)
    {
        var id = ProductIdArgument();
        var name = NameOption();
        var command = new Command("rename", "Change a product's display name. Its product ID and features are unchanged.") { id, name };
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var values = context.RequireAll(
                new RequiredInput(ProductIdName, context.ParseResult.GetValue(id), "Product ID"),
                new RequiredInput("--name", context.ParseResult.GetValue(name), "New display name"));
            var product = FindProduct(db, values[0]);
            product.DisplayName = values[1];
            db.SaveChanges();
            context.Console.Error.WriteLine($"Renamed product {product.ProductId} to \"{product.DisplayName}\".");
            return ExitCodes.Done;
        });
        return command;
    }

    private static Command BuildFeatureAdd(ToolHost host)
    {
        var id = ProductIdArgument();
        var feature = new Argument<string?>("feature")
        {
            Description = "The feature name, for example max-orders.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var type = new Option<string?>("--type") { Description = $"The feature type: {ProductRules.TypeChoices}." };
        var command = new Command("add", "Add a feature definition to a product. Definitions are never removed, renamed or retyped.")
        {
            id,
            feature,
            type,
        };
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var values = context.RequireAll(
                new RequiredInput(ProductIdName, context.ParseResult.GetValue(id), "Product ID"),
                new RequiredInput("<feature>", context.ParseResult.GetValue(feature), "Feature name"),
                new RequiredInput("--type", context.ParseResult.GetValue(type), $"Type ({ProductRules.TypeChoices})"));
            AddFeature(context, db, values[0], values[1], values[2]);
            return ExitCodes.Done;
        });
        return command;
    }

    private static void AddFeature(ToolContext context, ToolDbContext db, string productId, string featureName, string typeText)
    {
        var product = FindProduct(db, productId);
        ProductRules.CheckFeatureName(featureName);
        var type = ProductRules.ParseType(typeText);

        var existing = db.FeatureDefinitions.FirstOrDefault(f => f.ProductId == productId && f.Name == featureName);
        if (existing is not null)
        {
            throw new ToolRefusal(
                $"The feature {featureName} is already defined for {productId} as {ProductRules.TypeName(existing.Type)}. Definitions are add-only. Nothing changed.");
        }

        db.FeatureDefinitions.Add(new Data.FeatureDefinition
        {
            ProductId = product.ProductId,
            Name = featureName,
            Type = type,
            AddedUtc = context.Environment.TimeProvider.GetUtcNow(),
        });
        db.SaveChanges();
        context.Console.Error.WriteLine($"Added {ProductRules.TypeName(type)} feature {featureName} to {productId}.");
    }

    private static Command BuildList(ToolHost host)
    {
        var command = new Command("list", "List products with their feature definitions and number of licenses.");
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var products = db.Products
                .Select(p => new { p.ProductId, p.DisplayName, Licenses = db.Licenses.Count(l => l.ProductId == p.ProductId) })
                .AsEnumerable()
                .OrderBy(p => p.ProductId, StringComparer.Ordinal)
                .ToList();
            if (products.Count == 0)
            {
                context.Console.Error.WriteLine("No products. Add one with 'ucl product add <product-id> --name <name>'.");
                return ExitCodes.Done;
            }

            var features = db.FeatureDefinitions.AsEnumerable()
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToLookup(f => f.ProductId, StringComparer.Ordinal);

            // One block per product, a blank line between blocks:
            //   acme.commerce
            //     Name: Acme Commerce
            //     Licenses: 3
            //     Features:
            //       max-orders (number)    one line per feature, ordered by name; "Features: none" when empty
            var text = new StringBuilder();
            foreach (var product in products)
            {
                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.AppendLine(product.ProductId);
                text.AppendLine($"  Name: {product.DisplayName}");
                text.AppendLine($"  Licenses: {product.Licenses}");
                var own = features[product.ProductId].ToList();
                if (own.Count == 0)
                {
                    text.AppendLine("  Features: none");
                    continue;
                }

                text.AppendLine("  Features:");
                foreach (var f in own)
                {
                    text.AppendLine($"    {f.Name} ({ProductRules.TypeName(f.Type)})");
                }
            }

            context.Console.Out.Write(text.ToString());
            return ExitCodes.Done;
        });
        return command;
    }

    private static Product FindProduct(ToolDbContext db, string productId) =>
        db.Products.FirstOrDefault(p => p.ProductId == productId)
        ?? throw new ToolRefusal($"Unknown product {productId}. Nothing changed.");
}
