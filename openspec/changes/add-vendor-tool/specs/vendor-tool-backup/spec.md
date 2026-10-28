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

### Requirement: Backup import
The vendor SHALL import a backup zip into a folder that holds no installation. The tool SHALL
refuse a folder that holds one, saying to move or rename the current folder first, and change
nothing. Before importing it SHALL show the date the backup was taken and warn that licenses
issued after that date are not in it. A zip that is not a complete backup, or that was taken by a
newer tool version than the one running, SHALL be refused, changing nothing. After import the
tool SHALL use the imported folder as the remembered folder, and actions using a private key
SHALL accept the passphrase that was in effect when the backup was taken.

#### Scenario: Restore to a new machine
- **WHEN** the vendor imports `ucl-2026-10-28.zip` into `~/.ucl` on a machine with no
  installation
- **THEN** the tool shows the backup date and the warning, then creates the installation, and
  every product, license and key from the backup is there

#### Scenario: Folder already holds an installation
- **WHEN** the vendor imports into a folder holding an installation
- **THEN** the tool refuses, says to move or rename that folder first, and nothing changes

#### Scenario: Not a backup
- **WHEN** the vendor imports a zip that is not a backup made by the tool
- **THEN** the tool refuses, saying the file is not a backup, and nothing is created

#### Scenario: Backup from a newer tool version
- **WHEN** the backup was taken by a newer tool version than the one running
- **THEN** the tool refuses, naming both versions and saying to update the tool
