using System.Globalization;

namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// Issues license key strings. The prefix is set once per issuer. Holds no key, request or issued key
/// string after a call returns; the vendor keeps what it needs.
/// </summary>
public sealed class LicenseIssuer
{
    private readonly KeyPrefix _prefix;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;

    /// <summary>Creates an issuer.</summary>
    /// <param name="prefix">The key identifier prefix; null means <see cref="KeyPrefix.Default"/>.</param>
    /// <param name="timeProvider">The clock; null means <see cref="TimeProvider.System"/>.</param>
    public LicenseIssuer(KeyPrefix? prefix = null, TimeProvider? timeProvider = null)
        : this(prefix, timeProvider, SecureRandomSource.Instance)
    {
    }

    internal LicenseIssuer(KeyPrefix? prefix, TimeProvider? timeProvider, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _prefix = prefix ?? KeyPrefix.Default;
        _clock = timeProvider ?? TimeProvider.System;
        _random = random;
    }

    /// <summary>Issues a license, or throws one exception listing every problem with the request.</summary>
    /// <param name="request">What to issue.</param>
    /// <param name="signingKey">The private signing key.</param>
    /// <param name="definitions">The features the product has; null skips the check, <see cref="FeatureDefinitions.None"/> rejects every feature.</param>
    /// <returns>The key string, reference, key identifier and issue time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/>, its <see cref="LicenseRequest.Features"/> or <paramref name="signingKey"/> is null.</exception>
    /// <exception cref="LicenseIssueException">The request breaks an issue rule, or the key would be longer than 32,767 characters; nothing was generated or signed.</exception>
    /// <exception cref="InvalidOperationException">A <see cref="LicenseFeature"/> has a null <see cref="LicenseFeature.Value"/>: a programming error, not a request problem.</exception>
    public IssuedLicense Issue(LicenseRequest request, SigningPrivateKey signingKey, FeatureDefinitions? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Features, $"{nameof(request)}.{nameof(request.Features)}");
        ArgumentNullException.ThrowIfNull(signingKey);

        var problems = new List<LicenseProblem>();
        string? reference = null;
        if (request.Reference is not null)
        {
            if (ReferenceParser.TryParse(request.Reference, _prefix.ToFormatPrefix(), out var parsed, out var problem))
            {
                reference = parsed;
            }
            else
            {
                problems.Add(problem!);
            }
        }

        return Issue(
            request.Product,
            reference,
            LicenseExpiry.ToStatement(request.Expiry),
            request.DisplayName,
            request.VendorTag,
            request.Features,
            signingKey,
            problems,
            definitions);
    }

    /// <summary>
    /// Issues a license again from a verified one, or throws one exception listing every problem. The reference
    /// keeps its 10 characters under this issuer's prefix; the key part and issue time are new.
    /// </summary>
    /// <param name="request">What to issue.</param>
    /// <param name="signingKey">The private signing key.</param>
    /// <param name="definitions">The features the product has; null skips the check, <see cref="FeatureDefinitions.None"/> rejects every feature.</param>
    /// <returns>The key string, reference, key identifier and issue time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/>, its <see cref="ReissueRequest.Features"/> or <paramref name="signingKey"/> is null.</exception>
    /// <exception cref="LicenseIssueException">The request breaks an issue rule, including a reference that is not a displayed reference, or the key would be longer than 32,767 characters; no key is returned.</exception>
    /// <exception cref="InvalidOperationException">A <see cref="LicenseFeature"/> has a null <see cref="LicenseFeature.Value"/>: a programming error, not a request problem.</exception>
    public IssuedLicense Issue(ReissueRequest request, SigningPrivateKey signingKey, FeatureDefinitions? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Features, $"{nameof(request)}.{nameof(request.Features)}");
        ArgumentNullException.ThrowIfNull(signingKey);

        var problems = new List<LicenseProblem>();
        string? kept = null;
        if (KeyIdentifierFormat.TryReferenceOfDisplay(request.Reference, out var reference))
        {
            kept = reference;
        }
        else
        {
            problems.Add(new LicenseProblem("reference", "The reference must be a displayed reference such as LIC-8F3AK-M7RXB."));
        }

        return Issue(
            request.Product,
            kept,
            LicenseExpiry.ToStatement(request.Expiry),
            request.DisplayName,
            request.VendorTag,
            request.Features,
            signingKey,
            problems,
            definitions);
    }

    /// <summary>
    /// The one pipeline for every request type: checks the content and expiry, then generates, signs and builds the result.
    /// </summary>
    /// <param name="product">The product ID.</param>
    /// <param name="reference">The resolved 10-character reference, or null to generate one.</param>
    /// <param name="expiry">The stated expiry.</param>
    /// <param name="displayName">The display name, or null.</param>
    /// <param name="vendorTag">The vendor tag, or null.</param>
    /// <param name="features">The features.</param>
    /// <param name="signingKey">The private signing key.</param>
    /// <param name="problems">Problems already found by the caller, such as a rejected reference.</param>
    /// <param name="definitions">The feature definitions to check against, or null for none.</param>
    internal IssuedLicense Issue(
        string? product,
        string? reference,
        ExpiryStatement expiry,
        string? displayName,
        string? vendorTag,
        FeatureList features,
        SigningPrivateKey signingKey,
        List<LicenseProblem> problems,
        FeatureDefinitions? definitions = null)
    {
        // One clock read per call: the expiry check and the issue time must agree on the second.
        var now = _clock.GetUtcNow();
        var issued = new DateTimeOffset(now.UtcTicks - (now.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

        var inputs = features.ToInputs();
        problems.AddRange(ContentRules.Check(product, displayName, vendorTag, inputs));
        definitions?.Check(inputs, problems);
        ExpiryRule.TryResolve(expiry, issued, problems, out var expires);

        if (problems.Count > 0)
        {
            throw new LicenseIssueException(problems);
        }

        reference ??= _random.NextReference();
        var prefix = _prefix.ToFormatPrefix();
        var identifier = KeyIdentifierFormat.DisplayIdentifier(prefix, reference, _random.NextKeyPart());

        var payload = PayloadWriter.Write(new LicensePayload(signingKey.SigningKeyId, product!, issued, expires, displayName, vendorTag, inputs));
        var keyString = KeyEnvelope.Compose(identifier, payload, signingKey.Sign);

        if (keyString.Length > KeyEnvelope.MaxLength)
        {
            var over = keyString.Length - KeyEnvelope.MaxLength;
            throw new LicenseIssueException([new LicenseProblem(
                "features",
                $"The license is too large for a key ({over.ToString("N0", CultureInfo.InvariantCulture)} characters over the limit of {KeyEnvelope.MaxLength.ToString("N0", CultureInfo.InvariantCulture)}). Shorten or remove text features.")]);
        }

        return new IssuedLicense(keyString, KeyIdentifierFormat.DisplayReference(prefix, reference), identifier, issued);
    }
}
