# Key format

The license key string, byte for byte: what each part holds, how it is encoded, signed and read.
Written so a key can be decoded by hand and verified with any ECDSA library.

- **Stability.** Beta: any 0.x release may change this format (PDR-0024). From 1.0 it is
  permanent.
- **Sources.** ADR-0001 (envelope, identifier, signature), ADR-0002 as amended by ADR-0007
  (payload). Where this document and an ADR disagree, the ADR wins and this document is a bug.
- **Behaviour and product rules** (states, expiry, reissue, worked scenarios):
  [`license-examples.md`](license-examples.md).

Last checked against: ADR-0001, ADR-0002, ADR-0007 and the library at `0.1.0-beta.2`
(2026-10-20).

## At a glance

```
LIC-8F3AK-M7RXB-7Q2D . eyJrIjoiTmJfc200RnhoNWMiLCJwIjoi... . qp39tt3VnjxW0cO17BLwSvU_2P9K...
|__________________|   |_______________________________|   |___________________________|
 key identifier         base64url(payload JSON)             base64url(signature)
 ASCII, readable        UTF-8 JSON object                   64 bytes, always 86 chars
|________________________________________________________|
 signing input: the ASCII bytes of everything before the second "."
```

- Three segments separated by `.`. No segment contains a `.`.
- base64url is RFC 4648 section 5: alphabet `A-Z a-z 0-9 - _`, no `=` padding. A segment that
  does not re-encode to exactly the same text (stray trailing bits, padding) is refused.
- The whole string is printable ASCII with no whitespace. At most 32,767 characters.

## Worked example

`license-examples.md` key string 2 (signed with the public example key `Nb_sm4Fxh5c`; never
trust it in a product):

```
LIC-8F3AK-M7RXB-7Q2D.eyJrIjoiTmJfc200RnhoNWMiLCJwIjoiYWNtZS5jb21tZXJjZSIsImkiOjE3OTA4NDE2MDAsImUiOjE4MDM5NDU1OTksIm4iOiJDb21tZXJjZSBTdGFuZGFyZCIsInQiOiJDVVNULTAwNDIiLCJmIjp7ImVjb21tZXJjZSI6dHJ1ZSwibWF4LW9yZGVycyI6NTAwfX0.qp39tt3VnjxW0cO17BLwSvU_2P9KddI92Kl1gvpJUCcxpc735rxVX6hihYv1nrHARP9rS6KJaiT2JwRgpA12Tw
```

307 characters: identifier 20, payload 199, signature 86, two dots.

**1. Identifier** `LIC-8F3AK-M7RXB-7Q2D`

| Part | Value | Meaning |
|---|---|---|
| Prefix | `LIC` | Issuer's branding; may be another prefix or absent |
| Reference | `8F3AK-M7RXB` | Names the license; kept across reissues |
| Key part | `7Q2D` | New at every issue; tells two keys of one license apart |

**2. Payload** base64url-decoded, 149 bytes of UTF-8 JSON:

```json
{"k":"Nb_sm4Fxh5c","p":"acme.commerce","i":1790841600,"e":1803945599,"n":"Commerce Standard","t":"CUST-0042","f":{"ecommerce":true,"max-orders":500}}
```

| Label | Value | Meaning |
|---|---|---|
| `k` | `Nb_sm4Fxh5c` | Signing key ID: which trusted public key verifies this key |
| `p` | `acme.commerce` | Product ID |
| `i` | `1790841600` | Issued 2026-10-01T08:00:00Z (Unix seconds) |
| `e` | `1803945599` | Expires 2027-03-01T23:59:59Z; valid through that second |
| `n` | `Commerce Standard` | Display name |
| `t` | `CUST-0042` | Vendor tag |
| `f` | `ecommerce: true`, `max-orders: 500` | Features: one switch, one number |

**3. Signature** decodes to 64 bytes: `r` (32 bytes) then `s` (32 bytes).

**4. Verification.** ECDSA P-256 over SHA-256 of the signing input
`LIC-8F3AK-M7RXB-7Q2D.eyJrIjoi...fX0` (ASCII bytes), with the public key whose ID is `k`.

Independent check in Node.js, no library code involved:

```js
const crypto = require("node:crypto");
const key = "LIC-8F3AK-M7RXB-7Q2D.eyJrIjoi...";        // the full key string above
const spki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...";  // public key text after the "."

const [identifier, payload, signature] = key.split(".");
console.log(Buffer.from(payload, "base64url").toString("utf8"));     // the JSON above
const publicKey = crypto.createPublicKey({ key: Buffer.from(spki, "base64url"), format: "der", type: "spki" });
console.log(crypto.verify("sha256", Buffer.from(`${identifier}.${payload}`, "ascii"),
  { key: publicKey, dsaEncoding: "ieee-p1363" }, Buffer.from(signature, "base64url")));  // true
```

