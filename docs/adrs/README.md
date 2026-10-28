# Architecture decision records (ADR)

An ADR records how the behaviour is built: algorithm, format, packaging, dependencies. Product
behaviour is a PDR (`docs/decisions/`). Test: if the stack changed, an ADR would be
reconsidered.

Cite as ADR-NNNN. Shape: Status, Context, Decision, Alternatives Considered, Consequences,
Reversal Cost. Statuses: Decided, Dropped, Superseded by ADR-NNNN, Amended by ADR-NNNN,
Deferred. Update this index in the same change as any status line.

## Index

| ADR | Title | Status |
|---|---|---|
| [ADR-0001](0001-signing-and-key-string.md) | Signing algorithm, key string envelope and signing key ID | Amended by ADR-0007 |
| [ADR-0002](0002-payload-format-and-strict-read.md) | Payload format and strict read | Amended by ADR-0007 |
| [ADR-0003](0003-packaging-naming-and-dependencies.md) | Packaging, naming, clock and dependencies | Amended by ADR-0006, ADR-0010 |
| [ADR-0004](0004-public-api.md) | Public API surface | Amended by ADR-0010 |
| [ADR-0005](0005-example-keys-and-test-strategy.md) | Example key strings and test strategy | Amended by ADR-0010 |
| [ADR-0006](0006-test-runner-and-package-metadata.md) | Test runner and package metadata | Amended by ADR-0009 |
| [ADR-0007](0007-compact-payload-encoding.md) | Compact payload encoding and issue length check | Decided |
| [ADR-0008](0008-ci-runners-and-publishing.md) | CI host, runners and package publishing | Amended by ADR-0011, ADR-0016 |
| [ADR-0009](0009-release-command-version-file-and-log.md) | Release command, version file and release log | Decided |
| [ADR-0010](0010-bare-package-name.md) | Bare package name for the library | Decided |
| [ADR-0011](0011-vendor-tool-packaging-and-versioning.md) | Vendor tool packaging and versioning | Decided |
| [ADR-0012](0012-vendor-tool-command-line-and-console.md) | Vendor tool command line and console | Decided |
| [ADR-0013](0013-vendor-tool-database-and-folder.md) | Vendor tool database and folder | Decided |
| [ADR-0014](0014-vendor-tool-private-key-encryption.md) | Vendor tool private-key encryption | Decided |
| [ADR-0015](0015-vendor-tool-backup-and-export-formats.md) | Vendor tool backup and export formats | Decided |
| [ADR-0016](0016-github-release-job.md) | GitHub Release job in the release run | Decided |
