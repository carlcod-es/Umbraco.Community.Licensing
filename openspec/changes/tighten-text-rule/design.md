## Context

See proposal.md (Why). Current state: `src/Umbraco.Community.Licensing/Format/ContentRules.cs`
`CheckText` checks the display name and text feature values for the issuer
(`LicenseIssuer`, `FeatureDefinitions`) and for the strict reader (`PayloadReader`), so issue
and read share one function. It refuses only unpaired surrogates, control characters (by
`Rune.GetUnicodeCategory`, plus U+2028 and U+2029, ADR-0002), leading or trailing whitespace
(`char.IsWhiteSpace`) and lengths outside 1 to 64 or 1 to 256 scalar values. The library
targets `net10.0`; .NET 10's built-in Unicode data is version 16.0 (probed 2026-11-08), and a
site may run a later runtime with a later version. Tests that rely on emoji being accepted:
`KeyLengthSupport.Emoji` (oversized-key and exact-limit tests in `LicenseIssuerTests` and
`ReissueTests`) and the accepted-text cases in `PayloadReaderTests`. Example key strings
(`ExampleKeyStrings`, `docs/license-examples.md`) hold only ASCII text, so none changes.
Release tooling is Node 22 with `node:test` and no npm packages (ADR-0009).

Behaviour: `specs/license-generation/spec.md` (Text rule, Display name rule, Feature rules, Key
string shape) and `specs/license-validation/spec.md` (Strict reading of verified contents).

## Bar

