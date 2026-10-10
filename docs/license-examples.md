# License examples

Schema and worked examples. An example that contradicts a PDR is a documentation bug.

Last checked against: PDR-0001 to PDR-0023, ADR-0001 to ADR-0005 (2026-10-09, change
`add-license-core`, explore review, architect pass 2 and prefix explore, design.md Q13 to Q20). Key strings are added by `add-license-core` tasks.md
section 7 and verified by the example tests (ADR-0005).

> **Example signing key `Nb_sm4Fxh5c` is public** (ADR-0005). It exists so these strings can be
> checked. Never trust it in a product.

## Schema

```
  LICENSE KEY
  prefix         issuing setup     LIC by default, own, or none        PDR-0023
  product        required          vendor.product                      PDR-0004
  reference      required          generated at first issue, kept      PDR-0004
  key part       set by the core   new at every issue                  PDR-0005
  issued         set by the core   UTC, to the second                  PDR-0006
  expires        stated            UTC date and time, or perpetual     PDR-0007
  display name   optional          shown only                          PDR-0008
  vendor tag     optional          the vendor's label                  PDR-0009
  features       0..N              switch | number | text              PDR-0010 to PDR-0012
```

Limits (PDR-0017): product ID 64 characters, feature name 64, 50 features per key, key string
32,767 characters. Optional feature definitions at issue (PDR-0016).

Never in a key: customer name, email, company or any personal data (PDR-0014).

Payload (ADR-0002):

```json
{"signingKeyId":"Nb_sm4Fxh5c","product":"acme.commerce","issued":"2026-09-01T08:05:12Z",
 "expires":"2027-03-01T23:59:59Z","displayName":"Commerce Pro","vendorTag":"CUST-0042",
 "features":{"ecommerce":true,"max-orders":2500,"licensed-domains":"example.com,shop.example.com"}}
```

Key string (ADR-0001): `<key identifier>.<base64url payload>.<base64url signature>`.

Evaluation states (PDR-0002, PDR-0018): missing, unreadable, wrong product, signing key not
recognised, not verified, not supported, expired, valid. Licensed only when valid. A key whose
text before the first `.` is not a valid identifier is unreadable (PDR-0020).

Feature lookups (PDR-0019): switch, number, text; each answers only for a feature of its own
type on a valid key. Evaluation may run on every request (PDR-0022).

## Examples

Now is 2026-10-01T08:00:00Z unless stated.

**1. Minimal.** `acme.seo-toolkit`, `LIC-4HN7T-QW2ZC`, perpetual, no features. Licensed; every
feature lookup answers not granted.

**2. Typical.** `acme.commerce`, `LIC-8F3AK-M7RXB`, expiry stated as the date `2027-03-01`,
display name `Commerce Standard`, tag `CUST-0042`, `ecommerce`, `max-orders: 500`. Expiry signed
as `2027-03-01T23:59:59Z`: valid through that second, expired at `2027-03-02T00:00:00Z`.

**3. Expiry with a time.** Expiry stated as `2026-10-31T12:00:00Z`: valid at `12:00:00`, expired
at `12:00:01`.

**4. Renewal installed late.**
```
  key 1  LIC-8F3AK-M7RXB-7Q2D  issued 2025-10-15  expires 2026-10-15T23:59:59Z
  key 2  LIC-8F3AK-M7RXB-K9PX  issued 2026-10-01  expires 2027-10-15T23:59:59Z
```
Key 2 is sent on 2026-10-01 but installed on 2026-10-20. From 2026-10-16 to the install the
site reports *expired*, naming `LIC-8F3AK-M7RXB-7Q2D`. After the install: valid.

**5. Add-on by reissue; staging missed.**
```
  key 1  LIC-8F3AK-M7RXB-7Q2D  2026-03-01  ecommerce, max-orders 500
  key 2  LIC-8F3AK-M7RXB-K9PX  2026-06-15  ecommerce, max-orders 1500
  key 3  LIC-8F3AK-M7RXB-H8RC  2026-09-01  ecommerce, max-orders 1500, ai-assist
```
Production holds key 3: valid, `ecommerce, max-orders 1500, ai-assist`. Staging holds key 1:
valid, `ecommerce, max-orders 500`. The implementor compares identifiers (`...-7Q2D` against
`...-H8RC`). A refund is a reissue with the feature removed.

**6. Domains as text.** `licensed-domains: "example.com,shop.example.com"`. The library reports
the text; the product splits and matches. Adding `blog.example.com` is a reissue.

**7. Expiry typo.** Issued with `2099-03-01` by mistake, reissued an hour later with
`2027-03-01`. Whichever key is installed counts; the 2099 key stays valid until 2099 wherever it
is installed (no revocation).

**8. Two licenses bought by mistake.** `LIC-8F3AK-M7RXB` and `LIC-C2D8R-XE6GU`, each
`max-orders 500`. Only the installed key counts. Vendor guidance: reissue one with
`max-orders 1000` and refund the other.

**9. Same key in two settings sources.** The host's settings precedence reads one; the library
sees one key.

**10. Adding to an expired license.** Prefilled expiry `2026-09-01T23:59:59Z` is rejected naming
`expires`; the vendor sets a new expiry. Perpetual licenses are unaffected. The reissue request
keeps product `acme.commerce` and reference `LIC-8F3AK-M7RXB`; neither can be changed on it
(PDR-0021).

