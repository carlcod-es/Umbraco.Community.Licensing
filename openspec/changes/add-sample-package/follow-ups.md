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
- **nit / question for Carl** (resolved by Q24, PDR-0071; task 4.7): `LicenseStateLog.cs:34` the first report each start logs a
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

## Block 4.7 (reviewer, 2026-11-13, APPROVED)

- Acceptance met: "Good license at startup", "Expiring soon logged" (at startup and crossing into
  30 days while running, once), "Key expires" (Error), "Log holds no key", two-thread once.
- **nit**: `samples/Acme.ContactForm/Licensing/LicenseStateLog.cs` `?? "unknown"` expiry-date
  fallback is unreachable (expiring soon implies an expiry). Harmless.

## Section 4 re-review with 4.7 (supervisor, 2026-11-13, APPROVED)

- **nit (records, next opsx:update)**: `docs/decisions/0068-sample-shows-each-persona.md:40`
  "(Levels amended by PDR-0071.)" understates the change; PDR-0071 also changes the trigger from
  a state change to a condition change. Suggested: "(Amended by PDR-0071: one message per
  condition change, level by condition.)"
- **risk (6.2)**: `DemoClock` must be the one clock given to both the evaluator and
  `ContactFormLicensing`; test "DemoClock only for the dated entry" through `Check`'s
  `ExpiringSoon`, not the evaluator alone.
- **risk (6.2)**: each picker choice changes the condition, so a demo session logs Errors and
  Warnings on purpose; say so in "Demo/: delete when copying".
- **risk (5.2)**: committed `appsettings.json` must not raise the minimum level above
  Information for `Acme.*`, or a healthy start logs nothing.
- **note for Carl**: renewing an expiring-soon key logs one Information line (condition back to
  plain `Valid`). Follows the spec; no scenario names it.

## Block 5.1 (reviewer, 2026-11-13, APPROVED)

- Acceptance met at handler and view-model-builder level for every demo key.
- **minor**: `Form/ContactFormSurfaceController.cs` glue (ModelState, TempData, redirect) untested;
  a `SurfaceController` needs Umbraco services and no mocking package is allowed. The rendered
  page is checked in 5.2 (run the site) and 10.2.
- **minor**: `ContactFormSurfaceController.cs:27` `[RequestSizeLimit]` 1 MB: an oversized post gets
  a bare 413 and is not recorded, in every state (not a licensing refusal; design.md "Public
  form" requires the cap). Untested beyond the constant.
- **minor**: `Views/Shared/Components/ContactForm/Default.cshtml` does not repopulate fields after
  a validation error.
- **minor**: `ContactFormHandler.cs:41`, `ContactFormViewComponent.cs:11` a throwing key source
  would fail the page; `Check` itself has no throw path. Configured source reads options only.
- **nit**: `ContactFormHandler.cs:37` an empty file counts as no file.

## Block 5.2 (reviewer, 2026-11-13, APPROVED; task not ticked, see below)

- Restores `samples/Acme.ContactForm.Site/appsettings.Development.json`, deleted by accident in
  `fc1f052` (Carl confirmed); byte-identical to `0b8ec61`.
- Carl's added acceptance: no config effort for a cloner (no file to copy or edit, no env var,
  no user secret, no dev certificate). Met: http-only first launch profile `http`.
- **minor**: `Acme.ContactForm.Site/Properties/launchSettings.json` changed from CRLF to LF, so
  the diff is the whole file. Restore CRLF or accept; optionally `.gitattributes` (Architect).
- **minor**: `DemoContentSeeder` idempotence has no automated test; two-run evidence only.
- **nit**: `DemoContentSeeder` reads the view with `File.ReadAllTextAsync` (BOM dropped from the
  string); no runtime rewrite observed.
- **nit**: `http` profile `launchBrowser: false`, unlike the others.
- **Architect finding (out of scope, for triage)**: spec "Fresh clone" (sample-demo-site
  spec.md:99) requires the source "Configured key"; `Licensing/ILicenseKeySource.cs`
  `ConfiguredLicenseKeySource.Name` is "Site setting ContactForm:LicenseKey" (since 4.1). No test
  asserts the name. 5.2 acceptance "Fresh clone" not met as written until resolved.

## Section 5 (supervisor, 2026-11-13, APPROVED conditional on the 5.2 rework; rework APPROVED by reviewer)

- Out-of-scope triage: "Configured key" source name rated **minor**; fixed in the 5.2 rework
  (`ConfiguredLicenseKeySource.Name = "Configured key"`, literal asserted; "Fresh clone" at data
  level in `SiteSettingsTests`; dashboard display in 10.1).