**Production.** The rule decides whether a site owner's key reads as *valid* or *not
supported*, and a narrowing after release is not reversible for issued keys. Every spec
scenario has an automated test; the table is cross-checked against an independent source
(.NET 10's own Unicode data) for every code point; the generator has unit tests.

## Goals / Non-Goals

**Goals:** one rule, judged identically at issue and read on every runtime and operating
system; no runtime Unicode data in the rule; a table reproducible from published Unicode data.

**Non-Goals:** normalisation or look-alike detection; changes to the other fields' rules, the
lengths, the payload encoding or the public API; the vendor tool's typed fields
(`add-vendor-tool` Q40).

## Requirements

| R | Requirement | Spec requirement | Personas | Records |
|---|---|---|---|---|
| R1 | One text rule for display name and text values: printable ASCII; outside ASCII letters, marks, digits, punctuation of any script, less ignorable by default | Text rule; Display name rule; Feature rules | Site owner (what is shown is what was issued); vendor (one rule) | PDR-0042 |
| R2 | Same answer on every machine, by one fixed Unicode version | Text rule | Implementor (issue and read agree) | PDR-0042, ADR-0018 |
| R3 | Rule holds at read: a verified key breaking it is *not supported* | Strict reading of verified contents | Implementor; site owner | PDR-0042, PDR-0018 |
| R4 | Key string stays printable ASCII; oversized non-ASCII text refused at issue | Key string shape | Implementor | PDR-0025 |

Persona check: R1 can harm the vendor and site owner holding beta keys with emoji or invisible
characters (reissue, allowed by PDR-0024). No requirement harms a higher-priority persona.

## Questions settled in this change

| Q | Question | Answer | Record |
|---|---|---|---|
| Q1 | Which characters are allowed | The rule below | PDR-0042 |
| Q2 | Text feature values: own rule or shared | Shared with the display name. ASCII symbols (`= + < > ~ \| $ ^`) stay allowed, so technical values such as `example.com,shop.example.com` or `eu+uk` stay valid (Carl, 2026-11-08) | PDR-0042 |
| Q3 | Which scripts count as letters | Any script, by Unicode at a fixed version; moving to a later version is a widening. Consequences below (Carl, 2026-11-08) | PDR-0042 |
| Q4 | Two forms of the same accented letter | Both accepted and signed exactly as supplied, as today. The vendor tool handles matching for its own searches (Carl, 2026-11-08) | PDR-0042 |
| Q5 | Spaces inside the text | Only the ASCII space; repeats allowed, as today. Other space characters refused (Carl, 2026-11-08) | PDR-0042 |
| Q6 | Length limits | Unchanged: 1 to 64 (display name), 1 to 256 (text value), counted in characters as today (Carl, 2026-11-08) | PDR-0042, PDR-0017 |
| Q7 | Shared with the vendor tool | Moved to `add-vendor-tool` design.md Q40: a vendor-tool decision (Carl, 2026-11-08) | none (moved) |
| Q8 | Invisible characters Unicode classes as letters or marks (Hangul fillers, combining grapheme joiner, Mongolian variation selectors, Khmer inherent vowels) | Refused: everything Unicode marks ignorable by default is refused, whatever its category (Carl, 2026-11-08) | PDR-0042 |
| Q9 | Which Unicode version | 16.0.0, the version .NET 10 ships, so the table can be cross-checked (Architect) | ADR-0018 |
| Q10 | Where the character data comes from | A table generated from Unicode's published data by a Node script and committed as C#; no runtime Unicode data in the rule (Architect) | ADR-0018, amends ADR-0002 |
| Q11 | Unicode licence notice | `THIRD-PARTY-NOTICES.md` at the root, packed into the package; data files fetched at generation time, not committed (Carl, 2026-11-08) | ADR-0018 |

### Rule (Q1, Q8)

Allowed:
- Every printable ASCII character, space included (unchanged from today).
- Outside ASCII, from any script: letters (`Müller`, `Ακρόπολη`, `コマース`, `Łódź`), combining
  marks (an accent typed as a separate mark; vowel signs in Hindi or Thai), decimal digits, and
  punctuation (`– — « » ¿ ¡ ・ 、`).

Refused:
- Emoji and other symbols outside ASCII (`⭐ ❤ ™ © ® € ± →`).
- Emoji building blocks: the keycap mark, variation selectors, skin-tone modifiers.
- Invisible formatting: zero-width space and joiners, text-direction overrides, soft hyphen.
- Any other character Unicode marks ignorable by default, even when classed as a letter or a
  mark (Q8).
- Space characters other than the ASCII space (no-break, ideographic, en and em spaces).
- Numbers that are not decimal digits (`① ² ½ Ⅻ`).
- Control characters, U+2028 and U+2029 (as today).
- Characters the fixed Unicode version does not assign, private-use characters, unpaired
  surrogates (as today).
- Leading or trailing whitespace (as today).

Trade-off accepted: the boundary for symbols is ASCII, not a category (`$ +` allowed, `€ ±`
refused), so nothing valid today in ASCII is narrowed. "No symbols at all" was rejected: it
would refuse text values valid today.

Refused now, candidates to allow later (each a widening):
- Zero-width joiners (Persian and some Indic spelling).
- Mongolian variation selectors (some Mongolian spelling).
- Ideographic space (typed by Japanese keyboards).
- `™ ®` and currency outside ASCII (`€ £`).

### Consequences of any script at a fixed Unicode version (Q3)

- Issue and read give the same answer on every machine for the life of a key; the answer does
  not depend on the system's own Unicode version.
- Characters Unicode adds later are refused until the rule moves to a later version, a planned
  format change: sites on older product releases read keys using them as *not supported*.
- Allowed is not the same as displayed: a font or terminal without a script shows boxes. To
  state in the vendor documentation and tool guide, and to check in the terminals PDR-0038 names.
- Non-Latin text lengthens keys (up to 4 bytes per character before encoding); the oversized-key
  refusal (PDR-0025) already covers it.
- Look-alike letters across scripts do not matter in a display name (never looked up); they
  matter for the vendor tool's email search (`add-vendor-tool` Q37).
- Lengths count characters as stored, so an accent typed as a separate mark counts as one more.

## Decisions

### Fixed version and table (ADR-0018)

Unicode 16.0.0. The allowed set outside ASCII (categories L, Mn, Mc, Nd, P, less
Default_Ignorable_Code_Point; 791 ranges) is a generated file,
`src/Umbraco.Community.Licensing/Format/TextRuleTable.g.cs`: sorted inclusive ranges as a
`static ReadOnlySpan<int>`, the constant `UnicodeVersion`, and the source files' SHA-256 in the
header. Alternatives (runtime categories, Unicode 17.0, a C# generator, a bitmap, committing
the data) and why not: ADR-0018.

### Rule check in `ContentRules` (ADR-0018)

`CheckText` keeps its shape and order: required, unpaired surrogate (stop), length, control
characters, refused characters, leading or trailing whitespace. Changes:
- `IsControl` becomes fixed ranges (U+0000 to U+001F, U+007F to U+009F, U+2028, U+2029); the
  `Rune.GetUnicodeCategory` call goes.
- A new internal `TextRuleTable.IsAllowed(Rune)`: U+0020 to U+007E true; other ASCII false;
  otherwise binary search.
- A scan collects refused characters that are not control characters. One problem per field:
  `{subject} contains a character that is not allowed: U+2B50 at position 5` and, with more,
  `(and 2 others)`, followed by `Allowed: printable ASCII, and letters, combining marks, digits
  and punctuation from any script.` The character is never echoed.
- The reader needs no change: it already reports *not supported* when `ContentRules` reports
  any problem.

### Generator (ADR-0018)

`tools/unicode/text-rule-table.mjs` with `tools/unicode/text-rule-table.test.mjs`. Pure
functions (parse a UCD property file into ranges; build the allowed set; merge ranges; emit
C#) are exported and tested on small inline fixtures. The command downloads the two files
for the pinned version (or reads given paths), refuses a hash mismatch, and writes the C#
file. A `--compare <file>` option lists code points the new table would remove against an
existing one, for a future version move. CI runs the tests: `ci.yml` and `all-platforms.yml`
change the tool-test step to `node --test "tools/**/*.test.mjs"`.

### Line endings

`.gitattributes` holds one rule, `*.g.cs text eol=lf` (Carl, 2026-11-08): the generator writes LF,
so a Windows checkout with `core.autocrlf` keeps the generated table byte-identical to the
generator's output, and regenerating on Windows leaves `git status` clean. Repository-wide
normalisation was not chosen: it would rewrite every file in one commit for no gain here.
Architect-owned.

### Notice (ADR-0018)

`THIRD-PARTY-NOTICES.md` at the root with the Unicode License v3 text and the data's source,
packed into the library package root next to `README.md`. A new top-level path: owner
Architect, packing by worker-backend.

## Test strategy

- xUnit, test names follow spec scenario names. `ContentRulesTests` gains one test per Text
  rule scenario, with each listed example as a case, including U+3164, U+034F, U+FE0F, U+1F3FB,
  U+20E3, U+00AD, U+202E, U+200B to U+200D, U+00A0, U+3000, U+2002, U+2003, U+E000, an
  unassigned code point (U+0378) and a 17.0 letter (U+10940) refused; `コマース Pro`,
  `Ακρόπολη Pro`, `Łódź Edition`, `Commerce Pro – Édition`, both forms of `Café`, `Commerce  Pro`
  and every printable ASCII character accepted; a 4-byte letter (U+20000) accepted and counted
  as one character. Message tests: code point and position named, character not echoed, count
  of others.
- Cross-check test (`TextRuleTableTests`): for every code point U+0000 to U+10FFFF,
  `IsAllowed` equals "printable ASCII, or non-ASCII in L, Mn, Mc, Nd, P by
  `Rune.GetUnicodeCategory`, and not in the 267 listed code points". It runs when the runtime
  probes as Unicode 16.0 (U+1C89 assigned, U+10940 unassigned) and is skipped with a message
  otherwise. Plus: ranges sorted, non-overlapping, merged, none in ASCII, `UnicodeVersion` is
  `16.0.0`.
- Reader: `PayloadReaderTests` gains the license-validation scenarios ("Text breaking the text
  rule", "Text in any script"); accepted-text cases using emoji move to U+20000 (same UTF-8 and
  UTF-16 lengths, so surrogate-pair and length coverage stay); refused-length cases keep 65
  scalar values.
- Issuer: `KeyLengthSupport.Emoji` becomes a 4-byte letter helper (U+20000), so the
  exact-limit arithmetic in `LicenseIssuerTests` and `ReissueTests` is unchanged; "Key too
  long" uses 50 × 256 × `コ` as the spec states.
- Generator: `node --test "tools/**/*.test.mjs"`.
- Cross-platform: the full suite on Windows, macOS and Linux, run by Carl by hand
  (`all-platforms.yml` is disabled, ADR-0008), for "Same answer on every machine".

## Risks / Trade-offs

- [The pinned data changes at unicode.org, or the site is unreachable] → the generator refuses
  on hash mismatch or fails to download; the committed table is unaffected. Local paths can be
  given instead.
- [Test runtime moves past .NET 10 and the cross-check skips silently] → the skip message
  names the probe; `global.json` pins SDK 10.0.100 with `latestFeature`, so tests stay on .NET
  10 until Carl moves the SDK, which is the moment to move the Unicode version or accept the
  skip.
- [Beta keys with emoji or invisible characters] → *not supported* after upgrade; first action
  reissue (PDR-0024); stated in `docs/vendor-guide.md` with the beta warning.
- [Generated file edited by hand] → header says generated, with the command; the cross-check
  test fails on any drift from the categories.
- [A narrowing by mistake in a future version move] → `--compare` lists removed code points;
  Carl decides (PDR-0042).

## Migration Plan

Ships in the next `0.1.0-beta` release (Carl). No data migration. Beta keys breaking the rule
are reissued by the vendor. Rollback: revert the change; keys issued under the new rule are
valid under the old rule, since the new rule only refuses more.
