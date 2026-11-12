## Purpose

Lets the vendor take periodic backups of an installation as one zip file and restore one into a
new folder, so a lost or replaced machine does not lose signing keys or records.

## ADDED Requirements

### Requirement: Backup export
The vendor SHALL export the installation to a zip file at a path they name. The zip SHALL hold a
consistent copy of everything in the installation, taken while no action is changing it, and the
date and time it was taken. Private keys SHALL stay encrypted in the zip; the zip itself SHALL
NOT be encrypted. The export SHALL NOT need the passphrase.

#### Scenario: Backup taken
- **WHEN** the vendor exports a backup to `~/Backups/ucl-2026-10-28.zip`
- **THEN** the zip is written, the tool reports its path and time, and no passphrase is asked

### Requirement: Files named by the vendor
The tool SHALL NOT replace an existing file. When a file it would write exists, it SHALL say so,
showing the full path, and ask for another name. This applies to the backup export, the export by
signing key and a key string saved to a file. Setup creating `.ucl` in the home folder is
unchanged.

#### Scenario: Backup file already exists
- **WHEN** the vendor exports a backup to `~/Backups/ucl-2026-10-28.zip`, which exists
- **THEN** the tool says the file exists, showing its full path, asks for another name, and the
  existing file is unchanged

### Requirement: Missing folder for a written file
When the folder of a file the tool would write does not exist, the tool SHALL say so, showing
that folder's full path, and ask whether to create it, proposing yes. Yes SHALL create the folder,
with every missing folder above it, and write the file; no SHALL ask for the path again and
create nothing; `/cancel` SHALL end the action and create nothing. This applies to every file
named in *Files named by the vendor*.

#### Scenario: Backup folder missing, created
- **WHEN** the vendor exports a backup to `~/Backups/2026/ucl-2026-11-12.zip`, `~/Backups/2026`
  does not exist, and the vendor answers yes when asked to create it
- **THEN** the tool shows the folder's full path, creates `~/Backups/2026` (and `~/Backups` if
  missing), writes the zip and reports its path

#### Scenario: Backup folder missing, declined
- **WHEN** the vendor exports a backup to `~/Dropbx/ucl-2026-11-08.zip`, `~/Dropbx` does not
  exist, and the vendor answers no when asked to create it
- **THEN** the tool asks for the path again and creates no folder

### Requirement: Paths typed by the vendor
A path the vendor types SHALL be read from the current folder when it is relative. A typed path
that is `~` alone, or `~` followed by a path separator (`/`, and on Windows also `\`), SHALL
stand for the home folder (vendor-tool-setup: Folder location). Any other `~` SHALL be part of a
name. The tool SHALL still show every path in full, never with `~`. This applies to the backup
export, the export by signing key, a key string saved to a file and the backup import.

#### Scenario: Home folder typed as ~ on macOS or Linux
- **WHEN** user `carl` on macOS exports a backup to `~/Backups/ucl-2026-11-12.zip`
- **THEN** the zip is written to `/Users/carl/Backups/ucl-2026-11-12.zip` and the tool reports
  that full path

#### Scenario: Home folder typed as ~ on Windows
- **WHEN** user `carl` on Windows exports a backup to `~\Backups\ucl-2026-11-12.zip`
- **THEN** the zip is written to `C:\Users\carl\Backups\ucl-2026-11-12.zip` and the tool reports
  that full path

#### Scenario: Tilde in a name
- **WHEN** the vendor, in `~/acme`, imports `~backups/ucl.zip`
- **THEN** the tool reads `~/acme/~backups/ucl.zip`, a folder named `~backups` in the current
  folder

### Requirement: Files read by the tool
When a file the tool would read does not exist, or the path is a folder, the tool SHALL say so,
showing the full path, and ask again. This applies to the backup import.

#### Scenario: Import file not found
- **WHEN** the vendor imports `~/Backups/ucl-2026-11-01.zip`, which does not exist
- **THEN** the tool says the file does not exist, showing its full path, asks again, and nothing
  is created

### Requirement: Backup import
The vendor SHALL import a backup zip into a folder that holds no installation. From the startup
offer when no installation is found, the current folder or `.ucl` in the home folder SHALL be
offered as at setup (vendor-tool-setup: Folder location); from the menu, only the current folder
(vendor-tool-setup: New installation in the current folder). After import the session SHALL
continue with the imported installation. The tool SHALL
refuse a folder that holds one, saying to move or rename the current folder first, and change
nothing. Before importing it SHALL show the date the backup was taken and warn that licenses
issued after that date are not in it, then ask whether to restore, with no proposed; a no SHALL
change nothing and return to where the restore was started. A backup SHALL import on any supported operating system,
whichever one it was taken on. A zip that is not a complete backup, or that was taken by a
newer tool version than the one running, SHALL be refused, changing nothing. After import,
actions using a private key SHALL accept the passphrase that was in effect when the backup was taken.

#### Scenario: Restore to a new machine
- **WHEN** the vendor imports `ucl-2026-10-28.zip` into `~/.ucl` on a machine with no
  installation
- **THEN** the tool shows the backup date and the warning, then creates the installation, and
  every product, license and key from the backup is there

#### Scenario: Restore declined
- **WHEN** the vendor answers no after the backup date and the warning
- **THEN** nothing is created, nothing changes, and the startup offer or menu the restore was
  started from is shown

#### Scenario: Restore on another operating system
- **WHEN** the vendor imports a backup taken on Windows into `~/.ucl` on macOS
- **THEN** the installation is created with every product, license and key from the backup, and
  actions using a private key accept the passphrase in effect when the backup was taken

#### Scenario: Folder already holds an installation
- **WHEN** the vendor imports into a folder holding an installation
- **THEN** the tool refuses, says to move or rename that folder first, and nothing changes

#### Scenario: Not a backup
- **WHEN** the vendor imports a zip that is not a backup made by the tool
- **THEN** the tool refuses, saying the file is not a backup, and nothing is created

#### Scenario: Backup from a newer tool version
- **WHEN** the backup was taken by a newer tool version than the one running
- **THEN** the tool refuses, naming both versions and saying to update the tool
