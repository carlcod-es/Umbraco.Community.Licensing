# ADR-0022: Vendor tool release marks, installation switch and pasted keys

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q66, Q67, Q68, Q70. Amends ADR-0013, ADR-0021.

## Context

PDR-0051 holds whether a key is in a release per product, not per key: in shared mode one key
signs for several products, each with its own release. PDR-0052 forgets the held passphrase when
the session moves to a restored installation, counts menu choices as activity for the idle lock,
and lets every license question take a whole key string, accepted only when signed by one of the
installation's keys, expired or not. ADR-0013 stores the release mark as `SigningKeys.InReleaseUtc`;
ADR-0021 reads the idle time once when the session opens. The library must not change
(ADR-0019).

## Decision

- **Release marks.** A table `KeyReleases` (`SigningKeyId` + `ProductId` as key, `ConfirmedUtc`)
  replaces `SigningKeys.InReleaseUtc`. Release confirmed inserts a row for every listed key and
  the chosen product that lacks one. The products a key signs for are its `ProductId` in
  per-product mode and every row of `Products` in shared mode. A key is in a release for a
  product when the row exists; the switch warns, and Status shows, for every product it signs
  for without a row. A new key has no rows. Amends ADR-0013.
- **Moving installation.** When the session continues with a restored installation, it zeroes and
  drops the held passphrase in `SessionLock` and reads the idle time from the new installation's
  `Settings`. A new installation made by setup hands the lock the passphrase just set, with the
  idle time just chosen. Amends ADR-0021 "Idle lock" (idle time read when the session opens or
  moves installation).
- **Idle activity.** Unchanged in mechanism: ADR-0021 restarts the timer on every completed line
  or hidden-prompt answer, and a menu choice is a line read the same way. Tests now cover it.
- **Pasted key.** A license question first reads the answer as a reference under the prefix rule
  (PDR-0023). Otherwise it evaluates the answer as a key string with the library's
  `LicenseEvaluator`, no library change:
  - against one product of the installation, trusting every public key the installation holds
    for it (active, standby and replaced for any reason: a lookup set, not the release list);
  - when the state is *WrongProduct* and `ClaimedProduct` is a product of the installation, once
    more against that product.
  - *Valid* or *Expired*: `License.Reference` chooses the record; no record refuses as "no
    record". *SigningKeyNotRecognised*, or a claimed product the installation does not hold:
    "not issued by this installation". *Unreadable*, *NotVerified*, *NotSupported*: "not a
    reference or a key". Nothing from the key string is stored or logged.

## Alternatives Considered

| Option | Why rejected |
|---|---|
| Product list as JSON on `SigningKeys` | Not queryable by product; less plain for the later move to another database (PDR-0046) |
| Keep `InReleaseUtc` for per-product mode, add the table for shared mode | Two code paths for one rule |
| Keep the held passphrase after a restore, recheck it against the new passphrase check | Contradicts PDR-0052; holds a secret of another installation |
| Library API that reads a key against a set of keys without a product | A library change ADR-0019 avoids; two evaluations cost nothing at a terminal |
| Read the reference from the key identifier without verifying | PDR-0052 accepts only keys signed by the installation |

## Consequences

- Status and the switch join `KeyReleases` with the products a key signs for; in shared mode a
  product added later has no rows and is named until its release is confirmed.
- The migration in section 4 creates `KeyReleases` from the start; no data migration is needed,
  since no installation exists yet.
- A pasted key costs at most two signature checks.

## Reversal Cost

Low: release marks are one table read and written in two places; the license question is one
helper used by every action that asks for a license; the installation switch is one call on
`SessionLock`.
