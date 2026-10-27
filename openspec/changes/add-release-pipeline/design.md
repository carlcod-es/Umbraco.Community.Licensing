## Context

See proposal.md (Why). Current state: no CI, no `.github/`; `Version` `0.1.0-beta.1` typed in
`src/Umbraco.Community.Licensing.Core/Umbraco.Community.Licensing.Core.csproj` (ADR-0006; renamed to
`src/Umbraco.Community.Licensing/Umbraco.Community.Licensing.csproj` by `rename-to-bare-package-name`,
ADR-0010); tests
on Microsoft.Testing.Platform, run with `dotnet test --solution Umbraco.Community.Licensing.slnx`;
library tested only on macOS. The repository is public on GitHub in Carl's organisation
`carlcod-es` (Q9); `RepositoryUrl` and the README links already name it. Nothing is published yet.
Product rules: PDR-0027, PDR-0028, PDR-0029. Behaviour: `specs/release-process/spec.md`.

## Bar

**Production.** The pipeline publishes the production package that vendors install, and its
refusals are the only guard against publishing untested or misnumbered code. Every rule in the
release-process spec that can run without GitHub or nuget.org has an automated test; the
workflows themselves are verified by observed runs (see Test strategy).

## Goals / Non-Goals

**Goals:** a published package is always the tested build of a tagged commit on `main`; all
version information in repository files; fork pull requests never reach Carl's machines; no
long-lived publishing secret.

**Non-Goals:** symbol packages, GitHub Releases pages, signing the package, release notes in the
package metadata, automatic dependency updates, per-release multi-OS testing (PDR-0028), any
library change.

## Requirements

| R | Requirement | Spec requirement | Personas | Records |
|---|---|---|---|---|
| R1 | Release started only by Carl with the release command | Release started by Carl with the release command; Release tag | Vendor | PDR-0027 |
| R2 | Version from the line and the log, held in a repository file | Version numbering from the version line | Vendor; site owner (betas stay prereleases) | PDR-0024, PDR-0027 |
| R3 | Release log of every attempt | Release log | Vendor | PDR-0027 |
| R4 | Publish only the tested build; rehearsal | Release run publishes only tested packages | Vendor, implementor | PDR-0027 |
| R5 | Six refusals | Release refusals | Vendor; site owner | PDR-0027, PDR-0029 |
| R6 | Pull requests and `main` checked; fork runs gated | Change checks | Vendor (Carl as maintainer) | ADR-0008 |
| R7 | One test run per release, all-platform check on demand, README line | Test coverage per release | Vendor, implementor; site owner | PDR-0028 |

Persona check: no requirement harms a higher-priority persona. Site owners are protected by
the beta-status refusal (PDR-0024 stays visible on every beta).

## Questions settled in this change

