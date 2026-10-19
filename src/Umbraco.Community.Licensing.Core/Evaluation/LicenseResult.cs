namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// The outcome of one evaluation: a snapshot at the time of evaluation, so a kept result does not expire on its own.
/// Claims (<see cref="ClaimedProduct"/>, <see cref="ClaimedKeyIdentifier"/>) come from text the signature has not
/// vouched for; facts (<see cref="License"/>) exist only once it has. The result holds no key text other than the identifier.
/// </summary>
public sealed class LicenseResult
{
    private LicenseResult(
        LicenseState state, string? claimedProduct, string? claimedKeyIdentifier, VerifiedLicense? license)
    {
        State = state;
        ClaimedProduct = claimedProduct;
        ClaimedKeyIdentifier = claimedKeyIdentifier;
        License = license;
    }

    /// <summary>The state; the first failing check wins.</summary>
    public LicenseState State { get; }

    /// <summary>True only when <see cref="State"/> is <see cref="LicenseState.Valid"/>.</summary>
    public bool IsLicensed => State == LicenseState.Valid;

    /// <summary>
    /// The product the key claims, for failed states when it could be read and meets the product ID rule (two dot-separated
    /// parts of lowercase letters, digits and hyphens, at most 64 characters); otherwise null. A claim, not a fact.
    /// </summary>
    public string? ClaimedProduct { get; }

    /// <summary>The key identifier the key claims, for failed states when it could be read; otherwise null. A claim, not a fact.</summary>
    public string? ClaimedKeyIdentifier { get; }

    /// <summary>The verified facts, for <see cref="LicenseState.Valid"/> and <see cref="LicenseState.Expired"/> only; otherwise null.</summary>
    public VerifiedLicense? License { get; }

    /// <summary>Looks up a switch. Names match ignoring <c>a</c>-<c>z</c> case only, the same in every culture.</summary>
    /// <param name="name">The feature name.</param>
    /// <returns>True only when the state is <see cref="LicenseState.Valid"/> and the feature is an on switch; false for an absent name, another type or any other state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public bool HasSwitch(string name) => Lookup(name) is SwitchFeature { IsOn: true };

    /// <summary>Looks up a number. Names match ignoring <c>a</c>-<c>z</c> case only, the same in every culture.</summary>
    /// <param name="name">The feature name.</param>
    /// <returns>The number as issued, exactly; null when the state is not <see cref="LicenseState.Valid"/>, the name is absent or the feature is another type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public decimal? GetNumber(string name) => Lookup(name) is NumberFeature number ? number.Value : null;

    /// <summary>Looks up a text. Names match ignoring <c>a</c>-<c>z</c> case only, the same in every culture.</summary>
    /// <param name="name">The feature name.</param>
    /// <returns>The text as issued, exactly; null when the state is not <see cref="LicenseState.Valid"/>, the name is absent or the feature is another type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public string? GetText(string name) => Lookup(name) is TextFeature text ? text.Value : null;

    /// <summary>The state and the key identifier (claimed or verified) only; never any other part of the key string.</summary>
    /// <returns>For example <c>Valid LIC-8F3AK-M7RXB-7Q2D</c> or <c>NotVerified (claimed LIC-8F3AK-M7RXB-7Q2D)</c>.</returns>
    public override string ToString() => License is { } license
        ? $"{State} {license.KeyIdentifier}"
        : ClaimedKeyIdentifier is { } claimed ? $"{State} (claimed {claimed})" : State.ToString();

    private FeatureValue? Lookup(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return State == LicenseState.Valid ? License?.Features.Find(name) : null;
    }

    internal static LicenseResult Failed(LicenseState state, string? claimedProduct = null, string? claimedKeyIdentifier = null) =>
        new(state, claimedProduct, claimedKeyIdentifier, null);

    internal static LicenseResult Verified(LicenseState state, VerifiedLicense license) =>
        new(state, null, null, license);
}
