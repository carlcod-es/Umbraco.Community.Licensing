# PDR-0029: A release tag must match its release log row

- Status: Decided
- Date: 2026-10-14
- Source: `add-release-pipeline` design.md Q5; design.md Q11 (Carl, 2026-10-14). Amends PDR-0027.
- Personas: vendor (a tag pushed by mistake does not rebuild or republish an earlier version).
  No persona harmed: a release started with the release command always passes this check.

## Context

PDR-0027 shares the tag format `release-yyyymmdd` with Carl's personal release-tag command, which
tags without bumping the version or adding a log row. Used in this repository by habit, it would
start a release of the version already released. PDR-0027's refusals let that run build and
test, and only the package feed's duplicate check would stop it.

## Decision

- A release is also refused, and nothing is published, when the release log has no row for the
  version, or its row names a tag other than the one that started the release.
- This is a sixth refusal, added to the five in PDR-0027.

## Reasons

- The release command writes the tag into the log row it adds, so its releases always match.
- A tag that did not come from the release command is refused at the start, with a reason that
  names the mismatch, instead of failing late at the feed.
- A tag on a commit whose version has no log row also did not come from the release command.
  Example: Carl's shared release-tag command run before the first release, with version
  `0.1.0-beta.1` and an empty log, would publish `0.1.0-beta.1` with no row or note. The next
  release command would then work out `0.1.0-beta.1` again and fail late at the feed.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep PDR-0027's five refusals; the feed refuses a duplicate | The run builds and tests first and fails with the feed's message, which does not name the cause |
| Use a different tag format for this repository | PDR-0027 chose the shared format; both commands keep it |
| Refuse only a row naming a different tag; allow a missing row | Publishes a version with no log row or note; the next release repeats its number and fails at the feed |

## Consequences

- A release log row edited by hand to name another tag stops that release.
- A release log row removed by hand stops the release of that version.

## Notes

- 2026-10-14 (design.md Q11): extended to a missing row before any code was built on it.
