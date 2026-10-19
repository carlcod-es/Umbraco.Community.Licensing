namespace Umbraco.Community.Licensing.Core;

/// <summary>The type of a feature, as stated in a <see cref="FeatureDefinitions"/> list.</summary>
public enum FeatureType
{
    /// <summary>On when present; a switch is never stated as false.</summary>
    Switch,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A text.</summary>
    Text,
}
