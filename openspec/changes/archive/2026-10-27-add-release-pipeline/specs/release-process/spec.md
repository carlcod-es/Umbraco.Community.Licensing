## Purpose

Publishes the library so that every published version is exactly the code that was built and
tested from `main`, is numbered from a version line held in the repository, and is listed in a
release log that tells vendors what changed.

## ADDED Requirements

### Requirement: Release started by Carl with the release command
A release SHALL start only from the release command held in the repository, run by Carl at an
interactive prompt. The command SHALL refuse, changing nothing, when no person is present to
answer its prompts, when the current branch is not `main`, or when local `main` and the remote
`main` differ. Otherwise it SHALL, in order: work out the next version, ask for a one-line
"what changed" note, show the version, tag and note and ask for confirmation, then write the
version to the version file, add the release log row, commit those two files to `main`, push
`main`, create the tag and push the tag. Declining the confirmation SHALL change nothing.

#### Scenario: Release started
- **WHEN** Carl runs the release command on an up-to-date `main` with line `0.1-beta` and a
  release log whose highest beta row is `0.1.0-beta.2`, types the note "Adds reissue" and confirms
- **THEN** the version file holds `0.1.0-beta.3`, the release log gains a row for
  `0.1.0-beta.3` with the tag, today's date and "Adds reissue", one commit holding only those
  two files is pushed to `main`, and the tag is pushed on that commit

#### Scenario: No person present
- **WHEN** the release command is started by an automated process with no interactive prompt
- **THEN** it refuses, saying a release is started only by Carl, and changes nothing

#### Scenario: Not on main
- **WHEN** the release command is run on any branch other than `main`
- **THEN** it refuses, naming the branch, and changes nothing

#### Scenario: Out of step with the remote
- **WHEN** local `main` has commits the remote lacks, or lacks commits the remote has
- **THEN** it refuses, saying which, and changes nothing

#### Scenario: Empty note
- **WHEN** Carl enters an empty note, or a note of whitespace only
- **THEN** the command asks for the note again

#### Scenario: Confirmation declined
- **WHEN** Carl declines the confirmation
- **THEN** no file, commit or tag is changed or created

### Requirement: Version numbering from the version line
The version file SHALL hold a version line and the current version. The release command SHALL
work out the next version from the line and the release log alone, with no online lookup:

- A beta line `0.M-beta` gives `0.M.0-beta.N`, with N one more than the highest N in the log for
  that line, or 1 when the log has none.
- A release line `X`, X at least 1, gives `X.N.0`, with N one more than the highest N in the log
  for that major version, or 0 when the log has none.

Carl changes the line by hand. Any other line SHALL be refused by the command, changing nothing.

#### Scenario: First beta
- **WHEN** the line is `0.1-beta` and the release log has no rows
- **THEN** the next version is `0.1.0-beta.1`

#### Scenario: Numbers continue past a failed attempt
- **WHEN** the line is `0.1-beta` and the log holds `0.1.0-beta.1` (published) and
  `0.1.0-beta.2` (failed)
- **THEN** the next version is `0.1.0-beta.3`

#### Scenario: First release on line 1
- **WHEN** the line is `1` and the log holds only beta rows
- **THEN** the next version is `1.0.0`

#### Scenario: Next release on line 1
- **WHEN** the line is `1` and the highest row for major version 1 is `1.4.0`
- **THEN** the next version is `1.5.0`

#### Scenario: New major version
- **WHEN** the line is `2` and the log holds rows up to `1.7.0`
- **THEN** the next version is `2.0.0`

#### Scenario: Invalid line
- **WHEN** the line is `0.1`, `1-beta`, `1.0`, `0` or `abc`
- **THEN** the command refuses, naming the line and the two accepted forms, and changes nothing

### Requirement: Release tag
The release command SHALL tag the release commit `release-yyyymmdd` with the local date. When a
tag for that date already exists locally or on the remote, it SHALL add `-2`, `-3` and so on,
using the lowest free number.

#### Scenario: First release of the day
- **WHEN** no tag starting `release-20261020` exists and Carl releases on 2026-10-20
- **THEN** the tag is `release-20261020`

#### Scenario: Third release of the day
- **WHEN** `release-20261020` and `release-20261020-2` exist
- **THEN** the tag is `release-20261020-3`

### Requirement: Release log
The repository SHALL hold a release log with one row per release attempt: version, tag, date
and a one-line "what changed" note for vendors. The newest row SHALL come first. A row SHALL stay
when its release fails, and its version number SHALL NOT be reused, so the log can show gaps.
The release command SHALL refuse, before committing, when the next version already appears in
the log.

#### Scenario: Row added
- **WHEN** a release of `0.1.0-beta.1` is started on 2026-10-20 with tag `release-20261020` and
  note "First beta"
