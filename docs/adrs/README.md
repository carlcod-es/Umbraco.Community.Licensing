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
| [ADR-0003](0003-packaging-naming-and-dependencies.md) | Packaging, naming, clock and dependencies | Amended by ADR-0006 |
| [ADR-0004](0004-public-api.md) | Public API surface | Decided |
| [ADR-0005](0005-example-keys-and-test-strategy.md) | Example key strings and test strategy | Decided |
| [ADR-0006](0006-test-runner-and-package-metadata.md) | Test runner and package metadata | Amended by ADR-0009 |
| [ADR-0007](0007-compact-payload-encoding.md) | Compact payload encoding and issue length check | Decided |
| [ADR-0008](0008-ci-runners-and-publishing.md) | CI host, runners and package publishing | Decided |
| [ADR-0009](0009-release-command-version-file-and-log.md) | Release command, version file and release log | Decided |
