# Follow-ups: add-sample-package

Minors and nits from reviews. Read at the start of each apply session.

## Section 1 (supervisor, 2026-11-13)

- **minor** (resolved: CLAUDE.md fixed; design.md aligned by opsx:update, 2026-11-13): design.md:122
  says `Client/**` first creation is template output and every later edit is worker-frontend's,
  but design.md:228-229 has section 2 (worker-backend) apply the template patches. CLAUDE.md
  now says the patches are part of worker-backend's scaffold output; mirror that in
  design.md:122. Section 2 brief states it explicitly.
- **nit**: CLAUDE.md UI row mixes the generic rule with a change-specific provenance note.
  Move the note to design.md once a second UI project exists.
- **risk for section 7**: worker-frontend edits `Client/package.json` and the lockfile; the
  brief must repeat that any new npm package needs Carl's approval.

## Section 2 (Architect, 2026-11-13)

- **records drift** (resolved by opsx:update, 2026-11-13): ADR-0026 Decision and design.md
  "Umbraco and packages" table still name `Umbraco.Cms.Web.BackOffice` (no 17.x release) and
  `Umbraco.Cms.Persistence.Sqlite` (transitive through `Umbraco.Cms`), and omit the packages
  Carl approved on 2026-11-13 from the 17.8.1 templates (`Umbraco.Cms.Web.Common`,
  `Umbraco.Cms.Api.Common`, `Swashbuckle.AspNetCore` 10.2.3, `Umbraco.Cms.DevelopmentMode.Backoffice`,
  `Microsoft.ICU.ICU4C.Runtime` 72.1.0.3). ADR-0026's approval line already records the
  approved set.
- **records drift** (resolved by opsx:update, 2026-11-13): design.md (Context; Backoffice API and client) and
  task 2.2 say the template defects (hey-api peer minimum, 17 generator config, `hey-api.ts`
  cast) are patched in section 2 and listed in the sample README. On 17.8.1 none applied
  (worker-backend, verified by `npm ci`/`npm run build` unpatched). Reword to "checked; none
  needed on 17.8.1"; the README lists none.

## Block 2.1–2.3 (reviewer, 2026-11-13, APPROVED)

- **minor** (resolved in block 2.4, samples/README.md Notes): `samples/Acme.ContactForm.Site/appsettings.json` holds template-generated
  `Imaging.HMACSecretKey` and `Global.Id`. Add a `samples/README.md` note that these are demo
  values never to reuse (task 2.4 or 9.1).
- **nit** (resolved in block 2.4, samples/README.md Notes): `appsettings.Local.json` loads only `#if DEBUG` (template). Say in `samples/README.md`
  that the site runs in Debug for the unattended install (task 2.4).
- **nit**: `launchSettings.json` profile still named "Umbraco.Web.UI" with an IIS Express
  profile (template defaults). Cosmetic.
- **reminder**: template demo endpoints (`ping`, `whatsTheTimeMrWolf`, `whatsMyName` with
  `Thread.Sleep`, `whoAmI`) and starter dashboard must be removed when sections 6 and 7 add the
  real ones.

## Block 2.4 (reviewer, 2026-11-13, APPROVED)

- **minor**: `samples/run-demo.mjs:56-61` spawns `npm` with `shell: true` plus an args array on
  Windows; Node 24 prints DEP0190. Use one command string with `shell: true` on Windows.
- **minor**: `samples/run-demo.mjs:27-37` `newestMtime` follows symlinks (loop would recurse
  forever) and a deleted source file does not trigger a rebuild. Acceptable for the prototype.
- **nit**: `samples/run-demo.mjs:87` exits 130 for any signal that killed dotnet.
- **nit**: `samples/run-demo.test.mjs` has no case for equal mtimes, a missing input path, or
  `public`/`vite.config.ts`/`tsconfig.json` as inputs.
- Fresh-copy run to the login rests on worker-backend's evidence; Carl repeats it in 10.1.

## Section 2 (supervisor, 2026-11-13, APPROVED)

- **minor** (resolved: Carl confirmed, ADR-0026 names them): ADR-0026 approval line names four npm packages; the template set also brings
  `chalk` 5.6.2, `cross-env` 10.1.0 (unused by any script) and `node-fetch` 3.3.2. Asked Carl to
  confirm; then name them in the approval line.
