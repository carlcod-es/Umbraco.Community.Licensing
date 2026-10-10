## Context

Greenfield. The repository holds only `docs/personas.md`, root config (`Directory.Build.props`:
net10.0, nullable, warnings as errors), an empty `Umbraco.Community.Licensing.slnx` and agent
definitions. Requirements come from `CLEAN-PROJECT-PROMPT.md` (2026-10-09); see proposal.md
for motivation and specs/ for behaviour. Constraints: BCL only in the library, xUnit,
cross-platform, no host dependency.

## Bar

**Production.** The library is published for other vendors, gates paid features and is
security-relevant (signature verification). Every spec scenario has a test; tampering and
garbage inputs are tested; public API is documented (ADR-0003); example key strings in the docs
are verified by tests (ADR-0005).

## Requirements

Numbered for decision-record source lines. Behaviour is in specs/.

| R | Requirement | Spec | Personas | Records |
|---|---|---|---|---|
| R1 | Issue a license: request → key string, reference, identifier, issue time; no records | license-generation | Vendor | PDR-0001 |
| R2 | Contents and issue rules: product ID, reference, key part, issue time, expiry, display name, vendor tag, features, length limits, optional feature definitions; every problem listed | license-generation | Vendor; site owner (no personal data, clear expiry, no feature lost to a typo) | PDR-0004 to PDR-0012, PDR-0014, PDR-0016, PDR-0017, PDR-0023 |
| R3 | Reissue from a verified license | license-generation | Vendor, site owner | PDR-0013, PDR-0021 |
| R4 | Evaluate zero or one key for one product: ordered states, claims vs facts, never throws on key content | license-validation | Implementor, site owner, site visitor, backoffice editor | PDR-0001, PDR-0002, PDR-0003, PDR-0018, PDR-0020, PDR-0022, PDR-0023 |
| R5 | Feature lookup on a valid key | license-validation | Vendor, site owner | PDR-0010, PDR-0019 |
| R6 | Signing keys: create, derived ID, export/import, trusted set, rotation | signing-key-management | Vendor, implementor, site owner | PDR-0015, PDR-0018, ADR-0001 |
| R7 | Key string survives email and fits an environment variable; strict read | license-generation, license-validation | Implementor, site owner, site visitor | ADR-0001, ADR-0002, PDR-0017 |
| R8 | Worked examples kept in step with the library | (docs) | All | ADR-0005 |

Persona check: no requirement harms a higher-priority persona. The costs of one key per product
(PDR-0001) fall on the vendor and implementor, with mitigations recorded there.

## Questions settled in this change

