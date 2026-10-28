# PDR-0010: Feature types, name rule and lookup

- Status: Amended by PDR-0019
- Date: 2026-10-09
- Source: `add-license-core` design.md R2, R5, Q9 (`CLEAN-PROJECT-PROMPT.md` section 12, decision 5)
- Personas: vendor (flags and limits), site owner (a paid feature is never silently missed).

## Decision

A feature is a name and a typed value. Present means granted; absent means not granted; an
unknown name is ignored. Features never affect validity. "Tier" and "feature" are one concept.

| Part | Rule |
|---|---|
| Name | Lowercase `a-z`, digits, hyphens; starts with a letter. At most once per key. Looked up ignoring case of `a`-`z`, culture-invariant |
| Switch | A plain name. Explicit `false` is rejected |
| Number | PDR-0011 |
| Text | PDR-0012 |

Lookups answer only on a valid key. A lookup of a different type than the feature holds (a
number lookup on text `"500"`) answers not granted (Carl, 2026-10-09, `add-license-core`
design.md Q9). Vendors can stop such keys being issued with feature definitions (PDR-0016).
Name length and feature count are limited by PDR-0017.

## Reasons

- Free-form names matched ignoring case fail on look-alike letters (Latin `a`, Cyrillic `а`) and
  on culture-dependent case rules (Turkish i): a product asking for `licensed-domain` could
  silently miss a key issued with a look-alike name, and a paid feature would read as not
  granted.
- `false` would mean the same as absence; two spellings of "not granted" invite mistakes.
- Wrong-type lookups answer not granted because text is never interpreted (PDR-0012).

## Rejected options

| Option | Why rejected |
|---|---|
| Free-form names | Look-alike and culture case failures (above) |
| Convert text to number on lookup | Interprets text; `"5OO"` would fail differently from `"500"` |
