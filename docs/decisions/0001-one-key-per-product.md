# PDR-0001: One key per product

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R4
- Personas: site owner (always one current key per product to hand over), implementor (one key
  to install per product per environment). Vendor bears the cost of selling every change as a
  reissue.

## Context

A site may buy more for a product over time: an add-on, more capacity, an upgrade, a renewal.
Either several keys per product are combined at evaluation, or each change replaces the key.

## Decision

A key is one license for one product: identity, one expiry, one feature set. A site holds at
most one key per product. Evaluation takes zero or one key string for a product; where several
settings sources hold a key for the same product, the host's settings precedence picks one and
the library never sees a second. Every license change (add-on, capacity, upgrade, renewal,
refund) is a reissue under the same reference with the contents edited; the new key replaces
the old. No key supplied gives the state *missing*. A separately sold add-on is its own product
with its own key.

## Reasons

- The site owner always has exactly one current key per product.
- It removes superseding, duplicates, ties, vendor-error and conflict flags, per-type merge
  rules, the license count and the additive-number restriction (PDR-0011).

## Rejected options

| Option | Why rejected |
|---|---|
| Combine several keys per product | Most evaluation rules existed only for it; tied reissues summed into an over-grant; text values conflicted across licenses and answered nothing |
| Several keys, at most one counted | Keeps superseding and duplicates only to report a mistake |

## Consequences

| Cost | Who | Mitigation |
|---|---|---|
| A second license for the same product gives nothing until merged; the library never sees it | Site owner | Vendor guidance: sell every change as a reissue; merge duplicate purchases into one key and refund the other |
| The library cannot say a site holds an older key than the latest issued | Implementor | Key identifiers compare across environments |
| An older reissue, or a key issued in error, stays valid until its expiry wherever installed | Vendor | Inherent offline limit; shorter terms |
| Renewals and add-ons need the key replaced in every environment | Implementor | Documentation |

Not expressible: an add-on with its own term (unless sold as its own product); several
licenses for one product on one site.
