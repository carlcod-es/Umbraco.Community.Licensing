## Purpose

Lets a small vendor set up one issuing installation of the vendor tool, find it again on every
run and on another machine, and do all their work in a menu session: a person at a terminal
chooses actions from a menu and answers the tool's questions, until they quit. Scripted mode,
with nobody present, is not offered.

## ADDED Requirements

### Requirement: One vendor and one person per installation
An installation SHALL hold one vendor's issuing setup: one folder, one database, one key prefix
at a time, one signing-key mode. It SHALL have no user accounts or permissions: whoever can run the tool
against the folder can perform every action, and actions that use a private key also need the
passphrase. A vendor needing a second issuing setup SHALL create a second installation in
another folder (New installation in the current folder).

#### Scenario: Second installation
- **WHEN** a vendor whose session opened `~/.ucl` starts the tool in `~/acme-practice` and creates
  a new installation there from Settings
- **THEN** a second, independent installation is created there; sessions started in
  `~/acme-practice` see only its products, licenses and keys, and sessions started elsewhere
  still use `~/.ucl`

### Requirement: Guided setup
Setup SHALL run once per installation, only with a person answering at a terminal, and SHALL ask, in order, for: the folder (Folder location), the passphrase
(Passphrase rule; typed twice, hidden), the key prefix (`LIC` by default, the vendor's own, or none, by the
prefix rule), the signing-key mode (one signing key per product, proposed, or one shared across all
products), whether to create a standby key with each signing key, presented as recommended, and
the idle lock time (Idle lock).
In shared mode setup SHALL create the shared signing key (and its standby key when chosen).
Setup SHALL end by naming the folder's full path, what it holds (private signing keys,
encrypted; license records; customer emails and order references, not encrypted), and that the
folder must be backed up and kept out of folders synced to cloud storage, naming a few such
services as examples (for instance OneDrive, iCloud Drive or Dropbox) without presenting them as
a complete list, and then the next steps, named as menu entries:
add a product (in per-product mode, saying this creates its signing key under the passphrase
just set), then print the public key lines its release must trust. The session SHALL then
continue to the menu with the new installation, keeping the passphrase typed at setup in either
signing-key mode, under the idle lock (Idle lock). The signing-key mode
SHALL NOT change after setup; the prefix MAY (Changing the prefix), and so MAY the idle lock time. Setup SHALL refuse a folder that already holds an
installation, changing nothing. Setup SHALL take no answer from options, environment settings or
redirected input.

#### Scenario: Setup with defaults
- **WHEN** a vendor runs setup and accepts every proposed value, types a valid passphrase twice
  and keeps the standby key
- **THEN** an installation is created in `~/.ucl` with prefix `LIC`, one signing key per product,
  standby keys on and an idle lock of 15 minutes; the final message names the full folder path,
  the content, the backup advice, the sync warning with example services and the next steps, and
  the menu is shown

#### Scenario: Invalid prefix
- **WHEN** the vendor types prefix `acme-shop`
- **THEN** setup names the prefix rule and asks again; nothing is created until setup completes

#### Scenario: Passphrases differ
- **WHEN** the two passphrase entries differ
- **THEN** setup says so and asks for both again

#### Scenario: Passphrase too short
- **WHEN** the passphrase is shorter than 15 characters
- **THEN** setup names the minimum length and asks again

#### Scenario: Passphrase with an accented letter
- **WHEN** the vendor types `Grüße aus München 2026` as the passphrase
- **THEN** setup says the passphrase uses only letters A to Z, digits, spaces and the symbols on a
  standard keyboard, and asks again

#### Scenario: Folder already holds an installation
- **WHEN** setup is chosen for a folder that already holds an installation
- **THEN** setup refuses, naming the folder, and nothing in it changes

#### Scenario: Setup cancelled
- **WHEN** the vendor cancels at any setup question
- **THEN** nothing is created, and no folder is left behind that setup created

### Requirement: Passphrase rule
The passphrase SHALL be 15 to 256 characters, each a printable ASCII character (letters A to Z
and a to z, digits, space and the symbols on a standard keyboard). Spaces inside it SHALL be
allowed; a leading or trailing space SHALL be refused. No other rule SHALL apply. Setup SHALL
check the passphrase against this rule and, when it fails, name the rule broken and ask again.

#### Scenario: Passphrase of words
- **WHEN** the vendor types `correct horse battery staple` twice at setup
- **THEN** setup accepts it

#### Scenario: Passphrase with a trailing space
- **WHEN** the vendor types a passphrase ending in a space
- **THEN** setup says a passphrase cannot start or end with a space, and asks again

#### Scenario: Passphrase too long
- **WHEN** the vendor types a passphrase of 257 characters
- **THEN** setup names the maximum length and asks again

### Requirement: Idle lock
Setup SHALL ask for the idle lock time: a whole number of minutes from 1 to 1440, or `never`,
with 15 proposed. A session left with nothing typed for that long, neither an answer nor a menu
choice, SHALL forget the passphrase
and every decrypted private key; the next action that needs the passphrase, including one in
progress, SHALL ask for it again. With `never` the passphrase is kept until the session ends.
Settings SHALL show the idle lock time and change it, by the same rule, without the passphrase;
a change SHALL take effect from the next session.

