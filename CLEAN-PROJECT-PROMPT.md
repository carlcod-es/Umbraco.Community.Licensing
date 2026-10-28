# Clean-project prompt: offline license key library for .NET

Extracted 2026-10-08 from this repository before the reset. The old records (archived change
`2026-10-04-license-key-management`, the `one-key-per-product` change, PDRs, ADRs, examples)
were deleted in commit `a0b218a` and survive only in git history before it. Updated 2026-10-09
with the decisions of an explore session (section 12): one key per product with no combining,
no text set, expiry with a time. Where the old records and this prompt disagree, this prompt
wins.

Everything below the line is the prompt.

---

## 1. What to build

A .NET 10 library that lets a software vendor issue license keys for its products and lets the
product check them **entirely offline**. Three capabilities:

| Capability | Side | Does |
|---|---|---|
| License generation | Vendor (issuer) | Checks a license's contents and signs them into a pasteable key string. Keeps no records |
| License validation | Product (consumer), at runtime | Evaluates one key string for one product: its state and, when verified, its contents |
| Signing-key management | Both | Creates signing key pairs with derived IDs; holds a trusted set of public keys; supports rotation |

Context: the vendors are Umbraco package authors, but the library has **no dependency on
Umbraco or on any host**. It takes a key string (or none), a product ID, a set of trusted
public keys and a clock, and returns a result. Where keys are stored and how results are shown
is the host's concern (section 9).

## 2. Personas

Every requirement serves at least one persona and harms none. When interests conflict:
**site owner first, then implementor, then vendor.**

```
  VENDOR --issues key--> SITE OWNER --hands key--> IMPLEMENTOR --installs--> SITE
  (builds product)       (buys, pays)              (builds site, configures)    |
                                                                                v
                                              BACKOFFICE EDITOR, SITE VISITOR
                                              (use the product; never manage keys)
```

| Persona | Relationship to the license | Needs |
|---|---|---|
| Vendor | Issues keys, builds products, gates features | Keys survive email; one private key per product, never shipped; features usable as flags and limits (`max-orders`); decides what happens when a license fails |
| Site owner (primary) | Buys licenses from any channel (vendor store, marketplace, reseller) | What am I licensed for, what is about to expire, what is missing. Often not the person installing the key |
| Implementor | Installs keys, may look after many sites and environments | Which configured key is failing, why, where to fix it. Keys deployed through configuration, environment variables, vault secrets |
| Backoffice editor | Uses the product | May see warnings; must never read a full key (keys are bearer tokens) |
| Site visitor | Uses the public site | Never sees licensing. A licensing problem never crashes a page: the library reports, it never throws on a bad key |

## 3. Working rules for this project

- Record each decision with its reasons and rejected options. Product behaviour goes in a PDR
  (`docs/decisions/`), technology in an ADR (`docs/adrs/`). Test: if the stack changed, a PDR
  would still hold. The reasons are source material for the library's user documentation.
- Settle one open question at a time, with a recommendation. Lead with the simplest option
  that meets the stated criteria; give a worked example when the comparison is abstract.
- Keep a worked-examples document (`docs/license-examples.md`) in step with the decisions.
  An example that contradicts a decision is a documentation bug.
- Code: .NET 10, C# latest (file-scoped namespaces, primary constructors, records), xUnit,
  BCL only. Cross-platform: Windows, macOS, Linux. Node.js for any scripts; do not assume
  Python. Do not commit; the Product Owner commits.

## 4. Fixed technology decisions

Decided in the old project's signing ADR. Do not reopen them without a new reason.

| Decision | Choice | Why |
|---|---|---|
| Signature | ECDSA P-256, SHA-256, 64-byte IEEE P1363 (`r‖s`), `System.Security.Cryptography.ECDsa` | BCL, short signatures. RSA: 256-byte signatures for no gain. Ed25519: needs a third-party library on some platforms |
| Envelope | Custom fixed-algorithm string, not JWT | No `alg` header to downgrade (`alg: none`); no dependency |
| Payload | UTF-8 JSON, no whitespace, `System.Text.Json`, read strictly | Readable when decoded; strict read means a future field is refused, not half-read |
| Clock | `TimeProvider` injected into issuer and evaluator | Expiry tests run against a fixed time |
| Packaging | One BCL-only package for all three capabilities | Nothing to split until host-side features return |

