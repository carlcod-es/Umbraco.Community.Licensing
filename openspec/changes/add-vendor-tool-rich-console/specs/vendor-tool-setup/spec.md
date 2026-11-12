## MODIFIED Requirements

### Requirement: Menu session
Started with no command, the tool SHALL open a session: it shows a banner naming the tool and
its version, finds the installation (Folder location) and shows a menu with Products, Licenses,
Signing keys, Backup, Settings and Quit, chosen as a list (Choosing from a list).
Products SHALL offer add, rename, add a feature and list. Licenses SHALL offer issue, reissue,
renew, resend, find and list, show, and customer details. Signing keys SHALL offer status, add a
standby key, switch, trusted keys, release confirmed, reissues sent, and export by signing key.
Backup SHALL offer export, and restore into the current folder (New installation in the current
folder). Settings SHALL offer the folder in use, the prefix, the idle lock and a new installation
in the current folder. Each submenu SHALL end with a Back entry, which returns to the menu above.
After each action, and after a cancelled one, the session SHALL return to
the menu it was chosen from, until the vendor quits. The tool SHALL accept no command other than
help and version, and no input on the command. A person is present only when both the tool's
input and its output are a terminal. With nobody present, the tool SHALL
refuse before doing anything, saying it is run by a person at a terminal, and change nothing;
help and the version SHALL work with nobody present. Quitting SHALL end the session, forgetting
the passphrase and every decrypted private key, and exit with success. An action that fails for a
reason the tool does not check for SHALL store nothing, say in one line which action failed and
why, and return to the menu it was chosen from, keeping the session and the held passphrase. A
failure outside any action SHALL end the session with one line saying why, forgetting the
passphrase, and exit with a failure code. No technical detail of a failure SHALL be shown.

#### Scenario: Several jobs in one session
- **WHEN** the vendor adds product `acme.commerce`, adds feature `max-orders` to it, issues three
  licenses for it and quits, in one session
- **THEN** each action completes in turn, the menu returns after each, and the tool exits with
  success

#### Scenario: Banner at the start
- **WHEN** the vendor starts the tool at a terminal
- **THEN** a banner naming the tool and its version is shown before the folder in use and the
  main menu

#### Scenario: Back from a submenu
- **WHEN** the vendor opens Licenses and chooses Back
- **THEN** the main menu is shown and nothing is changed

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

#### Scenario: Action fails unexpectedly
- **WHEN** a backup export fails because the disk is full
- **THEN** the tool says in one line that the backup export failed and why, writes no backup
  file, and shows the Backup menu; the session keeps the passphrase it holds

#### Scenario: Failure outside an action
- **WHEN** something fails while no action is running
- **THEN** the tool says in one line why the session ended, forgets the passphrase, and exits
  with a failure code

### Requirement: Answers at a prompt
Each question SHALL say what it accepts and, when there is a proposed or current value, show it;
an empty answer SHALL accept it. Where a list of choices is known (products, features, signing
keys), the question SHALL be a list (Choosing from a list). A typed answer SHALL be edited with
the terminal's own line editing and paste. An answer that breaks its rule SHALL be refused by
naming the rule, and that question alone SHALL be asked again, keeping the earlier answers of
the action.
Where these specs say the tool refuses a value the vendor gives, a value typed at a question is
treated so. Every question SHALL show how to cancel the action: `/cancel` at a typed question
or the hidden prompt, Esc at a list; cancelling SHALL store nothing and return to the menu. The
passphrase SHALL be asked at a hidden prompt, and SHALL NOT be taken
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

#### Scenario: Action cancelled at a list
- **WHEN** the vendor presses Esc at the product list while issuing
- **THEN** nothing is signed or stored and the Licenses menu is shown

#### Scenario: Typed answer edited
- **WHEN** at the display name question the vendor types `Acme Shpo`, moves back with the arrow
  keys, corrects it to `Acme Shop` and presses Enter
- **THEN** `Acme Shop` is the answer

#### Scenario: Passphrase not taken from the environment
- **WHEN** the vendor starts the tool at a terminal with a passphrase in an environment setting
- **THEN** the tool does not use it; it asks for the passphrase at the hidden prompt when an
  action needs it

### Requirement: Operating systems and terminals
The tool SHALL run on Windows, macOS and Linux. On Windows it SHALL work in PowerShell, Windows
Terminal and Command Prompt. In each, every action SHALL behave as this spec states, including
setup, the menu, lists chosen with the arrow keys, Enter, Space and Esc, questions, hidden
passphrase entry, colour and files the tool writes.

#### Scenario: Setup in each Windows terminal
- **WHEN** the vendor runs setup in PowerShell, in Windows Terminal and in Command Prompt
- **THEN** in each, the folder is offered as a full Windows path, the passphrase is not shown
  while typed, and setup completes as on macOS and Linux

