## Context

See proposal.md (Why). Current state: one library project `src/Umbraco.Community.Licensing`
(BCL only, ADR-0003), one xUnit v3 test project, `Version.props` imported by the library only
(ADR-0009), `release.yml` packs and pushes the library (ADR-0008), then job `github-release`
creates a GitHub Release with one nuget.org link per package ID its **Package IDs** step reads from
the pack folder (ADR-0016). `0.1.0-beta.6` is on nuget.org.

The library already gives the tool everything it signs and checks with, so this change needs no
library change:

| Tool need | Library API |
|---|---|
| Create a key pair; get its PEM and public line | `SigningKeyPair.Create`, `SigningPrivateKey.ExportPem`, `SigningPublicKey.Export` |
| Sign from a decrypted PEM | `SigningPrivateKey.FromPem` |
| Prefix from setup | `KeyPrefix.Of`, `KeyPrefix.None`, `LicenseIssuer(prefix)` |
| Issue, and reissue from a record (reference typed into a request, PDR-0023) | `LicenseIssuer.Issue(LicenseRequest, ...)` with `Reference` set |
| Feature checks against definitions | `FeatureDefinitions` (PDR-0016) |
| Features and expiry from stored values | `FeatureList.Of`, `LicenseFeature`, `LicenseExpiry.At` / `On` / `Perpetual` |

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
every command scriptable; folder portable by copying; no library change; tool and library
released from one tested build.

**Non-Goals:** store integration, a long-running service, multiple users, encrypted database or
backup, private-key export (FR-11), passphrase change (FR-12), rich console UI (FR-13), bulk
reissue (FR-5), import of pre-tool keys (FR-16), changing signing-key mode (FR-17), reused
order reference warning (FR-18), several licenses in one run (FR-20), HSM or vault keys (`pre-1.0-considerations.md`
C2), localisation.

## Requirements

| R | Requirement | Spec | Personas | Records |
|---|---|---|---|---|
| R1 | One vendor, one person per installation; manual or scripted | vendor-tool-setup: One vendor and one person per installation; Running by hand or from scripts | Vendor | PDR-0030 |
| R2 | Guided setup; prefix changeable later | vendor-tool-setup: Guided setup; Changing the prefix | Vendor; site owner (prefix and keys set once, keys stay consistent) | PDR-0030, PDR-0023 |
| R3 | Folder location, moving | vendor-tool-setup: Folder location; Moving an installation | Vendor | PDR-0031 |
| R4 | Products and add-only feature definitions | vendor-tool-products | Vendor; site owner (no feature silently lost, PDR-0016) | PDR-0036 |
| R5 | Issue, reissue, renew, resend from records | vendor-tool-licenses | Vendor; site owner (renewal keeps paid-for days; lost key resent) | PDR-0033, PDR-0034, PDR-0021 |
| R6 | Records without key strings; customer fields editable and clearable | vendor-tool-licenses | Site owner (copied backup holds no usable key; erasure honoured); vendor | PDR-0033, PDR-0014 |
| R7 | Private keys encrypted; never exported | vendor-tool-signing-keys: Private keys encrypted with the passphrase | Vendor; site owner (no forged keys from a copied folder) | PDR-0032 |
| R8 | Standby, switch, trusted keys, export by signing key | vendor-tool-signing-keys | Vendor; implementor (release trusts the right keys); site owner (keys keep working after rotation) | PDR-0035, PDR-0015 |
| R9 | Backup export and import | vendor-tool-backup | Vendor; site owner (records survive a lost laptop) | PDR-0031 |
| R10 | Tool released with the library | release-process | Vendor | ADR-0011 |

Persona check: no requirement harms a higher-priority persona. Key strings are unchanged for site
owners and implementors. Backoffice editor and site visitor never meet the tool.

## Questions settled in this change

