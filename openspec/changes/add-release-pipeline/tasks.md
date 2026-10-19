Owners per CLAUDE.md and design.md (Ownership): sections 2 to 5 worker-backend; sections 1 and 6
Architect; section 7 Carl. Validation for every worker block:
`node --test "tools/release/*.test.mjs"`, `dotnet build Umbraco.Community.Licensing.slnx`,
`dotnet test --solution Umbraco.Community.Licensing.slnx`. Bar: production (design.md). Test
names follow spec scenario names. Spec: `specs/release-process/spec.md`. Records: PDR-0027,
PDR-0028, PDR-0029, ADR-0008, ADR-0009.

## 1. Ownership and release log file

Depends on: none. Owner: Architect.

- [x] 1.1 Add the design.md Ownership rows (`tools/**`, `.github/**`, `Version.props`, `RELEASES.md`) to the CLAUDE.md ownership table, and verify the four rows are present and match design.md
- [x] 1.2 Create `RELEASES.md` with a heading, one sentence on what the log holds (every release attempt, newest first, failed attempts kept) and the empty table header from ADR-0009, and verify by reading it against the ADR-0009 format (parsed by tests in 3.3)

## 2. Version file

Depends on: 1. Owner: worker-backend. Shared files assigned in the brief: `Version.props`, the
library project file. Records: ADR-0009, ADR-0006.

- [x] 2.1 Create `Version.props` (`VersionLine` `0.1-beta`, `Version` `0.1.0-beta.1`, per ADR-0009), import it in `src/Umbraco.Community.Licensing.Core/Umbraco.Community.Licensing.Core.csproj` with a forward-slash path and remove the project's own `Version`, and verify `dotnet pack` reports no warnings, the nuspec shows version `0.1.0-beta.1` and repository `https://github.com/carlcod-es/Umbraco.Community.Licensing`, and the test project is unaffected

## 3. Release rules module

Depends on: 1, 2. Owner: worker-backend. Path: `tools/release/release-rules.mjs` and its
`node:test` file. Node 22+, ES modules, no npm packages (ADR-0009). Spec requirements: Version
numbering from the version line, Release tag, Release log, Release refusals.

- [x] 3.1 Implement line parsing (`0.M-beta`, M ≥ 1; `X`, X ≥ 1; no leading zeros), next version from line and log versions, and the beta-status/belongs-to-line check, and verify by tests for every "Version numbering from the version line" scenario and the "Beta status disagrees" scenario
- [x] 3.2 Implement the tag format check (`release-yyyymmdd`, optional `-N`, N ≥ 2) and next tag from a local date and existing tag names, and verify by the "First release of the day", "Third release of the day" and "Malformed tag" scenarios
- [x] 3.3 Implement release log parsing (rows of the first table, newest first), row insert at the top with `|` escaped and single-line notes enforced, version occurrence count and row lookup by version, and verify by the "Row added", "Failed release keeps its row", "Version already in the log" and "Duplicate version in the log" scenarios, and a test that the repository's `RELEASES.md` parses with zero rows
- [x] 3.4 Implement `Version.props` read (line, version) and version write that leaves every other byte unchanged, and verify by tests including a round trip of the repository's `Version.props`
- [x] 3.5 Implement the release check over tag, version file and log (PDR-0027 refusals 1, 3, 4; PDR-0029) returning every reason, and verify by the "Malformed tag", "Duplicate version in the log", "Beta status disagrees" and "Tag pushed without the release command" and "Tag with no log row" scenarios, plus a passing case

## 4. Release command and release-run check

Depends on: 3. Owner: worker-backend. Paths: `tools/release/release.mjs`,
`tools/release/check.mjs` and their `node:test` files. Git through `execFileSync('git', [...])`
only (ADR-0009). Spec requirements: Release started by Carl with the release command, Release
refusals, Test coverage per release (platform report).

- [x] 4.1 Implement `check.mjs` (tag from its argument; the 3.5 check on the checked-out files; "on `main`" by `git merge-base --is-ancestor HEAD origin/main`; prints OS, version and architecture; exit 1 listing every reason), and verify by tests against temporary Git repositories with a bare remote: commit on `main` passes, the "Commit not on main" scenario, each 3.5 refusal reaches the exit code, and the platform line is printed
- [x] 4.2 Implement `release.mjs` with exported `runRelease({ cwd, prompt, now })` following the ADR-0009 command flow, and verify by integration tests against a temporary bare remote and clone for the "Release started", "Not on main", "Out of step with the remote" (ahead and behind), "Empty note", "Confirmation declined", "Invalid line" and "Third release of the day" (tags on the remote only) scenarios, each asserting files, commits and tags afterwards; the "Version already in the log" refusal cannot be reached end to end (the next version is always above every log row), so `runRelease` keeps it as a guard before writing, in an exported rules function verified by unit tests alongside the 3.3 test
- [x] 4.3 Add the CLI entry to `release.mjs` that refuses unless stdin and stdout are a TTY, and verify by a test that spawns it with piped stdin, expecting the "No person present" refusal, a non-zero exit and no change to the repository
- [x] 4.4 Make a failure after the release commit (push of `main` or of the tag) stop with the state and the exact `git` command to finish by hand, with no retry or force, and verify by an integration test where the remote rejects the tag push

