# ADR-0007: Compact payload encoding and issue length check

- Status: Decided
- Date: 2026-10-12
- Source: `add-license-core` design.md Q26. Amends ADR-0002 (encoder, field names, date format,
  worst-case arithmetic) and ADR-0001 (routing-read field names).

## Context

ADR-0002 wrote the payload with the `Utf8JsonWriter` default encoder, which is built for JSON
embedded in HTML: it escapes every non-ASCII character as `\uXXXX` (6 bytes; 12 outside the
BMP) and `" & ' + < >`. Its worst case (about 22,425 characters) assumed ASCII text; with
non-ASCII text a request passing every issue rule produced a key over the 32,767 read cap, which
then read as *unreadable* (supervisor finding, section 5). The payload is base64url-encoded and
never placed in HTML, so that escaping protects nothing. Carl accepts a less readable decoded
payload for a shorter key (2026-10-12), overriding ADR-0002's "readable when decoded" goal.

## Decision

**Encoder.** The payload writer escapes text values itself: only `"` (`\"`), `\` (`\\`) and
U+0000 to U+001F (upper-case `\uXXXX`); every other character is written as raw UTF-8. It writes
the quoted token with `Utf8JsonWriter.WriteRawValue`, validation on. Field names are fixed ASCII
and feature names `[a-z0-9-]`, so the writer's own name encoding leaves them unchanged. The
content rules already refuse control characters, lone surrogates, U+2028 and U+2029, so in
practice only `"` and `\` are escaped (2 bytes each). The reader is unchanged: it accepts raw
and escaped forms. (Amended 2026-10-12 during tasks.md 4.5; first written as a
`JavaScriptEncoder` subclass, same output bytes.)

**Field names.**

| ADR-0002 | Now | Absent (unchanged from ADR-0002) |
|---|---|---|
| `signingKeyId` | `k` | unreadable (routing read) |
| `product` | `p` | unreadable (routing read) |
| `issued` | `i` | not supported |
| `expires` | `e` | perpetual |
| `displayName` | `n` | none |
| `vendorTag` | `t` | none |
| `features` | `f` | none |

Field order, omissions, feature value types, number grammar, duplicate tracking and
unknown-field refusal stay as ADR-0002. Feature names are vendor content and stay in full.

**Dates.** `i` and `e` are JSON numbers holding Unix seconds, UTC. Write: the instant's whole
seconds as an integer. Read: the raw token must match `0|[1-9][0-9]{0,11}` and be at most
253402300799 (9999-12-31T23:59:59Z); a string, fraction, sign, exponent, leading zero or larger
value is *not supported*. Seconds are the unit, so PDR-0006 and PDR-0007 (to the second) hold.

```json
{"k":"Nb_sm4Fxh5c","p":"acme.commerce","i":1788249912,"e":1803945599,"n":"Commerce Pro",
 "t":"CUST-0042","f":{"ecommerce":true,"max-orders":2500,"licensed-domains":"example.com,shop.example.com"}}
```

**Routing read** extracts `p` and `k` (ADR-0001's "string `product` and `signingKeyId`").

**Issue length check** (PDR-0025). After the content rules pass, the issuer composes and signs
the key string; if it is longer than 32,767 characters it throws `LicenseIssueException` with one
problem naming `features` and the excess. The check is on the composed string, outside the
content rules, because only the composed key has a length; the signature is fixed at 86
characters, so the result does not depend on signing randomness.

**Worst case** (replaces ADR-0002's arithmetic). Every field at its limit: 64-character product
ID, display name and vendor tag; 50 text features, 64-character names, 256-character values.

| Text characters | Key length |
|---|---|
| ASCII letters and digits | 22,212 |
| `"` or `\` throughout | 39,364 |
| Accented (2 bytes) | 39,364 |
| CJK (3 bytes) | 56,516 |
| Emoji (4 bytes) | 73,668 |

No encoding fits the bottom rows: 12,800 arbitrary Unicode scalars need about 20 bits each,
about 32,000 bytes, against about 24,400 payload bytes inside the cap. Typical key
(`docs/license-examples.md` example 2): 471 characters under ADR-0002, 368 now.

## Alternatives Considered

| Option | Why not |
|---|---|
| Default encoder (ADR-0002) | 6× to 12× growth for non-ASCII and five ASCII characters; HTML protection irrelevant here |
| `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` | Believed still to escape characters outside the BMP (not verified); the writer's own escaping is smallest for every input and depends on no built-in encoder's ranges |
| Internal `JavaScriptEncoder` subclass | Its overridable members (`FindFirstCharacterToEncode`, `TryEncodeUnicodeScalar`) take pointers and need `AllowUnsafeBlocks`; no unsafe code in a security library. Same bytes as the writer's own escaping (Carl, 2026-10-12) |
| Full field names | About 70 characters more per key; Carl preferred shorter keys |
| ISO 8601 basic dates (`20260901T080512Z`) | Saves about 11 characters against 32 for Unix seconds |
| Signing key ID 11 → 6 characters | Saves 6; changes ADR-0001 and the public key export products embed |
| Positional array | Saves 38 more; optional fields need placeholders, strict read moves to arity checks, a decoded payload is meaningless without the spec |
| Deflate compression | Little gain on 200 to 300 bytes, none on the worst case; adds decompression-bomb handling on read |

## Consequences

- A decoded payload is read with the label table above.
- Keys written to ADR-0002's format read as *not supported* (unknown fields). None were released
  (PDR-0024); the worked-example keys are generated after this change (tasks.md 7.1).
- Tasks.md 4.5 changes the writer and both readers; 5.5 adds the length check.

## Reversal Cost

Low before the first release of keys; high from 1.0, as ADR-0001 and ADR-0002.
