# PDR-0042: Text rule allows characters by kind

- Status: Decided
- Date: 2026-11-08
- Source: `tighten-text-rule` design.md Q1 to Q6 and Q8
- Personas: site owner (the display name shown is the one issued, in any language; no hidden or
  misleading characters), implementor (issue and read agree everywhere; a key breaking the rule
  reports *not supported*), vendor (one rule to type against). Could harm the vendor and site
  owner holding beta keys with emoji or invisible characters: those keys need reissuing, which
  PDR-0024 allows for betas.

## Context

The display name (PDR-0008) and text feature values (PDR-0012) refused only control characters,
line breaks and leading or trailing whitespace. Everything else was accepted: emoji, symbols,
invisible characters such as zero-width spaces and text-direction overrides, and unusual
spaces. A site owner could be shown a display name that renders differently from what was
issued, and the vendor tool could not settle its own typed fields against a rule that loose.

## Decision

One text rule for the display name and text feature values. Amends PDR-0008 and PDR-0012.

Allowed:
- Every printable ASCII character, space included. Every ASCII value valid before stays valid.
- Outside ASCII, from any script: letters, combining marks, decimal digits and punctuation
  (`Müller`, `Ακρόπολη`, `コマース`, `Łódź`, `–`, `«»`, `¿`).

Refused:
- Emoji and other symbols outside ASCII (`⭐ ❤ ™ © ® € ± →`).
- Emoji building blocks: the keycap mark, variation selectors, skin-tone modifiers.
- Invisible formatting: zero-width space and joiners, text-direction overrides, soft hyphen.
- Any other character Unicode marks as ignorable by default, even when it is classed as a letter
  or a mark: the Hangul fillers, the combining grapheme joiner, the Mongolian variation
  selectors and the Khmer inherent vowels (Q8, Carl, 2026-11-08).
- Space characters other than the ASCII space (no-break, ideographic, en and em spaces).
- Numbers that are not decimal digits (`① ² ½ Ⅻ`).
- Control characters, U+2028 and U+2029, leading or trailing whitespace (as before).
- Characters not assigned in the rule's fixed version of Unicode, private-use characters, and
  unpaired surrogates.

Unchanged: lengths (1 to 64, 1 to 256, counted in characters, PDR-0017); repeated ASCII spaces
inside the text are allowed; text is signed exactly as supplied, never cleaned, so an accented
letter typed as one character or as a letter and a mark is accepted either way, as typed. The
rule holds at issue (rejected naming the field) and at read (*not supported*, PDR-0018).

What counts as a letter, mark, digit or punctuation is fixed to one version of Unicode, so the
answer is the same on every machine for the life of a key. Moving to a later version is a
planned format change.

**Principle:** refuse when unsure; widen later. Allowing more breaks no issued key; allowing
less turns keys site owners hold into *not supported*. After 1.0 the rule only widens.

Candidates to allow later, each a widening: the zero-width joiners (Persian and some Indic
spelling), the Mongolian variation selectors (some Mongolian spelling), the ideographic space (typed by Japanese keyboards), `™ ®`, and currency outside
ASCII (`€ £`).

## Reasons

- A site owner reads the display name; refusing invisible and direction-changing characters
  means what is shown is what was issued.
- Any script, because the library assumes nothing about where a purchase happened: a product
  sold in Greece or Japan names its edition in its own script.
- Ignorable-by-default characters show nothing; a Hangul filler is a known way to make a name
  look blank. Unicode lists them, so the rule needs no list of its own.
- No emoji (Carl): they bring sequences, modifiers and presentation variants that complicate
  display, length and comparison, and add nothing to a product name.
- Keeping every printable ASCII character changes nothing valid in ASCII today: technical text
  values such as `example.com,shop.example.com` or `eu+uk` stay valid.
- A fixed Unicode version keeps issue and read in agreement; following each system's own
  version would let a site refuse a key the vendor issued.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep the refused-character list | Emoji, invisible characters and direction overrides stay in |
| Latin with accents only | Shuts out products and site owners outside Europe; narrowing later would not be needed, but widening to other scripts would be a format change anyway |
| Any script, following the system's Unicode version | Issue and read can disagree; keys read as *not supported* by accident |
| No symbols at all, ASCII included | Refuses text values valid today (`eu+uk`, `a=b`); more impact for no gain |
| Separate rules for display name and text values | Two rules to learn; text values need nothing the shared rule refuses |
| One form only for accented letters | Refuses text a vendor typed correctly on another system; the display name is never compared |

## Consequences

- Beta keys holding a refused character read as *not supported* on sites with the new version;
  first action reissue. The vendor documentation states it with the beta warning (PDR-0024).
- Allowed is not displayed: a font or terminal without a script shows boxes. The vendor
  documentation and the vendor tool guide state it.
- Non-Latin text lengthens keys; the oversized-key refusal (PDR-0025) covers it.
- Lengths count characters as stored: an accent typed as a separate mark counts as one more.
- The symbol boundary is ASCII, not a kind: `$` and `+` are allowed, `€` and `±` are not.
- `docs/key-format.md` states the rule, so a reader in another language can reproduce it.
