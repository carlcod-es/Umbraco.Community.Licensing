# PDR-0023: Vendor-chosen key identifier prefix

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q20 (`docs/feature-requests.md` FR-1)
- Personas: vendor (branded keys). Implementor: loses one `LIC-` pattern across vendors when
  scanning settings or logs; minor, as the claimed product is still reported. Site owner:
  unaffected; a key from another product still reports *wrong product*.
- Amends: PDR-0004, PDR-0020

## Context

PDR-0004 fixed the key identifier as `LIC-` + reference + key part. Vendors want keys that carry
their own name, or no prefix at all. Nothing is released, so no issued key depends on `LIC-`.

## Decision

**Issuing setup.** A vendor MAY set a prefix once, when setting up issuing. It applies to every
key issued with that setup, first issues and reissues alike.

| Setting | Key identifier | Reference |
|---|---|---|
| not stated | `LIC-8F3AK-M7RXB-7Q2D` | `LIC-8F3AK-M7RXB` |
| `ACME` | `ACME-8F3AK-M7RXB-7Q2D` | `ACME-8F3AK-M7RXB` |
| none | `8F3AK-M7RXB-7Q2D` | `8F3AK-M7RXB` |

**Prefix rule.** 1 to 16 characters of uppercase `A`-`Z` and `0`-`9`. No hyphen, dot, space or
other character. A prefix breaking the rule is the vendor's configuration error and raises an
error at setup, before any key is issued.

| Prefix | |
|---|---|
| `LIC`, `ACME`, `ACMECOMMERCE2026` (16) | accepted |
| `acme`, `ACME_SHOP`, `ACME-SHOP`, `ACMECOMMERCE20261` (17), empty | error at setup |

**Evaluation.** Evaluation is set up as before, with no prefix. A key identifier is read from its
end: the last three groups are reference and key part (5, 5, 4 characters of the reference
alphabet); in front of them is nothing, or a prefix fitting the rule followed by `-`. Anything
else is *unreadable* with no claims (PDR-0020). The prefix is inside the signed text, so editing
it gives *not verified*.

```
  ACME-8F3AK-M7RXB-7Q2D   prefix ACME, reference 8F3AK-M7RXB, key part 7Q2D
  8F3AK-M7RXB-7Q2D        no prefix
  LIC-8F3AK-M7RXB-7Q2O    unreadable: O is not in the reference alphabet
  acme-8F3AK-M7RXB-7Q2D   unreadable: lowercase prefix
```

**Reissue.** A request built from a verified license keeps its reference (PDR-0021) and takes the
prefix of the issuing setup it is issued with. A vendor who changes prefix reissues with the
new one; the reference part is unchanged:

```
  prod     ACME-8F3AK-M7RXB-H8RC   reissued after the vendor moved from LIC to ACME
  staging  LIC-8F3AK-M7RXB-7Q2D    same reference 8F3AK-M7RXB: same license, staging is behind
```

**Reference typed into a request.** Accepted ignoring case, hyphens and spaces, when what remains
is either 10 characters, or the configured prefix followed by 10 characters. Anything else is
rejected naming `reference`. With no prefix configured, only the 10-character form is accepted.
With prefix `ACME`:

| Typed | Result |
|---|---|
| `ACME-8F3AK-M7RXB`, `acme 8f3ak m7rxb`, `8F3AK-M7RXB` | `ACME-8F3AK-M7RXB` |
| `ACME-8F3AK-M7RXB-7Q2D` | rejected: 18 characters, expected 10 or 14 |
| `LIC-8F3AK-M7RXB` | rejected: `LIC` is not the configured prefix |

**Branding only.** The prefix grants and protects nothing. A license is protected by the vendor's
private signing key and the trusted set shipped in the product. The vendor documentation states
this.

## Reasons

- Vendors get keys that name them, at no cost to evaluation setup.
- The prefix needs no check at evaluation: the signature already prevents relabelling.

  | Attempt by another party (BETA) | Result |
  |---|---|
  | Edits an `ACME-` key to `BETA-` | *not verified*: the prefix is signed |
  | Issues `BETA-` keys for ACME's product | Has no ACME private key: *signing key not recognised* |
  | Obtains ACME's private key | Can sign `ACME-` keys too; a compromise, handled by withdrawal and reissue (PDR-0015) |
  | Modifies ACME's product to trust BETA's key | Controls the product; a prefix check would be changed with it |
  | Issues `BETA-` keys for BETA's own product | Different product ID: *wrong product* |

- Reading a typed reference allows only the configured prefix, because any-prefix reading is not
  exact: `ACME-8F3AK-M7RXB-7Q2D` without separators could be read as prefix `ACME8F3A` and
  reference `KM7RXB7Q2D`, silently naming another license.
- The default `LIC` keeps every existing example and behaviour unchanged.

## Rejected options

| Option | Why rejected |
|---|---|
| Fixed `LIC-` (PDR-0004 as was) | No vendor branding |
| Evaluation checks the configured prefix | Adds nothing the signature does not prove; another product's key reads *unreadable* ("paste again") instead of *wrong product*; installed keys stop working when a vendor changes prefix |
| Prefix checked after verification, with its own state | Same rebrand breakage; one more setting to keep in step on two sides |
| Reissue keeps the old key's prefix | The vendor manages its prefix; a changed prefix would never reach reissued keys |
| Typed reference accepted with any prefix | Not exact; a pasted key identifier is read as another reference |
| Lowercase or symbols in the prefix | The identifier is exact and signed; lowercase would undo "a lowercased key is unreadable", and `-`, `.` and whitespace are separators or removed on read |

## Consequences

- Keys from different vendors no longer share one recognisable shape.
- After a prefix change, environments are compared by the reference part, not the whole
  identifier.
- The vendor documentation states that the prefix is branding only and grants no protection;
  the private signing key and the product's trusted set protect a license.
