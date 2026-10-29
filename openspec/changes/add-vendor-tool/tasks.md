Owners per CLAUDE.md and design.md (Ownership): sections 2 to 11 worker-backend; sections 1 and
12 Architect; section 13 Carl. Validation for every worker block:
`dotnet build Umbraco.Community.Licensing.slnx`,
`dotnet test --solution Umbraco.Community.Licensing.slnx`,
`node --test "tools/release/*.test.mjs"`. Bar: production (design.md). Test names follow spec
scenario names. Specs: `specs/vendor-tool-*/spec.md`, `specs/release-process/spec.md`. Records:
PDR-0030 to PDR-0036, ADR-0011 to ADR-0016.

## 1. Ownership

Depends on: none. Owner: Architect.

- [x] 1.1 Add the `.config/**` row (Shared: assigned in the brief) to the CLAUDE.md ownership table, and verify it matches design.md Ownership

## 2. Tool and test projects

Depends on: 1. Owner: worker-backend. Shared files assigned in the brief: the two `.slnx` entries,
`.config/dotnet-tools.json`. Records: ADR-0011, ADR-0012, ADR-0013.

- [x] 2.1 Create `src/Umbraco.Community.Licensing.Tool` (project reference to the library; `PackAsTool`, `ToolCommandName` `ucl`, `PackageId` `Umbraco.Community.Licensing.Tool`, `Version.props` import, metadata and icon as the library, README from `docs/vendor-tool.md`) and `tests/Umbraco.Community.Licensing.Tool.Tests` (xUnit v3 as the library tests), add both to the `.slnx`, add System.CommandLine 2.0.x stable, Microsoft.EntityFrameworkCore.Sqlite 10.0.x and Microsoft.EntityFrameworkCore.Design 10.0.x (private assets) pinned exactly, and verify `dotnet build` passes with no warnings, `dotnet run --project src/Umbraco.Community.Licensing.Tool -- --version` prints `Version.props`'s version, and the library project has no new package reference
- [x] 2.2 Add `dotnet-ef` 10.0.x as a pinned local tool in `.config/dotnet-tools.json` (approved by Carl, 2026-10-28), and verify `dotnet tool restore` and `dotnet ef --version` succeed

## 3. Console, commands and folder

Depends on: 2. Owner: worker-backend. Spec: vendor-tool-setup (Folder location; Running by hand or
from scripts). Records: ADR-0012, ADR-0013, PDR-0030, PDR-0031.

- [x] 3.1 Implement `ToolApp.Run(args, ToolEnvironment)`, `ToolEnvironment` (console, `TimeProvider`, home, current directory, environment, PBKDF2 iteration count), `ToolConsole` (stdout result, stderr messages and prompts, interactive detection, `--no-input`, hidden read), global options and exit codes 0/1/2 per design.md Command surface, and verify by tests for the "Missing input with nobody present" and "Prompted by hand" scenarios against a stub command
- [x] 3.2 Implement folder resolution (`--folder`, current directory holding `ucl.db`, `<home>/.ucl`), the `Using <path>` line on stderr, `ucl folder show`, and the "no installation" refusal with the setup offer when interactive (the offer may start setup once section 6 exists; until then test the offer and a declined answer), and verify by tests for "Current folder used", "Home installation used elsewhere", "Folder named on the command wins", "No installation found" and "No installation found with nobody present", and that stdout carries no `Using` line

## 4. Database

Depends on: 2, 3. Owner: worker-backend. Records: ADR-0013, PDR-0033.

- [ ] 4.1 Implement `ToolDbContext` with the design.md tables (no column for a key string), the initial migration, open with `secure_delete` on and `journal_mode` DELETE, migrations applied on open, and refusal of a database holding an unknown migration, and verify by tests that a new folder gets the schema, the pragmas read back as set, and a database with an extra migration row is refused naming "update the tool"

## 5. Private key protection

Depends on: 4. Owner: worker-backend. Spec: vendor-tool-signing-keys (Private keys encrypted with
the passphrase). Records: ADR-0014, PDR-0032.

