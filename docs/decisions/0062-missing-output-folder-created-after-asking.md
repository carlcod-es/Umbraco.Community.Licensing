# PDR-0062: A missing output folder is created after asking

- Status: Decided
- Date: 2026-11-12
- Source: `add-vendor-tool` design.md Q83 (Carl, 2026-11-12, with Q82). Amends PDR-0045.
- Personas: vendor (exports a backup into `~/Backups/2026/` without leaving the tool to make the
  folder; a typo is still shown before anything is created). Site owner (primary): an existing
  backup or export is still never replaced. Implementor, backoffice editor, site visitor:
  unaffected.

## Context

PDR-0045 refused to create a missing folder for a file the tool writes, so a typo such as
`~/Dropbx` was caught. The vendor then had to leave the session or another terminal to create a
folder they meant, for example a new folder per year of backups. Three actions write a file the
vendor names: the backup export, the export by signing key and a key string saved to a file.

## Decision

- When the folder of a file the tool would write does not exist, the tool shows that folder's
  full path, says it does not exist, and asks `Create it? [Y/n]`.
- Yes (or Enter) creates the folder, with every missing folder above it, and writes the file.
  No asks for the path again and creates nothing. `/cancel` ends the action and creates nothing.
- An existing file is still never replaced (PDR-0045). A path that is a folder is still asked
  again.
- The backup import is unchanged: a file the tool reads is never created.

## Reasons

- Making a folder for backups or exports is part of the vendor's task; the tool should not send
  them elsewhere to do it.
- Showing the full path of the missing folder before creating it keeps PDR-0045's guard against a
  typo: one keystroke declines it.
- Yes is proposed because the vendor typed the path on purpose; a typo is the exception.

## Rejected options

| Option | Why rejected |
|---|---|
| Never create a folder (PDR-0045 as it was) | Sends the vendor out of the session for an ordinary step |
| Create without asking, then say so | A typo leaves a stray folder, possibly inside a synced location |
| Create silently | The same, and the vendor is not told |

## Consequences

- If the write fails after the folder was created, the new folder stays; the vendor is told the
  write failed as for any other write error.
- The vendor guide describes the question.
