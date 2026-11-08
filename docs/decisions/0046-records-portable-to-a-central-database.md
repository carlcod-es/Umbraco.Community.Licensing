# PDR-0046: Records can move to a central database

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q44. Future work in `docs/feature-requests.md` FR-24,
  FR-25, FR-26.
- Personas: vendor (records are not trapped in the first version's store; a later move to a
  central database for shop integration loses nothing). Site owner (primary): reissues and
  renewals stay possible after a move, with no paid-for days lost to two copies disagreeing.
  Implementor, backoffice editor, site visitor: unaffected; keys do not change.

## Context

A later release is planned (Carl, 2026-11-08) in which the vendor's own shop issues and renews
licenses automatically when a customer completes a purchase, reading and writing the tool's
records through an API library. The records then live in one central database, the single
source of truth, which the vendor tool also works against. This change keeps every record in
the installation's folder (PDR-0031).

## Decision

**In this change:** every record the tool keeps (products, feature definitions, licenses, issue
entries, customer fields, signing keys with their encrypted private keys, and the installation's
settings) SHALL be complete enough to move to another database with nothing lost and nothing
depending on the folder or machine it came from.

**Direction for the later releases** (to be settled in those changes):
- The move from an installation to the central database is **one-way**. Afterwards the local
  installation no longer issues, reissues or changes keys, and says where its records went.
- Before the move starts, the tool warns the vendor that it is one-way and that the local
  installation will stop issuing, and asks for confirmation.
- The central database is the only place licenses are issued from.

## Reasons

- With two places able to issue, one license can be reissued in both: one copy holds a renewal
  the other lacks, and the site owner can receive a key that loses paid-for days.
- Records complete and free of local paths now mean the move needs no data the tool never kept.

## Rejected options

| Option | Why rejected |
|---|---|
| Two-way, both the installation and the central database issue | Two sources of truth; reissues can disagree |
| Decide nothing now | The first version could keep records in a form the move cannot carry |

## Consequences

- The later shop integration reopens PDR-0030 (no store integration; one person), PDR-0031
  (one folder; zip backups), PDR-0032 (passphrase typed for every signing), PDR-0033 (who can
  read customer data) and PDR-0039 (guided mode only). Each carries a "to revisit: FR-26" note.
- The hardest open question for that phase: how private keys are unlocked to sign with nobody
  present (FR-26).
