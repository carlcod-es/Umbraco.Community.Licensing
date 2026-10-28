# PDR-0024: Beta release with no stability promise

- Status: Decided
- Date: 2026-10-11
- Source: `add-license-core` design.md Q22
- Personas: vendor (early access, risk stated plainly). Site owner protected: guidance keeps beta
  keys away from paying customers, so no paid key breaks on a library update.

## Context

The first published release comes before the format and API have been used by any vendor. A
release must say what a vendor can rely on: once keys are in customers' hands, a format change
breaks them.

## Decision

- The first releases are betas: version 0.x, marked as prerelease.
- Before 1.0, any release may change the key format, the public API, or both. A key issued under
  a beta may read as *not supported* or *unreadable* under a later version and need reissuing.
- The vendor documentation states, in bold: do not issue beta keys to paying customers; reissue
  every key when moving to 1.0.
- From 1.0, the key format is permanent.

## Reasons

- Real use may show format or API changes are needed; a beta keeps them cheap.
- A plain warning is honest and keeps the cost on vendors who choose to ship early, not on
  site owners.

## Rejected options

| Option | Why rejected |
|---|---|
| Key format stable during the beta, API free | Commits to the format before any vendor has used it |
| Format and API both stable | "Beta" would then signal nothing |

## Consequences

The README carries the warning (`add-license-core` tasks.md 8.2). Moving to 1.0 is a release
decision that ends format changes; any change after it is a new format that older readers report
as *not supported* (ADR-0002).
