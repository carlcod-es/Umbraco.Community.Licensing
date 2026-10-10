# PDR-0019: Typed feature lookups only

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q14
- Personas: site owner and vendor (one answer to "is this feature on?"). Implementor unaffected.
- Amends: PDR-0010

## Context

The feature lookup requirement held a switch lookup, a number lookup, a text lookup and a
presence query that answered for a feature of any type. PDR-0010 (design.md Q9) says a lookup of
a different type than the feature holds answers not granted. The presence query contradicted it
for switches: a key with `pro: "false"` as text answered not granted to a switch lookup and
granted to the presence query.

## Decision

A product looks up a feature by name and type: switch, number or text. A switch lookup is
granted only when the feature holds a switch. There is no lookup that answers for a feature of
any type. To list everything a key holds, a licensing screen uses the features reported for a
valid or expired key (PDR-0002).

## Reasons

- One answer per question: the product cannot pick a call that grants what another call refuses.
- No persona needs "present, of any type" as a gate; the site owner's "what am I licensed for" is
  served by the reported features.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep the presence query beside the typed lookups | Gating with it grants `pro: "false"` |
| Presence query only, used as the switch lookup | Reverses design.md Q9 for switches |

## Consequences

A product that needs to know whether a name is present in any type reads the reported features.
