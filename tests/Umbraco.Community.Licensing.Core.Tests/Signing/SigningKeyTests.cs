using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Community.Licensing.Core.Tests.Signing;

public class SigningKeyTests
{
    private static readonly byte[] Data = "arbitrary bytes"u8.ToArray();

    [Fact]
    public void NewPair_ReturnsPrivateKeyPublicKeyAndId()
    {
        using var pair = SigningKeyPair.Create();

        Assert.NotNull(pair.PrivateKey);
        Assert.NotNull(pair.PublicKey);
        Assert.Matches("^[A-Za-z0-9_-]{11}$", pair.SigningKeyId);
        Assert.Equal(pair.SigningKeyId, pair.PublicKey.SigningKeyId);
    }

    [Fact]
    public void PairsAreIndependent_PublicKeysAndIdsDiffer()
    {
        using var a = SigningKeyPair.Create();
        using var b = SigningKeyPair.Create();

        Assert.NotEqual(a.PublicKey, b.PublicKey);
        Assert.NotEqual(a.SigningKeyId, b.SigningKeyId);
    }

    [Fact]
    public void SameKeySameId_AfterExportAndReimport()
    {
        using var pair = SigningKeyPair.Create();

        var reimported = SigningPublicKey.Parse(pair.PublicKey.Export());

        Assert.Equal(pair.SigningKeyId, reimported.SigningKeyId);
        Assert.Equal(pair.PublicKey, reimported);
        Assert.Equal(pair.PublicKey.GetHashCode(), reimported.GetHashCode());
    }

    [Fact]
    public void PrivateKeyKnowsItsId_WhenImportedOnItsOwn()
    {
        using var pair = SigningKeyPair.Create();
        using var imported = SigningPrivateKey.FromPem(pair.PrivateKey.ExportPem());

        Assert.Equal(pair.SigningKeyId, imported.SigningKeyId);
        Assert.Equal(pair.PublicKey, imported.PublicKey);
    }

    [Fact]
    public void PrivateKeyRoundTrip_SignaturesVerifyWithOriginalPublicKey()
    {
        using var pair = SigningKeyPair.Create();
        using var imported = SigningPrivateKey.FromPem(pair.PrivateKey.ExportPem());
        var trusted = TrustedSigningKeys.Create(pair.PublicKey);

        var signature = imported.Sign(Data);

        Assert.Equal(64, signature.Length);
        Assert.True(trusted.Verify(pair.SigningKeyId, Data, signature));
    }

    [Fact]
    public void PublicKeyRoundTrip_ImportedKeyVerifiesSignaturesFromMatchingPrivateKey()
    {
        using var pair = SigningKeyPair.Create();
        var imported = SigningPublicKey.Parse(pair.PublicKey.Export());
        var trusted = TrustedSigningKeys.Create(imported);

        Assert.Equal(pair.SigningKeyId, imported.SigningKeyId);
        Assert.True(trusted.Verify(imported.SigningKeyId, Data, pair.PrivateKey.Sign(Data)));
    }

    [Fact]
    public void PublicKeyExport_IsOneLineIdDotKey()
    {
        using var pair = SigningKeyPair.Create();

        var text = pair.PublicKey.Export();

        Assert.Equal(pair.SigningKeyId + ".", text[..12]);
        Assert.DoesNotContain('\n', text);
        Assert.DoesNotContain('=', text);
    }

