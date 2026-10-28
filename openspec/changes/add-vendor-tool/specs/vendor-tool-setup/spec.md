## Purpose

Lets a small vendor set up one issuing installation of the vendor tool, find it again on every
run and on another machine, and run every action by hand or from their own scripts.

## ADDED Requirements

### Requirement: One vendor and one person per installation
An installation SHALL hold one vendor's issuing setup: one folder, one database, one key prefix,
one signing-key mode. It SHALL have no user accounts or permissions: whoever can run the tool
against the folder can perform every action, and actions that use a private key also need the
passphrase. A vendor needing a second issuing setup SHALL create a second installation in
another folder.

#### Scenario: Second installation
- **WHEN** a vendor with an installation in `~/.ucl` runs setup for a folder `~/acme-issuing`
- **THEN** a second, independent installation is created there, and actions run against either
  folder see only that folder's products, licenses and keys

### Requirement: Guided setup
Setup SHALL run once per installation and SHALL ask, in order, for: the folder, the passphrase
(typed twice, hidden), the key prefix (`LIC` by default, the vendor's own, or none, by the
prefix rule), the signing-key mode (one signing key per product, or one shared across all
products), and whether to create a standby key with each signing key, presented as recommended.
In shared mode setup SHALL create the shared signing key (and its standby key when chosen).
Setup SHALL end by naming the folder's full path, what it holds (private signing keys,
encrypted; license records; customer emails and order references, not encrypted), and that the
folder must be backed up and kept out of folders synced to cloud storage. Prefix and
signing-key mode SHALL NOT change after setup. Setup SHALL refuse a folder that already holds an
installation, changing nothing.

#### Scenario: Setup with defaults
- **WHEN** a vendor runs setup and accepts every proposed value, types a valid passphrase twice
  and keeps the standby key
- **THEN** an installation is created in `~/.ucl` with prefix `LIC`, the chosen signing-key mode
  and standby keys on, and the final message names the full folder path, the content, the
  backup advice and the sync warning

#### Scenario: Invalid prefix
- **WHEN** the vendor types prefix `acme-shop`
- **THEN** setup names the prefix rule and asks again; nothing is created until setup completes

#### Scenario: Passphrases differ
- **WHEN** the two passphrase entries differ
- **THEN** setup says so and asks for both again

#### Scenario: Passphrase too short
- **WHEN** the passphrase is shorter than 12 characters
- **THEN** setup names the minimum length and asks again

#### Scenario: Folder already holds an installation
- **WHEN** setup is run for a folder that already holds an installation
- **THEN** setup refuses, naming the folder, and nothing in it changes

### Requirement: Folder location
Setup SHALL propose `.ucl` in the vendor's home folder, show its full path, and accept any other
folder the vendor types. Every later action SHALL use, in order of precedence: a folder named
on the command, a folder named in the tool's folder environment setting, the folder chosen at
setup or last pointed to, the proposed default. An action run against a folder that holds no
installation SHALL refuse, naming the folder and how to run setup or point the tool at an
installation.

#### Scenario: Chosen folder remembered
- **WHEN** the vendor chose `/Users/acme/issuing` at setup and later runs an action naming no
  folder
- **THEN** the action uses `/Users/acme/issuing`

#### Scenario: Folder named on the command wins
- **WHEN** the vendor names `~/acme-issuing` on a command while the remembered folder is `~/.ucl`
- **THEN** the action uses `~/acme-issuing`

#### Scenario: No installation found
- **WHEN** an action runs and the folder it resolves holds no installation
- **THEN** it refuses, naming the folder, and says to run setup or point the tool at an
  installation

### Requirement: Moving an installation
An installation SHALL keep working after its folder is copied to another folder or machine,
with no other file needed. The vendor SHALL be able to point the tool at a copied folder, and
the tool SHALL then use it as the remembered folder.

#### Scenario: New laptop
- **WHEN** a vendor copies the folder to a new machine with the tool installed and points the
  tool at it
- **THEN** every action works as on the old machine, and actions using a private key accept the
  same passphrase

### Requirement: Running by hand or from scripts
Every action SHALL be runnable with all its inputs given on the command, asking nothing. When an
input is missing and nobody is present to answer, the action SHALL refuse, naming each missing
input, and change nothing. The passphrase SHALL be accepted without a prompt from an environment
setting or from standard input, and never as a plain command argument. The result an action
produces for a script (a key string, a file path, a list) SHALL be written alone to standard
output; messages SHALL go to standard error. An action SHALL exit with success only when it
completed; a refusal SHALL exit with a failure code.

#### Scenario: Scripted issue
- **WHEN** a script runs the issue action with product, expiry and features given, and the
  passphrase in the environment setting, with no terminal attached
- **THEN** the key string alone is written to standard output and the action exits with success

#### Scenario: Missing input with nobody present
- **WHEN** the issue action runs without an expiry and with no terminal attached
- **THEN** it refuses, naming the expiry, writes no key, and exits with a failure code

#### Scenario: Prompted by hand
- **WHEN** the vendor runs the issue action at a terminal without an expiry
- **THEN** the action asks for the expiry
