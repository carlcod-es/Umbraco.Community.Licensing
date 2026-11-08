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
every action guided in a menu session (a person at a terminal, PDR-0039, PDR-0048); runs on Windows, macOS and Linux and
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
| R1 | One vendor, one person per installation; menu session only | vendor-tool-setup: One vendor and one person per installation; Menu session; Answers at a prompt | Vendor; site owner (summary before signing) | PDR-0030, PDR-0039, PDR-0048, PDR-0050 |
| R2 | Guided setup; prefix changeable later | vendor-tool-setup: Guided setup; Changing the prefix | Vendor; site owner (prefix and keys set once, keys stay consistent) | PDR-0030, PDR-0023 |
| R3 | Folder location, moving, second installation | vendor-tool-setup: Folder location; New installation in the current folder; Moving an installation | Vendor | PDR-0031, PDR-0040, PDR-0041, PDR-0048, PDR-0052 |
| R4 | Products and add-only feature definitions | vendor-tool-products | Vendor; site owner (no feature silently lost, PDR-0016) | PDR-0036 |
| R5 | Issue, reissue, renew, resend from records | vendor-tool-licenses | Vendor; site owner (renewal keeps paid-for days; lost key resent; the key they send back finds their license) | PDR-0033, PDR-0034, PDR-0021, PDR-0052 |
| R6 | Records without key strings; customer fields editable and clearable | vendor-tool-licenses | Site owner (copied backup holds no usable key; erasure honoured); vendor | PDR-0033, PDR-0014 |
| R7 | Private keys encrypted; never exported; passphrase kept for a session with an idle lock | vendor-tool-signing-keys: Private keys encrypted with the passphrase. vendor-tool-setup: Idle lock | Vendor; site owner (no forged keys from a copied folder or an unattended session) | PDR-0032, PDR-0049, PDR-0052 |
| R8 | Standby, switch, trusted keys, export by signing key | vendor-tool-signing-keys | Vendor; implementor (release trusts the right keys); site owner (keys keep working after rotation, in shared mode for every product) | PDR-0035, PDR-0015, PDR-0051 |
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
| Q4 | Folder location | Propose `~/.ucl`, show the full path, vendor may override; setup names the content, backup advice and sync warning (Carl, 2026-10-28); folder offer and resolution amended by Q29. Amended by Q69: no installation directly in the home folder; started there, only `~/.ucl` is offered | PDR-0031, PDR-0052 |
| Q5 | Moving and backups | Folder portable by copying; zip export and import (Carl, 2026-10-28) | PDR-0031 |
| Q6 | SQL Server | Dropped; a database file inside the folder only (Carl, 2026-10-28). Was an FR-2 constraint | ADR-0013 |
| Q7 | Protecting private keys | Encrypted with a passphrase set at setup; nothing else encrypted; asked once per command that signs or creates a key; batched runs supply it without a prompt (Carl, 2026-10-28). Amended by Q33: always typed at the hidden prompt. Amended by Q49: asked at the first signing action, kept for the session | PDR-0032, PDR-0039, PDR-0049 |
| Q8 | Private-key export | None in this change; FR-11 (Carl, 2026-10-28) | PDR-0032 |
| Q9 | Suggested expiry on renewal | Vendor types the expiry; tool suggests current expiry plus the last term (Carl, 2026-10-28). Term and months versus exact time: see Q25 | PDR-0034 |
| Q10 | Correcting or erasing customer data | Edit or clear order reference and email; no copy of a cleared value kept; records never deleted (Carl, 2026-10-28) | PDR-0033 |
| Q11 | Trusted keys for a release | Switch asks the reason; trusted list per product; leaked key stays listed until reissues confirmed sent (Carl, 2026-10-28). Amended by Q66: release confirmed per product | PDR-0035, PDR-0051 |
| Q12 | Storing key strings | Not stored; resend is reissue unchanged (Carl, 2026-10-28) | PDR-0033 |
| Q13 | Backup import over an installation | Refused; import only into a folder with no installation; backup date warning (Carl, 2026-10-28) | PDR-0031 |
| Q14 | Operators; passphrase change | One person, no accounts; passphrase change out, FR-12 (Carl, 2026-10-28) | PDR-0030, PDR-0032 |
| Q15 | Standby key stored apart (FR-4 issue 2) | Kept with the active key under the same passphrase; storing apart is FR-15 (Carl, 2026-10-28) | PDR-0032 |
| Q16 | Standby key after setup (FR-4 issues 1, 4) | Can be added later; marked "not yet in a release" until confirmed; switch warns; list of products without a standby key (Carl, 2026-10-28). Amended by Q45: switch also warns with no standby. Amended by Q66: "not yet in a release" held per product | PDR-0035, PDR-0047, PDR-0051 |
| Q17 | Changing feature definitions | Add-only; product ID fixed; display name editable; retiring a feature is FR-14 (Carl, 2026-10-28) | PDR-0036 |
| Q18 | Distribution, packaging, versioning | .NET tool package `Umbraco.Community.Licensing.Tool`, command `ucl`, .NET SDK prerequisite; same version as the library, released together (Carl, 2026-10-28) | ADR-0011 |
| Q19 | Command line and prompts | System.CommandLine; BCL console prompts; Spectre.Console is FR-13 (Carl, 2026-10-28). Amended by Q59: no System.CommandLine | ADR-0012, ADR-0020 |
| Q20 | Database and folder resolution | EF Core + SQLite in the folder; option, environment, pointer file, default (Carl, 2026-10-28); resolution replaced by Q29 | ADR-0013 |
| Q21 | Private-key encryption format | Encrypted PKCS#8 PEM, PBES2 AES-256-CBC + PBKDF2-SHA256 (Carl, 2026-10-28) | ADR-0014 |
| Q22 | Backup and export formats | Zip of a consistent database snapshot and a manifest; CSV export (Carl approved T6, 2026-10-28) | ADR-0015 |
| Q23 | Prefix and signing-key mode after setup | Prefix changeable: new issues and reissues use the current prefix, existing licenses keep theirs until reissued, typed references per PDR-0023. Signing-key mode fixed; changing it is FR-17 (Carl, 2026-10-28) | PDR-0030 |
| Q24 | Passphrase rule | At least 15 characters, length only (NIST SP 800-63B rev 4) (Carl, 2026-10-28) | PDR-0032 |
| Q25 | Length of the last term | Length the last renewal granted, from the later of the previous expiry and the renewal time to the expiry it set; first issue's term when never renewed; other reissues do not change it. Calendar months when whole months, exact time otherwise (Carl, 2026-10-28) | PDR-0034 |
| Q26 | Where customer fields are held | Email per license; order reference per issue entry; only the latest entry's order reference correctable; clearing removes all of a license's order references at once. Warning on a reused order reference is FR-18 (Carl, 2026-10-28) | PDR-0033 |
| Q27 | Proposed signing-key mode at setup | Per-product proposed; shared offered. Standby keys follow the mode: one per product signing key, or one for the shared key (Carl, 2026-10-29) | PDR-0030 |
| Q28 | Scripted setup | Setup only by a person at a terminal; no answers from options, environment or redirected input; refused with nobody present. Every other action stays scriptable. Passphrase always typed twice (Carl, 2026-10-29). Amended by Q33, Q47: guided, then menu session only; no action is scriptable | PDR-0030 |
| Q29 | Finding the installation; more than one | Amended by Q35. First of: `--folder`, the current folder when it holds an installation, `~/.ucl`. Setup offers `~/.ucl` (proposed) or the current folder. Nothing remembered; no folder environment setting. Each action states the folder used. No installation: refuse naming the folders checked; at a terminal, offer setup (Carl, 2026-10-29). Amended by Q52, Q57 | PDR-0031, ADR-0013, PDR-0048 |
| Q30 | Folder state at setup | `~/.ucl` created when missing; a folder holding other files but no installation accepted; only an existing installation refused (Carl, 2026-10-29) | PDR-0031 |
| Q31 | Next step after setup | Closing message ends with the next steps by mode: `product add` (per-product: creates the signing key, asks the passphrase), then `key trusted`. The guide gives the same first steps (Carl, 2026-10-29). Amended by Q48: next steps named as menu entries (Products > Add a product, Signing keys > Trusted keys) | PDR-0031 |
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
| Q45 | Switch when the new active key is not in a release | Warn and ask for confirmation whenever the key becoming active is not yet in a release: a standby not yet in a release, or a new key pair when there is no standby. Any reason, leak included; declining changes nothing (Carl, 2026-11-08). Amended by Q66: not in a release for every product it signs for, the products named | PDR-0047, PDR-0051 |
| Q46 | Session form | A menu session with numbered menus and line-by-line questions; FR-23 taken in. Selection lists, colour and full screen stay FR-13 (Carl, 2026-11-08) | PDR-0048 |
| Q47 | Commands alongside the session | Session only: `ucl` opens the session; only help and version remain; no inputs on the command (Carl, 2026-11-08) | PDR-0048 |
| Q48 | Menu | Products (add, rename, add feature, list); Licenses (issue, reissue, renew, resend, find and list, show, customer details); Signing keys (status, add standby, switch, trusted keys, release confirmed, reissues sent, export by key); Backup (export, restore into the current folder); Settings (folder, prefix, idle lock, new installation in the current folder); Quit. Returns after each action until Quit (Carl, 2026-11-08) | PDR-0048 |
| Q49 | When the passphrase is asked | At the first action in a session that signs or creates a key; kept in the session only, forgotten at Quit (Carl, 2026-11-08) | PDR-0049 |
| Q50 | Idle lock | Asked at setup: whole minutes 1 to 1440 or `never`, 15 proposed. Changeable in Settings without the passphrase, from the next session. When it fires, the passphrase and decrypted keys are forgotten (Carl, 2026-11-08). Amended by Q68: anything typed restarts the idle time, menu choices included; Q67: idle time read again when the session moves installation | PDR-0049, PDR-0052 |
| Q51 | Wrong passphrase | Asked again up to three tries, keeping the action's answers; then the action is cancelled, nothing stored, back to the menu (Carl, 2026-11-08) | PDR-0049 |
| Q52 | No installation found | Names the folders checked; offers setup, restore from a backup, or quit; setup or restore continues into the menu (Carl, 2026-11-08) | PDR-0048 |
| Q53 | A bad answer at a question | The rule is named and that question alone asked again, earlier answers kept; every question can cancel the action, storing nothing; proposed or current value shown, an empty answer accepts it (Carl, 2026-11-08) | PDR-0048 |
| Q54 | Optional inputs | Each asked in turn, an empty answer skips; features asked one by one from the definitions with their type; at reissue the current value is shown and an empty answer keeps it (Carl, 2026-11-08) | PDR-0048 |
| Q55 | Confirm before signing | Issue, reissue, renew and resend show a summary and ask whether to sign, yes proposed; no stores nothing (Carl, 2026-11-08) | PDR-0048 |
| Q56 | Getting a key out | Shown once on screen, then offered to save to a file the vendor names, under the never-replace rule (Carl, 2026-11-08) | PDR-0048 |
| Q57 | Second installation | Settings offers a new installation in the current folder, by setup or restore; refused in the home folder or a folder holding one; the session continues with it (Carl, 2026-11-08). Amended by Q67: after a restore the held passphrase is forgotten; after setup the one just set is kept | PDR-0048, PDR-0052 |
| Q58 | Stale rows | Q1, Q7, Q29, Q33, Q35, Q36, Q41 and Q42 marked amended by Q46 to Q57 (Carl, 2026-11-08) | PDR-0048, PDR-0049 |
| Q59 | Console for a menu session | Arguments handled by hand (help, version, anything else refused with exit 2); System.CommandLine removed; everything in the session on stdout; answers by `Console.ReadLine`; menus numbered, `0` Back or Quit (Carl approved removing System.CommandLine, 2026-11-08) | ADR-0020 |
| Q60 | Holding the passphrase for a session | Passphrase held as pinned bytes, never a string; no decrypted key outlives its action; idle lock by a `TimeProvider` timer restarted on each answer plus a check on each use; fake clock written in the test project (Carl, 2026-11-08) | ADR-0021 |
| Q61 | How a question is cancelled | Answer `/cancel` at any question, the hidden prompt included; `/remove` removes a number or text feature at reissue; a value equal to either cannot be entered; Ctrl+C cancelling an action is FR-27 (Carl, 2026-11-08). Amended by Q65: `/remove` also for display name and vendor tag | ADR-0020 |
| Q62 | Switch confirmation declined | Nothing changes and the Signing keys menu is shown, not a failure exit (Carl, 2026-11-08) | PDR-0050 |
| Q63 | Who counts as present | Input and output both a terminal; a redirected output is refused like nobody present (Carl, 2026-11-08) | PDR-0050, ADR-0020 |
| Q64 | Passphrase typed at setup | Kept for the session in both signing-key modes, under the idle lock; the closing message says the first product's key uses it (Carl, 2026-11-08) | PDR-0050, ADR-0021 |
| Q65 | Removing a display name or vendor tag at reissue | Allowed with the remove answer, as for number and text features (Carl, 2026-11-08) | PDR-0050 |
| Q66 | Release mark in shared mode | Held per product: Release confirmed is given for one product and marks the listed keys for that product only; the switch warns when the key becoming active is not in a release for every product it signs for, naming them; Status shows them beside each standby; a product added later and a new standby start with none. Only matters when a standby exists (Carl, 2026-11-08) | PDR-0051, ADR-0022 |
| Q67 | Passphrase when the session moves installation | Restored installation: the held passphrase is forgotten and the idle time read from it; new installation by setup: the passphrase just set is kept (Carl, 2026-11-08) | PDR-0052, ADR-0022 |
| Q68 | What restarts the idle lock | Anything typed, menu choices included (Carl, 2026-11-08) | PDR-0052, ADR-0022 |
| Q69 | Installation in the home folder | Never, from any entry point; started in the home folder, setup and restore at the startup offer offer only `~/.ucl`; replaces the home-folder warning (Carl, 2026-11-08) | PDR-0052 |
| Q70 | Choosing a license | Every license question takes the reference under the prefix rule or a whole key string signed by one of the installation's keys, expired or not; only its reference is used, nothing kept; a key not signed by the installation or with no record refused and asked again; no passphrase (Carl, 2026-11-08) | PDR-0052, ADR-0022 |
| Q71 | Stale rows | Q4, Q11, Q16, Q28, Q31, Q45, Q50 and Q57 marked amended by Q33, Q47, Q48 and Q66 to Q69 (Carl, 2026-11-08) | PDR-0051, PDR-0052 |

