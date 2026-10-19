# ADR-0006: Test runner and package metadata

- Status: Amended by ADR-0009
- Date: 2026-10-11
- Source: `add-license-core` design.md Q23, Q24. Amends ADR-0003.

## Context

ADR-0003 chose xUnit v3 with the VSTest packages and listed package metadata without values.
Tests run on VSTest only because the project opts out of Microsoft.Testing.Platform. The first
release is a beta (PDR-0024), and the version, author, repository and package README were unset
(`dotnet pack` defaulted to 1.0.0 and warned about the README).

## Decision

- **Test runner**: Microsoft.Testing.Platform (Carl, 2026-10-11). `global.json` sets
  `"test": { "runner": "Microsoft.Testing.Platform" }`; the test project drops
  `IsTestingPlatformApplication=false`. `Microsoft.NET.Test.Sdk` and
  `xunit.runner.visualstudio` are removed; they serve VSTest only, and `xunit.v3` runs on the
  platform natively. Test dependencies: `xunit.v3` only.
- **Package metadata** in the library project (Carl, 2026-10-11):
  - `Version` `0.1.0-beta.1` (PDR-0024: 0.x, prerelease).
  - `Authors` `Carl Sargunar`.
  - `RepositoryUrl` `https://github.com/carlcod-es/Umbraco.Community.Licensing`,
    `RepositoryType` `git`. The https form, since package consumers cannot use the SSH remote.
  - `PackageReadmeFile` `README.md`, packed from the repository root.

## Alternatives Considered

| Option | Why not |
|---|---|
| Stay on VSTest | Older runner; Carl chose the newer platform |
| Keep `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` for VSTest-only IDEs | Two dependencies for a runner no longer used |
| `0.1.0` without a prerelease suffix | NuGet lists it as a normal release; "beta" would live only in the README |
| SSH remote as `RepositoryUrl` | Not usable by package consumers |

## Consequences

`dotnet test` on .NET 10 runs through the platform; IDEs need Microsoft.Testing.Platform support.
A solution is passed with `--solution` (`dotnet test --solution Umbraco.Community.Licensing.slnx`);
the positional form is rejected under this runner, so scripts, CI and docs use the flag.
Each later release bumps `Version`.

## Reversal Cost

Low: project file and `global.json` edits; no key or API impact.