| Q | Question | Answer | Record |
|---|---|---|---|
| Q1 | How keys are issued | By hand, or batched from the vendor's own process; every command runs from arguments alone; no store integration (Carl, 2026-10-28) | PDR-0030 |
| Q2 | Jobs in this change | FR-2 minimum; products and feature definitions; issue and reissue from records; find and list; standby key (FR-4). Import of pre-tool keys out, FR-16 (Carl, 2026-10-28) | PDR-0030 |
| Q3 | Audience | Small vendors issuing only through the tool. Larger vendors use the library directly; private-key export for them is FR-11 (Carl, 2026-10-28) | PDR-0030, PDR-0032 |
| Q4 | Folder location | Propose `~/.ucl`, show the full path, vendor may override; setup names the content, backup advice and sync warning (Carl, 2026-10-28); folder offer and resolution amended by Q29 | PDR-0031 |
| Q5 | Moving and backups | Folder portable by copying; zip export and import (Carl, 2026-10-28) | PDR-0031 |
| Q6 | SQL Server | Dropped; a database file inside the folder only (Carl, 2026-10-28). Was an FR-2 constraint | ADR-0013 |
| Q7 | Protecting private keys | Encrypted with a passphrase set at setup; nothing else encrypted; asked once per command that signs or creates a key; batched runs supply it without a prompt (Carl, 2026-10-28) | PDR-0032 |
| Q8 | Private-key export | None in this change; FR-11 (Carl, 2026-10-28) | PDR-0032 |
| Q9 | Suggested expiry on renewal | Vendor types the expiry; tool suggests current expiry plus the last term (Carl, 2026-10-28). Term and months versus exact time: see Q25 | PDR-0034 |
| Q10 | Correcting or erasing customer data | Edit or clear order reference and email; no copy of a cleared value kept; records never deleted (Carl, 2026-10-28) | PDR-0033 |
| Q11 | Trusted keys for a release | Switch asks the reason; trusted list per product; leaked key stays listed until reissues confirmed sent (Carl, 2026-10-28) | PDR-0035 |
| Q12 | Storing key strings | Not stored; resend is reissue unchanged (Carl, 2026-10-28) | PDR-0033 |
| Q13 | Backup import over an installation | Refused; import only into a folder with no installation; backup date warning (Carl, 2026-10-28) | PDR-0031 |
| Q14 | Operators; passphrase change | One person, no accounts; passphrase change out, FR-12 (Carl, 2026-10-28) | PDR-0030, PDR-0032 |
| Q15 | Standby key stored apart (FR-4 issue 2) | Kept with the active key under the same passphrase; storing apart is FR-15 (Carl, 2026-10-28) | PDR-0032 |
| Q16 | Standby key after setup (FR-4 issues 1, 4) | Can be added later; marked "not yet in a release" until confirmed; switch warns; list of products without a standby key (Carl, 2026-10-28) | PDR-0035 |
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
| Q29 | Finding the installation; more than one | First of: `--folder`, the current folder when it holds an installation, `~/.ucl`. Setup offers `~/.ucl` (proposed) or the current folder. Nothing remembered; no folder environment setting. Each action states the folder used. No installation: refuse naming the folders checked; at a terminal, offer setup (Carl, 2026-10-29) | PDR-0031, ADR-0013 |
| Q30 | Folder state at setup | `~/.ucl` created when missing; a folder holding other files but no installation accepted; only an existing installation refused (Carl, 2026-10-29) | PDR-0031 |
| Q31 | Next step after setup | Closing message ends with the next steps by mode: `product add` (per-product: creates the signing key, asks the passphrase), then `key trusted`. The guide gives the same first steps (Carl, 2026-10-29) | PDR-0031 |
| Q32 | Setup offer edge cases | `--folder` naming a folder with no installation: refused, no offer. Accepted offer: setup runs in full, then the refused action is not run; exit 1 (Carl, 2026-10-29) | PDR-0031 |
| Q33 | `--passphrase-stdin` with no passphrase | Refused (exit 1) when stdin's first line is empty or stdin is at its end; no fall-through to `UCL_PASSPHRASE`. Refused at a terminal (stdin not redirected), since reading would echo it (Carl, 2026-10-29) | ADR-0012 |
| Q34 | Non-ASCII passphrases | `--passphrase-stdin` line read as UTF-8; every passphrase normalised to Unicode NFC at every source, setup included, before the check and PEM encryption (Carl, 2026-10-29) | ADR-0014 |

