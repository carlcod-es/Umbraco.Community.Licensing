using System.Buffers.Text;
using System.Text;
using static Umbraco.Community.Licensing.Core.Tests.Evaluation.EvaluationSupport;

namespace Umbraco.Community.Licensing.Core.Tests.Evaluation;

/// <summary>
/// Tampering and garbage (design.md "Test strategy"): no input may come out valid, expired or not supported, and none
/// may throw. Each case also runs the evaluation steps unguarded, so a case cannot reach its state only through the
/// evaluator's outer catch.
/// </summary>
public class AdversarialTests
{
    private const string Identifier = "LIC-8F3AK-M7RXB-7Q2D";

    private static readonly string Base64UrlAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    private static readonly LicenseEvaluator Evaluator_ = Evaluator();

    private static void AssertRefused(string? key)
    {
        RunUnguarded(key);
        var result = Evaluator_.Evaluate(key);

        Assert.NotEqual(LicenseState.Valid, result.State);
        Assert.NotEqual(LicenseState.Expired, result.State);
        Assert.NotEqual(LicenseState.NotSupported, result.State);
        Assert.False(result.IsLicensed);
        Assert.Null(result.License);
    }

    private static LicenseResult AssertRefusedAndGet(string? key)
    {
        AssertRefused(key);
        return Evaluator_.Evaluate(key);
    }

    /// <summary>The evaluation steps without the outer guard: any exception here would only have been hidden by it.</summary>
    private static void RunUnguarded(string? key)
    {
        var envelope = KeyEnvelope.Read(key);
        if (envelope.State != KeyEnvelopeState.Read)
        {
            return;
        }

        if (!PayloadRouting.TryRead(envelope.Payload!, out _, out var signingKeyId))
        {
            return;
        }

        if (Trusted.Contains(signingKeyId))
        {
            Trusted.Verify(signingKeyId, envelope.SigningInput!, envelope.Signature!);
        }

        PayloadReader.TryRead(envelope.Payload!, out _);
    }

    private static string Wrap(byte[] payload, string? signature = null) =>
        $"{Identifier}.{Base64Url.EncodeToString(payload)}.{signature ?? new string('A', 86)}";

    // Every position of the three segments ----------------------------------------------------------------------

