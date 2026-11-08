## Context

See proposal.md (Why). Current state: one library project `src/Umbraco.Community.Licensing`
(BCL only, ADR-0003), one xUnit v3 test project, `Version.props` imported by the library only
(ADR-0009), `release.yml` packs and pushes the library (ADR-0008), then job `github-release`
creates a GitHub Release with one nuget.org link per package ID its **Package IDs** step reads from
the pack folder (ADR-0016). `0.1.0-beta.6` is on nuget.org.

The library already gives the tool everything it signs and checks with, so this change needs no
library API or behaviour change. The one library edit gives the tool internal access to the
existing text check (ADR-0019):

| Tool need | Library API |
|---|---|
| Create a key pair; get its PEM and public line | `SigningKeyPair.Create`, `SigningPrivateKey.ExportPem`, `SigningPublicKey.Export` |
| Sign from a decrypted PEM | `SigningPrivateKey.FromPem` |
| Prefix from setup | `KeyPrefix.Of`, `KeyPrefix.None`, `LicenseIssuer(prefix)` |
| Issue, and reissue from a record (reference typed into a request, PDR-0023) | `LicenseIssuer.Issue(LicenseRequest, ...)` with `Reference` set |
| Feature checks against definitions | `FeatureDefinitions` (PDR-0016) |
| Features and expiry from stored values | `FeatureList.Of`, `LicenseFeature`, `LicenseExpiry.At` / `On` / `Perpetual` |
| Text rule for the tool's own fields (PDR-0043) | internal `ContentRules.CheckDisplayName` and `CheckText`, through `InternalsVisibleTo` (ADR-0019) |

`ReissueRequest.From` needs a verified key string, which the tool never stores (PDR-0033). The
tool builds a `LicenseRequest` with the stored reference and enforces the PDR-0021 rule (product
and reference kept) itself.

Product rules: PDR-0030 to PDR-0036. Behaviour: `specs/vendor-tool-*/spec.md`,
`specs/release-process/spec.md`.

## Bar

**Production.** The tool is published to nuget.org and holds vendors' private signing keys and
customer data. Every spec scenario has an automated test, except those listed as observed in
Test strategy.

## Goals / Non-Goals

**Goals:** a copied folder or backup never yields a usable signing key without the passphrase;
every action guided (a person at a terminal, PDR-0039); runs on Windows, macOS and Linux and
moves between them (PDR-0038, PDR-0040); folder portable by copying; records complete enough to
move to another database (PDR-0046); no library API or behaviour change; tool and library
released from one tested build.

**Non-Goals:** store integration, a long-running service, multiple users, encrypted database or
backup, private-key export (FR-11), passphrase change (FR-12), rich console UI (FR-13), bulk
reissue (FR-5), import of pre-tool keys (FR-16), changing signing-key mode (FR-17), reused
order reference warning (FR-18), several licenses in one run (FR-20), scripted mode (FR-22), central
database and shop integration (FR-24, FR-25, FR-26), HSM or vault keys (`pre-1.0-considerations.md`
C2), localisation.

## Requirements

| R | Requirement | Spec | Personas | Records |
|---|---|---|---|---|
| R1 | One vendor, one person per installation; menu session only | vendor-tool-setup: One vendor and one person per installation; Menu session; Answers at a prompt | Vendor; site owner (summary before signing) | PDR-0030, PDR-0039, PDR-0048 |
| R2 | Guided setup; prefix changeable later | vendor-tool-setup: Guided setup; Changing the prefix | Vendor; site owner (prefix and keys set once, keys stay consistent) | PDR-0030, PDR-0023 |
| R3 | Folder location, moving, second installation | vendor-tool-setup: Folder location; New installation in the current folder; Moving an installation | Vendor | PDR-0031, PDR-0040, PDR-0041, PDR-0048 |
| R4 | Products and add-only feature definitions | vendor-tool-products | Vendor; site owner (no feature silently lost, PDR-0016) | PDR-0036 |
| R5 | Issue, reissue, renew, resend from records | vendor-tool-licenses | Vendor; site owner (renewal keeps paid-for days; lost key resent) | PDR-0033, PDR-0034, PDR-0021 |
| R6 | Records without key strings; customer fields editable and clearable | vendor-tool-licenses | Site owner (copied backup holds no usable key; erasure honoured); vendor | PDR-0033, PDR-0014 |
| R7 | Private keys encrypted; never exported; passphrase kept for a session with an idle lock | vendor-tool-signing-keys: Private keys encrypted with the passphrase. vendor-tool-setup: Idle lock | Vendor; site owner (no forged keys from a copied folder or an unattended session) | PDR-0032, PDR-0049 |
| R8 | Standby, switch, trusted keys, export by signing key | vendor-tool-signing-keys | Vendor; implementor (release trusts the right keys); site owner (keys keep working after rotation) | PDR-0035, PDR-0015 |
| R9 | Backup export and import | vendor-tool-backup | Vendor; site owner (records survive a lost laptop) | PDR-0031 |
| R10 | Tool released with the library | release-process | Vendor | ADR-0011 |
| R11 | Windows, macOS and Linux; PowerShell, Windows Terminal, Command Prompt | vendor-tool-setup: Operating systems and terminals | Vendor | PDR-0038 |
| R12 | Typed values: product display name, customer email and order reference by the key's text rule; finding ignores case and accent form, whole value only; passphrase printable ASCII, 15 to 256 | vendor-tool-products: Adding a product; Feature definitions are add-only. vendor-tool-licenses: Customer email and order reference values; Matching when finding. vendor-tool-setup: Passphrase rule | Vendor; site owner (passphrase types the same on the next machine, so signing keys are not lost) | PDR-0043, PDR-0042 |
| R13 | Switch answered off is not granted; each feature asked once | vendor-tool-licenses: Switch values and features at reissue | Vendor; site owner and implementor unaffected (same key) | PDR-0044, PDR-0010, PDR-0048 |
| R14 | Named files never replaced, folders never created, asked again | vendor-tool-backup: Files named by the vendor; Files read by the tool. vendor-tool-signing-keys: Export by signing key | Site owner (an older backup survives); vendor | PDR-0045 |
| R15 | Records complete and free of local paths | vendor-tool-setup: Records can move to another database | Site owner (reissues survive a later move); vendor | PDR-0046 |

