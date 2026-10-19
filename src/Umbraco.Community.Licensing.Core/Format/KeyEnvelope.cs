using System.Buffers.Text;
using System.Text;

namespace Umbraco.Community.Licensing.Core;

/// <summary>How far reading a key string got (ADR-0001).</summary>
internal enum KeyEnvelopeState
{
    /// <summary>No text, or only whitespace.</summary>
    Missing,

    /// <summary>The text before the first <c>.</c> is not a key identifier; nothing is claimed.</summary>
    UnreadableWithoutIdentifier,

    /// <summary>The identifier matched but a later envelope check failed.</summary>
    UnreadableWithIdentifier,

    /// <summary>Identifier, payload, signature and signing input are all available.</summary>
    Read,
}

/// <summary>The outcome of <see cref="KeyEnvelope.Read"/>. Fields are set only as far as reading got.</summary>
internal sealed record KeyEnvelopeReadResult(
    KeyEnvelopeState State,
    string? Identifier = null,
    byte[]? Payload = null,
    byte[]? Signature = null,
    byte[]? SigningInput = null);

/// <summary>The key string <c>identifier.payload.signature</c>: composing it and reading it back (ADR-0001, ADR-0002).</summary>
internal static class KeyEnvelope
{
    /// <summary>The most characters a key string may hold after whitespace removal (ADR-0002).</summary>
    public const int MaxLength = 32_767;

    private const int SignatureLength = 64;

    /// <summary>
    /// Builds <c>identifier.base64url(payload)</c>, signs its ASCII bytes with <paramref name="sign"/>, and returns
    /// <c>identifier.base64url(payload).base64url(signature)</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="identifier"/> does not match <see cref="KeyIdentifierFormat.IdentifierPattern"/>; the issuer builds it, so this is a programming error.</exception>
    public static string Compose(string identifier, ReadOnlySpan<byte> payload, Func<ReadOnlySpan<byte>, byte[]> sign)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(sign);
        if (!KeyIdentifierFormat.IdentifierPattern.IsMatch(identifier))
        {
            throw new InvalidOperationException("The key identifier does not match the identifier pattern.");
        }

        var signingInput = $"{identifier}.{Base64Url.EncodeToString(payload)}";
        var signature = sign(Encoding.ASCII.GetBytes(signingInput));
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    /// <summary>Reads a key string. Never throws; the result says how far reading got.</summary>
    public static KeyEnvelopeReadResult Read(string? keyString)
    {
        if (keyString is null)
        {
            return new(KeyEnvelopeState.Missing);
        }

        var text = RemoveWhitespace(keyString);
        if (text.Length == 0)
        {
            return new(KeyEnvelopeState.Missing);
        }

        var firstDot = text.IndexOf('.');
        var identifierText = firstDot < 0 ? text.AsSpan() : text.AsSpan(0, firstDot);
        if (!KeyIdentifierFormat.IdentifierPattern.IsMatch(identifierText))
        {
            return new(KeyEnvelopeState.UnreadableWithoutIdentifier);
        }

        var identifier = identifierText.ToString();

        KeyEnvelopeReadResult Unreadable() => new(KeyEnvelopeState.UnreadableWithIdentifier, identifier);

        if (text.Length > MaxLength || firstDot < 0)
        {
            return Unreadable();
        }

        var secondDot = text.IndexOf('.', firstDot + 1);
        if (secondDot < 0 || text.IndexOf('.', secondDot + 1) >= 0)
        {
            return Unreadable();
        }

        var payloadText = text.AsSpan(firstDot + 1, secondDot - firstDot - 1);
        var signatureText = text.AsSpan(secondDot + 1);
        if (payloadText.IsEmpty || signatureText.IsEmpty)
        {
            return Unreadable();
        }

        if (!TryDecodeCanonical(payloadText, out var payload) || !TryDecodeCanonical(signatureText, out var signature)
            || signature.Length != SignatureLength)
        {
            return Unreadable();
        }

        // Identifier and payload text are ASCII here (pattern and base64url alphabet), so chars map to bytes one to one.
        var signingInput = Encoding.ASCII.GetBytes(text[..secondDot]);
        return new(KeyEnvelopeState.Read, identifier, payload, signature, signingInput);
    }

    private static string RemoveWhitespace(string value)
    {
        if (!value.Any(char.IsWhiteSpace))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes unpadded base64url; text that does not re-encode to itself (stray bits, padding) is refused.</summary>
    private static bool TryDecodeCanonical(ReadOnlySpan<char> text, out byte[] bytes)
    {
        bytes = [];
        if (!Base64Url.IsValid(text))
        {
            return false;
        }

        var decoded = Base64Url.DecodeFromChars(text);
        if (!Base64Url.EncodeToString(decoded).AsSpan().SequenceEqual(text))
        {
            return false;
        }

        bytes = decoded;
        return true;
    }
}