| Q | Question | Answer | Record |
|---|---|---|---|
| Q1 | Empty or whitespace-only key: missing or unreadable? | Missing (Carl, 2026-10-09) | PDR-0003 |
| Q2 | Reissue from an old key: is guidance enough? | Guidance only (Carl, 2026-10-09) | PDR-0013 |
| Q3 | Package and namespace name | `Umbraco.Community.Licensing.Core` (Carl, 2026-10-09) | ADR-0003 |
| Q4 | Personal data: enforce or guide? | Guidance only; field rules are the only enforcement (Carl, 2026-10-09) | PDR-0014 |
| Q5 | Payload names and `expires` format | Confirmed as seeded | ADR-0002 |
| Q6 | Public API surface | As ADR-0004 | ADR-0004 |
| Q7 | Example key strings | From the library, published throwaway key pair, example tests | ADR-0005 |
| Q8 | Expiry with a fraction of a second | Rejected naming `expires`, not rounded (Carl, 2026-10-09) | PDR-0007 |
| Q9 | Feature lookup of the wrong type | Answers not granted (Carl, 2026-10-09) | PDR-0010 |
| Q10 | Invalid product ID given to evaluation | Product ID given when the evaluator is created; invalid ID raises there, at startup. Evaluation never throws (Carl, 2026-10-09) | PDR-0002, ADR-0004 |
| Q11 | Can issuing block a wrong feature type? | Optional feature definitions at issue: wrong type and undefined names rejected; empty list = no features (Carl, 2026-10-09) | PDR-0016, ADR-0004 |
| Q12 | Length limits | Issue and read: product ID 64, feature name 64, 50 features, key string 32,767 (Carl, 2026-10-09) | PDR-0017, ADR-0002 |
| Q13 | A verified key whose contents this product cannot read: unreadable ("paste again") or its own state? | New state *not supported* after *not verified*; claims only; first action update the product, then ask the vendor (Carl, 2026-10-09) | PDR-0018, ADR-0002, ADR-0004 |
| Q14 | Switch lookup vs presence query of any type | Typed lookups only (switch, number, text); no presence query; a switch is granted only when the feature holds a switch (Carl, 2026-10-09) | PDR-0019, ADR-0004 |
| Q15 | State of a key whose text before the first `.` is not a valid identifier | *Unreadable*, no claims, no further check (Carl, 2026-10-09) | PDR-0020, ADR-0001 |
| Q16 | What a reissue request may change | Everything except the product and the reference (Carl, 2026-10-09) | PDR-0021, ADR-0004 |
| Q17 | Cost and frequency of evaluation | Per-request use supported: under 1 ms for a typical key, no memory growth per call; one documentation line for vendors who cache (Carl, 2026-10-09) | PDR-0022, ADR-0001, ADR-0005 |
| Q18 | Rotation guidance vs perpetual keys | Routine rotation never withdraws; withdraw only on compromise, then reissue (Carl, 2026-10-09) | PDR-0015 |
| Q19 | Empty trusted set at evaluation setup | Error at setup, as an invalid product ID (Carl, 2026-10-09) | PDR-0018, ADR-0004 |
| Q20 | Vendor-chosen key identifier prefix (`docs/feature-requests.md` FR-1) | Optional prefix at issuing setup, default `LIC`, or none; 1 to 16 of `A`-`Z` `0`-`9`, error at setup otherwise; evaluation accepts any prefix fitting the rule, no setting; reissue takes the current prefix; typed reference: 10 characters or configured prefix + 10; branding only, stated in vendor documentation (Carl, 2026-10-09) | PDR-0023, ADR-0001, ADR-0002, ADR-0004 |

## Decisions

### Components

```
  src/Umbraco.Community.Licensing.Core/
    Signing/      SigningKeyPair, SigningPrivateKey, SigningPublicKey, TrustedSigningKeys
    Issuing/      KeyPrefix, LicenseRequest, ReissueRequest, LicenseExpiry, FeatureList & values, FeatureType,
                  FeatureDefinitions, LicenseIssuer, IssuedLicense,
                  LicenseIssueException, LicenseProblem
    Evaluation/   LicenseEvaluator, LicenseResult, LicenseState, VerifiedLicense
    Format/       (internal) identifier & alphabet, content rules, payload writer,
                  strict payload reader, key string envelope, random source
```

Folders are internal organisation; all public types share the root namespace (ADR-0004).
Dependency direction: Issuing and Evaluation → Format and Signing; Format → nothing public.
The content rules are one internal component used by both the issuer (to reject) and the strict
reader (to report *not supported*), so issue and read never disagree.

### Signing and envelope — ADR-0001

ECDSA P-256 / SHA-256 / P1363, custom three-segment string with the identifier inside the
signing input (optional vendor prefix inside it, PDR-0023), signing key ID from SHA-256 of SPKI. Private key PKCS#8 PEM; public key
`<id>.<base64url SPKI>`. Verification imports a short-lived `ECDsa` per call for thread safety, within the PDR-0022
budget; per-thread instances are the fallback.

### Payload and strict read — ADR-0002

`Utf8JsonWriter` / `Utf8JsonReader`, own duplicate-name tracking, raw number grammar, exact
date-time format, lengths in Unicode scalar values, length limits on both sides (PDR-0017),
key string length checked before any decoding. A lenient routing read extracts `product`
and `signingKeyId` before verification; the strict read runs only on verified payloads.

### Packaging, naming, clock, dependencies — ADR-0003

