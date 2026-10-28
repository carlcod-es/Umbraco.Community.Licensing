# PDR-0004: Product ID, license reference and key identifier

- Status: Amended by PDR-0020, PDR-0023
- Date: 2026-10-09
- Source: `add-license-core` design.md R2
- Personas: implementor (search settings for the identifier a result names), site owner
  (search emails for it), vendor (stable product IDs, one reference per license).

## Decision

**Product ID.** Two dot-separated parts of lowercase `a-z`, digits and hyphens, each starting
with a letter, at most 64 characters (PDR-0017): `acme.commerce`. Guidance: the vendor part is something the vendor already owns
uniquely (domain, package-registry owner). It never changes once keys are issued.

**Reference.** 10 random characters from uppercase letters and digits without `0 O 1 I L`
(31 characters), shown `LIC-XXXXX-XXXXX` (`LIC-` is display only). Generated at first issue; on
reissue the vendor passes it in, accepted ignoring case, hyphens, spaces and the `LIC` prefix
(`lic-8f3ak m7rxb` → `LIC-8F3AK-M7RXB`); anything else is rejected.

**Key identifier.** Reference + 4-character key part (PDR-0005): `LIC-8F3AK-M7RXB-7Q2D`, at the
start of the key string as displayed.

## Reasons

- The reference names one license across its reissues. It is not secret and not proof of
  ownership. Uniqueness comes from randomness (about 8 × 10¹⁴ combinations); the library keeps
  no records to check it.
- The alphabet drops characters people confuse when reading a key aloud or from a screenshot.
- The identifier grants and reveals nothing; it lets people find the key a result names.

## Rejected options

| Option | Why rejected |
|---|---|
| Sequential reference | Needs records; the library keeps none |
| Free-form product names | Case and look-alike letters make two names for one product |

## Consequences

A product ID cannot be renamed after keys are issued; a renamed product is a new product.