## Open questions

None. Q37 to Q58 were raised in `opsx:explore`, Q59 to Q65 in `opsx:update`, and Q66 to Q71 in
an `opsx:explore` review of the menu session (Carl, 2026-11-08); all are settled above. Q37, Q38 and Q40 use the text rule from
`tighten-text-rule` (PDR-0042); this change waits for it.

## Decisions

### Components

```
  vendor at a terminal
        |  args (help / version only), keyboard (answers, hidden passphrase),
        |  current directory, home
        v
  +-- ucl (src/Umbraco.Community.Licensing.Tool) --------------------------+
  |  Program --> ToolApp.Run(args, ToolEnvironment)                       |
  |                 |  args check -> person present? -> Startup           |
  |                 v                                                      |
  |  Startup (folder resolve, Using line, no-installation offer)          |
  |     v                                                                  |
  |  Menu loop (main menu, submenus, Back, Quit)                          |
  |     |  runs one action at a time; cancel -> back to its menu          |
  |     v                                                                  |
  |  Actions/*  --uses-->  Questions (proposed value, rule, ask again,    |
  |     |                  lists, yes/no, /cancel, /remove, paths)        |
  |     |                     --> ToolConsole (read line, read hidden,     |
  |     |                         write, write error, person present?)    |
  |     +--> SessionLock (held passphrase bytes, idle timer, zeroing)     |
  |     +--> Installation (folder resolve, open + migrate ToolDbContext)  |
  |     +--> KeyProtection (passphrase check, encrypt / decrypt PEM)      |
  |     +--> Issuing (records -> LicenseRequest, FeatureDefinitions,      |
  |     |             ExpirySuggestion, summary)                           |
  |     +--> TypedValues (text rule via library, email, order ref,        |
  |     |                 match key, passphrase rule, idle lock rule)      |
  |     +--> NamedFiles (output and import path checks)                   |
  |     +--> TrustedKeys (list per product), SignedExport (CSV)           |
  |     +--> Backup (snapshot + manifest -> zip; zip -> new folder)       |
  +----------------|-------------------------------------------------------+
                   | project reference
                   v
         Umbraco.Community.Licensing (public API unchanged)

  folder (current directory or ~/.ucl):  ucl.db   <- the whole installation
```

