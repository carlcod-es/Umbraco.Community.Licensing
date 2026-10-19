namespace Umbraco.Community.Licensing.Core.Tests.Issuing;

/// <summary>Builds large requests and works out the key length they would have, without going through the issuer.</summary>
internal static class KeyLengthSupport
{
    private const int IdentifierLength = 20;
    private const int SignatureTextLength = 86;

    public static string Emoji(int count) => string.Concat(Enumerable.Repeat("\U0001F600", count));

    public static string Name(int index) => $"f{index:D2}" + new string('a', 61);

    public static FeatureList TextFeatures(int count, string value, FeatureList? start = null)
    {
        var features = start ?? FeatureList.Empty;
        for (var i = 0; i < count; i++)
        {
            features = features.With(Name(i), value);
        }

        return features;
    }

    /// <summary>The length of the key string for a perpetual license: identifier, payload text and signature text with two dots.</summary>
    public static int KeyLength(string signingKeyId, DateTimeOffset issued, string product, string? displayName, string? vendorTag, FeatureList features)
    {
        var payload = PayloadWriter.Write(new LicensePayload(
            signingKeyId, product, issued, null, displayName, vendorTag, features.ToInputs()));
        var payloadText = (payload.Length * 4 + 2) / 3;
        return IdentifierLength + 1 + payloadText + 1 + SignatureTextLength;
    }
}
