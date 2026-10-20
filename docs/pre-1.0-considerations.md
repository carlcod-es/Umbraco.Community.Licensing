# Pre-1.0 considerations

Observations from the library review of 2026-10-20 (library at `0.1.0-beta.2`, commit `e9da4b1`),
kept for consideration before 1.0. None is decided. Each one that is taken up becomes a design.md
Q-number in a change and is settled by a PDR or ADR; record that change in its Status column.

Cite as `pre-1.0-considerations.md` C1 and so on.

Product items from the same review are feature requests: FR-7 (standard first-action text),
FR-8 (same status wording across vendors' screens), FR-9 (grace period), in
[`feature-requests.md`](feature-requests.md). The key format is documented in
[`key-format.md`](key-format.md).

Review baseline: 816 tests pass, 1 skipped (macOS, arm64). Evaluation of a 288-character key
averages about 130 µs on macOS, against the 1 ms requirement of PDR-0022; creating `ECDsa` and
importing the key per call is about 6.5 µs of that.

| C | Title | Kind | Before 1.0 because | Status |
|---|---|---|---|---|
| C1 | Fields to settle before the format freezes | Product | Format permanent from 1.0 | Open |
| C2 | Signing without exporting the private key | Technology | No; additive API | Open |
| C3 | Symbols package | Packaging | No; cheap | Open |
| C4 | Built-in code analysis | Build | No; cheap | Open |
| C5 | API compatibility check from 1.0 | Build | Baseline is 1.0 | Open |
| C6 | Package metadata and Marketplace listing | Packaging / product | No | Open |
| C7 | Release runner exposure to other public repositories | Security | No; act soon | Open |
| C8 | `VerifiedLicense` can be constructed and edited by callers | API | Breaking change | Open |
| C9 | Unexplained index in `LicenseEvaluator` | Code | No | Open |
| C10 | Oversized key string copied before refusal | Code | No | Open |

## C1: Fields to settle before the format freezes

From 1.0 the key format is permanent (PDR-0024). A new payload field after that makes every older
product report new keys that carry it as *not supported* (ADR-0002, PDR-0018). Writers omit
optional fields, so only keys that use a new field are affected, but the vendor must then require
the newer product version.

Candidates to decide in or out now:

- Grace period set by the vendor in the key (FR-9 issue 1).
- "Not valid before" date (a key issued now that starts later).
- Domain or installation binding as a signed field, rather than a `licensed-domains` text feature
  the product enforces (README "Limits of offline licensing").

Personas: site owner (keys keep working across product versions), vendor (fewer forced product
updates).

## C2: Signing without exporting the private key

`LicenseIssuer.Issue` needs a `SigningPrivateKey`, which today comes only from an unencrypted
PKCS#8 PEM (`SigningPrivateKey.FromPem`) or `SigningKeyPair.Create`. ADR-0001 leaves storage to the
vendor, but a key held in an HSM or a cloud key vault never leaves it, so it cannot be used.

Options, all within the BCL:

1. `SigningPrivateKey.FromECDsa(ECDsa)`: accepts any `ECDsa` implementation, including
   HSM- or vault-backed subclasses. Checks P-256 as `FromPem` does.
2. A small signer abstraction (public key plus a sign-bytes function) that the issuer calls.
   Azure Key Vault `ES256` returns the 64-byte `r‖s` signature the format already uses.

No key format change. Reduces the chance of the event FR-5 handles (leaked private key). Would
amend ADR-0001. Personas: vendor; no other persona affected.

## C3: Symbols package

The package holds only the dll, XML documentation and README. Source Link commit metadata is
present (`<repository ... commit=...>`), but no symbols are published, so vendors cannot step into
the library. Set `IncludeSymbols` and `SymbolPackageFormat=snupkg`; `dotnet nuget push` uploads
the `.snupkg` next to the `.nupkg` with no workflow change. Would amend ADR-0003 or ADR-0006.

## C4: Built-in code analysis

`<AnalysisLevel>latest-recommended</AnalysisLevel>` in `Directory.Build.props` enables the SDK's
own CA rules; with `TreatWarningsAsErrors` they become binding. No new dependency. Expect a first
pass of fixes or justified suppressions.

## C5: API compatibility check from 1.0

`EnablePackageValidation` with `PackageValidationBaselineVersion` set to the 1.0 version (SDK
built in, no dependency) fails the build on an accidental breaking change to the public API.
Pointless before 1.0 because the beta has no stability promise (PDR-0024); set it in the change
that releases 1.0.

## C6: Package metadata and Marketplace listing

No `PackageIcon` or `PackageProjectUrl`; nuget.org shows the default icon. Whether to add the
`umbraco-marketplace` tag, which lists the package in the Umbraco Marketplace, is a product call:
the audience is package vendors, not site builders.

## C7: Release runner exposure to other public repositories

The self-hosted release runner (MINIDEV) is in an organisation runner group that allows public
repositories (`add-release-pipeline` `follow-ups.md` handover, 2026-10-19). `release.yml` runs
only on tag pushes and manual dispatch, so a fork cannot trigger it. But any other public
repository in `carlcod-es` with a pull-request workflow using `runs-on: self-hosted` could run
fork code on MINIDEV, the machine that publishes to NuGet.

Options: restrict the runner group to selected repositories (this one); require approval for
workflows from outside contributors' pull requests. Repository and organisation settings, Carl's
call. Related: `add-release-pipeline` `follow-ups.md` items 24 and 29.

## C8: `VerifiedLicense` can be constructed and edited by callers

`VerifiedLicense` is a public positional record (ADR-0004), so callers can construct one or use
`with` to change any value, for example its `Product`, before `ReissueRequest.From`. Its
documentation says "every value here is a verified fact", which does not hold for an instance the
evaluator did not produce.

Impact is small: `LicenseRequest` already lets a vendor set any product and reference, so
PDR-0021 is not bypassed in a way that matters. The fix (internal constructor, get-only
properties, sealed class instead of positional record) is a breaking API change, which the beta
allows (PDR-0024) and 1.0 does not. Would amend ADR-0004.

## C9: Unexplained index in `LicenseEvaluator`

`LicenseEvaluator.cs:116`, `identifier[..^17]`, recovers the prefix by counting back past the
16-character `XXXXX-XXXXX-XXXX` part and its hyphen. A `KeyIdentifierFormat.PrefixOf` helper
next to `ReferenceOf` would keep the identifier layout in one class.

## C10: Oversized key string copied before refusal

`KeyEnvelope.cs:65` removes whitespace (copying the string when any is present) before the
32,767-character check. A very large configuration value is copied once before it is refused.
The implementor controls the value, so the risk is low. A first check against a generous raw bound
would avoid the copy.
