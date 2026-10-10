# ADR-0004: Public API surface

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q6, Q10, Q11, Q13, Q14, Q16, Q19, Q20 (`CLEAN-PROJECT-PROMPT.md` section 10,
  technology item 2)

## Context

Vendors call the issuer from their own tooling; products call the evaluator on every request
that gates a feature. Request types must report missing values as problems, not compile errors.
Read-side types must not be nullable where a verified key guarantees a value.

## Decision

Namespace `Umbraco.Community.Licensing.Core`. All public types sealed unless noted. Records for
values.

**Signing keys**

```csharp
public sealed class SigningKeyPair : IDisposable
{
    public static SigningKeyPair Create();
    public SigningPrivateKey PrivateKey { get; }
    public SigningPublicKey PublicKey { get; }
    public string SigningKeyId { get; }
}
public sealed class SigningPrivateKey : IDisposable
{
    public static SigningPrivateKey FromPem(string pem);   // FormatException if not P-256 PKCS#8
    public string ExportPem();
    public SigningPublicKey PublicKey { get; }
    public string SigningKeyId { get; }
}
public sealed class SigningPublicKey : IEquatable<SigningPublicKey>
{
    public static SigningPublicKey Parse(string text);     // FormatException; ID mismatch included
    public string Export();                                 // "<id>.<base64url SPKI>"
    public string SigningKeyId { get; }
}
public sealed class TrustedSigningKeys                       // immutable
{
    public static TrustedSigningKeys Empty { get; }
    public static TrustedSigningKeys Create(params IEnumerable<SigningPublicKey> keys);
    public TrustedSigningKeys With(SigningPublicKey key);   // ArgumentException on ID held by a different key
    public TrustedSigningKeys Without(string signingKeyId);
    public bool Contains(string signingKeyId);
    public IReadOnlyCollection<string> SigningKeyIds { get; }
}
```

**Issuing**

```csharp
public sealed record LicenseRequest
{
    public string? Product { get; init; }
    public string? Reference { get; init; }
    public LicenseExpiry? Expiry { get; init; }
    public string? DisplayName { get; init; }
    public string? VendorTag { get; init; }
    public FeatureList Features { get; init; } = FeatureList.Empty;
}
public sealed record ReissueRequest                         // internal constructor
{
    public static ReissueRequest From(VerifiedLicense license); // no clock, no checks; perpetual stated
    public string Product { get; }                          // no init: `with { Reference = … }` is CS0200
    public string Reference { get; }
    public LicenseExpiry? Expiry { get; init; }
    public string? DisplayName { get; init; }
    public string? VendorTag { get; init; }
    public FeatureList Features { get; init; }
}
public abstract record LicenseExpiry                          // closed: private constructor
{
    public static LicenseExpiry On(DateOnly date);            // 23:59:59Z that day
    public static LicenseExpiry At(DateTimeOffset instant);   // fraction rejected at issue
    public static LicenseExpiry Perpetual { get; }
}
public abstract record FeatureValue;                          // closed: Switch, Number, Text
public sealed record SwitchFeature(bool IsOn) : FeatureValue; // read side always true
public sealed record NumberFeature(decimal Value) : FeatureValue;
public sealed record TextFeature(string Value) : FeatureValue;
public sealed record LicenseFeature(string Name, FeatureValue Value);
public sealed class FeatureList : IReadOnlyList<LicenseFeature>   // immutable, keeps order
{
    public static FeatureList Empty { get; }
    public static FeatureList Of(IEnumerable<LicenseFeature> features); // keeps duplicates, so issue reports them
    public FeatureList With(string name, bool on);        // replaces a same-name entry, else appends
    public FeatureList With(string name, decimal value);
    public FeatureList With(string name, string text);
    public FeatureList Without(string name);
}
public enum FeatureType { Switch, Number, Text }
public sealed class FeatureDefinitions : IReadOnlyCollection<KeyValuePair<string, FeatureType>>  // immutable
{
    public static FeatureDefinitions None { get; }        // empty: the product has no features
    public FeatureDefinitions Switch(string name);        // ArgumentException: invalid or repeated name
    public FeatureDefinitions Number(string name);
    public FeatureDefinitions Text(string name);
}
public sealed record KeyPrefix                              // private constructor
{
    public static KeyPrefix Default { get; }                // LIC
    public static KeyPrefix None { get; }                   // identifier without prefix
    public static KeyPrefix Of(string prefix);              // ArgumentException unless 1-16 of A-Z 0-9
    public string Value { get; }                            // "" for None
}
public sealed class LicenseIssuer(KeyPrefix? prefix = null, // null: KeyPrefix.Default
    TimeProvider? timeProvider = null)
{
    public IssuedLicense Issue(LicenseRequest request, SigningPrivateKey signingKey,
        FeatureDefinitions? definitions = null);          // null: feature rules only
    public IssuedLicense Issue(ReissueRequest request, SigningPrivateKey signingKey,
        FeatureDefinitions? definitions = null);          // same rules; reference kept, issuer's prefix, new key part
}
public sealed record IssuedLicense(string KeyString, string Reference, string KeyIdentifier,
    DateTimeOffset Issued);                               // ToString omits KeyString
public sealed class LicenseIssueException : Exception
{
    public IReadOnlyList<LicenseProblem> Problems { get; }
}
public sealed record LicenseProblem(string Field, string Message);
```

