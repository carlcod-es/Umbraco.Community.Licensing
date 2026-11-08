# Feature requests

Requests for a future change. Not decided, not scheduled. A request that is taken up moves into
a change's design.md as a Q-number and is settled there by a PDR or ADR.

Cite as FR-N.

| FR | Title | Raised | Status |
|---|---|---|---|
| FR-1 | Vendor-chosen key identifier prefix | Carl, 2026-10-09 | Taken into `add-license-core` (design.md Q20, PDR-0023) |
| FR-2 | Vendor issuing console tool | Carl, 2026-10-09 | Taken into `add-vendor-tool` (design.md Q1 to Q26; PDR-0030 to PDR-0036, ADR-0011 to ADR-0015). SQL Server dropped (Carl, 2026-10-28). Windows added: design.md Q32, PDR-0038 (Carl, 2026-11-07) |
| FR-3 | Example product package | Carl, 2026-10-09 | Open; possible later change |
| FR-4 | Standby signing key | Carl, 2026-10-10 | Taken into `add-vendor-tool` (design.md Q15, Q16; PDR-0032, PDR-0035). Issue 2 answered by the passphrase; storing apart is FR-15 |
| FR-5 | Responding to a compromised signing key | Carl, 2026-10-10 | Open; after FR-2 |
| FR-6 | CI and release pipeline | Carl, 2026-10-14 | Closed by `add-release-pipeline` (design.md Q1 to Q10; PDR-0027, PDR-0028, PDR-0029, ADR-0008, ADR-0009); first published version `0.1.0-beta.4`, 2026-10-27 |
| FR-7 | Standard first-action text per state | Library review, 2026-10-20 | Withdrawn for now (Carl, 2026-10-20): the library supplies the state only; vendors write all message text. PDR-0002 unchanged |
| FR-8 | Same status wording across vendors' screens | Library review, 2026-10-20 | Open. Screens stay out of the library (Carl, 2026-10-20) |
| FR-9 | Grace period after expiry | Library review, 2026-10-20 | Open. Until then vendors implement grace themselves (Carl, 2026-10-20) |
| FR-10 | Version marker for each release | Carl, 2026-10-27 | Closed by `add-github-release` (design.md Q1 to Q6; PDR-0037, ADR-0016); first GitHub Release `0.1.0-beta.6`, 2026-10-28 |
| FR-11 | Private-key export from the vendor tool | `add-vendor-tool` explore, 2026-10-28 | Open |
| FR-12 | Change the vendor tool passphrase | `add-vendor-tool` explore, 2026-10-28 | Open |
| FR-13 | Richer console prompts in the vendor tool | Carl, 2026-10-28 | Open |
| FR-14 | Retire a feature definition | `add-vendor-tool` explore, 2026-10-28 | Open |
| FR-15 | Standby key stored apart from the active key | `add-vendor-tool` explore, 2026-10-28 | Open |
| FR-16 | Import licenses issued before the vendor tool | `add-vendor-tool` explore, 2026-10-28 | Open |
| FR-17 | Change signing-key mode after setup | `add-vendor-tool` explore, 2026-10-28 | Open |
| FR-18 | Warn on a reused order reference | `add-vendor-tool` explore, 2026-10-28 | Open |
| FR-19 | Change the version line from the release command | Carl, 2026-10-28 | Open; after `add-github-release` |
| FR-20 | Issue several licenses in one vendor tool run | Carl, 2026-10-29 (`add-vendor-tool` update) | Open |
| FR-21 | Optional, vendor-defined call-home | Carl, 2026-11-07 (explore) | Open. Conflicts with the README "Offline" promise; see Issue |

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
exists. Windows is not covered by these machines. Update (Carl, 2026-11-07): `add-vendor-tool` also runs the suite on Windows and checks the tool by hand in PowerShell, Windows Terminal and Command Prompt (design.md Q32, PDR-0038).

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

## FR-10: Version marker for each release

**Request.** When a release is made, mark it by version as well as by the `release-yyyymmdd` tag
the release command already creates (Carl, 2026-10-27, during `add-release-pipeline` task 7.2).

**Personas.** Vendor: finds the source of any published version from its number.

**Issues to settle.**
1. Which form: a git tag per version (e.g. `v0.1.0-beta.4`), a GitHub Release (version, note,
   package attached), or both.
2. A second tag must not start a release run; only `release-*` tags do (PDR-0029).
3. Who creates it: the release command before the run, or the release run after a successful push
   (a marker for a failed attempt would point at an unpublished version).

**Related.** PDR-0027, PDR-0029, ADR-0009.

