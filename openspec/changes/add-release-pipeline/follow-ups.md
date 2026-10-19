# Follow-ups: add-release-pipeline

Minors and nits from reviews. Read at the start of each apply session.

## Handover (2026-10-19)

- Branch `feature/release-pipeline`. Sections 1–4 and the section 5 workflows committed
  (f4a422f). Staged, awaiting Carl's commit: plan revision disabling `all-platforms.yml` (Carl,
  2026-10-18; item 31), task 5.3 (reviewer-approved) and the section 5 supervisor re-review fixes.
  Section 5 supervisor-approved 2026-10-19.
- tasks.md: 13/20 ticked. Tasks 5.1 and 5.2 left unticked: acceptance is Carl observing runs on
  GitHub (deferred to PO). Local validation: 94 tool tests pass; dotnet build 0 warnings;
  dotnet test 816 passed, 1 skipped.
- Action pins (looked up 2026-10-15, all `node24`): checkout v7.0.1 `3d3c42e5`, setup-dotnet
  v6.0.0 `a98b5685`, setup-node v7.0.0 `82076278` (v7.1.0 of 2026-10-08 skipped, < 2 weeks),
  NuGet/login v1.2.0 `8d196754`.
- Deviation accepted: `tools/release/platform.mjs` (+ test) added in section 5 to print the
  ADR-0008 platform line with a single `node` command.
- Carl 2026-10-18: item 14 keep; item 29 yes (optional tag ruleset step in `docs/releasing.md`).
- Release runner: organisation-wide runner MINIDEV; needs label `licensing-release` and the
  runner group to allow this repository (public repositories allowed).
- Verifying 5.2 needs `release.yml` on `main` (`workflow_dispatch` lists only default-branch
  workflows); 5.1 can be observed on this change's pull request. Option: accept 5.2 during 7.1.
- Carl 2026-10-19: PR #2 merged to `main` (1fe4c08); CI run passed. `add-release-pipeline` task 5.1 met, ticked (14/20). Task 5.2: `release.yml` now on `main`; accept during task 7.1 rehearsal.
- 2026-10-19: task 6.1 done and ticked (15/20). Task 6.2 `docs/releasing.md` written; acceptance is Carl following it in task 7.1, so unticked. Task 6.3 waits for task 7.2.
- Next was: section 6 (Architect) when Carl says go. Task 6.2 must cover items 5, 23, 26, 28, 29,
  30, 31. Then section 7 (Carl).

