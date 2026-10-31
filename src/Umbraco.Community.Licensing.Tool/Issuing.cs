using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;
using LibraryFeatureType = Umbraco.Community.Licensing.FeatureType;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>What <c>license issue</c> was asked for, as typed (nothing parsed or checked yet).</summary>
/// <param name="ProductId">The product ID.</param>
/// <param name="Expires">A date, a UTC date and time, or <c>perpetual</c>.</param>
/// <param name="Features">Each <c>name=value</c>, in the order given.</param>
/// <param name="OrderRef">Goes on the issue entry.</param>
/// <param name="Email">Goes on the license.</param>
internal sealed record IssueInput(
    string ProductId,
    string Expires,
    IReadOnlyList<string> Features,
    string? DisplayName,
    string? VendorTag,
    string? OrderRef,
    string? Email);

/// <summary>What <c>license reissue</c>, <c>renew</c> or <c>resend</c> was asked for, as typed.</summary>
/// <param name="Kind">How the new entry is recorded.</param>
/// <param name="Expires">A date, a UTC date and time, or <c>perpetual</c> (the command resolves <c>suggested</c> and prompts).</param>
/// <param name="Features">Each <c>name=value</c> to add or change.</param>
/// <param name="RemoveFeatures">Each feature name to remove.</param>
/// <param name="DisplayName">Null keeps the latest entry's.</param>
/// <param name="VendorTag">Null keeps the latest entry's.</param>
/// <param name="OrderRef">Goes on the new entry; null = none.</param>
/// <param name="Email">Replaces the license's email when given.</param>
internal sealed record ReissueInput(
    IssueKind Kind,
    string Expires,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> RemoveFeatures,
    string? DisplayName,
    string? VendorTag,
    string? OrderRef,
    string? Email);

/// <summary>
/// Turns what the vendor typed into a signed license and its records: parses features by the product's
/// definitions, signs with the product's active key, and stores the license and its issue entry. The key
/// string is returned and never stored (PDR-0033).
/// </summary>
internal static class Issuing
{
    private const string ExpiryHelp =
        "Use a date (2027-03-01), a UTC date and time (2027-03-01T23:59:59Z) or perpetual.";

    private static readonly string[] InstantFormats = ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"];

    /// <summary>
    /// Issues a new license for <paramref name="input"/>. Every problem is found before anything is stored; one
    /// <c>SaveChanges</c> stores the license and its entry together.
    /// </summary>
    /// <param name="passphrase">Already verified against the installation's passphrase check.</param>
    /// <returns>The issued license, holding the key string for the caller to show once.</returns>
    /// <exception cref="ToolRefusal">Unknown product, no signing key, wrong passphrase, or one or more problems, all named.</exception>
    public static IssuedLicense Issue(ToolContext context, ToolDbContext db, IssueInput input, string passphrase)
    {
        var settings = InstallationSettings.Load(db);
        var product = ProductRules.FindProduct(db, input.ProductId);
        var definitions = ProductRules.DefinitionsFor(db, product.ProductId);
        var stored = SigningKeyStore.Active(db, settings, product.ProductId);

        var problems = new List<LicenseProblem>();
        var features = ParseFeatures(input.Features, definitions, problems);
        var expiry = ParseExpiry(input.Expires, problems);
        var orderRef = Optional("orderRef", input.OrderRef, problems);
        var email = Optional("email", input.Email, problems);

        var request = new LicenseRequest
        {
            Product = product.ProductId,
            Expiry = expiry?.Expiry,
            DisplayName = input.DisplayName,
            VendorTag = input.VendorTag,
            Features = features,
        };
        var issued = Sign(context, settings, stored, request, definitions, passphrase, problems);

        var license = new License
        {
            Reference = ReferenceOf(issued.Reference),
            ProductId = product.ProductId,
            CustomerEmail = email,
            CreatedUtc = issued.Issued,
        };
        license.Entries.Add(new IssueEntry
        {
            Reference = license.Reference,
            Kind = IssueKind.Issue,
            IssuedUtc = issued.Issued,
            ExpiresUtc = expiry!.Resolved,
            DisplayName = input.DisplayName,
            VendorTag = input.VendorTag,
            FeaturesJson = FeaturesJson.Write(features),
            OrderRef = orderRef,
            KeyIdentifier = issued.KeyIdentifier,
            SigningKeyId = stored.SigningKeyId,
        });
        db.Licenses.Add(license);
        db.SaveChanges();
        return issued;
    }

