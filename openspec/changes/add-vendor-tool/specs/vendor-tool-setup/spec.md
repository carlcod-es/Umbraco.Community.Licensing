## Purpose

Lets a small vendor set up one issuing installation of the vendor tool, find it again on every
run and on another machine, and run every action by hand or from their own scripts.

## ADDED Requirements

### Requirement: One vendor and one person per installation
An installation SHALL hold one vendor's issuing setup: one folder, one database, one key prefix
at a time, one signing-key mode. It SHALL have no user accounts or permissions: whoever can run the tool
against the folder can perform every action, and actions that use a private key also need the
passphrase. A vendor needing a second issuing setup SHALL create a second installation in
another folder.

#### Scenario: Second installation
- **WHEN** a vendor with an installation in `~/.ucl` runs setup in `~/acme-practice` and chooses
  the current folder
- **THEN** a second, independent installation is created there; actions run in `~/acme-practice`
  see only its products, licenses and keys, and actions run elsewhere still use `~/.ucl`

### Requirement: Guided setup
Setup SHALL run once per installation, only with a person answering at a terminal, and SHALL ask, in order, for: the folder (Folder location), the passphrase
(at least 15 characters, typed twice, hidden), the key prefix (`LIC` by default, the vendor's own, or none, by the
prefix rule), the signing-key mode (one signing key per product, proposed, or one shared across all
products), and whether to create a standby key with each signing key, presented as recommended.
In shared mode setup SHALL create the shared signing key (and its standby key when chosen).
Setup SHALL end by naming the folder's full path, what it holds (private signing keys,
encrypted; license records; customer emails and order references, not encrypted), and that the
folder must be backed up and kept out of folders synced to cloud storage, and then the next steps:
add a product (in per-product mode, saying this creates its signing key and asks for the
passphrase), then print the public key lines its release must trust. The signing-key mode
SHALL NOT change after setup; the prefix MAY (Changing the prefix). Setup SHALL refuse a folder that already holds an
installation, changing nothing. Setup SHALL take no answer from options, environment settings or
redirected input; run with nobody present to answer, it SHALL refuse, saying setup is run by a
person at a terminal, and create nothing.

#### Scenario: Setup with defaults
- **WHEN** a vendor runs setup and accepts every proposed value, types a valid passphrase twice
  and keeps the standby key
- **THEN** an installation is created in `~/.ucl` with prefix `LIC`, one signing key per product
  and standby keys on, and the final message names the full folder path, the content, the
  backup advice, the sync warning and the next steps

#### Scenario: Invalid prefix
- **WHEN** the vendor types prefix `acme-shop`
- **THEN** setup names the prefix rule and asks again; nothing is created until setup completes

#### Scenario: Passphrases differ
- **WHEN** the two passphrase entries differ
- **THEN** setup says so and asks for both again

#### Scenario: Passphrase too short
- **WHEN** the passphrase is shorter than 15 characters
- **THEN** setup names the minimum length and asks again

#### Scenario: Folder already holds an installation
- **WHEN** setup is run for a folder that already holds an installation
- **THEN** setup refuses, naming the folder, and nothing in it changes

#### Scenario: Setup with nobody present
- **WHEN** setup is run by a script with no terminal attached, or told not to ask
- **THEN** it refuses, saying setup is run by a person at a terminal, creates nothing, and exits
  with a failure code

### Requirement: Changing the prefix
The vendor SHALL show the installation's prefix and change it to `LIC`, their own or none, by the
prefix rule. A change SHALL need no passphrase and SHALL state that new issues and reissues use
the new prefix and existing licenses keep theirs until reissued. Existing records SHALL NOT
change. A reference typed after the change SHALL be read under the prefix rule with the new
prefix: the earlier prefix is refused, the 10-character form is accepted.

#### Scenario: Prefix changed
- **WHEN** the vendor of an installation with prefix `LIC` and license `LIC-8F3AK-M7RXB` changes
  the prefix to `ACME` and then issues a license
- **THEN** the new key identifier starts `ACME-`, the record of `LIC-8F3AK-M7RXB` is unchanged,
  and a reissue of it starts `ACME-8F3AK-M7RXB-`

#### Scenario: Earlier prefix typed
- **WHEN** after the change to `ACME` the vendor shows `LIC-8F3AK-M7RXB`
- **THEN** the reference is refused naming the configured prefix `ACME`; showing `8F3AK-M7RXB`
  finds the license

#### Scenario: Invalid prefix on change
- **WHEN** the vendor changes the prefix to `acme-shop`
- **THEN** the change is refused naming the prefix rule, and the prefix is unchanged

### Requirement: Folder location
Setup SHALL offer two folders, each shown as a full path: `.ucl` in the vendor's home folder,
proposed, and the current folder. When the current folder is the home folder itself, setup SHALL
warn that the installation's files would sit loose in it. Setup SHALL create `.ucl` when it does
not exist, and SHALL accept a chosen folder that holds other files but no installation. Every later action SHALL use the first
of: a folder named on the command, the current folder when it holds an installation, `.ucl` in
the home folder. The tool SHALL remember no folder between runs. Every action that uses an
installation SHALL state the folder's full path on standard error. When no installation is
found, the action SHALL refuse, naming the folders it checked; with a person present it SHALL
then offer to run setup, and start it only when the person accepts.

#### Scenario: Default folder created
- **WHEN** setup runs on a machine with no `~/.ucl` and the vendor accepts the proposed folder
- **THEN** `~/.ucl` is created and holds the installation

#### Scenario: Current folder with other files
- **WHEN** setup runs in `~/acme-issuing`, which holds other files but no installation, and the
  vendor chooses the current folder
- **THEN** the installation is created there and the other files are left untouched

#### Scenario: Current folder used
- **WHEN** the vendor runs an action in `~/acme-practice`, which holds an installation, while
  `~/.ucl` holds another
- **THEN** the action uses `~/acme-practice` and states its path

#### Scenario: Home installation used elsewhere
- **WHEN** the vendor runs an action in a folder holding no installation while `~/.ucl` holds one
- **THEN** the action uses `~/.ucl` and states its path

#### Scenario: Folder named on the command wins
- **WHEN** the vendor names `~/acme-practice` on a command run in a folder holding another
  installation
- **THEN** the action uses `~/acme-practice`

#### Scenario: No installation found
- **WHEN** an action runs at a terminal where neither the current folder nor `~/.ucl` holds an
  installation
- **THEN** it refuses, naming both folders, and offers to run setup; setup starts only if the
  vendor accepts

#### Scenario: No installation found with nobody present
- **WHEN** a script runs an action and no installation is found
- **THEN** it refuses, naming the folders checked, offers nothing, and exits with a failure code

### Requirement: Moving an installation
An installation SHALL keep working after its folder is copied to another folder or machine,
with no other file needed. The vendor uses a copied folder by running actions in it, by naming
it on the command, or by placing it at `.ucl` in the home folder.

#### Scenario: New laptop
- **WHEN** a vendor copies the folder to `~/.ucl` on a new machine with the tool installed
- **THEN** every action works as on the old machine, and actions using a private key accept the
  same passphrase

### Requirement: Running by hand or from scripts
Every action except setup (Guided setup) SHALL be runnable with all its inputs given on the
command, asking nothing. When an
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
