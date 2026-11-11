# PDR-0053: Unexpected failure in a session

- Status: Decided
- Date: 2026-11-09
- Source: `add-vendor-tool` design.md Q72. Amends PDR-0048.
- Personas: vendor (a failure is named in plain words, nothing is half-stored, and the session
  carries on without typing the passphrase again). Site owner (primary): unaffected; a failed
  action issues and records nothing. Implementor, backoffice editor, site visitor: unaffected.

## Context

The section 3 review of `add-vendor-tool` found that an action failing for a reason the tool does
not check for (a full disk, a file that cannot be written, a database error) ended the tool with
technical output and no defined result. PDR-0048 says what happens after a completed or a
cancelled action, not after a failed one.

## Decision

- **Failed action.** An action that fails unexpectedly stores nothing. The tool says in one line
  which action failed and why, in the error's own words, and shows the menu the action was
  chosen from. The session continues and the held passphrase stays held, under the idle lock.
  Setup and restore at the startup offer behave the same way: the offer is shown again.
- **Failure outside an action.** The session ends: one line saying why, the passphrase
  forgotten, and the failure exit already used for end of input.
- **No technical detail.** No error type or stack trace is shown or written anywhere.
- Exit codes are unchanged.

## Reasons

- Each action writes in one step at the end, so a failure leaves the records as they were and
  returning to the menu is safe.
- Ending the session would make the vendor type the passphrase again and gain nothing, since
  nothing was stored.
- The vendor cannot act on a stack trace, and writing one to a file would put data outside the
  installation folder (PDR-0031).

## Rejected options

| Option | Why rejected |
|---|---|
| End the session on every failure | The passphrase must be typed again and nothing is gained, since nothing was stored |
| A new exit code for a failure | No script runs the tool (PDR-0048), so no one reads it |
| Offer to show the technical detail | No persona can use it |

## Consequences

- The guide describes what a failure looks like and that nothing was stored.
- A failure the vendor cannot fix (for example a damaged database) is reported each time the
  action is tried.
