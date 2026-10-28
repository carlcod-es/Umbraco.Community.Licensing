## Why

A vendor reading the repository's GitHub Releases page finds nothing: releases are marked only by
`release-yyyymmdd` tags, which do not name the version (FR-10). A vendor holding a version number
from nuget.org cannot find its source, note or date from the repository page. Taken up now, ahead
of `add-vendor-tool`, so `0.1.0-beta.5` is the first release with a Releases entry (Carl,
2026-10-28).

## What Changes

- Each published release gets a GitHub Release on the release tag the release command already
  pushes. No second tag.
- Title: the version. Body: the release log note and a nuget.org link for each published
  package. Marked as a prerelease below 1.0.
- Created only after every package is published. A failed release attempt gets no Release.
- If creating the Release fails after publishing, the run reports the failure and can be re-run
  to create the missing Release without publishing again. Re-running when the Release already
  exists changes nothing.
- A rehearsal creates no Release.
- `docs/releasing.md` describes the Release, the new failure case and its re-run.
- Not included: Releases for versions published before this change (their tags were removed
  with the history reset), and attaching package files.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-process`: adds a GitHub Release for each published release, with its content,
  prerelease marking, failure handling and re-run.

## Impact

- Release workflow and release tooling: a new step after publishing, and broader repository
  permissions for that step only.
- Release documentation and the FR-10 status row.
- No library, API or package change. The packages published are unchanged.
- `add-vendor-tool` section 11 also changes the release workflow. It lands after this change,
  and its tool package then appears in the Release body with no further change.
- Personas: vendor (finds the version, note and source of any release from the repository).
  No persona is harmed: site owners and implementors see the same prerelease marking as on
  nuget.org.
