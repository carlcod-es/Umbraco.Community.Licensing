# ADR-0011: Vendor tool packaging and versioning

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q18. Amends ADR-0008.

## Context

The vendor tool (PDR-0030) is a console application for vendors who already build .NET products
on the library. ADR-0008 publishes one package from one tested build; ADR-0009 holds one version
in `Version.props`.

## Decision

- **Package.** A .NET tool package, `Umbraco.Community.Licensing.Tool`, command `ucl`, on
  nuget.org. Installed globally (`dotnet tool install -g`) or per folder with a tool manifest.
  Prerequisite: the .NET SDK (Carl, 2026-10-28).
- **Project.** `src/Umbraco.Community.Licensing.Tool`, project reference to the library (bundled
  into the tool package); tests in `tests/Umbraco.Community.Licensing.Tool.Tests`. The library
  gains no dependency.
- **Version.** The tool imports `Version.props`: same version as the library, released together.
  The release command is unchanged.
- **Release run** (amends ADR-0008): packs both projects into the pack folder; installs the tool
  from that folder into a temp tool path and runs `ucl --version`; pushes `*.nupkg` from the pack
  folder. Nothing is pushed unless every step passes.

## Alternatives Considered

| Option | Why not |
|---|---|
| Self-contained executables per OS | One build per OS and CPU, larger downloads, not on nuget.org, own update path |
| Source or sample only | Vendors maintain it themselves; not a product |
| Tool inside the library package | Adds the tool's dependencies to every product |
| Separate version line for the tool | A second version file, log and tag scheme |

## Consequences

- A tool-only fix also bumps the library's version (accepted in beta).
- The first push of the new package ID relies on the Trusted Publishing policy covering it; if
  refused, Carl publishes the first version with a short-lived API key.

## Reversal Cost

Low before the first tool release. Afterwards, splitting versions needs a second version file
and log, and a note in both READMEs.