    /// <summary>
    /// The license a typed reference names. The reference is read as the library reads one: case, hyphens and
    /// spaces ignored, then the 10 characters, or the installation's current prefix followed by 10 (PDR-0023).
    /// </summary>
    /// <exception cref="ToolRefusal">No license has that reference.</exception>
    public static License FindLicense(ToolDbContext db, InstallationSettings settings, string typed)
    {
        var bare = NormaliseReference(settings, typed);
        var license = db.Licenses.Include(l => l.Entries).FirstOrDefault(l => l.Reference == bare);
        if (license is not null)
        {
            return license;
        }

        // A reference that is not the 10 characters is most likely one typed under an earlier prefix: say which one is read.
        var hint = bare.Length == 10
            ? ""
            : settings.Prefix.Length == 0
                ? " No prefix is configured: give the 10 characters, for example 8F3AK-M7RXB."
                : $" The configured prefix is {settings.Prefix}: give {settings.Prefix}-8F3AK-M7RXB or 8F3AK-M7RXB.";
        throw new ToolRefusal($"Unknown license {typed}.{hint} Nothing changed.");
    }

    /// <summary>
    /// A typed reference reduced to the stored 10 characters: case, hyphens and spaces ignored, the installation's
    /// current prefix dropped when present (PDR-0023). The one reference reader, for lookups and filters alike.
    /// A reference typed under an earlier prefix does not match.
    /// </summary>
    public static string NormaliseReference(InstallationSettings settings, string typed)
    {
        var bare = string.Concat(typed.Where(c => c is not ('-' or ' '))).ToUpperInvariant();
        if (settings.Prefix.Length > 0 && bare.Length == settings.Prefix.Length + 10 && bare.StartsWith(settings.Prefix, StringComparison.Ordinal))
        {
            bare = bare[settings.Prefix.Length..];
        }

        return bare;
    }

    /// <summary>The expiry suggested for a reissue of <paramref name="license"/>, as text for <c>--expires</c>.</summary>
    public static string SuggestedExpiry(License license, bool isRenewal) =>
        ExpiryText(ExpirySuggestion.Suggest(LicenseRecords.Ordered(license), isRenewal));

