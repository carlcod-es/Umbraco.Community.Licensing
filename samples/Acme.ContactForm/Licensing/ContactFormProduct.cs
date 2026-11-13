using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Licensing;

/// <summary>
/// The product as a vendor ships it: its ID, its feature names and the public key it trusts, all compiled in. One
/// instance holds the single <see cref="LicenseEvaluator"/>; register it as a singleton.
/// </summary>
public sealed class ContactFormProduct
{
    public const string ProductId = "acme.contact-form";

    public const string FileUploadsFeature = "file-uploads";

    public const string MaxSubmissionsPerMonthFeature = "max-submissions-per-month";

    public const string LicensedDomainsFeature = "licensed-domains";

    /// <summary>
    /// The demo signer's public key as a bare trusted line. PUBLIC, DEMO-ONLY KEY: it is published in this repository,
    /// so a real product would never trust it. A test keeps it equal to samples/demo-signing-keys/demo.public.txt.
    /// </summary>
    public const string DemoPublicKey =
        "EA9GWbyG_L8.MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEKSzL0QNIpRE1QeAGJiCLoMG3EoVhJyxLuCuoaEfvxmDwSitTWwWRCfytVMpT9IpZevQPMaQFiosJXTeV--rgCg";

    public static TrustedSigningKeys TrustedSigningKeys { get; } = TrustedSigningKeys.Create(SigningPublicKey.Parse(DemoPublicKey));

    /// <param name="timeProvider">The clock expiry is judged against (the demo clock in the demo site); null for the system clock.</param>
    public ContactFormProduct(TimeProvider? timeProvider = null) =>
        Evaluator = new LicenseEvaluator(ProductId, TrustedSigningKeys, timeProvider);

    public LicenseEvaluator Evaluator { get; }
}
