namespace Umbraco.Community.Licensing.Core;

/// <summary>The value of a feature: <see cref="SwitchFeature"/>, <see cref="NumberFeature"/> or <see cref="TextFeature"/>.</summary>
public abstract record FeatureValue
{
    internal FeatureValue()
    {
    }
}

/// <summary>An on/off feature. Only an on switch is signed; the read side always reports true.</summary>
/// <param name="IsOn">Whether the feature is on.</param>
public sealed record SwitchFeature(bool IsOn) : FeatureValue;

/// <summary>A numeric feature.</summary>
/// <param name="Value">The number.</param>
public sealed record NumberFeature(decimal Value) : FeatureValue;

/// <summary>A text feature. Content problems are reported at issue, not here.</summary>
/// <param name="Value">The text.</param>
public sealed record TextFeature(string Value) : FeatureValue;

/// <summary>
/// A named feature of a license. Name and content problems are reported at issue, not here. A null
/// <paramref name="Value"/> is a programming error, not a problem: issue throws <see cref="InvalidOperationException"/>.
/// </summary>
/// <param name="Name">The feature name.</param>
/// <param name="Value">The feature value.</param>
public sealed record LicenseFeature(string Name, FeatureValue Value);