`ToolEnvironment` carries the console (with its person-present flag), `TimeProvider`, home
folder, current directory and the PBKDF2 iteration count, so a whole session runs in-process in
tests against a temp folder.

### Menu session

`ucl` takes no argument other than `--help` (`-h`, `-?`) and `--version` (ADR-0020). No
environment settings are read (ADR-0017). Startup, in order:

1. Arguments: help or version is written to stdout, exit 0. Any other argument is refused on
   stderr (takes no command other than help and version; used from its menu), exit 2, nothing
   read or written.
2. Person present (neither stdin nor stdout redirected, PDR-0050): otherwise refused on stderr, exit 1, nothing read or
   written.
3. Folder (Database and folder): the current directory when it holds `ucl.db`, else
   `<home>/.ucl`. Found: open it (a database the tool cannot open is refused on stderr, exit 1),
   write `Using <full path>`, and when the current directory is used and `<home>/.ucl` holds
   `ucl.db`, a note naming it as not used. Then the main menu.
4. None found: name both folders checked and offer `1 Set up a new installation`, `2 Restore from
   a backup`, `0 Quit`. Setup or restore asks for the folder (`<home>/.ucl` proposed, or the
   current directory, both in full; only `<home>/.ucl` when the current directory is home, so
   an installation never sits directly in it, PDR-0052), and on
   completion the session writes `Using <full path>` and shows the main menu. Quit exits 0.

