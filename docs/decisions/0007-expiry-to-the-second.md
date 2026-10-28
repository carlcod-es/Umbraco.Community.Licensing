# PDR-0007: Expiry is a stated UTC date and time to the second

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R2, Q8 (`CLEAN-PROJECT-PROMPT.md` section 12, decision 8)
- Personas: vendor (no accidental lifetime keys; expiry at a chosen time), site owner (clear
  expiry), implementor.

## Decision

- Every request states an expiry: a date, a date and time, or perpetual. Not stated is rejected,
  so a forgotten expiry never issues a lifetime key.
- Precision is the second, in UTC. A date alone means 23:59:59 UTC that day. A date and time in
  any offset is stored as the same instant in UTC. A fraction of a second is rejected, not
  rounded.
- At issue, an expiry earlier than the current second is rejected. A date alone of today is
  accepted.
- A key is valid through its stated second and expired once the clock is past it:
  `2027-03-01T12:00:00Z` is valid at `12:00:00.900`, expired at `12:00:01`.
- One expiry per license. An add-on bought mid-term ends with the license. Adding to an expired
  license needs a new expiry.
- The library takes a date or date-time only. Terms, months and period arithmetic are vendor
  tooling.

## Reasons

- A stated expiry removes the most expensive vendor mistake: a perpetual key by omission.
- Seconds let a vendor end a trial or term at a chosen time; a date alone covers the common case.
- Rejecting a fraction keeps the rule that the library signs what was asked, never a cleaned
  value (accepted by Carl, 2026-10-09, `add-license-core` design.md Q8).

## Rejected options

| Option | Why rejected |
|---|---|
| Date only | Cannot end a key at a set time |
| Expiry optional, absent = perpetual | A forgotten field issues a lifetime key |
| Round fractional seconds | Silently changes a stated value |

## Consequences

Documentation: a date alone ends at 23:59:59 UTC that day, which is earlier or later in local
time. A key issued in error stays valid until its expiry wherever it is installed.