`Reference` and `KeyIdentifier` are returned in display form with the issuer's prefix
(`LIC-8F3AK-M7RXB`, `LIC-8F3AK-M7RXB-7Q2D`; `8F3AK-M7RXB` with `KeyPrefix.None`). `With`/`Without` match names with the lookup rule (ignore `a`-`z` case).

`ReissueRequest` keeps the product and reference of the verified license (PDR-0021): neither has
a setter, so a changed reference cannot be expressed. A new reference is a fresh `LicenseRequest`.
`ReissueRequest.Reference` is the reference as read from the license, with its prefix
(`LIC-8F3AK-M7RXB`); the issuer takes its 10 characters and applies its own prefix, without
typed-reference parsing, so an `ACME` issuer reissues a `LIC-` key as `ACME-8F3AK-M7RXB-…`
(PDR-0023).

`KeyPrefix` is set once per issuer (PDR-0023): an invalid prefix throws from `KeyPrefix.Of`,
before any request is checked. A tool that stores the prefix as text restores it with
`KeyPrefix.Of(value)`, or `KeyPrefix.None` when `value` is empty.
Usage: `issuer.Issue(ReissueRequest.From(result.License!) with { Features = f.With("ai-assist", true) }, key)`.

**Evaluating**

```csharp
public enum LicenseState
{
    Missing, Unreadable, WrongProduct, SigningKeyNotRecognised, NotVerified, NotSupported,
    Expired, Valid
}
public sealed class LicenseEvaluator
{
    public LicenseEvaluator(string productId, TrustedSigningKeys trustedKeys,
        TimeProvider? timeProvider = null);               // ArgumentException: invalid productId, empty trustedKeys
    public string ProductId { get; }
    public LicenseResult Evaluate(string? keyString);     // never throws
}
public sealed class LicenseResult
{
    public LicenseState State { get; }
    public bool IsLicensed { get; }                       // State == Valid
    public string? ClaimedProduct { get; }                // failed states only, when readable
    public string? ClaimedKeyIdentifier { get; }          // failed states only, when readable
    public VerifiedLicense? License { get; }              // Valid and Expired only
    public bool HasSwitch(string name);                   // false unless Valid and a switch
    public decimal? GetNumber(string name);               // null unless Valid and a number
    public string? GetText(string name);                  // null unless Valid and a text
    public override string ToString();                    // state + identifier, never key text
}
public sealed record VerifiedLicense(
    string Product, string Reference, string KeyIdentifier, string SigningKeyId,
    DateTimeOffset Issued, DateTimeOffset? Expires, string? DisplayName, string? VendorTag,
    FeatureList Features)
{
    public bool IsPerpetual => Expires is null;
}
```

`LicenseResult` and `VerifiedLicense` hold no key text, so default and custom `ToString` cannot
leak it.

## Alternatives Considered

| Option | Why not |
|---|---|
| `Result`/`Try` pattern for issue | Vendors issue in tooling, not hot paths; one exception with every problem is simpler to surface |
| Exceptions for evaluation failures | Breaks "never throws on key content"; host code must not need try/catch per request |
| `HasFeature(name)` answering for any type | Grants `pro: "false"` held as text (PDR-0019) |
| `bool TryGetNumber(name, out decimal)` | Nullable return is shorter at call sites; no behavioural difference |
| Mutable trusted set | Concurrent mutation during evaluation; immutable makes "unchanged on error" trivial |
| Product ID per `Evaluate` call | An invalid ID throws in the request path on every call (design.md Q10) |
| Prefix as `string?` (`null` default, `""` none) | The spec makes an empty prefix a setup error, so `""` cannot also mean "no prefix" |
| Prefix per `Issue` call or request field | The prefix is set once at issuing setup (PDR-0023); per call, two keys from one setup could differ |
| Issuer options object | One setting does not justify it; can be added without breaking callers |
| Feature definitions on the issuer constructor | One issuer may serve several products |
| Feature definitions as a request field | A reissue request built from a key carries no definitions, so the check would be silently skipped |
| `null` and empty definitions both meaning "no check" | An empty product definition could not be expressed |
| Separate `ExpiresOn`/`ExpiresAt`/`IsPerpetual` request fields | "Both stated" becomes representable and must be rejected; one closed type cannot hold both |
| `LicenseRequest.ReissueOf` returning a plain request | `with { Reference = … }` compiles; the change would surface only at issue, if at all (PDR-0021) |
| One request type with a hidden original, `Issue` rejecting a changed product or reference | One type fewer, but the change is caught only at issue; the spec says the fields cannot be changed |
| Empty trusted set accepted by the evaluator | Every key on every site reports *signing key not recognised* (PDR-0018) |
| `Dictionary<string, FeatureValue>` for features | Cannot hold the duplicate a vendor tool might send, so it could not be reported |

## Consequences

"Neither or both expiries" reduces to "not stated" (`Expiry` null); "both" cannot be expressed.
`issued` and the key part have no request field, so "supplied" cannot be expressed either.
Two request types share the content fields; the issuer maps both onto one internal pipeline, so
the issue rules cannot diverge.

## Reversal Cost

Medium before 1.0 (callers recompile); high after.