**Closed** by `add-github-release` (Carl, 2026-10-28): a GitHub Release on the existing release
tag, created by the release run after publishing (PDR-0037, ADR-0016). Issue 2 does not arise: no
second tag. First Release: `0.1.0-beta.6`.

## FR-11: Private-key export from the vendor tool

**Request.** Larger vendors create signing keys with the tool, then issue from their own store
with the library. The store needs the private key as an unencrypted PEM.

**Until taken up (Carl, 2026-10-28).** The tool targets small vendors only (PDR-0030); no
private key leaves it (PDR-0032). Keys are stored in a standard encrypted format, so a vendor can
open one with the passphrase and common tools.

**Issues to settle.** Confirmation and warning before export; recording that a key was exported
and when; whether a larger vendor needs the full setup.

**Related.** PDR-0032, ADR-0014, `pre-1.0-considerations.md` C2.

## FR-12: Change the vendor tool passphrase

**Request.** Re-encrypt every private key under a new passphrase, e.g. after the old one may have
been seen.

**Issues to settle.** Older backups still open with the old passphrase only; the guide must say
which passphrase each backup needs.

**Related.** PDR-0032.

## FR-13: Richer console prompts in the vendor tool

**Request.** Selection lists, coloured tables and progress in the tool's prompts, e.g. with
Spectre.Console (Carl, 2026-10-28).

**Related.** ADR-0012 (BCL console prompts in `add-vendor-tool`).

## FR-14: Retire a feature definition

**Request.** Mark a feature as no longer offered: refused on new keys, dropped on reissue with a
notice.

**Related.** PDR-0036 (add-only), PDR-0016.

## FR-15: Standby key stored apart from the active key

**Request.** Export the standby private key to a separate encrypted file the vendor keeps
elsewhere, so it survives "folder copied and passphrase known" and "no backup" (FR-4 issue 2).

**Related.** PDR-0032, PDR-0035.

## FR-16: Import licenses issued before the vendor tool

**Request.** Record licenses a vendor issued with the library before using the tool, by reading
their key strings, verified against the vendor's keys.

**Issues to settle.** The vendor's existing private key must be brought into the tool (setup with
an existing key); key strings are not kept after reading (PDR-0033).

**Related.** PDR-0013, PDR-0033.

## FR-17: Change signing-key mode after setup

**Request.** Move an installation from one signing key per product to one shared key, or the
reverse, without a new installation.

**Issues to settle.** Which key survives or is created; every product's next release must trust
it before it signs; standby keys; the trusted lists during the move (PDR-0035).

**Related.** PDR-0030, PDR-0035, FR-5.

## FR-18: Warn on a reused order reference

**Request.** When an issue, reissue or renewal is given an order reference already held by an
issue entry, warn (by hand) or refuse unless confirmed (scripted), so a store script run twice
does not issue twice for one order.

**Related.** PDR-0033 (order reference per issue entry), PDR-0030 (batched use).

## FR-19: Change the version line from the release command

**Request.** The release command asks whether to keep the version line or move it, so moving
from `0.1-beta` to `0.2-beta` (numbering restarts at `0.2.0-beta.1`) needs no separate hand
edit and commit (Carl, 2026-10-28). An extra question on every release is acceptable (Carl,
2026-10-28); a command-line flag was the alternative offered.

**Personas.** Vendor: a minor bump is part of the release that brings the change, so the
version and its note are made together.

**Issues to settle.**
1. Choices offered: keep, next minor beta (`0.M+1-beta`), `1`; after 1.0, keep or next major.
   Whether any other line can be typed.
2. Refusals: going backwards or sideways (e.g. `0.2-beta` to `0.1-beta`, or a line already in
   the log).
3. Going to `1`: refuse, warn or remind about the pre-1.0 all-platform step and the README
   coverage line (PDR-0028, PDR-0024).
4. The confirmation shows the line change; the `Release <version>` commit holds the line, the
   version and the log row together.
5. Whether hand-editing `VersionLine` stays allowed.

**Related.** PDR-0027 (Carl changes the line by hand; amended by the change that takes this up),
PDR-0024, PDR-0028, ADR-0009, `docs/releasing.md` "Changing the version line".

## FR-20: Issue several licenses in one vendor tool run

**Request.** One vendor tool action issues several licenses, for example from an input file,
asking the passphrase once and decrypting each signing key once, so batched CLI use does not
pay the passphrase check and key decryption per license (Carl, 2026-10-29). Raised when the
`add-vendor-tool` scenario "Passphrase asked once" (three licenses in one action) was dropped:
the change issues one license per run.