- Rework also: upload cap enforced in `ContactFormHandler` (file over 1 MB not read, submission
  kept); `[RequestSizeLimit]` dropped (observed ineffective under Umbraco: "request body size
  limit could not be applied", 2 MB post accepted); seeder checks `Attempt.Success`;
  `launchSettings.json` CRLF restored.
- **minor (records, for Carl / opsx:update)**: design.md "Public form" says "Request size is
  capped at 1 MB for the upload"; the request body is now accepted up to Kestrel's default
  (~30 MB) and only the stored upload is capped. Reword, or add a site-wide body limit.
- **nit**: `DemoContentSeeder.cs:64` logs `createdType.Result`, line 43 logs `created.Status`;
  make both log the status.
- **nit**: seeder failure path and surface controller glue untested (prototype bar).
- **nit**: `Views/home.cshtml` has a BOM and no final newline (Umbraco write-back form); 10.1:
  check `git status` clean after the first start.
- **risks**: 6.2 picker's first entry uses `ConfiguredLicenseKeySource.Name`; one `DemoClock`
  for evaluator and licensing, store keeps `TimeProvider.System`. 10.4: a picker change between
  render and post drops the file silently (spec-conformant). 6.1: assert 4-of-3 dashboard data in
  controller tests. 7.2: show `KeySourceName` as given; setting name and env var separately.
  8: CI never exercises the seeder or the cap; any smoke step uses the `http` profile.

## Block 6.1 (reviewer, 2026-11-13, APPROVED)

- Acceptance met: per-action policies (no class-level policy; reflection tests), `license/status`
  returns the 4.4 admin data (incl. 4 of 3 and the new-month reset), `submissions` returns the
  month's list and banner; serialised with Umbraco's `ConfigureUmbracoBackofficeJsonOptions` for
  every demo key. OpenAPI document and operation IDs not exercised without a host: 6.3.
- **nit**: `ApiControllerTests.Submissions_banner_from_the_controller_holds_no_license_details`
  builds a second `ContactFormLicensing`; expose it on `Rig`.
- **nit**: `SubmissionItem`/`SubmissionsResponse` declared in `Controllers/SubmissionsController.cs`.
- **nit**: `Composers/AcmeContactFormApiComposer.cs` keeps template comments (commented-out
  `Contact`, doc links) and a blank line after `Compose(`.
- **note**: `submissions` returns visitor-entered text verbatim (intended for Content users).
- **for section 7**: client still calls the removed template endpoints and the old
  `acmecontactform` route; regenerate from `acme-contact-form`.

## Block 6.2 (reviewer, 2026-11-13, APPROVED; already in Carl's commit 807973b)

- Acceptance met: "Choose Expired", "Restart", "Settings unchanged", "Expiring soon at its demo
  date", "Demo setting off", "Editor tries to choose" (attribute level), README "Demo/: delete when
  copying". Optional `DemoKeySelection? demo = null` resolves as null through `ActivatorUtilities`.
- Decisions: `license/status` → `LicenseStatusResponse(Status, DemoSelection, DemoDate)` (Architect,
  keeps `Licensing/` demo-free); `POST demo/select` body `{ "entry": "<name>" }`, "configured" or
  "Configured key" → configured key; unknown entry → 400 ProblemDetails, selection unchanged;
  `DemoDate` `yyyy-MM-ddTHH:mm:ssZ`.
- **minor**: `samples/Acme.ContactForm.Tests/DemoPickerTests.cs:429-440`
  `A_user_refused_by_the_policy_...` exercises no refusal (fresh rig, `Chosen == null`). The real
  guard is the attribute test at 413-427. Rename, or observe in 10.3.
- **minor**: `DemoKeySource.Read()` and `DemoClock.GetUtcNow()` read `Chosen` separately; a change
  between them inside one `Check` could pair one entry's key with another's date. Negligible for
  the demo.
- **nit**: `Demo/ContactFormDemoServiceCollectionExtensions.cs:1-5` usings out of order.
- **nit**: `samples/README.md` "in the same request body" wording.

## Block 6.3 (worker-backend run, Architect spot-check, 2026-11-13; no repo files changed)

- Document `acme-contact-form` (OpenAPI 3.0.4, "Acme Contact Form Backoffice API" 1.0) served at
  `http://localhost:15329/umbraco/swagger/acme-contact-form/swagger.json` (`http` profile). Four
  paths under `/umbraco/acme-contact-form/api/v1/`: `GET demo/entries` (GetDemoEntries),
  `POST demo/select` (PostDemoSelect), `GET license/status` (GetLicenseStatus), `GET submissions`
  (GetSubmissions); each carries `Backoffice-User`. Property names with "key": `keyIdentifier`,
  `keyIdentifierVerified`, `keySourceName`; none carries the key string. `git status` unchanged
  after the run.
- Export kept outside the repo (Architect scratchpad); section 7.1 regenerates from a running site.
- **for 7.1**: `Client/package.json` `generate-client` points at
  `https://localhost:5000/umbraco/swagger/acmecontactform/swagger.json`; use the URL above.
- **note**: the site ignored the first SIGINT under the worker's background run; SIGTERM stopped it
  cleanly.

## Section 6 (supervisor, 2026-11-13, APPROVED)

Baseline for review: `35221e3`. No blockers or majors.

- **minor (records drift, before 7.1)** (resolved by opsx:update, 2026-11-13): design.md:222 and :174-176 do not name
  `LicenseStatusResponse` and its demo fields, the `{ "entry" }` body or the 400 for an unknown
  entry; the `Check` paragraph still says "the injected `TimeProvider`" (now `ContactFormProduct.Clock`).
  opsx:update.
- **minor (PDR-0069 "removed in one step", decide before 7.1)** (Carl agreed 2026-11-13; task 6.4): removing the demo takes three steps
  (delete `Demo/`, edit `LicenseStatusController.cs` and `UmbracoBuilderExtensions.cs`). Option:
  `license/status` returns `AdminLicenseStatus` directly (selection and date already come from
  `demo/entries`), and a `Demo/` composer `[ComposeAfter(typeof(ContactFormComposer))]` calls
  `AddContactFormDemo`, so deleting `Demo/` is the only step.
- **minor (before 7.1)** (Carl agreed; task 6.4): `Licensing/AdminLicenseStatus.cs:151-158` `Domain.Covered` true with "Not
  checked" when not `Valid` (open since section 4). Add `bool Checked`, or 7.2 shows a tick only
  when `Valid`.
- **minor** (task 6.4): `samples/README.md` does not describe `Controllers/`, `ContactFormApi.cs` or the API
  composer; add the endpoint/policy table and the per-action policy lesson.
- **minor** (task 6.4 rename; 10.3 amended by opsx:update): `DemoPickerTests.cs:429-440` rename (refuses nothing); add "POST `demo/select` as the
  editor is refused, dashboard unchanged" to 10.3 via opsx:update.
- **nit**: `LicenseStatusController.cs` reads `demo?.Chosen` twice; read once.
- **nit**: `Demo/ContactFormDemoServiceCollectionExtensions.cs` copies `ContactFormLicensing`'s
  constructor arguments; and wraps a new `ConfiguredLicenseKeySource`, dropping a host's own
  source in demo mode (comment).
- **nit**: no test serialises `demo/entries` and asserts no key string (safe by construction).
- **risks**: 7.1 `generate-client` URL (see block 6.3); 7.2/7.4 show `KeySourceName` as given and a
  null banner as nothing; 8: CI never exercises the OpenAPI document or real policy refusals.
- **still open for Carl**: design.md "Public form" 1 MB request-cap wording (section 5).

## Block 6.4 (reviewer, 2026-11-13, APPROVED)

- Acceptance met: `license/status` returns `AdminLicenseStatus` only; `Demo/ContactFormDemoComposer`
  `[ComposeAfter(typeof(ContactFormComposer))]`; no demo service referenced outside `Demo/`;
  scratch copy builds with `Demo/` deleted; `Domain.Checked` per demo key; README "Controllers/";
  test renamed; export repeated (Architect checked: `license/status` 200 → `AdminLicenseStatus`,
  `AdminDomainStatus` {host, checked, covered, text}, key-named properties only `keyIdentifier`,
  `keyIdentifierVerified`, `keySourceName`).
- Remaining non-`Demo/` references kept by the Architect: `ContactFormOptions.Demo` (design.md puts
  the `Demo:Enabled` default there) and the `ContactFormProduct.DemoPublicKey` constant.
- **minor (accepted deviation)**: `Acme.ContactForm.csproj:56` `EmbeddedResource` for
  `Demo\demo-keys.json` has `Condition="Exists(...)"` so deleting `Demo/` is one step; a missing
  file now drops the resource silently (demo key tests fail at run time).
- **minor**: `Demo/ContactFormDemoComposer.cs` still runs if a host disables `ContactFormComposer`;
  documented dependency, unverified Umbraco behaviour. README note or guard optional.
- **minor**: `samples/README.md` Controllers/ "answers 401 otherwise" is untested; soften or observe
  in 10.3.
- **nit**: README "(and `Client/src/demo/` once the client has it)" forward-looking.
- **for section 7**: the dashboard reads the demo selection and demo date from `demo/entries`;
  `license/status` has no demo fields.

## Section 6 re-review with 6.4 (supervisor, 2026-11-13, APPROVED)

- design.md matches the code; specs hold (demo selection via `KeySourceName` and `demo/entries`
  `Selected`; demo date via `demo/entries`); no spec or PDR change. csproj condition and
  `ContactFormOptions.Demo` acceptable against PDR-0069.
- **minor**: `samples/Acme.ContactForm/Demo/DemoController.cs:13` `DemoEntryItem` doc says
  `DemoDate` is set "while the entry is chosen"; it is the entry's date whether chosen or not. Fix
  the comment (worker-backend, next backend block).
- **minor**: `Demo/ContactFormDemoComposer.cs` with `ContactFormComposer` disabled, the demo
  `Replace` registers a `ContactFormLicensing` with missing dependencies. Fix: `AddContactFormDemo`
  calls `AddContactFormLicensing` first (marker makes it safe), or a README note.
- **nit**: `AdminDomainStatus.Covered` stays true when `Checked` is false; consider
  `Checked && DomainCovered`.
- **nit**: the 2 skipped tests are the explicit generators (as since section 3).
- **risks for section 7**: 7.1 regenerate after 6.4 from
  `http://localhost:15329/umbraco/swagger/acme-contact-form/swagger.json`. 7.2 shows covered only
  when `domain.checked`, otherwise `domain.text`; `keySourceName` as given. 7.4 refreshes itself and
  the dashboard after 204, shows the 400 `detail`, treats 404 as "no picker". **Client-side
  one-step removal**: nothing outside `src/demo/` may import from it (e.g. a separate manifest
  entry in `src/demo/`); proposed 7.4 acceptance: delete `src/demo/` in a scratch copy, then
  `npm run build` (needs opsx:update; Carl to decide).
- Section 8 / 10.3: real 401/403 refusals and the OpenAPI document are observed only.

## opsx:update (2026-11-13, Carl's answers)

- Resolved: design.md "Public form" reworded (upload read capped at 1 MB, request body at the server
  default); task 5.1 marked amended. 7.4 gains the client one-step removal check; design.md "Removed
  in one step" says the picker loads through its own manifest entry in `src/demo/`.
- Resolved records nits: tasks 2.2/2.4 "(amended by 2.5, PDR-0070)"; design.md generator writes
  `Demo/demo-keys.json` and the field list names `productVerified`; PDR-0068 amendment note
  reworded; ADR-0026 prose rewrapped (table rows left as they are).

## Block 7.1 (reviewer, 2026-11-13, APPROVED)

- Acceptance met: client regenerated from `http://localhost:15329/umbraco/swagger/acme-contact-form/swagger.json`
  (four SDK functions), template dashboard and greetings removed, `npm run build` strict, no `any`
  in hand-written code, lockfile unchanged.
- **note**: the regenerated `src/api/**` files are LF where the template's were CRLF, so the diff is
  large (~144/277 real lines). Optional (Architect): `.gitattributes` `eol=lf` for `Client/src/**`.
- **note**: `client.gen.ts` bakes `baseUrl: 'http://localhost:15329'`; `configureClient` replaces it at
  runtime (`auth.context.js` `client.setConfig({ baseUrl: serverUrl, ... })`).
- **for 7.2-7.4**: generated calls throw on error by default; a 404 (demo off) rejects with an empty
  body. Call `getDemoEntries`/`postDemoSelect` with `throwOnError: false` and read `response.status`
  and `error` (400 ProblemDetails).
- **nit**: `entrypoint.ts:25` empty `onUnload` body; `dashboards/manifest.ts` exports `[]` until 7.2.
- Generation needs a live site, so "generated matches the document" is not reproducible in CI.

## Block 7.2-7.3 (reviewer, 2026-11-13, APPROVED)

- Acceptance met in code and build; the observed checks are Carl's (10.2, 10.3). Lit escapes all
  visitor text; no `unsafeHTML`.
- Extension point for 7.4: window event `acme-contact-form-license-changed` (dashboard re-fetches)
  and `<umb-extension-slot type="acmeContactFormLicensePanel">` (custom types work in 17's slot).
- **minor (folded into 7.4)**: `license-dashboard.element.ts:91-94` shows "no longer in effect"
  twice (server `grants` already ends with it); the `included ? grants : "not included"` branch
  duplicates the server. Render `grants`; use `included`/`inEffect` for styling only.
- **minor (folded into 7.4)**: `license-dashboard.element.ts:52` client "Perpetual"; render
  `expiry` only.
- **minor (folded into 7.4)**: a static import of `./demo/manifest.js` in `bundle.manifests.ts` would
  break one-step removal; use `import.meta.glob("./demo/manifest.ts", { eager: true })` with
  `/// <reference types="vite/client" />`.
- **minor (accepted)**: tag texts ("unverified", "covered"/"not covered") and row labels are client
  layout text.
- **nit**: exported `LICENSE_CHANGED_EVENT`/`LICENSE_PANEL_EXTENSION_TYPE` must not be imported by
  `src/demo/`; make them module-private.
- **nit**: the submissions dashboard does not re-fetch on the license-changed event (different
  section; re-mount re-fetches).

## Block 7.4 (reviewer, 2026-11-13, APPROVED)

- Acceptance met: picker in the dashboard's `acmeContactFormLicensePanel` slot, shown only on a
  200 from `demo/entries`; entries in server order with descriptions; selection marked; refresh
  via the window event; demo-selection and demo-date notices; 400 `detail` shown. `npm run build`
  passes; the reviewer repeated the scratch copy with `src/demo/` deleted (builds, no demo chunk).
  Dashboard folds done (server `grants`/`expiry` as given; constants module-private).
  Observed 10.4 deferred to Carl.
- **nit**: `src/demo/demo-picker.element.ts:30-31` `#load()` has no try/catch; a network throw is an
  unhandled rejection (picker still renders nothing).
- **nit**: `demo-picker.element.ts:11` literal "Configured key" duplicates the server's name (spec
  names it literally).

## Section 7 (supervisor, 2026-11-13, CHANGES REQUESTED → rework)

- **major** (rework, worker-backend): `samples/README.md` lacked a "Client/" section and still
  described `Client/src/demo/` as future. Fixed in the README rework block.
- **minor (records)** (resolved by opsx:update, Carl 2026-11-13): design.md "Backoffice API and
  client" records the add-on points (slot type `acmeContactFormLicensePanel`, window event
  `acme-contact-form-license-changed`); "Removed in one step" names the `import.meta.glob` seam.
  ADR-0027 "Template patches" now says none was needed on 17.8.1.