#### Scenario: Locked after idle time
- **WHEN** the idle lock is 15 minutes, the vendor issues a license, leaves the session for 16
  minutes, then issues another
- **THEN** the second issue asks for the passphrase again

#### Scenario: Idle lock never
- **WHEN** the idle lock is `never` and the vendor issues licenses an hour apart in one session
- **THEN** the passphrase is asked only for the first

#### Scenario: Menu choices keep the session unlocked
- **WHEN** the idle lock is 15 minutes, the vendor issues a license, then for 30 minutes chooses
  menu entries at least every 10 minutes without starting a signing action, then issues another
- **THEN** the second issue asks for no passphrase

#### Scenario: Idle lock outside the rule
- **WHEN** the vendor types `0`, `1441` or `ten` as the idle lock time
- **THEN** the tool names the idle lock rule and asks again

#### Scenario: Idle lock changed
- **WHEN** the vendor changes the idle lock from 15 to 5 minutes in Settings
- **THEN** no passphrase is asked, the current session keeps 15 minutes, and the next session
  locks after 5

### Requirement: Changing the prefix
The vendor SHALL show the installation's prefix in Settings and change it to `LIC`, their own or none, by the
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
proposed, and the current folder. An installation SHALL never sit directly in the home folder:
when the current folder is the home folder itself, setup SHALL offer only `.ucl` in it. Setup
SHALL create `.ucl` when it does
not exist, and SHALL accept a chosen folder that holds other files but no installation. A session SHALL use the first
of: the current folder when it holds an installation, `.ucl` in the home folder. No other folder
SHALL be used, and a folder SHALL NOT be named on the command. The tool SHALL remember no folder
between runs. A session SHALL state the folder's full path when it opens and in Settings; when
the current folder is used and `.ucl` in the home folder also holds an installation, it SHALL
also note that one, saying it is not used. When no installation is found, the session SHALL name
the folders it checked and offer: set up a new installation, restore one from a backup
(vendor-tool-backup: Backup import), or quit. Either of the first two SHALL continue to the menu
with the installation it created. The home folder SHALL be the
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

#### Scenario: Started in the home folder
- **WHEN** setup runs from the startup offer in a session started in the home folder
- **THEN** only `~/.ucl` is offered, shown in full, and the home folder itself is not

#### Scenario: Current folder with other files
- **WHEN** setup runs in `~/acme-issuing`, which holds other files but no installation, and the
  vendor chooses the current folder
- **THEN** the installation is created there and the other files are left untouched

#### Scenario: Current folder used
- **WHEN** the vendor starts the tool in `~/acme-practice`, which holds an installation, while
  `~/.ucl` holds another
- **THEN** the session uses `~/acme-practice`, states its path, and notes that `~/.ucl` also
  holds an installation that is not used

#### Scenario: Home installation used elsewhere
- **WHEN** the vendor starts the tool in a folder holding no installation while `~/.ucl` holds one
- **THEN** the session uses `~/.ucl` and states its path

#### Scenario: No installation found
- **WHEN** the tool is started at a terminal where neither the current folder nor `~/.ucl` holds
  an installation
- **THEN** it names both folders and offers setup, restore from a backup, or quit; nothing is
  created unless the vendor chooses one

#### Scenario: Setup from the startup offer
- **WHEN** the vendor chooses setup at the startup offer and completes it
- **THEN** setup ends with its closing message and the menu is shown for the new installation

#### Scenario: Quit at the startup offer
- **WHEN** the vendor chooses quit at the startup offer
- **THEN** the tool ends, creates nothing and exits with success

### Requirement: New installation in the current folder
Settings SHALL offer a new installation in the current folder, by setup or by restoring a backup,
with the current folder as the only folder offered. It SHALL be refused, changing nothing, when
the current folder is the home folder or holds an installation. When it completes, the session
SHALL continue with the new installation and state its full path, holding the passphrase only
when it was set by this setup (vendor-tool-signing-keys: Private keys encrypted with the
passphrase).

#### Scenario: New installation from Settings
- **WHEN** the session started in `~/acme-practice` uses `~/.ucl`, and the vendor chooses a new
  installation in the current folder and completes setup
- **THEN** the installation is created in `~/acme-practice`, the session states that path and
  continues with it, and `~/.ucl` is unchanged

#### Scenario: Current folder is the home folder
- **WHEN** the session was started in the home folder and the vendor chooses a new installation
  in the current folder
- **THEN** it is refused, saying an installation cannot sit loose in the home folder, and nothing
  changes

### Requirement: Moving an installation
An installation SHALL keep working after its folder is copied to another folder or machine,
including a machine running another of the supported operating systems (Windows, macOS, Linux),
in either direction, with no other file needed. The vendor uses a copied folder by starting the tool in it or by
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

