# PDR-0020: A key without a valid identifier is unreadable

- Status: Amended by PDR-0023
- Date: 2026-10-09
- Source: `add-license-core` design.md Q15
- Personas: implementor (a typo in the identifier gets "paste again"), site owner (a valid key
  always reports its reference).
- Amends: PDR-0002, PDR-0004

## Context

The read rules took the identifier only when the text before the first `.` matched its format,
and otherwise read on with no identifier. Nothing said what such a key's state was. A key with
only its identifier damaged would end as *not verified*; a key a non-library tool signed without
an identifier would end as *valid* with no reference, breaking PDR-0002's facts for a valid key.

## Decision

A valid key identifier is part of a key string's shape. When the text before the first `.` does
not exactly match the identifier format, the key is *unreadable* with no claimed identifier and
no claimed product, and no further check runs.

```
  LIC-8F3AK-M7RXB-7Q2O.<intact payload>.<intact signature>   -> unreadable, no identifier
                     ^ O is not in the alphabet
```

## Reasons

- One rule; matches the existing "No identifier" scenario.
- "Paste the key again" is the right first action for a damaged identifier.
- The identifier format is fixed for good, so it never needs the *not supported* path
  (PDR-0018).

## Rejected options

| Option | Why rejected |
|---|---|
| Read on without one: damaged → *not verified*, verified without one → *not supported* | A branch for a case only a non-library tool produces |

## Consequences

A trusted tool other than this library that signs a key without an identifier gets *unreadable*
("paste again") rather than *not supported* ("ask the vendor"). Accepted: the library's issuer
always writes one.
