## 1. Library and test projects

Depends on: none. Owner: worker-backend. Paths: `src/**`, `tests/**`,
`Umbraco.Community.Licensing.slnx` (own project entries).

- [x] 1.1 Move `src/Umbraco.Community.Licensing.Core/` to `src/Umbraco.Community.Licensing/`, rename the project file to `Umbraco.Community.Licensing.csproj`, set `RootNamespace`, `AssemblyName` and `PackageId` to `Umbraco.Community.Licensing`, set `InternalsVisibleTo` to `Umbraco.Community.Licensing.Tests`, change every `namespace Umbraco.Community.Licensing.Core` to `namespace Umbraco.Community.Licensing`, delete the old folder including `bin/` and `obj/`, and verify `dotnet build src/Umbraco.Community.Licensing -c Release --no-incremental` reports 0 warnings and 0 errors
- [x] 1.2 Move `tests/Umbraco.Community.Licensing.Core.Tests/` to `tests/Umbraco.Community.Licensing.Tests/`, rename the project file to `Umbraco.Community.Licensing.Tests.csproj`, set `RootNamespace` to `Umbraco.Community.Licensing.Tests`, point the project reference at the new library project, change test namespaces and `using` lines from `Umbraco.Community.Licensing.Core[.Tests]` to `Umbraco.Community.Licensing[.Tests]`, make the assembly-name smoke test expect `Umbraco.Community.Licensing`, update both project entries in `Umbraco.Community.Licensing.slnx`, delete the old folder including `bin/` and `obj/`, and verify `dotnet build Umbraco.Community.Licensing.slnx -c Release --no-incremental` reports 0 warnings and `dotnet test --solution Umbraco.Community.Licensing.slnx -c Release --no-build` reports 817 total, 816 passed, 1 skipped, 0 failed

## 2. Release workflow

Depends on: 1. Owner: worker-backend (shared CI, assigned). Paths: `.github/workflows/release.yml`.

- [x] 2.1 Point the `dotnet pack` step in `release.yml` at `src/Umbraco.Community.Licensing/Umbraco.Community.Licensing.csproj`, and verify `dotnet pack src/Umbraco.Community.Licensing/Umbraco.Community.Licensing.csproj -c Release --no-build -o <temp dir>` produces `Umbraco.Community.Licensing.0.1.0-beta.2.nupkg` whose nuspec `id` is `Umbraco.Community.Licensing`, and `node --test "tools/release/*.test.mjs"` reports 98 passed

## 3. Documentation and records

Depends on: 1. Owner: Architect. Paths: `README.md`, `docs/releasing.md`, `RELEASES.md`
(header only), `openspec/changes/add-release-pipeline/`.

- [x] 3.1 Replace the package name and `using` line in `README.md`, rewrite its naming sentence to drop "Core", and update the package name and `.nupkg` name in `docs/releasing.md`, and verify by grep that neither file contains `Licensing.Core`
- [x] 3.2 Change the `RELEASES.md` header to name `Umbraco.Community.Licensing` and note that 0.1.0-beta.1 and 0.1.0-beta.2 were attempted as `Umbraco.Community.Licensing.Core` (ADR-0010), leaving rows unchanged, and verify `node --test "tools/release/*.test.mjs"` still reports 98 passed
- [x] 3.3 Run `opsx:update` on `add-release-pipeline` so its open task 7.2 and design.md name the new package ID and project path, and verify `openspec validate add-release-pipeline` passes

## 4. Final verification

Depends on: 1, 2, 3. Owner: Architect (runs checks; no file edits).

- [x] 4.1 Run `node tools/release/platform.mjs`, a clean `dotnet build Umbraco.Community.Licensing.slnx -c Release --no-incremental`, `dotnet test --solution Umbraco.Community.Licensing.slnx -c Release --no-build`, `node --test "tools/release/*.test.mjs"` and `dotnet pack`, and verify results match the design.md baseline (0 warnings; 816 passed, 1 skipped, 0 failed; 98 passed; package `Umbraco.Community.Licensing`), and that `grep -r "Licensing\.Core"` outside `openspec/changes/archive/`, this change, `RELEASES.md` history note and ADR historical text returns nothing
