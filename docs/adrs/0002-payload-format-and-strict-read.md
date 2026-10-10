# ADR-0002: Payload format and strict read

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R7, Q5, Q12, Q13, Q20 (`CLEAN-PROJECT-PROMPT.md` section 10,
  technology item 1)

## Context

The payload must be readable when decoded, compact, and read so strictly that a future or
malformed field is refused rather than half-read.

## Decision

UTF-8 JSON, no whitespace, written with `Utf8JsonWriter` (default encoder) and read with
`Utf8JsonReader`. Field order on write is as below.

```json
{"signingKeyId":"0F8NSYaNR_Q","product":"acme.commerce","issued":"2026-09-01T08:05:12Z",
 "expires":"2027-03-01T23:59:59Z","displayName":"Commerce Pro","vendorTag":"CUST-0042",
 "features":{"ecommerce":true,"max-orders":2500,"licensed-domains":"example.com,shop.example.com"}}
```

| Field | Encoding | Absent |
|---|---|---|
| `signingKeyId` | string, 11 characters | unreadable (routing read, before verification) |
| `product` | string, product ID rule | unreadable (routing read, before verification) |
| `issued` | exactly `yyyy-MM-ddTHH:mm:ssZ`, UTC | not supported |
| `expires` | exactly `yyyy-MM-ddTHH:mm:ssZ`, UTC | perpetual. `null` not supported |
| `displayName` | string, display name rule | none. `null`, `""` not supported |
| `vendorTag` | string, vendor tag rule | none. `null`, `""` not supported |
| `features` | object, name to value | none; `{}` also none. Written only when non-empty |

Feature type is the JSON token type: `true` switch; number; string text. `false`, `null`,
object, array: not supported.

**Names confirmed** (Q5): `displayName`, `vendorTag`; `issued` and `expires` as
`yyyy-MM-ddTHH:mm:ssZ`, parsed with `DateTimeOffset.ParseExact`, invariant culture, UTC.

**Numbers.** Grammar on issue and read: `0` or `[1-9][0-9]*`, optional `.` and 1 to 4 digits,
at most 15 digits in total; no sign, exponent or leading zero. Issue takes a `decimal`, strips
trailing fractional zeros and writes the invariant text as a raw JSON value (`2.50` → `2.5`).
Read checks the raw token bytes against the grammar, then parses `decimal`; never `double`.

**Text and lengths.** Lengths are counted in Unicode scalar values (`Rune`). Strings with
unpaired surrogates are rejected at issue (they cannot round-trip through UTF-8). "Control
characters" means categories Cc, plus U+2028 and U+2029; whitespace means `char.IsWhiteSpace`.
Non-ASCII is escaped by the default encoder on write; the reader unescapes.

**Strict read.** Runs only on a verified payload; any failure gives *not supported* (PDR-0018).
A verified payload fails if it has an unknown field; a repeated field
or feature name (the reader tracks names itself, as `System.Text.Json` does not); a date-time
not in its exact format; any value breaking an issue rule. Unknown feature *names* are accepted.
No version field: an unknown field being refused already guards format changes.

**Vendor tag on read.** The decoded string must match `^[A-Za-z0-9_.#/-]{1,64}\z` (ordinal;
`\z`, not `$`, which matches before a trailing `\n`). Escaped forms of allowed characters are
accepted. The default encoder escapes none of them on write.

**Length limits** (PDR-0017). Product ID 64 characters, feature name 64, 50 features: checked by
the shared content rules, so issue rejects and strict read gives *not supported*. Key string: after
whitespace removal and identifier extraction, a string longer than 32,767 characters is
unreadable before any base64url decoding or JSON reading. Worst case at the limits: payload
about 16,700 characters, base64url about 22,300, plus identifier (at most 33 with a 16-character prefix,
PDR-0023) and signature about 125, so about 22,425: inside the cap.

**Routing read** (before verification) uses the same reader in a lenient mode that extracts only
`product` and `signingKeyId`; any `JsonException` is caught and gives *unreadable*.

## Alternatives Considered

| Option | Why not |
|---|---|
| `JsonSerializer` with records | Silently accepts duplicate properties (last wins) and reads numbers via binary paths unless configured; strictness would be bolted on |
| CBOR or a custom binary payload | Not readable when decoded; `System.Formats.Cbor` is a NuGet package, not in the shared framework |
| A version field | Redundant with unknown-field refusal |
| Expiry as Unix seconds | Not readable when decoded |
| Lengths in UTF-16 code units | An emoji would count as 2; people count it as 1 |

## Consequences

Any future field is a format change that older readers report as *not supported* (first action:
update the product); a vendor issuing new-format keys must require the new library version.

## Reversal Cost

High after release, as ADR-0001: issued keys depend on it.
