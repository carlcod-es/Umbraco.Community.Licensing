using System.Text;

namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class PayloadRoutingTests
{
    private static bool TryRead(string json, out string product, out string keyId) =>
        PayloadRouting.TryRead(Encoding.UTF8.GetBytes(json), out product, out keyId);

    [Fact]
    public void TryRead_Adr0007Sample_ReturnsProductAndKeyId()
    {
        var json = "{\"k\":\"Nb_sm4Fxh5c\",\"p\":\"acme.commerce\",\"i\":1788249912,"
            + "\"f\":{\"ecommerce\":true,\"max-orders\":2500}}";

        Assert.True(TryRead(json, out var product, out var keyId));
        Assert.Equal("acme.commerce", product);
        Assert.Equal("Nb_sm4Fxh5c", keyId);
    }

    [Fact]
    public void TryRead_UnknownFieldsAndBadOtherFields_AreIgnored()
    {
        // Lenient: nothing but p and k is looked at, and content rules do not apply.
        Assert.True(TryRead(
            "{\"x\":[1,{\"y\":null}],\"p\":\"NOT A PRODUCT\",\"i\":42,\"k\":\"k\",\"f\":{\"a\":false}}",
            out var product,
            out var keyId));
        Assert.Equal("NOT A PRODUCT", product);
        Assert.Equal("k", keyId);
    }

    [Fact]
    public void TryRead_WhitespaceAndEscapedForms_AreRead()
    {
        Assert.True(TryRead(" { \"k\" : \"0F8NSYaNR_Q\" , \"\\u0070\" : \"acme.\\u0063ommerce\" } ", out var product, out var keyId));
        Assert.Equal("acme.commerce", product);
        Assert.Equal("0F8NSYaNR_Q", keyId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("\"p\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("true")]
    public void TryRead_NonObjectJson_IsUnreadable(string json) => Assert.False(TryRead(json, out _, out _));

    [Theory]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\"}")]
    [InlineData("{\"p\":\"acme.commerce\"}")]
    [InlineData("{}")]
    public void TryRead_MissingField_IsUnreadable(string json) => Assert.False(TryRead(json, out _, out _));

    [Theory]
    [InlineData("{\"k\":\"k\",\"p\":1}")]
    [InlineData("{\"k\":\"k\",\"p\":null}")]
    [InlineData("{\"k\":\"k\",\"p\":true}")]
    [InlineData("{\"k\":\"k\",\"p\":[\"a.b\"]}")]
    [InlineData("{\"k\":7,\"p\":\"a.b\"}")]
    [InlineData("{\"k\":{},\"p\":\"a.b\"}")]
    public void TryRead_NonStringField_IsUnreadable(string json) => Assert.False(TryRead(json, out _, out _));

    [Theory]
    [InlineData("{\"k\":\"k\",\"p\":\"a.b\",\"p\":\"a.b\"}")]
    [InlineData("{\"k\":\"k\",\"k\":\"j\",\"p\":\"a.b\"}")]
    public void TryRead_RepeatedField_IsUnreadable(string json) => Assert.False(TryRead(json, out _, out _));

    [Theory]
    [InlineData("{\"k\":\"k\",\"p\":\"a.b\"")]
    [InlineData("{\"k\":\"k\",\"p\":\"a.")]
    [InlineData("{\"k\":\"k\",\"prod")]
    [InlineData("{\"k\":\"k\",")]
    [InlineData("{")]
    [InlineData("{\"k\":\"k\",\"p\":\"a.b\",}")]
    [InlineData("{\"k\":\"k\" \"p\":\"a.b\"}")]
    [InlineData("{\"k\":\"k\",\"p\":\"a.b\"} x")]
    [InlineData("{\"k\":\"k\",\"p\":\"a.b\"}{}")]
    [InlineData("{\"k\":\"k\",\"p\":\"a.b\",\"x\":[1,2}")]
    public void TryRead_TruncatedOrMalformedJson_IsUnreadable(string json) => Assert.False(TryRead(json, out _, out _));

    [Fact]
    public void TryRead_InvalidUtf8_IsUnreadable()
    {
        byte[] bytes = [.. "{\"k\":\"k\",\"p\":\"a"u8, 0xC3, 0x28, .. "b\"}"u8];

        Assert.False(PayloadRouting.TryRead(bytes, out _, out _));
    }

    [Fact]
    public void TryRead_InvalidUtf8InIgnoredField_IsUnreadable()
    {
        byte[] bytes = [.. "{\"x\":\""u8, 0xFF, .. "\",\"k\":\"k\",\"p\":\"a.b\"}"u8];

        Assert.False(PayloadRouting.TryRead(bytes, out _, out _));
    }

    [Theory]
    [InlineData("{\"k\":\"k\",\"p\":\"a\\uD800b\"}")]
    [InlineData("{\"k\":\"k\",\"p\":\"a\\uDC00\"}")]
    [InlineData("{\"k\":\"\\uD800\",\"p\":\"a.b\"}")]
    [InlineData("{\"k\":\"k\",\"p\":\"\\uD800\\u0041\"}")]
    public void TryRead_EscapedLoneSurrogate_IsUnreadable(string json) => Assert.False(TryRead(json, out _, out _));

    [Fact]
    public void TryRead_EscapedSurrogatePair_IsRead()
    {
        Assert.True(TryRead("{\"k\":\"k\",\"p\":\"a\\uD83D\\uDE00\"}", out var product, out _));
        Assert.Equal("a\U0001F600", product);
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void TryRead_DeeplyNestedArrays_IsUnreadableWithoutThrowing(int depth)
    {
        var json = "{\"x\":" + new string('[', depth) + new string(']', depth) + ",\"k\":\"k\",\"p\":\"a.b\"}";

        Assert.False(TryRead(json, out _, out _));
        Assert.False(TryRead(new string('[', depth), out _, out _));
        Assert.False(TryRead("{\"k\":\"k\",\"p\":\"a.b\",\"x\":" + new string('{', depth), out _, out _));
    }

    [Theory]
    [InlineData("{\"signingKeyId\":\"0F8NSYaNR_Q\",\"product\":\"acme.commerce\"}")]
    [InlineData("{\"k\":\"0F8NSYaNR_Q\",\"product\":\"acme.commerce\"}")]
    [InlineData("{\"signingKeyId\":\"0F8NSYaNR_Q\",\"p\":\"acme.commerce\"}")]
    public void TryRead_Adr0002FieldNames_AreNotRouted(string json) => Assert.False(TryRead(json, out _, out _));

    // Depth counts the root object, so root plus 15 arrays is depth 16, the limit.
    [Theory]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void TryRead_NestingAtTheDepthLimit_ReadsAndOneDeeperDoesNot(int arrays, bool readable)
    {
        var json = "{\"x\":" + new string('[', arrays) + new string(']', arrays) + ",\"k\":\"0F8NSYaNR_Q\",\"p\":\"a.b\"}";

        Assert.Equal(readable, TryRead(json, out var product, out var keyId));
        Assert.Equal(readable ? "a.b" : string.Empty, product);
        Assert.Equal(readable ? "0F8NSYaNR_Q" : string.Empty, keyId);
    }

    [Fact]
    public void TryRead_EscapedLoneSurrogateInIgnoredField_IsRead()
    {
        // Only p and k are decoded, so a lone surrogate escape elsewhere cannot make GetString throw.
        Assert.True(TryRead("{\"\\uD800\":1,\"k\":\"0F8NSYaNR_Q\",\"p\":\"a.b\"}", out var product, out var keyId));
        Assert.Equal("a.b", product);
        Assert.Equal("0F8NSYaNR_Q", keyId);
    }

    [Fact]
    public void TryRead_Failure_LeavesOutputsEmpty()
    {
        Assert.False(TryRead("[]", out var product, out var keyId));
        Assert.Equal(string.Empty, product);
        Assert.Equal(string.Empty, keyId);
    }
}
