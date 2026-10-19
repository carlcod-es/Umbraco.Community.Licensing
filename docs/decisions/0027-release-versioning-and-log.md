# PDR-0027: Release versioning and release log

- Status: Amended by PDR-0029
- Date: 2026-10-14
- Source: FR-6 explore (Carl, 2026-10-14); `add-release-pipeline` design.md Q1
- Personas: vendor (a published version always matches what was tested, and each release says
  what changed). Site owner protected: betas stay marked as prereleases, so PDR-0024's guidance
  against issuing beta keys to paying customers stays visible.

## Context

The package version was typed by hand in the project, and a package could be built from any
working copy, committed or not (`add-license-core` follow-ups, moved to FR-6). A vendor installing
a published version needs it to be exactly the code that was tested, with a version that follows
PDR-0024: 0.x and prerelease until 1.0.

## Decision

- **Scope.** Releases are built, tested and published to the public package feed. The first
  published release is a beta, `0.1.0-beta.1`.
- **Start.** Carl starts a release with a release command held in this repository. It is never
  started by Claude or by anyone else. The command:
  1. bumps the version in a repository file;
  2. adds a row to the release log;
  3. commits and pushes to `main`;
  4. tags the commit `release-yyyymmdd`, with `-2`, `-3` for repeat releases on the same day.
- **Version source.** The release takes its version only from the repository file at the tagged
  commit. No version number is looked up online.
- **Numbering.** The file holds a version line, for example `0.1-beta` or `1`. The command adds
  the next number:
  - beta line: `0.1.0-beta.1`, `0.1.0-beta.2`, ...
  - release line `1`: `1.0`, `1.1`, `1.2`, ...
  - Carl changes the line by hand: to `1` when going to 1.0 (PDR-0024), and to the next major
    version for a breaking API change.
- **Release log.** A document in the repository lists every release attempt: version, tag, date
  and a short "what changed" note for vendors. The release command asks Carl for the note.
- **Failed release.** A release whose build or tests fail is not published. Its log row stays and
  its number is not reused, so the log can show gaps. The public package feed shows what was
  published.
- **Refusals.** A release is refused and nothing is published when:
  1. the tagged commit is not on `main`;
  2. the build or tests fail;
  3. the version appears more than once in the release log. The release command checks this
     before committing, and the release checks it again;
  4. the version and the line disagree on beta status. A version below 1.0 must be a
     prerelease, and a version of 1.0 or above must not be;
  5. the tag does not match `release-yyyymmdd`, with an optional `-N` suffix.

## Reasons

- All version information lives in repository files. A release can be traced and repeated
  without depending on any outside record.
- Carl makes every commit. The release itself only reads the repository, so the local copy is
  never behind after a release.
- The beta line makes every release a beta until Carl decides otherwise, so PDR-0024 holds
  without a manual step at each release.
- Building from the tagged commit on `main` rules out packages built from uncommitted work.
- Vendors need to know when a beta changed the key format and keys need reissuing (PDR-0024).
  The "what changed" note tells them.
- The repository's own release command travels with the repository, so releasing works on any
  machine Carl uses, and his other repositories are unaffected.

## Rejected options

| Option | Why rejected |
|---|---|
| Carl names the version at each release | Works, but the version would live outside the repository |
| Version edited by hand in the project before each release (as before) | Easy to forget; the package can fall out of step with the release record |
| Version fully generated from a date or counter | Cannot express beta versus 1.0 (PDR-0024) |
| Next number counted from earlier published releases | Needs an online lookup; Carl wants all version information in repository files |
| The release run writes the new number back to the repository | Adds commits to `main` that are not Carl's, and the local copy falls behind |
| Check for a duplicate version against the public package feed | An online lookup; the release log covers it, and the feed still refuses a true duplicate |
| Remove the failed release's log row so the number can be reused | Adds a manual step after a failure; a gap is harmless |
| Add the version bump to Carl's personal release-tag command | That command is shared across repositories and is not present on every machine |

## Consequences

- Post-1.0 releases have no patch numbers; a fix and a feature both bump the minor number.
- A breaking API change ships as a minor release if Carl forgets to change the line.
- Carl's personal release-tag command is unchanged. Both share the tag format.
- FR-6 is closed by the change that implements this.
