# ADR-0018: Text rule character table

- Status: Decided
- Date: 2026-11-08
- Source: `tighten-text-rule` design.md Q9 to Q11. Amends ADR-0002 (meaning of "control
  characters" and the characters a text value may hold).

## Context

PDR-0042 allows characters by kind (letters, combining marks, decimal digits, punctuation)
"as one fixed version of Unicode defines them", less those Unicode marks ignorable by default,
and requires the same answer on every machine for the life of a key. The library judges
characters at issue and at read (ADR-0002) and targets `net10.0`, but runs on whatever runtime
the site uses. .NET carries its own Unicode data, independent of the operating system: .NET 10
answers as Unicode 16.0 (checked 2026-11-08: U+1C89 and U+1E5D0 assigned, U+10940 and U+11DB0
from 17.0 unassigned). A later runtime carries a later version, so a site on .NET 11 would
answer `Rune.GetUnicodeCategory` differently from the vendor's .NET 10 for newly assigned
characters. .NET does not expose the Default_Ignorable_Code_Point property at all. Constraints:
core library BCL only; scripts and tooling Node.js.

## Decision

**Fixed version: Unicode 16.0.0**, the version .NET 10 ships, so the table can be cross-checked
against the runtime the tests run on.

**Table in the library.** A generated C# file,
`src/Umbraco.Community.Licensing/Format/TextRuleTable.g.cs`, holds the allowed code points
outside ASCII as sorted, merged, inclusive ranges in a `static ReadOnlySpan<int>` (start, end
pairs; compiled into the assembly's data, no allocation), with the constant
`UnicodeVersion = "16.0.0"` and the SHA-256 of each source file in its header. The set is
general category L (Lu Ll Lt Lm Lo), Mn, Mc, Nd or P (Pc Pd Ps Pe Pi Pf Po), less every
Default_Ignorable_Code_Point. At 16.0.0 that is 791 ranges, 144,779 code points; 267 code
points are removed as ignorable by default (U+034F, U+115F, U+1160, U+17B4, U+17B5, U+180B to
U+180D, U+180F, U+3164, U+FE00 to U+FE0F, U+FFA0, U+E0100 to U+E01EF).

**Lookup.** U+0020 to U+007E allowed without a lookup; any other ASCII refused; otherwise a
binary search of the ranges (at most 10 steps). Control characters are the fixed ranges
U+0000 to U+001F and U+007F to U+009F, plus U+2028 and U+2029, with no runtime category call,
so no part of the rule reads the runtime's Unicode data. Length stays counted in scalar
values; leading and trailing whitespace stays `char.IsWhiteSpace` (ADR-0002); those characters
are stable across Unicode versions.

**Generator.** `tools/unicode/text-rule-table.mjs`, Node 22, ES modules, no npm packages.
Inputs: `extracted/DerivedGeneralCategory.txt` and `DerivedCoreProperties.txt` from
`https://www.unicode.org/Public/16.0.0/ucd/`, downloaded with `fetch` or read from paths given
on the command line, each checked against a pinned SHA-256
(`7676ab75...f293` and `39d35161...dabd`, full values in the script) before use. Output is
deterministic; the generated file is committed and the data files are not. The library build
does not run the generator; it is run only when the Unicode version moves.

**Notice.** Unicode data is under the Unicode License v3, which asks that its notice go with
copies and derived data. `THIRD-PARTY-NOTICES.md` at the repository root holds that notice and
is packed into the library package root.

**Messages.** One problem per field for refused characters, naming the first refused
character as `U+XXXX` with its 1-based position in scalar values, and how many others there
are. The character itself is never echoed: it may be invisible or reverse the message's
direction. Control characters keep their own message.

## Alternatives Considered

| Option | Why not |
|---|---|
| The runtime's `Rune.GetUnicodeCategory` | Answers change with the runtime version; a site on a later .NET would accept or refuse differently from the vendor. No Default_Ignorable_Code_Point property. |
| Runtime categories plus a hard-coded list of later additions to refuse | Must be updated for every runtime release, and fails on a runtime newer than the list |
| Unicode 17.0.0 (latest) | Cannot be cross-checked against .NET 10; adds only rare scripts. Moving to it later is a widening. |
| Generate from .NET 10's own data with a C# tool | No Default_Ignorable_Code_Point; makes the runtime, not the standard, the source. Tooling is Node.js. |
| Two-stage bitmap lookup | Faster per character, but larger tables and more generator code; text is at most 256 characters and checked once per issue or read, so binary search over about 6 KB of ranges is fast enough |
| Commit the Unicode data files | About 1.4 MB in the repository for a table regenerated perhaps once a year; the pinned hash makes the download reproducible |
| Fetch the data in the build | The build would depend on the network and on unicode.org |
| Generated table with no notice | Small licensing risk; a notice file costs one file (Carl, 2026-11-08) |

## Consequences

- Issue and read give the same answer on every runtime and operating system; nothing in the
  rule depends on the host's Unicode data.
- A test compares every code point against .NET 10's categories: the table must equal the
  allowed categories less exactly the 267 listed code points. It runs only when the test
  runtime answers as Unicode 16.0 (the probe above) and is skipped with a message otherwise,
  so a test run on a later runtime does not fail falsely.
- Moving to a later Unicode version: change the version and hashes in the generator,
  regenerate, update the cross-check probe and the list, state it in `docs/key-format.md`.
  Allowed sets have only grown between versions for these categories, so this is a widening
  (PDR-0042). The generator reports any code point the move would remove, which would be a
  narrowing for Carl to decide.
- The package gains `THIRD-PARTY-NOTICES.md`; the vendor tool package (`add-vendor-tool`)
  carries the library and needs the same notice.
- Adds about 6 KB to the assembly.

## Reversal Cost

Low. The table sits behind one internal function in `ContentRules`; replacing the source or
the lookup changes no public API and no key format. Changing the fixed version is a format
change governed by PDR-0042.
