## Why

Vendors and implementors have documentation and API reference, but no working product that
shows the library end to end: where trusted keys live, when a key is evaluated, how features
gate behaviour, and what each persona should see for each state. FR-3 asked for one. Carl took
it up on 2026-11-13: a sample Umbraco package, running in a demo site in this repository, that
vendors can read and copy.

## What Changes

- **A fictional product**, Acme Contact Form, built as an Umbraco 17 package that uses the
  library the way a real vendor would. A visitor-facing contact form, with three licensed
  features: a switch (`file-uploads`), a number (`max-submissions-per-month`) and a text
  (`licensed-domains`).
- **Visitors are never affected by licensing.** The form keeps working whatever the license
  state. Unlicensed extras (uploads) are quietly absent; no licensing text, key or state ever
  reaches the public page.
- **A licensing dashboard for administrators** (Settings section): state, the first action for
  that state, key identifier, expiry, what each feature grants and which features are not
  included, and where the key is configured. It never shows the key string.
- **A warning banner for editors** in the product's own Content-section screen (the form
  submissions list) when the license needs attention, is about to expire, is over its monthly
  limit or does not cover this domain. The banner shows no key and no identifier; it points
  the editor to their administrator.
- **Owner warned, visitor not.** Going over the monthly submission limit or running on an
  unlisted domain is reported to administrators and editors; visitors' submissions are still
  accepted.
- **A demo site** in the repository: a seeded page with the form, and the package installed,
  that runs from a clone with no setup beyond a first-run login.
- **Committed demo keys**: a demo-only signing key pair (labelled never to trust) and one demo
  key for each state the library can produce from public issuing (all except `NotSupported`),
  plus Valid variants for a small plan, another domain and "expiring soon".
- **A demo key picker** on the administrator dashboard, shown only when the site's demo setting
  is on: switches the key the product evaluates without editing configuration, until the site
  restarts.
- **Never published.** The sample lives in this repository only, is built and tested on every
  change, and is outside the release.
- Out of this change: a real storage for submissions (kept only while the site runs), email
  sending, the `NotSupported` demo key, a Marketplace or NuGet listing, Umbraco 18.

## Capabilities

### New Capabilities

- `sample-package`: how the sample product uses the library and what it shows each persona:
  evaluation on use, feature gating, the administrator dashboard, the editor banner, visitor
  output with no licensing, and what is logged.
- `sample-demo-site`: running the sample: the demo site and its seeded page, the committed demo
  keys and what each demonstrates, the demo key picker, and the sample's place outside the
  release.

### Modified Capabilities

None. The library and the vendor tool are unchanged.

## Impact

- **New folder** `samples/` holding the sample package, the demo site and their tests, with
  its own solution. It needs an owner in the CLAUDE.md ownership table before work starts.
- **Library** (`src/Umbraco.Community.Licensing`): unchanged. The sample uses its public API
  only, so it also checks that the public API is enough for a real product.
- **Dependencies**: Umbraco 17 and its backoffice client toolchain, for the sample only, named
  in design.md and its ADRs; need Carl's approval before apply. The library and the vendor tool
  gain nothing.
- **CI**: a check that builds and tests the sample. The release run is unchanged and never
  builds or packs the sample.
- **Docs**: a sample README; links from `README.md`, `docs/vendor-guide.md`,
  `docs/implementor-guide.md`, `docs/development.md`.
- **Records**: PDR-0067 to PDR-0069, ADR-0026 to ADR-0028. FR-3 taken into this change.
- **Personas**: vendor (a working reference to copy); implementor (sees which key fails, why
  and where to fix it); site owner (sees what they are licensed for, what expires, what is
  missing); backoffice editor (warned without seeing keys); site visitor (unaffected in every
  state).
