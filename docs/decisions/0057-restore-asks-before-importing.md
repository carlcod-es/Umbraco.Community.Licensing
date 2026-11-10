# PDR-0057: Restore asks before importing

- Status: Decided
- Date: 2026-11-09
- Source: `add-vendor-tool` design.md Q76. Amends PDR-0031.
- Personas: vendor (sees the backup date and the warning before anything is created, and can
  stop). Site owner: a restore taken in error, which would lose licenses issued since the backup
  from the vendor's records, is less likely. Implementor, backoffice editor, site visitor:
  unaffected.

## Context

A restore shows the date the backup was taken and warns that licenses issued after that date
are not in it (PDR-0031). The specs did not say whether the vendor can stop after reading the
warning; the section 10 apply of `add-vendor-tool` raised it.

## Decision

- After the backup date and the warning, restore asks `Restore this backup? [y/N]`, no
  proposed.
- No, or a cancel, creates nothing, changes nothing, and returns to where the restore was
  started: the startup offer, the Backup menu or the Settings menu.
- Yes imports the backup as before.

## Reasons

- A warning the vendor cannot act on is only a notice; the date may show the wrong file was
  chosen.
- No is proposed because the restore is the step that cannot be undone from the menu, and the
  backup's licenses are older than the vendor may expect.

## Rejected options

| Option | Why rejected |
|---|---|
| Show the warning and import with no question | The vendor cannot act on the warning |
| Yes proposed | An Enter typed out of habit would import a backup the vendor did not check |

## Consequences

- The guide shows the restore question and says no leaves everything as it was.
