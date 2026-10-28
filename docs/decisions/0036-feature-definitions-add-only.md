# PDR-0036: Products and feature definitions are add-only

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q17. Builds on PDR-0016.
- Personas: site owner (primary: no paid feature silently read as not granted). Vendor: a typo is
  fixed by adding the right name.

## Context

The tool holds each product's feature definitions and checks every issue against them
(PDR-0016). A released product reads each feature by name and type. Changing a type makes the
product read it as not granted (PDR-0010); removing one makes reissue of licenses carrying it
fail.

## Decision

- A feature definition, once added, is never removed, renamed or changed in type.
- A product ID never changes. A product's display name can be edited.

## Reasons

- The simplest rule that keeps every issued key readable by every release of the product.

## Rejected options

| Option | Why rejected |
|---|---|
| Add and retire (retired features refused on new keys, dropped on reissue with a notice) | One more state and rule; FR-14 |
| Free editing | Allows the silent "not granted" case |

## Consequences

- A dropped or mistyped feature stays defined; new keys simply leave it out.
