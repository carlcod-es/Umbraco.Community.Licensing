using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>
/// Issues the ten demo keys with the committed demo signing keys and writes Demo/demo-keys.json (ADR-0028). Explicit:
/// never part of a default run. Regenerating replaces every key and identifier; the committed file is the source of
/// truth, so run the always-on tests afterwards. Run it for a library format change during the beta.
/// Run from the repository root (any OS): <c>dotnet run --project samples/Acme.ContactForm.Tests -- -explicit only -method "*DemoKeyGenerator*" -showLiveOutput</c>.
/// </summary>
public class DemoKeyGenerator(ITestOutputHelper output)
{
    private const string Product = "acme.contact-form";
    private const string OtherProduct = "acme.commerce";
    private const string UploadsFeature = "file-uploads";
    private const string LimitFeature = "max-submissions-per-month";
    private const string DomainsFeature = "licensed-domains";

    private static readonly DateTimeOffset IssueClock = new(2025, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiringSoonDemoDate = new(2030, 1, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly FeatureDefinitions Definitions = FeatureDefinitions.None
        .Switch(UploadsFeature).Number(LimitFeature).Text(DomainsFeature);

    private static FeatureList FullPlan(string domains = "localhost") => FeatureList.Empty
        .With(UploadsFeature, true).With(LimitFeature, 1000m).With(DomainsFeature, domains);

    [Fact(Explicit = true)]
    public void WriteDemoKeys()
    {
        var demoKey = DemoKeyFiles.LoadPrivate("demo.pem");
        var untrustedKey = DemoKeyFiles.LoadPrivate("untrusted.pem");
        var issuer = new LicenseIssuer(KeyPrefix.Of("ACME"), new DemoKeyFile.FixedTimeProvider(IssueClock));

        string Issue(string name, string product, LicenseExpiry expiry, FeatureList features, SigningPrivateKey signer) =>
            issuer.Issue(
                new LicenseRequest { Product = product, DisplayName = name, Expiry = expiry, Features = features },
                signer,
                Definitions).KeyString;

        var fullPlanKey = Issue("Valid: full plan", Product, LicenseExpiry.Perpetual, FullPlan(), demoKey);
        var signatureStart = fullPlanKey.LastIndexOf('.') + 1;
        var editAt = signatureStart + 10;
        var edited = fullPlanKey.ToCharArray();
        edited[editAt] = edited[editAt] == 'A' ? 'B' : 'A';

        // (name, description, key, demo date, intended state, intended expiry)
        var specs = new (string Name, string Description, string? Key, DateTimeOffset? DemoDate, LicenseState State, DateTimeOffset? Expires)[]
        {
            ("Valid: full plan", "A perpetual license with every feature: uploads, 1000 submissions a month, localhost.",
                fullPlanKey, null, LicenseState.Valid, null),
            ("Valid: small plan", "A license that expires on 2099-12-31 with no uploads and 3 submissions a month, for localhost.",
                Issue("Valid: small plan", Product, LicenseExpiry.On(new DateOnly(2099, 12, 31)),
                    FeatureList.Empty.With(LimitFeature, 3m).With(DomainsFeature, "localhost"), demoKey),
                null, LicenseState.Valid, new DateTimeOffset(2099, 12, 31, 23, 59, 59, TimeSpan.Zero)),
            ("Valid: another domain", "A perpetual license with every feature, but for www.example.com only.",
                Issue("Valid: another domain", Product, LicenseExpiry.Perpetual, FullPlan("www.example.com"), demoKey),
                null, LicenseState.Valid, null),
            ("Valid: expiring soon", "A valid license that is 14 days from expiry at its demo date, so the expiring-soon warning shows.",
                Issue("Valid: expiring soon", Product, LicenseExpiry.On(new DateOnly(2030, 1, 31)), FullPlan(), demoKey),
                ExpiringSoonDemoDate, LicenseState.Valid, new DateTimeOffset(2030, 1, 31, 23, 59, 59, TimeSpan.Zero)),
            ("Expired", "A license whose expiry, 2026-01-31, has passed.",
                Issue("Expired", Product, LicenseExpiry.On(new DateOnly(2026, 1, 31)), FullPlan(), demoKey),
                null, LicenseState.Expired, new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero)),
            ("No key", "No key is configured at all.",
                null, null, LicenseState.Missing, null),
            ("Damaged key", "Text that is not a license key, as from a bad copy and paste.",
                "not-a-license-key", null, LicenseState.Unreadable, null),
            ("Another product's key", "A genuine license, signed by the trusted signer, for a different product (acme.commerce).",
                Issue("Another product's key", OtherProduct, LicenseExpiry.Perpetual, FeatureList.Empty, demoKey),
                null, LicenseState.WrongProduct, null),
            ("Untrusted signer", "A well-formed license signed by a key the product does not trust.",
                Issue("Untrusted signer", Product, LicenseExpiry.Perpetual, FullPlan(), untrustedKey),
                null, LicenseState.SigningKeyNotRecognised, null),
            ("Edited key", "The full-plan key with one character of its signature changed, so it no longer verifies.",
                new string(edited), null, LicenseState.NotVerified, null),
        };

        var trusted = TrustedSigningKeys.Create(DemoKeyFiles.LoadPublic("demo.public.txt"));
        var entries = new List<DemoKeyEntry>();
        foreach (var spec in specs)
        {
            var at = spec.DemoDate ?? DemoKeyFile.FixedClock;
            var result = new LicenseEvaluator(Product, trusted, new DemoKeyFile.FixedTimeProvider(at)).Evaluate(spec.Key);
            Assert.True(spec.State == result.State, $"{spec.Name}: expected {spec.State} but the library reports {result.State}.");

            var license = result.License;
            if (license is not null)
            {
                Assert.Equal(spec.Expires, license.Expires);
            }

            entries.Add(new DemoKeyEntry
            {
                Name = spec.Name,
                Description = spec.Description,
                Key = spec.Key,
                DemoDate = spec.DemoDate is { } d ? DemoKeyFile.FormatInstant(d) : null,
                State = result.State.ToString(),
                Product = license?.Product ?? result.ClaimedProduct,
                ProductVerified = license is not null,
                Expires = license?.Expires is { } e ? DemoKeyFile.FormatInstant(e) : null,
                Features = license?.Features.ToDictionary(f => f.Name, f => DemoKeyFile.ToJson(f.Value)) ?? [],
            });
        }

        var json = System.Text.Json.JsonSerializer.Serialize(entries, DemoKeyFile.Json);
        Directory.CreateDirectory(Path.GetDirectoryName(DemoKeyFile.SourcePath)!);
        File.WriteAllText(DemoKeyFile.SourcePath, json + "\n");
        output.WriteLine(json);
    }
}
