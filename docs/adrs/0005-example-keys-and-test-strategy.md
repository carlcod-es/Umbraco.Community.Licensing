# ADR-0005: Example key strings and test strategy

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R8, Q7, Q13, Q17 (`CLEAN-PROJECT-PROMPT.md` section 10, technology
  item 4)

## Context

`docs/license-examples.md` must hold real key strings that the library accepts, and must not
drift from it. ECDSA signatures and the reference and key part are random, so fresh output
cannot be compared with committed text.

## Decision

**Throwaway example key pair**, published here, trusted by tests only. Never trust it in a
product: anyone can sign with it.

```
Signing key ID: Nb_sm4Fxh5c

Public key:
Nb_sm4Fxh5c.MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEU3Th2LxtmpokIglgJQMTPGENY3hAKh5m7pAz4inUYfZv-wgUo28dAgl5sOMDF5GZfoT2P-_UEkLARRvYoK0_dA

Private key:
-----BEGIN PRIVATE KEY-----
MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgDSxFHquMwza8xVc8
2go00wyccvzla9OspC5gg9ZDbBmhRANCAARTdOHYvG2amiQiCWAlAxM8YQ1jeEAq
HmbukDPiKdRh9m/7CBSjbx0CCXmw4wMXkZl+hPY/79QSQsBFG9igrT90
-----END PRIVATE KEY-----
```

Generated 2026-10-09 with Node.js `crypto` (P-256, PKCS#8, SPKI) to the ADR-0001 export formats.
A test checks the public key imports, its ID is `Nb_sm4Fxh5c`, and it matches the private key.

**Generating examples.** An explicit (not run by default) xUnit test issues every worked
example with the example private key and a fixed clock, through an internal seam that fixes the
random key part, so identifiers match the document (`LIC-8F3AK-M7RXB-7Q2D`). It prints the key
strings; the Architect pastes them into `docs/license-examples.md` and into the test project's
example constants.

**Example tests** (run always) evaluate each committed key string with the example public key
and a fixed clock and assert the documented state and contents. A test asserts every committed
key string appears verbatim in `docs/license-examples.md` (repository root found by walking up
to `Umbraco.Community.Licensing.slnx`).

**Seams.** `internal` interfaces for randomness (reference and key part), and an internal call
that signs a raw payload (for the *not supported* example: an unknown field signed by a trusted
key), exposed to the test project with `InternalsVisibleTo`. No public test hooks.

**Test strategy** (bar: production, design.md):
- Unit tests per component: identifier and alphabet, issue rules, payload writer and strict
  reader, envelope, signing keys, trusted set, evaluator, feature lookup.
- One or more tests per spec scenario, named after it.
- Tampering tests: flip each byte of the identifier, payload and signature segments of a valid
  key; no variant evaluates as valid, expired or not supported (each needs a verified signature),
  and none throws.
- Garbage tests: a fixed list of hostile strings (empty, 1 MB, control characters, deep JSON,
  wrong padding) never throws.
- Culture tests run lookups and number writing under `tr-TR` and `de-DE`.
- Performance tests (PDR-0022): after warm-up, 10,000 evaluations of a 500-character key average
  under 1 ms (`Stopwatch`). Memory: `GC.GetTotalMemory(true)` after 1,000 calls and after
  100,000 calls differ by less than a fixed 1 MB. Each logs OS, processor count and runtime
  through the test output. Trait `Category=Performance` so they can be filtered on a loaded
  machine.
- CI on Windows, macOS and Linux is not in this change; cross-platform safety rests on BCL-only
  APIs and no paths in the library.

## Alternatives Considered

| Option | Why not |
|---|---|
| Deterministic ECDSA (RFC 6979) | Not exposed by the BCL `ECDsa` |
| Examples as schema only, no strings | Strings are what implementors paste; examples would not prove the library accepts them |
| Separate console tool to generate | A new project and top-level path for a one-off; an explicit test does it |
| Public key-part override | A test hook in the public API invites misuse |

## Consequences

Regenerating examples changes every committed string; done only when the format or examples
change, in the same change as the docs.

## Reversal Cost

Low: test project and docs only.
