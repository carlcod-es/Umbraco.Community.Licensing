# Feature requests

Requests for a future change. Not decided, not scheduled. A request that is taken up moves into
a change's design.md as a Q-number and is settled there by a PDR or ADR.

Cite as FR-N.

| FR | Title | Raised | Status |
|---|---|---|---|
| FR-1 | Vendor-chosen key identifier prefix | Carl, 2026-10-09 | Taken into `add-license-core` (design.md Q20, PDR-0023) |
| FR-2 | Vendor issuing console tool | Carl, 2026-10-09 | Open; a later change after `add-license-core` |
| FR-3 | Example product package | Carl, 2026-10-09 | Open; possible later change |
| FR-4 | Standby signing key | Carl, 2026-10-10 | Open; with or after FR-2 |
| FR-5 | Responding to a compromised signing key | Carl, 2026-10-10 | Open; after FR-2 |
| FR-6 | CI and release pipeline | Carl, 2026-10-14 | Taken into `add-release-pipeline` (design.md Q1 to Q10; PDR-0027, PDR-0028, PDR-0029, ADR-0008, ADR-0009) |
| FR-7 | Standard first-action text per state | Library review, 2026-10-20 | Withdrawn for now (Carl, 2026-10-20): the library supplies the state only; vendors write all message text. PDR-0002 unchanged |
| FR-8 | Same status wording across vendors' screens | Library review, 2026-10-20 | Open. Screens stay out of the library (Carl, 2026-10-20) |
| FR-9 | Grace period after expiry | Library review, 2026-10-20 | Open. Until then vendors implement grace themselves (Carl, 2026-10-20) |

## FR-1: Vendor-chosen key identifier prefix

**Request.** A vendor may replace the `LIC-` prefix of the key identifier with its own (e.g.
`ACME-8F3AK-M7RXB-7Q2D`) or omit it (`8F3AK-M7RXB-7Q2D`). Optional; `LIC-` stays the default.

**Personas.** Vendor: branded keys. Site owner and implementor: a key that names its vendor may be
easier to match to a product; but keys from different vendors no longer share one recognisable
shape.

**Affected records.** PDR-0004 (`LIC-` display form; `LIC` ignored when a reference is passed
in), PDR-0020 (identifier format fixed for good), ADR-0001 (identifier pattern),
`docs/license-examples.md`.

**Issues to settle.**

1. **Older products read new keys as *unreadable*.** The identifier is checked before
   verification (PDR-0020). A product built on a library version that knows only `LIC-` reports a
   custom-prefix key as *unreadable*, first action "paste the key again", which cannot fix it.
   *Not supported* (PDR-0018) does not apply: it is reached only after verification. Avoidable
   only if the first released version already accepts any prefix within a prefix rule (see Q
   below).
2. **Prefix rule.** Characters, case and length must be fixed: no `.` (segment separator), no
   whitespace (removed on read), safe in email and environment variables, short enough to keep
   the identifier readable. The prefix is inside the signing input, so it is authentic on a
   verified key and a claim otherwise.
3. **Reference passed in on reissue.** PDR-0004 accepts `lic-8f3ak m7rxb`. `LIC` can be stripped
   unambiguously because `I` and `L` are not in the reference alphabet. A custom prefix made of
   alphabet characters (e.g. `ACME`) is resolved by length only (a reference is 10 characters);
   the rule must say which prefixes are accepted there. Reissue keeps the reference (PDR-0021);
   whether it also keeps the prefix must be decided.
4. **Recognising a key.** A fixed `LIC-` lets implementors, hosts and secret scanners spot a
   license key in settings or logs by one pattern. Custom prefixes weaken this; no prefix removes
   it.
5. **Where the prefix is set.** Per product or per issue; and whether evaluation must be told the
   expected prefix or accepts any prefix matching the rule.

**Open question for `add-license-core` (before keys ship).** Should the first release's reader
accept any prefix matching a prefix rule, while the issuer still writes only `LIC-`? Cheap now;
avoids issue 1 for every product version released before FR-1. Otherwise FR-1 later requires
products to update before custom-prefix keys are issued to their sites.

## FR-2: Vendor issuing console tool

**Request.** A console application, shipped alongside the library, that small vendors use to
issue keys. It uses the library for every signing and verification step and adds what the
library leaves out: records of products, feature definitions and issued keys, and a guided
first-run setup (signing keys, issuing prefix, configuration values).

**Constraints (Carl).** Shipped as a .NET global tool. Its own database: SQL Server or SQLite.
Runs entirely on the vendor's hardware. One vendor per installation: one database, one issuing
setup, one prefix. A vendor needing a second issuer runs the tool from another path with another
database.

**Settled so far.** At setup the vendor chooses one signing key per product, or one shared
across all their products (Carl, 2026-10-10). The library supports both (PDR-0015).

Replacing a signing key (Carl, 2026-10-10). Minimum tool features:

1. **Switch to a new signing key.** Create a pair and make it the issuing key; the old key stays
   in the records, no longer used. One action for leak, loss and routine rotation.
