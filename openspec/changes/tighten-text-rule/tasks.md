Owners per CLAUDE.md: section 1 Architect; sections 2 and 3 worker-backend; section 4 Architect;
section 5 Carl. Validation for every worker block: `node --test "tools/**/*.test.mjs"`,
`dotnet build Umbraco.Community.Licensing.slnx -c Release`,
`dotnet test --solution Umbraco.Community.Licensing.slnx -c Release --no-build`. Bar:
production (design.md). Test names follow spec scenario names. Specs:
`specs/license-generation/spec.md`, `specs/license-validation/spec.md`. Records: PDR-0042,
PDR-0017, PDR-0018, PDR-0024, PDR-0025, ADR-0018, ADR-0002, ADR-0007, ADR-0009.

## 1. Ownership and notice

Depends on: none. Owner: Architect.

- [x] 1.1 Add `THIRD-PARTY-NOTICES.md` to the CLAUDE.md ownership table (Architect; packed by worker-backend), and verify the table names an owner for every top-level path this change touches (`tools/unicode/` falls under `tools/**`)
- [x] 1.2 Write `THIRD-PARTY-NOTICES.md`: the Unicode License v3 text, the data used (Unicode Character Database 16.0.0, `DerivedGeneralCategory.txt`, `DerivedCoreProperties.txt`) and where it is used (`Format/TextRuleTable.g.cs`), and verify the licence text against unicode.org/license.txt
- [x] 1.3 Add `.gitattributes` holding one rule, `*.g.cs text eol=lf`, with its ownership row in CLAUDE.md (Architect), added 2026-11-08 by `opsx:update` (Carl), and verify `git check-attr eol -- src/Umbraco.Community.Licensing/Format/TextRuleTable.g.cs` reports `lf` and `git ls-files --eol` shows the file as LF in the index and working tree

## 2. Table generator

Depends on: none. Owner: worker-backend. Paths: `tools/unicode/text-rule-table.mjs`,
`tools/unicode/text-rule-table.test.mjs`; shared files assigned in the brief:
`.github/workflows/ci.yml`, `.github/workflows/all-platforms.yml` (tool-test step only). Node
22+, ES modules, no npm packages. Record: ADR-0018.

- [x] 2.1 Implement and export the pure functions: parse a UCD property file (single code points and `..` ranges, comments and blank lines ignored) into ranges for given values; build the allowed set outside ASCII (L, Mn, Mc, Nd, P less Default_Ignorable_Code_Point); merge into sorted inclusive ranges; emit the C# file (generated-file header with the command, `UnicodeVersion`, both SHA-256 values, `static ReadOnlySpan<int>` pairs), and verify by `node:test` tests on inline fixtures: a range line, a single line, an ignorable letter removed, adjacent ranges merged, ASCII excluded, byte-identical output for the same input
- [x] 2.2 Implement the command: download both files for 16.0.0 with `fetch`, or read paths given as arguments; refuse a SHA-256 mismatch naming the file; write `src/Umbraco.Community.Licensing/Format/TextRuleTable.g.cs`; `--compare <file>` lists code points the new table would remove from an existing one, and verify by tests with an injected fetch (hash match writes the file, mismatch refuses, `--compare` lists a removed range) and one real run printing 791 ranges and 267 ignorable code points removed
- [x] 2.3 Change the tool-test step in `ci.yml` and `all-platforms.yml` to `node --test "tools/**/*.test.mjs"`, and verify the command runs the release and unicode tests locally and both files parse as YAML

## 3. Library rule

Depends on: 2 (generator), 1 (notice file, for 3.5). Owner: worker-backend. Paths:
`src/Umbraco.Community.Licensing/Format/ContentRules.cs`,
`src/Umbraco.Community.Licensing/Format/TextRuleTable.g.cs`,
`src/Umbraco.Community.Licensing/Format/TextRuleTable.cs`, `tests/**`; shared file assigned in
the brief: `src/Umbraco.Community.Licensing/Umbraco.Community.Licensing.csproj` (pack item
only). No public API change. Records: ADR-0018, PDR-0042.

