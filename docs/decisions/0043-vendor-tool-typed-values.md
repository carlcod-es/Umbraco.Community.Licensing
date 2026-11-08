# PDR-0043: Vendor tool typed values

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q37, Q38, Q39, Q40. Amends PDR-0032, PDR-0033, PDR-0036.
  Builds on PDR-0042.
- Personas: vendor (one character rule for everything shown or found; finds a customer however
  the value was typed; a passphrase that types the same on every machine). Site owner (primary):
  a passphrase that cannot be typed on the vendor's next machine is a lost signing key, which
  means new keys for site owners; ASCII removes that risk. Their email is stored as given and
  still cleared completely on erasure. Implementor, backoffice editor, site visitor: unaffected;
  none of these values is in a key.

## Context

Every value the vendor types into the tool is typed by a person. Names and email addresses must
allow common characters such as umlauts and accents; emoji are not wanted. The key's own text
rule (PDR-0042) settles which characters a display name and text values may hold. The tool's own
fields, which never go into a key, needed a rule: the product display name, the customer email,
the order reference and the passphrase. Accented letters can be typed in two forms (one
character, or a letter and a combining mark) that look the same on screen.

## Decision

**One character rule for shown and found values.** The product display name, the customer email
and the order reference follow the PDR-0042 text rule: all printable ASCII, and letters,
combining marks, decimal digits and punctuation from any script; no emoji, symbols outside
ASCII, invisible characters, other spaces, control characters, or leading or trailing
whitespace. Each is stored exactly as typed.

| Value | Rule | Length | Finding |
|---|---|---|---|
| Product display name (Q40) | PDR-0042, checked by the library's rule, not a copy | 1 to 64 | not searched |
| Customer email (Q37) | PDR-0042, plus: exactly one `@` with text before and after it, no spaces | 3 to 254 | whole address; ignores case; both forms of an accented letter match |
| Order reference (Q38) | PDR-0042 | 1 to 64 | whole reference; ignores case; both forms of an accented letter match; spaces exact |

- The product display name is checked at `product add` and `product rename`. A refused rename
  keeps the earlier name.
- The email check is light: it does not apply the full address syntax of the email standards.
- Finding never matches part of a value: `1042` does not find `ORD-1042`.
- Spaces in an order reference match exactly: `ORD  1042` (two spaces) does not find `ORD 1042`.

**Passphrase (Q39).** Printable ASCII only (letters A to Z, digits, space and the symbols on a
standard keyboard), 15 to 256 characters. Spaces inside are allowed; a leading or trailing space
is refused at setup. No composition rules (PDR-0032). A refusal at setup says which rule was
broken and asks again.

**Principle:** refuse when unsure; widen later. None of these values is in a key, so a later
widening affects no site owner; narrowing would refuse values already stored.

## Reasons

- One rule for every shown value: the vendor learns it once, and invisible or look-alike
  characters cannot make two products or customers look the same in a list.
- A later default of a license's display name from the product display name would work as is:
  every stored product name already meets the key's rule.
- Accented names and addresses are real and the shop has already accepted them; refusing them
  would leave the vendor unable to find the customer.
- A light email check: the shop has already accepted the address and the tool sends no mail; a
  strict check would mostly refuse addresses copied correctly.
- Ignoring case and matching both accent forms when finding: the vendor searches for what they
  can see; a different keyboard or a paste from a mail client must not return "no licenses
  found".
- Whole-value finding: a partial match turns a lookup into a fuzzy search and can show another
  customer's license.
- The PDR-0042 characters for order references cover every common shop and payment format
  (`#1042`, `INV-2026-0042`, `pi_3Nq...`, `ORD 1042`, `UMB:1042`) and any script; characters a
  shell treats specially are quoted on the command line or typed at the prompt.
- ASCII passphrase: it types the same on every keyboard layout and operating system (PDR-0040),
  the hidden prompt gives no clue when an accented letter came out differently, and another tool
  opening the stored keys (PDR-0032, no lock-in) receives exactly what was typed. Length is the
  defence, not variety, so ASCII costs no strength.
- Edge spaces in a passphrase are invisible at a hidden prompt and some paste and terminal paths
  trim them, which would leave a passphrase that can never be matched again.
- A 256-character maximum is above the "allow at least 64" of NIST SP 800-63B and fits values
  from password managers.

## Rejected options

| Option | Why rejected |
|---|---|
| Looser tool-only rule for the product display name (allow `™ ®`) | Two rules for the vendor to tell apart; names that would fail a later default to the license display name |
| No rule beyond length for the product display name | Invisible characters let two products look the same |
| ASCII-only email | Refuses real addresses the shop accepted; the customer cannot be found |
| Full email address syntax | Refuses odd but real addresses copied correctly; the tool sends no mail |
| Order reference limited to the vendor tag's characters | Refuses `ORD 1042`, `UMB:1042` and non-Latin references from the start (Carl: in from the start) |
| A short explicit list for order references | A second rule to learn; refuses `PO(1042)`, `A&B-1042` and other scripts |
| Runs of spaces treated as one when finding an order reference | One more rule to explain; a pasted reference already matches exactly |
| Partial matching when finding | Can show the wrong customer's license |
| Accented letters in the passphrase | May not type on another machine or layout; two forms of one letter; another tool may not open the keys |
| Edge spaces allowed in the passphrase | Can be lost invisibly, leaving a passphrase that never matches |

## Consequences

- A vendor wanting `™` in a product name, or umlauts in their passphrase, chooses differently.
  `™ ®` are already candidates for a later widening of PDR-0042.
- The tool guide lists each rule, shows quoting for order references with shell characters, and
  says the passphrase uses only characters on a standard keyboard.
