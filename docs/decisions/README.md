# Product decision records (PDR)

A PDR records how the library behaves as a site owner, implementor or vendor experiences it.
Test: if the technology stack changed, the PDR would still hold. Technology decisions are ADRs
(`docs/adrs/`). The reasons and rejected options are source material for the library's user
documentation.

Cite as PDR-NNNN. Statuses: Decided, Dropped, Superseded by PDR-NNNN, Amended by PDR-NNNN,
Deferred. Update this index in the same change as any status line.

## Index

| PDR | Title | Status |
|---|---|---|
| [PDR-0001](0001-one-key-per-product.md) | One key per product | Decided |
| [PDR-0002](0002-evaluation-states-and-claims.md) | Evaluation states and what a result reports | Amended by PDR-0018, PDR-0020 |
| [PDR-0003](0003-empty-key-is-missing.md) | An empty or whitespace-only key is missing | Decided |
| [PDR-0004](0004-product-id-reference-identifier.md) | Product ID, license reference and key identifier | Amended by PDR-0020, PDR-0023 |
| [PDR-0005](0005-random-key-part.md) | Random key part at every issue | Decided |
| [PDR-0006](0006-issue-time-informational.md) | Issue time is informational | Decided |
| [PDR-0007](0007-expiry-to-the-second.md) | Expiry is a stated UTC date and time to the second | Decided |
| [PDR-0008](0008-display-name.md) | Display name | Decided |
| [PDR-0009](0009-vendor-tag.md) | Vendor tag | Decided |
| [PDR-0010](0010-feature-types-and-names.md) | Feature types, name rule and lookup | Amended by PDR-0019 |
| [PDR-0011](0011-numbers-not-additive.md) | Numbers are not additive-only | Decided |
| [PDR-0012](0012-text-is-opaque.md) | Text values are opaque; no text set | Decided |
| [PDR-0013](0013-reissue-from-presented-key.md) | Reissue from the presented key | Amended by PDR-0021 |
| [PDR-0014](0014-personal-data-by-guidance.md) | Personal data kept out by vendor guidance | Decided |
| [PDR-0015](0015-signing-keys-and-rotation.md) | Signing keys, trusted set and rotation | Decided |
| [PDR-0016](0016-feature-definitions-at-issue.md) | Feature definitions at issue | Decided |
| [PDR-0017](0017-length-limits.md) | Length limits | Decided |
| [PDR-0018](0018-not-supported-state.md) | Not supported state; empty trusted set at setup | Decided |
| [PDR-0019](0019-typed-lookups-only.md) | Typed feature lookups only | Decided |
| [PDR-0020](0020-identifier-required.md) | A key without a valid identifier is unreadable | Amended by PDR-0023 |
| [PDR-0021](0021-reissue-fixes-product-and-reference.md) | A reissue keeps its product and reference | Decided |
| [PDR-0022](0022-evaluation-per-request.md) | Evaluation on every request | Decided |
| [PDR-0023](0023-key-identifier-prefix.md) | Vendor-chosen key identifier prefix | Decided |

## Template

```markdown
# PDR-NNNN: <title>

- Status: Decided
- Date: YYYY-MM-DD
- Source: <change> design.md R<n> / Q<n>
- Personas: <who it serves; who it could harm and why it does not>

## Context
<the problem, in product terms>

## Decision
<the behaviour>

## Reasons
<why; these feed the user documentation>

## Rejected options
| Option | Why rejected |
|---|---|

## Consequences
<costs accepted, documentation it requires>
```
