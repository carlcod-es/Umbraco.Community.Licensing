namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// Evaluates license keys for one product, offline. Create it once at startup; it is immutable, so one instance is
/// safe to share across threads and to call on every request.
/// </summary>
public sealed class LicenseEvaluator
{
    private readonly TrustedSigningKeys _trustedKeys;
    private readonly TimeProvider _clock;

    /// <summary>Creates an evaluator.</summary>
    /// <param name="productId">The product ID the keys must be for; it follows the product ID rule.</param>
    /// <param name="trustedKeys">The public keys to trust; it must hold at least one.</param>
    /// <param name="timeProvider">The clock; null means <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="productId"/> or <paramref name="trustedKeys"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="productId"/> breaks the product ID rule, or <paramref name="trustedKeys"/> is empty.</exception>
    public LicenseEvaluator(string productId, TrustedSigningKeys trustedKeys, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(trustedKeys);

        var problems = new List<LicenseProblem>();
        ContentRules.CheckProduct(productId, problems);
        if (problems.Count > 0)
        {
            throw new ArgumentException($"Invalid product ID: {problems[0].Message}", nameof(productId));
        }

        if (trustedKeys.SigningKeyIds.Count == 0)
        {
            throw new ArgumentException("The trusted set holds no keys, so no key could ever be recognised.", nameof(trustedKeys));
        }

        ProductId = productId;
        _trustedKeys = trustedKeys;
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The product ID this evaluator accepts keys for.</summary>
    public string ProductId { get; }

    /// <summary>Evaluates a key string. Never throws, whatever the string holds; needs no network.</summary>
    /// <param name="keyString">The key string, or null when none is configured.</param>
    /// <returns>Exactly one result.</returns>
    public LicenseResult Evaluate(string? keyString)
    {
        try
        {
            return EvaluateCore(keyString);
        }
        catch (Exception)
        {
            // Safety net only: every known failure has its own check below. Claims are dropped as they cannot be trusted here.
            return LicenseResult.Failed(LicenseState.Unreadable);
        }
    }

    private LicenseResult EvaluateCore(string? keyString)
    {
        var envelope = KeyEnvelope.Read(keyString);
        switch (envelope.State)
        {
            case KeyEnvelopeState.Missing:
                return LicenseResult.Failed(LicenseState.Missing);
            case KeyEnvelopeState.UnreadableWithoutIdentifier:
                return LicenseResult.Failed(LicenseState.Unreadable);
            case KeyEnvelopeState.UnreadableWithIdentifier:
                return LicenseResult.Failed(LicenseState.Unreadable, claimedKeyIdentifier: envelope.Identifier);
        }

        var identifier = envelope.Identifier!;
        if (!PayloadRouting.TryRead(envelope.Payload!, out var product, out var signingKeyId))
        {
            return LicenseResult.Failed(LicenseState.Unreadable, claimedKeyIdentifier: identifier);
        }

        // The claimed product is reported only when it meets the product ID rule (PDR-0026); the state is unaffected, as a
        // product that breaks the rule cannot equal this evaluator's own. This evaluator's own ID passed the rule at
        // construction, so a match needs no check and the common valid path allocates nothing here.
        var isOwnProduct = string.Equals(product, ProductId, StringComparison.Ordinal);
        string? claimedProduct = product;
        if (!isOwnProduct)
        {
            var claimedProblems = new List<LicenseProblem>();
            ContentRules.CheckProduct(product, claimedProblems);
            claimedProduct = claimedProblems.Count == 0 ? product : null;
        }

        LicenseResult Failed(LicenseState state) => LicenseResult.Failed(state, claimedProduct, identifier);

        if (!isOwnProduct)
        {
            return Failed(LicenseState.WrongProduct);
        }

        if (!_trustedKeys.Contains(signingKeyId))
        {
            return Failed(LicenseState.SigningKeyNotRecognised);
        }

        if (!_trustedKeys.Verify(signingKeyId, envelope.SigningInput!, envelope.Signature!))
        {
            return Failed(LicenseState.NotVerified);
        }

        // The p/k equality check below is belt and braces: routing and the strict reader agree today, so it cannot currently fire.
        // The reader's failure text embeds payload-controlled names, so it is dropped, never reported.
        if (!PayloadReader.TryRead(envelope.Payload!, out var payload) || payload is null
            || !string.Equals(payload.Product, product, StringComparison.Ordinal)
            || !string.Equals(payload.SigningKeyId, signingKeyId, StringComparison.Ordinal))
        {
            return Failed(LicenseState.NotSupported);
        }

        var prefix = identifier.Length > 16 ? identifier[..^17] : null;
        var license = new VerifiedLicense(
            payload.Product,
            KeyIdentifierFormat.DisplayReference(prefix, KeyIdentifierFormat.ReferenceOf(identifier)),
            identifier,
            payload.SigningKeyId,
            payload.Issued,
            payload.Expires,
            payload.DisplayName,
            payload.VendorTag,
            FeatureList.FromInputs(payload.Features));

        // One clock read per evaluation; expiry is whole seconds, valid through its stated second.
        var now = _clock.GetUtcNow();
        var currentSecond = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var expired = payload.Expires is { } expires && currentSecond > expires;
        return LicenseResult.Verified(expired ? LicenseState.Expired : LicenseState.Valid, license);
    }
}