- **minor** (fixed): ADR-0026 approval line said "with the hey-api peer-version patch",
  contradicting the next bullet. Parenthesis removed.
- **minor**: template C# uses block-scoped namespaces (`Constants.cs`, `Composers/`,
  `Controllers/`); CLAUDE.md requires file-scoped. Convert whatever survives section 6; consider
  renaming `Constants` (shadows `Umbraco.Cms.Core.Constants`).
- **nit**: `run-demo.mjs` starts the first launch profile, which also binds https; with no dev
  certificate Kestrel fails. README line on `dotnet dev-certs https`, or an http-only profile (9.1).
- **risks for briefs**:
  - Cite ADR-0026's approval line, not design.md's package table, until the `opsx:update`
    (do it before section 6).
  - Section 3: the generator test finds repo paths by walking up to `samples/Acme.ContactForm.slnx`;
    check the MTP explicit-test command syntax before writing it into the README.
  - Section 6: template API name `acmecontactform` vs required `acme-contact-form`; the template
    base class has class-level `SectionAccessContent`, which must not stack on per-action policies.
  - Sections 6.3/7.1: `generate-client` points at `https://localhost:5000/.../acmecontactform/`;
    fix port and document name.
  - Section 10.1: log in and reach the backoffice, not just the login page.
- **heads-up for Carl**: committed demo PEM private keys (section 3) may trigger GitHub secret
  scanning on push.

## Block 2.5 (reviewer, 2026-11-13, APPROVED)

- Committed by Carl (`0b8ec61`) before review; reviewed against `HEAD~1..HEAD -- samples/`.
  No findings above nit. "First run" from a fresh copy rests on worker-backend's evidence
  (demo login 200, wrong password 401); Carl repeats it in 10.1.
- **note**: the default shell runs Node 20, which `run-demo.mjs` refuses (needs 24.13+). Worth
  saying in `docs/development.md` (9.1).

## Section 2 re-review with 2.5 (supervisor, 2026-11-13, APPROVED)

- **nit**: tasks.md 2.2 and 2.4 (ticked) still describe the removed local-settings step and the
  "No administrator password committed" scenario. Next `opsx:update`: mark them "(amended by
  2.5, PDR-0070)".
- **nit**: `docs/adrs/0026-sample-solution-umbraco-and-ci.md:11` one ~130-char line; rewrap.
- **risk**: Carl's working tree has a gitignored `appsettings.Local.json` (another admin) and an
  installed SQLite database, so the demo login will not work there; delete both for a local
  run. 10.1 uses a fresh clone.
- **risk for section 8**: any CI step that starts the site must use the Development
  environment, where the demo administrator lives.
- **risk for 9.1**: Node 24.13+ needed for `run-demo.mjs` and for the `tools/**` test glob.

## Block 3.1 (reviewer, 2026-11-13, APPROVED)

- **minor**: `samples/Acme.ContactForm.Tests/DemoSigningKeyTests.cs` (~64-75)
  `NoPrivateKeyInThePackage`: `built` is the test assembly folder while its comment says package
  output; it overlaps `assemblyFolder`. Rename or drop one check.
- **minor**: same test, `Assembly.Load("Acme.ContactForm")` fails with a load error, not a clear
  message, if the project reference is dropped. Acceptable.
- **nit**: `samples/demo-signing-keys/README.md:1` `//` warning renders as plain text; required.
- **for section 4**: `SigningPublicKey.Parse` cannot read `*.public.txt` (leading `//` lines).
  The package holds the bare line as a constant; add a test comparing it to
  `DemoKeyFiles.LoadPublic("demo.public.txt")` so they cannot drift.

## Block 3.2–3.3 (reviewer, 2026-11-13, APPROVED)

- **nit**: `samples/Acme.ContactForm.Tests/DemoKeyFile.cs` JSON options use
  `UnsafeRelaxedJsonEscaping` (readable apostrophe in "Another product's key"); safe as JSON
  data, add a one-line comment saying it must not be inlined into HTML.
- **nit**: `DemoKeyTests.Not_supported_not_demonstrated` asserts README wording; a rewording
  fails it. Accepted.
