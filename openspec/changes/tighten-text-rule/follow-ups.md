# Follow-ups: tighten-text-rule

Minors and nits from reviews. Read at the start of each apply session.

## Section 1 (supervisor, 2026-11-08)

- nit, `THIRD-PARTY-NOTICES.md:9`: "(ADR-0018)" cites a repository document a NuGet consumer
  does not have. Drop it or use a repository URL. Owner: Architect.
- nit, `THIRD-PARTY-NOTICES.md:12`: add SPDX `Unicode-3.0` beside "Unicode License v3" for
  licence scanners. Owner: Architect.
- nit, `THIRD-PARTY-NOTICES.md:3`: generalise "The Umbraco.Community.Licensing package" when
  the vendor tool package carries the file (task 4.5). Owner: Architect.
- note: the local machine runs Node 20.20.2; `node --test "tools/**/*.test.mjs"` needs Node
  21+. CI pins Node 22, so the CI step is unaffected. Local validation in briefs uses
  shell-expanded paths (`node --test tools/release/*.test.mjs tools/unicode/*.test.mjs`). The
  tasks.md header still names the glob form: align through `opsx:update` if Carl wants.
- note: `release.yml` runs only `tools/release/*.test.mjs`, so the unicode tests run in CI, not
  in the release run. Intended: the release run needs only release tooling.
- note: when the Unicode version moves, fetch the licence again and compare (copyright year).

## Block 2 (reviewer, 2026-11-08)

- nit, `tools/unicode/text-rule-table.mjs` `run`: `--compare` with the default `--out`
  overwrites the source table after reading it. Add a usage note: pass `--out` to compare
  without overwriting. Owner: worker-backend.
- nit, `tools/unicode/text-rule-table.mjs` `parseArgs`: only `--flag value` pairs; a stray
  positional gives "Unknown argument". Acceptable.
- nit, `tools/unicode/text-rule-table.test.mjs`: no test for an unparseable `--compare` file.
  Owner: worker-backend.

## Section 2 (supervisor, 2026-11-08)

- nit, `tools/unicode/text-rule-table.mjs:169`: a network failure throws a bare
  `TypeError: fetch failed`; wrap and rethrow naming the file. Owner: worker-backend.
- nit, `tools/unicode/text-rule-table.test.mjs:179,188`: the `--compare` test does not assert the
  exit codes. Owner: worker-backend.
- nit, `tools/unicode/text-rule-table.mjs:197-202`: `--compare` exits 0 when it finds removed
  code points; consider non-zero or a "narrowing: decision needed (PDR-0042)" line. Owner:
  worker-backend, optional.
- nit, `tools/unicode/text-rule-table.mjs:133-141`: `parseGenerated` reads to end of file and
  assumes sorted, merged ranges; stop at `};` and merge. Owner: worker-backend.
- note: no `.gitattributes`; Windows `core.autocrlf` checkouts hold `.g.cs` with CRLF. Resolved by
  task 1.3 (`.gitattributes`, Architect-owned).

## Block 3a (reviewer, 2026-11-08)

- nit, `tests/Umbraco.Community.Licensing.Tests/Format/TextRuleTableTests.cs:107-109`: the
  hard-coded ignorable list has no assertion that it totals 267. Owner: worker-backend, optional.

## Block 3b (reviewer, 2026-11-08)

- minor, `tests/Umbraco.Community.Licensing.Tests/Format/PayloadReaderTests.cs:662-669`:
  `TryRead_TextBreakingTheTextRule_IsRefused` asserts only refusal, not that the text rule
  caused it; assert on the reported problem. Owner: worker-backend.
- nit, `src/Umbraco.Community.Licensing/Format/ContentRules.cs` (scan): add a comment that a
  control character suppresses the refused message for that character. Owner: worker-backend.
- nit, `ContentRules.cs`: `TextScan` record struct carrying a `Rune`; no allocation, fine.
- note: "Same answer on every machine" across operating systems is task 5.1 (Carl, by hand).

## Section 3 (supervisor, 2026-11-08)

- nit, `tests/Umbraco.Community.Licensing.Tests/Format/ContentRulesTests.cs`
  (`TextRule_EmojiBuildingBlocksRefused`, case `"\U0001F44D\U0001F3FB"`): the refusal reported is
  U+1F44D, not the skin-tone modifier; use `"Pro\U0001F3FB"` or assert `U+1F3FB`. Owner:
  worker-backend.
- nit, `src/Umbraco.Community.Licensing/Format/TextRuleTable.cs:6,12`: "a text feature value"
  should read "a display name or text feature value". Owner: worker-backend.
- nit, `tests/Umbraco.Community.Licensing.Tests/Format/TextRuleTableTests.cs`: snake_case method
  names; rename to the suite's PascalCase style. Owner: worker-backend.
- note: leading/trailing whitespace still uses `char.IsWhiteSpace`; on a later runtime only the
  list of problems could differ, never the verdict. Revisit when the Unicode version moves.

## Section 4 (supervisor, 2026-11-08)

Fixed by the Architect in the same section (docs it owns):
- minor, `docs/key-format.md`: "categories have only grown" overclaimed; now "so far" and a
  removal is a narrowing decided separately (PDR-0042), as ADR-0018 says. Fixed.
- minor, `docs/implementor-guide.md`: the beta-key bullet now refines the how-it-works
  `NotSupported` action (product already updated, go to the second action) instead of a
  separate first action. Fixed.
- minor, `docs/implementor-guide.md`, `docs/vendor-guide.md`: lists of refused characters now
  include unusual spaces and point to the full table. Fixed.
- nit, `docs/vendor-guide.md`: "refused only" sentence now names line breaks. Fixed.
- nit, `docs/vendor-guide.md`: rewrapped. Fixed.

Open:
- note: when Carl releases (task 5.2), the RELEASES.md "What changed" text could name the text
  rule (PDR-0042) so beta.6 vendors are warned.

## Tasks 1.3 and 4.5 (supervisor, 2026-11-08)

- One major (Q40 note mixed product and license display names) and two minors and two nits:
  fixed and re-reviewed, approved.
- note: `openspec validate add-vendor-tool --strict` fails on long-requirement warnings in its
  specs (pre-existing). Fix when Q37 to Q43 are settled, before its apply phase.
