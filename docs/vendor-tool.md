# Umbraco.Community.Licensing.Tool

`ucl` is the vendor tool for [Umbraco.Community.Licensing](https://github.com/carlcod-es/Umbraco.Community.Licensing):
it holds your products, your signing keys and a record of every license you issue, and it
issues, reissues, renews and resends license keys from those records. It is for a small vendor
who issues every key by hand. If you issue keys from your own code instead, use the library
directly ([vendor guide](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/vendor-guide.md)).

> **Beta: no stability before 1.0.** Any 0.x release may change the key format. Do not issue
> beta keys to paying customers, and reissue every key when moving to 1.0.

Everything happens in a menu session at a terminal: you start `ucl`, choose actions from
numbered menus, answer one question at a time, and quit when you are done.

## Install

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows, macOS or Linux.
Then, in a terminal:

```
dotnet tool install --global Umbraco.Community.Licensing.Tool --prerelease
```

`--prerelease` is needed while the tool is in beta. To update to the latest version:

```
dotnet tool update --global Umbraco.Community.Licensing.Tool --prerelease
```

If the terminal does not find `ucl` after installing, open a new terminal, and check that the
.NET tools folder is on your path: `.dotnet/tools` in your home folder (on Windows,
`%USERPROFILE%\.dotnet\tools`).

To build and run the tool from source instead, see
[Development](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/development.md).

Install the latest version of the tool, not one matching a library version. The tool and the
library are released together under one version number, but a release can publish the library
and fail to publish the tool. That number then exists for the library only, and the next release
publishes both. So the tool can skip version numbers the library has.

## Start ucl

Type `ucl` and press Enter. The tool opens a session, says which installation it uses, and shows
the main menu:

```
Using /Users/alex/.ucl

Main menu
  1 Products
  2 Licenses
  3 Signing keys
  4 Backup
  5 Settings
  0 Quit
Choose (a number from 0 to 5):
```

After each action the tool shows the menu it was chosen from again, until you choose Quit. In a
submenu, `0` is Back.

`ucl` is used only by a person at a terminal. When its input or its output is redirected (to or
from a file, or a pipe) it refuses, says so, changes nothing and exits with code 1. It takes no
commands; the only arguments it accepts work anywhere, also from a script:

| Argument | What it does |
|---|---|
| `--help` (`-h`, `-?`) | Shows a short help and the menu |
| `--version` | Shows the version |

Any other argument is refused with exit code 2.

**Quit when you leave the terminal.** While a session is open it may hold your passphrase
(Passphrase and idle lock).

## Answering questions

Each question says what it accepts. When there is a proposed value or a current value it is
shown, and pressing Enter accepts it:

```
Key prefix (LIC, your own (1 to 16 characters of A to Z and 0 to 9) or none; Enter for LIC; /cancel to cancel):
```

- **Refused answers.** An answer that breaks the rule is refused with the rule named, and that
  question alone is asked again. Earlier answers in the action are kept.
- **Lists.** Where there is a list (products, features, signing keys, reasons) it is shown
  numbered. Answer with the number or the ID.
- **Optional values.** Enter leaves an optional value out. At a reissue, Enter keeps the current
  value.
- **`/cancel`** at any question, the passphrase included, cancels the action. Nothing is stored
  and the menu is shown again.
- **`/remove`** at a reissue removes the display name, the vendor tag, or a number or text
  feature, where the question offers it.
- `/cancel` and `/remove` are read as the whole answer, in any case. So a value that is exactly
  `/cancel` or `/remove` cannot be entered.
- **Ctrl+C** ends the whole session at once, not only the action. An action that was not
  finished stores nothing, because each action writes only after its last answer.
- **Paths** you type may be full, or relative to the current folder. `~` on its own, or
  followed by `/` (on Windows also `\`), stands for your home folder: `~/keys/a.txt` is
  `a.txt` in a `keys` folder in your home folder. Any other `~` is part of a name:
  `~keys/a.txt` is read from a folder named `~keys` inside the current folder. The tool then
  shows the path in full.

## Set up

The first time you start `ucl`, it finds no installation and names the two folders it looked in:

```
No installation found. Looked in:
  /Users/alex/work
  /Users/alex/.ucl

What now?
  1 Set up a new installation
  2 Restore from a backup
  0 Quit
```

Choose `1`. Setup asks, in order:

1. **Folder.** `.ucl` in your home folder, proposed, or the current folder, both shown in full.
2. **Passphrase**, typed twice. Nothing is shown while you type (Passphrase and idle lock).
3. **Key prefix.** `LIC` proposed, your own (1 to 16 characters of A to Z and 0 to 9), or
   `none`. It starts every key identifier, as in `LIC-8F3AK-M7RXB-7Q2D`.
4. **Signing-key mode.** One signing key per product (recommended, proposed) or one signing key
   shared by all products. This cannot be changed later.
5. **Standby keys.** `[Y/n]`, recommended. A standby key is a spare signing key that your
   product releases trust before you need it (Signing keys).
6. **Idle lock**, in minutes, 15 proposed (Passphrase and idle lock).

Nothing is created until you answer the last question. Cancelling at any question leaves nothing
behind. Setup creates `.ucl` when it does not exist. A chosen folder may hold other files; it is
refused only when it already holds an installation.

When setup completes it names the folder and what it holds, tells you to back it up and keep it
out of folders synced to cloud storage, and gives the next steps. Then the session continues to
the main menu with the passphrase you just set.

### Why not directly in the home folder

An installation is a folder you copy, back up and move as a whole, so it never sits loose in your
home folder among everything else. When you start `ucl` in your home folder, setup offers only
`.ucl` in it.

## First steps

Setup's closing message names steps 1 and 3; steps 2 and 4 follow from them.

1. **Products > Add a product.** In per-product mode this also creates the product's signing key
   (and its standby key), encrypted with your passphrase. In shared mode setup has already
   created the shared signing key.
2. **Products > Add a feature**, for each feature your product reads.
3. **Signing keys > Trusted keys.** Prints the public key lines your product release must
   trust. Put them in the product (Trusted keys) and ship that release. Then confirm it with
   **Signing keys > Release confirmed**.
4. **Licenses > Issue.**

## Where the installation is

An installation is a folder holding the file `ucl.db`. That one file holds everything: products,
feature definitions, signing keys, license records and settings.

Each session checks two folders, in this order, and uses the first that holds `ucl.db`:

1. The folder you started `ucl` in.
2. `.ucl` in your home folder.

Nothing else is searched, and no folder is remembered between sessions. The session states the
full path when it opens, and **Settings > Folder in use** shows it again. When the current folder
is used and `.ucl` in your home folder also holds an installation, the session notes that:

```
Note: /Users/alex/.ucl also holds an installation; it is not used in this session.
```

So to use another installation, start `ucl` in its folder.

### Home folder on each operating system

| OS | Home folder | `.ucl` in it |
|---|---|---|
| Windows | `C:\Users\alex` (your profile folder) | `C:\Users\alex\.ucl` |
| macOS | `/Users/alex` | `/Users/alex/.ucl` |
| Linux | `/home/alex` | `/home/alex/.ucl` |

The tool always shows full paths in your operating system's own form, never `~`, even when you
typed one with `~`.

### A second installation

For a second issuing setup (for example a practice installation, or another business), make a
folder, start `ucl` in it, and choose **Settings > New installation in the current folder**:
**Set up** or **Restore from a backup**. Only the current folder is offered. It is refused in
your home folder and in a folder that already holds an installation. The session then continues
with the new installation. Sessions started in that folder use it; sessions started elsewhere
still use `.ucl`.

## Passphrase and idle lock

Your private signing keys are stored encrypted with your passphrase. Nothing else in the
installation is encrypted.

**The rule.** 15 to 256 characters, each a letter A to Z or a to z, a digit, a space or a
symbol on a standard keyboard. Spaces inside are fine; it cannot start or end with a space.
Accented letters and other scripts are refused, so the passphrase types the same on any
keyboard and any operating system. A few words make a good passphrase:
`correct horse battery staple`.

**When it is asked.** Once per session, at the first action that signs or creates a key: Add a
product (per-product mode), Issue, Reissue, Renew, Resend, Add a standby key and Switch. The
session keeps it for later actions. After setup, the passphrase you just set is kept. Reading
anything, backups and settings never ask for it. A wrong passphrase is said to be wrong and
asked again, three tries in all; after the third the action is cancelled and nothing is stored.

**The idle lock.** When nothing has been typed in the session for the idle lock time (menu
choices count), the session forgets the passphrase and every decrypted key. The next action that
needs it asks again. Set at setup (1 to 1440 minutes, or `never`), changed in
**Settings > Idle lock** without the passphrase; a change applies from the next session. With
`never`, the passphrase is kept until you quit.

Quitting, Ctrl+C or closing the terminal forgets the passphrase. It is never written anywhere.

**Losing the passphrase.** The private signing keys cannot be recovered without it, from the
installation or from any backup. Keep it in a password manager, apart from your backups. The
passphrase cannot be changed. If it is lost, nothing in that installation can sign again, and
Switch needs the passphrase too. The records stay readable (Find and list, Show a license, Export
by signing key). Licenses already issued keep working on releases that trust their keys; for new
keys you need a new installation, with new signing keys that your next releases trust.

## The menu

### Products

| Entry | What it does |
|---|---|
| **Add a product** | Asks the product ID (two dot-separated parts like `acme.commerce`, fixed for good) and the display name. In per-product mode creates the product's signing key, and its standby key when standby keys are on. In shared mode the product uses the shared key. |
| **Rename a product** | Changes the display name, showing the current one. Nothing changes until a valid name is given; `/cancel` keeps the current one. |
| **Add a feature** | Shows the product's features so far, then asks the feature name (`max-orders`) and its type: switch (on or off), number or text. Features are add-only: never removed, renamed or retyped. A product has at most 50 features, the most a license carries. |
| **List products** | Each product with its display name, features and number of licenses. |

### Licenses

**Choosing a license.** Every question that asks for a license takes its reference
(`LIC-8F3AK-M7RXB`, or `8F3AK-M7RXB`) or a whole key string. So when a site owner sends you
their key, paste it: the tool reads which license it is from the key, expired or not, and uses
nothing else from it and keeps nothing. A key not issued by this installation, or one with no
record, is refused and the question asked again. Choosing a license never needs the passphrase.

After a prefix change, a reference must be typed with the current prefix or without one; a
pasted key from before the change still works.

| Entry | What it does |
|---|---|
| **Issue** | Asks the product, expiry, display name, vendor tag, each of the product's features in turn, order reference and customer email; everything but the product and expiry is optional. Then the summary. |
| **Reissue** | For a changed license: an add-on, a new expiry, a corrected display name. Shows each current value; Enter keeps it, a new value changes it, `/remove` removes it. Adds an issue entry with an optional order reference. |
| **Renew** | Asks the expiry, proposing a suggestion, then the order reference and customer email. Recorded as a renewal. |
| **Resend** | For a site owner who lost their key: the same license, nothing changed, a new key string. An expired license cannot be resent; renew or reissue it instead. |
| **Find and list** | Filters by product, license, order reference and customer email, each optional; leave all empty to list every license. Each row shows reference, product, current expiry, customer email and latest order reference. |
| **Show a license** | The license with every issue entry, newest first: kind, time, expiry, features, order reference, key identifier and signing key. |
| **Customer details** | Change the customer email, clear it, correct the latest order reference, or clear every order reference. |

**Values at issue.** Expiry is a date like `2027-03-01` (the end of that day, UTC), a UTC date
and time like `2027-03-01T23:59:59Z`, or `perpetual`. A switch is `on` or `off`: `off` leaves it
out of the key, the same as giving it no value (at a reissue, `off` removes it). A number has at
most 4 decimal places and 15 digits. The display name, vendor tag and text features follow the
key's field rules in the
[vendor guide](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/vendor-guide.md).

**Summary and signing.** Issue, Reissue, Renew and Resend show a summary (product, reference,
expiry, display name, vendor tag, features, customer email, order reference) and ask
`Sign this license? [Y/n]`. No stores nothing. Yes asks the passphrase if the session does not
hold it, signs, and shows the key string once:

```
Issued LIC-FDVH4-5D746. The key string is shown once and is not stored:
LIC-FDVH4-5D746-TWS7.eyJrIjoid...
Save the key to a file? [y/N]
```

Copy it from the screen, or answer `y` and give a file path: the file holds the key string alone
(Files the tool writes). The tool never stores key strings; to give a site owner their key again,
use Resend.

**Suggested expiry at renewal.** The current expiry plus the length of the last term: the last
renewal's term, or the first issue's when never renewed. Whole calendar months are added as
months (a one-year license renews to the same day next year), any other term as exact time. A
perpetual license stays perpetual. Press Enter to accept, or type another expiry.

**Customer details.** The customer email belongs to the license; an order reference belongs to
each issue entry. Change the email or correct the latest order reference by typing the new
value. Clearing cannot be undone, so the tool first shows what will go and asks, with no
proposed:

```
License: LIC-FDVH4-5D746
Order references to clear:
  2026-11-10T16:38:09Z  renewal  ORD-1874
  2026-11-10T16:36:00Z  issue  ORD-1042
Clear these order references? They cannot be undone. [y/N]
```

No (or Enter, or `/cancel`) clears nothing. A cleared value leaves no copy in the installation.
Backups taken earlier still hold it (Backup). Licenses and issue entries are never deleted.

### Signing keys

| Entry | What it does |
|---|---|
| **Status** | Each product's signing key and standby key; beside a standby, the products whose release does not trust it yet. Answer `y` to list only signing keys without a standby key. |
| **Add a standby key** | Adds a standby key to a signing key that has none. |
| **Switch** | Replaces a signing key, giving the reason: `routine`, `loss` or `leak`. The standby key becomes the active key and a new standby is created; with no standby, a new key pair becomes active. The replaced key signs nothing more and stays in the records. In shared mode the switch applies to every product. |
| **Trusted keys** | Prints the public key lines a product's next release must trust. |
| **Release confirmed** | Prints a product's trusted list and asks whether a release of that product trusts it. |
| **Reissues sent** | For a key replaced after a leak: confirms the reissued keys are sent, so it leaves the trusted list. |
| **Export by signing key** | Writes a CSV of every issue entry signed by a chosen key. |

**Trusted keys.** The output is ready to paste into your product:

```
// Trusted public keys for acme.commerce
// active
sLzjrPW7GYc.MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...
// standby
Qw6evBsHsrE.MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...
```

Lines starting `// ` are comments in C# and in .NET JSON configuration. Each key line goes in
quotes, in `SigningPublicKey.Parse("...")` or as a JSON string in configuration
([vendor guide: Check a license in the product](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/vendor-guide.md#check-a-license-in-the-product)).
The list holds the active key, the standby key and every replaced key, except one replaced after
a leak once its reissues are sent.

**Release confirmed, per product.** When a release of a product ships trusting the printed list,
choose **Release confirmed** for that product and answer `y`. The tool then knows which keys
each product's sites trust. In shared mode the shared keys sign for every product, so confirm
each product's release as it ships; a confirmation counts for that product only.

**Why a switch can ask first.** Sites trust only the keys in the release they run. If the key
becoming active is not yet in a release of every product it signs for, the switch names those
products, warns that their sites will report new keys as signed by a key not recognised, and
asks `Switch anyway? [y/N]`. No changes nothing. This is why a standby key helps: once a release
trusts it, a switch goes ahead at once and new keys work on those sites. It asks whatever the
reason, leak included, because in a leak you may still decide to switch first.

**With standby keys off**, a signing key has no standby, so a switch makes a new active key and
no standby. Add one afterwards with **Add a standby key**.

**Export by signing key** writes a CSV (UTF-8, readable by spreadsheet tools) with the columns
`reference,key_identifier,product,issued_utc,expires_utc`, `perpetual` for no expiry. Every
entry is included, also those since reissued and those expired. The reference column uses the
current prefix; the key identifier keeps the prefix it was issued with.

### Backup

| Entry | What it does |
|---|---|
| **Export** | Writes the whole installation to a zip file you name. No passphrase. |
| **Restore into the current folder** | Restores a backup into the folder you started `ucl` in. |

**Export.** The zip holds a consistent copy of the installation and the time it was taken. Take
backups often and name them by date, such as `ucl-2026-11-08.zip`, since the tool never replaces
a file. The private keys stay encrypted inside the zip; **the zip itself is not encrypted**, and
customer emails and order references in it are readable. Store backups as you would the
installation, out of cloud-synced folders unless you encrypt them.

A backup taken before you cleared a customer email or order reference still holds it. To honour
an erasure request fully, also delete or replace older backups that hold it.

**Restore.** Restore asks for the backup file and shows when it was taken:

```
This backup was taken on 2026-11-10 16:38:38 UTC.
Licenses issued after that date are not in the backup.
It will be restored into /Users/alex/practice.
Restore this backup? [y/N]
```

No changes nothing. Yes creates the installation and the session continues with it. The
session then holds no passphrase: the next signing action asks for the passphrase that was in
effect when the backup was taken. Restore is refused into a folder that already holds an
installation (move or rename it first) and, from the menus, in your home folder. A file that is
not a backup made by `ucl` is refused, and so is a backup taken by a newer version of the tool:
update the tool, then restore.

From the startup offer (no installation found), restore asks for the folder as setup does:
`.ucl` in your home folder or the current folder.

Times the tool shows are UTC.

### Settings

| Entry | What it does |
|---|---|
| **Folder in use** | The installation's full path, and the note when `.ucl` holds an installation that is not used. |
| **Prefix** | Shows the prefix and changes it: `LIC`, your own, or `none`. New issues and reissues use the new prefix; existing licenses keep theirs until reissued. No passphrase. |
| **Idle lock** | Shows the idle lock and changes it, from the next session. No passphrase. |
| **New installation in the current folder** | Set up or restore a new installation in the current folder (A second installation). |

## Rules for typed values

| Value | Rule |
|---|---|
| Product display name | 1 to 64 characters: printable ASCII, and letters, combining marks, digits and punctuation from any script (`コマース Müller – EU` is fine; `™` and emoji are not) |
| Customer email | 3 to 254 characters of the same kind, exactly one `@` with text before and after it, no spaces. `jürgen@müller.de` is fine |
| Order reference | 1 to 64 characters of the same kind. `#1042`, `INV-2026-0042`, `ORD 1042` and `pi_3NqXk2LkdIwHu7ix0yJ8` are fine |
| Passphrase | 15 to 256 characters on a standard keyboard, no space at the start or end |
| Idle lock | A whole number of minutes from 1 to 1440, or `never` |

Values are stored exactly as typed.

**Finding** by customer email or order reference matches the whole value, ignoring case, and
treats the two forms of an accented letter (one character, or a letter plus a combining mark) as
the same. Part of a value does not match: `1042` does not find `ORD-1042`. Spaces must match
exactly.

## Files the tool writes

Backups, CSV exports and saved keys go to a path you type. The tool never replaces an existing
file: when the file exists, it says so with the full path and asks again.

When the file's folder does not exist, the tool shows that folder's full path and asks
`Create it? [Y/n]`. Yes (or Enter) creates it, with any missing folders above it, and writes the
file. No asks for the path again and creates nothing, so a mistyped folder such as `~/Dropbx` is
caught before anything is made. `/cancel` ends the action and creates nothing.

When a backup to restore does not exist, or the path is a folder, the tool asks again. It never
creates a folder for a file it reads.

## Leak and loss

**Leak** (someone else may have your private key, so they could sign keys):

1. **Signing keys > Switch**, reason `leak`, for the product (or the shared key). New keys are
   signed with the new key from now on.
2. **Signing keys > Trusted keys**, and ship a release trusting that list (it still holds the
   leaked key, marked `LEAKED`), then **Release confirmed**. Skip this when the standby that
   became active was already in a release.
3. **Signing keys > Export by signing key** for the leaked key: the CSV lists every license it
   signed.
4. **Licenses > Reissue** each license in it that is still in use and not yet reissued with the new
   key (paste the reference from the CSV; Show a license tells you) and send the
   new keys to the site owners.
5. **Signing keys > Reissues sent** for the leaked key. It leaves the trusted list.
6. **Signing keys > Trusted keys** again, ship a release trusting the new list, and **Release
   confirmed**. Sites on that release no longer accept keys signed by the leaked key; until they
   update, whoever has it can still sign keys they accept.

Choose when to ship step 6: sooner closes the leak, later breaks fewer sites still waiting for
their reissued key.

**Loss** (the private key is gone, nobody else has it): **Switch**, reason `loss`. Licenses
already issued keep working, and the lost key stays in the trusted list. If the new active key
was a standby already in a release, new keys work straight away; otherwise ship a release
trusting the new key first.

Back up the installation, so a lost laptop is not a lost key.

## Moving and restoring between operating systems

An installation works on Windows, macOS and Linux, in either direction, with the same passphrase.

- **Copy the folder.** Quit `ucl`, copy the whole installation folder to the new machine, and
  either put it at `.ucl` in the home folder or start `ucl` in it.
- **Or restore a backup** on the new machine, from the startup offer or **Backup > Restore into
  the current folder**.

The database holds no paths or machine names, so nothing needs changing after a move. Install the
same or a newer version of the tool on the new machine first: an older version refuses an
installation a newer one has used, and a backup a newer one has taken.

## Windows terminals

`ucl` works in Windows PowerShell 5.1, PowerShell 7, Windows Terminal and Command Prompt. In each,
the passphrase is hidden while typed, and arrow keys and Backspace edit an answer. Paste a key
as your terminal allows (Ctrl+V or a right-click, depending on its settings). If your home folder's name has letters outside your console's code
page, Command Prompt may show them as `?`; the path is still right, only its display is not.

## Your responsibility for the database

The installation is yours to look after. It holds your signing keys and your customers' emails
and order references, which are personal data. The tool encrypts only the private keys. Keep the
machine and its backups secure, honour erasure requests (Customer details, and older backups),
and keep at least one recent backup somewhere other than the machine.