| Q | Question | Answer | Record |
|---|---|---|---|
| Q1 | How a release starts, where the version comes from, how it is numbered and logged | Release command run by Carl; version line in a file; log of every attempt; five refusals (Carl, 2026-10-14, FR-6 explore) | PDR-0027 |
| Q2 | What each release is tested on | Once, on whichever test machine is available; all three OSes before 1.0; README line (Carl, 2026-10-14, FR-6 explore) | PDR-0028 |
| Q3 | Where builds run, and who may run code on Carl's machines | GitHub Actions; hosted runners for pull requests and the all-platform check, Carl's self-hosted runners for releases; approval required for all external contributors (Carl, 2026-10-14); all-platform check disabled 2026-10-18, see Q6; release runner label `licensing-release` dropped for now, any self-hosted runner takes a release (Carl, 2026-10-19) | ADR-0008 |
| Q4 | How the release authenticates to nuget.org | Trusted Publishing; no stored API key (Carl, 2026-10-14) | ADR-0008 |
| Q5 | A tag pushed without the release command (e.g. Carl's shared release-tag command) | Refused: the log row for the version must name the tag (Carl, 2026-10-14) | PDR-0029 |
| Q6 | Means for PDR-0028's pre-1.0 check on all three OSes | Manually started all-platform workflow, built now (Carl, 2026-10-14); kept but disabled with `if: false`, Carl runs the suite by hand on each OS (Carl, 2026-10-18) | ADR-0008 |
| Q7 | Release command language, version file and log format | Node.js `tools/release/`, `Version.props`, `RELEASES.md` table (Architect; Node.js by project constraint) | ADR-0009 |
| Q8 | Release command guards PDR-0027 leaves open | Refuses with no TTY, off `main`, out of step with `origin/main`, or uncommitted edits to its two files; asks for confirmation before writing; rehearsal run without a tag (Architect, for Carl's review of this change) | ADR-0009, ADR-0008 |
| Q9 | Where the repository lives when the first package is published | `https://github.com/carlcod-es/Umbraco.Community.Licensing`; transferred by Carl, remote, `RepositoryUrl` and README links updated (Carl, 2026-10-14) | ADR-0008 |
| Q10 | Which nuget.org account owns the package | nuget.org organisation `Carlcod-es`; policy created by Carl's user `CarlCod_es`, which is `NUGET_USER` (Carl, 2026-10-14) | ADR-0008 |
| Q11 | A tag whose version has no release log row | Refused, as a row naming a different tag (Carl, 2026-10-14) | PDR-0029 |
| Q12 | A tag run whose tests passed but whose NuGet login or push failed, with the version not on nuget.org | Re-run allowed: same tag, same version; the re-run rebuilds and retests the same commit (Carl, 2026-10-19) | ADR-0008 |

## Decisions

### Components

```
  Carl ──> node tools/release/release.mjs ──┐ reads/writes Version.props, RELEASES.md
                    │ uses                  │ commit + push main, tag + push tag
                    v                       v
          tools/release/release-rules.mjs   GitHub (public repo)
                    ^                       │
                    │ uses                  ├─ pull_request / push main ─> ci.yml (hosted ubuntu)
          tools/release/check.mjs <─────────┼─ tag release-* ───────────> release.yml (Carl's runner)
                                            │                              checks → build → test
                                            │                              → pack → NuGet/login → push
                                            └─ manual ──────────────────> all-platforms.yml (hosted ×3, disabled)

  Version.props ──imported by──> Umbraco.Community.Licensing.csproj ──> package version
```

### Workflows, runners, publishing — ADR-0008

Three workflows; hosted runners for anything a stranger can trigger, self-hosted only for tag
pushes (write access required). Trusted Publishing via `NuGet/login`; `id-token: write` only in
`release.yml`. One job builds, tests, packs and pushes, so the pushed file is the tested file.
Actions pinned by commit SHA. New dependencies, approved with this change: `actions/checkout`,
`actions/setup-dotnet`, `actions/setup-node`, `NuGet/login`.

### Release command, version file, release log — ADR-0009

`Version.props` imported by the library project only; three-part versions; `RELEASES.md` table,
newest first. All rules in one pure module shared by the command and the release-run check, so
the command and the run cannot disagree. `runRelease` takes a prompt and a clock so the command
is tested end to end against temporary Git repositories; only the TTY check sits outside it.

### Ownership

New top-level paths, added to the CLAUDE.md ownership table in tasks.md 1.1:

| Path | Owner |
|---|---|
| `tools/**` | worker-backend |
| `.github/**` | Shared (CI): assigned to worker-backend in the brief |
| `Version.props` | Shared: assigned in the brief; `VersionLine` changed only by Carl, `Version` only by the release command after this change |
| `RELEASES.md` | Architect creates it; rows added only by the release command |

## Test strategy

| Level | What | How |
|---|---|---|
| Unit | Line parsing, next version, beta status, tag format and next tag, log parse/insert/escape, version file read/write, release checks | `node:test`, one test per spec scenario it covers, named after it |
| Integration | Release command against a temporary bare remote and clone: every "Release started by Carl" and "Release log" scenario, tag `-N` against remote tags, failure after commit | `node:test`, scripted prompt and fixed clock; real `git` |
| Integration | `check.mjs` against temporary repos: commit on `main` passes; commit only on a branch refused; each refusal message; exit codes | `node:test`, real `git` |
| Integration | No-TTY refusal | Spawn `release.mjs` with piped stdin; expect refusal and no change |
| Unit | Tests that read repository files (`RELEASES.md`, `Version.props`) assert they are valid, never their current content, so a release commit cannot break them (tasks.md 8.3) | `node:test` |
| Build | Package version comes from `Version.props`; no pack warnings | `dotnet pack`, nuspec inspected |
| Observed | `ci.yml` on this change's pull request; `release.yml` rehearsal on Carl's runner; first published release `0.1.0-beta.3` (`0.1.0-beta.1` failed its tool tests, tasks.md 8.3; `0.1.0-beta.2` was attempted as `.Core` and not published, ADR-0010) | Carl's observation, recorded in tasks.md |
| Deferred | "All-platform check" scenario: Carl runs the validation commands by hand on Windows, a Linux machine and macOS per `docs/releasing.md`, one result per OS. Not checked in this change; checked at the pre-1.0 step (PDR-0028), tracked by follow-ups.md item 31 (Carl, 2026-10-19) | Carl's observation at the pre-1.0 step |

Validation commands for every block: `node --test "tools/release/*.test.mjs"`,
`dotnet build Umbraco.Community.Licensing.slnx`,
`dotnet test --solution Umbraco.Community.Licensing.slnx`.

## Risks / Trade-offs

- [Fork pull request edits a workflow to target the self-hosted label] → Approval required for
  all external contributors; Carl reads workflow diffs before approving (ADR-0008).
- [No release runner online] → Job queues, fails after 24 hours; re-run allowed when tests never
  ran (ADR-0008). Releasing guide says so.
- [NuGet login or push fails after tests pass] → Nothing published; re-run allowed while nuget.org
  does not list the version (Q12, ADR-0008). Releasing guide says so. An empty key from login
  fails at a guard step with an explicit message (tasks.md 9.1); happened 2026-10-20 at
  `0.1.0-beta.2`, before nuget.org was set up, as an unrelated push error. Happened again
  2026-10-27 at `0.1.0-beta.3` with nuget.org set up: the push path mixed `\` and `/` on the
  Windows runner and NuGet did not expand the wildcard (tasks.md 10.1). A rehearsal skips the
  push, so only a tag run exercises it.
- [First release fails on an untested runner and burns `0.1.0-beta.1`] → Rehearsal on the runner
  before the first tag (tasks.md 7.1). A burnt number is harmless (PDR-0027). Happened 2026-10-20 for a reason the rehearsal could not see: two tool tests asserted `RELEASES.md` had no rows, so the release commit failed them (tasks.md 8.3). A rehearsal after any release commit runs against a non-empty log and catches this class.
- [Tests fail on Linux or Windows on first run] → `ci.yml` runs every pull request on Linux;
  Carl runs the suite by hand on each OS before 1.0 (PDR-0028). Windows is untested until then
  (all-platform workflow disabled, Carl, 2026-10-18); failures become a follow-up, not hidden. Happened 2026-10-19: the task 7.1 rehearsal on Windows found an off-curve public key rejected with the wrong exception type; fixed by tasks.md 8.1.
- [Trusted Publishing for a package ID that does not exist yet] → Policy is per repository and
  workflow, not per package; if nuget.org refuses the first push, Carl publishes the first version
  from a short-lived API key and records it in follow-ups.md.
- [Push of `main` succeeds, tag push fails] → Command stops and prints the exact `git push`
  command for the tag; the release has not started, nothing is published.
- [Carl's shared release-tag command used here] → Refused by PDR-0029 before the build.
- [A self-hosted runner added to the organisation for other work takes a release] → Runner group
  repository access limits which runners serve this repository; restoring the `licensing-release`
  label is one line (Q3, ADR-0008).
- [Self-hosted runner state between jobs] → Clean checkout per job; caches only.

## Migration Plan

1. Merge this change to `main` after its blocks pass review.
2. Carl does the one-time setup in `docs/releasing.md`: runner, approval setting, Trusted
   Publishing policy owned by `Carlcod-es`, `NUGET_USER` variable `CarlCod_es`.
3. Rehearsal on the runner, then `node tools/release/release.mjs` → `0.1.0-beta.1` (failed its tool
   tests, 2026-10-20). After section 8: rehearsal on `main`, then the release command →
   `0.1.0-beta.2` (attempted as `.Core`, not published). After `rename-to-bare-package-name`
   (ADR-0010): rehearsal on `main`, then the release command → `0.1.0-beta.3`.

Rollback: delete the workflows; restore `Version` in the project file. An already published
version cannot be removed from nuget.org, only unlisted.
