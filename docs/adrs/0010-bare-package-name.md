# ADR-0010: Bare package name for the library

- Status: Decided
- Date: 2026-10-20
- Source: `rename-to-bare-package-name` design.md Q1. Amends ADR-0003, ADR-0004, ADR-0005.

## Context

ADR-0003 named the library `Umbraco.Community.Licensing.Core` and reserved the bare name for a
later host package. Before the first nuget.org publish Carl asked to drop `.Core`. Neither ID is
on nuget.org (checked 2026-10-20), so renaming costs no deprecation.

## Decision

- Package ID, assembly name and root namespace: `Umbraco.Community.Licensing` (Carl,
  2026-10-20). Amends ADR-0003 (package, projects) and ADR-0004 (namespace).
- Projects: `src/Umbraco.Community.Licensing/` and `tests/Umbraco.Community.Licensing.Tests/`;
  test namespaces `Umbraco.Community.Licensing.Tests[.<Folder>]`. Amends ADR-0003 and the test
  project path in ADR-0005.
- The later host package takes a suffix, chosen by the change that adds it.
- Version sequence continues (next release `0.1.0-beta.3`); attempts 0.1.0-beta.1 and
  0.1.0-beta.2 were made as `Umbraco.Community.Licensing.Core` and stay in `RELEASES.md`.

## Alternatives Considered

| Option | Why not |
|---|---|
| Keep `Umbraco.Community.Licensing.Core` (ADR-0003) | Carl wants the bare name for the package every vendor installs |
| Namespace only: bare namespace, package ID `.Core` | Package and namespace differ; Carl preferred the core holding the bare ID (core + suffixed integration, as `Serilog` / `Serilog.AspNetCore`) |
| Restart versions at `0.1.0-beta.1` under the new ID | Release log is one sequence; PDR-0027 does not reuse numbers |

## Consequences

Vendors write `using Umbraco.Community.Licensing;`. ADR-0003's concern stands: the name mentions
Umbraco while the code does not depend on it; the README says so. The host package needs a
suffix.

## Reversal Cost

Low before first publish. After publish: a new package ID and a deprecation notice.