**11. Failed keys.** Each environment evaluating `acme.commerce`:
```
  prod     valid                       LIC-8F3AK-M7RXB-7Q2D, expires 2027-03-01T23:59:59Z, ecommerce, max-orders 500
  staging  not verified                claims to be LIC-8F3AK-M7RXB-K9PX for acme.commerce   (paste lost a character)
  dev      wrong product               claims to be LIC-5E3FT-BPZ7Y-2MHS for zenith.commerce-shipping
  test     unreadable                  claims to be LIC-2V5C9-HKD3P-X3BN                     (cut off by email)
  uat      signing key not recognised  claims to be LIC-8F3AK-M7RXB-H8RC for acme.commerce   (new signing key, old product)
  demo     not verified                claims to be LIC-8F3AK-M7RXB-P6TY for acme.commerce   (edited: expires 2099, max-orders 999999)
  new      missing                     (setting empty: "" — PDR-0003)
  copy     unreadable                  claims to be LIC-8F3AK-M7RXB-7Q2D                     (pasted 40,000 characters of a log with it)
  typo     unreadable                  no claims                                             (identifier hand-edited to ...-7Q2O)
  old      not supported               claims to be LIC-8F3AK-M7RXB-W4TN for acme.commerce   (key from a newer vendor tool, product not updated)
```
Only prod is licensed. The edited key's claims appear nowhere. *Not supported* means the key is
intact and signed by a trusted key; the first action is to update the product, not to paste
again. A product shipped with no trusted keys fails when evaluation is set up, before any key is
read (PDR-0018). A key wrapped across lines or with
a trailing newline is still valid.

**12. Trial.** No trial kind. A short expiry, optionally a `trial` switch the product checks.
Conversion is a reissue under the same reference.

**13. Separate add-on package.** `zenith.commerce-shipping` is its own product with its own key.

**14. Rejected at issue** (at 2026-10-01T08:00:00Z; no key produced; one error lists every
problem):

| Input | Field | Reason |
|---|---|---|
| no expiry stated | `expires` | Expiry must be stated |
| `expires 2026-09-30` | `expires` | Ends 2026-09-30T23:59:59Z: in the past |
| `expires 2026-10-01T07:59:59Z` | `expires` | In the past |
| `expires 2027-03-01T12:00:00.5Z` | `expires` | Fraction of a second |
| `product commerce`, `product Acme.Commerce`, 65 characters | `product` | Product ID rule |
| reference with `O`, or wrong length | `reference` | Reference rule |
| display name `" Pro"`, with a line break, or 65 characters | `displayName` | Display name rule |
| vendor tag `INV 2026 04`, `jane@acme.com`, `""`, 65 characters | `vendorTag` | Vendor tag rule |
| `pro: false` | `features.pro` | Absence means not granted |
| `max-orders: -200`, `storage-gb: 2.12345` | `features.<name>` | Number rule |
| `max-orders` twice, `Max Orders: 500`, a 65-character name | `features` | Name rule |
| 51 features | `features` | At most 50 |
| `max-orders: "500"` (text) with `max-orders` defined as a number | `features.max-orders` | Feature definitions (PDR-0016) |
| `max-order: 500` with definitions that do not name `max-order` | `features.max-order` | Feature definitions (PDR-0016) |
| `licensed-domain: ""`, `" example.com"`, a line break, 257 characters | `features.licensed-domain` | Text rule |
| reference `LIC-8F3AK-M7RXB` or `ACME-8F3AK-M7RXB-7Q2D` with prefix `ACME` configured | `reference` | Configured prefix + 10 characters, or 10 characters (PDR-0023) |

Rejected at issuing setup, before any request (PDR-0023): prefix `acme`, `ACME_SHOP`,
`ACME-SHOP`, empty, or `ACMECOMMERCE20261` (17 characters).

Allowed: `expires 2026-10-01` (date alone, today); `expires 2026-10-01T08:00:00Z`;
`licensed-domains: "a.com,A.com"`; `discount-rate: 0.15`. Not expressible in the API, so not
rejectable: supplying `issued` or a key part; stating two expiries. Not detected (PDR-0014):
a customer name in a display name or text feature — the vendor documentation forbids it.

**15. Cached result.** A product evaluates at 10:00 a key expiring `2027-03-01T12:00:00Z` and keeps
the *valid* result. At `12:00:01` the kept result still reads valid; a new evaluation reads
*expired*. Evaluating on every request avoids this (PDR-0022).

**16. Routine rotation.** Perpetual `LIC-4HN7T-QW2ZC-...` was signed by key A. The vendor trusts
key B alongside A and issues with B from then on, never withdrawing A. The perpetual key stays
valid. A is withdrawn only if its private key is compromised, after which every key it signed
reads *signing key not recognised* until reissued (PDR-0015).

**17. Vendor prefix.** Acme sets prefix `ACME` at issuing setup; Zenith states no prefix.
```
  ACME-8F3AK-M7RXB-7Q2D   acme.commerce        reference ACME-8F3AK-M7RXB
  8F3AK-M7RXB-7Q2D        zenith.commerce-...  reference 8F3AK-M7RXB
```
Products evaluate either without being told a prefix. An `ACME-` key pasted into Zenith's
product reads *wrong product*. A copy of an Acme key relabelled `BETA-8F3AK-M7RXB-7Q2D` reads
*not verified*: the prefix is signed. A reissue of `LIC-8F3AK-M7RXB-7Q2D` after Acme moves to
`ACME` starts `ACME-8F3AK-M7RXB-`; the reference part is unchanged. **The prefix is branding
only:** the private signing key and the product's trusted set protect a license (PDR-0023).

## Key strings

Added by `add-license-core` tasks.md section 7 (example public key `Nb_sm4Fxh5c`).