## Key identifier

```
^(?:[A-Z0-9]{1,16}-)?[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{4}$
  |_________________| |______________________________| |_______________|
  optional prefix      reference (10 characters)         key part (4)
```

- **Prefix:** 1 to 16 characters of `A-Z 0-9`, then `-`; or none. Set per issuer (PDR-0023).
  Branding only, but signed: relabelling a key makes it fail verification.
- **Alphabet** of reference and key part: 31 characters, `23456789ABCDEFGHJKMNPQRSTUVWXYZ`
  (no `0 O 1 I L`), chosen uniformly by a cryptographic random source.
- **Reference:** two groups of five. Generated at first issue, kept on reissue (PDR-0004,
  PDR-0021). With its prefix it is the displayed reference, `LIC-8F3AK-M7RXB`.
- **Key part:** new at every issue (PDR-0005).
- Reference and key part exist only in the identifier, never in the payload. Upper case only:
  lowercasing an identifier makes it unreadable.

## Payload

Compact UTF-8 JSON object, no whitespace. Fields are written in the order below; optional fields
are omitted when not set.

| Label | Field | JSON type | Rule | When absent |
|---|---|---|---|---|
| `k` | Signing key ID | string | exactly 11 characters (see Signing keys) | unreadable |
| `p` | Product ID | string | two dot-separated parts of `a-z 0-9 -`, each starting with a letter; at most 64 characters | unreadable |
| `i` | Issued | number | Unix seconds, UTC | not supported |
| `e` | Expires | number | Unix seconds, UTC | perpetual |
| `n` | Display name | string | text rule, 1 to 64 | none |
| `t` | Vendor tag | string | 1 to 64 of `A-Z a-z 0-9 - _ . # /` | none |
| `f` | Features | object | name to value; at most 50; written only when not empty | none |

**Dates.** A JSON integer token matching `0|[1-9][0-9]{0,11}`, at most `253402300799`
(9999-12-31T23:59:59Z). No string, sign, fraction, exponent or leading zero. Whole seconds only.

**Features.** The name is the property name: 1 to 64 characters of `a-z 0-9 -`, starting with a
letter; each name once. The type is the JSON token type:

| Type | Token | Example | Rule |
|---|---|---|---|
| Switch | `true` | `"ecommerce":true` | Only `true`; an off switch is simply absent |
| Number | number | `"max-orders":2500`, `"discount-rate":0.15` | `0` or `[1-9][0-9]*`, optional `.` and 1 to 4 digits, at most 15 digits in total; no sign or exponent; written without trailing fractional zeros (`2.50` is written `2.5`) |
| Text | string | `"licensed-domains":"example.com,shop.example.com"` | Text rule, 1 to 256 |

`false`, `null`, arrays and nested objects are not feature values.

**Text rule** (display name, text features; PDR-0042). Length counted in Unicode scalar values.
Characters are judged by **Unicode 16.0.0**, the same at issue and at read on every machine and
runtime: the library carries its own table, generated from the Unicode Character Database
(`extracted/DerivedGeneralCategory.txt` and `DerivedCoreProperties.txt`), and does not use the
runtime's Unicode data (ADR-0018). Outside ASCII a character is allowed when its general category
is L, Mn, Mc, Nd or P and it is not Default_Ignorable_Code_Point. A later Unicode version is a
format change; between versions these categories have so far only grown, so a move is expected
to widen the rule. A move that would remove a character is a narrowing, decided separately
(PDR-0042).

