# PDR-0031: Tool folder, moving and backups

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q4, Q5, Q6, Q13, Q29, Q30, Q31, Q32
- Personas: vendor (knows where keys and records are; survives a new laptop). Site owner: records
  of what they bought survive the vendor's hardware.

## Context

The tool holds private signing keys and customer data on the vendor's own machine. The vendor
must know where, keep it backed up, and take it to a new machine.

## Decision

- **Location.** Setup offers `.ucl` in the vendor's home folder (proposed) or the current
  folder, each as a full path, and warns when the current folder is the home folder itself.
  Setup ends by naming the path, what it holds, and that it must be backed up and kept out of
  folders synced to cloud storage, then the next steps: add a product, then print the public key
  lines its release must trust. The tool guide gives the same first steps (design.md Q31, Carl,
  2026-10-29). Later actions use, in order: a folder named on the command,
  the current folder when it holds an installation, `~/.ucl`. No folder is remembered between
  runs. Every action states the folder it uses. With no installation found, the action refuses
  naming the folders checked and, with a person present, offers to run setup (design.md Q29,
  Carl, 2026-10-29). Setup creates `~/.ucl` when missing and accepts a folder holding other files
  but no installation (design.md Q30, Carl, 2026-10-29). The refused action does not run after
  setup; a folder named on the command that holds no installation is refused with no offer
  (design.md Q32, Carl, 2026-10-29).
- **Self-contained.** Everything the installation holds is in that folder. Copying it to another
  machine and pointing the tool at it keeps every action working, with the same passphrase.
- **Backup export.** A zip of a consistent copy of the installation, with the date taken. No
  passphrase needed; private keys stay encrypted in it; the zip itself is not encrypted.
- **Backup import.** Only into a folder with no installation: named on the command, or the
  current folder or `~/.ucl` offered as at setup; otherwise refused, saying to move
  or rename the current folder. Shows the backup date and warns that licenses issued later are
  not in it. Refuses a file that is not a backup or comes from a newer tool version.
- **One database file.** No database server.

## Reasons

- Desktop and Documents are not offered as presets: they are often synced to cloud storage on
  Windows and macOS, which would send keys and customer data off the vendor's machine without
  their choosing. They can still be typed.
- A hidden home folder is the usual place for developer tools and is not normally synced; the
  closing message covers the risk of forgetting it in backups.
- Copying a folder, or one zip, is the simplest move and backup a vendor can do.
- Import never overwrites, so a restore cannot destroy newer records.

## Rejected options

| Option | Why rejected |
|---|---|
| Desktop, Documents as presets | Often cloud-synced; keys and customer data leave the machine |
| Fixed location | The vendor cannot choose or run a second installation |
| Remembered folder, or a folder environment setting | A second setup or a restore quietly changes which installation later actions use; an environment setting in a shell profile overrides the current folder. Stating the folder on every action covers the wrong-folder risk instead (design.md Q29) |
| Setup only into an empty folder | Refuses a vendor who keeps notes or scripts beside the installation; the home-folder warning covers the main risk (design.md Q30) |
| Start setup when no installation is found | An accidental command would lead into permanent choices; offered, not started (design.md Q29) |
| Run the action after setup | A new installation is empty, so most actions would then refuse; one run, one outcome (design.md Q32) |
| Offer setup for a named folder | Setup takes no folder option and offers only `~/.ucl` or the current folder, so it could not create the installation there; a mistyped folder would lead into setup (design.md Q32) |
| Keys in the OS key store | Cannot be moved by copying the folder |
| SQL Server (FR-2 constraint, dropped by Carl) | The database is not in the folder: moving and zip backup would cover keys and settings only |
| Import replaces after confirmation, with an automatic safety copy | More to specify; renaming the folder first does the same |
| Import merges records | Conflicts need rules; most complex |
| Encrypt the zip | Only private keys need it, and they are encrypted already (PDR-0032) |

## Consequences

- A restored older backup lacks licenses issued after it, which are then missing from a leak
  export (PDR-0035). The guide says to back up after each issuing session.
- A backup taken before a customer email was cleared still holds it (PDR-0033).