The menu tree (numbers as shown; `0` is Back in a submenu, Quit in the main menu):

```
  1 Products        1 Add a product            product ID, display name
                    2 Rename a product         product (list), display name (current shown)
                    3 Add a feature            product (list), feature name, type switch|number|text
                    4 List products
  2 Licenses        1 Issue                    product (list), expiry, display name, vendor tag,
                                               each defined feature in turn, order reference,
                                               customer email -> summary -> sign
                    2 Reissue                  license, expiry (current), display name (current;
                                               /remove), vendor tag (current; /remove), each
                                               defined feature (current; /remove for number or
                                               text), order reference,
                                               customer email (current) -> summary -> sign
                    3 Renew                    license, expiry (suggested), order reference,
                                               customer email (current) -> summary -> sign
                    4 Resend                   license -> summary -> sign
                    5 Find and list            product, license, order reference, customer email
                                               (each optional; all empty lists every license)
                    6 Show a license           license
                    7 Customer details         license, then 1 change email, 2 clear email,
                                               3 correct latest order reference, 4 clear all
                                               order references
  3 Signing keys    1 Status                   only those without a standby key? [y/N]
                    2 Add a standby key        signing key without one (list)
                    3 Switch                   signing key (list: products, or shared), reason
                                               routine|loss|leak, confirmation when the new
                                               active key is not in a release for every
                                               product it signs for (products named)
                    4 Trusted keys             product (list)
                    5 Release confirmed        product (list), list shown, confirm [y/N]
                    6 Reissues sent            signing key replaced for leak (list), confirm [y/N]
                    7 Export by signing key    signing key (list), file path
  4 Backup          1 Export                   file path
                    2 Restore into the current folder
                                               file path (current directory only; refused in the
                                               home folder or a folder holding an installation)
  5 Settings        1 Folder in use            full path, and the unused-installation note
                    2 Prefix                   current shown; new prefix LIC|<prefix>|none
                    3 Idle lock                current shown; minutes 1 to 1440 or never
                    4 New installation in the current folder
                                               1 Set up, 2 Restore from a backup (refused in the
                                               home folder or a folder holding an installation)
  0 Quit
```

