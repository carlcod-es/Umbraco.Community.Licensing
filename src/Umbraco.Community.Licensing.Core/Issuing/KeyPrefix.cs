namespace Umbraco.Community.Licensing.Core;

/// <summary>The branding prefix of issued key identifiers, such as <c>LIC</c> in <c>LIC-8F3AK-M7RXB</c>. Set once per issuer; it is not signed and does not affect validity.</summary>
public sealed record KeyPrefix
{
    private KeyPrefix(string value) => Value = value;

    /// <summary>The default prefix, <c>LIC</c>.</summary>
    public static KeyPrefix Default { get; } = new("LIC");

    /// <summary>Identifiers without a prefix.</summary>
    public static KeyPrefix None { get; } = new(string.Empty);

    /// <summary>The prefix text; an empty string for <see cref="None"/>.</summary>
    public string Value { get; }

    /// <summary>Creates a prefix of 1 to 16 characters of <c>A</c>-<c>Z</c> and <c>0</c>-<c>9</c>.</summary>
    /// <param name="prefix">The prefix text. To restore a stored value that may be empty, use <see cref="None"/> for the empty case.</param>
    /// <returns>The prefix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="prefix"/> is empty, longer than 16 characters, or holds anything but <c>A</c>-<c>Z</c> and <c>0</c>-<c>9</c>.</exception>
    public static KeyPrefix Of(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (!KeyPrefixRule.IsValid(prefix))
        {
            throw new ArgumentException("A key prefix must be 1 to 16 characters of A-Z and 0-9.", nameof(prefix));
        }

        return new KeyPrefix(prefix);
    }

    /// <summary>The prefix as the format code takes it: null for <see cref="None"/>, never an empty string.</summary>
    internal string? ToFormatPrefix() => Value.Length == 0 ? null : Value;

    /// <inheritdoc/>
    public override string ToString() => Value;
}