- **minor (line endings)** (Carl 2026-11-13: LF for `Client/**`): `.gitattributes` gains
  `samples/Acme.ContactForm/Client/** text eol=lf`. **Carl**: run
  `git add --renormalize samples/Acme.ContactForm/Client` as its own commit.
- **nit**: `src/demo/demo-picker.element.ts:67` demo date shown raw (`yyyy-MM-ddTHH:mm:ssZ`).
- **nit**: `demo-picker.element.ts:45` cast of `result.error` to `{ detail?, title? }` (not `any`).
- **risks**: section 8: Node 24 ≥ 24.13 on Ubuntu, `npm ci`; check esbuild's Linux binary resolves
  (local npm warns "allowScripts"); CI cannot check `src/api/**` against the live OpenAPI document.
  Section 9: `docs/development.md` Node 24 and the generate-client prerequisite. 10.3: calling
  `license/status`/`demo/select` as the editor needs devtools or a token.

## Section 7 README rework (reviewer, 2026-11-13, APPROVED)

- Finding fixed: "Client/" section added; Demo/ lines corrected.
- **nit**: `samples/README.md` Client/ says the only client words are labels and the three tags;
  the two load-failure messages are client text too.

## Section 7 re-review (supervisor, 2026-11-13, APPROVED)

- Major resolved; README, design.md and ADR-0027 agree with the code. `.gitattributes` rule scoped
  to `Client/**` (no binaries tracked there). Index: 22 files LF, 9 CRLF until Carl's renormalize.