- **records drift (nit)**: the generator also writes `Demo/demo-keys.json`, design.md and
  ADR-0028 say "printing". Align by `opsx:update`.
- **notes**: references are random (the library needs 10 chars from its alphabet), so
  `DisplayName` carries the entry name; prefix `ACME`. Entries 9 and 10 record the claimed
  product with `productVerified: false` (field added beyond design.md's list).

## Section 3 (supervisor, 2026-11-13, APPROVED)

- **minor**: `samples/Acme.ContactForm.Tests/DemoKeyFile.cs:82-86` JSON options use the default
  `NewLine` (CRLF on Windows) while the generator appends `\n`: mixed endings on a Windows
  regeneration. Set `NewLine = "\n"`. Optional (Architect): `.gitattributes` `eol=lf` for
  `samples/demo-signing-keys/*` and `demo-keys.json`.
- **minor**: `DemoKeyTests.cs:310` hard-codes `UntrustedKeyId`; read it from
  `DemoKeyFiles.LoadPublic("untrusted.public.txt")` so a deliberate rotation does not look like a
  regression.
- **minor** (section 6.2): `DemoKeyEntry`/`LoadEmbedded` live in the test project; move them to
  the package's `Demo/` when `DemoKeySelection` lands so one reader serves both.
- **nit**: `DemoKeyFile.cs:79` FixedClock comment: "after Expired's expiry, before the others expire".
- **records drift (nit)**: design.md field list lacks `productVerified` (with the "printing" drift above).
- **risks for section 4**:
  - 4.1: test `ContactFormProduct`'s public key constant against
    `DemoKeyFiles.LoadPublic("demo.public.txt")`; switch `DemoKeyTests` to the product's ID and
    trusted keys so "evaluated by the sample product" holds literally.
  - 4.3: fix the expiring-soon day count as whole days rounded down (14 d 11:59:59 → 14) and
    test 14/30/31 against it.
  - 5.2: regenerating changes the full-plan key; the README regeneration paragraph must say to
    update `ContactForm:LicenseKey` in the site's `appsettings.json`.
  - Section 8 / before push: committed demo PEMs may raise GitHub or gitleaks private-key alerts;
    dismiss as test data or allowlist `samples/demo-signing-keys/`.

## Block 4.1–4.2 (reviewer, 2026-11-13, APPROVED after one rework)

- Rework: the renewal test now uses a real `IOptionsMonitor` with a configuration reload (the major from the first review).
- **nit**: `samples/Acme.ContactForm.Tests/LicensingCoreTests.cs:82-88`
  `Demo_is_off_by_default_and_binds_from_configuration` binds with `Get<>` rather than through
  the monitor. Acceptable: it checks the default and the binding only.

## Block 4.3 (reviewer, 2026-11-13, APPROVED)

- **minor** (for 5.1): `samples/Acme.ContactForm/Licensing/ContactFormLicensing.cs:53` a zero or
  negative `max-submissions-per-month` gives `OverLimit` from the first submission. `Check`
  cannot throw; 5.1's controller must record the submission whatever `OverLimit` says (already
  required by "Visitor never blocked"). Optionally treat a negative number as no limit.
- **minor**: `ContactFormLicensing.cs:53` number feature floored and clamped to `int`; safe, but
  add a comment and a test with a fractional or huge number.
- **minor**: `ContactFormLicensing.cs:58-64` a `licensed-domains` of only commas gives an empty
  list, so no host is covered. Strict reading of the spec; untested.
- **nit**: `ContactFormLicensing.cs:46` `host ?? ""` on a non-nullable parameter.

## Block 4.4 (reviewer, 2026-11-13, APPROVED)

- **note**: `FirstActions` uses `#pragma warning disable CS8524` (undefined numeric enum values);
  a new named state still fails the build (CS8509). Reached only from the admin projection.
- **minor**: `samples/Acme.ContactForm/Licensing/AdminLicenseStatus.cs` `Feature` number format
  `"0.############################"`; use `"G"` with the invariant culture.
- **minor**: `LicenseProjectionTests.cs` no dashboard test asserts the claimed identifier, marked
  unverified, for `SigningKeyNotRecognised` or `NotVerified` (spec names `WrongProduct` only).
