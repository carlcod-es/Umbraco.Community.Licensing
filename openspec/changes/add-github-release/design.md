## Context

See proposal.md (Why). Current state: `.github/workflows/release.yml` is one job, `release`, on a
self-hosted runner, with workflow-level `permissions: { contents: read, id-token: write }`
(ADR-0008). It checks, tests, builds, packs into `${{ runner.temp }}/packages`, and on a tag push
logs in to nuget.org and pushes `*.nupkg` from that folder. Release tooling lives in
`tools/release/` as Node.js ES modules with no npm packages, tested with `node:test`
(ADR-0009). `release-rules.mjs` already parses `Version.props` (`readVersionFile`) and
`RELEASES.md` (`parseLog`, `findRow`). The remote holds no `release-*` tags after the history
reset. Product rules: PDR-0037. Behaviour: `specs/release-process/spec.md` (Release page for
each published release).

## Bar

**Production.** The job runs on every published release and writes to the public repository
page. Body composition, package ID extraction, prerelease marking and the exists/create/fail
handling have automated tests. The workflow wiring is verified by observed runs (see Test
strategy).

## Goals / Non-Goals

**Goals:** a Release for every published version, never for an unpublished one; write access
held only by the job that needs it; re-runnable without publishing again.

**Non-Goals:** Releases for earlier versions; attached files; editing or deleting existing
Releases; changes to the release command, the checks or the push.

## Requirements

| R | Requirement | Spec requirement | Personas | Records |
|---|---|---|---|---|
| R1 | Release on the release tag, titled by version, note and package links, prerelease below 1.0 | Release page for each published release | Vendor; site owner (beta marking stays visible) | PDR-0037, PDR-0024 |
| R2 | Created only after publishing; none for failed attempts or rehearsals | Release page for each published release | Vendor | PDR-0037 |
| R3 | Failure reported; re-run creates it without publishing; existing Release left unchanged | Release page for each published release | Vendor (Carl as maintainer) | PDR-0037, ADR-0016 |

Persona check: no requirement harms a higher-priority persona.

## Questions settled in this change

| Q | Question | Answer | Record |
|---|---|---|---|
| Q1 | Which version marker (FR-10 issue 1) | A GitHub Release on the existing release tag; no second tag (Carl, 2026-10-28) | PDR-0037 |
| Q2 | Release content | Version as title; release log note and one nuget.org link per published package; no attached files (Carl, 2026-10-28) | PDR-0037 |
| Q3 | Release creation fails after publishing (FR-10 issue 3) | Created by the run after the push; failure reported; re-runnable without publishing again; existing Release unchanged (Carl, 2026-10-28) | PDR-0037, ADR-0016 |
| Q4 | Order against `add-vendor-tool` | This change first, before `0.1.0-beta.5` (Carl, 2026-10-28) | none (scheduling) |
| Q5 | How the Release is created | Node script in `tools/release/` calling the GitHub REST API with `fetch`; package IDs from the packed file names (Architect) | ADR-0016 |
| Q6 | Where write access lives, and how the re-run avoids a duplicate push | Separate GitHub-hosted job with `contents: write` only; **Re-run failed jobs** re-runs it alone; no `--skip-duplicate` (Architect) | ADR-0016, amends ADR-0008 |

FR-10 issue 2 (a second tag must not start a run) does not arise: no second tag.

## Decisions

### Separate job with its own permissions (ADR-0016)

`github-release` runs after `release` succeeds, on a tag push only, on `ubuntu-latest`, with
`permissions: { contents: write }`. `release` declares `contents: read, id-token: write` at job
level; the workflow level drops to `contents: read`. The write token never reaches Carl's runner
or the build and test steps. Carl's answer to Q3 offered `--skip-duplicate` as the way to
re-run; a separate job gives the same outcome (re-run without publishing) while keeping
ADR-0008's rule that a duplicate push fails. Alternatives in ADR-0016.

### Script, not `gh` or a third-party action (ADR-0016)

`tools/release/github-release.mjs`, Node 22, no npm packages, two commands:

- `package-ids <folder>`: reads the version from `Version.props` at the repository root (as
  `create` does; the workflow has no version to pass), lists `*.nupkg` in the folder, strips
  `.<version>.nupkg`,
  prints the IDs space-separated, and appends `package-ids=<ids>` to `$GITHUB_OUTPUT` when set.
  Fails when the folder has no package for the version, or a file does not end with the version.
