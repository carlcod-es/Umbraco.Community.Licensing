# PDR-0015: Signing keys, trusted set and rotation

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R6, Q18
- Personas: vendor (keep one private key per product safe; rotate it), implementor (an outdated
  product names the cause), site owner (a perpetual key keeps working through routine rotation).

## Decision

- Creating a key pair returns a private key, a public key and a signing key ID, each exportable
  separately. The ID is derived from the public key, never chosen, so it cannot be paired with
  the wrong key. The exported public key carries its ID, so a product can trust it with no other
  input. The private key stays with the vendor and is never shipped.
- A product holds a trusted set of public keys addressed by ID. Adding a different key under an
  ID already held raises an error (configuration, not key content) and leaves the set unchanged.
- Rotation: trust the new key alongside the old; switch issuing to the new key. Routine rotation
  never withdraws the old key: its public key stays trusted, which costs nothing while its
  private key is safe (design.md Q18). Withdrawing an ID is for a compromised private key only,
  followed by reissuing every key it signed. Licenses signed by a withdrawn key report *signing
  key not recognised*. Adding a key never affects licenses signed with an already-trusted key.
- An empty trusted set is refused when evaluation is set up (PDR-0018).

## Reasons

Rotation without a flag day: old and new keys verify side by side. A derived ID removes the
mistake of labelling a key with the wrong ID. Keeping old keys trusted protects perpetual
licenses: without records the vendor cannot know which keys an old signing key still backs, and
a perpetual key depends on it for good.

## Rejected options

| Option | Why rejected |
|---|---|
| Vendor-chosen key IDs | Can be paired with the wrong key |
| One trusted key per product | Rotation would invalidate every key at once |
| Withdraw the old ID once no valid licenses depend on it | The vendor cannot check it without records; perpetual keys always depend on it, so their owners are stranded by housekeeping |

## Consequences

Private key compromise: withdrawing its ID also invalidates every legitimate key it signed
until reissued. Documented as an inherent limit.
