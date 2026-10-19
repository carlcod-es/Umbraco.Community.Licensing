using System.Collections;

namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// An immutable list of the features a product has, each with its type, passed to
/// <see cref="LicenseIssuer.Issue(LicenseRequest, SigningPrivateKey, FeatureDefinitions?)"/> to catch misspelt names and wrong types.
/// <see cref="None"/> means the product has no features. Names are compared ordinally; the feature name rule allows lowercase only,
/// so no case folding is needed. Build a list from <see cref="None"/>: <c>FeatureDefinitions.None.Switch("ecommerce").Number("max-orders")</c>.
/// </summary>
public sealed class FeatureDefinitions : IReadOnlyCollection<KeyValuePair<string, FeatureType>>
{
    private readonly KeyValuePair<string, FeatureType>[] _items;

    private FeatureDefinitions(KeyValuePair<string, FeatureType>[] items) => _items = items;

    /// <summary>No definitions: the product has no features, so any feature in a request is rejected.</summary>
    public static FeatureDefinitions None { get; } = new([]);

    /// <inheritdoc/>
    public int Count => _items.Length;

    /// <summary>Adds a switch.</summary>
    /// <param name="name">The feature name.</param>
    /// <returns>A new list; this list is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> breaks the feature name rule or is already defined.</exception>
    public FeatureDefinitions Switch(string name) => Add(name, FeatureType.Switch);

    /// <summary>Adds a number.</summary>
    /// <param name="name">The feature name.</param>
    /// <returns>A new list; this list is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> breaks the feature name rule or is already defined.</exception>
    public FeatureDefinitions Number(string name) => Add(name, FeatureType.Number);

    /// <summary>Adds a text.</summary>
    /// <param name="name">The feature name.</param>
    /// <returns>A new list; this list is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> breaks the feature name rule or is already defined.</exception>
    public FeatureDefinitions Text(string name) => Add(name, FeatureType.Text);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, FeatureType>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, FeatureType>>)_items).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Adds a problem for each request feature that is not defined or has another type. Features with an invalid name are skipped
    /// (the content rules already report them) and each name is reported at most once, however often the request repeats it.
    /// </summary>
    internal void Check(IReadOnlyList<FeatureInput> features, List<LicenseProblem> problems)
    {
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            // Valid names are at most 64 lowercase characters, so they are safe to put in a message as they are.
            if (!ContentRules.IsValidFeatureName(feature.Name) || reported.Contains(feature.Name!))
            {
                continue;
            }

            var name = feature.Name!;
            var index = Array.FindIndex(_items, d => string.Equals(d.Key, name, StringComparison.Ordinal));
            var field = $"features.{name}";
            if (index < 0)
            {
                problems.Add(new LicenseProblem(field, $"The feature {name} is not defined for this product."));
            }
            else if (_items[index].Value != ToType(feature.Kind))
            {
                problems.Add(new LicenseProblem(field, $"The feature {name} is defined as a {Describe(_items[index].Value)} but was given as a {Describe(ToType(feature.Kind))}."));
            }
            else
            {
                continue;
            }

            reported.Add(name);
        }
    }

    private static FeatureType ToType(FeatureKind kind) => kind switch
    {
        FeatureKind.Switch => FeatureType.Switch,
        FeatureKind.Number => FeatureType.Number,
        _ => FeatureType.Text,
    };

    private static string Describe(FeatureType type) => type switch
    {
        FeatureType.Switch => "switch",
        FeatureType.Number => "number",
        _ => "text",
    };

    private FeatureDefinitions Add(string name, FeatureType type)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!ContentRules.IsValidFeatureName(name))
        {
            throw new ArgumentException($"A feature name must be 1 to {ContentRules.MaxFeatureNameLength} characters of lowercase a-z, digits and hyphens, starting with a letter.", nameof(name));
        }

        if (Array.Exists(_items, d => string.Equals(d.Key, name, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"The feature {name} is already defined.", nameof(name));
        }

        return new FeatureDefinitions([.. _items, new KeyValuePair<string, FeatureType>(name, type)]);
    }
}
