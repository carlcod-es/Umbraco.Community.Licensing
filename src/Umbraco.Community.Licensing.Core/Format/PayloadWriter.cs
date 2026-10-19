using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// Writes the payload as compact UTF-8 JSON (ADR-0002 as amended by ADR-0007): fixed field order, field names
/// <c>k p i e n t f</c>, optional fields and empty <c>f</c> omitted, numbers as raw normalised text (never
/// <see cref="double"/>), dates as Unix-second integers. Text is written as raw UTF-8 with only <c>"</c>, <c>\</c>
/// and U+0000 to U+001F escaped. The writer trusts its input: the issuer validates with <see cref="ContentRules"/>
/// first. Input that cannot be written correctly (a number failing <see cref="NumberRule.TryNormalise"/>, a date with
/// a fraction of a second or outside 1970-01-01 to 9999-12-31, a missing name or text, text with an unpaired
/// surrogate) is a programming error and throws <see cref="InvalidOperationException"/>.
/// </summary>
/// <remarks>
/// The writer escapes text itself (ADR-0007 as amended) and hands it to <see cref="Utf8JsonWriter"/> with
/// <c>WriteRawValue</c>. An encoder subclass was rejected because it needs AllowUnsafeBlocks. Field names are fixed ASCII, and feature names hold only a-z, 0-9 and hyphen, which the writer's own
/// name encoding leaves alone.
/// </remarks>
internal static class PayloadWriter
{
    // 9999-12-31T23:59:59Z, the largest instant the reader accepts (ADR-0007).
    private const long MaxUnixSeconds = 253402300799;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Write(LicensePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteText(writer, "k", payload.SigningKeyId);
            WriteText(writer, "p", payload.Product);
            writer.WriteNumber("i", ToUnixSeconds(payload.Issued, "issued"));
            if (payload.Expires is { } expires)
            {
                writer.WriteNumber("e", ToUnixSeconds(expires, "expires"));
            }

            if (payload.DisplayName is not null)
            {
                WriteText(writer, "n", payload.DisplayName);
            }

            if (payload.VendorTag is not null)
            {
                WriteText(writer, "t", payload.VendorTag);
            }

            if (payload.Features.Count > 0)
            {
                writer.WriteStartObject("f");
                foreach (var feature in payload.Features)
                {
                    WriteFeature(writer, feature);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteFeature(Utf8JsonWriter writer, FeatureInput feature)
    {
        if (feature.Name is null)
        {
            throw new InvalidOperationException("A feature has no name.");
        }

        writer.WritePropertyName(feature.Name);
        switch (feature.Kind)
        {
            case FeatureKind.Switch:
                if (!feature.IsOn)
                {
                    throw new InvalidOperationException($"The switch {feature.Name} is off; a switch is written only when on.");
                }

                writer.WriteBooleanValue(true);
                break;

            case FeatureKind.Number:
                if (!NumberRule.TryNormalise(feature.Number, out var text, out var reason))
                {
                    throw new InvalidOperationException($"The number {feature.Name} cannot be written: {reason}");
                }

                writer.WriteRawValue(text, skipInputValidation: false);
                break;

            default:
                WriteTextValue(writer, feature.Text
                    ?? throw new InvalidOperationException($"The text {feature.Name} has no value."));
                break;
        }
    }

    private static long ToUnixSeconds(DateTimeOffset value, string field)
    {
        if (value.UtcTicks % TimeSpan.TicksPerSecond != 0)
        {
            throw new InvalidOperationException($"The {field} time has a fraction of a second; the payload carries whole seconds.");
        }

        var seconds = value.ToUnixTimeSeconds();
        if (seconds is < 0 or > MaxUnixSeconds)
        {
            throw new InvalidOperationException($"The {field} time is outside 1970-01-01 to 9999-12-31; the payload cannot carry it.");
        }

        return seconds;
    }

    private static void WriteText(Utf8JsonWriter writer, string name, string value)
    {
        writer.WritePropertyName(name);
        WriteTextValue(writer, value);
    }

    private static void WriteTextValue(Utf8JsonWriter writer, string value) =>
        writer.WriteRawValue(EscapeToUtf8(value), skipInputValidation: false);

    // The quoted JSON string token as UTF-8: raw except ", \ and U+0000 to U+001F (ADR-0007).
    private static byte[] EscapeToUtf8(string value)
    {
        var buffer = new ArrayBufferWriter<byte>(value.Length + 2);
        Append(buffer, "\"");

        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '"' && c != '\\' && c >= ' ')
            {
                continue;
            }

            Append(buffer, value.AsSpan(start, i - start));
            start = i + 1;
            Append(buffer, c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                _ => $"\\u{(int)c:X4}",
            });
        }

        Append(buffer, value.AsSpan(start));
        Append(buffer, "\"");
        return buffer.WrittenSpan.ToArray();
    }

    private static void Append(ArrayBufferWriter<byte> buffer, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        try
        {
            var span = buffer.GetSpan(StrictUtf8.GetMaxByteCount(text.Length));
            buffer.Advance(StrictUtf8.GetBytes(text, span));
        }
        catch (EncoderFallbackException ex)
        {
            throw new InvalidOperationException("The text is not valid UTF-16 (it holds an unpaired surrogate).", ex);
        }
    }
}
