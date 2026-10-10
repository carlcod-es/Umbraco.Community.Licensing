# ADR-0001: Signing algorithm, key string envelope and signing key ID

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R6, R7, Q15, Q17, Q20. Carried from the pre-reset signing ADR
  (`CLEAN-PROJECT-PROMPT.md` section 4); not reopened.

## Context

Keys must verify offline, survive email, stay short, and need nothing beyond the BCL on
Windows, macOS and Linux.

## Decision

**Signature.** ECDSA P-256 with SHA-256, signature as 64-byte IEEE P1363 (`r‖s`), via
`System.Security.Cryptography.ECDsa` with `DSASignatureFormat.IeeeP1363FixedFieldConcatenation`.
Imported keys are checked to be on P-256 (OID `1.2.840.10045.3.1.7`); any other curve or key
type is refused.

**Key string.**

```
LIC-8F3AK-M7RXB-7Q2D.eyJzaWduaW5nS2V5SWQiOi...fX0.1AqpE9feDTqwkvVIdd4mRlzJEl...bULr_Q
|__________________| |_________________________| |_________________________|
 key identifier       base64url(payload)          base64url(signature), 86 chars
|_______________________________________________|
 signing input: ASCII bytes of everything before the last "."
```

- Three segments split on `.`; base64url per RFC 4648 section 5, no padding, via
  `System.Buffers.Text.Base64Url`. A segment that does not re-encode to the same text
  (non-canonical trailing bits) is unreadable.
- The signing input includes the visible identifier, so editing it (even lowercasing) fails
  verification. Reference and key part live only in the identifier, not in the payload.
- The identifier starts with the issuer's prefix and `-` (`LIC-` by default), or with no prefix
  (PDR-0023). The prefix is in the signing input, so a relabelled key is *not verified*;
  evaluation needs no prefix setting.
- Reading follows `CLEAN-PROJECT-PROMPT.md` section 4.4: remove every `char.IsWhiteSpace`
  character; the text before the first `.` must fully match
  `^(?:[A-Z0-9]{1,16}-)?[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{4}\z`, otherwise *unreadable* with
  no claims and no further check (PDR-0020); exactly three non-empty segments; signature decodes
  to 64 bytes; payload is a JSON object with string `product` and `signingKeyId`; then wrong
  product, signing key not recognised, not verified; then strict read (ADR-0002), whose failure
  is *not supported* (PDR-0018).

**Signing key ID.** First 8 bytes of SHA-256 over the public key's SubjectPublicKeyInfo DER,
base64url: 11 characters.

**Key export.**
- Private key: PKCS#8 PEM (`-----BEGIN PRIVATE KEY-----`), unencrypted. Vault and file storage
  are the vendor's concern.
- Public key: one line, `<signingKeyId>.<base64url(SPKI DER)>`, e.g.
  `Nb_sm4Fxh5c.MFkwEwYHKoZIzj0CAQYI...`. On import the ID is recomputed and must equal the
  carried ID. One line fits a C# string constant in the product.

**Thread safety.** `ECDsa` instances are not documented as thread-safe. The trusted set holds
SPKI bytes; verification imports a short-lived `ECDsa` per call. The private key wrapper owns
one `ECDsa` and is `IDisposable`; the issuer uses it only within the call.

## Alternatives Considered

| Option | Why not |
|---|---|
| RSA-2048 | 256-byte signature (342 base64url characters) for no gain |
| Ed25519 | Not in the BCL on every supported platform; needs a third-party library |
| JWT / JWS | `alg` header invites downgrade (`alg: none`); needs a dependency or hand-rolled JOSE anyway |
| DER signature format | Variable length; P1363 is fixed 64 bytes |
| Public key as PEM | Multi-line; awkward in a C# constant and in environment variables; carries no ID |
| Encrypted PKCS#8 for the private key | Password handling belongs to vendor tooling (deferred issuing add-on) |

## Consequences

- ECDSA signatures are randomised: issuing the same request twice gives different key strings.
  Example tests verify committed strings rather than compare fresh output (ADR-0005).
- ECDSA malleability: `(r, n−s)` also verifies, so a second valid string exists for every key.
  It carries the same identifier and contents, and a key is a bearer token already; accepted.
- Typical keys are 250 to 500 characters.
- Importing an `ECDsa` per call must fit PDR-0022 (under 1 ms per evaluation, no memory growth);
  each instance is disposed within the call. Measured by the performance tests (ADR-0005). If a
  platform misses the budget, the fallback is one `ECDsa` per thread per trusted key held by the
  evaluator: bounded by threads × keys, never by calls.

## Reversal Cost

High after release: every issued key and every shipped trusted public key depends on it. A
change would need a new envelope distinguishable from this one.