    /// <summary>An expiry as <c>--expires</c> takes it: <c>perpetual</c> or a UTC date and time.</summary>
    public static string ExpiryText(DateTimeOffset? expiry) =>
        expiry is { } value ? value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) : "perpetual";

    /// <summary>
    /// Issues <paramref name="license"/> again from its latest issue entry, with <paramref name="input"/>'s changes,
    /// keeping its product and reference. Every problem is found before anything is stored; one
    /// <c>SaveChanges</c> stores the new entry and any email change.
    /// </summary>
    /// <param name="passphrase">Already verified against the installation's passphrase check.</param>
    /// <returns>The issued license, holding the key string for the caller to show once.</returns>
    /// <exception cref="ToolRefusal">No signing key, wrong passphrase, or one or more problems, all named.</exception>
    public static IssuedLicense Reissue(ToolContext context, ToolDbContext db, License license, ReissueInput input, string passphrase)
    {
        var settings = InstallationSettings.Load(db);
        var product = ProductRules.FindProduct(db, license.ProductId);
        var definitions = ProductRules.DefinitionsFor(db, product.ProductId);
        var stored = SigningKeyStore.Active(db, settings, product.ProductId);
        var latest = LicenseRecords.Latest(license);

        var problems = new List<LicenseProblem>();
        var features = FeaturesJson.Read(latest.FeaturesJson);
        foreach (var name in input.RemoveFeatures)
        {
            var kept = features.Without(name);
            if (ReferenceEquals(kept, features))
            {
                problems.Add(new LicenseProblem("--remove-feature", $"The license has no feature \"{name}\"."));
            }

            features = kept;
        }

        foreach (var change in ParseFeatures(input.Features, definitions, problems))
        {
            if (input.RemoveFeatures.Any(n => n.Equals(change.Name, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add(new LicenseProblem("--feature", $"\"{change.Name}\" is both changed and removed."));
            }

            features = change.Value switch
            {
                SwitchFeature s => features.With(change.Name, s.IsOn),
                NumberFeature n => features.With(change.Name, n.Value),
                TextFeature t => features.With(change.Name, t.Value),
                _ => throw new InvalidOperationException("A feature needs a value."),
            };
        }

        var expiry = ParseExpiry(input.Expires, problems);
        var orderRef = Optional("orderRef", input.OrderRef, problems);
        var email = Optional("email", input.Email, problems);
        var displayName = input.DisplayName ?? latest.DisplayName;
        var vendorTag = input.VendorTag ?? latest.VendorTag;

        var request = new LicenseRequest
        {
            Product = product.ProductId,
            Reference = license.Reference,
            Expiry = expiry?.Expiry,
            DisplayName = displayName,
            VendorTag = vendorTag,
            Features = features,
        };
        var issued = Sign(context, settings, stored, request, definitions, passphrase, problems);

        if (email is not null)
        {
            license.CustomerEmail = email;
        }

        license.Entries.Add(new IssueEntry
        {
            Reference = license.Reference,
            Kind = input.Kind,
            IssuedUtc = issued.Issued,
            ExpiresUtc = expiry!.Resolved,
            DisplayName = displayName,
            VendorTag = vendorTag,
            FeaturesJson = FeaturesJson.Write(features),
            OrderRef = orderRef,
            KeyIdentifier = issued.KeyIdentifier,
            SigningKeyId = stored.SigningKeyId,
        });
        db.SaveChanges();
        return issued;
    }

    /// <summary>
    /// Signs <paramref name="request"/> with the stored key and refuses with every problem: the caller's
    /// <paramref name="problems"/> and the library's together. The key is made even when the caller already has
    /// problems, only to collect the library's; nothing of it is kept.
    /// </summary>
    private static IssuedLicense Sign(
        ToolContext context,
        InstallationSettings settings,
        SigningKey stored,
        LicenseRequest request,
        FeatureDefinitions definitions,
        string passphrase,
        List<LicenseProblem> problems)
    {
        using var key = Unlock(stored, passphrase);
        var prefix = settings.Prefix.Length == 0 ? KeyPrefix.None : KeyPrefix.Of(settings.Prefix);
        var issuer = new LicenseIssuer(prefix, context.Environment.TimeProvider);

        IssuedLicense issued;
        try
        {
            issued = issuer.Issue(request, key, definitions);
        }
        catch (LicenseIssueException ex)
        {
            // An expiry we could not read reaches the library as "not stated"; the vendor is told once.
            var named = problems.Any(p => p.Field == "expires");
            throw Refuse([.. problems, .. ex.Problems.Where(p => !(named && p.Field == "expires"))]);
        }
        catch (ArgumentException ex)
        {
            throw Refuse([.. problems, new LicenseProblem(ex.ParamName ?? "request", ProductRules.WithoutParameterName(ex.Message))]);
        }

        if (problems.Count > 0)
        {
            throw Refuse(problems);
        }

        return issued;
    }

    /// <summary>The 10-character reference inside a displayed one (<c>LIC-8F3AK-M7RXB</c> or <c>8F3AK-M7RXB</c>).</summary>
    public static string ReferenceOf(string displayReference)
    {
        var groups = displayReference[^11..];
        return groups[..5] + groups[6..];
    }

    /// <summary>
    /// Reads each <c>name=value</c> as the product defines the name. A name the product does not define is kept
    /// as text so the library reports it with every other problem.
    /// </summary>
    private static FeatureList ParseFeatures(
        IReadOnlyList<string> arguments,
        FeatureDefinitions definitions,
        List<LicenseProblem> problems)
    {
        var features = new List<LicenseFeature>();
        foreach (var argument in arguments)
        {
            var split = argument.IndexOf('=');
            if (split < 0)
            {
                problems.Add(new LicenseProblem("--feature", $"\"{argument}\" is not name=value."));
                continue;
            }

            var name = argument[..split];
            var value = argument[(split + 1)..];
            var field = $"features.{name}";
            LibraryFeatureType? type = definitions.Where(d => d.Key == name).Select(d => (LibraryFeatureType?)d.Value).FirstOrDefault();
            switch (type)
            {
                case LibraryFeatureType.Switch when value.Equals("on", StringComparison.OrdinalIgnoreCase):
                    features.Add(new LicenseFeature(name, new SwitchFeature(true)));
                    break;
                case LibraryFeatureType.Switch when value.Equals("off", StringComparison.OrdinalIgnoreCase):
                    // The library refuses an off switch (a switch is granted by being present) and says so.
                    features.Add(new LicenseFeature(name, new SwitchFeature(false)));
                    break;
                case LibraryFeatureType.Switch:
                    problems.Add(new LicenseProblem(field, $"The switch {name} takes on or off, not \"{value}\"."));
                    break;
                case LibraryFeatureType.Number:
                    if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    {
                        features.Add(new LicenseFeature(name, new NumberFeature(number)));
                    }
                    else
                    {
                        problems.Add(new LicenseProblem(field, $"The number {name} takes a number such as 500, not \"{value}\"."));
                    }

                    break;
                default:
                    features.Add(new LicenseFeature(name, new TextFeature(value)));
                    break;
            }
        }

        return FeatureList.Of(features);
    }

    private sealed record ParsedExpiry(LicenseExpiry Expiry, DateTimeOffset? Resolved);

    private static ParsedExpiry? ParseExpiry(string text, List<LicenseProblem> problems)
    {
        text = text.Trim();
        if (text.Equals("perpetual", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedExpiry(LicenseExpiry.Perpetual, null);
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            // The library signs a date as 23:59:59 UTC that day.
            return new ParsedExpiry(LicenseExpiry.On(date), new DateTimeOffset(date.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc)));
        }

        if (DateTimeOffset.TryParseExact(text, InstantFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant))
        {
            return new ParsedExpiry(LicenseExpiry.At(instant), instant);
        }

        problems.Add(new LicenseProblem("expires", $"\"{text}\" is not an expiry. {ExpiryHelp}"));
        return null;
    }

    private static string? Optional(string field, string? value, List<LicenseProblem> problems)
    {
        if (value is null)
        {
            return null;
        }

        value = value.Trim();
        if (value.Length > 0)
        {
            return value;
        }

        problems.Add(new LicenseProblem(field, "Give a value or leave it out."));
        return null;
    }

    private static SigningPrivateKey Unlock(SigningKey stored, string passphrase)
    {
        try
        {
            return KeyProtection.Decrypt(stored.EncryptedPrivateKeyPem, passphrase);
        }
        catch (CryptographicException)
        {
            throw new ToolRefusal("Wrong passphrase: it does not open the signing key. Nothing stored.");
        }
    }

    private static ToolRefusal Refuse(IReadOnlyList<LicenseProblem> problems) =>
        new($"The license was not issued:{string.Concat(problems.Select(p => $"{Environment.NewLine}  {p.Field}: {p.Message}"))}{Environment.NewLine}Nothing stored.");
}

/// <summary>A license's features as stored on an issue entry: a JSON array of <c>{name, type, value}</c>.</summary>
internal static class FeaturesJson
{
    public static string Write(FeatureList features)
    {
        var array = new JsonArray();
        foreach (var feature in features)
        {
            var (type, value) = feature.Value switch
            {
                SwitchFeature s => ("switch", JsonValue.Create(s.IsOn)),
                NumberFeature n => ("number", JsonValue.Create(n.Value)),
                TextFeature t => ("text", JsonValue.Create(t.Value)),
                _ => throw new InvalidOperationException("A feature needs a value."),
            };
            array.Add(new JsonObject { ["name"] = feature.Name, ["type"] = type, ["value"] = value });
        }

        return array.ToJsonString();
    }

    public static FeatureList Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var features = new List<LicenseFeature>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var value = item.GetProperty("value");
            FeatureValue parsed = item.GetProperty("type").GetString() switch
            {
                "switch" => new SwitchFeature(value.GetBoolean()),
                "number" => new NumberFeature(value.GetDecimal()),
                _ => new TextFeature(value.GetString()!),
            };
            features.Add(new LicenseFeature(name, parsed));
        }

        return FeatureList.Of(features);
    }
}