- **nit** (widens the README rework nit): `samples/README.md:119` client-written text also includes
  the load-failure messages and the picker's heading, notice, "Demo date:" label and error
  fallbacks. Reword in a later worker-backend block (9.1).
- **risks**: commit `git add --renormalize samples/Acme.ContactForm/Client` before section 8; CI
  cannot check `src/api/**` against the live OpenAPI document (section 8 to accept or add a check).

## Block 8.1 (reviewer, 2026-11-13, APPROVED; task not ticked: GitHub run deferred to PO)

- Acceptance: workflow content, "Library change", "Release run", every step run locally (npm ci,
  npm run build, run-demo test 7/7, dotnet build 0 warnings, dotnet test 281 passed / 2 explicit
  skipped) met. Green GitHub Actions run: deferred to Carl.
- **minor (Carl to decide)**: `.github/workflows/sample.yml` paths omit `Version.props`, which the
  library csproj imports; a change there does not trigger the sample run. Add it to both path
  lists (opsx:update to design.md's path list) or accept.
- **nit**: `node-version: 24` floats; `run-demo.mjs` needs ≥ 24.13 but CI does not run it; latest
  24.x is above that.
- **note**: local `npm ci` warns that esbuild/fsevents install scripts are not covered by
  `allowScripts`; confirm esbuild works on the first Ubuntu run.

## Section 8 (supervisor, 2026-11-13, APPROVED; 8.1 open until Carl reports the GitHub run)

- Workflow matches design.md and ADR-0026; all commands re-run (sample, root solution, tools
  148/148). Nothing found that fails only on Linux (import casing, csproj separators, esbuild and
  rollup Linux binaries in the lockfile, untracked local files handled).
- **minor (accepted by Carl 2026-11-13)**: `Version.props` not in the paths. It
  holds only version strings; `ci.yml` catches a malformed file; adding it means an Umbraco restore on
  every version bump and an opsx:update to design.md and ADR-0026.
- **nit**: `node-version: 24` floats (consistent with `ci.yml`); no `workflow_dispatch` (beyond design).
- **risks**: confirm esbuild on the first Ubuntu run; do not make "Sample" a required check (path
  filter would leave other PRs waiting); demo PEM alerts to dismiss; no NuGet cache (slower, correct);
  9.1: `docs/development.md` gives Node 24.13+ and the local equivalent of `sample.yml`.
- 8.1 ticked 2026-11-13: Carl reported "Sample / build-test" green on PR #7 (esbuild fine on Ubuntu).

## Block 9.1 (reviewer, 2026-11-13, APPROVED)

- Acceptance met: links in README.md ("What it does", Documentation table), docs/vendor-guide.md,
  docs/implementor-guide.md, docs/development.md (Node 24.13+, run, CI-equivalent commands,
  generate-client prerequisite, secret-scanning note); samples/README.md reviewed end to end; every
  link and anchor resolves (Architect's check); every samples/README.md command run on macOS by the
  worker (generate-client: no diff; generators run and restored); sample build 0 warnings, tests
  282 passed / 2 explicit skipped; run-demo tests 9/9; tools tests 148/148 on Node 24 (quoted glob).
- Folded in (Carl 2026-11-13): run-demo.mjs `spawnPlan` (no `shell: true`, DEP0190; Windows path
  tested by unit test only); `DemoEntryItem.DemoDate` comment; `AddContactFormDemo` calls
  `AddContactFormLicensing` first, with test; Controllers/ 401 observed by hand (unauthenticated
  curl, all four endpoints) and stated as such.
- **nit**: `samples/README.md` "`Licensing/` itself holds no demo code" sits oddly beside
  `ContactFormOptions.Demo` (`DemoOptions`, setting only; accepted by Carl). Reword or leave.
- **nit** (worker): `Demo/ContactFormDemoComposer.cs` doc comment still frames `[ComposeAfter]` as
  required; with `AddContactFormLicensing` called first it is only the order that makes the demo's
  `Replace` win. Reword later or leave.
- **note**: the swagger document carries no doc-comment text, so the comment change needs no client
  regeneration.

## Section 9 (supervisor, 2026-11-13, APPROVED)

Baseline for review: HEAD. Validation re-run: sample build 0 warnings, 282/2 skipped; root solution
1673 passed / 2 skipped; run-demo 9/9; tools 148/148 (unquoted glob on Node 20). No records drift
from this section (design.md:284 still true; `[ComposeAfter]` kept, no longer load-bearing).

- **minor** (vendor persona): secret-scanning advice too broad and inconsistent.
  `samples/demo-signing-keys/README.md:21` and `docs/development.md:161-164`: dismiss as "used in
  tests" only for `samples/demo-signing-keys/demo.pem` / `untrusted.pem` after checking the demo
  warning; any other path is a real leak (revoke, rotate); drop "false positive".
- **minor (out of scope, rated minor)**: `design.md:112` layout lists `Api/  Management API
  controllers`; the code has `Controllers/` and `DemoController` in `Demo/`. Next opsx:update.
  Resolved by opsx:update 2026-11-16: design.md layout tree now lists `Controllers/`.
- **minor (section 10 readiness)**: `samples/README.md` Controllers/ does not say how to call an
  endpoint as a given back-office user (Swagger UI at `/umbraco/swagger`, `acme-contact-form`
  document), nor that the default Editors group has Content but not Settings access (10.3).
- **nit**: `README.md:33` "every state" and `docs/implementor-guide.md:33` "each state" overclaim
  (`NotSupported` not demonstrated).
- **nit**: `docs/implementor-guide.md:32` "Editors see only a short banner" reads as library
  behaviour; "In the sample, ...".
- **nit**: `samples/README.md` ~150: mention `ContactForm__Demo__Enabled=false` for 10.4 instead of
  editing a tracked file.
- **risks for section 10**: this working copy's gitignored `appsettings.Local.json` overrides the
  admin login (`sample-admin@example.test`) and `umbraco/Data` is already installed, so the
  README login works only from a fresh clone; the shell's default Node is 20 (`nvm use 24`);
  Windows npm path unit-tested only; root README links resolve only after merge to main.

## Section 9 nits block (reviewer, 2026-11-13, APPROVED; folded in at Carl's go-ahead)

- Resolved: secret-scanning advice (both texts agree; only `demo.pem`/`untrusted.pem` with the
  warning; any other file is a leak: stop trusting, rotate); Controllers/ how to call an endpoint as a
  user (Swagger UI, Development only) and Editors group without Settings (seeded DB: content, media,
  forms); "every state but `NotSupported`"; "In the sample, ... editors see a short banner";
  `ContactForm__Demo__Enabled=false` for one run (POSIX and PowerShell; holds by code reading).
- **minor (observe in 10.3)**: `samples/README.md:107` Swagger "Authorize and sign in" and "Try it
  out" steps are described from the UI config, not completed. Carl's 10.3 run exercises them; reword
  if they differ.
- **minor/nit**: `samples/README.md` Demo/ PowerShell `$env:` stays set for the terminal session; add
  `Remove-Item Env:ContactForm__Demo__Enabled`.
- Not observed: demo off with the env var (unauthenticated is 401 either way); 10.4 observes it.

## Section 9 re-review (supervisor, 2026-11-13, APPROVED)

Reviewed `git diff --cached 6e51a19` (docs only). All supervisor findings resolved; both
secret-scanning texts agree and match the vendor-guide rotation.

- **nit**: `samples/README.md:109` "Content, Media and Forms": Forms is not installed; say Content and
  Media.
- **nit**: `samples/README.md:107` "Swagger UI is served in development only": confirm Umbraco 17's
  condition in 10.3, or say "in the demo site (Development)".
- **nit**: `docs/development.md:165`, `samples/demo-signing-keys/README.md:21`: "still starts with the
  warning" is a weak check; stronger is unchanged in git history or public key matching
  `demo.public.txt`/`untrusted.public.txt`. Optional.
- **risk**: in Debug, `appsettings.Local.json` is added after environment variables, so a
  `ContactForm:Demo:Enabled` there beats the documented env var. Carl's Local.json does not set it.
  One clause in samples/README.md later.
- Carried: fresh clone for 10.1; `nvm use 24`; Windows npm path unit-tested only; root README links
  resolve after merge; 10.3 confirms Swagger Authorize/Try it out; 10.4 observes the env var.

## Block 11.1 (reviewer, 2026-11-16, APPROVED)

Acceptance met. Forwarding (307 on `/` and `/umbraco`) and the authorize request (302 to login,
no ID2083) observed by the worker with `curl`, using the standard `umbraco-back-office` client
query rather than one captured from the login page; Carl's 10.1 exercises the real sign-in.
README heading printed by `run-demo.mjs`: "Trust the development certificate" (task 11.4 adds it).

- **nit**: `samples/run-demo.mjs` main: the `spawnSync` of the certificate check and
  `check.error ? 1 : check.status` are untested (thin; mapping covered by `certFailureMessage`).
- **nit**: `samples/run-demo.mjs` `certFailureMessage`: hard-codes `https://localhost:44365`,
  duplicating `launchSettings.json`.
- **nit**: `samples/Acme.ContactForm.Site/Program.cs` ~19: doubled blank line before
  `UseHttpsRedirection`.
- **note**: `UseHttpsRedirection` knows the https port only under a launch profile or
  `ASPNETCORE_URLS`; `dotnet run --no-launch-profile` would not redirect. The script always passes
  the profile.

## Block 11.2 (reviewer, 2026-11-16, APPROVED)

Acceptance met: worker ran two starts from empty data, the Expired check via `POST demo/select`
and the old-data warning; reviewer repeated the two starts and found the views byte-identical
after seeding (`git status` unchanged). Carl's `umbraco/Data` moved aside and restored, identical.

- **deviation (accepted, minor)**: the layout is set in `Views/_ViewStart.cshtml`, not inside the
  templates: Umbraco's template service reads `Layout = "..."` in a template as a master template
  (`MasterTemplateNotFound`). Matches design.md "plain Razor layout, not an Umbraco template".
  design.md "Home and Contact pages" could name `_ViewStart.cshtml` at the next opsx:update.
- **note**: `Views/home.cshtml` and `contact.cshtml` are committed with a UTF-8 BOM, LF, no
  trailing newline, matching what Umbraco writes when it creates the templates. Do not strip the
  BOM. (The worker's report said it stripped it; the files show it did not.)
- **minor**: `Views/_Layout.cshtml:12` `<title>`: an empty heading gives " - Acme".
- **nit**: `Views/_ViewImports.cshtml`: mixed line endings (HEAD CRLF, four added `@inject` lines LF).
- **nit (out of block, Architect)**: `.gitattributes` has no `eol` rule for the site's `Views/*.cshtml`;
  on Windows with `core.autocrlf=true` the checkout gives CRLF and Umbraco may rewrite as LF,
  dirtying `git status`. Untested. Candidate: `samples/Acme.ContactForm.Site/Views/*.cshtml text eol=lf`.

## Block 11.3 (reviewer, 2026-11-16, APPROVED)

Acceptance met: `generate-client` reads the https address; build passes; the worker's
`generate-client` run against the site changed only `baseUrl` in `src/api/client.gen.ts` and
`types.gen.ts`. No `15329` left in the client.

- **nit**: git warns `Client/package.json` has CRLF in the working copy, normalised to LF on the
  next touch; diff is one line.

## Block 11.4 (reviewer, 2026-11-16, APPROVED)

Acceptance met by reading and by the worker's run on macOS (`--check --trust` rc 0; site, `/umbraco`,
Swagger JSON 200; plain address 307; `generate-client` no new diff). Not run: `--trust` (password
prompt), deleting `umbraco/Data`, PowerShell. Folded in the Section 9 README nits (Content and
Media; Swagger in the demo site's Development environment; `Remove-Item Env:...`; Local.json
overrides the env var).

- **minor**: `samples/README.md:60` "removes the demo administrator": the next start recreates it
  from the committed unattended settings (or `appsettings.Local.json`). Reword: "...and any content
  you changed. The next start installs again and recreates the demo administrator from the
  committed settings (or from your `appsettings.Local.json`, if you have one)."
- **minor (Architect check)**: `samples/README.md:18` links
  `enforcing-ssl#trust-https-certificate-on-linux`; the page exists but on the .NET 10 view the
  confirmed anchor is `#trust-https-certificate-on-linux-with-linux-dev-certs` (headings are
  version-gated). The page also says `dotnet dev-certs https --trust` is official on Ubuntu and
  Fedora only; SLES/openSUSE/RHEL need manual `certutil` steps.
- **nit**: `samples/README.md:52-54`: say the site name and footer text are edited on the Home page.
- **nit**: worker addition beyond the brief: the "Data from an earlier version" consequence
  sentence (see the minor above).
- **minor (Architect, after review)**: `samples/README.md` "First run" step list says `run-demo.mjs`
  "Runs `dotnet run --project samples/Acme.ContactForm.Site`"; it now first runs
  `dotnet dev-certs https --check --trust` and starts with `--launch-profile https`.

## Section 11 (supervisor, 2026-11-16, APPROVED)

Baseline: HEAD. Validation re-run: sample build 0 warnings; 282 passed / 2 skipped; run-demo 13/13;
client build passes. Nothing under `src/`, `tests/`, `.github/` changed.

- **minor (fix before 10.1 recommended)**: `samples/README.md:30-34` "First run" step list: add
  "Checks the development certificate with `dotnet dev-certs https --check --trust`; if it is not
  trusted, prints the trust command and starts anyway." and "Runs `dotnet run --project
  samples/Acme.ContactForm.Site --launch-profile https`." (same as the Block 11.4 entry).
- **minor**: `samples/README.md:60` admin-recreated wording (Block 11.4 entry).
- **minor**: `samples/README.md:18` Linux anchor: use
  `#trust-https-certificate-on-linux-with-linux-dev-certs` or link the page without an anchor.
- **minor (PDR-0073, vendor/implementor)**: `samples/README.md` "Home and Contact" does not name the
  files that make up the site (`Composers/DemoContentSeeder.cs`, `DemoContentComposer.cs`,
  `Views/_ViewStart.cshtml`, `_Layout.cshtml`, `home.cshtml`, `contact.cshtml`,
  `wwwroot/css/site.css`); add one sentence.
- **minor (record drift, next opsx:update)**: design.md "Home and Contact pages" (~343): add that
  the layout is applied through `Views/_ViewStart.cshtml`, because Umbraco reads `Layout =` in a
  template as a master template. No ADR amendment.
- **nits** (already recorded): `_Layout.cshtml:12` title; `_ViewImports.cshtml` mixed endings
  (make all CRLF); `Program.cs:17-18` blank line; `run-demo.mjs:87` hard-coded address.
- **out-of-scope triage, rated minor, folded in by the Architect**: `.gitattributes` gains
  `text eol=lf` for `samples/Acme.ContactForm.Site/Views/home.cshtml` and `contact.cshtml` only
  (both already LF in the index; BOM kept). A wildcard was avoided: `_ViewImports.cshtml` is mixed
  and `Views/Partials/**` are CRLF in the index.
- **risks for section 10**: 10.4: check `appsettings.Local.json` does not set
  `ContactForm:Demo:Enabled` before using the env var. 10.5: after deleting the data, the admin is
  recreated from `appsettings.Local.json` if it sets one, not the documented login. 10.1: an older
  untrusted dev certificate needs `dotnet dev-certs https --clean` then `--trust` (not in the
  README). The ID2083 fix was checked only by curl; 10.1 is the first real browser sign-in.
  Windows checkout untested.

## Section 11 README fixes (reviewer APPROVED after one rework; supervisor re-review APPROVED, 2026-11-16)

Carl approved fixing the four `samples/README.md` minors before section 10. Resolved: First run
step list (certificate check, `--launch-profile https`); deleting the data recreates the demo
administrator (committed settings or `appsettings.Local.json`); Linux link anchor
`#trust-https-certificate-on-linux-with-linux-dev-certs` with the Ubuntu/Fedora note; a paragraph
naming the site's files (rework: the seeder reads only the two template views; layout and
stylesheet are used by the views). No new findings.

Still open: design.md `_ViewStart.cshtml` drift (next opsx:update); nits listed under "Section 11";
section 10 risks (older untrusted dev certificate needs `dotnet dev-certs https --clean` then
`--trust`; check `appsettings.Local.json` for `ContactForm:Demo:Enabled` before 10.4).

## opsx:update (2026-11-17, Carl's answers)

Section 10 partial run recorded (valid key works, invalid key blocks access); the rest deferred
to PO, sign-off needed before release. New: PDR-0074 (hidden submissions, masked email), PDR-0075
(About text, footer repository link; "no licensing outside the form" narrowed to license state),
PDR-0076 (default key 5 a month); design.md Q28 to Q31, R15, R16; tasks.md 10.6 and section 12.
Resolved here: design.md now names `Views/_ViewStart.cshtml` (Section 11 drift). The
`DemoKeyGenerator` filter also matching `DemoSigningKeyGenerator` is handled in task 12.3.

## Block 12.1 (reviewer, 2026-11-17, APPROVED)

Acceptance met. Build 0 warnings; 297 passed / 2 skipped (generators). Visibility uses the count
of this month's listed submissions (real clock), agreeing with the spec; one `Check` per request.

- **minor**: `samples/Acme.ContactForm.Tests/ApiControllerTests.cs` `Over_the_limit_hides_only_the_later_ones`
  checks only `Name` on the hidden fourth item; add `Null` asserts on `Message` and `ReceivedUtc`
  (the serialisation test covers all five fields for an `Expired` item only).
- **nit**: same file, `Fixing_the_license_shows_them_again`: shown items not checked for
  `ReceivedUtc` or `Note == null`.
- **nit**: same file declares its own `HiddenNote` constant rather than `SubmissionItem.HiddenNote`;
  deliberate (pins the spec wording). Leave.
- **for 12.2**: `SubmissionItem` positional order changed (`Hidden` first); the regenerated client follows.

## Block 12.2 (reviewer, 2026-11-17, APPROVED)

Acceptance met. `src/api/` diff limited to `SubmissionItem`. Worker verified in a signed-in
headless Chrome (built-in Node WebSocket, no dependency): "Expired" chosen, two submissions, two
masked lines with the server's note under the banner; site left on "Configured key". Hidden
items render as a list after the shown-rows table (`uui-table-cell` has no colspan).

- **nit**: `submissions-dashboard.element.ts`: a hidden item with a null `note` would show only
  the email. Acceptable.
- **nit**: the list-after-table layout relies on the server ordering hidden items after shown
  ones (true by the rule); documented in a code comment.
- **note**: Carl's working copy was installed with `appsettings.Local.json`, so its admin is the
  one set there, not the demo login (known; README says so). 10.x runs use a fresh clone.

## Block 12.3 (reviewer, 2026-11-17, APPROVED)

Acceptance met; no findings. Set re-issued with the unchanged README filter `*DemoKeyGenerator*`:
it cannot match `DemoSigningKeyGenerator.WriteDemoSigningKeys` (reviewer confirmed), so only
`DemoKeyGenerator.WriteDemoKeys` ran. New full-plan key `ACME-7Q8DQ-G7NUK-TTMJ` in the site's
`appsettings.json`; old identifier gone from the repo; `samples/demo-signing-keys/` unchanged.
Fresh start from a scratch copy (worker): `Valid`, "0 of 5 submissions used this month".

- **records note**: the follow-up in "opsx:update (2026-11-17)" that the filter also matches
  `DemoSigningKeyGenerator` was wrong; 12.3's conditional README change was not needed.

## Block 12.4 (reviewer, 2026-11-17, APPROVED)

Acceptance met (start-ups from empty data, Expired check and section-11 data warning on the
worker's scratch-copy evidence; code reading agrees). About text verbatim from PDR-0075;
`home.cshtml` keeps BOM, LF, no trailing newline.

- **note (accepted)**: `wwwroot/css/site.css` gains `.about { white-space: pre-line; }` so the
  Textarea's line breaks show; not named by the task.
- **tooling note**: the `umbraco-swagger` authorize request answers 400 `invalid_request` with
  `scope=offline_access`; omit `scope`.

## Block 12.5 (reviewer, 2026-11-17, APPROVED)

Acceptance met; links resolve; statements match 12.1 to 12.4.

- **nit (kept)**: `docs/vendor-guide.md` "Weigh this against the site owner's interests" is new
  advice; follows PDR-0074's persona note.

## Section 12 (supervisor, 2026-11-17, CHANGES REQUESTED → rework)

Baseline: HEAD. Validation: sample build 0 warnings; 297 passed / 2 skipped; client build; run-demo
13/13; library solution builds. Nothing under `src/`, `tests/`, `.github/`,
`samples/demo-signing-keys/` changed.

- **F1 major** (sent back to worker-backend): hidden-submission tests short of design.md "Test
  strategy" (every non-`Valid` state; full plan 5 of 5 and 7 of 5; "Short parts and no at sign";
  "Fixing the license shows them again" on the full plan).
- **F2 minor (for Carl)**: `samples/README.md:134` "or wait for a new month ... shows them all in
  full" is half true: a new month helps only over the limit, and the screen lists only this
  month. Suggested: "Fix the license and the next request shows this month's submissions in full.
  Over the limit, a new month starts the count again for new submissions; the screen lists only
  the current month."
- **F3 minor (for Carl, Architect via opsx:update)**: tasks.md 10.3 and 10.6 depend on this month's
  in-memory count, which carries across picker changes and earlier steps; with the default key at
  5, 10.3's "Valid and within limits" and 10.6's "first five in full" need a restart first (or
  the earlier submissions counted in).
- **F4 nit**: hidden items render as a list after the table, not "rows" (no colspan in
  `uui-table-cell`); behaviour matches the spec.
- **risks**: 12.2 layout relies on hidden items following shown ones; section 10 uses a fresh clone
  (Carl's working copy has a different admin from `appsettings.Local.json`); Node 24 for CI/docs.

## Section 12 rework (reviewer, 2026-11-17, APPROVED)

F1 resolved: not-valid theory over the six non-`Valid` demo entries (guarded by
`Not_valid_covers_six_states`), full plan at 5 of 5 and 7 of 5, `Short_parts_and_no_at_sign`,
"Fixing the license shows them again" on the full plan with shown-item checks. F2 resolved
(Carl approved folding in): README sentence reworded. 307 passed / 2 skipped. No findings above nit.

## Section 12 re-review (supervisor, 2026-11-17, APPROVED)

F1 and F2 resolved; no regressions. Validation: sample build 0 warnings; 307 passed / 2 skipped;
client build (Node 24); run-demo 13/13; library solution builds. Nits only (test's own copy of the
note text, kept deliberately; F4 list vs rows). F3 (tasks.md 10.3/10.6 count dependence) to
`opsx:update` at Carl's choice.

## opsx:update (2026-11-17, Carl's answer to section 12 F3)

tasks.md 10.3 ("Valid and within limits") and 10.6 ("Over the limit hides only the later ones")
now say to restart the site first, so this month's in-memory count starts at 0. tasks.md only;
design.md and PDR-0076 already say a restart clears the count.