## 5. Workflows

Depends on: 2, 4. Owner: worker-backend. Shared paths assigned in the brief: `.github/workflows/`.
Actions pinned by full commit SHA with a version comment, latest stable at least two weeks old;
dependencies `actions/checkout`, `actions/setup-dotnet`, `actions/setup-node`, `NuGet/login`
approved with this change (ADR-0008). Spec requirements: Change checks, Release run publishes
only tested packages, Release refusals, Test coverage per release.

- [x] 5.1 Add `ci.yml` per ADR-0008 (pull requests to `main` and pushes to `main`; `ubuntu-latest`; `contents: read`; platform line; tool tests; Release build; `dotnet test --solution`), and verify by Carl's observation that this change's pull request shows the check passing (deferred to PO until pushed)
- [ ] 5.2 Add `release.yml` per ADR-0008 (tag `release-*` and `workflow_dispatch`; runs-on `self-hosted` (label `licensing-release` dropped, Carl 2026-10-19); full-history checkout; `check.mjs` and publish only on a tag push; tool tests; Release build with `ContinuousIntegrationBuild`; test; pack; `NuGet/login` with `vars.NUGET_USER`; `dotnet nuget push` without `--skip-duplicate`; `contents: read`, `id-token: write`; `concurrency: release`), and verify by Carl's observation of a rehearsal run on his runner: tests pass, the platform line names his machine, a package is produced and nothing is published (deferred to PO)
- [x] 5.3 Disable `all-platforms.yml` (ADR-0008, Carl 2026-10-18): add `if: false` to the `build-test` job with a comment that it was deliberately disabled and Carl runs the full suite manually on each OS when needed, and verify by reading the file: the job has `if: false`, the comment is present, nothing else changed

## 6. Documentation

Depends on: 4, 5. Owner: Architect.

- [x] 6.1 Add the PDR-0028 pre-1.0 line to `README.md` verbatim, and a link to `RELEASES.md`, and verify the line matches PDR-0028 character for character
- [ ] 6.2 Write `docs/releasing.md`: one-time setup (register a self-hosted runner available to this repository, and its prerequisites; "Require approval for all external contributors"; nuget.org Trusted Publishing policy owned by the nuget.org organisation `Carlcod-es` for `release.yml`; `NUGET_USER` repository variable `CarlCod_es`, per ADR-0008), rehearsal, the release command, changing the line, what each refusal means, failed releases and when a re-run is allowed, the pre-1.0 all-platform step (Carl runs the validation commands by hand on Windows, Linux and macOS; `all-platforms.yml` is disabled, dispatching it gives a skipped run, not a check, and how to re-enable it; the "All-platform check" scenario is checked at the pre-1.0 step per PDR-0028, not in this change, tracked by follow-ups.md item 31); link it from the README Documentation table, and verify by Carl following it in 7.1
- [ ] 6.3 Update `docs/feature-requests.md` FR-6 status to closed by this change once 7.2 passes, and verify the row names the change and the first published version

## 7. First release

Depends on: 5, 6, 8, and this change merged to `main`. Owner: Carl.

- [ ] 7.1 Carl completes the `docs/releasing.md` one-time setup and runs a rehearsal on `main`, and verify by Carl's observation that it passes on his runner
- [ ] 7.2 Carl runs `node tools/release/release.mjs` with note "First beta", and verify by Carl's observation that `RELEASES.md` has the `0.1.0-beta.1` row, the release run passes, and nuget.org lists `Umbraco.Community.Licensing.Core` `0.1.0-beta.1` as a prerelease owned by `Carlcod-es`, whose repository commit is the tagged commit

## 8. Windows fix from the rehearsal

Depends on: none. Owner: worker-backend. Path: `src/Umbraco.Community.Licensing.Core/Signing/SigningPublicKey.cs`.
Found in the task 7.1 rehearsal on MINIDEV (Windows), 2026-10-19; approved by Carl, 2026-10-19.

- [ ] 8.1 Make `SigningPublicKey.FromSpki` turn `PlatformNotSupportedException` from the platform import into `FormatException`, as it does `CryptographicException` (Windows CNG throws `PlatformNotSupportedException` for a point not on the curve; `SigningPrivateKey` already catches both), and verify `SigningKeyTests.PublicKeyParse_RejectsPointNotOnCurve` passes unchanged on Linux and on Carl's Windows runner in the task 7.1 rehearsal
