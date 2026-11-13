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