| Characters | Unicode general category | Examples | Rule |
|---|---|---|---|
| Printable ASCII, U+0020 to U+007E | any | `a Z 0 . , - + = < > ~ \| $ ^ @ :` and space | allowed |
| Letters, any script | L (Lu, Ll, Lt, Lm, Lo) | `é ü ß ł α ж コ 中` | allowed |
| Combining marks | Mn, Mc | the accent in `e` + U+0301; Hindi and Thai vowel signs | allowed, except the ignorable-by-default characters below (variation selectors U+FE00 to U+FE0F and U+E0100 to U+E01EF among them) |
| Decimal digits | Nd | `٣ ३` | allowed |
| Punctuation | P (Pc, Pd, Ps, Pe, Pi, Pf, Po) | `– — « » ¿ ¡ ・ 、` | allowed |
| Symbols outside ASCII | S (Sm, Sc, Sk, So) | `⭐ ❤ ™ © ® € ± →`, emoji, skin-tone modifiers | refused |
| Enclosing marks | Me | the keycap U+20E3 in `1️⃣` | refused |
| Other numbers | Nl, No | `① ² ½ Ⅻ` | refused |
| Spaces other than U+0020 | Zs, Zl, Zp | no-break U+00A0, ideographic U+3000, en and em spaces, U+2028, U+2029 | refused; U+2028 and U+2029 are reported as control characters |
| Ignorable by default | Unicode property Default_Ignorable_Code_Point, in any category | Hangul fillers U+115F, U+1160, U+3164, U+FFA0; combining grapheme joiner U+034F; Mongolian variation selectors U+180B to U+180D and U+180F; Khmer U+17B4, U+17B5 | refused, even where the category above allows it |
| Invisible formatting | Cf | zero-width space U+200B, joiners U+200C and U+200D, direction overrides U+202A to U+202E and U+2066 to U+2069, soft hyphen U+00AD | refused |
| Control characters | Cc: U+0000 to U+001F, U+007F to U+009F | tab, line feed | refused |
| Unassigned, private use, surrogates | Cn, Co, Cs | | refused; an unpaired surrogate is refused |

No leading or trailing whitespace. Repeated spaces inside are allowed. Text is signed exactly as
supplied, never cleaned or normalised: an accented letter typed as one character or as a letter
and a combining mark is accepted either way, as two different strings. A rule that is unsure
refuses; allowing more later breaks no issued key.

Releases up to 0.1.0-beta.6 refused only control characters, U+2028, U+2029, leading or
trailing whitespace and unpaired surrogates. A key issued by them with a character now refused
reads as *not supported* and is reissued (PDR-0024).

**String encoding.** Written as raw UTF-8; only `"` (`\"`), `\` (`\\`) and U+0000 to U+001F
(`\uXXXX`) are escaped. A reader accepts any valid JSON escape.

**No version field.** A field the reader does not know makes a verified key *not supported*
(PDR-0018). A future field is therefore a format change that older products report as *not
supported*, first action "update the product".

## Signature

- ECDSA on curve P-256 (secp256r1, OID `1.2.840.10045.3.1.7`) with SHA-256.
- Encoding: IEEE P1363, `r‖s`, 64 bytes, base64url to 86 characters. Not DER.
- Signing input: the ASCII bytes of `identifier.payload` exactly as they appear in the key string
  (the base64url text, not the decoded JSON). Editing the identifier, the prefix or one payload
  character breaks verification.

## Signing keys

- **Private key:** PKCS#8 PEM (`-----BEGIN PRIVATE KEY-----`), unencrypted. Stays with the
  vendor.
- **Public key text:** one line, `<signing key ID>.<base64url SPKI DER>`, for example
  `Nb_sm4Fxh5c.MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...`. The SPKI is the 91-byte uncompressed
  P-256 SubjectPublicKeyInfo; other curves and encodings are refused on import.
- **Signing key ID:** the first 8 bytes of SHA-256 over the SPKI DER, base64url: 11 characters.
  On import the ID is recomputed and must equal the one in the text.

## Reading a key

The order of checks decides the state; the first that fails wins.

| Step | Check | Fails as |
|---|---|---|
| 1 | Remove every whitespace character (`char.IsWhiteSpace`, including U+00A0, U+2028, U+0085); nothing left | missing |
| 2 | Text before the first `.` matches the identifier pattern | unreadable, nothing claimed |
| 3 | At most 32,767 characters; exactly three non-empty segments; canonical base64url; signature 64 bytes | unreadable, identifier claimed |
| 4 | Payload is valid UTF-8, a single JSON object with one string `p` and one string `k` (lenient: other fields ignored) | unreadable, identifier claimed |
| 5 | `p` equals the product's own ID | wrong product |
| 6 | `k` is in the product's trusted set | signing key not recognised |
| 7 | Signature verifies | not verified |
| 8 | Strict read: every field known, none repeated, every value within the rules above | not supported |
| 9 | Current UTC second is after `e` | expired |
| | Otherwise | valid |

Before step 7 everything read from the key is a claim: anyone can write it. Only from step 8 on
are values facts.

## Size

A typical key is 250 to 500 characters (PDR-0022); the worked example is 307. Every field at its limit with ASCII text gives about
22,200 characters. Non-ASCII text grows the payload (up to 4 bytes per character before base64url);
a request whose key would exceed 32,767 characters is refused at issue, naming `features`
(PDR-0025).
