## Why

The package, assembly and namespace are `Umbraco.Community.Licensing.Core`. Carl wants the bare
name `Umbraco.Community.Licensing` for the library before its first nuget.org publish (Carl,
2026-10-20). Neither name is on nuget.org yet (checked 2026-10-20: both 0.1.0-beta attempts
failed to publish), so the rename costs no deprecation or migration.

## What Changes

- **BREAKING** (source only, no published consumers): package ID, assembly name and root
  namespace change from `Umbraco.Community.Licensing.Core` to `Umbraco.Community.Licensing`.
  Vendors write `using Umbraco.Community.Licensing;`.
- Library and test projects and their folders take the new name.
- Release workflow, README, release guide and release log header name the new package.
- The bare name is no longer kept for the later host package; that package takes a suffix,
  chosen by the change that adds it.
- No behaviour change: key format, public types and members, evaluation states are unchanged.

Personas: serves the vendor (shorter, unsuffixed namespace and package name). No effect on
site owner, implementor, site visitor or backoffice editor.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. No spec mentions the package or namespace name; `skip_specs: true`.

## Impact

- `src/Umbraco.Community.Licensing.Core/` → `src/Umbraco.Community.Licensing/`
- `tests/Umbraco.Community.Licensing.Core.Tests/` → `tests/Umbraco.Community.Licensing.Tests/`
- `Umbraco.Community.Licensing.slnx`, `.github/workflows/release.yml`
- `README.md`, `docs/releasing.md`, `RELEASES.md` header
- Records: ADR-0003, ADR-0004, ADR-0005 amended by ADR-0010
- `add-release-pipeline` (in progress): its open task 7.2 names the old package ID; revised
  through `opsx:update` on that change
- Archived changes keep the old name as history.
