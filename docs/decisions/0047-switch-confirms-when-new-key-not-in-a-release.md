# PDR-0047: A switch confirms whenever the new active key is not in a release

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q45. Amends PDR-0035.
- Personas: site owner (primary: newly issued keys are not broken by a switch without the vendor
  seeing it first). Vendor: one extra question, only when a switch would break keys on existing
  sites. Implementor, backoffice editor, site visitor: unaffected.

## Context

PDR-0035 warns and asks for confirmation only when a standby key not yet in a release becomes
active. With no standby key (standby keys declined at setup, or a signing key never given one),
a switch makes a new key pair active. No release can trust that key yet, so every key issued
after the switch is reported as signed by a key not recognised on sites until they update the
product, and the vendor was told nothing.

## Decision

- When the key becoming active is not yet in a release (a standby key not yet in a release, or a
  new key pair because there is no standby key), the switch warns that sites on releases that do
  not trust it will report keys signed by it as signed by a key not recognised, and asks for
  confirmation before switching.
- This applies whatever the reason: routine, loss or leak.
- Declining changes nothing.
- The confirmation is a question in the menu session, answered like any other (PDR-0048).

## Reasons

- The case with no standby key always breaks newly issued keys on existing sites; it needs the
  warning more than the standby case does.
- After a leak or loss the break is unavoidable, but the vendor should still see the consequence
  before acting.

## Rejected options

| Option | Why rejected |
|---|---|
| Warn only, no confirmation | The switch cannot be undone; a warning scrolled past protects no site owner |
| Skip the confirmation for a leak | The consequence for site owners is the same; the vendor still decides when to switch |

## Consequences

- A vendor without standby keys answers one question on every switch.
- The tool guide explains why the question appears and that a standby key in a release avoids it.