Persona check: no requirement harms a higher-priority persona. Key strings are unchanged for site
owners and implementors. Backoffice editor and site visitor never meet the tool.

## Questions settled in this change

| Q | Question | Answer | Record |
|---|---|---|---|
| Q1 | How keys are issued | By hand, or batched from the vendor's own process; every command runs from arguments alone; no store integration (Carl, 2026-10-28). Amended by Q33: guided mode only; inputs may be given on the command. Amended by Q47: menu session only, no inputs on the command | PDR-0030, PDR-0039, PDR-0048 |
| Q2 | Jobs in this change | FR-2 minimum; products and feature definitions; issue and reissue from records; find and list; standby key (FR-4). Import of pre-tool keys out, FR-16 (Carl, 2026-10-28) | PDR-0030 |
| Q3 | Audience | Small vendors issuing only through the tool. Larger vendors use the library directly; private-key export for them is FR-11 (Carl, 2026-10-28) | PDR-0030, PDR-0032 |
| Q4 | Folder location | Propose `~/.ucl`, show the full path, vendor may override; setup names the content, backup advice and sync warning (Carl, 2026-10-28); folder offer and resolution amended by Q29 | PDR-0031 |
| Q5 | Moving and backups | Folder portable by copying; zip export and import (Carl, 2026-10-28) | PDR-0031 |
| Q6 | SQL Server | Dropped; a database file inside the folder only (Carl, 2026-10-28). Was an FR-2 constraint | ADR-0013 |
| Q7 | Protecting private keys | Encrypted with a passphrase set at setup; nothing else encrypted; asked once per command that signs or creates a key; batched runs supply it without a prompt (Carl, 2026-10-28). Amended by Q33: always typed at the hidden prompt. Amended by Q49: asked at the first signing action, kept for the session | PDR-0032, PDR-0039, PDR-0049 |
| Q8 | Private-key export | None in this change; FR-11 (Carl, 2026-10-28) | PDR-0032 |
| Q9 | Suggested expiry on renewal | Vendor types the expiry; tool suggests current expiry plus the last term (Carl, 2026-10-28). Term and months versus exact time: see Q25 | PDR-0034 |
| Q10 | Correcting or erasing customer data | Edit or clear order reference and email; no copy of a cleared value kept; records never deleted (Carl, 2026-10-28) | PDR-0033 |
| Q11 | Trusted keys for a release | Switch asks the reason; trusted list per product; leaked key stays listed until reissues confirmed sent (Carl, 2026-10-28) | PDR-0035 |
| Q12 | Storing key strings | Not stored; resend is reissue unchanged (Carl, 2026-10-28) | PDR-0033 |
| Q13 | Backup import over an installation | Refused; import only into a folder with no installation; backup date warning (Carl, 2026-10-28) | PDR-0031 |
| Q14 | Operators; passphrase change | One person, no accounts; passphrase change out, FR-12 (Carl, 2026-10-28) | PDR-0030, PDR-0032 |
| Q15 | Standby key stored apart (FR-4 issue 2) | Kept with the active key under the same passphrase; storing apart is FR-15 (Carl, 2026-10-28) | PDR-0032 |
| Q16 | Standby key after setup (FR-4 issues 1, 4) | Can be added later; marked "not yet in a release" until confirmed; switch warns; list of products without a standby key (Carl, 2026-10-28). Amended by Q45: switch also warns with no standby | PDR-0035, PDR-0047 |
| Q17 | Changing feature definitions | Add-only; product ID fixed; display name editable; retiring a feature is FR-14 (Carl, 2026-10-28) | PDR-0036 |
| Q18 | Distribution, packaging, versioning | .NET tool package `Umbraco.Community.Licensing.Tool`, command `ucl`, .NET SDK prerequisite; same version as the library, released together (Carl, 2026-10-28) | ADR-0011 |
| Q19 | Command line and prompts | System.CommandLine; BCL console prompts; Spectre.Console is FR-13 (Carl, 2026-10-28) | ADR-0012 |
| Q20 | Database and folder resolution | EF Core + SQLite in the folder; option, environment, pointer file, default (Carl, 2026-10-28); resolution replaced by Q29 | ADR-0013 |
| Q21 | Private-key encryption format | Encrypted PKCS#8 PEM, PBES2 AES-256-CBC + PBKDF2-SHA256 (Carl, 2026-10-28) | ADR-0014 |
| Q22 | Backup and export formats | Zip of a consistent database snapshot and a manifest; CSV export (Carl approved T6, 2026-10-28) | ADR-0015 |
| Q23 | Prefix and signing-key mode after setup | Prefix changeable: new issues and reissues use the current prefix, existing licenses keep theirs until reissued, typed references per PDR-0023. Signing-key mode fixed; changing it is FR-17 (Carl, 2026-10-28) | PDR-0030 |
| Q24 | Passphrase rule | At least 15 characters, length only (NIST SP 800-63B rev 4) (Carl, 2026-10-28) | PDR-0032 |
| Q25 | Length of the last term | Length the last renewal granted, from the later of the previous expiry and the renewal time to the expiry it set; first issue's term when never renewed; other reissues do not change it. Calendar months when whole months, exact time otherwise (Carl, 2026-10-28) | PDR-0034 |
| Q26 | Where customer fields are held | Email per license; order reference per issue entry; only the latest entry's order reference correctable; clearing removes all of a license's order references at once. Warning on a reused order reference is FR-18 (Carl, 2026-10-28) | PDR-0033 |
| Q27 | Proposed signing-key mode at setup | Per-product proposed; shared offered. Standby keys follow the mode: one per product signing key, or one for the shared key (Carl, 2026-10-29) | PDR-0030 |
| Q28 | Scripted setup | Setup only by a person at a terminal; no answers from options, environment or redirected input; refused with nobody present. Every other action stays scriptable. Passphrase always typed twice (Carl, 2026-10-29) | PDR-0030 |
| Q29 | Finding the installation; more than one | Amended by Q35. First of: `--folder`, the current folder when it holds an installation, `~/.ucl`. Setup offers `~/.ucl` (proposed) or the current folder. Nothing remembered; no folder environment setting. Each action states the folder used. No installation: refuse naming the folders checked; at a terminal, offer setup (Carl, 2026-10-29). Amended by Q52, Q57 | PDR-0031, ADR-0013, PDR-0048 |
| Q30 | Folder state at setup | `~/.ucl` created when missing; a folder holding other files but no installation accepted; only an existing installation refused (Carl, 2026-10-29) | PDR-0031 |
| Q31 | Next step after setup | Closing message ends with the next steps by mode: `product add` (per-product: creates the signing key, asks the passphrase), then `key trusted`. The guide gives the same first steps (Carl, 2026-10-29) | PDR-0031 |
| Q32 | Operating systems and terminals | Windows, macOS and Linux; on Windows PowerShell, Windows Terminal and Command Prompt; `~/.ucl` is `.ucl` in each OS's home folder, paths shown in full; sync warning names example services; Windows checked by the automated suite and by hand in all three terminals (Carl, 2026-11-07) | PDR-0038, ADR-0017 |
| Q33 | Guided or scripted | Guided mode only: a person at a terminal, inputs allowed on the command, passphrase always typed at a hidden prompt, every action refused with nobody present, result may be redirected. Scripted mode is FR-22 (Carl, 2026-11-08). Amended by Q47, Q49, Q56: menu session only; passphrase once per session; key saved to a file, not redirected | PDR-0039, ADR-0017, PDR-0048, PDR-0049 |
| Q34 | Moving between operating systems | A folder copy or backup moves between any of the three, either direction, same passphrase (Carl, 2026-11-08) | PDR-0040, ADR-0017 |
| Q35 | Startup | Only the current folder, then `~/.ucl`; no folder option; an unused second installation is noted; setup from the offer ends with a generic retry line; `ucl` alone lists the commands; startup menu is FR-23 (Carl, 2026-11-08). Amended by Q46, Q52: `ucl` opens the menu session (FR-23 taken in); no installation offers setup or restore, then the menu | PDR-0041, ADR-0017, PDR-0048 |
| Q36 | Help, version and PowerShell 5.1 redirect | Help, version and the command list are not actions and work with nobody present; the PowerShell 5.1 redirect encoding is documented, not handled (Carl, 2026-11-08). Amended by Q47, Q56: no command list or redirect remain; help and version only | PDR-0039, ADR-0017, PDR-0048 |
| Q37 | Customer email: characters, length, matching | PDR-0042 text rule, exactly one `@` with text before and after, no spaces, 3 to 254; accented addresses allowed; stored as typed; finding matches the whole address, ignoring case and the form of an accented letter (Carl, 2026-11-08) | PDR-0043, ADR-0019 |
| Q38 | Order reference: characters and length | PDR-0042 text rule, 1 to 64; stored as typed; finding as Q37, spaces matched exactly, never part of a value (Carl, 2026-11-08) | PDR-0043, ADR-0019 |
| Q39 | Passphrase characters and maximum | Printable ASCII only, 15 to 256, spaces inside allowed, no leading or trailing space (Carl, 2026-11-08) | PDR-0043, ADR-0019 |
| Q40 | Product display name rule (moved from `tighten-text-rule` Q7) | The key's display-name rule (PDR-0042), 1 to 64, checked at `product add` and `product rename` through the library, not a copy; a refused rename keeps the earlier name. The tool reaches the internal check through `InternalsVisibleTo` (Carl, 2026-11-08) | PDR-0043, ADR-0019 |
| Q41 | `--feature name=off` | Not granted: left out at issue, removed at reissue as `--remove-feature`; a feature given twice, or given and removed, refused (Carl, 2026-11-08). Amended by Q54: features asked one by one, so no contradiction arises | PDR-0044, PDR-0048 |
| Q42 | Output and import files | An output file is never replaced and its folder never created; the tool says so with the full path and asks again, also when the path was given on the command (Amended by Q47: always asked). An import path that is missing or a folder is asked again (Carl, 2026-11-08) | PDR-0045, ADR-0019 |
| Q43 | Stale rows | Q1 and Q7 marked amended by Q33 (Carl, 2026-11-08) | PDR-0039 |
| Q44 | Records and a later central database | Every record complete and free of local paths, so it can move to another database with nothing lost. The later move is one-way and settled in FR-24 to FR-26 (Carl, 2026-11-08) | PDR-0046 |
| Q45 | Switch when the new active key is not in a release | Warn and ask for confirmation whenever the key becoming active is not yet in a release: a standby not yet in a release, or a new key pair when there is no standby. Any reason, leak included; declining changes nothing (Carl, 2026-11-08) | PDR-0047 |
| Q46 | Session form | A menu session with numbered menus and line-by-line questions; FR-23 taken in. Selection lists, colour and full screen stay FR-13 (Carl, 2026-11-08) | PDR-0048 |
| Q47 | Commands alongside the session | Session only: `ucl` opens the session; only help and version remain; no inputs on the command (Carl, 2026-11-08) | PDR-0048 |
| Q48 | Menu | Products (add, rename, add feature, list); Licenses (issue, reissue, renew, resend, find and list, show, customer details); Signing keys (status, add standby, switch, trusted keys, release confirmed, reissues sent, export by key); Backup (export, restore into the current folder); Settings (folder, prefix, idle lock, new installation in the current folder); Quit. Returns after each action until Quit (Carl, 2026-11-08) | PDR-0048 |
| Q49 | When the passphrase is asked | At the first action in a session that signs or creates a key; kept in the session only, forgotten at Quit (Carl, 2026-11-08) | PDR-0049 |
| Q50 | Idle lock | Asked at setup: whole minutes 1 to 1440 or `never`, 15 proposed. Changeable in Settings without the passphrase, from the next session. When it fires, the passphrase and decrypted keys are forgotten (Carl, 2026-11-08) | PDR-0049 |
| Q51 | Wrong passphrase | Asked again up to three tries, keeping the action's answers; then the action is cancelled, nothing stored, back to the menu (Carl, 2026-11-08) | PDR-0049 |
| Q52 | No installation found | Names the folders checked; offers setup, restore from a backup, or quit; setup or restore continues into the menu (Carl, 2026-11-08) | PDR-0048 |
| Q53 | A bad answer at a question | The rule is named and that question alone asked again, earlier answers kept; every question can cancel the action, storing nothing; proposed or current value shown, an empty answer accepts it (Carl, 2026-11-08) | PDR-0048 |
| Q54 | Optional inputs | Each asked in turn, an empty answer skips; features asked one by one from the definitions with their type; at reissue the current value is shown and an empty answer keeps it (Carl, 2026-11-08) | PDR-0048 |
| Q55 | Confirm before signing | Issue, reissue, renew and resend show a summary and ask whether to sign, yes proposed; no stores nothing (Carl, 2026-11-08) | PDR-0048 |
| Q56 | Getting a key out | Shown once on screen, then offered to save to a file the vendor names, under the never-replace rule (Carl, 2026-11-08) | PDR-0048 |
| Q57 | Second installation | Settings offers a new installation in the current folder, by setup or restore; refused in the home folder or a folder holding one; the session continues with it (Carl, 2026-11-08) | PDR-0048 |
| Q58 | Stale rows | Q1, Q7, Q29, Q33, Q35, Q36, Q41 and Q42 marked amended by Q46 to Q57 (Carl, 2026-11-08) | PDR-0048, PDR-0049 |

