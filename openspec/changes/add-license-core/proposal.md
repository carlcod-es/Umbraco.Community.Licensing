## Why

Umbraco package vendors have no shared way to license paid packages: each rolls its own key
scheme or ships unprotected. Site owners then face a different scheme per package, and
implementors cannot tell which key is failing or why. This change builds the core: a library
that issues license keys and checks them entirely offline, with no host dependency, so a later
host-side change (Umbraco key sourcing, inventory, backoffice screen) has a settled base.

Seed: `CLEAN-PROJECT-PROMPT.md` (2026-10-09). Open questions from its section 10 settled with
Carl on 2026-10-09 at the start of this change (design.md Q1 to Q4).

## What Changes

- **License generation** (vendor side): checks a license's contents against the issue rules
  and signs them into a pasteable, one-line key string. Reports every problem with a request at
  once, each naming its field. An optional prefix chosen at issuing setup (default `LIC`, or
  none) brands the key identifier; it is branding only. Optionally checks features against the
  vendor's declared feature names and types, catching wrong types and misspelled names before a key ships. Keeps
  no records. Builds a reissue request from a verified
  license, keeping its product and reference, so a vendor without records can add to the key a
  site owner presents.
- **License validation** (product side, at runtime): evaluates zero or one key string for one
  product and returns one result: missing, unreadable, wrong product, signing key not
  recognised, not verified, not supported, expired, or valid. Never throws because of key
  content. An unverified key may identify itself; it never describes or grants an entitlement.
  Feature lookups are typed (switch, number, text) and answer only on a valid key, for a feature
  of their own type. Cheap enough to evaluate on every request.
- **Signing-key management** (both sides): creates signing key pairs whose IDs are derived
  from the public key; exports and imports each half; holds a trusted set of public keys
  addressed by ID; supports rotation by trusting old and new keys side by side.
- **Decision records**: the first PDRs (product rules) and ADRs (signing, payload, packaging,
  API, example keys), their index READMEs, and `docs/license-examples.md` with worked examples
  whose key strings are produced by the library and checked by tests.

One key per product per site (PDR-0001). A license change is a reissue that replaces the
installed key; there is no combining of keys.

## Capabilities

### New Capabilities

- `license-generation`: the key identifier prefix, issue rules for a license's contents (product ID, reference, expiry,
  display name, vendor tag, features, length limits), optional feature definitions, the issued
  key string, the all-problems rejection, and building a reissue request from a verified
  license.
- `license-validation`: reading a supplied key string, the ordered evaluation states, what a
  result reports for each state, strict reading of verified contents, and feature lookup.
- `signing-key-management`: key-pair creation, derived signing key IDs, export and import of
  each half, the trusted set, and rotation.

### Modified Capabilities

None. No specs exist yet.

## Impact

- New library project and test project under `src/` and `tests/`, added to
  `Umbraco.Community.Licensing.slnx`. No existing code.
- One published package with no Umbraco or host dependency (ADR-0003).
- New docs: `docs/decisions/` (PDR-0001 to PDR-0023), `docs/adrs/` (ADR-0001 to ADR-0005),
  `docs/license-examples.md`. `README.md` and `CLAUDE.md` status lines updated.
- Personas served: site owner (what am I licensed for, what lapsed), implementor (which key,
  why, where to fix), vendor (issue without records, gate features). Backoffice editor and site
  visitor are protected: no result carries a full key, and evaluation never throws on a bad
  key.

### Out of scope

- Vendor issuing console tool: a .NET global tool on the vendor's hardware with its own
  database, product definitions, issued-key records and guided setup (`docs/feature-requests.md`
  FR-2). It uses this library; the library keeps no records.
- Example product package (`docs/feature-requests.md` FR-3).
- Host-side Umbraco package: key sourcing from configuration, environment variables and vault;
  inventory; backoffice screen; package registration; product dependencies.
- Revocation, online validation, machine or domain binding, clock-rollback protection
  (inherent offline limits, documented).
- Several licenses for one product on one site; add-ons with a term of their own (unless sold
  as their own product).
- Dropped: trial or standard license kinds, release-date gating, text set, combining licenses.
