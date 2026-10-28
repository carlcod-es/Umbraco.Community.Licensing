# PDR-0008: Display name

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R2 (`CLEAN-PROJECT-PROMPT.md` section 12, decision 1)
- Personas: site owner (reads `Commerce Pro`, not feature names), implementor.

## Decision

Optional. 1 to 64 characters; no leading or trailing whitespace, line breaks or control
characters; one plain string in the vendor's language; signed exactly as supplied, never trimmed
(rejected instead). Display only: no part in evaluation or feature lookup. Reported for verified
keys only.

## Reasons

A generic licensing screen can show `Commerce Pro` instead of raw feature names. Rejecting
rather than trimming means what the vendor sees in their tool is what the site owner sees.

## Rejected options

| Option | Why rejected |
|---|---|
| None; derive from features | Feature names are for code, not people |
| Localised names | Size and complexity for a label; one string in the vendor's language is enough |