- [ ] 5.1 Implement the passphrase check (PBKDF2-SHA256, salt, stored iterations, fixed-time compare), encrypt and decrypt of private keys per ADR-0014, and passphrase sources (`--passphrase-stdin`, `UCL_PASSPHRASE`, hidden prompt), and verify by tests for "Opened by another tool" (`ECDsa.ImportFromEncryptedPem` with and without the passphrase), a wrong passphrase refused before any key is created, and one encrypt/decrypt at 600,000 iterations completing

## 6. Setup

Depends on: 5. Owner: worker-backend. Spec: vendor-tool-setup (Guided setup; One vendor and one
person per installation; Moving an installation; Changing the prefix). Records: PDR-0030,
PDR-0031, PDR-0023.

- [ ] 6.1 Implement `ucl setup` (folder, passphrase twice, prefix, signing-key mode, standby offer; shared key created in shared mode; folder offered as `~/.ucl` (proposed) or the current directory, with the home-directory warning; closing message with full path, content, backup advice, sync warning and the next steps by mode), and verify by tests for "Setup with defaults" (next steps checked in per-product and shared mode), "Invalid prefix", "Passphrases differ", "Passphrase too short", "Folder already holds an installation", "Second installation", "Setup with nobody present" (redirected stdin, and `--no-input` at a terminal), "Default folder created", "Current folder with other files", and "No installation found" accepted (setup starts), all through the scripted interactive console; tests of later sections create installations through the same console
- [ ] 6.2 Verify "New laptop": a test copies a set-up folder to another temp home's `.ucl`, runs from an unrelated current directory, and issues a key with the same passphrase (after section 8; tick with 8)
- [ ] 6.3 Implement `ucl prefix show` and `prefix set` (prefix rule, no passphrase, message on what changes, records untouched), and verify by tests for "Prefix changed", "Earlier prefix typed" and "Invalid prefix on change" (after section 8; tick with 8)

## 7. Products

Depends on: 6. Owner: worker-backend. Spec: vendor-tool-products. Records: PDR-0036, PDR-0016.

- [ ] 7.1 Implement `ucl product add` (per-product mode creates the signing key and standby key, needs the passphrase), `product rename`, `product feature add` (add-only) and `product list`, and verify by tests for every vendor-tool-products scenario in both signing-key modes

## 8. Licenses

Depends on: 7. Owner: worker-backend. Spec: vendor-tool-licenses. Records: PDR-0033, PDR-0034,
PDR-0021, PDR-0023.