### 4.1 Key string

```
LIC-8F3AK-M7RXB-7Q2D.eyJzaWduaW5nS2V5SWQiOi...fX0.1AqpE9feDTqwkvVIdd4mRlzJEl...bULr_Q
|__________________| |_________________________| |_________________________|
 key identifier       base64url(payload)          base64url(signature), 86 chars
|_______________________________________________|
 signing input: ASCII bytes of everything before the last "."
```

- Three segments split on `.`; base64url per RFC 4648 section 5, no padding.
- The signing input includes the visible identifier, so editing it (even lowercasing) fails
  verification. Reference and key part live only in the identifier, not the payload.

### 4.2 Payload

```json
{"signingKeyId":"0F8NSYaNR_Q","product":"acme.commerce","issued":"2026-09-01T08:05:12Z",
 "expires":"2027-03-01T23:59:59Z","displayName":"Commerce Pro","vendorTag":"CUST-0042",
 "features":{"ecommerce":true,"max-orders":2500,"licensed-domains":"example.com,shop.example.com"}}
```

| Field | Encoding | Absent |
|---|---|---|
| `signingKeyId` | string, 11 characters (4.3) | unreadable |
| `product` | string, product ID rule (5.1) | unreadable |
| `issued` | exactly `yyyy-MM-ddTHH:mm:ssZ`, UTC | unreadable |
| `expires` | exactly `yyyy-MM-ddTHH:mm:ssZ`, UTC | perpetual. `null` unreadable |
| `displayName` | string, display name rule (5.4) | none. `null`, `""` unreadable |
| `vendorTag` | string, vendor tag rule (5.5) | none. `null`, `""` unreadable |
| `features` | object, name to value | none; `{}` also means none |

Feature type is the JSON token type:

| JSON | Feature type |
|---|---|
| `true` | switch |
| number | number |
| string | text |
| `false`, `null`, object, array | unreadable |

`displayName` is confirmed (2026-10-09). The payload field names and the `expires` format still
need recording in an ADR (section 10).

**Numbers.** Grammar on issue and read: `0` or `[1-9][0-9]*`, optional `.` and 1 to 4 digits,
at most 15 digits total; no sign, exponent or leading zero. Issuer takes a `decimal`, strips
trailing fractional zeros, writes invariant culture (`2.50` → `2.5`). Reader checks the raw
token against the grammar, then reads `decimal`; never `double`.

**Strict read.** A verified key is *unreadable* if it has: an unknown field; a repeated field
or feature name (System.Text.Json does not detect these; the reader must); a date-time not in
its exact format; any value breaking a rule checked at issue. Unknown feature *names* are fine
(ignored at lookup). No version field: unknown fields being unreadable already guards format
changes.

**Vendor tag on read.** Check the decoded string against `^[A-Za-z0-9_.#/-]{1,64}\z` (ordinal;
`\z`, not `$`, which matches before a trailing `\n`). Escaped JSON forms of allowed characters
are accepted. The default System.Text.Json encoder escapes none of the allowed characters on
write.

### 4.3 Signing key ID

First 8 bytes of SHA-256 over the public key's SubjectPublicKeyInfo DER, base64url: 11
characters. Derived, never chosen: key-pair creation returns it, the trusted set computes it,
the issuer derives it from the private key. It cannot be paired with the wrong key.

### 4.4 Reading a supplied string

1. Remove all whitespace.
2. **Identifier**: the text before the first `.` (or the whole string), only if it fully
   matches `^LIC-[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{5}-[2-9A-HJKMNP-Z]{4}$`. Otherwise no
   identifier. Later steps do not change it.
3. **Segments**: exactly three non-empty base64url segments; signature decodes to 64 bytes.
   Else unreadable.
