## Context

See proposal.md - Why. Current state: one library project and one test project, both named
`Umbraco.Community.Licensing.Core[.Tests]`; namespaces follow the project name, flat in the
library (`Umbraco.Community.Licensing.Core`), per folder in tests
(`Umbraco.Community.Licensing.Core.Tests.<Folder>`). About 140 references in ~80 files. The
release tooling (`tools/release/`) does not name the package; `release.yml` names the project
path for `dotnet pack`. `Version.props` is imported by relative path and is unaffected.

Baseline before the change (2026-10-20, macOS arm64): `dotnet build -c Release` 0 warnings
0 errors; `dotnet test --solution` 817 total, 816 passed, 1 skipped (explicit
`ExampleKeyGenerator`), 0 failed; `node --test "tools/release/*.test.mjs"` 98 passed;
`dotnet pack` succeeds.

## Bar

**Production.** The library is about to be published. The bar from `add-license-core` stands.

**Test strategy:** no new tests. The existing suite is the regression check: the same counts
as the baseline must pass after the rename. The one test whose expectation changes is the
assembly-name smoke test, which must assert the new name. `dotnet pack` must produce
`Umbraco.Community.Licensing.<version>.nupkg`.

## Goals / Non-Goals

**Goals:** one name, `Umbraco.Community.Licensing`, for package ID, assembly, root namespace
and project folder; tests follow as `Umbraco.Community.Licensing.Tests`.

**Non-Goals:** naming the host package; changing public types, sub-namespaces or the key
format; editing archived changes; changing `Version.props` (the next release continues the
version sequence, PDR-0027).

## Questions settled in this change

| Q | Question | Answer | Record |
|---|---|---|---|
| Q1 | Library package and namespace name | `Umbraco.Community.Licensing` for package ID, assembly, root namespace and folders; host package takes a suffix later (Carl, 2026-10-20) | ADR-0010 |

## Decisions

- **Full rename, not namespace only** (Q1, ADR-0010). Alternative: change only the namespace
  and keep package ID `.Core`, leaving the bare ID to the host package (the
  `Microsoft.Extensions.*.Abstractions` pattern). Carl chose the full rename: the core is what
  every vendor installs, so it gets the bare name (the `Serilog` / `Serilog.AspNetCore`
  pattern).
- **Test project** becomes `Umbraco.Community.Licensing.Tests`, namespaces
  `Umbraco.Community.Licensing.Tests.<Folder>`, so production and test names stay parallel.
- **Folders moved, history kept.** Workers move files on disk; git detects the renames when
  Carl commits. No `git mv` (workers do not touch git state).
- **Version continues.** `Version.props` keeps `0.1.0-beta.2`; the release command bumps to
  `0.1.0-beta.3` under the new ID. Restarting at `beta.1` under a new ID was rejected: the
  release log is one sequence and PDR-0027 does not reuse numbers.

## Risks / Trade-offs

- [Stale `bin/`/`obj/` under the old folders produce confusing builds] → delete the old
  folders entirely after the move; verify with a `--no-incremental` build.
- [Missed reference] → final task greps for `Licensing.Core` outside archived changes and
  historical record text.
- [Carl's uncommitted edits in `README.md`] → the README edit is a one-word replacement on
  lines Carl has not changed; staged with the rest for Carl to review.
- [Host package later needs a suffix] → accepted; decided by that change.

## Migration Plan

None: no published consumers. Rollback: revert the commit.
