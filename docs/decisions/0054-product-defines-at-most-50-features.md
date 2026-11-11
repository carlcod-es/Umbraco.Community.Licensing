# PDR-0054: A product defines at most 50 features

- Status: Decided
- Date: 2026-11-09
- Source: `add-vendor-tool` design.md Q73. Amends PDR-0036.
- Personas: vendor (told at once, while defining the product, rather than when a customer is
  waiting for a key). Site owner (primary): unaffected; every key still carries every feature
  it was sold with. Implementor, backoffice editor, site visitor: unaffected.

## Context

A license carries at most 50 features (PDR-0017). Feature definitions are add-only and had no
limit (PDR-0036), so a product could define a 51st feature that no license could then carry in
full. The section 7 review of `add-vendor-tool` raised it.

## Decision

- A product defines at most 50 features, the most a license carries.
- Products > Add a feature refuses a product that already defines 50 features. The refusal comes
  when the product is chosen, before the feature name is asked, names the limit of 50, stores
  nothing and shows the Products menu.

## Reasons

- A definition no license can carry is a trap: the vendor would find out only at issue.
- Refusing at the product saves typing a feature name that cannot be stored.

## Rejected options

| Option | Why rejected |
|---|---|
| Allow any number, refuse at issue only | The vendor discovers the problem in front of a customer, and definitions cannot be removed (PDR-0036) |
| Refuse after the feature name is typed | The answer is wasted; the limit does not depend on the name |

## Consequences

- The guide states the limit beside Add a feature.
- Issue and reissue still check the library's limit on the features set; with this limit it
  cannot be exceeded by defined features.