4. **Routing claims**: payload is a JSON object with string `product` and `signingKeyId`. Else
   unreadable.
5. **Checks** in order: wrong product, signing key not recognised, not verified.
6. **Schema**: a verified key is checked against every issue rule; failure is unreadable.

| Supplied | Identifier | Outcome |
|---|---|---|
| `LIC-8F3AK-M7RXB-7Q2D.eyJzaWdu` (cut off) | `LIC-8F3AK-M7RXB-7Q2D` | unreadable |
| `LIC-8F3AK-M7R` | none | unreadable |
| `lic-8f3ak-m7rxb-7q2d.eyJ...` (lowercased) | none | unreadable |
| `Hunter2!` | none | unreadable |

Whether an empty or whitespace-only string is *missing* or *unreadable* is open (section 10).

## 5. License contents and issue rules

A key is **one license for one product**: its identity, expiry and **one feature set**. A site
holds **one key per product** (section 6). There is no purchase list and no base / add-on kind.
Selling an add-on, more capacity, an upgrade, a renewal or a refund means reissuing the license
under the same reference with the contents edited; the implementor installs the new key in
place of the old one.

```
  LICENSE KEY
  product        required          vendor.product
  reference      required          generated at first issue, kept on every reissue
  key part       set by the core   new at every issue
  issued         set by the core   UTC, to the second
  expires        stated            UTC date and time to the second, or perpetual
  display name   optional          shown only
  vendor tag     optional          the vendor's label, e.g. a customer ID
  features       0..N              switch | number | text
```

Never in a key: customer name, email, company or any personal data. This applies to the
display name and the vendor tag too.

### 5.1 Product ID

Two dot-separated parts of lowercase `a-z`, digits and hyphens, each starting with a letter:
`acme.commerce`. Guidance: the vendor part is something the vendor already owns uniquely
(domain, package-registry owner). It never changes once keys are issued.

### 5.2 License reference and key identifier

| Item | Rule |
|---|---|
| Alphabet | Uppercase letters and digits without `0 O 1 I L` (31 characters) |
| Reference | 10 random characters, shown `LIC-XXXXX-XXXXX` (`LIC-` is display only). Generated by the core at first issue; on reissue the caller passes it in, accepted ignoring case, hyphens and spaces (`lic-8f3ak m7rxb` → `LIC-8F3AK-M7RXB`); an invalid one is rejected |
| Key part | 4 random characters, generated by the core at every issue, never a caller input |
| Key identifier | Reference + key part, `LIC-8F3AK-M7RXB-7Q2D`, at the start of the key string as displayed |

The reference names one license. It is **not secret and not proof of ownership**. Uniqueness
comes from randomness (about 8 × 10¹⁴ combinations); the core keeps no records to check it.
The identifier grants and reveals nothing; it lets the implementor search their settings and
the site owner their emails for the key a result names.

**Why a random key part** (confirmed 2026-10-09). It tells reissues of one license apart in
the identifier, the only part people can see and search: staging on `...-7Q2D`, production on
`...-H8RC` shows staging is behind. Two reissues of one license matching by chance: about 1 in
923,000, harmless because their issue times differ. Rejected:

| Option | Why rejected |
|---|---|
| Sequence (`-0001`, `-0002`) from the presented key | No records; reissuing from an old key repeats a number and gives a false "newer" signal |
| Derived from the issue time | Longer; implies an order people may trust over `issued` |
| None (reference only) | Reissues indistinguishable by identifier |

### 5.3 Issue time and expiry

- `issued`: set by the core from the clock at signing, UTC, truncated to the second. Never a
  caller input. Informational: shows when a key was issued and tells reissues apart by time.
  It plays no part in evaluation.
- `expires`: every request **states** an expiry: a UTC date and time, or perpetual. Neither or
  both is rejected, so a forgotten expiry can never issue a lifetime key.
- Precision is the second. A date alone is accepted and means the end of that day in UTC
  (`2027-03-01` → `2027-03-01T23:59:59Z`).