- [ ] 8.1 Implement the expiry suggestion per design.md (term from the last renewal entry, or the first issue when never renewed; calendar months when the term was whole months, exact time otherwise; perpetual; non-renewal), and verify by tests for every "Suggested expiry" scenario (including "Second renewal after an early renewal", "Add-on before renewal" and "Renewal after a lapse") plus month-end (2026-01-31 to 2026-02-28) and leap-day cases
- [ ] 8.2 Implement `ucl license issue` (feature values parsed by definition, signing with the product's active key, record and issue entry, key string to stdout once), and verify by tests for "License issued", "Undefined feature", "Wrong passphrase" and "Scripted issue", and that the key verifies with `LicenseEvaluator` trusting the product's public keys
- [ ] 8.3 Implement `license reissue`, `license renew` (with `--expires suggested`, recorded as kind renewal) and `license resend` (entry with no order reference), with `--order-ref` setting the new entry's order reference, building a `LicenseRequest` with the stored reference and product, and verify by tests for "Add-on bought", "Site owner lost the key", "Renewal keeps the earlier order reference" and each suggested-expiry case through the command
- [ ] 8.4 Implement `license list` (filters by product, reference under the prefix rule, order reference on any entry, email; row shows email and latest order reference), `license show` (issue entries newest first, each with its order reference) and `license customer` (edit or clear email; correct the latest entry's order reference; `--clear-order-refs` for every entry), and verify by tests for "Find by customer email", "Find by an earlier order reference", "License history", "Correcting the latest order reference", "Clearing order references", "Erasure request" (cleared email and order references absent from every byte of `ucl.db`), "No delete", and "No passphrase for reading" for `license list` and `license show` (no passphrase prompt or source read)
- [ ] 8.5 Verify "Copied folder": a test issues, reissues and resends licenses, then scans every file in the folder for each key string, and finds none

## 9. Signing keys

Depends on: 8. Owner: worker-backend. Spec: vendor-tool-signing-keys. Records: PDR-0035,
PDR-0015, PDR-0032.

- [ ] 9.1 Implement `ucl key status` (with `--without-standby`) and `key add-standby`, and verify by tests for "Standby added later" and "Products without a standby key", including shared mode
- [ ] 9.2 Implement `ucl key switch` with reason, standby promotion, new standby, the "not yet in a release" warning and confirmation (`--yes` for scripts), and verify by tests for "Routine rotation with standby", "Standby not yet in a release", a switch with no standby, and new issues signed by the new key
- [ ] 9.3 Implement `ucl key trusted`, `key released` and `key reissues-sent`, and verify by tests for "After routine rotation", "During a leak", "After the reissues are sent", "Release confirmed" and "No passphrase for reading" for `key trusted`, and that `LicenseEvaluator` trusting the printed lines accepts keys from every listed key and reports *signing key not recognised* for the withdrawn one
- [ ] 9.4 Implement `ucl key export` (CSV per ADR-0015, refuses an existing file), and verify by tests for "Leak export", CSV quoting, and that the file holds no key string

## 10. Backup

Depends on: 8, 9. Owner: worker-backend. Spec: vendor-tool-backup. Records: ADR-0015, PDR-0031.

- [ ] 10.1 Implement `ucl backup export` (snapshot, manifest, zip, refuses an existing file, no passphrase), and verify by tests for "Backup taken" and that the zip's private keys are the encrypted PEMs
- [ ] 10.2 Implement `ucl backup import` (manifest checks, target from `--folder` or asked as at setup, refusal naming `--folder` with nobody present, target without installation, date warning, integrity check, migrations), and verify by tests for "Restore to a new machine", "Folder already holds an installation", "Not a backup" and "Backup from a newer tool version", and that a restored installation issues with the backup's passphrase

## 11. Release workflow

Depends on: 2. Owner: worker-backend. Shared file assigned in the brief:
`.github/workflows/release.yml`. Spec: release-process (Release run publishes only tested
packages; Release page for each published release). Records: ADR-0011, ADR-0008, ADR-0016.

- [ ] 11.1 Pack the tool project alongside the library into the pack folder, add a step that installs the tool from the pack folder into a temp tool path and runs `ucl --version` before the push, and keep the push of `*.nupkg` from the pack folder, and verify locally that `dotnet pack` of both projects yields two packages with the same version, that the install-and-run step succeeds, and that `node tools/release/github-release.mjs package-ids <pack folder>` prints both `Umbraco.Community.Licensing` and `Umbraco.Community.Licensing.Tool`; the workflow itself is verified by Carl's rehearsal (13.3)

## 12. Documentation

Depends on: 6 to 11. Owner: Architect.

- [ ] 12.1 Write `docs/vendor-tool.md` (install with the .NET SDK, setup, first steps after setup matching setup's closing message (add a product, define features, print trusted keys for the release, issue a first license), folder and moving, passphrase and loss, scripting with `--no-input` and the passphrase sources, every command, leak and loss steps from FR-2 using the tool, backups and older backups holding cleared emails, vendor responsibility for the database), and verify every command in it against `ucl --help` output
- [ ] 12.2 Add a "Vendor tool" section to `README.md` linking the guide, and verify the link resolves
- [ ] 12.3 Update `docs/releasing.md` for two packages (job summary, **Pack** and **Package IDs** expectations in the rehearsal steps, Release page with two links, first push of `Umbraco.Community.Licensing.Tool` under Trusted Publishing), and verify every step name it cites matches `release.yml` after 11.1

## 13. Verification by Carl

Depends on: 2 to 12. Owner: Carl.

- [ ] 13.1 Run the validation commands on macOS and on Linux (FR-2), and verify both pass; record the result per OS here
- [ ] 13.2 Open a stored private key with `openssl pkey` and the passphrase, and verify it opens with the passphrase and fails without it
- [ ] 13.3 Run a release rehearsal on a test machine, and verify it packs both packages, the install-and-run step passes, and **Package IDs** prints both package IDs
