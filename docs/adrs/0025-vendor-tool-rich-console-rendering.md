# ADR-0025: Vendor tool rich console rendering

- Status: Decided
- Package approval: pending Carl (requested 2026-11-12); recorded here with the pinned version at apply
- Date: 2026-11-12
- Source: `add-vendor-tool-rich-console` design.md Q7, Q9, Q11. Amends ADR-0017, ADR-0020.

## Context

PDR-0063 to PDR-0066 bring the `psw` look to `ucl`: arrow-key lists with Esc, multi-select,
tables, panels, colour, a banner and a working indicator. ADR-0020 kept the console to the BCL,
read every answer with `Console.ReadLine`, and named Spectre.Console as FR-13. ADR-0021 keeps the
passphrase in a pinned buffer zeroed on every path. The tool's tests run whole sessions through a
scripted console.

## Decision

- **Spectre.Console** in the tool project only, pinned exactly; version recorded here at apply
  (task 1.1; `psw` ships 0.49.1 on .NET 10). MIT. No `Spectre.Console.Cli`, no
  `Spectre.Console.Testing`. The library is unchanged (ADR-0003).
- **Spectre for rendering only**: `Markup`, `Table`, `Panel`, `FigletText`, `Status`, and its
  live display for the tool's own list prompts.
- **The tool's own list prompts** (single and multi) on Spectre's live display, keys from
  `Console.ReadKey(intercept: true)`: Up, Down, Page Up, Page Down, Home, End, Enter, Space
  (multi), Esc. They start on the proposed entry and return an index, indexes or a cancel.
- **Text and yes/no questions** stay on `Console.ReadLine` with a markup-rendered question line.
  **Hidden passphrase** stays on the ADR-0020 and ADR-0021 reader.
- **Console seam.** `ToolConsole` gains `Render(IRenderable)`, `Choose`, `ChooseMany` and
  `Working`. Only `TerminalConsole` and the test console touch Spectre's `IAnsiConsole`. `Choose`
  and `ChooseMany` raise `AnswerRead` (ADR-0021).
- **Tests.** The scripted console renders through a Spectre console with no ANSI, no colour and
  width 120 into its transcript, answers a list by label, 1-based position or `<esc>`, and records
  a `Working` status line. List key handling is unit-tested through a key-source seam in
  `TerminalConsole`.
- **Escaping.** Every value not written in the tool's source reaches Spectre as `Text` or via
  `Markup.Escape`, through one helper.
- **Plain copy output.** Key strings and trusted-key lines are written with `ToolConsole.Write`,
  never through `Render`.
- **Terminal detection** is Spectre's: colour depth, Unicode, ANSI and Windows virtual-terminal
  support, which reads `NO_COLOR`, `TERM` and similar variables. Amends ADR-0017 and ADR-0020:
  the tool reads no environment variable for its own settings or the passphrase; the console
  library may read the standard terminal variables for display only.

## Alternatives Considered

| Option | Why not |
|---|---|
| Spectre's `SelectionPrompt` and `MultiSelectionPrompt` | No Esc to cancel, no starting entry, no cancel result to the caller (PDR-0063) |
| Spectre's `TextPrompt` for typed answers | Reads keys itself: loses the terminal's line editing, history and input methods (ADR-0020) |
| Spectre's `TextPrompt.Secret()` for the passphrase | Returns a `string` that cannot be zeroed (ADR-0021) |
| Hand-written ANSI with no package | Re-implements tables, frames, width and Unicode detection, Windows virtual-terminal setup |
| Terminal.Gui | Full-screen interface; larger dependency; not the `psw` style asked for |
| `Spectre.Console.Testing` | The scripted console already gives transcripts and answers; a second package for tests only |

## Consequences

- One runtime dependency added to the tool; the tool package grows.
- Session tests answer lists by label or position; Back and Quit move from `0` to the last entry.
- The legacy Windows console host is covered by observed checks, not automation.
- FR-27 (Ctrl+C) is unaffected: Ctrl+C is still not intercepted.

## Reversal Cost

Medium. Rendering sits behind `ToolConsole` and one styling helper, so returning to plain output
means replacing `Render` and the two list prompts; the specs' list behaviour (Esc, starting
entry) would then need the BCL key loop kept.