## Decisions

### Components

```
  vendor / script
        |  args, stdin (passphrase), env UCL_PASSPHRASE, current directory
        v
  +-- ucl (src/Umbraco.Community.Licensing.Tool) --------------------------+
  |  Program --> ToolApp.Run(args, ToolEnvironment)                       |
  |                 |  System.CommandLine root: setup folder prefix       |
  |                 |                   product license key backup        |
  |                 v                                                      |
  |  Commands/*  --uses-->  ToolConsole (in, out, err, interactive?,       |
  |     |                    hidden read)                                  |
  |     +--> Installation (folder resolve, open + migrate ToolDbContext)  |
  |     +--> KeyProtection (passphrase check, encrypt / decrypt PEM)      |
  |     +--> Issuing (records -> LicenseRequest, FeatureDefinitions,      |
  |     |             ExpirySuggestion)                                    |
  |     +--> TrustedKeys (list per product), SignedExport (CSV)           |
  |     +--> Backup (snapshot + manifest -> zip; zip -> new folder)       |
  +----------------|-------------------------------------------------------+
                   | project reference
                   v
         Umbraco.Community.Licensing (unchanged)

  folder (current directory or ~/.ucl):  ucl.db   <- the whole installation
```

`ToolEnvironment` carries the console, `TimeProvider`, home folder, current directory,
environment variables and the PBKDF2 iteration count, so every command runs in-process in tests
against a temp folder.

### Command surface

Global options: `--folder <path>`, `--passphrase-stdin`, `--no-input` (never prompt, even at a
terminal). Environment: `UCL_PASSPHRASE`.

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
  ucl backup import <file.zip> [--folder <empty target>]
