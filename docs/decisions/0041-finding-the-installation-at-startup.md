# PDR-0041: Finding the installation at startup

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q35. Amends PDR-0031. Startup menu moved to
  `docs/feature-requests.md` FR-23.
- Personas: vendor (always knows which installation an action uses; fewer ways to point at the
  wrong one). Site owner (primary): keys and records land in the vendor's intended installation.
  Implementor: unaffected.

## Context

PDR-0031 finds the installation from, in order, a folder named on the command, the current
folder when it holds an installation, and `.ucl` in the home folder, and offers setup when none
is found. Carl reviewed startup in guided mode (PDR-0039) and settled what the tool does when
launched, when two installations are present, after setup started from the offer, and whether a
folder can be named on the command (Carl, 2026-11-08).

## Decision

- **Folders checked.** Only the current folder, then `.ucl` in the home folder (PDR-0038). A
  folder cannot be named on the command; to use another installation, the vendor runs the tool
  from its folder (amends PDR-0031).
- **Folder reported.** Every action states the full path of the installation it uses. When the
  current folder is used and `.ucl` in the home folder also holds an installation, the action
  notes that one as not used.
- **None found.** The action refuses, naming the folders checked, and offers setup (PDR-0031).
  Setup started this way ends after its closing message with a line telling the vendor to run
  their command again, without naming the command. The command first given does not run.
- **No command.** `ucl` run with no command lists the commands. It checks no folder and offers
  no setup. A startup menu is FR-23.
- **Backup import.** The target is the current folder or `.ucl` in the home folder, offered as at
  setup; no target is named on the command (amends PDR-0031).

## Reasons

- Two places to look, both stated, are easier to reason about than three; a vendor sees exactly
  which installation they are in from where they ran the tool.
- Naming the unused installation catches issuing from a practice installation, or the reverse,
  which cannot be undone because records are never deleted (PDR-0033).
- A fresh installation has no products, so continuing the original command after setup would
  almost always refuse again. The closing message already gives the next steps (PDR-0031). The
  retry line is generic so it stays correct whatever was typed.
- Listing the commands is the usual behaviour of a command-line tool and keeps the first version
  small; a menu suits guided mode but needs its own design (FR-23).

## Rejected options

| Option | Why rejected |
|---|---|
| Folder named on the command (PDR-0031) | Carl chose two places only; changing folder does the same |
| Say nothing about the unused installation | The path line alone is easy to skim past |
| Run the original command after setup | Usually refuses again on an empty installation; chains two flows |
| Retry line naming the command | Carl chose a generic instruction |
| Startup check and menu when launched with no command | Larger first version; FR-23 |

## Consequences

- Restoring a backup to another folder means creating that folder and running the import from
  inside it.
- The tool guide explains the two folders, the note on an unused installation, and changing into
  a folder to use another installation.
