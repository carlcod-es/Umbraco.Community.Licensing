# PDR-0034: Reissue and renewal from records

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q9, Q25. Builds on PDR-0013, PDR-0021.
- Personas: site owner (primary: renewing early loses no days; add-ons keep the expiry). Vendor:
  one keypress for a usual renewal.

## Context

PDR-0013 reissues from the key a site owner presents, which may be outdated. The tool reissues
from its own records instead. Product and reference are kept (PDR-0021). The vendor sets the new
expiry.

## Decision

- Reissue from the record changes any of expiry, display name, vendor tag, features; product and
  reference never change.
- The vendor types the expiry. The tool suggests:

  | Reissue | Suggestion |
  |---|---|
  | Renewal of a time-limited license | Current expiry plus the length of the last term |
  | Renewal of a perpetual license | Perpetual |
  | Any other reissue (add-on, feature change) | Current expiry |

- The last term runs from the last issue's issue time to its expiry. It is counted in calendar
  months when it was a whole number of months (same day of month and time of day, a day past a
  month's end counting as its last day), and in exact time otherwise (design.md Q25, Architect
  assumption).

  ```
  issued 2025-11-30T23:59:59Z, expires 2026-11-30T23:59:59Z   renewed 2026-11-20  ->  2027-11-30T23:59:59Z
  issued 2026-01-15T10:00:00Z, expires 2026-07-15T10:00:00Z   renewed             ->  2027-01-15T10:00:00Z
  issued 2026-03-10T09:00:00Z, expires 2026-04-09T09:00:00Z   renewed (30 days)   ->  2026-05-09T09:00:00Z
  ```

- Scripts take the suggestion only when they ask for it.

## Reasons

- Typing the expiry needs no new setting; the suggestion makes the usual case one keypress.
- Extending from the current expiry means a site owner who renews early loses nothing.
- Counting months keeps a monthly or yearly term on the same day each time; exact time alone
  turns a six-month term into 181 days.

## Rejected options

| Option | Why rejected |
|---|---|
| Extend by a period set per product | A setting to define and keep for each product |
| Extend from today | Penalises renewing early |
| Extend from the later of today or the current expiry | Two rules to explain; the vendor can type a date after a lapse |
| No suggestion | Date arithmetic by hand on every renewal |

## Consequences

- After a lapse the suggestion is still current expiry plus term, which may be in the past; the
  library rejects a past expiry (PDR-0013), and the vendor types a later date.
