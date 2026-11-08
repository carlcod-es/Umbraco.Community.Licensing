# PDR-0051: Release confirmed per product

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q66. Amends PDR-0035, PDR-0047.
- Personas: site owner (primary: in shared mode, a switch no longer breaks keys on one product's
  sites because another product's release shipped the standby). Implementor (each product's
  release trusts the new key before keys signed by it reach its sites). Vendor: one confirmation
  per product release, as each ships. Backoffice editor, site visitor: unaffected.

## Context

Each product release ships the public keys it trusts (PDR-0015). PDR-0035 marks a standby key
"not yet in a release" until the vendor confirms a release trusts it, and PDR-0047 warns before
a switch to a key not yet in a release. In shared mode one standby key serves several products,
and each product has its own release. One confirmation, for any product, marked the key as in a
release for all of them: after a commerce release shipped the standby, a switch went ahead with
no warning while shipping sites, on a release that did not trust the new key, reported every
newly issued key as signed by a key not recognised.

The question only arises when a standby key exists. With none, a switch makes a new key pair
active that no release can trust yet, and the switch always warns (PDR-0047).

## Decision

- **Confirmed per product.** Release confirmed is given for one product: a release of this
  product is published and trusts the key list printed for it. It marks every key in that list
  as in a release for that product only.
- **Switch.** The switch warns and asks for confirmation (PDR-0047) when the key becoming active
  is not in a release for at least one product it signs for. The warning names those products.
  With no standby key it warns as before.
- **Status.** Beside each standby key, the tool shows the products whose release does not yet
  trust it.
- **New product in shared mode.** A product added after a key was confirmed starts with that key
  not in a release for it.
- **New standby after a switch.** It starts in a release for no product.
- **Per-product mode.** Unchanged in effect: each key signs for one product.

## Reasons

- Products ship separately; the tool can only know what the vendor tells it, product by product.
- The warning exists to protect site owners (PDR-0047); it must not be skipped because a
  different product's sites are safe.
- The vendor confirms at the moment each release ships, which is when they know it is true.

## Rejected options

| Option | Why rejected |
|---|---|
| One confirmation covers every product in shared mode | A vendor who releases one product now and another next month either cannot confirm or confirms early, which recreates the break; a product added later is silently assumed to trust the list |
| Always warn on a switch in shared mode | The warning shows even when every release is up to date, so vendors learn to pass it; that weakens PDR-0047 |
| Keep one mark per key and explain it in the guide | The site owner's risk stays; the only protection is the vendor reading the guide |
| Drop shared mode from this change | Reverses PDR-0030; vendors who want one key across their products lose it |

## Consequences

- In shared mode, Status names products not yet shipping a standby until each product's release
  is confirmed.
- A product added in shared mode and never released is named in the warning until its first
  release is confirmed. It has no sites yet, so the vendor may confirm or switch anyway.
- The tool guide explains confirming each product's release as it ships.