- **nit**: `AdminLicenseStatus.cs` `BuildDomain` "Not checked: … Host: {host}" lacks a full stop.
- **nit**: `Dashboard_data_with_a_valid_key`/`_damaged_key` are subsets of the all-entries theory;
  kept for the scenario names.

## Block 4.5–4.6 (reviewer, 2026-11-13, APPROVED)

- **minor**: `samples/Acme.ContactForm.Tests/AddContactFormTests.cs`
  `A_site_key_in_configuration_reaches_the_snapshot` configures no key and asserts `Missing`
  (misleading name); the twice-called test asserts only `NotEqual(Missing)` for the perpetual
  full-plan key. Assert `Valid`, and rename or fix the other.
- **minor / risk for section 6**: `samples/Acme.ContactForm/Extensions/ContactFormServiceCollectionExtensions.cs:20`
  the guard returns early on any `ContactFormProduct` descriptor, skipping the options binding.
  Section 6 brief: register demo services after `AddContactForm()` with `Replace` (product and
  licensing get the demo clock; the store keeps the real clock), or guard on a dedicated marker.
- **minor (accepted risk)**: `Extensions/UmbracoBuilderExtensions.cs` `AddContactForm()` is a
  one-line delegate, compile-only, no test.
- **nit**: `LicenseStateLogTests.cs` concurrency test uses 4 threads × 300 iterations; fast and
  deterministic.

## Section 4 (supervisor, 2026-11-13, APPROVED)

- **minor** (section 6.1): `samples/Acme.ContactForm/Licensing/AdminLicenseStatus.cs:140-146`
  dashboard data for "Fourth submission on a three-submission plan" (4 of 3, over limit) and the
  over-limit report clearing in a new month are not asserted. Add with 6.1's controller tests.
- **minor** (section 6.2): `Extensions/ContactFormServiceCollectionExtensions.cs:21` guard on a
  private marker type instead of `ContactFormProduct` (confirms block 4.5–4.6 minor).
- **minor** (section 6.2): `ContactFormServiceCollectionExtensions.cs:28-37` product, licensing
  and store share the container's `TimeProvider`. Never register `DemoClock` as `TimeProvider`;
  replace the product and licensing registrations with one `DemoClock` instance; the store keeps
  `TimeProvider.System`.
- **minor** (section 6.2): `Licensing/ContactFormLicensing.cs:33-38` the expiring-soon clock is
  separate from the evaluator's. Expose `ContactFormProduct.Clock` and use it, dropping the
  constructor parameter, so they cannot differ.
- **minor** (section 5): `samples/README.md` "Licensing/: what to copy": `ContactFormLicensing`
  needs `ISubmissionStore` (a monthly count) and `HostString`; say keep the interface and replace
  only `InMemorySubmissionStore`.
- **minor** (section 6 or 7): `AdminLicenseStatus.cs:148-153` `Domain.Covered` is true with text
  "Not checked" when not `Valid`. Add `Checked`, or the client shows `Domain.Text` with no tick
  unless `Valid`.
- **nit / question for Carl**: `LicenseStateLog.cs:34` the first report each start logs a
  warning, including "now Valid" on a healthy site. Matches the spec; Information for a first
  `Valid` would change the spec wording.
- **nit**: `InMemorySubmissionStore.cs:18-21` unbounded list on a public form; prototype, spam is
  a non-goal. Comment or cap in 5.1.
- **nit** (section 6.1): projection serialisation tests use Web defaults, not the Management
  API's serializer; repeat the no-key/no-details checks on controller output.
- **accepted**: `#pragma warning disable CS8524` in `FirstActions`; `AddContactForm()` compile-only.
- **brief notes**: section 5 passes `Request.Host.Value ?? ""`, records whatever `OverLimit`,
  `DomainCovered` or the state say, adds no state logging of its own, and the README regeneration
  note says to update `ContactForm:LicenseKey` in the site's `appsettings.json`. Section 6.2
  decorates `ILicenseKeySource` via `ActivatorUtilities.CreateInstance<ConfiguredLicenseKeySource>`,
  registers demo services after `AddContactForm()`, reads `Demo:Enabled` at registration (no
  runtime toggle). Section 7 shows a null banner as nothing.
