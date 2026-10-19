using System.Buffers.Text;
using System.Text;
using Umbraco.Community.Licensing.Core.Tests.Signing;

namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class KeyEnvelopeTests : IDisposable
{
    private const string Identifier = "LIC-8F3AK-M7RXB-7Q2D";

    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("""{"product":"acme.commerce"}""");

    private readonly SigningPrivateKey _privateKey = SigningPrivateKey.FromPem(ExampleKeys.PrivateKeyPem);

    public void Dispose() => _privateKey.Dispose();

    private string Valid() => KeyEnvelope.Compose(Identifier, Payload, _privateKey.Sign);

    private static string Sig(int length) => Base64Url.EncodeToString(new byte[length]);

    private static string Pay(int chars) => new('A', chars);

    [Fact]
    public void ComposeThenRead_RoundTrips()
    {
        var result = KeyEnvelope.Read(Valid());

        Assert.Equal(KeyEnvelopeState.Read, result.State);
        Assert.Equal(Identifier, result.Identifier);
        Assert.Equal(Payload, result.Payload);
        Assert.Equal(64, result.Signature!.Length);
    }

    [Fact]
    public void SigningInput_IsAsciiBytesBeforeLastDot_AndSignatureVerifies()
    {
        var key = Valid();
        var result = KeyEnvelope.Read(key);

        Assert.Equal(Encoding.ASCII.GetBytes(key[..key.LastIndexOf('.')]), result.SigningInput);
        var trusted = TrustedSigningKeys.Create(_privateKey.PublicKey);
        Assert.True(trusted.Verify(ExampleKeys.SigningKeyId, result.SigningInput, result.Signature));
    }

    [Fact]
    public void WrappedByAnEmailClient_ReadsTheSame()
    {
        var key = Valid();
        var expected = KeyEnvelope.Read(key);
        var wrapped = new StringBuilder("  \t");
        for (var i = 0; i < key.Length; i++)
        {
            wrapped.Append(key[i]);
            if (i % 7 == 3)
            {
                wrapped.Append(i % 3 == 0 ? "\r\n" : i % 3 == 1 ? " \t" : "\n ");
            }
        }

        wrapped.Append("\r\n");
        var result = KeyEnvelope.Read(wrapped.ToString());

        Assert.Equal(KeyEnvelopeState.Read, result.State);
        Assert.Equal(expected.Identifier, result.Identifier);
        Assert.Equal(expected.Payload, result.Payload);
        Assert.Equal(expected.Signature, result.Signature);
        Assert.Equal(expected.SigningInput, result.SigningInput);
    }

    [Theory]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D.eyJzaWdu")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D.")]
    public void CutOff_UnreadableWithIdentifier(string text)
    {
        var result = KeyEnvelope.Read(text);

        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, result.State);
        Assert.Equal(Identifier, result.Identifier);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void CutOff_RealKeyTruncatedInSignature_UnreadableWithIdentifier()
    {
        var key = Valid();
        var result = KeyEnvelope.Read(key[..^10]);

        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, result.State);
        Assert.Equal(Identifier, result.Identifier);
    }

    [Theory]
    [InlineData("LIC-8F3AK-M7R")]
    [InlineData("Hunter2!")]
    [InlineData(".payload.sig")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D-X.a.b")]
    public void NoIdentifier_UnreadableWithoutIdentifier(string text)
    {
        var result = KeyEnvelope.Read(text);

        Assert.Equal(KeyEnvelopeState.UnreadableWithoutIdentifier, result.State);
        Assert.Null(result.Identifier);
    }

    [Fact]
    public void NoIdentifier_ValidKeyLowercased()
    {
        var result = KeyEnvelope.Read(Valid().ToLowerInvariant());

        Assert.Equal(KeyEnvelopeState.UnreadableWithoutIdentifier, result.State);
        Assert.Null(result.Identifier);
    }

    [Fact]
    public void IdentifierTypo_UnreadableWithoutIdentifier()
    {
        var key = Valid().Replace("7Q2D", "7Q2O", StringComparison.Ordinal);
        var result = KeyEnvelope.Read(key);

        Assert.Equal(KeyEnvelopeState.UnreadableWithoutIdentifier, result.State);
        Assert.Null(result.Identifier);
    }

    [Fact]
    public void TooLong_UnreadableWithIdentifier()
    {
        var result = KeyEnvelope.Read(Identifier + "." + new string('A', 32_800));

        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, result.State);
        Assert.Equal(Identifier, result.Identifier);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Boundary_32767CharactersAfterWhitespaceRemoval_PassesTheLengthStep()
    {
        // 20 + 1 + 32,659 + 1 + 86 = 32,767; both segments decode, so only the cap could refuse it.
        var key = Identifier + "." + Pay(32_659) + "." + Sig(64);
        Assert.Equal(KeyEnvelope.MaxLength, key.Length);

        Assert.Equal(KeyEnvelopeState.Read, KeyEnvelope.Read(key).State);
        Assert.Equal(KeyEnvelopeState.Read, KeyEnvelope.Read(key + "\r\n \t" + "  ").State);
    }

    [Fact]
    public void Boundary_32768Characters_UnreadableWithIdentifier()
    {
        var key = Identifier + "." + Pay(32_660) + "." + Sig(64);
        Assert.Equal(KeyEnvelope.MaxLength + 1, key.Length);

        var result = KeyEnvelope.Read(key);

        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, result.State);
        Assert.Equal(Identifier, result.Identifier);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void NonCanonicalPayload_TrailingBitsSet_UnreadableWithIdentifier()
    {
        // "AB" decodes to one byte but re-encodes to "AA".
        var result = KeyEnvelope.Read($"{Identifier}.AB.{Sig(64)}");

        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, result.State);
        Assert.Equal(Identifier, result.Identifier);
        Assert.Equal(KeyEnvelopeState.Read, KeyEnvelope.Read($"{Identifier}.AA.{Sig(64)}").State);
    }

    [Fact]
    public void NonCanonicalSignature_TrailingBitsSet_UnreadableWithIdentifier()
    {
        var canonical = Sig(64);
        Assert.EndsWith("A", canonical, StringComparison.Ordinal);
        var tampered = canonical[..^1] + "B";

        var result = KeyEnvelope.Read($"{Identifier}.AA.{tampered}");

        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, result.State);
    }

    [Theory]
    [InlineData("AA==")]
    [InlineData("A")]
    [InlineData("A*A")]
    [InlineData("A+/A")]
    public void BadPayloadText_UnreadableWithIdentifier(string payload)
    {
        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, KeyEnvelope.Read($"{Identifier}.{payload}.{Sig(64)}").State);
    }

    [Fact]
    public void PaddedSignature_UnreadableWithIdentifier()
    {
        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, KeyEnvelope.Read($"{Identifier}.AA.{Sig(64)}==").State);
    }

    [Fact]
    public void TwoSegments_UnreadableWithIdentifier()
    {
        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, KeyEnvelope.Read($"{Identifier}.AA").State);
    }

    [Fact]
    public void FourSegments_UnreadableWithIdentifier()
    {
        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, KeyEnvelope.Read($"{Identifier}.AA.{Sig(64)}.AA").State);
        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, KeyEnvelope.Read($"{Identifier}.AA.{Sig(64)}.").State);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA")]
    public void EmptySegment_UnreadableWithIdentifier(string other)
    {
        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, KeyEnvelope.Read($"{Identifier}.{other}.").State);
        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, KeyEnvelope.Read($"{Identifier}..{(other.Length == 0 ? Sig(64) : other)}").State);
    }

    [Theory]
    [InlineData(63)]
    [InlineData(65)]
    [InlineData(0)]
    [InlineData(128)]
    public void WrongSignatureLength_UnreadableWithIdentifier(int length)
    {
        var key = KeyEnvelope.Compose(Identifier, Payload, _ => new byte[length]);
        var result = KeyEnvelope.Read(key);

        Assert.Equal(KeyEnvelopeState.UnreadableWithIdentifier, result.State);
        Assert.Equal(Identifier, result.Identifier);
    }

    [Theory]
    [InlineData("lic-8F3AK-M7RXB-7Q2D")]
    [InlineData("LIC-8F3AK-M7RXB")]
    [InlineData("ABCDEFGHIJKLMNOPQ-8F3AK-M7RXB-7Q2D")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2\u00C9")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D\n")]
    [InlineData("")]
    public void Compose_InvalidIdentifier_Throws(string identifier) =>
        Assert.Throws<InvalidOperationException>(() => KeyEnvelope.Compose(identifier, Payload, _privateKey.Sign));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \r\n\t  ")]
    public void NullOrWhitespaceOnly_Missing(string? text)
    {
        var result = KeyEnvelope.Read(text);

        Assert.Equal(KeyEnvelopeState.Missing, result.State);
        Assert.Null(result.Identifier);
    }

    [Theory]
    [InlineData("\0")]
    [InlineData("\uD800")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D.\uD800.\uDFFF")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D\n")]
    [InlineData("....")]
    public void OddInput_NeverThrows(string text)
    {
        var result = Record.Exception(() => KeyEnvelope.Read(text));

        Assert.Null(result);
    }
}