- [x] 3.1 Generate and commit `TextRuleTable.g.cs` with the 2.2 command; add `TextRuleTable.IsAllowed(Rune)` (U+0020 to U+007E allowed, other ASCII refused, else binary search); add `TextRuleTableTests`: ranges sorted, non-overlapping, merged, none in ASCII, `UnicodeVersion` is `16.0.0`, and the every-code-point cross-check against `Rune.GetUnicodeCategory` less the 267 listed code points, run when the runtime probes as Unicode 16.0 (U+1C89 assigned, U+10940 unassigned) and skipped with `Assert.Skip` naming the probe otherwise, and verify the tests pass on .NET 10 and the cross-check is not skipped
- [x] 3.2 Change `ContentRules.CheckText`: `IsControl` by fixed ranges (U+0000 to U+001F, U+007F to U+009F, U+2028, U+2029) with no `Rune.GetUnicodeCategory`; one problem per field for other refused characters naming the first as `U+XXXX` with its 1-based scalar position, the count of others, and the allowed kinds, never echoing the character (wording in design.md "Rule check"); update the type's doc comment, and verify by `ContentRulesTests` for every Text rule scenario in `specs/license-generation/spec.md` by name with each listed example, the Display name rule and Feature rules scenarios, the cases listed in design.md "Test strategy", and message tests (code point, position, not echoed, count)
- [x] 3.3 Replace `KeyLengthSupport.Emoji` with a 4-byte letter helper (U+20000) in `LicenseIssuerTests` and `ReissueTests`, and make the "Key too long" test use 50 text features of 256 × `コ` as the spec states, and verify the exact-limit test still reaches 32,767 and every issuer and reissue test passes
- [x] 3.4 In `PayloadReaderTests`, move accepted emoji text to U+20000 (keeping surrogate-pair and length coverage) and add the license-validation scenarios "Text breaking the text rule" (display name `Pro ⭐`, a text feature with U+200B; *not supported* with claimed product and identifier) and "Text in any script" (`コマース Pro` reported unchanged), and verify every reader and evaluator test passes and the example key tests (`ExampleTests`, `ExampleDocumentTests`) pass unchanged
- [x] 3.5 Pack `THIRD-PARTY-NOTICES.md` into the library package root next to `README.md`, and verify `dotnet pack -c Release` produces a `.nupkg` listing `THIRD-PARTY-NOTICES.md` at its root

## 4. Documentation

Depends on: 3. Owner: Architect.

- [x] 4.1 In `docs/key-format.md`, name Unicode 16.0.0 as the rule's version, state that characters ignorable by default are refused (already in the table) and how the version moves (ADR-0018), and remove the "Pending" note, and verify the table against `TextRuleTable` by the cases in `ContentRulesTests`
- [x] 4.2 In `docs/vendor-guide.md`, replace the "ships with change" sentence with the release that has the rule, add that beta keys holding a refused character read as *not supported* and need reissuing (PDR-0024), and that allowed is not displayed (fonts without a script show boxes), and verify each statement against PDR-0042
- [x] 4.3 In `docs/implementor-guide.md`, add the *not supported* case for a beta key holding a refused character (first action: ask the vendor to reissue), and verify against the license-validation spec
- [x] 4.4 Update the `docs/license-examples.md` "Last checked against" line for ADR-0017 and ADR-0018 (no schema or example change from them), and verify `ExampleDocumentTests` still passes
- [x] 4.5 Add a follow-up to `openspec/changes/add-vendor-tool` through `opsx:update` (brought forward from after archive, Carl, 2026-11-08): the tool package carries `THIRD-PARTY-NOTICES.md`, and its typed text fields use this rule (Q40), and verify the note is recorded in that change's design.md

## 5. Verification

Depends on: 3, 4. Owner: Carl.

- [ ] 5.1 Run the full test suite on Windows, macOS and Linux, and verify it passes on all three with the cross-check not skipped ("Same answer on every machine")
- [ ] 5.2 Release the next `0.1.0-beta` with the release command, and verify a key with display name `コマース Pro` issues and reads as *valid*, and one with `Pro ⭐` is refused naming `displayName`

## Workflow follow-up

- Archive the change after supervisor approval and Carl's commit, then sync specs.
- Then settle `add-vendor-tool` Q37 to Q43 through `opsx:update`; task 4.5's note is already
  recorded there.
