using System.Text.Json;

namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// The lenient routing read (ADR-0001 as amended by ADR-0007), run before verification: extracts only <c>p</c> (product)
/// and <c>k</c> (signing key ID) from a top-level JSON object and ignores everything else. No content rules apply here.
/// Anything it cannot read (non-object, a missing or non-string field, a field that repeats, invalid UTF-8,
/// truncated or malformed JSON, trailing content, nesting deeper than <see cref="MaxDepth"/>) is unreadable
/// and never throws.
/// </summary>
internal static class PayloadRouting
{
    // Far above a real payload (depth 2) yet low enough that 10,000 nested arrays are refused at once.
    private const int MaxDepth = 16;

    public static bool TryRead(ReadOnlySpan<byte> payload, out string product, out string signingKeyId)
    {
        product = string.Empty;
        signingKeyId = string.Empty;

        if (!PayloadJson.IsValidUtf8(payload))
        {
            return false;
        }

        string? foundProduct = null;
        string? foundKeyId = null;

        try
        {
            var reader = new Utf8JsonReader(payload, new JsonReaderOptions { MaxDepth = MaxDepth });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    // The reader throws on trailing content; false here means nothing follows the object.
                    if (reader.Read() || foundProduct is null || foundKeyId is null)
                    {
                        return false;
                    }

                    product = foundProduct;
                    signingKeyId = foundKeyId;
                    return true;
                }

                // Inside an object the next token is a property name.
                // A name holding a lone surrogate escape cannot be p or k, and comparing it would throw.
                var comparable = !(reader.ValueIsEscaped && PayloadJson.HasUnpairedSurrogateEscape(reader.ValueSpan));
                var isProduct = comparable && reader.ValueTextEquals("p"u8);
                var isKeyId = comparable && reader.ValueTextEquals("k"u8);
                if (!reader.Read())
                {
                    return false;
                }

                if (!isProduct && !isKeyId)
                {
                    reader.Skip();
                    continue;
                }

                if (reader.TokenType != JsonTokenType.String
                    || PayloadJson.HasUnpairedSurrogateEscape(reader.ValueSpan))
                {
                    return false;
                }

                var text = reader.GetString();
                if (isProduct)
                {
                    if (foundProduct is not null)
                    {
                        return false; // repeated field: ambiguous, so unreadable
                    }

                    foundProduct = text;
                }
                else
                {
                    if (foundKeyId is not null)
                    {
                        return false;
                    }

                    foundKeyId = text;
                }
            }

            return false; // ended before the closing brace
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            // The routing read promises never to throw; malformed JSON has no non-throwing reader API.
            return false;
        }
    }
}
