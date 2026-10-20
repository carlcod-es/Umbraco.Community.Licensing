## Why

No package has been published. The version is typed by hand in the project, a package can be
built from any working copy, committed or not, and the library has been tested only on macOS
(`add-license-core` follow-ups, moved to `docs/feature-requests.md` FR-6). A vendor installing a
published version needs it to be exactly the code that was tested, numbered per PDR-0024 (0.x
and prerelease until 1.0), with a note saying what changed. The FR-6 explore settled the product
rules (PDR-0027, PDR-0028); this change builds them.

## What Changes

- **Release command** held in the repository, started only by Carl. It sets the next version
  from the version line, adds a row to the release log with a "what changed" note Carl types,
  commits and pushes to `main`, and tags the commit `release-yyyymmdd` (`-2`, `-3` for repeats
  that day) (PDR-0027).
- **Version line and version file** in the repository: `0.1-beta` gives `0.1.0-beta.1`,
  `0.1.0-beta.2`, ...; line `1` gives `1.0`, `1.1`, ... Carl changes the line by hand. No version
  is looked up online.
- **Release log** listing every release attempt: version, tag, date, what changed. A failed
  attempt keeps its row and its number.
- **Release run** on each release tag: takes the version from the tagged commit, builds and
  tests once on whichever of Carl's test machines is available, and publishes the one package to
  the public package feed only if every check passes (PDR-0027, PDR-0028).
- **Refusals**: nothing is published when the tagged commit is not on `main`, the build or tests
  fail, the version appears more than once in the log, the version and the line disagree on beta
  status, the tag does not match the format, or the log has no row for the version, or its row
  names a different tag (PDR-0027, PDR-0029).
- **Change checks**: every pull request to `main` and every push to `main` is built and tested.
  Pull requests from anyone are checked without running on Carl's machines.
- **All-platform check on demand**: Carl can run the full test suite on Windows, Linux and macOS
  at any time. Required once before 1.0 (PDR-0028); the first run also settles the
  `add-license-core` follow-ups left open for Windows and Linux. The all-platform workflow is kept
  but disabled; Carl runs the suite by hand on each operating system (Carl, 2026-10-18).
- **README** states which operating systems each release is tested on (PDR-0028).
- **Releasing guide** for Carl: one-time setup and the release steps.
- **First release**: started by Carl after the change is built. `0.1.0-beta.1` failed
  before publishing and `0.1.0-beta.2` was attempted as `.Core` and not published (2026-10-20);
  the first published release is
  `0.1.0-beta.3` under `Umbraco.Community.Licensing` (PDR-0027, ADR-0010).

## Capabilities

### New Capabilities

- `release-process`: starting a release, version numbering from the version line, the release
  log, the release refusals, publication of tested packages, change checks on pull requests and
  `main`, test coverage per release and the on-demand all-platform check.

### Modified Capabilities

None. The library's behaviour does not change.

## Impact

- New: release command and its tests, version file, release log, two active workflows
  (change checks, release) and a disabled all-platform workflow, releasing guide.
- Changed: the library project takes its version from the version file instead of a hand-typed
  value; README gains the per-release testing line; on Windows, parsing a public key whose point is not on the curve fails with the documented format error (found in the release rehearsal).
- Records: PDR-0029 (new, amends PDR-0027); ADR-0008 and ADR-0009 (new); ADR-0006 amended by
  ADR-0009. PDR-0027 and PDR-0028 source lines name this change's design.md Q-numbers.
  `docs/feature-requests.md` FR-6 is taken into this change.
- Outside the repository, done by Carl: register his test machines for releases, allow the
  release run to publish to the package feed, and require approval before workflows run for
  outside contributors.
- Personas: vendor (published package matches tested code, each release says what changed);
  implementor (told which operating systems each release was tested on); site owner protected
  (betas stay prereleases, PDR-0024).
