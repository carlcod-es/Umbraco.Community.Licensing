# PDR-0034: Reissue and renewal from records

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q9, Q25, Q35. Builds on PDR-0013, PDR-0021.
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

- The last term is the length the last renewal granted: from the later of the expiry before that
  renewal and the renewal's issue time, to the expiry the renewal set. A license never renewed
  has the term of its first issue: from its issue time to the expiry it set. Other reissues
  (add-on, feature change, resend) do not change the term. The term is measured on UTC calendar
  dates, ignoring time of day: in calendar months when start and end share a day of month (a day
  past a month's end counting as its last day), otherwise in whole days; the suggestion keeps
  the current expiry's time of day. A term starting and ending on the same date gives no
  extension: the current expiry is suggested (design.md Q25, Q35; Carl, 2026-10-28,
  2026-10-31).

  ```
  first issue 2025-11-30T23:59:59Z expiring 2026-11-30T23:59:59Z
    renewed 2026-11-20 (early)               term 12 months (first issue)   -> 2027-11-30T23:59:59Z
    renewed again                            term 2026-11-30 .. 2027-11-30  -> 2028-11-30T23:59:59Z
  first issue 2026-01-15T10:00:00Z expiring 2027-01-15T10:00:00Z
    add-on 2026-09-01, then renewed          term 12 months (first issue)   -> 2028-01-15T10:00:00Z
  first issue 2026-01-15T10:00:00Z expiring 2026-07-15T10:00:00Z
    renewed                                  term 6 months, not 181 days    -> 2027-01-15T10:00:00Z
  expired 2026-11-30T23:59:59Z; renewed 2027-02-01T09:00:00Z, vendor typed 2028-02-01T09:00:00Z
    renewed again                            term 2027-02-01 .. 2028-02-01  -> 2029-02-01T09:00:00Z
  first issue 2026-03-10T09:00:00Z expiring 2026-04-09T09:00:00Z
    renewed                                  term 30 days                   -> 2026-05-09T09:00:00Z
  first issue 2025-11-30T14:23:11Z expiring 2026-11-30 (23:59:59Z)
    renewed                                  term 12 months, dates only     -> 2027-11-30T23:59:59Z
  ```

- Scripts take the suggestion only when they ask for it.

## Reasons

- Typing the expiry needs no new setting; the suggestion makes the usual case one keypress.
- Extending from the current expiry means a site owner who renews early loses nothing.
- Counting months keeps a monthly or yearly term on the same day each time; exact time alone
  turns a six-month term into 181 days.
- Measuring on dates keeps a date expiry on its day. The first issue's time of day is when the
  vendor ran the command, so comparing times would count most first terms in exact time and move
  the expiry by hours at each renewal.
- Measuring the term from the last renewal keeps it the same after an early renewal, a lapse,
  or reissues in between; a goodwill extension by a later reissue does not lengthen the next
  renewal.

## Rejected options

| Option | Why rejected |
|---|---|
| Extend by a period set per product | A setting to define and keep for each product |
| Extend from today | Penalises renewing early |
| Extend from the later of today or the current expiry | Two rules to explain; the vendor can type a date after a lapse |
| No suggestion | Date arithmetic by hand on every renewal |
| Term from the last issue entry's issue time to its expiry | Any reissue changes it: a yearly license renewed ten days early next suggests 12 months and 10 days; an add-on in month 8 makes the next renewal about 4 months |
| Months only when the time of day also matches, exact time otherwise | A date-expiry license's first term never matches: issued 14:23:11Z expiring 23:59:59Z, it renews to 2027-12-01T09:36:47Z and drifts further each year |
| Dates only when the expiry is 23:59:59Z | Two rules; an expiry typed as an instant still drifts |
| Whole months when within one day of a whole number | An extra rule for a one-time, one-day difference |

## Consequences

- After a lapse the suggestion is still current expiry plus term, which may be in the past; the
  library rejects a past expiry (PDR-0013), and the vendor types a later date. The next term is
  then measured from that renewal's issue time.
- The record marks which reissues were renewals.
- Dates are UTC: a vendor east of UTC issuing in the morning may have the previous UTC date as
  the issue date. The first renewal then suggests a day past whole months and the vendor retypes
  it; later terms run expiry to expiry.