One package `Umbraco.Community.Licensing.Core`. `TimeProvider` injected. Library: no
dependencies. Tests: `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
(approved by Carl, 2026-10-09).

### Public API — ADR-0004

Nullable request fields so missing values are reported as problems; non-nullable verified read
types; one `LicenseIssueException` listing every problem; optional `FeatureDefinitions` passed
per issue call; `LicenseIssuer` set up once with an optional `KeyPrefix` (default `LIC`, or none;
invalid prefix throws at setup, PDR-0023); `ReissueRequest` with product and reference fixed (no
setter, PDR-0021), reissued under the issuer's prefix;
`LicenseEvaluator` created per product (product ID and non-empty trusted set checked at
construction); `LicenseResult` with state, claims, verified license and three typed lookups
(`HasSwitch`, `GetNumber`, `GetText`; PDR-0019).

### Evaluation flow

```
 new LicenseEvaluator(productId, trustedKeys, clock)
   productId invalid or trustedKeys empty ────────► throw ArgumentException (startup)

 Evaluate(keyString)                                 never throws
   null / empty after whitespace removal ─────────► Missing
   text before first "." ≠ identifier pattern ────► Unreadable (no claims), stop
   longer than 32,767 characters ─────────────────► Unreadable (+ identifier), not decoded
   3 segments, base64url canonical, sig 64 bytes,
   routing read (product, signingKeyId) ── fail ──► Unreadable (+ identifier)
   product ≠ evaluator's product ─────────────────► WrongProduct (+ claims)
   signingKeyId ∉ trusted ────────────────────────► SigningKeyNotRecognised (+ claims)
   verify(signing input, sig) ── fail ────────────► NotVerified (+ claims)
   strict read ── fail ───────────────────────────► NotSupported (+ claims)
   now (truncated to second) > expires ───────────► Expired (+ VerifiedLicense)
   ───────────────────────────────────────────────► Valid (+ VerifiedLicense)
```

Everything in `Evaluate` runs inside one guard: an unexpected exception from
decoding or crypto becomes *unreadable* rather than escaping (personas: site visitor). The guard
is a safety net; tests assert the specific paths do not rely on it.

### Examples and tests — ADR-0005

Published throwaway key pair `Nb_sm4Fxh5c`; an explicit generator test with an internal seam
fixing the key part and a seam signing a raw payload; committed key strings evaluated by always-run tests; a test that each
string appears in `docs/license-examples.md`.

## Test strategy

Follows from the production bar; detail in ADR-0005.

| Level | What |
|---|---|
| Unit | Each internal component (identifier, rules, writer, reader, envelope, signing, trusted set) |
| Spec | At least one test per `#### Scenario` in specs/, named after it |
| Adversarial | Byte-flip every position of a valid key; hostile-string list; never valid, never throws |
| Culture | `tr-TR`, `de-DE` for lookups and number writing |
| Examples | Committed key strings from `docs/license-examples.md` evaluate as documented |
| Performance | 10,000 evaluations average under 1 ms; no memory growth over 100,000 calls (PDR-0022) |

Validation command for every block: `dotnet test Umbraco.Community.Licensing.slnx`.

## Risks / Trade-offs

- [Format is permanent once keys ship] → ADR-0001 and ADR-0002 reviewed before apply; examples
  exercise every field type.
- [ECDSA signature malleability gives a second valid string per key] → Same identifier and
  contents; keys are bearer tokens. Accepted in ADR-0001.
- [Published example private key] → Trusted only in tests; ADR-0005 and the examples doc say
  so in bold.
- [1 ms budget missed on a slow machine or platform crypto (macOS)] → Per-call `ECDsa` import
  measured by the performance tests; per-thread instances per trusted key as fallback (ADR-0001).
- [No CI matrix in this change] → BCL-only, no paths in the library; CI is a later change.
- [Clock rollback, no revocation, no binding] → Inherent offline limits, documented in the
  README (PDR-0001, PDR-0015).

## Migration Plan

None: first release. No package is published by this change; publishing is Carl's step.