- At issue, an expiry earlier than the current second is rejected. A date alone of today is
  accepted (its end of day is still ahead).
- A key is valid **through** its stated second and expired once the clock is past it:
  `2027-03-01T12:00:00Z` is valid at `12:00:00`, expired at `12:00:01`.
- One expiry per license. An add-on bought mid-term ends with the license. Adding features to
  an expired license requires the reissue to set a new expiry (a renewal plus the add-on).
- The library takes a date or date-time only. Terms, months and period arithmetic are vendor
  tooling.

### 5.4 Display name (optional)

Confirmed 2026-10-09. 1 to 64 characters; no leading or trailing whitespace, line breaks or
control characters; one plain string in the vendor's language; signed exactly as supplied,
never trimmed (rejected instead). Display only: no part in evaluation or feature lookup. Lets a
generic licensing screen show `Commerce Pro` instead of raw feature names.

### 5.5 Vendor tag (optional)

1 to 64 characters from `A-Z a-z 0-9 - _ . # /`; no spaces; case kept; signed exactly as
supplied; an invalid value is rejected, never cleaned. Not unique, never compared. A label
only: no effect on evaluation or gating. Reported for verified keys only.
Guidance: hold a **stable customer or account ID**, so every reissue of a license keeps the
same tag; the reference links the license's history in the vendor's records.

| Input | Result |
|---|---|
| `#1001`, `SHOP-2026-000123`, `pi_3NkX9a2eZvKYlo2C1`, `INV/2026/04` | accepted |
| `INV 2026 04` | rejected: space |
| `jane@acme.com` | rejected: `@` (and personal data) |
| empty | rejected: omit it instead |
| 65+ characters | rejected |

### 5.6 Features

A name and a typed value. Present means granted; absent means not granted; an unknown name is
ignored. Features never affect validity. "Tier" and "feature" are one concept: whether `pro`
is a bundle is a vendor convention. The library reports values; the product enforces them.

| Part | Rule |
|---|---|
| Name | Lowercase `a-z`, digits, hyphens; starts with a letter. At most once per key. Looked up ignoring case of `a`-`z`, culture-invariant |
| Switch | A plain name. Explicit `false` is rejected |
| Number | Zero or positive; at most 4 decimal places and 15 digits. Any count, limit, amount or rate (`discount-rate: 0.15` is fine): nothing is summed |
| Text | Non-empty; at most 256 characters; no leading or trailing whitespace, line breaks or control characters. Never interpreted: `"500"` is text, and a comma means nothing to the library |

**Lists in text.** A list such as licensed domains is one text value with a vendor convention,
for example comma-separated: `licensed-domains: "example.com,shop.example.com"`. The product
splits and matches it. Adding or removing a value is a reissue. The vendor normalises values
at issue (for example lowercases domains).

**Why names are restricted** (kept 2026-10-09). Free-form names matched ignoring case fail on
look-alike letters (Latin `a`, Cyrillic `а`) and on culture-dependent case rules (Turkish i):
a product asking for `licensed-domain` could silently miss a key issued with a look-alike name,
and a paid feature would read as not granted. The old second reason, keeping names aligned
across combined licenses, no longer applies.

**Why no text set** (removed 2026-10-09). Its only gain over text was a union of values across
several licenses for one product. With one key per product there is no union, and text with a
vendor convention carries a list on one key with no extra type, rules or encoding.

### 5.7 Issuing

- Input: product ID, optional reference, stated expiry, optional display name, optional
  vendor tag, features, private signing key. Returns the key string, reference, key identifier
  and issue time.
- The signing key ID is derived from the private key. The private key is not retained,
  cached or exposed after the call. The core keeps no record of anything it issued.
- A rejected request raises one error listing **every** problem, each naming its field
  (`expires`, `vendorTag`, `features.max-orders`, `features.licensed-domains`). No key is
  produced.
- Key string: one line of printable characters with no whitespace. Typical keys are 250 to
  500 characters.

### 5.8 Reissue from the presented key

