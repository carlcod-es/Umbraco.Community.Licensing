# PDR-0056: Trusted key comment lines start with `//`

- Status: Decided
- Date: 2026-11-09
- Source: `add-vendor-tool` design.md Q75. Amends PDR-0035.
- Personas: implementor (primary: the printed list pastes into the product's code or
  configuration with its role lines kept). Vendor: copies one block per release. Site owner,
  backoffice editor, site visitor: unaffected.

## Context

Trusted keys prints, for a product, each public key line its next release must trust, preceded
by a comment line naming its role (PDR-0035). The library reads one key per call and has no
comment syntax of its own, so the comment form was not set. Implementors put the keys in the
product's code or configuration (vendor guide: Check a license in the product). The section 9
apply of `add-vendor-tool` raised it.

## Decision

- Every comment line printed by Trusted keys and Release confirmed starts with `// `: the
  heading naming the product, and the role line before each key (active, standby, replaced with
  its reason, a leaked key marked as leaked).
- Key lines are printed unchanged, one per line, never starting with `/`.

## Reasons

- `//` is a comment in the product's code and in .NET JSON configuration, so the role lines can
  stay next to the keys they describe when pasted.

## Rejected options

| Option | Why rejected |
|---|---|
| `#` | Not a comment in the product's code or JSON configuration; the implementor deletes the role lines and loses which key is which |
| Print a ready-made code snippet | Ties the tool to one way of loading keys; the key lines alone serve code and configuration |

## Consequences

- The tool guide shows the printed block and how each key line is used.
