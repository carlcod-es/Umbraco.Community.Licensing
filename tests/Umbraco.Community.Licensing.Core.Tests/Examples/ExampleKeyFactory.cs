using System.Text.RegularExpressions;
using Umbraco.Community.Licensing.Core.Tests.Evaluation;
using Umbraco.Community.Licensing.Core.Tests.Signing;
using Umbraco.Community.Licensing.Core.Tests.Support;
using static Umbraco.Community.Licensing.Core.Tests.Evaluation.EvaluationSupport;

namespace Umbraco.Community.Licensing.Core.Tests.Examples;

/// <summary>
/// Issues the key strings of docs/license-examples.md with the ADR-0005 example key, fixed clocks and fixed identifiers.
/// Used only by the explicit generator: ECDSA is randomised, so the committed <see cref="ExampleKeyStrings"/> are one run's
/// output and no test compares a fresh run with them.
/// </summary>
internal static class ExampleKeyFactory
{
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    public static readonly SigningPrivateKey SecondKey = SigningPrivateKey.FromPem(ExampleKeys.SecondPrivateKeyPem);

    /// <summary>Position in the payload segment dropped for the "staging" key of example 11.</summary>
    public static int StagingDropIndex(string payloadSegment) => payloadSegment.Length / 2;

    public static IReadOnlyList<(string Name, string Key)> Build()
    {
        var typical = Typical();
        var ex2 = Issue("8F3AKM7RXB", "7Q2D", Now, typical);
        var ex13 = Issue("5E3FTBPZ7Y", "2MHS", Now, Plain("zenith.commerce-shipping", On(2027, 3, 1)));
        var ex17Acme = Issue("8F3AKM7RXB", "7Q2D", Now, typical, KeyPrefix.Of("ACME"));
        var kept = Issue("8F3AKM7RXB", "K9PX", Now, typical);
        var ex7A = Issue("M4Q7ZB2NHS", "T7VA", Now, Plain("acme.commerce", On(2099, 3, 1)));

        return
        [
            ("Example1", Issue("4HN7TQW2ZC", "J3WM", Now, Plain("acme.seo-toolkit", LicenseExpiry.Perpetual))),
            ("Example2", ex2),
            ("Example3", Issue("6TWQ4NJ8KD", "3XFA", Now, Plain("acme.commerce", LicenseExpiry.At(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero))))),
            ("Example4Key1", Issue("8F3AKM7RXB", "7Q2D", new(2025, 10, 15, 9, 0, 0, TimeSpan.Zero), Orders(On(2026, 10, 15), 500m))),
            ("Example4Key2", Issue("8F3AKM7RXB", "K9PX", Now, Orders(On(2027, 10, 15), 500m))),
            ("Example5Key1", Issue("8F3AKM7RXB", "7Q2D", new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero), Orders(On(2027, 3, 1), 500m))),
            ("Example5Key2", Issue("8F3AKM7RXB", "K9PX", new(2026, 6, 15, 8, 0, 0, TimeSpan.Zero), Orders(On(2027, 3, 1), 1500m))),
            ("Example5Key3", Issue("8F3AKM7RXB", "H8RC", new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
                Orders(On(2027, 3, 1), 1500m) with { Features = Orders(On(2027, 3, 1), 1500m).Features.With("ai-assist", true) })),
            ("Example6", Issue("9KCHPW3TRM", "5YBN", Now, Plain("acme.commerce", On(2027, 3, 1)) with
            {
                Features = FeatureList.Empty.With("licensed-domains", "example.com,shop.example.com"),
            })),
            ("Example7KeyA", ex7A),
            ("Example7KeyB", ReissueExample7B(ex7A)),
            ("Example13", ex13),
            ("Example11Staging", DropCharacter(kept)),
            ("Example11Test", CutOff(Issue("2V5C9HKD3P", "X3BN", Now, typical))),
            ("Example11Uat", Issue("8F3AKM7RXB", "H8RC", Now, typical, key: SecondKey)),
            ("Example11Demo", Edited(Issue("8F3AKM7RXB", "P6TY", Now, typical))),
            ("Example11Typo", HandEditedIdentifier(ex2)),
            ("Example11Old", SignRaw(RawPayload(extra: ",\"s\":\"EU\""), "LIC-8F3AK-M7RXB-W4TN")),
            ("Example17Acme", ex17Acme),
            ("Example17None", Issue("8F3AKM7RXB", "7Q2D", Now, Plain("zenith.commerce-shipping", On(2027, 3, 1)), KeyPrefix.None)),
            ("Example17Beta", "BETA-" + ex17Acme["ACME-".Length..]),
        ];
    }

    private static LicenseExpiry On(int year, int month, int day) => LicenseExpiry.On(new DateOnly(year, month, day));

    private static LicenseRequest Typical() => Request();

    private static LicenseRequest Plain(string product, LicenseExpiry expiry) =>
        new() { Product = product, Expiry = expiry };

    private static LicenseRequest Orders(LicenseExpiry expiry, decimal maxOrders) => new()
    {
        Product = "acme.commerce",
        Expiry = expiry,
        Features = FeatureList.Empty.With("ecommerce", true).With("max-orders", maxOrders),
    };

    private static string Issue(
        string reference, string keyPart, DateTimeOffset issueTime, LicenseRequest request,
        KeyPrefix? prefix = null, SigningPrivateKey? key = null) =>
        new LicenseIssuer(prefix, new FixedTimeProvider(issueTime), new FixedIdentifierSource(reference, keyPart))
            .Issue(request, key ?? Key).KeyString;

    /// <summary>Evaluates key A and reissues it an hour later with the corrected expiry, as the vendor would.</summary>
    private static string ReissueExample7B(string keyA)
    {
        var later = new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
        var result = new LicenseEvaluator("acme.commerce", Trusted, later).Evaluate(keyA);
        var request = ReissueRequest.From(result.License!) with { Expiry = On(2027, 3, 1) };
        return new LicenseIssuer(null, later, new FixedIdentifierSource("M4Q7ZB2NHS", "D3KE")).Issue(request, Key).KeyString;
    }

    private static string DropCharacter(string key)
    {
        var parts = Segments(key);
        var at = StagingDropIndex(parts[1]);
        return $"{parts[0]}.{parts[1].Remove(at, 1)}.{parts[2]}";
    }

    /// <summary>Changes the final character of the identifier from D to O, as a person retyping it might.</summary>
    private static string HandEditedIdentifier(string key)
    {
        var dot = key.IndexOf('.');
        return key[..(dot - 1)] + "O" + key[dot..];
    }

    private static string CutOff(string key)
    {
        var dot = key.IndexOf('.');
        return key[..(dot + 1 + 60)];
    }

    private static string Edited(string key)
    {
        var json = PayloadJson(key);
        var expiry = new DateTimeOffset(2099, 3, 1, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds();
        var edited = Regex.Replace(json, "\"e\":\\d+", $"\"e\":{expiry}")
            .Replace("\"max-orders\":500", "\"max-orders\":999999");
        if (edited == json || !edited.Contains("999999"))
        {
            throw new InvalidOperationException("The payload edit did not apply.");
        }

        return WithPayload(key, edited);
    }
}