A vendor with no records adds to the key the site owner presents. The library builds a reissue
request from a **verified** license: product, reference, expiry (perpetual stated explicitly),
display name, vendor tag and features. The vendor edits any of them except the product, then
issues. Building it does nothing else: no clock, no checks. Issuing checks as usual, so a
prefilled past expiry is rejected, naming the expiry. A key that failed verification offers no
license to build from.

Why it is safe: the key is a bearer token, so its holder already holds everything it grants. A
reissue from it gives only that plus what was just bought. The reference alone is not proof.

Risk, handled by guidance only (open, section 10): a vendor reissuing from an old key
(forwarded from an old email) produces a key without features bought since; once installed it
replaces the newer key. Guidance: reissue from the key installed on the site; vendor records
close the gap.

Indicative API (refine during design):

```csharp
var current = LicenseRequest.ReissueOf(result.License!);
var next = current with { Features = current.Features!.With("max-orders", 2500m) };
issuer.Issue(next, signingKey);
```

Request fields stay nullable so a missing value is reported as a problem, not a compile error.
Read-side types (`VerifiedLicense`) are separate, with non-nullable fields.

## 6. Evaluation

Input: one product ID, **zero or one** key string, a trusted key set and a clock. Output: one
result. Never throws because of a key's content (empty, truncated, garbage, altered).

The host supplies at most one key per product. Where several settings sources hold a key for
the same product (settings file, environment variable, vault), the host's settings precedence
picks one; the library never sees a second key.

### 6.1 One state, first failing check wins

```
     no key supplied                             -> missing
  1  cannot be read                              -> unreadable
  2  claims another product                      -> wrong product
  3  signing key ID not in the trusted set       -> signing key not recognised
  4  signature does not verify                   -> not verified
  ---- verified from here: claims are facts ----
  5  past its expiry                             -> expired
     otherwise                                   -> valid
```

The product is **licensed** when the state is valid. Otherwise every feature lookup answers
not granted.

Verified but breaking a schema rule (a faulty vendor tool) is *unreadable*, with its
identifier; the vendor must reissue.

Reasons name what was observed, not a presumed cause; "tampered" is never used:

| Reason | First action | Who |
|---|---|---|
| Missing | Install the key; ask the site owner for it | Implementor, site owner |
| Unreadable | Paste the key again | Implementor |
| Wrong product | Move the key to the product it names | Implementor |
| Signing key not recognised | Update the product; then ask the vendor | Implementor, vendor |
| Not verified | Paste again; then ask the vendor | Implementor, vendor |
| Expired | Renew | Site owner |

### 6.2 What a result reports

| State | Reports |
|---|---|
| Missing | The state only |
| Failed (1 to 4) | Reason; claimed product and claimed key identifier when readable, worded as claims. **Never** issue time, expiry, display name, vendor tag or features |
| Verified (5+) | As facts: product, key identifier, issue time, expiry or perpetual, display name, vendor tag, features. An expired key's features are reported so the site owner sees what lapsed; lookups still answer not granted |

The rule: an unverified claim may identify a key; it may never describe or grant an
entitlement. No result contains any part of a key string other than its identifier.

### 6.3 Feature lookup

| Type | Lookup on a valid key |
|---|---|
| Switch | Granted when present |
| Number | The value as issued, exact (decimal) |
| Text | The value as issued, exact |

Names are looked up ignoring case of `a`-`z` (5.6). An absent or unknown name answers not
granted. Matching inside a text value (domains, wildcards, `www.` variants) is the product's
code.

### 6.4 Why one key per product (decided 2026-10-09)

Every license change is a reissue followed by a replace, so the site owner always has exactly
one current key per product to hand to the implementor. This removes superseding, duplicates,
ties, the vendor-error and conflict flags, per-type merge rules, the license count and the
additive-number restriction. A separately sold add-on is its own product with its own key
(example 13).

