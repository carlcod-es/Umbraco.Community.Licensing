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
| [PDR-0002](0002-evaluation-states-and-claims.md) | Evaluation states and what a result reports | Amended by PDR-0018, PDR-0020, PDR-0026 |
| [PDR-0003](0003-empty-key-is-missing.md) | An empty or whitespace-only key is missing | Decided |
| [PDR-0004](0004-product-id-reference-identifier.md) | Product ID, license reference and key identifier | Amended by PDR-0020, PDR-0023 |
| [PDR-0005](0005-random-key-part.md) | Random key part at every issue | Decided |
| [PDR-0006](0006-issue-time-informational.md) | Issue time is informational | Decided |
| [PDR-0007](0007-expiry-to-the-second.md) | Expiry is a stated UTC date and time to the second | Decided |
| [PDR-0008](0008-display-name.md) | Display name | Amended by PDR-0042 |
| [PDR-0009](0009-vendor-tag.md) | Vendor tag | Decided |
| [PDR-0010](0010-feature-types-and-names.md) | Feature types, name rule and lookup | Amended by PDR-0019 |
| [PDR-0011](0011-numbers-not-additive.md) | Numbers are not additive-only | Decided |
| [PDR-0012](0012-text-is-opaque.md) | Text values are opaque; no text set | Amended by PDR-0042 |
| [PDR-0013](0013-reissue-from-presented-key.md) | Reissue from the presented key | Amended by PDR-0021 |
| [PDR-0014](0014-personal-data-by-guidance.md) | Personal data kept out by vendor guidance | Decided |
| [PDR-0015](0015-signing-keys-and-rotation.md) | Signing keys, trusted set and rotation | Decided |
| [PDR-0016](0016-feature-definitions-at-issue.md) | Feature definitions at issue | Decided |
| [PDR-0017](0017-length-limits.md) | Length limits | Amended by PDR-0025 |
| [PDR-0018](0018-not-supported-state.md) | Not supported state; empty trusted set at setup | Decided |
| [PDR-0019](0019-typed-lookups-only.md) | Typed feature lookups only | Decided |
| [PDR-0020](0020-identifier-required.md) | A key without a valid identifier is unreadable | Amended by PDR-0023 |
| [PDR-0021](0021-reissue-fixes-product-and-reference.md) | A reissue keeps its product and reference | Decided |
| [PDR-0022](0022-evaluation-per-request.md) | Evaluation on every request | Decided |
| [PDR-0023](0023-key-identifier-prefix.md) | Vendor-chosen key identifier prefix | Decided |
| [PDR-0024](0024-beta-without-stability-promise.md) | Beta release with no stability promise | Decided |
| [PDR-0025](0025-issue-refuses-oversized-key.md) | Issue refuses a key too long to deploy | Decided |
| [PDR-0026](0026-claimed-product-meets-product-id-rule.md) | A claimed product is reported only when it is a valid product ID | Decided |
| [PDR-0027](0027-release-versioning-and-log.md) | Release versioning and release log | Amended by PDR-0029 |
| [PDR-0028](0028-test-coverage-per-release.md) | Test coverage per release | Decided |
| [PDR-0029](0029-release-tag-matches-log.md) | A release tag must match its release log row | Decided |
| [PDR-0030](0030-vendor-tool-audience-and-operation.md) | Vendor tool audience and operation | Amended by PDR-0039, PDR-0048; to revisit: FR-26 (PDR-0046) |
| [PDR-0031](0031-tool-folder-moving-and-backups.md) | Tool folder, moving and backups | Amended by PDR-0038, PDR-0040, PDR-0041, PDR-0045, PDR-0048, PDR-0052; to revisit: FR-26 (PDR-0046) |
| [PDR-0032](0032-private-keys-protected-by-passphrase.md) | Private keys protected by a passphrase | Amended by PDR-0039, PDR-0043, PDR-0049; to revisit: FR-26 (PDR-0046) |
| [PDR-0033](0033-license-records-and-customer-data.md) | License records and customer data | Amended by PDR-0043; to revisit: FR-26 (PDR-0046) |
| [PDR-0034](0034-reissue-and-renewal-from-records.md) | Reissue and renewal from records | Amended by PDR-0039, PDR-0044, PDR-0048, PDR-0052 |
| [PDR-0035](0035-signing-key-switch-standby-and-trusted-keys.md) | Signing key switch, standby keys and trusted keys | Amended by PDR-0047, PDR-0051 |
| [PDR-0036](0036-feature-definitions-add-only.md) | Products and feature definitions are add-only | Amended by PDR-0043, PDR-0054 |
| [PDR-0037](0037-github-release-per-published-release.md) | A GitHub Release for each published release | Decided |
| [PDR-0038](0038-vendor-tool-operating-systems-and-terminals.md) | Vendor tool operating systems and terminals | Amended by PDR-0039, PDR-0040, PDR-0048 |
| [PDR-0039](0039-vendor-tool-guided-mode-only.md) | Vendor tool runs in guided mode only | Amended by PDR-0048, PDR-0049, PDR-0050; to revisit: FR-26 (PDR-0046) |
| [PDR-0040](0040-installation-moves-between-operating-systems.md) | An installation moves between operating systems | Decided |
| [PDR-0041](0041-finding-the-installation-at-startup.md) | Finding the installation at startup | Amended by PDR-0048 |
| [PDR-0042](0042-text-rule-allows-characters-by-kind.md) | Text rule allows characters by kind | Decided |
| [PDR-0043](0043-vendor-tool-typed-values.md) | Vendor tool typed values | Decided |
| [PDR-0044](0044-switch-off-means-not-granted.md) | A switch typed as off means not granted | Amended by PDR-0048 |
| [PDR-0045](0045-tool-never-overwrites-files-or-creates-folders.md) | The tool never overwrites a file or creates a folder it was not asked for | Amended by PDR-0048 |
| [PDR-0046](0046-records-portable-to-a-central-database.md) | Records can move to a central database | Decided |
| [PDR-0047](0047-switch-confirms-when-new-key-not-in-a-release.md) | A switch confirms whenever the new active key is not in a release | Amended by PDR-0050, PDR-0051 |
| [PDR-0048](0048-vendor-tool-menu-session.md) | The vendor tool runs as a menu session | Amended by PDR-0050, PDR-0052, PDR-0053 |
| [PDR-0049](0049-passphrase-kept-for-a-session-with-idle-lock.md) | Passphrase kept for a session, with an idle lock | Amended by PDR-0050, PDR-0052 |
| [PDR-0050](0050-menu-session-refinements.md) | Menu session refinements | Decided |
| [PDR-0051](0051-release-confirmed-per-product.md) | Release confirmed per product | Decided |
| [PDR-0052](0052-menu-session-review-refinements.md) | Menu session review refinements | Decided |
| [PDR-0053](0053-unexpected-failure-in-a-session.md) | Unexpected failure in a session | Decided |
| [PDR-0054](0054-product-defines-at-most-50-features.md) | A product defines at most 50 features | Decided |

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