**Personas.** Vendor: batched issuing from their own process (PDR-0030) is faster and the
passphrase is supplied once. Site owner: keys unchanged.

**Issues to settle.**
1. Input: file format, standard input, or repeated arguments.
2. Output: how each key string is paired with its request on standard output.
3. A refusal part-way: all or nothing, or per-license results.
4. Whether reissue, renew and resend batch too.

**Related.** PDR-0030 (batched use), PDR-0032 (passphrase once per run), FR-18.

## FR-21: Optional, vendor-defined call-home

**Request.** A product can optionally call a service the vendor builds and hosts, and the vendor
defines that call: when it is made, what it sends, and what the product does with the answer
(Carl, 2026-11-07). The library makes the call and handles the answer; the vendor's service is
the vendor's own. Whether the library also supplies the vendor's side is open (question 1).

**Issue.** This reverses a headline promise. The README says "**Offline.** A key is checked
entirely on the site. No licensing server, no phone-home, no network call, nothing to go down",
and the license-validation spec says an evaluation SHALL work with no network access. The
README "Limits of offline licensing" (no revocation, no binding, trusted clock, bearer token)
are the limits call-home would address. The change that takes this up must reword the promise
(for example "works fully offline; calling home is optional and off by default") and state
which limits still apply. Evaluation itself stays offline and under 1 ms (PDR-0022): a call is
never made inside a page request.

**Principle proposed (explore, 2026-11-07).** The signed key stays the source of truth. An
answer can inform, take away (binding mode only) or deliver a new signed key; it can never grant
anything by itself. A forged answer can then at most take away, and must first pass the check
that it came from the vendor.

**Personas.**
- Site owner (primary): a renewed key arrives before expiry; a vendor outage or a vendor going
  out of business must not switch off a product they paid for.
- Implementor: needs to see that a product calls home, what it sends, when it last succeeded,
  and why it fails; firewalled or air-gapped sites must keep working; local, staging and
  production must not count as three sites.
- Vendor: revocation (refunds, leaked keys, FR-5), activation and seat limits, status messages.
- Backoffice editor: cannot trigger calls; sees only what the product chooses to show.
- Site visitor: never in the page path; never sees licensing messages; no failure reaches a page.

### Explored options

**Capabilities.** A later change may take up some, not all.

| Capability | Serves | Risk to |
|---|---|---|
| Revocation check | Vendor | Site owner, if a wrong answer switches off a paid product |
| Activation / seat limit | Vendor | Implementor (environments counted as sites) |
| Key refresh (renewed or reissued key fetched by the site) | Site owner | Little, if optional |
| Status messages from the vendor | Site owner, implementor | Editor if noisy; never the visitor |
| Usage reporting | Vendor | Site owner privacy (PDR-0014) |

**Modes.**

| Mode | If the vendor service never answers | Suits |
|---|---|---|
| Off (today) | Key alone decides | Everyone who wants offline only |
| Advisory | Key alone decides; answers add messages and new keys only | Site owner |
| Binding | Key decides for a grace window, then not licensed | Vendor (revocation and seats work) |

Explore leaning: off by default; advisory when enabled; binding as an explicit vendor opt-in
with a library-set minimum grace window.

**When: a fixed menu of triggers the vendor combines**, not free rules, so implementors see the
same behaviour across products. Named presets may sit on top ("status only", "auto-renew",
"activation").

```
 installed       ...in use...        expiry window      expires      after expiry
     |                                   |                 |              |
 [on install /   [on site start]    [N days before]   [on becoming   [while expired,
  key change]    [every N hours/      expiry]           expired]       every N]
                  days]
                 [on demand: a person presses "check now"]
                 [product asks explicitly, e.g. before taking a seat]
                 [retry after failure, backing off]

 NEVER: inside a page request.
 Library enforces: minimum gap between calls, calls spread across sites, a maximum frequency.
```

An optional trigger: state not valid (`NotSupported`, `WrongProduct`), asking whether a working
key or product version exists.

**What: payload in three groups.**

```
  SAFE (base payload)          CAREFUL (vendor enables;       NEVER
                               disclosed to site owner)
  -------------------          ------------------------       -----
  trigger that fired           site address / domain          full key string (bearer token)
  key identifier (PDR-0023)    environment name               editor names or emails
  product ID, license ref      installation ID (random,       site visitor data
  signing key ID                 per site)                    site content
  library and product version  usage figures named by the     personal data (PDR-0014)
  locally evaluated state        vendor (e.g. orders vs
  expiry, last-checked time      max-orders)
  proof the key is held        vendor-defined extra fields
```

The connection itself reveals the site's network address; vendor documentation says so.

**After the answer.**