    private static IEnumerable<char> Replacements(char original)
    {
        var index = Base64UrlAlphabet.IndexOf(original);
        if (index >= 0)
        {
            yield return Base64UrlAlphabet[(index + 1) % 64];
            yield return Base64UrlAlphabet[(index + 63) % 64];
        }
        else
        {
            yield return 'A';
        }

        yield return char.IsAsciiLetter(original) ? (char.IsAsciiLetterUpper(original) ? char.ToLowerInvariant(original) : char.ToUpperInvariant(original)) : 'B';
        yield return '!';   // outside the alphabet
        yield return '=';   // padding
        yield return '.';   // segment separator
        yield return '\0';  // control character
        yield return ' ';   // whitespace, which is stripped: a deletion
        yield return 'é';
        yield return '\uD800'; // lone surrogate
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryPositionOfSegment_ChangedToOtherCharacters_IsNeverAccepted(int segment)
    {
        var key = Issue();
        var parts = Segments(key);
        var offset = parts.Take(segment).Sum(p => p.Length + 1);
        var cases = 0;

        for (var i = 0; i < parts[segment].Length; i++)
        {
            var position = offset + i;
            foreach (var replacement in Replacements(key[position]).Distinct().Where(c => c != key[position]))
            {
                AssertRefused(key[..position] + replacement + key[(position + 1)..]);
                cases++;
            }
        }

        Assert.True(cases > parts[segment].Length * 5);
    }

    [Fact]
    public void EveryPositionOfSegments_ChangedToOtherAlphabetCharacters_ReportsAClaimOnlyWhereTheIdentifierIsIntact()
    {
        // A tampered payload or signature leaves a readable identifier, so the claim is kept; never a licence.
        var key = Issue();
        var identifierLength = Segments(key)[0].Length;

        for (var position = identifierLength + 1; position < key.Length; position++)
        {
            if (key[position] == '.')
            {
                continue;
            }

            var other = key[position] == 'A' ? 'B' : 'A';
            var result = AssertRefusedAndGet(key[..position] + other + key[(position + 1)..]);
            Assert.True(result.ClaimedKeyIdentifier is null or Identifier);
        }
    }

    [Fact]
    public void SeparatorsChangedOrRemoved_IsNeverAccepted()
    {
        var key = Issue();
        var dots = Enumerable.Range(0, key.Length).Where(i => key[i] == '.').ToArray();
        Assert.Equal(2, dots.Length);

        foreach (var dot in dots)
        {
            AssertRefused(key.Remove(dot, 1));
            foreach (var replacement in new[] { 'A', '-', '_', '!', '=', '\0' })
            {
                AssertRefused(key[..dot] + replacement + key[(dot + 1)..]);
            }
        }
    }

    // Hostile strings ---------------------------------------------------------------------------------------

    private static string RandomText(int length, int seed)
    {
        var random = new Random(seed);
        return string.Create(length, random, static (span, r) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (char)r.Next(0, 0x10000); // includes lone surrogates
            }
        });
    }

    [Fact]
    public void HostileString_Empty_IsMissing()
    {
        Assert.Equal(LicenseState.Missing, AssertRefusedAndGet("").State);
        Assert.Equal(LicenseState.Missing, AssertRefusedAndGet(null).State);
        Assert.Equal(LicenseState.Missing, AssertRefusedAndGet(" \r\n\t ").State);
    }

    [Theory]
    [InlineData(32_768, 1)]
    [InlineData(1_000_000, 2)]
    public void HostileString_RandomText_IsUnreadableWithNoClaims(int length, int seed)
    {
        var result = AssertRefusedAndGet(RandomText(length, seed));

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Null(result.ClaimedKeyIdentifier);
        Assert.Null(result.ClaimedProduct);
    }

    [Theory]
    [InlineData("\0")]
    [InlineData("\0\0\0\0.\0\0.\0")]
    [InlineData("\u001B[31mLIC-8F3AK-M7RXB-7Q2D\u001B[0m")]
    [InlineData("\u0001\u0002\u0003\u007F\u0085")]
    [InlineData("‮DCBA⁦")]
    [InlineData("LIC-8F3AK-M7RXB-7Q2D\0.AAAA.AAAA")]
    [InlineData("𐀀.\uDBFF.\uDFFF")]
    public void HostileString_ControlCharacters_AreRefused(string key)
    {
        Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet(key).State);
    }

    [Fact]
    public void HostileString_ControlCharacterInsideEachSegment_IsUnreadable()
    {
        var key = Issue();
        foreach (var at in new[] { 3, Segments(key)[0].Length + 5, key.Length - 5 })
        {
            Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet(key.Insert(at, "\0")).State);
        }
    }

    [Fact]
    public void HostileString_LongWithNoIdentifier_IsUnreadableWithNoClaim()
    {
        // Envelope order: whitespace removal, identifier pattern, then the length cap.
        var result = AssertRefusedAndGet(new string('x', 40_000));

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Null(result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void HostileString_LongWithValidIdentifier_IsUnreadableClaimingIt()
    {
        var result = AssertRefusedAndGet(Identifier + "." + new string('A', 40_000) + "." + new string('A', 86));

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
        Assert.Null(result.ClaimedProduct);
    }

    [Fact]
    public void HostileString_LongWhitespaceAroundAValidKey_IsStrippedBeforeTheLengthCap()
    {
        // 40,000 whitespace characters do not count towards the cap; the key itself is untouched, so it stays valid.
        var key = Issue();
        var padded = new string(' ', 20_000) + key + new string('\n', 20_000);

        RunUnguarded(padded);
        Assert.Equal(LicenseState.Valid, Evaluator_.Evaluate(padded).State);
    }

    [Fact]
    public void HostileString_DeepJsonPayload_IsUnreadableWithoutOverflow()
    {
        const int depth = 10_000;
        var nested = new string('[', depth) + new string(']', depth);
        var objectNested = $$"""{"k":"{{Signing.ExampleKeys.SigningKeyId}}","p":"{{Product}}","i":1,"x":{{nested}}}""";
        var objectInKey = string.Concat(Enumerable.Repeat("{\"a\":", depth)) + "1" + new string('}', depth);

        foreach (var json in new[] { nested, objectNested, objectInKey, new string('[', depth) })
        {
            var bytes = Encoding.UTF8.GetBytes(json);

            // Garbage signature and a genuine signature over the deep payload.
            foreach (var key in new[] { Wrap(bytes), SignRaw(json) })
            {
                var result = AssertRefusedAndGet(key);
                Assert.Equal(LicenseState.Unreadable, result.State);
                Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
            }

            Assert.False(PayloadReader.TryRead(bytes, out _));
        }
    }

    [Fact]
    public void HostileString_PaddedBase64_IsUnreadable()
    {
        // A payload whose length is not a multiple of 3 gets padding when standard base64 is used.
        var json = RawPayload(extra: ",\"n\":\"ab\"");
        var payload = Encoding.UTF8.GetBytes(json);
        while (payload.Length % 3 == 0)
        {
            json = json.Insert(1, " ");
            payload = Encoding.UTF8.GetBytes(json);
        }

        var padded = Convert.ToBase64String(payload);
        Assert.EndsWith("=", padded, StringComparison.Ordinal);
        var urlPadded = padded.Replace('+', '-').Replace('/', '_');

        var signature = Segments(Issue())[2];
        Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet($"{Identifier}.{urlPadded}.{signature}").State);
        Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet($"{Identifier}.{urlPadded.TrimEnd('=')}=.{signature}").State);
        Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet(Issue() + "==").State);
        Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet(Issue() + "=").State);
        Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet($"{Identifier}.{Segments(Issue())[1]}.{signature}==").State);
    }

    [Fact]
    public void HostileString_StandardBase64Alphabet_IsUnreadable()
    {
        var payload = Encoding.UTF8.GetBytes(RawPayload(extra: ",\"n\":\"???>>>\""));
        var standard = Convert.ToBase64String(payload).TrimEnd('=');
        Assert.Contains('+', standard);

        Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet($"{Identifier}.{standard}.{Segments(Issue())[2]}").State);
    }

    [Theory]
    [InlineData(".AAAA")]
    [InlineData(".")]
    [InlineData(".A.B.C")]
    [InlineData("..")]
    [InlineData("..AAAA")]
    public void HostileString_ExtraSegments_AreUnreadable(string suffix)
    {
        var result = AssertRefusedAndGet(Issue() + suffix);

        Assert.Equal(LicenseState.Unreadable, result.State);
        Assert.Equal(Identifier, result.ClaimedKeyIdentifier);
    }

    [Fact]
    public void HostileString_MissingOrEmptySegments_AreUnreadable()
    {
        var parts = Segments(Issue());

        foreach (var key in new[]
        {
            Identifier,
            $"{Identifier}.{parts[1]}",
            $"{Identifier}..{parts[2]}",
            $"{Identifier}.{parts[1]}.",
            $"{Identifier}..",
            $".{parts[1]}.{parts[2]}",
            $"{parts[1]}.{parts[2]}",
            $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[0]}.{parts[1]}.{parts[2]}",
        })
        {
            Assert.Equal(LicenseState.Unreadable, AssertRefusedAndGet(key).State);
        }
    }

    // Hostile payloads inside a well-formed envelope ----------------------------------------------------------

    /// <summary>Each hostile payload with the state it gets when signed by a trusted key; unsigned, none may be accepted.</summary>
    private static IEnumerable<(byte[] Payload, LicenseState SignedState)> HostilePayloadCases()
    {
        var k = Signing.ExampleKeys.SigningKeyId;
        var i = IssueTime.ToUnixTimeSeconds();

        yield return (Array.Empty<byte>(), LicenseState.Unreadable);
        yield return ((byte[])[0xFF, 0xFE, 0xFD], LicenseState.Unreadable);
        yield return ((byte[])[0xC0, 0xAF], LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes("null"), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes("[]"), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes("\"p\""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes("{"), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes("{}"), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":{{i}}"""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":{{i}}}{}"""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","p":"{{Product}}","i":{{i}}}"""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":1,"p":"{{Product}}","i":{{i}}}"""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":null,"i":{{i}}}"""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"\uD800","p":"{{Product}}","i":{{i}}}"""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"\uDC00x","i":{{i}}}"""), LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":{{i}},"\uD800":1}"""), LicenseState.NotSupported);
        yield return (Encoding.UTF8.GetBytes($$"""{"\uD800\uD800":0,"k":"{{k}}","p":"{{Product}}","i":{{i}}}"""), LicenseState.NotSupported);
        yield return (Encoding.UTF8.GetBytes($$$"""{"k":"{{{k}}}","p":"{{{Product}}}","i":{{{i}}},"f":{"a":"\uD800"}}"""), LicenseState.NotSupported);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":1e999}"""), LicenseState.NotSupported);
        yield return (Encoding.UTF8.GetBytes($$$"""{"k":"{{{k}}}","p":"{{{Product}}}","i":{{{i}}},"f":{"a":{{{new string('9', 5_000)}}}}}"""), LicenseState.NotSupported);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":{{i}},"n":"{{new string('x', 20_000)}}"}"""), LicenseState.NotSupported);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":{{i}},"n":"a\u0000b"}"""), LicenseState.NotSupported);
        yield return (Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":{{i}}} """), LicenseState.Valid);
        yield return ((byte[])[0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes($$"""{"k":"{{k}}","p":"{{Product}}","i":{{i}}}""")], LicenseState.Unreadable);
        yield return (Encoding.UTF8.GetBytes("{\"k\":\"" + k + "\",\"p\":\"" + Product), LicenseState.Unreadable);
    }

    private static IEnumerable<byte[]> HostilePayloads() => HostilePayloadCases().Select(c => c.Payload);

    [Fact]
    public void HostilePayload_WithAForgedOrGarbageSignature_IsNeverAccepted()
    {
        var count = 0;
        foreach (var payload in HostilePayloads())
        {
            var forged = KeyEnvelope.Compose(Identifier, payload, Stranger.Sign);
            var garbage = Wrap(payload);

            Assert.True(AssertRefusedAndGet(forged).State is LicenseState.Unreadable or LicenseState.NotVerified);
            Assert.True(AssertRefusedAndGet(garbage).State is LicenseState.Unreadable or LicenseState.NotVerified);
            count++;
        }

        Assert.True(count > 20);
    }

    [Fact]
    public void HostilePayload_LoneSurrogateInIgnoredPropertyName_ForgedIsNotVerified()
    {
        // Routing skips the odd name; the strict read would refuse it, which only matters once a signature holds.
        var json = $$"""{"k":"{{Signing.ExampleKeys.SigningKeyId}}","p":"{{Product}}","i":{{IssueTime.ToUnixTimeSeconds()}},"\uD800":1}""";
        var payload = Encoding.UTF8.GetBytes(json);

        var forged = AssertRefusedAndGet(KeyEnvelope.Compose(Identifier, payload, Stranger.Sign));
        Assert.Equal(LicenseState.NotVerified, forged.State);
        Assert.Equal(Product, forged.ClaimedProduct);
        Assert.Equal(Identifier, forged.ClaimedKeyIdentifier);

        Assert.Equal(LicenseState.NotVerified, AssertRefusedAndGet(Wrap(payload)).State);
    }

    [Fact]
    public void HostilePayload_LoneSurrogateInIgnoredName_SignedByTrustedKey_IsNotSupported()
    {
        // A vendor-signed payload, deliberately outside the adversarial "never not supported" rule: routing skips the odd
        // name, the strict reader refuses it, and only a trusted signature gets this far.
        var json = $$"""{"k":"{{Signing.ExampleKeys.SigningKeyId}}","p":"{{Product}}","i":{{IssueTime.ToUnixTimeSeconds()}},"\uD800":1}""";

        Assert.Equal(LicenseState.NotSupported, Evaluator_.Evaluate(SignRaw(json)).State);
    }

    [Fact]
    public void HostilePayload_SignedByATrustedKey_NeverThrows()
    {
        // Each state follows the design.md flow: routing failures are unreadable, a verified payload the strict reader
        // refuses is not supported, and only trailing whitespace is still valid JSON. A byte order mark is not accepted by the
        // JSON reader, so it is unreadable. A hidden outer-guard hit would show as an unexpected unreadable.
        var count = 0;
        foreach (var (payload, expected) in HostilePayloadCases())
        {
            var key = KeyEnvelope.Compose(Identifier, payload, Key.Sign);
            RunUnguarded(key);
            var result = Evaluator_.Evaluate(key);

            Assert.True(expected == result.State, $"Payload {count}: expected {expected}, got {result.State}.");
            Assert.Equal(expected == LicenseState.Valid, result.License is not null);
            count++;
        }

        Assert.True(count > 20);
    }
}
