# PDR-0012: Text values are opaque; no text set

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R2 (`CLEAN-PROJECT-PROMPT.md` section 12, decision 2)
- Personas: vendor (carry a list such as domains), implementor.

## Decision

A text value is 1 to 256 characters; no leading or trailing whitespace, line breaks or control
characters. It is never interpreted: `"500"` is text, and a comma means nothing to the library.
A list, such as licensed domains, is one text value with a vendor convention, for example
`licensed-domains: "example.com,shop.example.com"`. The product splits and matches it. Adding or
removing a value is a reissue. The vendor normalises values at issue.

## Reasons

The text set's only gain over text was a union of values across several licenses for one
product. With one key per product there is no union, and text with a vendor convention carries
a list on one key with no extra type, rules or encoding.

## Rejected options

| Option | Why rejected |
|---|---|
| Text set type | No union to compute; extra type, rules and encoding |
| Library splits on commas | A convention, not a rule; vendors may need another separator |