| Answer | What happens |
|---|---|
| Confirmed | Last-successful time recorded and visible to the implementor |
| Replacement key | Verified locally like any key (signature, product, trusted signing key), then handed to the host to store |
| Revoked | Binding mode only; changes the result (question 9) |
| Seat granted / limit reached | Binding mode only; as revoked |
| Message | Passed to the product to show the site owner or implementor; vendor writes all text (FR-7 withdrawn) |
| Next-check hint | Adjusts the schedule within the library's bounds |
| Unreachable, error, or not proven to come from the vendor | The mode's fallback rule |

**Requirements found in explore** (to carry into the change's specs):
1. Never called inside a page request; evaluation stays offline and under 1 ms (PDR-0022).
2. Off unless the vendor enables it.
3. The site proves it holds the key without sending the key.
4. The answer proves it came from the vendor; an unproven answer counts as no answer.
5. A new key from an answer is verified exactly as an installed key is.
6. The implementor can see what would be sent, what was sent, and when a call last succeeded.
7. A binding answer survives a restart; a revoked site taken offline does not get its license
   back.
8. Calls are rate-limited and spread across sites; failures back off.

### Questions to answer

> **Blocking: may an answer ever reduce what a valid signed key grants?** Advisory only keeps
> PDR-0002 unchanged and the change small, but leaves revocation unsolved. Binding allows
> revocation and seats, and brings questions 9 to 12. Most questions below depend on this
> (question 2).

1. **Scope of the library.** Client only: the library defines what is asked and what answer is
   expected, and each vendor builds a service to that contract. Or client and vendor side: the
   library also supplies the answering side, possibly next to the vendor tool's records
   (PDR-0030, PDR-0033, PDR-0034); a service is a different audience and operation from a
   console tool.
2. **Advisory or binding** (the blocking question above).
3. **Who may turn it off.** Can the implementor or site owner disable call-home for a product?
   In advisory mode no harm follows; in binding mode it equals being unreachable. Persona
   priority favours the site owner.
4. **Trigger menu.** Which triggers are offered; whether presets are offered; the library's
   minimum gap, maximum frequency and spreading rule.
5. **Where the vendor defines it.** Fixed in the product, in the signed key (a key format change,
   to decide before 1.0 or not at all: PDR-0024, `docs/pre-1.0-considerations.md`), or both.
   Can different keys for one product call home differently?
6. **Site identity.** Address, environment name, installation ID, or none; which are on by
   default; how environments are told apart so they are not counted as seats.
7. **Vendor-defined payload fields.** Allowed or not; name, type and length rules (PDR-0010,
   PDR-0017); no personal-data scanning, as PDR-0014.
8. **Disclosure.** How the site owner and implementor are told a product calls home and what it
   sends: vendor documentation only, or something the library shows.
9. **Revoked and seat-refused states.** A new evaluation state (amends PDR-0002 and every host's
   state table) or a flag on an otherwise valid result. First action and audience for each.
10. **Fallback when unreachable** in binding mode: grace window length, who sets it, the library
    minimum, and what the site owner is told during it.
11. **Remembering answers.** What the host must keep across restarts, and what happens when that
    store is lost or cleared.
12. **A clock check.** Whether the service's time may be used to detect a site clock set back
    (README "site's clock is trusted"), and what follows.
13. **Replacement keys.** Applied at once or only after the host stores them; whether a key that
    grants less, expires sooner or is older than the installed key is accepted; whether the
    implementor is told.
14. **Messages.** Audience (site owner, implementor; never visitor), length limits, and whether
    the backoffice editor may see them.
15. **Who may trigger on demand.** Implementor and site owner only; how the product enforces it.
16. **Not-valid trigger.** Whether a `NotSupported` or `WrongProduct` key may call home to ask
    for a working key or product version.
17. **Usage reporting.** In scope at all, or left to vendors outside the library.
18. **README and positioning.** The new wording of the "Offline" promise and the "Limits of
    offline licensing" section.

**Related.** PDR-0001 (an older key stays valid: "inherent offline limit"), PDR-0002 (states),
PDR-0010 and PDR-0017 (field rules), PDR-0014 (personal data), PDR-0015 and PDR-0035 (trusted
signing keys), PDR-0022 (evaluation per request), PDR-0023 (key identifier safe to share),
PDR-0024 (format permanent from 1.0), PDR-0030, PDR-0033, PDR-0034 (vendor tool records), FR-5
(compromised signing key), FR-7 (vendor writes message text), FR-9 (grace period), README
"Limits of offline licensing", `docs/pre-1.0-considerations.md` (binding as a signed field).