## Open questions

None. Q37 to Q58 were raised in `opsx:explore` (Carl, 2026-11-08) and are settled above. Q37,
Q38 and Q40 use the text rule from `tighten-text-rule` (PDR-0042); this change waits for it.

## Decisions

### Components

```
  vendor / script
        |  args, keyboard (prompts, hidden passphrase), current directory, home
        v
  +-- ucl (src/Umbraco.Community.Licensing.Tool) --------------------------+
  |  Program --> ToolApp.Run(args, ToolEnvironment)                       |
  |                 |  System.CommandLine root: setup folder prefix       |
  |                 |                   product license key backup        |
  |                 v                                                      |
  |  Commands/*  --uses-->  ToolConsole (in, out, err, person present?,    |
  |     |                    hidden read)                                  |
  |     +--> Installation (folder resolve, open + migrate ToolDbContext)  |
  |     +--> KeyProtection (passphrase check, encrypt / decrypt PEM)      |
  |     +--> Issuing (records -> LicenseRequest, FeatureDefinitions,      |
  |     |             ExpirySuggestion)                                    |
  |     +--> TypedValues (text rule via library, email, order ref,        |
  |     |                 match key, passphrase rule)                      |
  |     +--> NamedFiles (output and import path checks, ask again)        |
  |     +--> TrustedKeys (list per product), SignedExport (CSV)           |
  |     +--> Backup (snapshot + manifest -> zip; zip -> new folder)       |
  +----------------|-------------------------------------------------------+
                   | project reference
                   v
         Umbraco.Community.Licensing (public API unchanged)

  folder (current directory or ~/.ucl):  ucl.db   <- the whole installation
```

