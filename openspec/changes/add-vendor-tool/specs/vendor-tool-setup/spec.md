## Purpose

Lets a small vendor set up one issuing installation of the vendor tool, find it again on every
run and on another machine, and run every action in guided mode: a person at a terminal, with
the tool asking for whatever the command does not give. Scripted mode, with nobody present, is
not offered.

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
folder must be backed up and kept out of folders synced to cloud storage, naming a few such
services as examples (for instance OneDrive, iCloud Drive or Dropbox) without presenting them as
a complete list, and then the next steps:
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
  backup advice, the sync warning with example services and the next steps

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
- **WHEN** setup is run by a script with no terminal attached
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
of: the current folder when it holds an installation, `.ucl` in the home folder. No other folder
SHALL be used, and a folder SHALL NOT be named on the command. The tool SHALL remember no folder
between runs. Every action that uses an installation SHALL state the folder's full path on
standard error; when the current folder is used and `.ucl` in the home folder also holds an
installation, the action SHALL also note that one, saying it is not used. When no installation
is found, the action SHALL refuse, naming the folders it checked, then offer to run setup, and
start it only when the person accepts. Setup started this way SHALL end after its closing
message, followed by a line telling the vendor to run their command again, without naming the
command; the command first given SHALL NOT run. Run with no command, the tool SHALL list its
commands and check no folder. The home folder SHALL be the
user's home folder as the operating system defines it (on Windows, the user's profile folder).
Every path the tool shows SHALL be the full path in the operating system's own form, never
abbreviated with `~`. In this spec, `~/.ucl` and `~/<name>` stand for `.ucl` and `<name>` in the
home folder.

#### Scenario: Default folder on Windows
- **WHEN** user `carl` runs setup on Windows with no `.ucl` in the profile folder
- **THEN** setup proposes `C:\Users\carl\.ucl`, shown in full, and on acceptance creates it and
  names that path in the closing message

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
- **THEN** the action uses `~/acme-practice`, states its path, and notes that `~/.ucl` also
  holds an installation that is not used

#### Scenario: Home installation used elsewhere
- **WHEN** the vendor runs an action in a folder holding no installation while `~/.ucl` holds one
- **THEN** the action uses `~/.ucl` and states its path

#### Scenario: No installation found
- **WHEN** an action runs at a terminal where neither the current folder nor `~/.ucl` holds an
  installation
- **THEN** it refuses, naming both folders, and offers to run setup; setup starts only if the
  vendor accepts

#### Scenario: Setup accepted from the prompt
- **WHEN** the vendor runs the issue action with no installation found and accepts the offer to
  run setup
- **THEN** setup runs, ends with its closing message and a line telling the vendor to run their
  command again, and no license is issued

#### Scenario: Launched with no command
- **WHEN** the vendor runs the tool with no command
- **THEN** it lists its commands, checks no folder and offers no setup

### Requirement: Moving an installation
An installation SHALL keep working after its folder is copied to another folder or machine,
including a machine running another of the supported operating systems (Windows, macOS, Linux),
in either direction, with no other file needed. The vendor uses a copied folder by running actions in it or by
placing it at `.ucl` in the home folder.

#### Scenario: New laptop
- **WHEN** a vendor copies the folder to `~/.ucl` on a new machine with the tool installed
- **THEN** every action works as on the old machine, and actions using a private key accept the
  same passphrase

#### Scenario: Moved to another operating system
- **WHEN** a vendor copies an installation folder made on macOS to `C:\Users\carl\.ucl` on
  Windows, or one made on Windows to `~/.ucl` on Linux
- **THEN** every action works as on the old machine, every product, license and key is there,
  and actions using a private key accept the same passphrase

### Requirement: Guided mode
Every action SHALL run in guided mode: a person at a terminal answers the tool. An action MAY be
given any of its inputs on the command; the tool SHALL ask only for those not given. The
passphrase SHALL be asked at a hidden prompt every time an action needs it, and SHALL NOT be
taken from a command argument, an environment setting or standard input. With nobody present to
answer (no terminal attached for input), every action SHALL refuse before doing anything, saying
the tool is run by a person at a terminal, and change nothing. Listing the commands, help and
the tool's version are not actions: they SHALL work with nobody present. The result of an action (a key
string, a file path, a list) SHALL be written alone to standard output, so a person may redirect
it to a file; prompts and messages SHALL go to standard error. An action SHALL exit with success
only when it completed; a refusal SHALL exit with a failure code.

#### Scenario: Inputs given on the command
- **WHEN** the vendor runs the issue action at a terminal with product, expiry and features given
- **THEN** the tool asks only for the passphrase, hidden, and writes the key string

#### Scenario: Prompted by hand
- **WHEN** the vendor runs the issue action at a terminal without an expiry
- **THEN** the action asks for the expiry

#### Scenario: Nobody present
- **WHEN** a script runs the issue action with every input given and no terminal attached
- **THEN** it refuses, saying the tool is run by a person at a terminal, writes no key, changes
  nothing, and exits with a failure code

#### Scenario: Help and version with nobody present
- **WHEN** a script with no terminal attached runs the tool with no command, asks for help, or
  asks for the version
- **THEN** the commands, the help or the version are written and the tool exits with success

#### Scenario: Passphrase not taken from the environment
- **WHEN** the vendor runs the issue action at a terminal with a passphrase in an environment
  setting or piped to standard input
- **THEN** the tool does not use it; it asks for the passphrase at the hidden prompt, or refuses
  as nobody present when standard input is not the terminal

#### Scenario: Result redirected to a file
- **WHEN** the vendor runs the issue action at a terminal with standard output redirected to a
  file and types the passphrase at the prompt
- **THEN** the file holds the key string alone, and the prompts and messages appear on screen

### Requirement: Operating systems and terminals
The tool SHALL run on Windows, macOS and Linux. On Windows it SHALL work in PowerShell, Windows
Terminal and Command Prompt. In each, every action SHALL behave as this spec states, including
setup, prompts, hidden passphrase entry, the result alone on standard output when it is
redirected to a file, and messages on standard error. Windows PowerShell 5.1 writes a redirected
file in its own encoding, which the tool does not control; the tool guide SHALL tell its users
to copy the key from the screen, use PowerShell 7, or write the file with an explicit plain-text
encoding.

#### Scenario: Setup in each Windows terminal
- **WHEN** the vendor runs setup in PowerShell, in Windows Terminal and in Command Prompt
- **THEN** in each, the folder is offered as a full Windows path, the passphrase is not shown
  while typed, and setup completes as on macOS and Linux

#### Scenario: Key redirected to a file on Windows
- **WHEN** the vendor runs the issue action in Command Prompt or PowerShell 7 with standard
  output redirected to a file
- **THEN** the file holds the key string alone, and the prompts and messages appear on screen
