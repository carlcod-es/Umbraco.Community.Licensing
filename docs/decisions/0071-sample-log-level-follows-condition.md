# PDR-0071: The sample's log level follows the license condition

- Status: Decided
- Date: 2026-11-13
- Source: `add-sample-package` design.md Q24. Amends PDR-0068.
- Personas: implementor and site owner (primary: a healthy start reads as Information, a lost
  license as an Error they can filter and alert on, an approaching expiry as a Warning while
  there is still time to renew). Vendor: a log pattern worth copying. Backoffice editor, site
  visitor: unaffected.

## Context

PDR-0068 logged every license state change as a warning, including the first evaluation after
start. A healthy site therefore wrote a warning on every start, and an expired or missing key
was logged at the same level as a working one. An approaching expiry was not logged at all,
because the state stays `Valid`.

## Decision

- The sample logs the license **condition**: the state and, for a `Valid` license, whether it is
  expiring soon (30 days or fewer, as PDR-0068).
- One message when the condition differs from the last one logged, including the first
  evaluation after the site starts; none on repeats.
- Level by condition:
  - `Valid`, not expiring soon: **Information**.
  - `Valid`, expiring soon: **Warning**, naming the expiry date.
  - Any other state (`Expired`, `Missing`, `Unreadable`, `WrongProduct`,
    `SigningKeyNotRecognised`, `NotVerified`, `NotSupported`): **Error**.
- The same levels at startup and for every later change, including a running site's license
  crossing into expiring soon.
- Unchanged from PDR-0068: the message names the state and the key identifier (or claimed
  identifier, or none), never the key string.

## Reasons

- A warning on every healthy start teaches people to ignore warnings.
- For the implementor, a product running unlicensed is an error, whichever state caused it; the
  dashboard gives the first action.
- An approaching expiry needs action but nothing is broken yet, which is what a warning means.
- Logging the crossing into expiring soon reaches implementors who watch logs but not the
  dashboard.

## Rejected options

| Option | Why rejected |
|---|---|
| Every change a warning (PDR-0068) | A healthy start logs a warning; a lost license is no louder than a good one (Carl, 2026-11-13) |
| Only `Expired` an error, other bad states warnings | They are equally unlicensed (Carl, 2026-11-13) |
| New levels for the startup message only | A license lost while running is the case that most needs an error (Carl, 2026-11-13) |
| Expiring soon reported only at startup and on the dashboard | A long-running site would never log it (Carl, 2026-11-13) |

## Consequences

- The sample's log rule is its own; the library logs nothing.
- A vendor copying the sample gets levels an implementor can alert on without extra rules.
