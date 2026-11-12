# PDR-0066: Vendor tool screen presentation

- Status: Decided
- Date: 2026-11-12
- Source: `add-vendor-tool-rich-console` design.md Q2, Q6, Q7, Q9, Q10. Amends PDR-0048.
- Personas: vendor (records read as tables, results stand out, slow steps show they are working).
  Site owner (primary): a key copied from the screen is the key alone, as before. Implementor:
  trusted-key lines copy exactly into a release. Backoffice editor, site visitor: unaffected.

## Context

PDR-0048 kept the tool's output plain and left colour and progress to FR-13. Carl asked for the
`psw` look: banner, tables, panels, colour and spinners (2026-11-12).

## Decision

- **Banner** naming the tool and its version when the session opens.
- **Tables** for lists of products, licenses, issue entries and signing keys, one column per
  field the specs name.
- **Panels** for the summary before signing and setup's closing message. Help stays plain text,
  as it may be redirected.
- **Colour** for questions, proposed values, success, warnings and refusals. Every message still
  says in words what happened; colour is never the only signal.
- **Working indicator** while the passphrase is checked, a key is created, or a backup is written
  or restored.
- **Values as typed.** Anything a vendor or customer typed is shown exactly, never read as
  styling.
- **Copyable output stays plain.** A key string and each line printed by Trusted keys are printed
  on their own line, uncoloured, outside any frame or table.
- **Typed answers** keep the terminal's own line editing and paste.
- **Colour off** when the terminal cannot show it, or the vendor has set the common terminal
  setting asking programs for no colour (`NO_COLOR`). Everything else works the same.

## Reasons

- Tables and colour make records and outcomes faster to read.
- A border or colour code copied with a key would break the key in the site owner's hands, or a
  trusted line in the implementor's release.
- Line editing at typed answers is what vendors use to fix a typo, and what the Windows terminal
  checks verify.
- Honouring the no-colour setting serves vendors who need plain output.

## Rejected options

| Option | Why rejected |
|---|---|
| Key string inside the summary panel | Border characters copied with it |
| Colour as the only sign of a refusal | Lost without colour |
| A text prompt that reads single keys | Loses the terminal's line editing and paste |
| A setting in the tool to turn colour off | The common terminal setting already exists |

## Consequences

- The guide shows the new screens and says how colour is turned off.
- Copying a key from the screen is checked by hand in every supported terminal.
