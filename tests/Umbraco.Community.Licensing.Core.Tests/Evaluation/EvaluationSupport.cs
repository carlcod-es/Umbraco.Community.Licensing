using System.Buffers.Text;
using System.Text;
using Umbraco.Community.Licensing.Core.Tests.Signing;
using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Evaluation;

/// <summary>Builds keys and evaluators for the evaluation tests. The example key is the trusted one; <see cref="Stranger"/> is not.</summary>
internal static class EvaluationSupport
{
    public const string Product = "acme.commerce";

    public const string Reference = "8F3AKM7RXB";

    public const string KeyPart = "7Q2D";

    public static readonly DateTimeOffset IssueTime = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset Later = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    public static readonly SigningPrivateKey Key = SigningPrivateKey.FromPem(ExampleKeys.PrivateKeyPem);

    /// <summary>A key pair the evaluators do not trust.</summary>
    public static readonly SigningPrivateKey Stranger = SigningKeyPair.Create().PrivateKey;

    public static TrustedSigningKeys Trusted { get; } = TrustedSigningKeys.Create(Key.PublicKey);

    public static LicenseEvaluator Evaluator(DateTimeOffset? now = null, string product = Product) =>
        new(product, Trusted, new FixedTimeProvider(now ?? Later));

    public static LicenseRequest Request(
        string product = Product, LicenseExpiry? expiry = null, FeatureList? features = null) => new()
        {
            Product = product,
            Expiry = expiry ?? LicenseExpiry.On(new DateOnly(2027, 3, 1)),
            DisplayName = "Commerce Standard",
            VendorTag = "CUST-0042",
            Features = features ?? FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m),
        };

    public static LicenseIssuer Issuer(KeyPrefix? prefix = null) =>
        new(prefix, new FixedTimeProvider(IssueTime), new FixedIdentifierSource(Reference, KeyPart));

    public static string Issue(LicenseRequest? request = null, KeyPrefix? prefix = null, SigningPrivateKey? key = null) =>
        Issuer(prefix).Issue(request ?? Request(), key ?? Key).KeyString;

    /// <summary>Signs a hand-written payload with the example key, as a faulty or newer vendor tool might.</summary>
    public static string SignRaw(string payloadJson, string identifier = "LIC-8F3AK-M7RXB-7Q2D") =>
        KeyEnvelope.Compose(identifier, Encoding.UTF8.GetBytes(payloadJson), Key.Sign);

    public static string RawPayload(string product = Product, string? extra = null, string? features = null)
    {
        var issued = IssueTime.ToUnixTimeSeconds();
        return $$"""{"k":"{{ExampleKeys.SigningKeyId}}","p":"{{product}}","i":{{issued}}{{extra}}{{(features is null ? "" : $",\"f\":{{{features}}}")}}}""";
    }

    public static string[] Segments(string keyString) => keyString.Split('.');

    public static string PayloadJson(string keyString) =>
        Encoding.UTF8.GetString(Base64Url.DecodeFromChars(Segments(keyString)[1]));

    /// <summary>Replaces the payload of a key and keeps its identifier and signature, as someone editing the key would.</summary>
    public static string WithPayload(string keyString, string payloadJson)
    {
        var parts = Segments(keyString);
        return $"{parts[0]}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payloadJson))}.{parts[2]}";
    }
}

/// <summary>Fixed reference and key part so tests can name the identifier.</summary>
internal sealed class FixedIdentifierSource(string reference, string keyPart) : IRandomSource
{
    public string NextReference() => reference;

    public string NextKeyPart() => keyPart;
}
