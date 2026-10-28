# PDR-0016: Feature definitions at issue

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q11
- Personas: site owner (a paid feature is not lost to a vendor typo), vendor (mistakes caught
  before a key ships). Implementor unaffected.

## Context

The issue rules check each feature on its own. They cannot know that `max-orders` should be a
number, or that `max-order` is a typo. Either mistake produces a valid key whose feature the
product reads as not granted (PDR-0010), so the site owner loses what they paid for until the
vendor reissues. The risk is highest in vendor tooling that maps loose input (forms, CSV, store
webhooks) onto features.

## Decision

A vendor may pass feature definitions with an issue request: each feature name with its type
(switch, number, text).

- With definitions: an undefined name is rejected, naming `features.<name>`; a type other than
  the defined one is rejected, naming `features.<name>`. A defined feature may be left out.
- An empty list of definitions means the product has no features: any feature is rejected.
- Without definitions: features are checked by the feature rules alone, as before.
- Definitions follow the feature name rule and name each feature once. Invalid definitions are
  the vendor's configuration error, raised before any request is checked.
- Reissues are checked the same way.

## Reasons

- Catches wrong types and misspelled names at issue, the only point where the vendor can still
  fix them without a site owner noticing.
- Optional: a vendor issuing from typed code can skip it; nothing changes for them.
- The deferred issuing add-on's product definitions can build on it instead of re-deciding it.

## Rejected options

| Option | Why rejected |
|---|---|
| Guidance only (share typed constants between tooling and product) | Nothing in the library catches the mistake |
| Defer to the issuing add-on | Protects only vendors who use the add-on, and not in this change |
| Check types only, allow undefined names | Misses typos, which fail the site owner the same way |
| Convert or coerce values to the defined type | Interprets values; contradicts PDR-0012 |