| Option | Why rejected |
|---|---|
| Combine several keys per product (the 2026-10-06 shape) | Most of the evaluation rules exist only for it; tied reissues summed into an over-grant; text values conflicted across licenses and answered nothing |
| Several keys, at most one license counted | Keeps superseding and duplicates only to report a mistake |

Costs, accepted:

| Cost | Who | Mitigation |
|---|---|---|
| A second license for the same product gives nothing until merged; the library cannot warn, it never sees it | Site owner | Vendor guidance: sell every change as a reissue; merge duplicate purchases into one key and refund the other. A separately sold add-on is its own product |
| The library cannot say a site holds an older key than the latest issued | Implementor | Identifiers compare across environments (example 5) |
| An older reissue, or a key issued in error, stays valid until its expiry wherever installed | Vendor | Inherent offline limit (section 9); shorter terms |
| Renewal and add-ons need the key replaced in every environment | Implementor | Documentation (section 11) |

## 7. Signing-key management

- Create a key pair: returns private key, public key and signing key ID, each exportable
  separately. The exported public key carries its ID, so a validator can trust it with no other
  input. The private key stays with the vendor and is never shipped in the product.
- Trusted set: a consumer holds several public keys addressed by ID. Adding a public key under
  an ID already held for a different key raises an error (configuration, not key content) and
  leaves the set unchanged.
- Rotation: trust the new key alongside the old; switch issuing to the new key; withdraw the
  old ID once no valid licenses depend on it. Withdrawn keys' licenses report *signing key not
  recognised*. Adding a key never affects licenses signed with an already-trusted key.

## 8. Worked examples

Now is 2026-10-01T08:00:00Z unless stated. Contents are shown in schema form, not as key
strings.

**1. Minimal.** `acme.seo-toolkit`, `LIC-4HN7T-QW2ZC`, perpetual, no features. Licensed; every
feature lookup answers no.

**2. Typical.** `acme.commerce`, `LIC-8F3AK-M7RXB`, expiry stated as the date `2027-03-01`,
display name `Commerce Standard`, tag `CUST-0042`, `ecommerce`, `max-orders: 500`. Expiry
signed as `2027-03-01T23:59:59Z`: valid through that second, expired at
`2027-03-02T00:00:00Z`.

**3. Expiry with a time.** Expiry stated as `2026-10-31T12:00:00Z`: valid at `12:00:00`,
expired at `12:00:01`.

**4. Renewal installed late.**
```
  key 1  LIC-8F3AK-M7RXB-7Q2D  issued 2025-10-15  expires 2026-10-15T23:59:59Z
  key 2  LIC-8F3AK-M7RXB-K9PX  issued 2026-10-01  expires 2027-10-15T23:59:59Z
```
Key 2 is sent on 2026-10-01 but installed on 2026-10-20. From 2026-10-16 to the install the
site reports *expired*, naming `LIC-8F3AK-M7RXB-7Q2D`; the site owner's email search for that
identifier finds the old key, and the renewal email holds key 2. After the install: valid.

**5. Add-on by reissue; staging missed.** The site owner buys an extra 1,000 orders, then AI
Assist. Each time the vendor reissues from the presented key and edits the features:
```
  key 1  LIC-8F3AK-M7RXB-7Q2D  2026-03-01  ecommerce, max-orders 500
  key 2  LIC-8F3AK-M7RXB-K9PX  2026-06-15  ecommerce, max-orders 1500
  key 3  LIC-8F3AK-M7RXB-H8RC  2026-09-01  ecommerce, max-orders 1500, ai-assist
```
Production holds key 3: valid, `ecommerce, max-orders 1500, ai-assist`. Staging still holds
key 1: valid, `ecommerce, max-orders 500`. The library cannot know key 1 was replaced; the
implementor compares identifiers (`...-7Q2D` against `...-H8RC`). A refund is a reissue with
the feature removed; it takes effect only when the new key is installed.

**6. Domains as text.** `licensed-domains: "example.com,shop.example.com"`. The library reports
the text; the product splits on commas and matches. Adding `blog.example.com` is a reissue with
`"example.com,shop.example.com,blog.example.com"`.