```

Feature values follow the product's definition: switch `name=on|off`, number `name=500`, text
`name=eu`. Expiry: `2027-03-01` (date, PDR-0007), `2027-03-01T23:59:59Z`, `perpetual`. A missing
input at a terminal is prompted; with `--no-input` or redirected stdin it is a refusal naming the
option. `ucl setup` takes no options: `--folder` or `--passphrase-stdin` on it is a usage error (exit 2)
naming the option; `UCL_PASSPHRASE` is not read; the folder is asked: `~/.ucl` (proposed) or the
current directory, with a warning when the current directory is home; `~/.ucl` is created when missing; other files
in the chosen folder are left alone. The closing message ends with the next steps:
`ucl product add <product-id> --name <name>` (per-product mode: notes it creates the signing key
and asks the passphrase), then `ucl key trusted <product-id>`. It runs only when interactive
(ADR-0012 detection) and otherwise exits 1 (vendor-tool-setup: Setup with nobody present). `--order-ref` on issue, reissue and renew sets the new issue entry's order reference; on
`license customer` it corrects the latest entry's. `prefix set` needs no passphrase. Prompts and
messages go to stderr; the result (key string, file path, list) to stdout. Every command that opens
an installation first writes `Using <full path>` to stderr. No installation found: exit 1 naming
the folders checked; when interactive, then `Run setup now? [y/N]`, starting setup on yes. The action is not run after setup; exit 1 either
way. `--folder` naming a folder with no `ucl.db`: exit 1 naming it, no offer.
`folder show` prints the resolved folder to stdout. `backup import` without `--folder` asks for
the target as setup does, and with no person present refuses naming `--folder`.
Exit codes: 0 done, 1 refused (validation, wrong passphrase, missing installation), 2 usage
error.

### Packaging and release — ADR-0011

`PackAsTool`, `ToolCommandName` `ucl`, `PackageId` `Umbraco.Community.Licensing.Tool`, imports
`Version.props`, packs `docs/vendor-tool.md` as its README and `assets/icon.png`. `release.yml`
packs both projects into the pack folder and pushes `*.nupkg`; a step installs the tool from the
pack folder into a temp tool path and runs `ucl --version` before the push. Amends ADR-0008.
**Package IDs** then yields both IDs, so the GitHub Release links both packages with no change to
`github-release.mjs` or job `github-release` (ADR-0016; release-process: One link per published
package).

### Command line and console — ADR-0012

System.CommandLine 2.0 for parsing, help and validation. Prompts with BCL `Console`: interactive
when stdin is not redirected and `--no-input` is absent; prompts on stderr so stdout can be
redirected; hidden passphrase read with `Console.ReadKey(intercept: true)`. Passphrase precedence:
`--passphrase-stdin` (first line of stdin), `UCL_PASSPHRASE`, prompt. `--passphrase-stdin` reads the
first line as UTF-8; an empty line or end of stdin is refused, not passed to the next source; at a
terminal it is refused (Q33).

### Database and folder — ADR-0013

EF Core 10 with SQLite, one file `ucl.db` in the folder; the folder holds an installation when
`ucl.db` exists. Schema by EF migrations, applied on open in a transaction; a database holding a
migration the running tool does not know is refused ("update the tool"). Every connection sets
`PRAGMA secure_delete = ON` and `journal_mode = DELETE`, so a cleared email leaves no copy in free
pages or a WAL file (vendor-tool-licenses: Customer details). Folder precedence: `--folder`,
the current directory when it holds `ucl.db` (parents not searched), `<home>/.ucl`. Nothing is
stored outside the folder.

Tables:

| Table | Columns (key first) |
|---|---|
| `Settings` | `Name`, `Value` — prefix, signing-key mode, standby on, passphrase check (salt, iterations, hash) |
| `Products` | `ProductId`, `DisplayName`, `CreatedUtc` |
| `FeatureDefinitions` | `ProductId` + `Name`, `Type`, `AddedUtc` |
| `SigningKeys` | `SigningKeyId`, `ProductId` (null in shared mode), `PublicKey` (export line), `EncryptedPrivateKeyPem`, `Role` (active / standby / replaced), `CreatedUtc`, `ReplacedUtc`, `ReplacedReason`, `InReleaseUtc`, `ReissuesSentUtc` |
| `Licenses` | `Reference` (10 characters), `ProductId`, `CustomerEmail`, `CreatedUtc` |
| `IssueEntries` | `Id`, `Reference`, `Kind` (issue / reissue / renewal / resend), `IssuedUtc`, `ExpiresUtc` (null = perpetual), `DisplayName`, `VendorTag`, `FeaturesJson`, `OrderRef`, `KeyIdentifier`, `SigningKeyId` |

`KeyIdentifier` keeps the prefix each entry was issued with, so a prefix change needs no data
change. `OrderRef` is indexed for find by order reference.

No column holds a key string.

### Private keys — ADR-0014

Create: `SigningKeyPair.Create()` → `ExportPem()` → `ECDsa.ImportFromPem` →
`ExportEncryptedPkcs8PrivateKeyPem(passphrase, PBES2 AES-256-CBC, PBKDF2-HMAC-SHA256, 600,000
iterations)`. Use: `ECDsa.ImportFromEncryptedPem` → `ExportPkcs8PrivateKeyPem` →
`SigningPrivateKey.FromPem`, disposed when the command ends. The passphrase is checked first
against the `Settings` passphrase check (PBKDF2-SHA256, 16-byte salt, same iteration count,
`CryptographicOperations.FixedTimeEquals`), so a mistyped passphrase in per-product mode cannot
encrypt a new key under a different passphrase. Tests pass a low iteration count through
`ToolEnvironment`; the count is stored with each PEM and check, so decrypting reads it back. The
passphrase is normalised to NFC before the check and before encryption or decryption, so the same
characters give the same bytes from any source or OS (Q34).

### Backup and export — ADR-0015

Export: `VACUUM INTO` a temp file (consistent snapshot, no free pages), then a zip holding
`manifest.json` (`format` 1, `toolVersion`, `lastMigration`, `takenUtc`) and `ucl.db`. Import: read
the manifest; refuse an unknown format, a missing entry, or a `lastMigration` the tool does not
know; check the target folder has no `ucl.db`; show `takenUtc` and the warning; extract;
`PRAGMA integrity_check`; apply pending migrations. Neither command
overwrites an existing output file. Signed-key export: CSV (RFC 4180, UTF-8, header
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
`InReleaseUtc` on every listed key that lacks it. `key switch` warns when the standby becoming
active has no `InReleaseUtc`.

### Ownership

| Path | Owner |
|---|---|
| `src/Umbraco.Community.Licensing.Tool/**`, `tests/Umbraco.Community.Licensing.Tool.Tests/**` | worker-backend (under `src/**`, `tests/**`) |
| `Umbraco.Community.Licensing.slnx` | worker-backend, for the two new project entries |
| `.github/workflows/release.yml` | Shared (CI): assigned to worker-backend in the brief |
| `.config/dotnet-tools.json` (new top-level path, `dotnet-ef` for creating migrations) | Shared: assigned to worker-backend in the brief. `dotnet-ef` approved by Carl, 2026-10-28 |
| `docs/vendor-tool.md`, `docs/releasing.md`, `README.md`, records | Architect |

## Test strategy

| Level | What | How |
|---|---|---|
| Unit | Expiry suggestion (every Suggested expiry scenario plus month-end and leap cases), folder precedence, trusted list, CSV quoting, feature value parsing, passphrase check | xUnit, test names follow spec scenarios |
| Integration | Every command scenario in the five vendor-tool specs, in-process against a temp folder with a real SQLite file, scripted console (interactive and not), fixed `TimeProvider`, low PBKDF2 count | xUnit; one temp folder per test |
| Integration | Issued keys verify with the library's `LicenseEvaluator` trusting the tool's `key trusted` lines | xUnit |
| Integration | No key string in the folder: issue several licenses, scan every byte of `ucl.db` for each key string; cleared email absent from the file | xUnit |
| Integration | Encrypted PEM opens with `ECDsa.ImportFromEncryptedPem` and the passphrase; not without | xUnit |
| Integration | Backup round trip into a new folder; refusals; newer-version manifest | xUnit |
| Integration | Production iteration count: one test runs a single encrypt/decrypt at 600,000 to confirm it completes (time printed, not asserted) | xUnit |
| Package | Pack the tool, install it from the pack folder into a temp tool path, run `ucl --version` | `release.yml` step; locally by the worker |
| Observed | Stored PEM opened with `openssl pkey` and the passphrase | Carl, once, recorded in tasks.md |
| Observed | Full suite on macOS and Linux before archive (FR-2) | Carl, recorded in tasks.md |

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
- [Passphrase in `UCL_PASSPHRASE` visible to processes of the same user] → Documented;
  `--passphrase-stdin` offered for scripts that can pipe it.
- [Cloud-synced folder chosen anyway] → Setup warns; the tool cannot detect sync reliably on
  every OS.
- [Lost passphrase] → Keys unrecoverable (FR-2 loss case). Setup and the guide say to keep it in a
  password manager, apart from backups.
- [Older backups hold cleared emails] → Stated in the guide (PDR-0033).
- [PBKDF2 at 600,000 iterations runs once for the check and once per key encrypted or decrypted:
  two derivations per signing command, three for `product add` with standby, about 1 to 1.5 s] →
  Acceptable for commands run by hand or in small batches.
- [First push of a new package ID under Trusted Publishing] → The policy is per repository and
  workflow (ADR-0008); if nuget.org refuses, Carl publishes the first tool version with a
  short-lived API key and records it in follow-ups.md.
- [Lockstep versions: a tool-only fix bumps the library version] → Accepted in beta (ADR-0011).
- [System.CommandLine API differs between 2.0 betas and 2.0 stable] → Pin the stable 2.0.x
  exactly; the brief names it.

## Migration Plan

1. Build sections 1 to 12 under the apply-phase team; Carl commits per section.
2. Carl runs the full suite on macOS and Linux, opens a stored PEM with OpenSSL and rehearses a
   release (section 13).
3. Next release (`0.1.0-beta.7` or later) publishes both packages. Rollback: unlist the tool
   version on nuget.org; the library is unaffected.