- `create <tag> <ids>`: reads `Version.props` and `RELEASES.md` from the repository root,
  finds the row for the version (fails naming the version if none, or naming both tags if the
  row names a different tag), builds the body, then
  GET-or-POST against `GITHUB_API_URL` / `GITHUB_REPOSITORY` with `GITHUB_TOKEN`.

Pure functions (body, IDs from file names, prerelease from version, request decision from a
response) are exported and tested; the HTTP call takes an injectable `fetch` so the
exists/created/failed paths are tested without network.

Body format (Markdown):

```
<note>

- [Umbraco.Community.Licensing 0.1.0-beta.5](https://www.nuget.org/packages/Umbraco.Community.Licensing/0.1.0-beta.5)
```

Prerelease: the version has a `-` prerelease part. Below 1.0 every version is a prerelease
(PDR-0024, enforced by the release checks), so this matches "below 1.0" in the spec.

### Workflow wiring

- `release` job: `outputs: { package-ids: ${{ steps.package-ids.outputs.package-ids }} }`; new
  step `id: package-ids` after **Pack** and before **NuGet login**, on every run including
  rehearsals (no `if`), run from the workspace root (no
  `working-directory`; the script finds `Version.props` from the current checkout) with the pack
  folder as its argument: `package-ids "${{ runner.temp }}/packages"`. Placed before publishing so a failure publishes
  nothing and every rehearsal exercises it; `needs: release` already keeps `github-release` from
  running unless the push succeeded (revised 2026-10-28, section 2 supervisor).
- `github-release` job: `needs: release`, `if: github.event_name == 'push' && github.ref_type == 'tag'`,
  checkout with `persist-credentials: false`, setup-node 22, then
  `create "$TAG" "$IDS"` with step `env` `GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}`,
  `TAG: ${{ github.ref_name }}`, `IDS: ${{ needs.release.outputs.package-ids }}`. Expressions
  go through `env`, never into the command, in the job that holds the write token (bash on
  `ubuntu-latest`).
- Steps are single `node` commands, as in ADR-0008.

## Test strategy

- `node --test "tools/release/*.test.mjs"` (CI and every release run) covers: body for one and
  two packages; prerelease for `0.1.0-beta.5` and not for `1.0.0`; package IDs from file names,
  including a dotted ID and a mismatched version; missing log row; existing Release (no POST,
  success); 404 then POST with the expected payload; POST failure reported with status and
  message; spec scenario names as test names.
- Workflow verified by observation: a rehearsal shows `github-release` skipped; the
  `0.1.0-beta.5` release shows the Release page with title, note, link and prerelease marking.
  The failure and re-run path is covered by tests, not provoked live.

## Risks / Trade-offs

- [Carl uses "Re-run all jobs" after a Release failure] → the push fails as a duplicate and the
  Release job is skipped. Nothing wrong is published. `docs/releasing.md` names **Re-run failed
  jobs**.
- [Default `GITHUB_TOKEN` permissions are read-only] → the `carlcod-es` organisation sets the
  default to read-only, and the repository setting is disabled (Carl, 2026-10-28). That setting
  is only the default: a job's `permissions` key still grants `contents: write` (GitHub docs,
  "Setting the permissions of the GITHUB_TOKEN for your repository"). No setting change is
  needed, and the read-only default stays for every other job. If the job still gets a 403, it
  fails with GitHub's message; fallback options are in ADR-0016.
- [Hosted runner outage] → the Release job queues or fails; the package is already published;
  re-run the job later.
- [`add-vendor-tool` section 11 edits the same workflow] → this change lands first; its brief for
  section 11 must keep the `package-ids` step before **NuGet login**, the job output and the
  `github-release` job, and the install-and-run step must write no `.nupkg` into the pack folder.
  `add-vendor-tool` gets that note through `opsx:update` after this change is archived.

## Migration Plan

Lands before `0.1.0-beta.5`. Carl runs a rehearsal, then releases `0.1.0-beta.5`. No
repository or organisation setting changes. Rollback: remove the job and step;
existing Releases stay.
