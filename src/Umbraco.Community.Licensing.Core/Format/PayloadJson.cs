using System.Text.Unicode;

namespace Umbraco.Community.Licensing.Core;

/// <summary>Byte-level checks shared by the routing read and the strict read, so a bad string takes its own path rather than an outer catch.</summary>
internal static class PayloadJson
{
    /// <summary>True when the bytes are well-formed UTF-8.</summary>
    public static bool IsValidUtf8(ReadOnlySpan<byte> bytes) => Utf8.IsValid(bytes);

    /// <summary>
    /// True when the raw JSON text holds a <c>\uXXXX</c> escape that is an unpaired surrogate (a high surrogate not
    /// followed by an escaped low one, or a low one on its own). <c>Utf8JsonReader.GetString</c> throws on those.
    /// Escapes occur only inside strings, so this may run over a whole payload. An escaped backslash is skipped.
    /// </summary>
    public static bool HasUnpairedSurrogateEscape(ReadOnlySpan<byte> raw)
    {
        var i = 0;
        while (i < raw.Length)
        {
            if (raw[i] != (byte)'\\')
            {
                i++;
                continue;
            }

            if (i + 1 >= raw.Length)
            {
                return false; // a dangling backslash is malformed JSON; the reader reports it
            }

            if (raw[i + 1] != (byte)'u')
            {
                i += 2;
                continue;
            }

            if (!TryHex4(raw, i + 2, out var unit))
            {
                return false; // a bad escape is malformed JSON; the reader reports it
            }

            if (unit is >= 0xD800 and <= 0xDBFF)
            {
                if (i + 11 < raw.Length
                    && raw[i + 6] == (byte)'\\'
                    && raw[i + 7] == (byte)'u'
                    && TryHex4(raw, i + 8, out var low)
                    && low is >= 0xDC00 and <= 0xDFFF)
                {
                    i += 12;
                    continue;
                }

                return true;
            }

            if (unit is >= 0xDC00 and <= 0xDFFF)
            {
                return true;
            }

            i += 6;
        }

        return false;
    }

    private static bool TryHex4(ReadOnlySpan<byte> raw, int start, out int value)
    {
        value = 0;
        if (start + 4 > raw.Length)
        {
            return false;
        }

        for (var k = 0; k < 4; k++)
        {
            var c = (char)raw[start + k];
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }

            value = (value << 4) | (c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10);
        }

        return true;
    }
}
