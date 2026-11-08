# ADR-0019: Vendor tool typed values, matching and named files

- Status: Amended by ADR-0020
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q37, Q38, Q39, Q40, Q42. Amends ADR-0003, ADR-0013.

## Context

PDR-0043 gives the tool's own typed values a rule. The product display name, customer email and
order reference follow the PDR-0042 text rule, and the display name is checked "by the
library's rule, not a copy". Email and order reference are found ignoring case and either form
of an accented letter, matching the whole value. The passphrase is printable ASCII, 15 to 256
characters, with no leading or trailing space. PDR-0045 says a file the vendor names is never
replaced and its folder never created; the tool asks again. PDR-0046 wants every record complete
and free of local paths.

The text rule lives in the library as `ContentRules` (internal), with `TextRuleTable` generated
from Unicode 16.0.0 (ADR-0018). `ContentRules.CheckDisplayName` is internal. The general
`CheckText(field, subject, value, maxLength, problems)` is private. ADR-0003 limits
`InternalsVisibleTo` to the test project.

## Decision

- **Reaching the text rule** (amends ADR-0003): the library project adds `InternalsVisibleTo`
  for `Umbraco.Community.Licensing.Tool`, and `ContentRules.CheckText` changes from private to
  internal. No public type or member changes (ADR-0004) and no library behaviour changes. A .NET
  tool package bundles the library assembly it was built with, and ADR-0011 releases both at one
  version, so the tool never runs against a different library build.
- **Product display name**: `ContentRules.CheckDisplayName` (field `displayName`, 1 to 64).
- **Customer email**: `CheckText` with field `customerEmail` and maximum 254. Then the tool's own
  checks: at least 3 characters, exactly one `@` with at least one character before and after,
  and no U+0020.
- **Order reference**: `CheckText` with field `orderRef` and maximum 64.
- **Match key** (amends ADR-0013): `value.Normalize(NormalizationForm.FormC).ToLowerInvariant()`.
  The stored value is kept exactly as typed. `Licenses.CustomerEmailKey` and
  `IssueEntries.OrderRefKey` hold the key and are indexed. Finding compares keys for equality,
  so a part of a value never matches and spaces match exactly. Each key is set, corrected and
  cleared together with its value, under `secure_delete` (ADR-0013).
- **Passphrase rule**: every UTF-16 code unit in U+0020 to U+007E, length 15 to 256, first and
  last not U+0020. Checked at setup. Later prompts check only against the stored passphrase check
  (ADR-0014). A non-ASCII key at the hidden prompt is taken as typed and then fails the rule.
- **Named output files** (backup export, key export): the path is resolved with
  `Path.GetFullPath`. The tool refuses and asks again at the prompt when any of these is true:
  the path exists as a file, the path is a folder, or the parent folder does not exist. This
  applies whether the path came from `--out` or from a prompt. The file is opened with
  `FileMode.CreateNew`, so a file created between the check and the write is never replaced.
  Backup export writes its `VACUUM INTO` snapshot to a temp file, then zips it into that stream.
- **Import path**: when `File.Exists` is false (missing, or a folder), the tool says so with the
  full path and asks again.
- **Portable records**: timestamps are stored as UTC. No stored value holds a path, machine name
  or OS-specific form (ADR-0017). Every record is readable with a plain SQLite connection,
  without EF Core.

## Alternatives Considered

| Option | Why not |
|---|---|
| New public library API, such as `TextRule.Check(value, maxLength)` | Adds public surface (ADR-0004) and a library change for a need only the tool has today. Can be added later without breaking the tool |
| Copy the text rule into the tool | PDR-0043 says not a copy; two tables could drift when Unicode is bumped (ADR-0018) |
| Check the display name by issuing a throwaway license | Covers the display name only, not email or order reference; misuses issuing |
| SQLite `COLLATE NOCASE` or `lower()` | ASCII only; does not fold `Ü`, does not join the two forms of an accented letter |
| Compare in memory with culture-aware `CompareOptions.IgnoreCase` | Result depends on the ICU or NLS build of each OS; no index; the stored key is deterministic |
| NFKC instead of NFC | Folds compatibility characters (`ﬁ` to `fi`) the PDR did not ask for |
| Ask "overwrite? y/N" | PDR-0045 rejected it |

## Consequences

- The library project file carries a second `InternalsVisibleTo`. A library refactor that breaks
  the tool fails the same build and test run.
- `ToLowerInvariant` is simple case mapping, not full case folding: `ß` does not match `SS`.
  Accepted; rare in emails and order references.
- Two extra indexed columns. Erasure tests check that the key columns are cleared too.
- A later public text-rule API (if FR work needs one) replaces the internal call with no change
  in behaviour.

## Reversal Cost

Low. The value checks and match key sit in one tool component. Removing `InternalsVisibleTo`
needs a public API or a copy. Dropping the key columns needs a migration and a different find.