| # | Source | Severity | Item | Status |
|---|---|---|---|---|
| 1 | Section 1 supervisor | nit | `RELEASES.md` intro is two sentences; `add-release-pipeline` task 1.2 says one. Content accurate. | Open; leave unless Carl wants it trimmed |
| 2 | Section 1 supervisor | risk | No `.gitattributes`: a Windows checkout with `core.autocrlf=true` gives `RELEASES.md` and `Version.props` CRLF. Section 3 parser/writer must accept `\r\n` and keep the file's line ending; tests cover it. Alternative (`.gitattributes` with `eol=lf`) needs a new ownership row and Carl's call. | Carl 2026-10-14: handled in code, no `.gitattributes`. Folded into section 3 brief |
| 3 | Section 2 supervisor | risk | `Version.props` holds `<Version>` and `<VersionLine>`; the `add-release-pipeline` task 3.4 writer must match `<Version>` exactly, not as a prefix. Round-trip test covers it. | Folded into section 3 brief |
| 4 | Section 2 supervisor | risk | First release writes an unchanged `Version.props` (already `0.1.0-beta.1`); the commit still holds the new `RELEASES.md` row. `add-release-pipeline` task 4.2 "Release started" tests cover the empty-log case. | Folded into section 4 brief |
| 5 | Section 2 supervisor | nit | Local packs carry a released version; `docs/releasing.md` (`add-release-pipeline` task 6.2) says never push a local pack. | Done: `docs/releasing.md` (task 6.2) |
| 6 | Section 3 reviewer | nit | `tools/release/release-rules.test.mjs:331`, `:338`: "Scenario: Duplicate version in the log" and "Scenario: Malformed tag" each appear twice (rule level and `checkRelease`). Suffix the rule-level one (e.g. "(rule)") so a failure names one test. | Done (section 3, renamed "(rule)") |
| 7 | Section 3 reviewer | nit | `readVersionFile` does not skip XML comments; a commented-out `<Version>` counts as a duplicate and is refused. Fails safe. | Open |
| 8 | Section 3 reviewer | note | `nextTag` uses the `Date`'s local fields; section 4 passes `new Date()` (or the injected clock), never a UTC-shifted value. Section 3 supervisor (minor): the row date (`yyyy-mm-dd`) has no helper; section 4 adds `localDate(date)` to `release-rules.mjs`, used by `nextTag` and the row, and tests a clock near midnight. Test clocks use local-time constructors, not `Z` strings. | Folded into section 4 brief |
| 9 | Section 3 supervisor | minor | `.gitignore`: stock `[Rr]elease/` ignored ADR-0009 `tools/release/`; `!/tools/release/` added by the Architect in section 3 so the tooling is committed and CI sees it. | Done (section 3) |
| 10 | Section 4 block B reviewer | minor | `release.mjs` main-module guard silently exits 0 through a symlink (same defect as block A's major); Ctrl-D during a prompt exits 13 with no message; multi-line note rejected only after confirmation. | Done (block B2) |
| 11 | Section 4 block B reviewer | nit | `release.test.mjs:322-343`: main-push-rejected test does not assert the hook's text in the message (state assertions already prove the hook ran). | Open |
| 12 | Section 4 block B reviewer | nit | `release.test.mjs`: no test with other files already staged before the release (`git commit --only` covers it by design). | Open |
| 13 | Section 4 block B reviewer | nit | `release.test.mjs:210-250`: loop cleanup removes `work` and `remote.git` by name; `afterEach` cleanup would be cleaner. | Open |
| 14 | Section 4 block A reviewer | minor | Platform line is `os.type() os.release() os.arch()` (kernel versions, e.g. `Darwin 25.6.0`, `Linux 6.8.0-azure`), meets the spec; friendlier names (e.g. `RUNNER_OS`) would be a PO call. | Carl 2026-10-18: keep as is |
| 15 | Section 4 block A reviewer | note | `release.yml` (`add-release-pipeline` task 5.2) needs `actions/checkout` with `fetch-depth: 0` so `origin/main` exists, and runs `node tools/release/check.mjs "${{ github.ref_name }}"` from the repo root. | Folded into section 5 brief |
| 16 | Section 4 block A re-review, block B2 review | minor | The "unnormalised path" CLI tests in `check.test.mjs` and `release.test.mjs` cannot fail: Node resolves `process.argv[1]` before the script runs, so `..` never reaches the guard (B2 reviewer probed it; passes on the pre-fix guard too). The symlink tests cover the real defect. Drop the `..` tests or rename them as spawn-path smoke tests. | Open |
| 17 | Section 4 block B2 review | nit | `release.mjs` `questioner`: after Ctrl-D the shell prompt may land on the "Cancelled. Nothing changed." line; print a leading newline. | Open |
| 18 | Section 4 supervisor | minor | `release.mjs` read the clock twice (tag before prompts, row date after confirm): tag and row date could straddle midnight. | Done (block B3) |
| 19 | Section 4 supervisor | nit | `release.mjs` used `cwd` as the repo root; from a subdirectory it failed with a raw Node error. Tests did not clear inherited `GIT_DIR`/`GIT_INDEX_FILE`/`GIT_WORK_TREE`. | Done (block B3) |
| 20 | Section 4 supervisor | nit | `isMainModule` duplicated word for word in `check.mjs` and `release.mjs`; change both together. | Open |
| 21 | Section 4 supervisor | risk | `check.mjs` does not compare HEAD with the tag's commit; `release.yml` (`add-release-pipeline` task 5.2) must check out the tag ref. A `git rev-parse <tag>^{commit}` = HEAD reason is a candidate for a later change. | Folded into section 5 brief |
| 22 | Section 4 supervisor | note | `setup-node` must pin Node 22+ (`node --test` glob needs 21+); quote the glob so Node, not the shell, expands it. | Folded into section 5 brief |
| 23 | Section 4 supervisor | note | `git fetch origin --tags` refuses with git's raw "would clobber existing tag" when a local tag differs from the remote; safe (before any write). `docs/releasing.md` (`add-release-pipeline` task 6.2) explains it. | Done: `docs/releasing.md` (task 6.2) |
| 24 | Section 5 reviewer | minor | `release.yml:36`: `${{ github.ref_name }}` is interpolated into the run line; a tag like ``release-$(…)`` would run in the shell before `check.mjs` refuses it. Only writers push tags. Hardening: pass the tag through `env:` and read it in `check.mjs` (changes its CLI contract). | Open; candidate for a later change |
| 25 | Section 5 reviewer | minor | `release.yml:53`: `NUGET_API_KEY` interpolated into the push command line. Masked and short-lived; an `env:` reference differs between bash and pwsh. | Accepted as is |
| 26 | Section 5 Architect / reviewer | note | All four pinned actions run on `node24`: the self-hosted runner needs a runner version that supports it; Git on PATH. | Done: `docs/releasing.md` (task 6.2) |
| 27 | Section 5 worker | nit | On a tag push the platform line prints twice (platform step and `check.mjs`). Platform step also covers the rehearsal. | Open |
| 28 | Section 5 supervisor | minor | `release.yml:25-28`: setup-dotnet installs to the machine-wide dotnet directory by default; a runner service account (Windows: NETWORK SERVICE) usually cannot write there. `docs/releasing.md` runner prerequisites: set `DOTNET_INSTALL_DIR` in the runner's `.env` to a writable directory, or preinstall the SDK `global.json` resolves to. Task 7.1 rehearsal would catch it. | Done: `docs/releasing.md` (task 6.2) |
| 29 | Section 5 supervisor | nit | Any writer can push a `release-*` tag; `check.mjs` is the gate. A GitHub tag ruleset restricting `release-*` creation to Carl would also cover most of item 24. Repository setting: Carl's call. | Carl 2026-10-18: yes, optional setup step. Done: `docs/releasing.md` (task 6.2) |
| 30 | Section 5 supervisor | note | `docs/releasing.md` says a rehearsal (`workflow_dispatch`) never publishes, whichever ref it runs on; re-running a failed tag run re-checks against current `origin/main` and still passes. | Done: `docs/releasing.md` (task 6.2) |
| 31 | Carl 2026-10-18 | note | `all-platforms.yml` disabled (`add-release-pipeline` task 5.3 revised). The `add-license-core` Windows/Linux follow-ups (signing-key import cost, off-curve key rejection), the Windows/macOS run of the release tool tests and the spec scenario "All-platform check" stay open until Carl runs the suite by hand on Windows, a Linux machine and macOS. Deferred to the pre-1.0 step (PDR-0028; Carl, 2026-10-19). | Open; pre-1.0 |
| 32 | Section 5 supervisor re-review | risk | Windows and macOS have no CI run; an OS-specific failure may first show on the release runner and burn a version number (PDR-0027). Running the suite by hand on MINIDEV's OS before the task 7.1 rehearsal reduces it; the rehearsal itself also catches it before any tag. | Open; Carl's call |
| 33 | Section 6 reviewer | major | `docs/releasing.md` allowed a re-run after a NuGet login or push failure with tests passed; ADR-0008 only allowed one when tests never ran. | Carl 2026-10-19: re-run allowed. design.md Q12, ADR-0008 revised; guide cites ADR-0008 |
| 34 | Section 6 reviewer | minor | `docs/releasing.md`: pre-test re-run row now excludes Release check and says a workflow fix needs a new release; catch-all row for unlisted release-run reasons; cancel message described as Ctrl-D only; `Declined. Nothing changed.` named; where to record pre-1.0 results named. | Done |
| 35 | Section 6 reviewer | note | Ctrl-C at a release command prompt is untested; confirm what it prints during task 7.1 and add it to the guide if it differs. | Open; task 7.1 |
