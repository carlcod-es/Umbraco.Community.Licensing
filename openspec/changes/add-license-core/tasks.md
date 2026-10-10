Owners per CLAUDE.md: sections 1 to 7 worker-backend (`src/**`, `tests/**`); section 8 Architect
(`docs/`, `README.md`). Validation for every block: `dotnet build Umbraco.Community.Licensing.slnx`
and `dotnet test Umbraco.Community.Licensing.slnx`. Bar: production (design.md). Test names
follow spec scenario names.

## 1. Projects and test harness

Depends on: none. Shared files assigned in the brief: the two project entries in
`Umbraco.Community.Licensing.slnx`, package metadata in the library project file, test
package references (ADR-0003; dependencies approved by Carl, 2026-10-09).

- [ ] 1.1 Create `src/Umbraco.Community.Licensing.Core/` (net10.0 from `Directory.Build.props`, root namespace and PackageId `Umbraco.Community.Licensing.Core`, `GenerateDocumentationFile`, MIT licence expression, description, no package references, `InternalsVisibleTo` the test project) and add it to the solution, and verify `dotnet build` succeeds and `dotnet list package` shows no references
- [ ] 1.2 Create `tests/Umbraco.Community.Licensing.Core.Tests/` with `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` pinned to stable versions at least two weeks old, referencing the library, added to the solution, and verify `dotnet test` runs and reports one passing placeholder-free smoke test (library assembly loads)
- [ ] 1.3 Add a test `FixedTimeProvider` (settable UTC now) and a culture-scope helper (`tr-TR`, `de-DE`), and verify by tests that `GetUtcNow` returns the set instant and the culture is restored after the scope

## 2. Content rules and identifiers (internal)

Depends on: 1. Spec: license-generation (product ID, reference, expiry, display name, vendor tag,
feature rules, length limits, key identifier prefix). Records: PDR-0004 to PDR-0012, PDR-0017,
PDR-0023, ADR-0001 (identifier pattern), ADR-0002 (text and lengths).

- [ ] 2.1 Implement the reference alphabet (31 characters, no `0 O 1 I L`), an internal random source seam, reference generation (10), key part generation (4), the prefix rule (1 to 16 of `A`-`Z` `0`-`9`), display forms with a prefix or none, and the identifier pattern with an optional prefix (ADR-0001), and verify by tests that generated values use only the alphabet, the seam fixes values, `LIC-8F3AK-M7RXB-7Q2D`, `ACME-8F3AK-M7RXB-7Q2D`, `ACMECOMMERCE2026-8F3AK-M7RXB-7Q2D` and `8F3AK-M7RXB-7Q2D` match, while lowercase, wrong-length, `acme-` and 17-character-prefix forms do not
- [ ] 2.2 Implement reference parsing against the configured prefix (ignoring case, hyphens and spaces, accept 10 characters, or the configured prefix followed by 10; with no prefix only 10; anything else rejected naming `reference`), and verify by the "Reference accepted loosely", "Reference with a vendor prefix", "Reference with another prefix or a key part" and "Invalid reference" scenarios
- [ ] 2.3 Implement the product ID, display name, vendor tag, feature name, number (grammar, 4 decimals, 15 digits, trailing-zero normalisation, invariant culture) and text rules, with the PDR-0017 limits (product ID 64, feature name 64, 50 features), as one internal rule set returning problems with field names, with lengths in Unicode scalar values and unpaired surrogates rejected, and verify by tests covering every valid and invalid input in the license-generation rule scenarios (including "Product ID too long" and "Too many features" at and over each limit) and `docs/license-examples.md` example 14, run also under `tr-TR` and `de-DE`
- [ ] 2.4 Implement expiry resolution (date alone → 23:59:59Z, any offset → UTC, fraction rejected, earlier than the current second rejected, equal accepted) against a clock, and verify by the "Expiry is stated" scenarios

## 3. Signing keys

Depends on: 1. Spec: signing-key-management. Records: PDR-0015, ADR-0001, ADR-0004.

