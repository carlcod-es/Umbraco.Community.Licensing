# PDR-0063: Vendor tool lists are chosen with the arrow keys

- Status: Decided
- Date: 2026-11-12
- Source: `add-vendor-tool-rich-console` design.md Q2 to Q5 (`docs/feature-requests.md` FR-13,
  taken into this change). Amends PDR-0048.
- Personas: vendor (moves through menus and lists as in other .NET terminal tools such as `psw`,
  without reading numbers). Site owner, implementor, backoffice editor, site visitor: unaffected;
  nothing stored or signed changes.

## Context

PDR-0048 made every menu and list numbered, answered by typing a number or an ID, with `0` for
Back or Quit, and left selection lists to FR-13. Carl asked to bring the tool in line with
Package Script Writer's `psw` tool (2026-11-12).

## Decision

- **Lists.** Every menu and every choice from a known list (products, features, signing keys,
  signing-key mode, folder offer, startup offer, Settings > New installation) is a list: arrow
  keys move the highlight, Enter chooses. Typed numbers and IDs at a list are not accepted.
- **Start.** The highlight starts on the proposed or current entry, otherwise the first.
- **Long lists** page and say that more entries follow.
- **Back and Quit** are entries: the last entry of each submenu is Back; of the main menu, Quit.
- **Esc** cancels the action at a list, storing nothing, as `/cancel` does at a typed question. In
  a submenu Esc goes back. At the main menu and the startup offer it does nothing, so quitting
  is always chosen on purpose.
- **Optional choices** (a filter when finding licenses) hold an entry to leave it out.
- **Typed questions** are unchanged: `/cancel` and `/remove` stay there and at the hidden
  passphrase prompt.

## Reasons

- Arrow-key lists are what vendors meet in `psw` and other .NET tools; no number to read off and
  type.
- Starting on the proposed entry keeps PDR-0048's "Enter accepts the proposed value".
- Esc is the key terminal users reach for to back out; a visible Back entry tells those who do not
  know it.
- Ignoring Esc at the main menu avoids ending a session, and dropping the held passphrase, by a
  stray key.

## Rejected options

| Option | Why rejected |
|---|---|
| Arrows and typed numbers | Carl chose arrows only, as `psw` (2026-11-12) |
| Back or Cancel entry only, no Esc | Slower for vendors who expect Esc (Carl, 2026-11-12) |
| Esc only, no Back entry | Not discoverable |
| Esc at the main menu quits | One stray key ends the session and forgets the passphrase |
| Type-to-search in lists | Not needed at the sizes a small vendor has; later if asked |

## Consequences

- The vendor guide describes lists, Esc and the Back and Quit entries instead of numbers.
- A vendor used to the numbered menus of the first tool release relearns the menus once.
