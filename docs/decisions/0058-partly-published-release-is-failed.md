# PDR-0058: A partly published release is a failed release

- Status: Decided
- Date: 2026-11-10
- Source: `add-vendor-tool` design.md Q78 (raised by the section 11 supervisor review). Amends
  PDR-0027.
- Personas: vendor (the tool and the library they install always come from a release that
  published both; one recovery to follow). Implementor: a library version published alone is
  the tested one, so nothing they restored is wrong. Site owner, backoffice editor, site
  visitor: unaffected.

## Context

A release run publishes two packages, the library and the vendor tool, at one version
(release-process). They are pushed one after the other. If the library is pushed and
the tool push then fails, the library at that version is public and the tool is not. A
published version cannot be replaced or pushed again, so running the release again fails as a
duplicate. PDR-0027 covers a release that fails before anything is published, not one that
fails half way.

## Decision

- A release where one package was published and the other was not is a failed release under
  PDR-0027: its number is not reused.
- The package that was published stays published and is not unlisted. The release log row
  stays.
- No Release page is made for it.
- Carl fixes the cause and releases again with the release command, under the next number,
  which publishes both packages.

## Reasons

- It is the same recovery as any other failed release, so there is one rule to follow.
- The library that was published is the tested build; nothing about it is wrong.
- The tool and library stay released together from the next number on.

## Rejected options

| Option | Why rejected |
|---|---|
| Run the release again, skipping the package already published | Changes the release run's rule that a re-run fails on a published version; the tool would be published by a different run from the library |
| Unlist the library version | Unlisting does not remove it; anyone who already restored it is left on a version that looks withdrawn |
| Publish the tool by hand | The published tool would not be the one the release run built and tested |

## Consequences

- A library version can exist on the public package feed with no vendor tool of the same
  version. The vendor guide says to install the latest tool version, which may skip numbers.
- The release guide lists this case in its failure table with this recovery.