Questions (ADR-0020): each shows what it accepts, the proposed or current value (Enter accepts
it) and `/cancel`; a refused answer names the rule and asks that question again, keeping the
action's earlier answers. Lists are numbered; the answer is the number or the ID. Values typed:
expiry `2027-03-01` (date, PDR-0007), `2027-03-01T23:59:59Z` or `perpetual`; switch `on` or
`off` (`off` leaves it out at issue, removes it at reissue, PDR-0044); number and text by the
definition's type; empty skips an optional input at issue and keeps the current value at reissue.
The suggested expiry is the proposed answer at renewal (Expiry suggestion). "license" in the
menu tree is the license question: a reference or a whole key string (Choosing a license).

Setup questions, in order: folder (startup offer only; from Settings the current directory is the
only folder and is not asked), passphrase twice (hidden), prefix (`LIC` proposed), signing-key
mode (per product proposed, or shared), standby keys (`[Y/n]`, recommended), idle lock (15
proposed). Setup creates the folder and database only after the last answer, in one step, so a
cancelled setup leaves nothing behind. The closing message names the full path, the content,
backup advice, the sync warning naming example services, and the next steps as menu entries:
Products > Add a product (per-product mode: creates its signing key under the passphrase just
set), then Signing keys > Trusted keys. In both modes the session keeps the passphrase typed at
setup, under the idle lock (PDR-0050, ADR-0021). The session then writes `Using <full path>` and shows the main menu.