    [Fact]
    public void CarriedIdAltered_ImportFailsNamingMismatch()
    {
        using var pair = SigningKeyPair.Create();
        var text = pair.PublicKey.Export();
        var altered = (text[0] == 'A' ? 'B' : 'A') + text[1..];

        var ex = Assert.Throws<FormatException>(() => SigningPublicKey.Parse(altered));

        Assert.Contains("mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotAKey_ImportAsPublicOrPrivateFails()
    {
        Assert.Throws<FormatException>(() => SigningPublicKey.Parse("Hunter2!"));
        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem("Hunter2!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("Nb_sm4Fxh5c.")]
    [InlineData("Nb_sm4Fxh5c.!!!!")]
    [InlineData("Nb_sm4Fxh5c.AAAA")]
    public void PublicKeyParse_RejectsMalformedText(string text) =>
        Assert.Throws<FormatException>(() => SigningPublicKey.Parse(text));

    [Fact]
    public void NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SigningPublicKey.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => SigningPrivateKey.FromPem(null!));
    }

    [Fact]
    public void PublicExportHoldsNoPrivateMaterial_CannotBeImportedAsPrivateKey()
    {
        using var pair = SigningKeyPair.Create();

        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(pair.PublicKey.Export()));
    }

    [Fact]
    public void PrivateKeyImport_RejectsOtherCurvesAndPemKinds()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var rsa = RSA.Create(2048);

        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(p384.ExportPkcs8PrivateKeyPem()));
        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(rsa.ExportPkcs8PrivateKeyPem()));
        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(p256.ExportECPrivateKeyPem()));
        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(p256.ExportSubjectPublicKeyInfoPem()));
        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem("-----BEGIN PRIVATE KEY-----\n!!!\n-----END PRIVATE KEY-----"));
    }

    [Fact]
    public void PublicKeyParse_RejectsOtherCurvesAndKinds()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var rsa = RSA.Create(2048);

        foreach (var spki in new[] { p384.ExportSubjectPublicKeyInfo(), rsa.ExportSubjectPublicKeyInfo() })
        {
            var text = SigningPublicKey.DeriveId(spki) + "." + Convert.ToBase64String(spki).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Assert.Throws<FormatException>(() => SigningPublicKey.Parse(text));
        }
    }

    /// <summary>Explicit-parameter P-256 key generated once with OpenSSL (-param_enc explicit); platform importers disagree on it.</summary>
    private const string ExplicitCurvePrivateKeyPem = """
        -----BEGIN PRIVATE KEY-----
        MIIBeQIBADCCAQMGByqGSM49AgEwgfcCAQEwLAYHKoZIzj0BAQIhAP////8AAAAB
        AAAAAAAAAAAAAAAA////////////////MFsEIP////8AAAABAAAAAAAAAAAAAAAA
        ///////////////8BCBaxjXYqjqT57PrvVV2mIa8ZR0GsMxTsPY7zjw+J9JgSwMV
        AMSdNgiG5wSTamZ44ROdJreBn36QBEEEaxfR8uEsQkf4vOblY6RA8ncDfYEt6zOg
        9KE5RdiYwpZP40Li/hp/m47n60p8D54WK84zV2sxXs7LtkBoN79R9QIhAP////8A
        AAAA//////////+85vqtpxeehPO5ysL8YyVRAgEBBG0wawIBAQQgIBLL/CnHZ0Qp
        2LauBQPmr63X8E7hWd+3Ev6k26aWfVqhRANCAASSGAmPke8KUF/ApYuAfjdd54Dp
        VWJhyrZj0641g95EdcjemZk+lYv2jr3EHN+Jv0zJdrR3UkYfwE9ky5414oWt
        -----END PRIVATE KEY-----
        """;

    private const string ExplicitCurveSpki = "MIIBSzCCAQMGByqGSM49AgEwgfcCAQEwLAYHKoZIzj0BAQIhAP____8AAAABAAAAAAAAAAAAAAAA________________MFsEIP____8AAAABAAAAAAAAAAAAAAAA_______________8BCBaxjXYqjqT57PrvVV2mIa8ZR0GsMxTsPY7zjw-J9JgSwMVAMSdNgiG5wSTamZ44ROdJreBn36QBEEEaxfR8uEsQkf4vOblY6RA8ncDfYEt6zOg9KE5RdiYwpZP40Li_hp_m47n60p8D54WK84zV2sxXs7LtkBoN79R9QIhAP____8AAAAA__________-85vqtpxeehPO5ysL8YyVRAgEBA0IABJIYCY-R7wpQX8Cli4B-N13ngOlVYmHKtmPTrjWD3kR1yN6ZmT6Vi_aOvcQc34m_TMl2tHdSRh_AT2TLnjXiha0";

    [Fact]
    public void ExplicitCurveParameters_PublicKeyParseThrowsFormatException()
    {
        var spki = Base64Url.DecodeFromChars(ExplicitCurveSpki);
        var text = SigningPublicKey.DeriveId(spki) + "." + ExplicitCurveSpki;

        Assert.Throws<FormatException>(() => SigningPublicKey.Parse(text));
    }

    [Fact]
    public void ExplicitCurveParameters_PrivateKeyFromPemThrowsFormatException() =>
        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(ExplicitCurvePrivateKeyPem));

    [Fact]
    public void PublicKeyParse_TrimsSurroundingWhitespace()
    {
        using var pair = SigningKeyPair.Create();
        var text = pair.PublicKey.Export();

        Assert.Equal(pair.PublicKey, SigningPublicKey.Parse("\n  " + text + "\r\n"));
    }

    [Fact]
    public void PublicKeyParse_RejectsTrailingBytesAndNonCanonicalEncoding()
    {
        using var pair = SigningKeyPair.Create();
        var spki = pair.PublicKey.Spki;
        byte[] trailing = [.. spki, 0];
        byte[] flipped = [.. spki];
        flipped[0] ^= 1;

        foreach (var bad in new[] { trailing, flipped, spki[..^1] })
        {
            var text = SigningPublicKey.DeriveId(bad) + "." + Base64Url.EncodeToString(bad);
            Assert.Throws<FormatException>(() => SigningPublicKey.Parse(text));
        }
    }

    private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    // Pins Base64Url.DecodeFromChars behaviour (follow-ups: several texts may import to one key). Observed on .NET 10:
    // embedded whitespace in the key part is skipped, so the text imports to the same key.
    [Theory]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData("\t")]
    public void PublicKeyParse_WhitespaceInsideTheKeyPart_IsToleratedAndYieldsTheSameKey(string whitespace)
    {
        using var pair = SigningKeyPair.Create();
        var text = pair.PublicKey.Export();
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var spaced = text[..(dot + 11)] + whitespace + text[(dot + 11)..];

        var parsed = SigningPublicKey.Parse(spaced);

        Assert.Equal(pair.PublicKey, parsed);
        Assert.Equal(pair.SigningKeyId, parsed.SigningKeyId);
        Assert.Equal(text, parsed.Export());
    }

    // Observed on .NET 10: the 4 unused bits of the final character must be zero, so a non-canonical last character is
    // rejected and only one text imports to a given key.
    [Fact]
    public void PublicKeyParse_NonCanonicalTrailingBitsInTheLastCharacter_AreRejected()
    {
        using var pair = SigningKeyPair.Create();
        var text = pair.PublicKey.Export();
        var last = Base64UrlAlphabet.IndexOf(text[^1]);
        Assert.Equal(0, last & 0b1111);
        var altered = text[..^1] + Base64UrlAlphabet[last | 0b1111];

        Assert.NotEqual(text, altered);
        Assert.Throws<FormatException>(() => SigningPublicKey.Parse(altered));
    }

    [Fact]
    public void PrivateKeyFromPem_SurroundingWhitespaceAndOtherText_StillImports()
    {
        using var pair = SigningKeyPair.Create();

        using var imported = SigningPrivateKey.FromPem("\n  \r\n" + pair.PrivateKey.ExportPem() + "\n\n  ");
        using var commented = SigningPrivateKey.FromPem("my signing key\n" + pair.PrivateKey.ExportPem() + "\nend of file\n");

        Assert.Equal(pair.PublicKey, imported.PublicKey);
        Assert.Equal(pair.PublicKey, commented.PublicKey);
    }

    [Fact]
    public void PrivateKeyFromPem_TwoPrivateKeyBlocks_ThrowsFormatException()
    {
        using var a = SigningKeyPair.Create();
        using var b = SigningKeyPair.Create();

        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(a.PrivateKey.ExportPem() + b.PrivateKey.ExportPem()));
        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(a.PrivateKey.ExportPem() + "\n\n" + a.PrivateKey.ExportPem()));
    }

    [Fact]
    public void PrivateKeyFromPem_PrivateKeyFollowedByPublicKeyBlock_ThrowsFormatException()
    {
        using var pair = SigningKeyPair.Create();
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.Throws<FormatException>(() => SigningPrivateKey.FromPem(
            pair.PrivateKey.ExportPem() + "\n" + p256.ExportSubjectPublicKeyInfoPem()));
    }

    [Fact]
    public void PublicKeyParse_RejectsPointNotOnCurve()
    {
        using var pair = SigningKeyPair.Create();
        byte[] bad = [.. pair.PublicKey.Spki];
        bad[^1] ^= 1;
        var text = SigningPublicKey.DeriveId(bad) + "." + Base64Url.EncodeToString(bad);

        Assert.Throws<FormatException>(() => SigningPublicKey.Parse(text));
    }

    [Fact]
    public void Disposed_PrivateKeyCannotSign()
    {
        var pair = SigningKeyPair.Create();
        var key = pair.PrivateKey;
        pair.Dispose();

        Assert.Throws<ObjectDisposedException>(() => key.Sign(Data));
    }

    [Fact]
    public void Verify_FalseForBadSignatureWrongKeyAndUnknownId_NeverThrows()
    {
        using var a = SigningKeyPair.Create();
        using var b = SigningKeyPair.Create();
        var trusted = TrustedSigningKeys.Create(a.PublicKey);
        var signature = a.PrivateKey.Sign(Data);
        var tampered = (byte[])signature.Clone();
        tampered[10] ^= 1;

        Assert.False(trusted.Verify(a.SigningKeyId, Data, tampered));
        Assert.False(trusted.Verify(a.SigningKeyId, "other"u8, signature));
        Assert.False(trusted.Verify(a.SigningKeyId, Data, signature.AsSpan(0, 63)));
        Assert.False(trusted.Verify(a.SigningKeyId, Data, new byte[64]));
        Assert.False(trusted.Verify(a.SigningKeyId, Data, []));
        Assert.False(trusted.Verify(b.SigningKeyId, Data, b.PrivateKey.Sign(Data)));
        Assert.False(trusted.Verify(null!, Data, signature));
    }

    [Fact]
    public void ExampleKeys_PublicKeyParsesWithDocumentedIdAndMatchesPrivateKey()
    {
        var publicKey = SigningPublicKey.Parse(ExampleKeys.PublicKey);
        using var privateKey = SigningPrivateKey.FromPem(ExampleKeys.PrivateKeyPem);

        Assert.Equal(ExampleKeys.SigningKeyId, publicKey.SigningKeyId);
        Assert.Equal(ExampleKeys.SigningKeyId, privateKey.SigningKeyId);
        Assert.Equal(publicKey, privateKey.PublicKey);
        Assert.Equal(ExampleKeys.PublicKey, publicKey.Export());
        Assert.True(TrustedSigningKeys.Create(publicKey).Verify(publicKey.SigningKeyId, Data, privateKey.Sign(Data)));
    }
}
