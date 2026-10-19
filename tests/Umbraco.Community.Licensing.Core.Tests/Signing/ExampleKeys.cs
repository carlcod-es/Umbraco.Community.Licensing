namespace Umbraco.Community.Licensing.Core.Tests.Signing;

/// <summary>The throwaway example key pair published in ADR-0005. Trusted by tests only.</summary>
internal static class ExampleKeys
{
    public const string SigningKeyId = "Nb_sm4Fxh5c";

    public const string PublicKey =
        "Nb_sm4Fxh5c.MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEU3Th2LxtmpokIglgJQMTPGENY3hAKh5m7pAz4inUYfZv-wgUo28dAgl5sOMDF5GZfoT2P-_UEkLARRvYoK0_dA";

    public const string PrivateKeyPem =
        """
        -----BEGIN PRIVATE KEY-----
        MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgDSxFHquMwza8xVc8
        2go00wyccvzla9OspC5gg9ZDbBmhRANCAARTdOHYvG2amiQiCWAlAxM8YQ1jeEAq
        HmbukDPiKdRh9m/7CBSjbx0CCXmw4wMXkZl+hPY/79QSQsBFG9igrT90
        -----END PRIVATE KEY-----
        """;

    /// <summary>The ID of the second throwaway key. Test-only: never trust it in a product.</summary>
    public const string SecondSigningKeyId = "MoA_5TxIpJs";

    /// <summary>The public half of the second throwaway key, the "other" signer in the worked examples. Test-only.</summary>
    public const string SecondPublicKey =
        "MoA_5TxIpJs.MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE5i1jmCPxyzkwqbN_wN57kGnYT19B36WyIUqAYv9hi9WGIz7bwZdRg99dRi_1WXmhseDwe6JzWKKq4aUJ4S8nVA";

    /// <summary>The private half of the second throwaway key. Published in this repository; test-only, never trusted in a product.</summary>
    public const string SecondPrivateKeyPem =
        """
        -----BEGIN PRIVATE KEY-----
        MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgvT8MYXk8waj3Vkke
        7X7ghXk6ngmTbydC317UciGd0D+hRANCAATmLWOYI/HLOTCps3/A3nuQadhPX0Hf
        pbIhSoBi/2GL1YYjPtvBl1GD311GL/VZeaGx4PB7onNYoqrhpQnhLydU
        -----END PRIVATE KEY-----
        """;
}
