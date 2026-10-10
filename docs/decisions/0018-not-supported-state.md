# PDR-0018: Not supported state; empty trusted set at setup

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q13, Q19
- Personas: implementor (a first action that fixes the problem), site owner (an intact key is not
  called damaged). Vendor: one more state to handle in the product, with the same first action
  as *signing key not recognised*.
- Amends: PDR-0002

## Context

PDR-0002 made a verified key whose contents break a read rule *unreadable*, first action "paste
the key again". Two causes reach that point, and neither is fixed by pasting: a faulty vendor
tool, and a key issued with a newer library version that adds a field this product version
refuses. The second happens on every format change, on every site running an older product
version. The key is intact and signed by a trusted key; the library knows this because the
contents are read only after verification.

Separately, PDR-0002 checks the product ID when evaluation is set up but not the trusted set. An
empty set can only be a vendor bug, and it shows up as every key on every site reporting
*signing key not recognised*.

## Decision

**Not supported.** A new state between *not verified* and *expired*:

```
  4  signature does not verify             -> not verified
  ---- verified from here ----
  5  contents cannot be read               -> not supported
  6  past its expiry                       -> expired
     otherwise                             -> valid
```

A key that verifies but whose contents break any read rule (unknown field, repeated field or
feature name, wrong format, unsupported value, over a limit) is *not supported*. It reports the
claimed product and claimed key identifier only, like the other failed states. Not licensed.

| Reason | First action | Who |
|---|---|---|
| Not supported | Update the product; then ask the vendor | Implementor, vendor |

*Unreadable* now means only that the string could not be read before verification: its first
action, "paste the key again", is right for every remaining cause.

**Empty trusted set.** Setting up evaluation with an empty trusted set is the product's own
configuration error and raises an error at setup, as an invalid product ID does (design.md Q10).
An empty set remains valid as a value while a set is built.

## Reasons

- The library knows the key is intact; reporting it as damaged sends the implementor round a
  paste loop that cannot succeed.
- The name describes what was observed (this product cannot read the key), not a presumed cause
  (newer format or faulty tool), following PDR-0002.
- A configuration error surfaces at the vendor's own startup, never as a per-request state on
  customer sites (design.md Q10 reasoning).

## Rejected options

| Option | Why rejected |
|---|---|
| Keep *unreadable*, first action "paste again; then update the product; then ask the vendor" | Every cut-off paste also suggests updating the product; noise on the most common failure |
| Keep as is | Fails the implementor on every future format change (ADR-0002: a new field is refused by older readers) |
| Allow an empty trusted set | The bug is found only as every key failing on customer sites |

## Consequences

Hosts render one more state. A vendor shipping an empty trusted set ships a product that fails at
startup rather than one that reports every key as unrecognised.
