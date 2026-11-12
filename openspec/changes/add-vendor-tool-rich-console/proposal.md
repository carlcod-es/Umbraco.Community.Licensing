## Why

The vendor tool (`add-vendor-tool`) asks everything as plain numbered menus and typed lines
(PDR-0048 "Plain prompts"), leaving selection lists, colour and progress to FR-13. Package Script
Writer's `psw` tool shows what a .NET terminal tool for Umbraco developers can feel like: arrow-key
menus, multi-select, tables, panels, colour, a banner, spinners and a review-and-edit step before
the result. Vendors meet both tools in the same community. Carl asked to bring `ucl` in line with
`psw` (2026-11-12), taking up FR-13.

This change is planned now and applied after `add-vendor-tool` is archived, so the first tool
release ships with the current interface and this one follows (design.md Q1).

## What Changes

- **Arrow-key lists** for every menu and every choice from a known list (products, features,
  signing keys, signing-key mode, folder offer, startup offer). Arrows and Enter choose; the
  cursor starts on the proposed or current entry. Typed numbers and IDs at a list go.
  **BREAKING** for vendors used to typing menu numbers; the guide changes with it.
- **Esc cancels** the action at a list, as `/cancel` does at a typed question; submenus show
  **Back** and the main menu **Quit** as entries. Typed `/cancel` and `/remove` stay at text
  questions and the hidden passphrase prompt.
- **Switches chosen together**: at issue and reissue the product's switch features are one
  multi-select (Space to tick, Enter to accept); at reissue the granted ones start ticked.
  Number and text features are still asked one by one.
- **Review before signing**: after the summary of an issue, reissue or renewal the vendor chooses
  Sign, Change answers or Cancel. Change answers ticks the answers to change, asks only those
  again with the current value proposed, and shows the summary again. Resend offers Sign or
  Cancel.
- **Presentation**: a banner when the session opens; lists of products, licenses, issue entries
  and signing keys as tables; the summary and setup's closing note in panels (help stays plain, as it may be redirected); colour for
  questions, proposed values, success, warnings and refusals, with the words still saying what
  happened; a spinner while slow work runs (passphrase check, key creation, backup, restore).
- **Kept as is**: key strings and trusted-key lines are printed plain, uncoloured and outside
  any border, each on its own line, so they copy cleanly; text answers keep the terminal's own
  line editing and paste; the passphrase stays at the tool's own hidden prompt; the tool still
  refuses with nobody at the terminal and takes no command other than help and version.
- **Colour off** when the terminal cannot show it or the vendor has asked terminals for no colour.
- Out of this change: Ctrl+C cancelling an action (FR-27), scripted mode (FR-22), typing to
  search a list, a full-screen interface.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

These capabilities are created by `add-vendor-tool` and reach `openspec/specs/` when it is
archived, before this change is applied.

- `vendor-tool-setup`: *Menu session* (lists, Back and Quit entries, banner), *Answers at a
  prompt* (lists chosen with arrow keys, Esc to cancel at a list), *Operating systems and
  terminals* (list keys and colour in every supported terminal); new requirements *Choosing from
  a list*, *Esc at a list*, *Several choices from a list*, *Screen presentation*, *Working
  indicator*, *Values shown as typed*, *Copyable output stays plain* and *Colour can be turned
  off*.
- `vendor-tool-licenses`: *Issuing a license* (review with Sign, Change answers, Cancel),
  *Switch values and features at reissue* (switches as one multi-select), *Resending a license*
  (Sign or Cancel).

## Impact

- **Vendor tool** (`src/Umbraco.Community.Licensing.Tool`): question and menu rendering, every
  action's summary step, every list and report output. No change to stored records, backups,
  key strings or the library.
- **Tests** (`tests/Umbraco.Community.Licensing.Tool.Tests`): the scripted console and every
  session script that answers a menu or list.
- **Dependencies**: one new package for the tool, named in design.md and its ADR; needs Carl's
  approval before apply.
- **Docs**: `docs/vendor-tool.md` (menus, lists, Esc, review step, colour), help text (menu wording only).
- **Records**: PDR-0063 to PDR-0066, ADR-0025; amends PDR-0044, PDR-0048, ADR-0017, ADR-0020.
  FR-13 taken into this change.
- **Personas**: vendor (faster, clearer sessions); site owner (keys still copy intact, a review
  step still stands between a wrong key and them); implementor, backoffice editor, site visitor
  unaffected.