**7. Expiry typo.** The vendor issues with `expires 2099-03-01` by mistake, then reissues an
hour later with `2027-03-01`. Whichever key is installed counts. If the 2099 key was installed
and never replaced, it stays valid until 2099: the library cannot tell it was corrected (no
revocation, section 9).

**8. Two licenses bought by mistake.** `LIC-8F3AK-M7RXB` and `LIC-C2D8R-XE6GU`, each
`max-orders 500`. Only the installed key counts: `max-orders 500`. Vendor guidance: reissue one
license with `max-orders 1000` and refund the other.

**9. Same key in two settings sources** (settings file and environment variable). The host's
settings precedence reads one; the library sees one key.

**10. Adding to an expired license.** Prefilled expiry `2026-09-01T23:59:59Z` is rejected
naming `expires`; the vendor sets a new expiry. Perpetual licenses are unaffected.

**11. Failed keys.** Seven sites or environments, each evaluating `acme.commerce`:
```
  prod     valid                       LIC-8F3AK-M7RXB-7Q2D, expires 2027-03-01T23:59:59Z, ecommerce, max-orders 500
  staging  not verified                claims to be LIC-8F3AK-M7RXB-K9PX for acme.commerce   (paste lost a character)
  dev      wrong product               claims to be LIC-5E3FT-BPZ7Y-2MHS for zenith.commerce-shipping
  test     unreadable                  claims to be LIC-2V5C9-HKD3P-X3BN                     (cut off by email)
  uat      signing key not recognised  claims to be LIC-8F3AK-M7RXB-H8RC for acme.commerce   (new signing key, old product)
  demo     not verified                claims to be LIC-8F3AK-M7RXB-P6TY for acme.commerce   (edited: expires 2099, max-orders 999999)
  new      missing
```
Only prod is licensed. The edited key's claims appear nowhere. A key wrapped across lines by an
email client, or with a trailing newline, is still valid.

**12. Trial.** No trial kind. A short expiry, optionally a `trial` switch the product checks.
Conversion is a reissue under the same reference.

**13. Separate add-on package.** `zenith.commerce-shipping` is its own product with its own key.
A product that needs both checks both.

**14. Rejected at issue** (at 2026-10-01T08:00:00Z; no key produced):

| Input | Reason |
|---|---|
| no expiry stated; or both an expiry and perpetual | Expiry must be stated, once |
| `expires 2026-09-30` | Date alone ends 2026-09-30T23:59:59Z: in the past |
| `expires 2026-10-01T07:59:59Z` | In the past |
| `product commerce`, `product Acme.Commerce` | Product ID rule |
| reference with `O`, or wrong length | Reference rule |
| `issued` or key part supplied | Set by the core |
| display name `" Pro"`, with a line break, or 65 characters | Display name rule |
| vendor tag `INV 2026 04`, `jane@acme.com`, `""`, 65 characters | Vendor tag rule |
| `pro: false` | Absence means not granted |
| `max-orders: -200`, `"5OO"` as a number, `storage-gb: 2.12345` | Number rule |
| `max-orders` twice | Name at most once per key |
| `Max Orders: 500` | Name rule |
| `licensed-domain: ""`, `" example.com"`, text with a line break, 257 characters | Text rule |
| customer name or email anywhere | No personal data |

Allowed: `expires 2026-10-01` (date alone, today); `expires 2026-10-01T08:00:00Z`;
`licensed-domains: "a.com,A.com"` (text, never interpreted); `discount-rate: 0.15`.

## 9. Out of scope

**Inherent limits, documented, not mitigated:**
- No revocation before expiry (offline tokens). An older reissue, or a key issued in error,
  stays valid until its expiry wherever it is installed. Shorter terms plus renewal instead.
- Clock rollback defeats expiry; the server owner controls the clock.
- Private key compromise: withdrawing its ID also invalidates every legitimate key it signed
  until reissued.
- No machine, domain or install binding. A text feature such as `licensed-domains` lets the
  vendor bind by convention; keys remain bearer tokens.