`ToolEnvironment` carries the console (with its person-present flag), `TimeProvider`, home
folder, current directory and the PBKDF2 iteration count, so every command runs in-process in tests
against a temp folder.

### Command surface

No global options other than `--help` and `--version`. No environment settings are read
(ADR-0017).

```
  ucl setup
  ucl folder show
  ucl prefix show | ucl prefix set LIC|<prefix>|none
  ucl product add <product-id> --name <display name>
  ucl product rename <product-id> --name <display name>
  ucl product feature add <product-id> <feature> --type switch|number|text
  ucl product list
  ucl license issue <product-id> --expires <date|instant|perpetual> [--feature name=value]...
                    [--display-name] [--vendor-tag] [--order-ref] [--email]
  ucl license reissue <reference> [--expires <...|suggested>] [--feature name=value]...
                    [--remove-feature name]... [--display-name] [--vendor-tag] [--order-ref] [--email]
  ucl license renew <reference> [--expires <...|suggested>] [--order-ref] [--email]
  ucl license resend <reference>
  ucl license list [--product] [--reference] [--order-ref] [--email]
  ucl license show <reference>
  ucl license customer <reference> [--order-ref <v> | --clear-order-refs] [--email <v> | --clear-email]
  ucl key status [--without-standby]
  ucl key add-standby <product-id|shared>
  ucl key switch <product-id|shared> --reason routine|loss|leak [--yes]
  ucl key trusted <product-id>
  ucl key released <product-id>
  ucl key reissues-sent <signing-key-id>
  ucl key export <signing-key-id> --out <file.csv>
  ucl backup export --out <file.zip>
  ucl backup import <file.zip>
```