Signing: issue, reissue, renew and resend show the summary (product, reference when known,
expiry, display name, vendor tag, features, customer email, order reference) and ask `Sign this
license? [Y/n]`; no stores nothing and shows the Licenses menu. Then the passphrase when the
session holds none (ADR-0021), the signing, and one transaction writing the record. The key
string is written once, on its own line, then `Save the key to a file? [y/N]`; yes asks for a path
under the named-file checks and writes the key string alone (ADR-0020).

The passphrase is asked, when the session does not hold it, only by actions that sign or create a key: Add a product in per-product
mode, issue, reissue, renew, resend, Add a standby key and Switch. Up to three tries per action
(ADR-0021). Prefix and idle lock changes need no passphrase; an idle lock change is stored and
applies from the next session.

Restore (startup offer, Backup menu, or Settings) asks for the backup's path, shows the date it
was taken and the warning, imports, writes `Using <full path>` and continues the session with the
restored installation (Backup and export). The session then holds no passphrase and reads the
idle time from the restored installation; a new installation made by setup keeps the passphrase
just set (ADR-0022).

Exit codes: 0 Quit, help, version, Quit at the startup offer; 1 nobody present, end of input, a
database the tool cannot open; 2 any other argument.

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

### Console and session — ADR-0020, ADR-0021, ADR-0017

- Arguments handled by hand; no System.CommandLine (ADR-0020, superseding ADR-0012).
- A person is present when neither stdin nor stdout is redirected (`Console.IsInputRedirected`
  and `Console.IsOutputRedirected` both false), checked once after the arguments; a redirected
  stderr does not count (vendor-tool-setup: Menu session, PDR-0050).
- Inside the session everything goes to stdout. stderr carries only the refusals written before
  the session opens.
- Answers read with `Console.ReadLine`, trimmed before their rule is checked; `null` (end of
  input) ends the session with exit 1, storing nothing from the action in progress. Ctrl+C is
  not intercepted.
- `/cancel` (any question) and `/remove` (reissue: display name, vendor tag, number and text
  features) are reserved, whole answer,
  any case.
- Hidden passphrase: `Console.ReadKey(intercept: true)` into a pinned buffer, nothing echoed,
  Backspace and Enter handled, the only passphrase source.
- Each action asks every question first and writes in one transaction at the end; cancelling
  is an exception the menu loop catches.
- Passphrase held for the session as pinned bytes in `SessionLock`; idle lock by a
  `TimeProvider` timer restarted on each line read (menu choices included) and each hidden-prompt
  answer, plus a check on each use; zeroed at Quit, end of input, the end of the run and when
  the session moves to a restored installation, whose idle time is then read; no decrypted key
  outlives its action (ADR-0021, ADR-0022).
- The tool does not set `Console.OutputEncoding`; key strings are ASCII. A key saved to a file is
  ASCII, no byte order mark, no line ending.

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
| `Settings` | `Name`, `Value` — prefix, signing-key mode, standby on, idle lock (minutes or `never`), passphrase check (salt, iterations, hash) |
| `Products` | `ProductId`, `DisplayName`, `CreatedUtc` |
| `FeatureDefinitions` | `ProductId` + `Name`, `Type`, `AddedUtc` |
| `SigningKeys` | `SigningKeyId`, `ProductId` (null in shared mode), `PublicKey` (export line), `EncryptedPrivateKeyPem`, `Role` (active / standby / replaced), `CreatedUtc`, `ReplacedUtc`, `ReplacedReason`, `ReissuesSentUtc` |
| `KeyReleases` | `SigningKeyId` + `ProductId`, `ConfirmedUtc` — a key is in a release for a product when its row exists (ADR-0022) |
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
- Product display name: `ContentRules.CheckDisplayName` at Add a product and Rename a product.
- Customer email: `CheckText` (field `customerEmail`, maximum 254). Then at least 3 characters,
  exactly one `@` with text before and after, and no U+0020.
