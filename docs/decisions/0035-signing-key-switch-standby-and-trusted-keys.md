# PDR-0035: Signing key switch, standby keys and trusted keys

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q11, Q16 (`docs/feature-requests.md` FR-2 key
  replacement, FR-4 issues 1, 3, 4). Builds on PDR-0015.
- Personas: implementor (a release trusts the right keys); site owner (keys keep working after a
  rotation; a leak does not strand them); vendor (the tool works out the list).

## Context

Each product release ships the public keys it trusts (PDR-0015). Which keys belong in the list
depends on why each old key was replaced: routine and lost keys stay trusted, a leaked key is
withdrawn once its licenses are reissued. Working this out by hand during a leak invites
mistakes.

## Decision

- **Record.** Every issue entry records its signing key ID.
- **Standby keys.** Offered at setup as recommended (FR-4). Can be added later to a signing key
  that has none. Marked "not yet in a release" until the vendor confirms a release trusts it.
  The vendor lists products with their signing key and standby status, or only products without
  a standby key.
- **Switch.** The vendor switches a signing key giving a reason: routine, loss or leak. The
  standby becomes active and a new standby is created; with no standby, a new key pair becomes
  active. The old key is kept, signs nothing more. In shared mode the switch applies to every
  product. Switching to a standby not yet in a release warns that sites on older releases will
  report *signing key not recognised*, and asks for confirmation.
- **Trusted keys.** Per product, the tool prints the public key lines its next release must
  trust, each with its role: active, standby, every replaced key except one replaced after a
  leak whose reissues the vendor has confirmed sent. Until that confirmation the leaked key stays
  listed, marked leaked (FR-2 leak steps 2 and 5). The vendor confirms when a release trusts the
  printed list.
- **Export by signing key.** One row per issue signed by a chosen key: reference, key identifier,
  product, issue time, expiry. No key strings. Superseded and expired rows included.

## Reasons

- The reason given at the switch is the only input the tool needs to build every later list.
- Keeping the leaked key listed until reissues are sent gives the interim release of FR-2 step 2
  without manual editing at the worst moment.
- The "not yet in a release" mark answers FR-4 issue 4: the warning comes before a switch that
  would break sites.

## Rejected options

| Option | Why rejected |
|---|---|
| List every key not withdrawn; leak interim by guidance | The vendor rebuilds the step-2 list by hand during a leak |
| Standby at setup only | A vendor who declined has no route |
| Remove a leaked key at the switch | Reissued keys must work on sites before the leaked key is withdrawn |

## Consequences

- Bulk reissue after a leak and contacting site owners stay manual (FR-5): the vendor resends
  each license on the export.
- A product released without confirming `released` keeps its standby marked "not yet in a
  release"; the warning may then be a false alarm.