Feature values follow the product's definition: switch `name=on|off`, number `name=500`, text
`name=eu`. `off` leaves a switch out of the key at issue and removes it at reissue, as
`--remove-feature`. A command giving one feature twice, or giving it and removing it, exits 1
naming the feature (PDR-0044). Expiry: `2027-03-01` (date, PDR-0007), `2027-03-01T23:59:59Z`, `perpetual`. A missing
input is prompted. With stdin redirected (nobody present, ADR-0017) every command except bare
`ucl`, `--help` and `--version` refuses with exit 1 before doing anything. Bare `ucl` prints help
to stdout and exits 0. The passphrase is read only at the hidden prompt. `ucl setup` takes no
options; the folder is asked: `~/.ucl` (proposed) or the
current directory, with a warning when the current directory is home; `~/.ucl` is created when missing; other files
in the chosen folder are left alone. The closing message ends with the next steps:
`ucl product add <product-id> --name <name>` (per-product mode: notes it creates the signing key
and asks the passphrase), then `ucl key trusted <product-id>`. `--order-ref` on issue, reissue and renew sets the new issue entry's order reference; on
`license customer` it corrects the latest entry's. `prefix set` needs no passphrase. Prompts and
messages go to stderr; the result (key string, file path, list) to stdout. Every command that opens
an installation first writes `Using <full path>` to stderr, then, when the current directory is
used and `<home>/.ucl` also holds `ucl.db`, a note naming it as not used. No installation found:
exit 1 naming the folders checked, then `Run setup now? [y/N]`, starting setup on yes; setup
started this way ends after its closing message with a generic line telling the vendor to run
their command again, exits 0, and does not run the original command. `folder show` prints the
resolved folder to stdout. `backup import` asks for the target as setup does. A path for
`--out` or `backup import` that fails the named-file checks (ADR-0019) is reported with its full
path and asked again at the prompt, also when it was given on the command.
Exit codes: 0 done, 1 refused (validation, wrong passphrase, missing installation), 2 usage
error.