- No online validation or licensing server.

**Not expressible in this shape:**
- An add-on with a term of its own (monthly add-on on a yearly license), unless sold as its
  own product. One expiry per license.
- Several licenses for one product on one site. One key per product (6.4).

**Future changes** (decided in the old project, deferred; restate under one key per product
when resumed):
- **Issuing add-on**: an optional local .NET tool for small vendors that keeps product
  definitions and issued-key records (no personal data; signing keys stored apart from data),
  issues, re-sends, renews and rotates. The core never depends on it. Its old decisions assumed
  one key per purchase (license types with a role, add-on records linked to a base, order
  reference as the vendor tag, add-on expiry aligned to base) and must be re-decided. Its
  records are what close the "reissued from an old key" gap (5.8).
- **Host-side (Umbraco)**: key sourcing from configuration, environment variables and Key
  Vault (Key Vault in a separate package so others avoid the dependency); one key per product,
  with the host's settings precedence picking between sources; one shared store per site for
  all vendors; package registration (product ID, display name, vendor, renewal link,
  required / optional); an inventory with one row per product, local-time expiry and advance
  warning; a view-only backoffice screen shipped once per site, never showing a full key;
  site labels per stored key; dependencies between products; an Umbraco (or generic host)
  version range in the key. Leaning for storage: named entries, `Licensing:Keys:<name>`, one
  name per product.
- **Dropped, not deferred**: a trial / standard kind of license; release-date gating; text set
  (2026-10-09); combining several licenses per product (2026-10-09).

## 10. Open items for the new project

Technology (Architect):
1. Confirm the payload names `displayName` and `vendorTag`, and `expires` as
   `yyyy-MM-ddTHH:mm:ssZ` (4.2). Record in the ADR with the strict-read rules.
2. Public API surface: request and result types, how expiry is stated (for example
   `LicenseExpiry.On(date)`, `LicenseExpiry.At(dateTime)`, `LicenseExpiry.Perpetual`), and
   feature lookup methods per type.
3. Package and namespace name. The library has no Umbraco dependency; decide whether
   `Umbraco.Community.Licensing` still fits.
4. Generate the worked examples' key strings from the library itself, with a throwaway key
   pair published in the ADR, and keep them as example tests.

Product (Analyst), one at a time:
1. Reissuing from an old key (5.8): guidance only today. Decide whether that is enough.
2. An empty or whitespace-only key string: *missing* or *unreadable*?

## 11. Documentation notes

Must appear in the vendor and implementor documentation:
- Text values are never interpreted. A list (for example domains) is a vendor convention such
  as comma-separated values, split by the product. Adding or removing a value is a reissue.
- One key per product. Sell every change (add-on, capacity, upgrade, renewal, refund) as a
  reissue. Merge a duplicate purchase into one key and refund the other.
- An add-on sold separately (for example through a channel that cannot reissue) is its own
  product with its own key.
- Reissue from the key installed on the site, not one from an old email.
- A renewal or add-on takes effect only when the new key is installed, in every environment.
  Compare key identifiers to find an environment running an older key.
- An expiry given as a date alone ends at 23:59:59 UTC that day. A key issued in error stays
  valid until its expiry wherever it is installed.

## 12. Decisions from the explore session 2026-10-09

Each needs a PDR (with the reasons and rejected options above):

| # | Decision | Where |
|---|---|---|
| 1 | Display name confirmed | 5.4 |
| 2 | Text set removed; text is opaque; lists are a vendor convention changed by reissue | 5.6 |
| 3 | One key per product; combining, superseding, duplicates, ties and conflict removed; *missing* state added | 6 |
| 4 | Numbers no longer additive-only (follows from 3) | 5.6 |
| 5 | Feature name rule kept | 5.6 |
| 6 | `issued` kept, informational only | 5.3 |
| 7 | Key part kept, random, generated at every issue | 5.2 |
| 8 | Expiry is a UTC date-time to the second; a date alone means 23:59:59Z; past rejected at issue; valid through the stated second | 5.3 |