#### Scenario: Lists in each Windows terminal
- **WHEN** the vendor opens Licenses with the arrow keys and Enter, ticks a switch with Space
  while issuing and presses Esc at a list, in PowerShell, in Windows Terminal and in Command
  Prompt
- **THEN** in each, the list moves, ticks and cancels as on macOS and Linux

#### Scenario: Key saved to a file on Windows
- **WHEN** the vendor issues a license in Command Prompt or Windows PowerShell 5.1 and saves the
  key to a file
- **THEN** the file holds the key string alone, as on macOS and Linux

## ADDED Requirements

### Requirement: Choosing from a list
A list SHALL show its entries with one highlighted, the proposed or current entry when there is
one, otherwise the first. The arrow keys SHALL move the highlight and Enter SHALL choose it. A
list longer than the screen SHALL page and say that more entries follow. A list whose answer is
optional SHALL hold an entry to leave it out.

#### Scenario: Proposed entry highlighted
- **WHEN** setup asks for the signing-key mode
- **THEN** one signing key per product is highlighted, and Enter chooses it

#### Scenario: Optional filter left out
- **WHEN** the vendor finds and lists licenses and chooses the entry to leave out the product
- **THEN** licenses of every product are listed

### Requirement: Esc at a list
Esc at a list in an action SHALL cancel the action, store nothing and return to the menu. In a
submenu Esc SHALL return to the menu above. At the main menu and the startup offer Esc SHALL do
nothing.

#### Scenario: Esc in a submenu
- **WHEN** the vendor presses Esc in the Signing keys menu
- **THEN** the main menu is shown

#### Scenario: Esc at the main menu
- **WHEN** the vendor presses Esc at the main menu
- **THEN** the main menu stays and the session continues

#### Scenario: Long product list
- **WHEN** the installation holds more products than fit on the screen and the vendor issues
- **THEN** the product list shows a page, says more entries follow, and moving past the last one
  shown brings the next into view

### Requirement: Several choices from a list
Where the vendor may choose several entries at once, the list SHALL show a box beside each entry.
Space SHALL tick or untick the highlighted entry, Enter SHALL accept the ticked entries, which may
be none, and Esc SHALL cancel the action. The entries already granted or chosen SHALL start
ticked.

#### Scenario: Two ticked
- **WHEN** the vendor ticks two entries with Space and presses Enter
- **THEN** those two are chosen

#### Scenario: None ticked
- **WHEN** the vendor presses Enter with no entry ticked
- **THEN** no entry is chosen and the action continues

### Requirement: Screen presentation
The tool SHALL show lists of records (products, licenses, issue entries, signing keys) as tables
with a heading per column, and the summary before signing and setup's closing message in framed
panels; help stays plain text. It SHALL use colour for questions, proposed values, success,
warnings and refusals, and every message SHALL still say in words what happened.

#### Scenario: Licenses as a table
- **WHEN** the vendor finds and lists licenses for `acme.commerce`
- **THEN** a table shows reference, product, current expiry, customer email and latest order
  reference for each

#### Scenario: Refusal in words
- **WHEN** an answer is refused
- **THEN** the refusal names the rule in words, whether or not colour is shown

### Requirement: Working indicator
While the passphrase is checked, a key is created, or a backup is written or restored, the tool
SHALL show that it is working, and SHALL ask no question until the work ends.

#### Scenario: Working on a backup
- **WHEN** the vendor exports a backup
- **THEN** the tool shows that it is working until the backup is written, then reports its path

### Requirement: Values shown as typed
A value the vendor or a customer typed SHALL be shown exactly as typed, never read as styling.

#### Scenario: Styling characters in a value
- **WHEN** the vendor issues with display name `Acme [red]Shop[/]`
- **THEN** the summary and the key carry `Acme [red]Shop[/]` exactly as typed, and no part of it
  changes colour

### Requirement: Copyable output stays plain
A key string, and each line printed by Trusted keys, SHALL be printed on its own line, with no
colour, frame, border or other characters on that line, so it copies from the screen exactly.

#### Scenario: Key copied from the screen
- **WHEN** the vendor issues a license and copies the key string from the screen
- **THEN** the copied text is the key string alone, and it verifies

#### Scenario: Trusted key lines copied
- **WHEN** the vendor prints the trusted keys for `acme.commerce` and copies the lines
- **THEN** each comment line and each public key line is copied as printed, with no border or
  colour characters

### Requirement: Colour can be turned off
The tool SHALL show no colour when the terminal cannot show it, or when the vendor has set the
common terminal setting that asks programs for no colour. Everything else SHALL work the same.

#### Scenario: No colour asked for
- **WHEN** the vendor starts the tool with the no-colour terminal setting on
- **THEN** the menus, lists, tables and messages are shown without colour, and every action works
