# PDR-0072: The sample demo site is served at a secure local address

- Status: Decided
- Date: 2026-11-16
- Source: `add-sample-package` design.md Q25. Amends PDR-0070. Raised by Carl's run of task
  10.1: the backoffice login was refused at the plain local address.
- Personas: vendor and implementor (the demo site can be logged into after one documented step).
  Site owner (primary): unaffected; the sample is never published. Backoffice editor: can sign
  in to the demo backoffice. Site visitor: the demo pages are reached as before, through the
  secure address.

## Context

PDR-0070 promised one command from a clone to a logged-in backoffice, with no setup step. The
demo site was started and documented at a plain (http) local address, and the backoffice
refused to sign anyone in there, so the "First run" scenario failed when Carl ran it
(2026-11-15).

## Decision

- **The demo site is served at a secure (https) local address**, and the sample documentation
  gives that address for the site and the backoffice.
- **One documented setup step**: trusting the local development certificate once. The sample
  documentation names it as the only step and gives its command.
- **The plain address still answers** and forwards to the same path at the secure address.
- Everything else in PDR-0070 stands: the fixed demo administrator, documented as demo-only.

## Reasons

- Signing in to the backoffice needs a secure address; the demo exists to show the backoffice
  screens.
- A trusted certificate means no browser warning on every visit; it is a one-time step many
  developers have already done.
- Forwarding the plain address keeps old links and habits working.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep the plain address and let the backoffice sign in over it | Carl chose a secure address, matching how a real site runs |
| No certificate step; accept the browser warning | A warning on first visit looks broken to someone evaluating the product |
| Serve only the secure address | A plain link or habit would fail with no hint where to go |

## Consequences

- PDR-0070's "no setup step" becomes "one documented setup step".
- The sample README, the start command's output and every documented demo address change to
  the secure address.
- Section 10 of the change is run again from a fresh clone.
