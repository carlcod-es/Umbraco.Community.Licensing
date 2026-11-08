## Why

The library issues and verifies keys but keeps no records (PDR-0013, PDR-0014). A small vendor
using it alone must hold signing keys safely, remember what each customer bought, reissue from
the key a site owner sends back, and work out by hand which keys a product release must trust
after a rotation or a leak. `docs/feature-requests.md` FR-2 asked for a vendor tool that adds
what the library leaves out; FR-4 asked for a standby signing key. With `0.1.0-beta.6`
published, the library is ready to gain its main issuing client.

## What Changes

- **Vendor tool**: a console application for small vendors who issue keys only through it. Runs as
  a menu session: `ucl` opens a menu (Products, Licenses, Signing keys, Backup, Settings, Quit),
  a person at a terminal chooses actions and answers line-by-line questions, and the menu
  returns until they quit. Only help and version are commands. A bad answer is asked again;
  optional inputs are asked one by one; a summary is confirmed before signing; a new key is shown
  and may be saved to a file. Scripted mode, with nobody present, is not offered (PDR-0039,
  PDR-0048). No store integration. One vendor and one person per installation (PDR-0030).
- **Guided setup**: folder (`~/.ucl` proposed, or the current folder), passphrase, key
  prefix (PDR-0023), one signing key per product or one shared, standby key offered as
  recommended, idle lock time (PDR-0049). Setup names what is stored and says it must be backed up and kept out of
  synced folders. The prefix and idle lock can be changed later; the signing-key mode cannot (PDR-0030,
  PDR-0031). No installation found: setup or restore, then the menu; a second installation is
  made from Settings in the current folder (PDR-0048).
- **Self-contained folder**: everything the tool holds is in one folder that keeps working when
  moved to another machine. The tool uses the current folder when it holds an installation,
  otherwise `~/.ucl`; nothing else is remembered. Backup export to a zip and import of a zip into a folder with no
  installation (PDR-0031).
- **Private keys protected by a passphrase**: the only encrypted content; asked at the first
  action in a session that signs or creates a key and kept for the session until Quit or the idle
  lock; three tries; never exported from the tool (PDR-0032, PDR-0049).
- **Products and feature definitions**: add-only; product ID and feature name and type never
  change (PDR-0036).
- **Issue, reissue, renew, resend** from stored records. Renewal suggests the current expiry plus
  the last term. Key strings are never stored; resend is a reissue with nothing changed
  (PDR-0033, PDR-0034).
- **Records**: find and list licenses; optional customer email per license and order reference
  per issue, editable and clearable; records never deleted (PDR-0033).
- **Signing keys**: switch with a reason (routine, loss, leak); record the signing key of every
  issue; export by signing key; standby key at setup or later; list of products without a
  standby key; the public key lines each product's next release must trust, including the
  interim list during a leak (PDR-0035).
- **Typed values**: the product display name, customer email and order reference follow the
  key's text rule, so accents and other scripts are allowed and emoji are not. Email and order
  reference are found ignoring case and accent form, and only by the whole value. The
  passphrase is printable ASCII, 15 to 256 characters (PDR-0043).
- **Switch off**: a switch answered `off` is not granted; each feature is asked once (PDR-0044,
  PDR-0048).
- **Named files**: an export, backup or saved key never replaces a file and never creates a folder. A
  missing import file is asked again (PDR-0045).
- **Portable records**: every record is complete and free of local paths, ready for a later
  one-way move to a central database (PDR-0046).
- **Release**: the release run publishes the tool package alongside the library, same version.
- Out of this change, logged as feature requests: private-key export for larger vendors (FR-11),
  passphrase change (FR-12), richer console prompts (FR-13), retiring feature definitions
  (FR-14), standby key stored apart (FR-15), importing licenses issued before the tool (FR-16),
  changing signing-key mode (FR-17), warning on a reused order reference (FR-18), issuing several
  licenses in one run (FR-20), scripted mode (FR-22), central database and
  shop integration (FR-24, FR-25, FR-26). Bulk reissue and leak follow-through stay with FR-5.

## Capabilities

### New Capabilities

- `vendor-tool-setup`: setup, folder location and moving, second installation, passphrase,
  prefix, signing-key mode, idle lock, menu session and its questions, operating systems and
  terminals.
- `vendor-tool-products`: products and feature definitions.
- `vendor-tool-licenses`: issue, reissue, renewal suggestion, resend, records, finding and
  listing, customer fields.
- `vendor-tool-signing-keys`: private key protection, standby keys, switching, trusted public
  keys per product, export by signing key.
- `vendor-tool-backup`: backup export and import.

### Modified Capabilities

- `release-process`: a release publishes the tool package as well as the library, under the same
  version, from the same tested build.

## Impact

- Depends on `tighten-text-rule`, which tightens the key's text rule first; the tool's typed text
  fields use that rule (design.md Q37, Q38, Q40; PDR-0043). The tool package carries
  `THIRD-PARTY-NOTICES.md` (ADR-0018).
- New: tool project and its test project; tool user guide; README section. The README is slim
  (`tighten-text-rule`): the section is one bullet and a row in its Documentation table linking
  the tool guide; detail stays in the guide.
- Changed: release workflow packs and pushes two packages; CI and release checks run the tool's
  tests with the library's.
- Library: no public API or behaviour change. The tool reaches the internal text-rule check
  through `InternalsVisibleTo` (ADR-0019).
- Records: PDR-0030 to PDR-0036, PDR-0038 to PDR-0041, PDR-0043 to PDR-0049 (new); ADR-0011 to
  ADR-0015, ADR-0017, ADR-0019 (new; ADR-0011 amends ADR-0008; ADR-0017 amends ADR-0012 and
  ADR-0013; ADR-0019 amends ADR-0003 and ADR-0013). `docs/feature-requests.md`: FR-2, FR-4 and FR-23
  taken into this change; FR-11 to FR-18, FR-20 and FR-22 to FR-26 added.
- Dependencies (approved by Carl, 2026-10-28): System.CommandLine,
  Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design (build only),
  `dotnet-ef` local tool (creating migrations).
- Prerequisite for vendors: the .NET SDK.
- Personas: vendor (records, safe keys, guided rotation). Site owner (primary): keys unchanged;
  renewals keep paid-for days; lost keys resent from records; a copied backup holds no usable
  keys. Implementor: a release after a key change trusts the right keys. Backoffice editor and
  site visitor: unaffected.