- Order reference: `CheckText` (field `orderRef`, maximum 64).
- A refused value names the field and its rule and stores nothing.
- Match key: `Normalize(NormalizationForm.FormC).ToLowerInvariant()`; finding compares keys for
  equality.
- Passphrase rule at setup: every character U+0020 to U+007E, 15 to 256, first and last not
  U+0020.
- Idle lock: a whole number 1 to 1440 (ASCII digits only) or `never`, any case.
- Output files (backup export, key export, key saved to a file), always asked at a question:
  full path from `Path.GetFullPath`. The tool asks again when the path is an existing file, a
  folder, or has a missing parent folder. The file is opened with `FileMode.CreateNew`.
- Import path: asked again when it is missing or a folder.

### Private keys — ADR-0014

Create: `SigningKeyPair.Create()` → `ExportPem()` → `ECDsa.ImportFromPem` →
`ExportEncryptedPkcs8PrivateKeyPem(passphrase, PBES2 AES-256-CBC, PBKDF2-HMAC-SHA256, 600,000
iterations)`. Use: `ECDsa.ImportFromEncryptedPem` → `ExportPkcs8PrivateKeyPem` →
`SigningPrivateKey.FromPem`, disposed before the action returns. The passphrase is checked first
against the `Settings` passphrase check (PBKDF2-SHA256, 16-byte salt, same iteration count,
`CryptographicOperations.FixedTimeEquals`), so a mistyped passphrase in per-product mode cannot
encrypt a new key under a different passphrase. Every step takes the passphrase as bytes from
`SessionLock`, never as a string (ADR-0021). Tests pass a low iteration count through
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
perpetual. Other reissues: current expiry. The result is the proposed answer to the expiry
question.

### Trusted keys

Per product (or the shared key set): active, standby, every replaced key with reason routine or
loss, and a key replaced for leak while `ReissuesSentUtc` is null. The products a key signs for
are its `ProductId` in per-product mode and every product in shared mode (ADR-0022). Release
confirmed, for the chosen product, inserts a `KeyReleases` row for every listed key that lacks
one for that product. Switch warns and asks `[y/N]` when the key becoming active lacks a row for
any product it signs for, naming those products: a standby not released for every product, or
the new key pair created when there is no standby (it has no rows), for every reason (PDR-0047,
PDR-0051). Declining writes nothing and shows the Signing keys menu (PDR-0050). Status shows,
beside each standby, the products it lacks a row for. A new standby and a product added later
start with no rows.

### Choosing a license — ADR-0022

One question helper, used by Reissue, Renew, Resend, Show a license, Customer details and the
reference filter of Find and list. The answer is first read as a reference under the prefix
rule (PDR-0023). Otherwise it is evaluated as a key string with `LicenseEvaluator`, trusting
every public key the installation holds for a product (active, standby, replaced for any
reason): once against the first product, and when the result is *WrongProduct* with a
`ClaimedProduct` the installation holds, once against that product. *Valid* or *Expired*:
`License.Reference` chooses the record, or "no record" is said. *SigningKeyNotRecognised*, or a
claimed product not held: "not issued by this installation". *Unreadable*, *NotVerified*,
*NotSupported*: "not a reference or a key". Each refusal asks again. No passphrase; the key
string is not stored or logged (PDR-0033, PDR-0052).

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

The bar is unchanged (production). Every scenario is driven through `ToolApp.Run` as a whole
session: the scripted console holds the answers in order (menu numbers included), a fake
`TimeProvider` (written in the test project, ADR-0021) can be advanced before any answer, and the
transcript of everything written is asserted. A test ends with Quit, so the exit code and the
return to each menu are checked as well.