### Packaging and release — ADR-0011

`PackAsTool`, `ToolCommandName` `ucl`, `PackageId` `Umbraco.Community.Licensing.Tool`, imports
`Version.props`, packs `docs/vendor-tool.md` as its README and `assets/icon.png`, and
`THIRD-PARTY-NOTICES.md` at the package root, as the library does: the tool ships the library and
its generated Unicode table (ADR-0018). `release.yml` packs both projects into the pack folder and
pushes `*.nupkg`; a step installs the tool from the pack folder into a temp tool path and runs
`ucl --version` before the push. Amends ADR-0008.
**Package IDs** then yields both IDs, so the GitHub Release links both packages with no change to
`github-release.mjs` or job `github-release` (ADR-0016; release-process: One link per published
package).

### Command line and console — ADR-0012, ADR-0017

System.CommandLine 2.0 for parsing, help and validation. Prompts with BCL `Console`. A person is
present when stdin is not redirected; redirected stdout or stderr does not count. Prompts on
stderr so stdout can be redirected; hidden passphrase read with `Console.ReadKey(intercept:
true)`, the only passphrase source. The tool does not set `Console.OutputEncoding`; key strings
are ASCII.

### Database and folder — ADR-0013

EF Core 10 with SQLite, one file `ucl.db` in the folder; the folder holds an installation when
`ucl.db` exists. Schema by EF migrations, applied on open in a transaction; a database holding a
migration the running tool does not know is refused ("update the tool"). Every connection sets
`PRAGMA secure_delete = ON` and `journal_mode = DELETE`, so a cleared email leaves no copy in free
pages or a WAL file (vendor-tool-licenses: Customer details). Folder precedence (ADR-0017): the
current directory when it holds `ucl.db` (parents not searched), then `<home>/.ucl`, home from
`Environment.SpecialFolder.UserProfile`; paths shown with `Path.GetFullPath`. Nothing is stored
outside the folder. No stored value holds an absolute path or anything OS-specific; encrypted
PEMs are stored with `\n` line endings; backup zip entries use `/` (PDR-0040).

Tables:

| Table | Columns (key first) |
|---|---|
| `Settings` | `Name`, `Value` — prefix, signing-key mode, standby on, passphrase check (salt, iterations, hash) |
| `Products` | `ProductId`, `DisplayName`, `CreatedUtc` |
| `FeatureDefinitions` | `ProductId` + `Name`, `Type`, `AddedUtc` |
| `SigningKeys` | `SigningKeyId`, `ProductId` (null in shared mode), `PublicKey` (export line), `EncryptedPrivateKeyPem`, `Role` (active / standby / replaced), `CreatedUtc`, `ReplacedUtc`, `ReplacedReason`, `InReleaseUtc`, `ReissuesSentUtc` |
| `Licenses` | `Reference` (10 characters), `ProductId`, `CustomerEmail`, `CustomerEmailKey`, `CreatedUtc` |
| `IssueEntries` | `Id`, `Reference`, `Kind` (issue / reissue / renewal / resend), `IssuedUtc`, `ExpiresUtc` (null = perpetual), `DisplayName`, `VendorTag`, `FeaturesJson`, `OrderRef`, `OrderRefKey`, `KeyIdentifier`, `SigningKeyId` |

`KeyIdentifier` keeps the prefix each entry was issued with, so a prefix change needs no data
change. `CustomerEmailKey` and `OrderRefKey` hold the match key (ADR-0019) and are indexed for
finding; each is set, corrected and cleared together with its value.

No column holds a key string. Timestamps are UTC. Every record is complete without the folder's
other files and readable with a plain SQLite connection (PDR-0046).

### Typed values and named files — ADR-0019

- The library project adds `InternalsVisibleTo` for the tool, and `ContentRules.CheckText`
  changes from private to internal. No public type or behaviour changes.
- Product display name: `ContentRules.CheckDisplayName` at `product add` and `product rename`.
- Customer email: `CheckText` (field `customerEmail`, maximum 254). Then at least 3 characters,
  exactly one `@` with text before and after, and no U+0020.
