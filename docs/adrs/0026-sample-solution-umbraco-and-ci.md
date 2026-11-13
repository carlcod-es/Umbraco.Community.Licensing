# ADR-0026: Sample solution, Umbraco version and CI

- Status: Decided
- Package approval: pending Carl (requested 2026-11-13); recorded here with the pinned versions at task 2.1
- Date: 2026-11-13
- Source: `add-sample-package` design.md Q2, Q4, Q19, Q20

## Context

PDR-0067 adds an Umbraco 17 sample package and demo site that is never published but is checked
on every change. `release.yml` builds, tests and packs all of `Umbraco.Community.Licensing.slnx`
on Node 22. The library is BCL-only (ADR-0003). Umbraco 17's backoffice client needs Node 24.13
or later, and Umbraco's transitive packages raise NuGet advisory warnings from time to time,
which the root `TreatWarningsAsErrors` would turn into failures.

## Decision

- **Own solution** `samples/Acme.ContactForm.slnx`; the root solution and `release.yml` are
  unchanged. Every sample project has `IsPackable=false`.
- **`samples/Directory.Build.props`** imports the root props and adds only `WarningsNotAsErrors`
  NU1901 to NU1904.
- **Project reference** from the sample package to `src/Umbraco.Community.Licensing` (Carl,
  2026-11-13).
- **Umbraco 17 LTS**, exact pin at the latest 17.x patch at apply, for all Umbraco packages in
  the sample: the package takes `Umbraco.Cms.Web.Website`, `Umbraco.Cms.Web.BackOffice` and
  `Umbraco.Cms.Api.Management`; the site `Umbraco.Cms` and `Umbraco.Cms.Persistence.Sqlite`, as
  the 17 templates list them. Tests take `xunit.v3` `[4.0.1]` and no mocking package.
- **Scaffolded** from `Umbraco.Templates` 17.x installed into a temporary template hive; the
  templates are not a repo dependency.
- **CI**: `.github/workflows/sample.yml` on pull requests and pushes to `main` touching
  `samples/**`, `src/Umbraco.Community.Licensing/**`, `global.json`, `Directory.Build.props` or
  the workflow. Ubuntu, Node 24, client `npm ci` and `npm run build`, then sample `dotnet build`
  and `dotnet test` in Release. Actions pinned by commit.

## Alternatives Considered

| Option | Why not |
|---|---|
| Sample projects in the root solution | The release would restore Umbraco, need Node 24, and could pack the sample |
| NuGet reference to the published library | Lags the source; beta API changes break the sample until bumped; does not catch breaks before release (Carl chose project reference) |
| Umbraco 18 | Short-term support; most vendors target the LTS |
| Central package management for the sample | One solution; the repo pins inline elsewhere |
| Relaxing warnings as errors for the sample | Hides real warnings; only advisories need relaxing |
| A sample job in `ci.yml` | Node 24 and Umbraco restore on every library-only run of the existing job; separate paths filter is clearer |

## Consequences

- The sample adds no package to the library or the vendor tool.
- CI gains a second workflow; a library change runs both.
- An Umbraco advisory shows as a warning in the sample build, not a failure.

## Reversal Cost

Low. Moving to Umbraco 18 or a NuGet reference changes the sample's project files only; folding
the sample into the root solution needs `release.yml` changes.