### Requirement: Records can move to another database
Every record the tool keeps (products, feature definitions, licenses, issue entries, customer
fields, signing keys with their encrypted private keys, and the installation's settings) SHALL
be complete enough to move to another database with nothing lost. No record SHALL depend on the
folder, machine or operating system it was made on.

#### Scenario: Records hold no local path
- **WHEN** an installation has products, licenses, issue entries and signing keys, and every
  stored value is read
- **THEN** no value holds the installation's folder path, the home folder path or the machine
  name

#### Scenario: Records complete without the folder
- **WHEN** every stored record of an installation is read, without the folder's other files
- **THEN** each product, feature definition, license, issue entry, customer field, signing key
  with its encrypted private key, and setting is there, and a license can be reissued from them

### Requirement: Menu session
Started with no command, the tool SHALL open a session: it finds the installation (Folder
location) and shows a menu with Products, Licenses, Signing keys, Backup, Settings and Quit.
Products SHALL offer add, rename, add a feature and list. Licenses SHALL offer issue, reissue,
renew, resend, find and list, show, and customer details. Signing keys SHALL offer status, add a
standby key, switch, trusted keys, release confirmed, reissues sent, and export by signing key.
Backup SHALL offer export, and restore into the current folder (New installation in the current
folder). Settings SHALL offer the folder in use, the prefix, the idle lock and a new installation
in the current folder. After each action, and after a cancelled one, the session SHALL return to
the menu it was chosen from, until the vendor quits. The tool SHALL accept no command other than
help and version, and no input on the command. A person is present only when both the tool's
input and its output are a terminal. With nobody present, the tool SHALL
refuse before doing anything, saying it is run by a person at a terminal, and change nothing;
help and the version SHALL work with nobody present. Quitting SHALL end the session, forgetting
the passphrase and every decrypted private key, and exit with success.

#### Scenario: Several jobs in one session
- **WHEN** the vendor adds product `acme.commerce`, adds feature `max-orders` to it, issues three
  licenses for it and quits, in one session
- **THEN** each action completes in turn, the menu returns after each, and the tool exits with
  success

#### Scenario: Nobody present
- **WHEN** a script starts the tool with no terminal attached
- **THEN** it refuses, saying the tool is run by a person at a terminal, changes nothing, and
  exits with a failure code

#### Scenario: Output redirected
- **WHEN** the vendor starts the tool at a terminal with its output redirected to a file
- **THEN** it refuses, saying the tool is run by a person at a terminal, changes nothing, and
  exits with a failure code

#### Scenario: Help and version with nobody present
- **WHEN** a script with no terminal attached asks the tool for help or its version
- **THEN** the help or the version is written and the tool exits with success

#### Scenario: Command given
- **WHEN** the vendor starts the tool with `license issue acme.commerce`
- **THEN** it refuses, saying the tool takes no command other than help and version and is used
  from its menu, and changes nothing

### Requirement: Answers at a prompt
Each question SHALL say what it accepts and, when there is a proposed or current value, show it;
an empty answer SHALL accept it. Where a list of choices is known (products, features, signing
keys), the question SHALL show it. An answer that breaks its rule SHALL be refused by naming the
rule, and that question alone SHALL be asked again, keeping the earlier answers of the action.
Where these specs say the tool refuses a value the vendor gives, a value typed at a question is
treated so. Every question SHALL show how to cancel the action; cancelling SHALL store nothing
and return to the menu. The passphrase SHALL be asked at a hidden prompt, and SHALL NOT be taken
from a command argument, an environment setting or standard input.

#### Scenario: Bad answer asked again
- **WHEN** issuing, the vendor chooses `acme.commerce`, then types `2027-02-30` as the expiry
- **THEN** the tool names the expiry rule and asks for the expiry again, keeping
  `acme.commerce`

#### Scenario: Proposed value accepted
- **WHEN** a question shows a proposed value and the vendor gives an empty answer
- **THEN** the proposed value is used

#### Scenario: Action cancelled
- **WHEN** the vendor cancels an issue at the customer email question
- **THEN** nothing is signed or stored and the Licenses menu is shown

#### Scenario: Passphrase not taken from the environment
- **WHEN** the vendor starts the tool at a terminal with a passphrase in an environment setting
- **THEN** the tool does not use it; it asks for the passphrase at the hidden prompt when an
  action needs it

### Requirement: Operating systems and terminals
The tool SHALL run on Windows, macOS and Linux. On Windows it SHALL work in PowerShell, Windows
Terminal and Command Prompt. In each, every action SHALL behave as this spec states, including
setup, the menu, questions, hidden passphrase entry and files the tool writes.

#### Scenario: Setup in each Windows terminal
- **WHEN** the vendor runs setup in PowerShell, in Windows Terminal and in Command Prompt
- **THEN** in each, the folder is offered as a full Windows path, the passphrase is not shown
  while typed, and setup completes as on macOS and Linux

#### Scenario: Key saved to a file on Windows
- **WHEN** the vendor issues a license in Command Prompt or Windows PowerShell 5.1 and saves the
  key to a file
- **THEN** the file holds the key string alone, as on macOS and Linux