- Order reference: `CheckText` (field `orderRef`, maximum 64).
- A refused value names the field and its rule and stores nothing.
- Match key: `Normalize(NormalizationForm.FormC).ToLowerInvariant()`; finding compares keys for
  equality.
- Passphrase rule at setup: every character U+0020 to U+007E, 15 to 256, first and last not
  U+0020.
- Output files (backup export, key export): full path from `Path.GetFullPath`. The tool asks
  again when the path is an existing file, a folder, or has a missing parent folder. The file is
  opened with `FileMode.CreateNew`.
- Import path: asked again when it is missing or a folder.

### Private keys — ADR-0014

Create: `SigningKeyPair.Create()` → `ExportPem()` → `ECDsa.ImportFromPem` →
`ExportEncryptedPkcs8PrivateKeyPem(passphrase, PBES2 AES-256-CBC, PBKDF2-HMAC-SHA256, 600,000
iterations)`. Use: `ECDsa.ImportFromEncryptedPem` → `ExportPkcs8PrivateKeyPem` →
`SigningPrivateKey.FromPem`, disposed when the command ends. The passphrase is checked first
against the `Settings` passphrase check (PBKDF2-SHA256, 16-byte salt, same iteration count,
`CryptographicOperations.FixedTimeEquals`), so a mistyped passphrase in per-product mode cannot
encrypt a new key under a different passphrase. Tests pass a low iteration count through
`ToolEnvironment`; the count is stored with each PEM and check, so decrypting reads it back.

### Backup and export — ADR-0015

Export: `VACUUM INTO` a temp file (consistent snapshot, no free pages), then a zip holding
`manifest.json` (`format` 1, `toolVersion`, `lastMigration`, `takenUtc`) and `ucl.db`. Import: read
the manifest; refuse an unknown format, a missing entry, or a `lastMigration` the tool does not
know; check the target folder has no `ucl.db`; show `takenUtc` and the warning; extract;
`PRAGMA integrity_check`; apply pending migrations. Backup export and key export write the
named file under the named-file checks (ADR-0019). Signed-key export: CSV (RFC 4180, UTF-8, header
`reference,key_identifier,product,issued_utc,expires_utc`, `perpetual` for no expiry).

### Expiry suggestion