| Level | What | How |
|---|---|---|
| Unit | Expiry suggestion (every Suggested expiry scenario plus month-end and leap cases), folder precedence, trusted list, CSV quoting, feature value parsing (including `off` and `/remove`), passphrase check, passphrase rule, idle lock rule, email and order reference rules, match key (case, both accent forms, spaces, no partial match), named-file checks, argument handling (help, version, everything else refused) | xUnit, test names follow spec scenarios |
| Unit | Questions: proposed value, rule named and asked again, earlier answers kept, numbered lists by number or ID, yes/no, `/cancel` at a line and at the hidden prompt, end of input | xUnit against a stub action |
| Unit | `SessionLock`: held after unlock, locked when the fake clock passes the idle time with no answer (timer fired, bytes zeroed), each answer, including a menu choice, restarts the timer, check on use when the timer has not run, `never` never locks, zeroed at Quit, at end of input and on moving to a restored installation | xUnit, fake `TimeProvider` |
| Integration | Every scenario in the five vendor-tool specs, as whole sessions in-process against a temp folder with a real SQLite file, a person present, fake `TimeProvider`, low PBKDF2 count; nobody present as one run per refusal (input redirected, output redirected) | xUnit; one temp folder per test |
| Integration | Issued keys verify with the library's `LicenseEvaluator` trusting the lines printed by Trusted keys | xUnit |
| Integration | Release marks in shared mode with two and three products: Status, the switch warning naming products, a product added after confirmation, a new standby with no marks (PDR-0051) | xUnit |
| Integration | License question: reference, pasted key (current, expired, signed by a replaced key, issued before a prefix change), another installation's key, a key with no record, garbage (PDR-0052) | xUnit |
| Integration | No key string in the folder: issue several licenses, scan every byte of `ucl.db` for each key string; cleared email, order references and their match keys absent from the file | xUnit |
| Integration | Records complete without the folder: read every table with a plain SQLite connection (no EF Core) and reissue a license from those values alone; no value holds the folder path, home path or machine name (PDR-0046) | xUnit |
| Integration | Encrypted PEM opens with `ECDsa.ImportFromEncryptedPem` and the passphrase; not without | xUnit |
| Integration | Backup round trip into a new folder from each entry point (startup offer, Backup menu, Settings); refusals; newer-version manifest | xUnit |
| Integration | Portability: a folder copied and a backup restored into a different temp home work; no stored value contains the source folder's path | xUnit |
| Integration | Production iteration count: one test runs a single encrypt/decrypt at 600,000 to confirm it completes (time printed, not asserted) | xUnit |
| Package | Pack the tool, install it from the pack folder into a temp tool path, run `ucl --version` | `release.yml` step; locally by the worker |
| Observed | Stored PEM opened with `openssl pkey` and the passphrase | Carl, once, recorded in tasks.md |
| Observed | Full suite on macOS, Linux and Windows before archive (FR-2, PDR-0038) | Carl, recorded in tasks.md |
| Observed | Menu session in Windows PowerShell 5.1, PowerShell 7, Windows Terminal and Command Prompt: setup, hidden passphrase, line editing at a question, `/cancel`, a key saved to a file, non-ASCII home path, refusal with input or output redirected | Carl, recorded in tasks.md |
| Observed | Idle lock in a real terminal: left past the idle time, the next signing asks again | Carl, once, recorded in tasks.md |
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
  bar; lifetime is one signing. `pre-1.0-considerations.md` C2 (`FromECDsa`) would remove the
  string; not in this change.
- [Passphrase held in memory while the session is unlocked] → Pinned bytes, zeroed at the idle
  lock, Quit and end of input (ADR-0021). With `never`, only quitting clears it; the guide says to
  quit when leaving the terminal (PDR-0049).
- [Ctrl+C or a closed terminal ends the process without zeroing] → Memory goes with the process;
  an action interrupted before its last question stores nothing (one transaction at the end).
- [A value of exactly `/cancel` or `/remove` cannot be entered] → Accepted; stated in the guide
  (ADR-0020).
- [Non-ASCII home path shown as `?` in a legacy-code-page Command Prompt] → Accepted; the tool
  does not change the console code page (ADR-0017); checked by Carl (tasks.md 13.4).
- [Cloud-synced folder chosen anyway] → Setup warns; the tool cannot detect sync reliably on
  every OS.
- [Lost passphrase] → Keys unrecoverable (FR-2 loss case). Setup and the guide say to keep it in a
  password manager, apart from backups.
- [Older backups hold cleared emails] → Stated in the guide (PDR-0033).
- [600,000 PBKDF2 iterations cost about half a second per unlock and per signing] → Acceptable
  for a person at a terminal (ADR-0021).
- [First push of a new package ID under Trusted Publishing] → The policy is per repository and
  workflow (ADR-0008); if nuget.org refuses, Carl publishes the first tool version with a
  short-lived API key and records it in follow-ups.md.
- [Lockstep versions: a tool-only fix bumps the library version] → Accepted in beta (ADR-0011).
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
