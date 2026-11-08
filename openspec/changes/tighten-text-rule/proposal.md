# Proposal

Status: from `opsx:explore`, completed at `opsx:propose` (Carl, 2026-11-08). Every question is
settled; Q7 moved to `add-vendor-tool`.

## Why

The text rule for a key's free text (display name, text feature values) lists what is refused,
not what is allowed. Anything else is accepted: emoji, pictographs, symbols, every script,
invisible characters (zero-width spaces, text-direction overrides such as U+202E), runs of
spaces and non-breaking spaces. The same accented name can be stored in two forms that look
identical. A site owner can therefore be shown a display name that renders differently from
what the vendor issued, and the vendor tool (`add-vendor-tool`) cannot settle its own typed
fields until the key's rule is fixed. The format is still in beta (PDR-0024), so this is the
time to tighten it.

## What Changes

- **BREAKING (beta):** the display name and text feature values share one text rule that allows
  every printable ASCII character and, outside ASCII, letters, combining marks, decimal digits
  and punctuation from any script. Emoji, other symbols and invisible characters are refused
  (Carl, 2026-11-08). Every ASCII string valid today stays valid.
- The rule holds at issue (the request is rejected naming the field) and at read (a verified key
  breaking it is *not supported*), so issue and read never disagree.
- Existing beta keys that break the new rule read as *not supported* on sites with the new
  version and need reissuing; PDR-0024 allows this for betas.
- Unchanged: the vendor tag, product ID, feature names, prefix and reference rules, already
  ASCII-only.
- Documentation: the root README is slimmed to the basics (what the library does, the beta
  warning, the operating-system line, links). Its detail moves unchanged to `docs/how-it-works.md`,
  `docs/vendor-guide.md` (which also states the field rules) and `docs/implementor-guide.md`
  (Carl, 2026-11-08).

## Principle

Least impact on the license specification while refusing characters that cause problems.
Widening (allowing more) breaks no issued key; narrowing makes keys site owners hold *not
supported*. So: refuse when unsure, widen later, never narrow after 1.0 (Carl, 2026-11-08).

## Questions

Settled questions Q1 to Q8, the rule and its consequences: design.md "Questions settled in
this change". Q8 (characters ignorable by default) was settled at `opsx:propose` (Carl,
2026-11-08).

## Personas

- Site owner (primary): the display name shown matches what was issued; no hidden or misleading
  characters.
- Implementor: keys stay pasteable and deployable; a key breaking the rule reports *not
  supported*, first action reissue.
- Vendor: one rule to type against, in the library and the tool; beta keys with emoji or
  invisible characters need reissuing.
- Backoffice editor, site visitor: unaffected beyond seeing clean display names.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `license-generation`: Display name rule, Feature rules (text value), and the oversized-key
  scenario that uses emoji.
- `license-validation`: Strict reading of verified contents gains scenarios for text breaking
  the rule and for text in any script.

## Impact

- Records: PDR-0042 (amends PDR-0008 and PDR-0012; PDR-0017 lengths unchanged);
  `docs/key-format.md` text rule; `docs/license-examples.md` and its "Last checked against"
  line; `docs/decisions/README.md`. PDR-0042 cites design.md Q1 to Q6 and Q8; ADR-0018 cites Q9 to Q11.
- Library: issue and read rules change together; a release under the beta line. The rule's
  character table is generated from Unicode data, which adds a third-party notice to the
  package (ADR-0018).
- `add-vendor-tool` waits for this change; its typed text fields use this rule.
