# PDR-0064: Switch features are chosen together

- Status: Decided
- Date: 2026-11-12
- Source: `add-vendor-tool-rich-console` design.md Q2. Amends PDR-0044, PDR-0048.
- Personas: vendor (sees every switch a product has at once and ticks what was bought). Site
  owner (primary): the key carries exactly the ticked switches, as it carried the switches typed
  `on`. Implementor, backoffice editor, site visitor: unaffected.

## Context

PDR-0048 asks each feature in turn; PDR-0044 reads a switch typed `off` as not granted. A product
with several switches means several on/off questions in a row.

## Decision

- At issue and reissue, the product's switch features are one list where several can be ticked:
  Space ticks or unticks, Enter accepts, none ticked is allowed, Esc cancels the action.
- Ticked is granted. Unticked is not: at issue it is left out of the key; at reissue it is
  removed (PDR-0044 unchanged in effect).
- At reissue the switches the license grants start ticked.
- Number and text features are still asked one by one, as PDR-0048 says.
- A product with no switch shows no list.
- Each feature is still given once per action.

## Reasons

- One view of every switch is quicker and shows what is not granted as clearly as what is.
- Starting ticked at reissue keeps "empty answer keeps the current value".

## Rejected options

| Option | Why rejected |
|---|---|
| Keep one on/off question per switch | Slower; Carl asked for multi-select (2026-11-12) |
| Number and text features in the same list | They need a value, not a tick |

## Consequences

- The guide shows the switch list at issue and reissue.
