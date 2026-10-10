# ADR-0003: Packaging, naming, clock and dependencies

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q3 (`CLEAN-PROJECT-PROMPT.md` section 10, technology
  item 3)

## Context

The core has no Umbraco or host dependency. A host-side Umbraco package is deferred. Vendors are
Umbraco package authors and search for Umbraco community packages.

## Decision

- **One package** for generation, validation and signing-key management:
  `Umbraco.Community.Licensing.Core` (Carl, 2026-10-09). Root namespace the same. The bare name
  `Umbraco.Community.Licensing` is reserved for the later host package.
- **Projects**: `src/Umbraco.Community.Licensing.Core/` and
  `tests/Umbraco.Community.Licensing.Core.Tests/`, both in `Umbraco.Community.Licensing.slnx`.
  `InternalsVisibleTo` the test project only (ADR-0005).
- **Target**: `net10.0` (from `Directory.Build.props`). `GenerateDocumentationFile` on for the
  library, so with warnings as errors every public member is documented. Package metadata: MIT
  licence expression, README, description, tags; no icon yet.
- **Clock**: `System.TimeProvider` injected into issuer and evaluator, defaulting to
  `TimeProvider.System`.
- **Dependencies**: library, none beyond the shared framework. Tests: `xunit.v3`,
  `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` (approved by Carl, 2026-10-09), latest
  stable versions at least two weeks old at apply time, pinned in the test project file. No `FakeTimeProvider` package: a
  fixed-time `TimeProvider` subclass in the test project (a few lines).

## Alternatives Considered

| Option | Why not |
|---|---|
| `Umbraco.Community.Licensing` for the core | Suggests an Umbraco dependency; leaves the host package needing an awkward suffix |
| Host-neutral name (`Community.Licensing`) | Loses discoverability with the actual audience |
| Separate issuer and validator packages | Nothing to split: both are BCL-only and small; vendors need both |
| `DateTime.UtcNow` / custom clock interface | `TimeProvider` is the BCL standard and testable |
| `Microsoft.Extensions.TimeProvider.Testing` | A dependency for a class the tests can write in a few lines |
| xUnit v2 | v3 is current; v2 is in maintenance |

## Consequences

The package name mentions Umbraco while the code does not depend on it; the README says so.

## Reversal Cost

Low before first publish (rename). After publish: a new package ID and a deprecation notice.
