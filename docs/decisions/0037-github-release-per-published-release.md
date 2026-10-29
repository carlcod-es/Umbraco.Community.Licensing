# PDR-0037: A GitHub Release for each published release

- Status: Decided
- Date: 2026-10-28
- Source: `add-github-release` design.md Q1, Q2, Q3; FR-10. Builds on PDR-0027, PDR-0029.
- Personas: vendor (finds the version, note and source of a release from the repository). No
  persona harmed: the prerelease marking matches nuget.org, so PDR-0024's beta warning stays
  visible to site owners and implementors.

## Context

A release is marked only by its `release-yyyymmdd` tag, which does not name the version
(PDR-0027). A vendor holding a version number from nuget.org cannot find its source or note from
the repository page.

## Decision

- **Form.** Each published release gets a GitHub Release attached to the release tag that started
  it. No second tag per version (Carl, 2026-10-28).
- **Content.** Title: the version. Body: the release log note for the version and one nuget.org
  link per published package, naming the version. No package files attached (Carl, 2026-10-28).
- **Prerelease.** Marked as a prerelease exactly when the version is below 1.0 (PDR-0024).
- **When.** Created by the release run only after every package is published. A failed attempt
  and a rehearsal get no Release, so every Release names a version nuget.org lists.
- **Failure after publishing.** The run reports the failure; the packages stay published. Carl
  re-runs the Release step alone, which creates the Release without publishing again. A Release
  that already exists for the tag is left unchanged (Carl, 2026-10-28).
- **Earlier versions.** No Release for versions published before this decision; their tags were
  removed with the repository history reset. `0.1.0-beta.5` (tag `release-20261028`) was released
  before the workflow change and has no Release; the first is `0.1.0-beta.6` (Carl, 2026-10-28).

## Reasons

- The release tag already names the tested commit. A Release on it adds the version and note
  without a second tag that could be mistaken for a release start (PDR-0029).
- nuget.org stays the single download. Attached files would duplicate it with no checksum gain.
- Creating the Release after publishing means a Release never points at an unpublished version.
- A re-run that does not publish again avoids the duplicate-version failure on nuget.org, so a
  transient failure needs no new version number.

## Rejected options

| Option | Why rejected |
|---|---|
| Version tag (`v0.1.0-beta.5`) as well as the Release | A second tag per commit; the Release already shows the version |
| Version tag only, no Release | Shows no note or package link |
| Attach the `.nupkg` files | A second download source for the same file |
| Release created by the release command, before the run | A failed attempt would leave a Release for an unpublished version |
| A failed Release step fails the release; Carl creates it by hand | Recommended at first; Carl chose a re-runnable step |
| Update an existing Release on re-run | Would overwrite edits Carl made on GitHub |

## Consequences

- The release documentation gains the Release, its failure case and the re-run.
- Editing a Release on GitHub does not change the release log; the log stays the record.
- FR-10 is closed by the change that implements this.