2. **Record the signing key of every issued license.**
3. **Export by signing key.** One row per license key signed by a chosen signing key: reference,
   key identifier, product, issue time, expiry. No key strings. Superseded and expired rows are
   left in; the vendor reads them from the issue time and expiry.

Everything else is vendor guidance:

| | Leak | Loss |
|---|---|---|
| Others can sign? | Yes | No |
| Issued licenses | Replaced; the leaked key is withdrawn from products | Stay valid; the public key stays trusted |
| Tool | Features 1, 3 | Feature 1 |

Leak, in order (no standby key, FR-4):

```
  1  switch issuing to new key B                 (feature 1)
  2  release product v1.1 trusting {A, B}        reissued keys need B trusted on the site
  3  export licenses signed by A                 (feature 3)
  4  reissue each with B; send to site owners    tool, or library reissue (PDR-0013)
  5  release v1.2 trusting {B} only              withdraws A (PDR-0015)
  6  sites update and install their new key      until then the holder of A can still sign
```

A site on v1.2 without its reissued key reads *signing key not recognised*. The vendor chooses
when to release v1.2: sooner closes the leak, later fails fewer sites.

Loss: switch to B; release a version trusting B; new customers on older versions update before
their key works (FR-4 removes this). Guidance: back up every private key.

Further handling of a leak: FR-5.

Per issued license the vendor may store an order number or reference and a customer email,
both optional (Carl, 2026-10-10). Held only in the tool's database, never in a key (PDR-0014).
A non-personal order reference may also go in the key's vendor tag (PDR-0009); that is the
vendor's choice.

**Cross-platform testing (Carl, 2026-10-12).** The change that builds the tool runs the full
test suite (library and tool) on macOS and Linux, on Carl's machines, before it is archived.
The library has run only on macOS so far (`add-license-core`); this closes that gap until CI
exists. Windows is not covered by these machines.

**Personas.** Vendor: issue and reissue from records instead of from the key a site owner
presents. Site owner: unaffected; keys are the same.

**Personal data.** The tool may hold a customer email per license; the library and keys never
hold personal data (PDR-0014). The tool's documentation states that the vendor is responsible
for the data held in its database.

**Affected records.** PDR-0013 (reissue from a presented key stays the path for vendors without
the tool), PDR-0014, PDR-0016 (definitions held by the tool), PDR-0023 (prefix set at setup).

## FR-3: Example product package

**Request.** A sample package showing a product using the library: trusted keys, evaluation,
feature lookups, and what to show for each state.

**Personas.** Implementor and vendor: a working reference.

## FR-4: Standby signing key

**Request.** The vendor tool (FR-2) creates a standby key pair alongside each active signing
key. The standby public key is trusted by the product from its first release but signs nothing.
When the active key is leaked or lost, the vendor switches to the standby key and a new standby
key is created.

**Why.** Signing keys do not expire (PDR-0015), so rotation is forced only by a leak or a loss.
The trusted set changes only with a product release, and sites update late. Without a standby
key, keys issued after a forced rotation read *signing key not recognised* on every site still
running an older product version. With one, any site at most one rotation behind keeps
working.

```
  setup    active A, standby B; product v1.0 trusts {A, B}
  leak/loss of A: switch to B, create standby C; v1.1 trusts {B, C} (drops A if leaked)
  site on v1.0: keys issued with B are valid
```

**Personas.** Site owner: a key bought after a leak or loss works without updating the product.
Implementor: no forced update to install a new key. Vendor: two private keys to protect.

**Library.** No change: a trusted key that has signed nothing is already allowed
(`add-license-core` signing-key-management spec, Rotation).

**Issues to settle.**
1. Settled: the tool offers the standby key at setup, presented as recommended; the vendor
   chooses (Carl, 2026-10-10). Open: creating one later, which helps only once a product
   release trusts it.
2. The standby private key must survive what destroys or leaks the active key: stored and
   backed up apart from it. How the tool guides or enforces this.