Pure function over the license's issue entries, the current expiry and the kind of reissue.
Term: take the last `renewal` entry R and the entry before it P; start = max(P.ExpiresUtc,
R.IssuedUtc), end = R.ExpiresUtc. With no renewal entry: start = first entry's `IssuedUtc`, end =
its `ExpiresUtc`. Renewal of a time-limited license: if start and end share day of month (a day
past a month's end counting as its last day) and time of day, add the same number of calendar
months to the current expiry (clamping to month end); otherwise add `end - start`. Perpetual:
perpetual. Other reissues: current expiry. `--expires suggested` takes it without a prompt.

### Trusted keys

Per product (or the shared key set): active, standby, every replaced key with reason routine or
loss, and a key replaced for leak while `ReissuesSentUtc` is null. `key released` sets
`InReleaseUtc` on every listed key that lacks it. `key switch` warns and asks `[y/N]` when the key
becoming active has no `InReleaseUtc`: a standby not yet released, or the new key pair created
when there is no standby (it never has one), for every reason (PDR-0047). Declining writes
nothing and exits 1; `--yes` gives the answer on the command.

### Ownership

| Path | Owner |
|---|---|
| `src/Umbraco.Community.Licensing.Tool/**`, `tests/Umbraco.Community.Licensing.Tool.Tests/**` | worker-backend (under `src/**`, `tests/**`) |
| `Umbraco.Community.Licensing.slnx` | worker-backend, for the two new project entries |
| `src/Umbraco.Community.Licensing/Umbraco.Community.Licensing.csproj` (`InternalsVisibleTo` the tool) and `src/Umbraco.Community.Licensing/Format/ContentRules.cs` (`CheckText` internal) | worker-backend (under `src/**`). No public API or behaviour change (ADR-0019) |
| `.github/workflows/release.yml` | Shared (CI): assigned to worker-backend in the brief |
| `.config/dotnet-tools.json` (new top-level path, `dotnet-ef` for creating migrations) | Shared: assigned to worker-backend in the brief. `dotnet-ef` approved by Carl, 2026-10-28 |
| `docs/vendor-tool.md`, `docs/releasing.md`, `README.md`, records | Architect |

## Test strategy

| Level | What | How |
|---|---|---|
| Unit | Expiry suggestion (every Suggested expiry scenario plus month-end and leap cases), folder precedence, trusted list, CSV quoting, feature value parsing (including `off` and contradictions), passphrase check, passphrase rule, email and order reference rules, match key (case, both accent forms, spaces, no partial match), named-file checks | xUnit, test names follow spec scenarios |
| Integration | Every command scenario in the five vendor-tool specs, in-process against a temp folder with a real SQLite file, scripted console with a person present (and one test per refusal with nobody present), fixed `TimeProvider`, low PBKDF2 count | xUnit; one temp folder per test |
| Integration | Issued keys verify with the library's `LicenseEvaluator` trusting the tool's `key trusted` lines | xUnit |
| Integration | No key string in the folder: issue several licenses, scan every byte of `ucl.db` for each key string; cleared email, order references and their match keys absent from the file | xUnit |
| Integration | Records complete without the folder: read every table with a plain SQLite connection (no EF Core) and reissue a license from those values alone; no value holds the folder path, home path or machine name (PDR-0046) | xUnit |
| Integration | Encrypted PEM opens with `ECDsa.ImportFromEncryptedPem` and the passphrase; not without | xUnit |
| Integration | Backup round trip into a new folder; refusals; newer-version manifest | xUnit |
| Integration | Portability: a folder copied and a backup restored into a different temp home work; no stored value contains the source folder's path | xUnit |
| Integration | Production iteration count: one test runs a single encrypt/decrypt at 600,000 to confirm it completes (time printed, not asserted) | xUnit |
| Package | Pack the tool, install it from the pack folder into a temp tool path, run `ucl --version` | `release.yml` step; locally by the worker |
| Observed | Stored PEM opened with `openssl pkey` and the passphrase | Carl, once, recorded in tasks.md |
| Observed | Full suite on macOS, Linux and Windows before archive (FR-2, PDR-0038) | Carl, recorded in tasks.md |
| Observed | Guided use in Windows PowerShell 5.1, PowerShell 7, Windows Terminal and Command Prompt: setup, hidden passphrase, redirects, non-ASCII home path, nobody-present refusal | Carl, recorded in tasks.md |
| Observed | Folder copy and backup restore between operating systems (PDR-0040) | Carl, recorded in tasks.md |

Validation for every block: `dotnet build Umbraco.Community.Licensing.slnx`,
`dotnet test --solution Umbraco.Community.Licensing.slnx`,
`node --test "tools/release/*.test.mjs"`.

## Risks / Trade-offs

- [Creating migrations needs the `dotnet-ef` CLI] → Added as a pinned local tool in
  `.config/dotnet-tools.json` (approved by Carl, 2026-10-28; tasks.md 2.2). Alternative rejected:
  `EnsureCreated` with no migrations, which cannot upgrade a vendor's database in a later
  version.
- [Decrypted PEM lives briefly as a .NET string, which cannot be zeroed] → Accepted for this
  bar; lifetime is one command. `pre-1.0-considerations.md` C2 (`FromECDsa`) would remove the
  string; not in this change.
- [Passphrase typed once per license; no batch] → Accepted for guided mode (PDR-0039);
  FR-22 and FR-20.
- [Windows PowerShell 5.1 writes a redirected key file as UTF-16] → Documented in the guide
  (PDR-0039); the tool cannot control the shell.
- [Non-ASCII home path shown as `?` in a legacy-code-page Command Prompt] → Accepted; the tool
  does not change the console code page (ADR-0017); checked by Carl (tasks.md 13.4).
- [Stderr redirected hides the prompts while the tool waits] → Person-present detection reads
  stdin only; the guide says not to redirect stderr.
- [Cloud-synced folder chosen anyway] → Setup warns; the tool cannot detect sync reliably on
  every OS.
- [Lost passphrase] → Keys unrecoverable (FR-2 loss case). Setup and the guide say to keep it in a
  password manager, apart from backups.
- [Older backups hold cleared emails] → Stated in the guide (PDR-0033).
- [600,000 PBKDF2 iterations cost about half a second per command that signs] → Acceptable; once
  per command.
- [First push of a new package ID under Trusted Publishing] → The policy is per repository and
  workflow (ADR-0008); if nuget.org refuses, Carl publishes the first tool version with a
  short-lived API key and records it in follow-ups.md.
- [Lockstep versions: a tool-only fix bumps the library version] → Accepted in beta (ADR-0011).
- [System.CommandLine API differs between 2.0 betas and 2.0 stable] → Pin the stable 2.0.x
  exactly; the brief names it.
- [Tool uses library internals through `InternalsVisibleTo`] → Safe: the tool package bundles
  the library assembly it was built with, and both release at one version (ADR-0011). A library
  refactor that breaks the tool fails the same build (ADR-0019).
- [`ToLowerInvariant` is simple case mapping, not full case folding: `ß` does not find `SS`] →
  Accepted; rare in emails and order references (ADR-0019).

## Migration Plan

1. Build sections 1 to 12 under the apply-phase team; Carl commits per section.
2. Carl runs the full suite on macOS, Linux and Windows, checks the tool by hand in the Windows
   terminals, moves an installation between operating systems, opens a stored PEM with OpenSSL
   and rehearses a release (section 13).
3. Next release (`0.1.0-beta.7` or later) publishes both packages. Rollback: unlist the tool
   version on nuget.org; the library is unaffected.
