using Umbraco.Community.Licensing.Tool.Data;
using LibraryFeatureType = Umbraco.Community.Licensing.FeatureType;
using StoredFeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>The product ID and feature name rules (the library's, not copies of them), and the stored definitions as the library reads them.</summary>
internal static class ProductRules
{
    /// <summary>The feature types as typed on the command line, in the order they are listed.</summary>
    public const string TypeChoices = "switch, number or text";

    // The evaluator needs a non-empty trusted set, so one throwaway public key is made, once (the private key is discarded at once).
    private static readonly Lazy<TrustedSigningKeys> Throwaway = new(() =>
    {
        using var pair = SigningKeyPair.Create();
        return TrustedSigningKeys.Create(pair.PublicKey);
    });

    /// <summary>
    /// Checks <paramref name="productId"/> against the library's product ID rule. The rule is internal to the
    /// library; its public route is the <see cref="LicenseEvaluator"/> constructor, which throws for a product ID
    /// that breaks it (ParamName <c>productId</c>) and keeps nothing.
    /// </summary>
    /// <exception cref="ToolRefusal">The product ID breaks the rule; the message is the library's.</exception>
    public static void CheckProductId(string productId)
    {
        try
        {
            _ = new LicenseEvaluator(productId, Throwaway.Value);
        }
        catch (ArgumentException ex) when (ex.ParamName == "productId")
        {
            throw new ToolRefusal($"{WithoutParameterName(ex.Message)} Nothing changed.");
        }
    }

    /// <summary>Checks <paramref name="name"/> against the library's feature name rule.</summary>
    /// <exception cref="ToolRefusal">The name breaks the rule; the message is the library's.</exception>
    public static void CheckFeatureName(string name)
    {
        try
        {
            _ = FeatureDefinitions.None.Switch(name);
        }
        catch (ArgumentException ex) when (ex.ParamName == "name")
        {
            throw new ToolRefusal($"{WithoutParameterName(ex.Message)} Nothing changed.");
        }
    }

    /// <summary>Parses <c>switch</c>, <c>number</c> or <c>text</c> (any case).</summary>
    /// <exception cref="ToolRefusal">Anything else.</exception>
    public static StoredFeatureType ParseType(string value) => value.ToLowerInvariant() switch
    {
        "switch" => StoredFeatureType.Switch,
        "number" => StoredFeatureType.Number,
        "text" => StoredFeatureType.Text,
        _ => throw new ToolRefusal($"Unknown feature type '{value}'. Use {TypeChoices}. Nothing changed."),
    };

    /// <summary>The type as typed on the command line.</summary>
    public static string TypeName(StoredFeatureType type) => type.ToString().ToLowerInvariant();

    /// <summary>
    /// The library's feature definitions for a product, built from its stored rows: what
    /// <see cref="LicenseIssuer.Issue(LicenseRequest, SigningPrivateKey, FeatureDefinitions?)"/> checks a request against.
    /// A product without rows gives <see cref="FeatureDefinitions.None"/>.
    /// </summary>
    public static FeatureDefinitions DefinitionsFor(ToolDbContext db, string productId)
    {
        var definitions = FeatureDefinitions.None;
        foreach (var row in db.FeatureDefinitions.Where(f => f.ProductId == productId).AsEnumerable().OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            definitions = row.Type switch
            {
                StoredFeatureType.Switch => definitions.Switch(row.Name),
                StoredFeatureType.Number => definitions.Number(row.Name),
                StoredFeatureType.Text => definitions.Text(row.Name),
                _ => throw new ToolRefusal($"The feature {row.Name} of {productId} has an unknown type. Nothing changed."),
            };
        }

        return definitions;
    }

    // ArgumentException.Message appends " (Parameter 'x')" (on a second line in some runtimes); the library's own sentence is wanted.
    private static string WithoutParameterName(string message)
    {
        var end = message.IndexOfAny(['\r', '\n']);
        var line = end < 0 ? message : message[..end];
        var suffix = line.LastIndexOf(" (Parameter '", StringComparison.Ordinal);
        return suffix < 0 ? line : line[..suffix];
    }
}