3. Withdrawal after a leak still needs a product release dropping the leaked key, and a reissue
   of every license it signed (from the tool's records).
4. A site two rotations behind breaks; the tool could warn before a second rotation.

## FR-5: Responding to a compromised signing key

**Request.** Tool support beyond the FR-2 minimum (switch key, record signing key per license,
export by signing key) for a leaked private key.

**Candidates.**
1. Reissue in bulk every license on the export with the new key.
2. Reach the site owners of reissued licenses, using the customer email where the vendor stored
   one (FR-2).
3. Track which reissued keys were delivered or confirmed installed.
4. Guide the order of steps in FR-2 (product release trusting the new key before reissue;
   release withdrawing the leaked key after delivery).

**Personas.** Site owner: receives a working key before the leaked key is withdrawn. Vendor:
less manual work after a leak.

**Related.** FR-4 (a standby key removes FR-2 step 2), PDR-0013, PDR-0015.

## FR-6: CI and release pipeline

**Request.** Automated build, test and package publishing for the library, closing the
release notes left open by `add-license-core` (its `follow-ups.md`).

**Raised from `add-license-core` follow-ups.**
1. The package version is hard-coded in the project. Supply it from CI, or add a release
   checklist step (ADR-0006).
2. The package records the HEAD commit. Pack and publish only from a clean commit.
3. Run tests on Microsoft.Testing.Platform with `dotnet test --solution` (ADR-0006).

**Also open.** The library has run only on macOS. Windows and Linux test runs, including
signing-key import cost and off-curve key rejection on each platform, are unconfirmed
(`add-license-core` follow-ups, section 3). FR-2 covers Linux manually until CI exists;
Windows has no coverage.

**Personas.** Vendor and implementor: a published package that is built, tested and versioned
the same way every time.

## FR-7: Standard first-action text per state

**Request.** The library supplies one fixed English text per `LicenseState`: what the state means
and its first action, as in the README states table. Vendors show it as is, or replace or
translate it. Today PDR-0002 leaves all message text to the host, so each vendor words
`Expired` or `NotVerified` differently.

**Status.** Withdrawn for now (Carl, 2026-10-20): the library supplies the state only; vendors
write all message text. PDR-0002 ("the library supplies the state, not message text") is
unchanged. Approved earlier the same day, then withdrawn after the explore below.

**Personas.** Site owner (primary): keys from several vendors give the same instruction for the
same state. Implementor: one set of fixes to learn. Vendor: no text to write; can still replace
it.

**Explored before withdrawal (Carl, 2026-10-20).** Starting point if this FR is revisited:
- Audience: one text per state, written for whoever reads it. Rejected: separate site owner and
  implementor texts (double the text; the vendor must know who is looking).
- Content: generic; no product, vendor or claim named. Observed fact, then the first action
  naming who acts in plain words ("the site's developer", "the site owner", "the product's
  vendor"). `Valid` has a text, no first action.
- Language: English only; library-shipped translations would be a separate FR.
- Replacement: per state, whole text, 0 to 8 states, set once per product at setup; blank is a
  setup error; states not replaced keep the standard text.
- Access: standard text and product text for any state, with or without a result. `ToString()`
  unchanged.
- Stability: meaning and first action fixed from 1.0; wording may improve, noted in the release
  log; vendors must not parse it.

**Issues to settle** (as raised; issues 1 to 4 were answered in the explore above).
1. Where the text lives: a lookup by state, a property on `LicenseResult`, or both.
2. Whether the text is part of the stability promise from 1.0 (changing wording is not a key
   format change).
3. Whether it may name the claimed key identifier or product (claims, not facts; PDR-0026).
4. How vendors translate it: replace the whole text, or the library ships translations.

**Out of scope.** Any screen or UI. Where and how the text is shown is the vendor's decision.

## FR-8: Same status wording across vendors' screens

**Request.** A site owner holding keys for several products from several vendors sees each
product's license status in that vendor's own screens, in the same terms.

**Not the library's.** The library has no screens and will not gain any, in the core package or
an add-on. Whether a product shows license status, where (backoffice dashboard, health check,
log, settings page) and how, is entirely the vendor's (Carl, 2026-10-20).

**What the library contributes.** Only data: the state, the verified facts (`VerifiedLicense`),
the claims. Message text is the vendor's (FR-7 withdrawn, Carl, 2026-10-20); the README states
table is the shared reference for each state's meaning and first action.

**Issues to settle.**
1. Whether anything is needed. Candidate: a non-UI way for each product to report its evaluated
   state on a site, so a vendor or third party could build an overview. Any such overview screen
   would be a separate package, not part of this library.
2. Whether consistent wording across vendors is still a goal now that FR-7 is withdrawn, or this
   FR closes.

**Personas.** Site owner: one vocabulary across products. Vendor: unchanged freedom over its own
UI.

**Related.** FR-3 (example product package shows one vendor's screen as an example only), FR-7.

## FR-9: Grace period after expiry

**Request.** Keep a license granting its features for a short period after its expiry, so a
renewal installed a few days late does not switch features off.

**Until taken up (Carl, 2026-10-20).** Vendors who want grace implement it themselves. An
`Expired` result carries the verified license (`result.License`, with `Expires` and `Features`);
feature lookups on the result answer "not granted", so the vendor reads `License.Features`
directly. README vendor notes show the pattern.

**Personas.** Site owner (primary): features do not stop on the morning of a late renewal
(`docs/license-examples.md` example 4). Vendor: one shared rule instead of its own. Site visitor:
no feature disappearing mid-renewal.

**Issues to settle.**
1. Where the grace period is set: by the product (evaluator option, no key change), or by the
   vendor per key (a signed field; a key format change, so decided before 1.0 or not at all,
   PDR-0024).
2. Which state a key in grace reports: `Valid`, `Expired` with lookups answering, or a new state
   (a new state changes PDR-0002 and every host's state table).
3. Whether lookups answer during grace, and whether the product can tell grace from valid to warn
   the site owner.
4. `FeatureList` has no public lookup by name today (`Find` is internal); vendor-built grace
   needs LINQ over the list. A public lookup may be worth adding regardless.

**Related.** PDR-0002 (states and lookups), PDR-0007 (expiry to the second), PDR-0022 (cached
results and expiry).