- **THEN** the log's first row reads `0.1.0-beta.1`, `release-20261020`, `2026-10-20`,
  "First beta"

#### Scenario: Failed release keeps its row
- **WHEN** the release of `0.1.0-beta.2` fails its tests
- **THEN** its row stays in the log and the next release is `0.1.0-beta.3`

#### Scenario: Version already in the log
- **WHEN** the log already holds a row for the version the command works out
- **THEN** the command refuses, naming the version, and changes nothing

### Requirement: Release run publishes only tested packages
Pushing a release tag SHALL start a release run. The run SHALL take the version only from the
version file at the tagged commit, build and test that commit once, and publish one package,
for every operating system, to the public package feed only when every check passes. The
published package SHALL carry that version and name the tagged commit. A release whose checks
fail SHALL publish nothing, and the run SHALL report which check failed. Carl SHALL be able to
rehearse a release run on one of his test machines without a tag: it builds, tests and packages
the chosen commit and publishes nothing.

#### Scenario: Successful release
- **WHEN** the release command pushes tag `release-20261020` on a commit whose version file holds
  `0.1.0-beta.1` and every check passes
- **THEN** package version `0.1.0-beta.1` built from that commit is published, listed as a
  prerelease

#### Scenario: Tests fail
- **WHEN** one test fails during the release run
- **THEN** nothing is published and the run reports the failing test

#### Scenario: Rehearsal
- **WHEN** Carl starts a rehearsal on `main`
- **THEN** the build, tests and packaging run on one of his test machines and nothing is
  published

### Requirement: Release refusals
A release run SHALL refuse, publishing nothing and naming the reason, when any of these holds:

1. the tag does not match `release-yyyymmdd`, with an optional `-N` suffix where N is 2 or more;
2. the tagged commit is not on `main`;
3. the version appears more than once in the release log;
4. the version and the line disagree on beta status: a version below 1.0 is not a prerelease, or
   a version of 1.0 or above is a prerelease, or the version does not belong to the line;
5. the release log has no row for the version, or its row names a different tag;
6. the build or tests fail.

Checks 1 to 5 SHALL run before the build.

#### Scenario: Malformed tag
- **WHEN** tag `release-2026-10-20` or `release-20261020-1` is pushed
- **THEN** the run refuses, naming the tag format

#### Scenario: Commit not on main
- **WHEN** a release tag is pushed on a commit that exists only on a feature branch
- **THEN** the run refuses, saying the commit is not on `main`

#### Scenario: Duplicate version in the log
- **WHEN** the release log holds two rows for `0.1.0-beta.2`
- **THEN** the run refuses, naming the version

#### Scenario: Beta status disagrees
- **WHEN** the version file holds line `1` and version `1.0.0-beta.1`, or line `0.1-beta` and
  version `0.1.0`
- **THEN** the run refuses, naming the version and the line

#### Scenario: Tag pushed without the release command
- **WHEN** tag `release-20261021` is pushed on a commit whose log row for its version names
  `release-20261020`
- **THEN** the run refuses, naming both tags

#### Scenario: Tag with no log row
- **WHEN** tag `release-20261015` is pushed on a commit whose version file holds
  `0.1.0-beta.1` and whose release log has no row for `0.1.0-beta.1`
- **THEN** the run refuses, naming the version and saying a release starts with the release command

### Requirement: Change checks
Every pull request to `main`, and every push to `main`, SHALL be built and fully tested, and the
result SHALL be shown on the pull request or commit. A pull request SHALL NOT run on Carl's
own machines. A pull request from someone without write access SHALL be checked only after
Carl approves the run.

#### Scenario: Pull request checked
- **WHEN** a contributor opens a pull request to `main`
- **THEN** after any required approval, the build and all tests run and the pull request shows
  pass or fail

#### Scenario: Push to main checked
- **WHEN** a commit is pushed to `main`
- **THEN** the build and all tests run and the commit shows pass or fail

### Requirement: Test coverage per release
Each release SHALL be built and tested once, on whichever of Carl's test machines is available,
and the release run SHALL report the operating system it tested on. Carl SHALL be able to run
the full test suite on Windows, Linux and macOS at any time, with a result per operating system
and nothing published. The README SHALL state, in one line, before 1.0: "Runs on Windows, Linux
and macOS. Each release is tested on one of them; all three will be checked before 1.0."

#### Scenario: Release reports its test platform
- **WHEN** a release run completes its tests
- **THEN** its output names the operating system the tests ran on

#### Scenario: All-platform check
- **WHEN** Carl starts the all-platform check
- **THEN** the full suite runs on Windows, Linux and macOS, each reports pass or fail, and
  nothing is published

#### Scenario: README states coverage
- **WHEN** a vendor reads the README before 1.0
- **THEN** it says each release is tested on one of Windows, Linux and macOS, and all three will
  be checked before 1.0