- [ ] 3.1 Implement `SigningKeyPair.Create`, `SigningPrivateKey` (PKCS#8 PEM export/import, P-256 only, `IDisposable`, exposes its public key and ID) and the derived signing key ID (first 8 bytes of SHA-256 over SPKI DER, base64url), and verify by the "New pair", "Pairs are independent", "Private key knows its ID" and "Private key round trip" scenarios
- [ ] 3.2 Implement `SigningPublicKey` export `<id>.<base64url SPKI>` and `Parse` (recompute and compare ID, P-256 only, `FormatException` otherwise), and verify by the "Public key round trip", "Carried ID altered", "Not a key" and "Public export holds no private material" scenarios, plus a test that the ADR-0005 example public key parses with ID `Nb_sm4Fxh5c` and matches the example private key
- [ ] 3.3 Implement immutable `TrustedSigningKeys` (`Empty`, `Create`, `With`, `Without`, `Contains`, `SigningKeyIds`) holding SPKI bytes, and verify by the "Several keys", "Same key twice" and "Different key under a held ID" scenarios (the last using two keys forced to one ID through an internal constructor)

## 4. Payload and key string envelope (internal)

Depends on: 2, 3. Spec: license-validation (reading a supplied string, strict reading),
license-generation (key string shape). Records: PDR-0018, PDR-0020, PDR-0023, ADR-0001, ADR-0002.

- [ ] 4.1 Implement the payload writer (`Utf8JsonWriter`, field order and omissions per ADR-0002, numbers as raw normalised text, dates `yyyy-MM-ddTHH:mm:ssZ`), and verify by tests asserting exact JSON for the ADR-0002 sample and that `features` and optional fields are omitted when empty
- [ ] 4.2 Implement the lenient routing read (`product`, `signingKeyId` only; any malformed input → unreadable, never throws), and verify by tests for non-object JSON, missing or non-string fields, invalid UTF-8 and truncated JSON
- [ ] 4.3 Implement the strict reader (unknown field, repeated field or feature name, exact date format, `null`/`""` optional fields, unsupported feature token types, raw number grammar, every content rule and limit from 2.3, escaped allowed characters accepted; a failure is a result the evaluator maps to *not supported*), and verify by one test per strict-read case in ADR-0002 and the "Faulty vendor tool", "Unknown field" and "Over a limit" payloads
- [ ] 4.4 Implement the envelope: compose `identifier.payload.signature`; read by removing `char.IsWhiteSpace`, requiring the text before the first `.` to match the identifier pattern (otherwise unreadable with no claims, no further step), refusing more than 32,767 characters before any decoding, splitting exactly three non-empty segments, canonical base64url decoding, 64-byte signature check, and verify by the "Wrapped by an email client", "Cut off", "No identifier", "Identifier typo" and "Too long" read cases, a 32,767-character boundary test, and a non-canonical-base64url test

## 5. Issuing

Depends on: 2, 3, 4. Spec: license-generation. Records: PDR-0004 to PDR-0014, PDR-0016, PDR-0021, PDR-0023, ADR-0004.

- [ ] 5.1 Implement `KeyPrefix` (`Default` = `LIC`, `None`, `Of` raising `ArgumentException` on a prefix breaking the 2.1 rule) and the request types (`LicenseRequest`, `LicenseExpiry`, `FeatureValue` family, `LicenseFeature`, `FeatureList` with `With`/`Without`/`Of`), and verify by the "Longest prefix" and "Invalid prefix" scenarios and tests that `With` replaces a same-name entry ignoring `a`-`z` case, `Of` keeps duplicates, and order is preserved
- [ ] 5.2 Implement `LicenseIssuer` (constructor takes an optional `KeyPrefix`, `null` meaning `Default`, and clock) and `Issue` for `LicenseRequest` (collect every problem, then throw one `LicenseIssueException`; otherwise generate or parse the reference against the issuer's prefix, generate the key part, build the identifier with the prefix, set issue time from the clock truncated to the second, write payload, sign with P1363, return `IssuedLicense` whose `ToString` omits the key string; retain nothing), and verify by the "Typical issue", "Minimal issue", "Several problems", "First issue generates a reference", "Reissue keeps the reference, new key part", "Issue time from the clock", "Pasteable key", "Default prefix", "Vendor prefix" and "No prefix" (issue) scenarios
- [ ] 5.3 Implement `FeatureType`, immutable `FeatureDefinitions` (`None`, `Switch`, `Number`, `Text`; invalid or repeated name raises `ArgumentException`) and the optional `definitions` parameter of `Issue` (undefined name or other type rejected naming `features.<name>`, problems added to the same list; `null` skips the check), and verify by every "Optional feature definitions" scenario and the two definition rows of `docs/license-examples.md` example 14
- [ ] 5.4 Implement `ReissueRequest` (internal constructor; `Product` and `Reference` with no setter, other fields `init`), `ReissueRequest.From(VerifiedLicense)` (no clock, no checks, perpetual stated explicitly) and the `Issue(ReissueRequest, …)` overload (same internal pipeline as 5.2; the reference's 10 characters kept under the issuer's prefix without typed-reference parsing, new key part), and verify by the "Add a feature", "Prefilled past expiry", "Perpetual carried over", "Product and reference kept" and "Prefix changed by the vendor" scenarios, the last asserting by reflection that `Product` and `Reference` have no setter (using an evaluator stub only if section 6 is not yet available; otherwise the real evaluator)

## 6. Evaluation

Depends on: 3, 4, 5. Spec: license-validation, signing-key-management (rotation). Records: PDR-0001 to PDR-0003, PDR-0010, PDR-0015, PDR-0018 to PDR-0020, PDR-0022, PDR-0023, ADR-0001, ADR-0004, ADR-0005.

- [ ] 6.1 Implement `LicenseEvaluator` (constructor takes product ID, trusted keys and clock and raises `ArgumentException` on an invalid product ID or an empty trusted set), `Evaluate(keyString)`, `LicenseState`, `LicenseResult` and `VerifiedLicense` per the design.md evaluation flow (missing for null, empty or whitespace-only; ordered checks; strict read after verification, failure → *not supported*; expiry against the clock truncated to the second; outer guard mapping unexpected exceptions to unreadable), and verify by every scenario under "Evaluate one key for one product" (including "Empty trusted set"), "Missing key", the "Other prefix accepted", "No prefix" (evaluation) and "Relabelled key" read scenarios, "One state, first failing check wins" (including "Verified but not readable by this product"), "Strict reading of verified contents" and "Expiry boundary"
- [ ] 6.2 Implement result reporting (claims only for failed states, facts for expired and valid, `ToString` with state and identifier only), and verify by the "Unverified claims stay claims", "Expired reports what lapsed" and "No key text in results" scenarios, the last asserting no segment of the key string appears in `ToString` of the result or the verified license
- [ ] 6.3 Implement `HasSwitch`, `GetNumber`, `GetText` (valid only; ordinal ignore `a`-`z` case; wrong type → not granted; no lookup of any type), and verify by every "Feature lookup" scenario (including "Switch lookup on text"), including under `tr-TR`
- [ ] 6.4 Add the rotation tests, and verify by the "Old and new trusted together", "Perpetual key after routine rotation", "Old key withdrawn" and "Validation needs only public keys" scenarios
- [ ] 6.5 Add adversarial tests: flip every byte position of the identifier, payload and signature segments of a valid key, and a hostile-string list (empty, 32,768 and 1 MB random, control characters, 10,000-deep JSON payload, padded base64, extra segments), and verify that no case returns *valid*, *expired* or *not supported* and none throws
- [ ] 6.6 Add performance tests (trait `Category=Performance`, OS, processor count and runtime logged to test output): after warm-up, 10,000 evaluations of a valid 500-character key average under 1 ms; `GC.GetTotalMemory(true)` after 1,000 and after 100,000 evaluations differ by less than 1 MB; and verify by the "Per-request cost", "No memory growth" and "A result is a snapshot" scenarios

## 7. Worked examples

Depends on: 5, 6. Records: PDR-0015, PDR-0018, PDR-0020, PDR-0023, ADR-0005. Shared: `docs/license-examples.md` is read by a test only;
the Architect pastes the strings (8.1).

- [ ] 7.1 Add an explicit (not run by default) generator test that issues examples 1 to 7, 11, 13 and 17 of `docs/license-examples.md` with the ADR-0005 example private key, a fixed clock and the random seam fixing the documented identifiers, and prints each key string; add the variants example 11 needs (one character dropped, cut off, wrong product, signed by a second throwaway key, edited payload, identifier hand-edited to `...-7Q2O`, and `LIC-8F3AK-M7RXB-W4TN` with an unknown field signed by the example key through the internal raw-payload seam), and the example 17 keys (`ACME-8F3AK-M7RXB-7Q2D` issued with prefix `ACME`, `8F3AK-M7RXB-7Q2D` for `zenith.commerce-shipping` with no prefix, and the `ACME-` key relabelled `BETA-`), and verify by running it explicitly and observing the printed strings
- [ ] 7.2 Commit the printed strings as test constants and add always-run example tests evaluating each with the example public key at the documented time, asserting the documented state, identifier and contents, plus example 16 (example 1 evaluated with the example key and the second throwaway key both trusted is *valid*), and verify by `dotnet test`
- [ ] 7.3 Add a test asserting every example key string constant appears verbatim in `docs/license-examples.md` (repository root found by walking up to `Umbraco.Community.Licensing.slnx`), and verify it fails before 8.1 and passes after

## 8. Documentation

Depends on: 6 (8.2), 7 (8.1). Owner: Architect.

- [ ] 8.1 Paste the key strings from 7.1 into the "Key strings" section of `docs/license-examples.md` and update its "Last checked against" line, and verify by the 7.3 test passing
- [ ] 8.2 Write the `README.md` usage section (create keys, issue, reissue, evaluate, look up features, rotate) and the vendor and implementor notes from `CLEAN-PROJECT-PROMPT.md` section 11 plus PDR-0014 (no personal data), the first action per state including *not supported* (PDR-0018), the one-line caching note (PDR-0022), withdrawal of a signing key only on compromise followed by reissue (PDR-0015), the issuing prefix as branding only, protected by the private signing key and the product's trusted set (PDR-0023), and the inherent offline limits, and verify by Carl's review of the README
