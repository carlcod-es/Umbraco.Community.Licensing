## Context

See proposal.md (Why). Current state, as `add-vendor-tool` leaves it:

- `ToolConsole` is the only console access: read a line, read hidden, write, write error, person
  present, and an `AnswerRead` event the idle lock listens to (ADR-0020, ADR-0021).
  `TerminalConsole` is the real one; tests use `ScriptedConsole` (a queue of answers and a
  transcript).
- `Questions` renders every question as one line, `<question> (<accepts>; Enter for …; /cancel to
  cancel): `, reads it with `ReadLine`, and renders lists as numbered lines answered by number or
  ID (`AskChoice`, `AskChoiceOrSkip`, `AskYesNo`, `AskText`, `AskHidden`).
- `MenuLoop` renders `Menus.Main` as numbered entries, `0` for Back or Quit.
- Every action writes its summaries, lists and reports as plain `WriteLine` calls.
- Whole-session tests answer menus and lists with numbers (`"2"`, `"1"`); about 1,670 tests.

Reference for the look: `psw` (Package Script Writer CLI), built on Spectre.Console 0.49.1:
`SelectionPrompt`, `MultiSelectionPrompt`, `Confirm`, `Ask`, `Status` spinners, `Table`, `Panel`,
`FigletText`, markup. `psw` does not test its interface.

## Bar

**Production**, unchanged from `add-vendor-tool`. The tool is on nuget.org and holds vendors'
private signing keys. Every spec scenario has an automated test, except those listed as observed
in Test strategy.

## Goals / Non-Goals

**Goals:** the `psw` look and feel for every menu, list, summary and report; no change to what
is stored, signed, exported or backed up; the passphrase handled exactly as now (ADR-0021); the
whole-session tests keep driving every scenario; works in the four Windows terminals and on
macOS and Linux.

**Non-Goals:** Ctrl+C cancelling an action (FR-27); scripted mode (FR-22); type-to-search in
lists; a full-screen interface; mouse; themes or colour settings; localisation; changing `--help`
beyond the menu wording.

## Requirements

| R | Requirement | Spec | Personas | Records |
|---|---|---|---|---|
| R1 | Menus and choices as arrow-key lists; Back and Quit entries; Esc cancels | vendor-tool-setup: Menu session; Answers at a prompt; Choosing from a list; Esc at a list | Vendor | PDR-0063 |
| R2 | Switch features chosen together | vendor-tool-setup: Several choices from a list. vendor-tool-licenses: Issuing a license; Switch values and features at reissue | Vendor; site owner (unchanged key contents) | PDR-0064 |
| R3 | Review before signing: Sign, Change answers, Cancel; resend Sign or Cancel | vendor-tool-licenses: Issuing a license; Resending a license | Site owner (a wrong key is still stopped before it reaches them); vendor | PDR-0065 |
| R4 | Banner, tables, panels, colour, working indicator; values shown as typed | vendor-tool-setup: Menu session; Screen presentation; Working indicator; Values shown as typed | Vendor | PDR-0066 |
| R5 | Key strings and trusted-key lines plain | vendor-tool-setup: Copyable output stays plain | Site owner (key arrives intact); implementor (release trusts the lines as copied) | PDR-0066 |
| R6 | Colour off when the terminal cannot or the vendor asks | vendor-tool-setup: Colour can be turned off | Vendor | PDR-0066 |
| R7 | Text answers keep line editing; passphrase prompt unchanged | vendor-tool-setup: Answers at a prompt | Vendor; site owner (passphrase safety unchanged) | PDR-0066, ADR-0025 |
| R8 | Every supported terminal | vendor-tool-setup: Operating systems and terminals | Vendor | PDR-0038 |

Persona check: key strings, records and backups are byte-for-byte as before, so site owners and
implementors see no change; the review step keeps PDR-0048's last stop before a key goes out.
Backoffice editor and site visitor never meet the tool.

## Questions settled in this change

| Q | Question | Answer | Records |
|---|---|---|---|
| Q1 | Sequencing with `add-vendor-tool` | Plan now; apply after `add-vendor-tool` is archived, so its specs are in `openspec/specs/` and the first tool release ships with the current interface (Carl, 2026-11-12) | — |
| Q2 | Which `psw` features | Arrow-key menus; tables, panels, colour; banner and spinners; multi-select for switches and the review loop (Carl, 2026-11-12) | PDR-0063 to PDR-0066 |
| Q3 | Typed numbers at a list | No: arrows and Enter only, as `psw` (Carl, 2026-11-12) | PDR-0063 |
| Q4 | Cancelling at a list | Esc cancels as `/cancel` does; submenus show Back, the main menu Quit; typed `/cancel` and `/remove` stay at text and hidden questions (Carl, 2026-11-12) | PDR-0063 |
| Q5 | Esc where there is no action | In a submenu Esc goes back; at the main menu and the startup offer it does nothing, so Quit is always chosen on purpose (Architect, for Carl to confirm) | PDR-0063 |
| Q6 | Key strings and trusted lines | Printed plain, uncoloured, unframed, each on its own line (Architect, keeps PDR-0048 "Key out" and 13.4 "the key copied from the screen verifies"; for Carl to confirm) | PDR-0066 |
| Q7 | Typed answers | Keep the terminal's own line editing and paste; only lists read single keys (Architect, keeps ADR-0020 and 13.4 "arrow keys and Backspace edit an answer"; for Carl to confirm) | PDR-0066, ADR-0025 |
| Q8 | What Change answers offers | Every answer of the action except the product (issue) and the license (reissue, renew); changing those means Cancel and start again. Resend offers no change (Architect, for Carl to confirm) | PDR-0065 |
| Q9 | Colour off | When the terminal cannot show colour, or the vendor has set the common no-colour terminal setting (`NO_COLOR`) (Architect, for Carl to confirm) | PDR-0066, ADR-0025 |
| Q10 | Help | Plain text, as it may be redirected; only the menu wording changes (Architect) | PDR-0066 |
| Q11 | Console library | Spectre.Console, as `psw` and FR-13 name, for rendering; the tool's own list prompts on top of it (Architect; the package needs Carl's approval) | ADR-0025 |

## Decisions

### Spectre.Console renders; the tool keeps its own prompts — ADR-0025

- **Package.** `Spectre.Console`, pinned exactly in the tool project only, at the latest stable
  version at apply (0.49.1 is what `psw` ships on .NET 10; task 1.1 records the chosen number in
  ADR-0025). MIT licence. No `Spectre.Console.Cli`, no `Spectre.Console.Testing`. The library
  project gains nothing (ADR-0003).
- **Rendering only from Spectre**: `Markup`, `Table`, `Panel`, `FigletText`, `Rule`, `Status`
  (spinner) and the live display used by the list prompts. Spectre's own `SelectionPrompt`,
  `MultiSelectionPrompt`, `TextPrompt` and `ConfirmationPrompt` are **not** used:
  - its lists cannot be cancelled with Esc, cannot start on a proposed entry and cannot report a
    cancel to the caller (Q4, Q5);
  - its `TextPrompt` reads keys itself, so the terminal's line editing, history and input-method
    handling are lost (ADR-0020 rejected that for text, Q7), and `.Secret()` returns a `string`
    that cannot be zeroed (ADR-0021).
- **Tool list prompts** (`ListPrompt`, `MultiListPrompt`): drawn with Spectre's live display,
  keys read through `ToolConsole`. Up/Down/PageUp/PageDown/Home/End move; Enter chooses; Space
  ticks (multi only); Esc cancels. The highlight starts on the proposed or current entry. A
  hint line under each list says which keys to use, including Esc, so every question still shows
  how to cancel. Paging at the terminal's height, with "more entries follow" as `psw` does.
- **Text, yes/no and hidden questions** keep `ReadLine` and `AskHidden` as now; their question
  line is rendered with markup (question, accepted values in grey, proposed value in green). Yes
  and no stay typed (`[Y/n]`), so `/cancel` keeps working there.

### Console seam — ADR-0025

`ToolConsole` grows three members; nothing else in the tool touches Spectre's `AnsiConsole`.

| Member | Terminal | Scripted (tests) |
|---|---|---|
| `Render(IRenderable)` | Spectre `IAnsiConsole` on stdout | A Spectre `IAnsiConsole` with no ANSI, no colour, fixed width 120, writing to the transcript |
| `Choose(ListView)` → index or cancel; `ChooseMany(ListView)` → indexes or cancel | The tool list prompts, keys from `Console.ReadKey(intercept: true)` | The next scripted answer: an entry's label, its 1-based position, `<esc>`; for many, labels or positions separated by commas, or empty for none. The rendered list is written to the transcript |
| `Working(string, Func<T>)` | Spectre `Status` spinner around the work | Writes the status text once to the transcript, runs the work |

- `Choose` and `ChooseMany` raise `AnswerRead` when they return, so a list choice restarts the
  idle lock as a line does (PDR-0052). Esc raises it too.
- Accepting positions in the scripted console keeps most existing session scripts as they are:
  `"2"` at the main menu still opens Licenses. Back and Quit move from `0` to the last entry, so
  those answers change.
- The list prompts' key handling is unit-tested against a key-source seam in `TerminalConsole`
  (a `Func<ConsoleKeyInfo>`), separately from the session tests.

### Escaping every value — ADR-0025

Every string that did not come from the tool's own source (display names, vendor tags, emails,
order references, feature values, paths, product IDs, exception messages) reaches Spectre only as
`Text` or through `Markup.Escape`. One helper builds every styled line; a test issues with display
name `Acme [red]Shop[/]` and finds it verbatim in the summary (spec: Styling characters in a
value). An unescaped `[` would otherwise throw or restyle the line.

### Plain output for copying

Key strings and trusted-key lines go through `ToolConsole.Write` as raw text, never through
`Render`, never inside a `Panel` or `Table`, with no colour (spec: Copyable output stays plain).
The rest of the output around them may be styled.

### Colour, Unicode and the terminal — ADR-0025

- Spectre's own detection decides colour depth, Unicode and ANSI support, including `NO_COLOR`,
  `TERM` and Windows virtual-terminal support. This reads environment variables for display only;
  the tool itself still reads none for its settings, and the passphrase never (amends ADR-0017,
  ADR-0020 "no environment variables").
- Borders use Spectre's safe borders, so a terminal without Unicode gets ASCII frames. Glyphs such
  as `✓` are only written when the profile reports Unicode; otherwise words alone.
- Colour roles: question bold; proposed value green; refusal red; warning yellow; success green;
  hints grey. Every message keeps its words; colour is never the only signal (spec: Refusal in
  words).

### Banner

`FigletText("UCL")` and a line with `Umbraco.Community.Licensing.Tool` and `ToolApp.Version`,
shown once when the session opens, before the `Using` line. When the terminal is narrower than
the banner, a one-line heading instead. Not shown for `--help` or `--version`.

### Tables and panels

- Tables: Products > List products; Licenses > Find and list, Show a license (issue entries);
  Signing keys > Status; the restore and backup reports where they list items. Columns are the
  fields the specs name, in that order.
- Panels: the summary before signing (header: Issue, Reissue, Renew or Resend); setup's closing
  message; the folder-in-use report.
- Trusted keys stays plain lines (Plain output for copying).

### Working indicator

`Working` wraps: the passphrase check and private-key decryption (PBKDF2 at 600,000 iterations,
ADR-0014), key-pair creation and encryption, backup write, and restore (unzip, checks,
migrations). Questions are never asked inside `Working`.

### Switch features chosen together

At issue and reissue, `LicenseActions` asks one `ChooseMany` over the product's switch
definitions (ticked = granted; at reissue the granted ones start ticked), then the number and
text features one by one as now. No switches: no list. The stored and signed features are as
before: `off` meant left out (PDR-0044), and unticked means the same.

### Review before signing

One `SigningReview` used by issue, reissue and renew: render the summary panel, `Choose` among
Sign (proposed), Change answers, Cancel. Change answers runs `ChooseMany` over the action's
answer names (Q8), re-asks only the ticked ones with the earlier answer as the proposed value,
and loops back to the summary. The action's answers live in one record the review can update,
so a re-asked feature or switch list is the same code as the first ask. Resend uses the same
panel with Sign and Cancel only. Esc at the review is Cancel. Nothing is signed or stored before
Sign.

### Menus

`Menus.Main` is unchanged in shape; `MenuLoop` renders each level with `Choose`, adds Back (in a
submenu) or Quit (main menu) as the last entry, and treats Esc as Back in a submenu and ignores
it at the main menu and the startup offer (Q5). The startup offer and Settings > New
installation become lists the same way.

### Ownership

| Path | Owner |
|---|---|
| `src/Umbraco.Community.Licensing.Tool/**`, `tests/Umbraco.Community.Licensing.Tool.Tests/**` | worker-backend. The tool's interface lives in the same project as its data and actions, so no UI project is created and worker-frontend has no paths here |
| `src/Umbraco.Community.Licensing.Tool/Umbraco.Community.Licensing.Tool.csproj` (`Spectre.Console` reference) | worker-backend, in the brief that adds it, after Carl's approval |
| `docs/vendor-tool.md`, records, `docs/feature-requests.md` | Architect |

## Test strategy

The bar is production. The whole-session approach of `add-vendor-tool` stays: every scenario runs
through `ToolApp.Run` with the scripted console, a fake `TimeProvider` and a temp folder.

| Level | What | How |
|---|---|---|
| Unit | List prompts: highlight starts on the proposed entry; Up/Down/Page/Home/End; paging hint; Enter returns the index; Space ticks; Enter with none ticked; Esc cancels; Esc ignored where configured; `AnswerRead` raised on Enter and Esc | xUnit against `TerminalConsole` with a scripted key source and a Spectre console writing to a string |
| Unit | Escaping: every styled-line helper with `[`, `]`, `[red]`, `[/]` in values | xUnit |
| Unit | Colour off: a Spectre profile with no colour renders the same words | xUnit |
| Integration | Every new and modified scenario in the two spec deltas as whole sessions; every existing session test still passing (menus answered by label or position, Back and Quit as last entries) | xUnit |
| Integration | Key string and trusted lines in the transcript on lines of their own with nothing else on the line, through the same `Render` setup the terminal uses with colour on | xUnit |
| Integration | Review loop: Change answers re-asks only the ticked answers, proposes earlier values, loops to the summary; Cancel and Esc store nothing; resend offers two entries | xUnit |
| Integration | Switch multi-select at issue and reissue, including a product with no switches; stored and signed features equal those from the same choices before this change | xUnit |
| Observed | Lists, Space, Esc, colour, spinner, banner and copying a key from the screen in Windows PowerShell 5.1, PowerShell 7, Windows Terminal and Command Prompt, and in macOS Terminal and a Linux terminal; `NO_COLOR=1` once | Carl, recorded in tasks.md |

Validation for every worker block: `dotnet build Umbraco.Community.Licensing.slnx`,
`dotnet test --solution Umbraco.Community.Licensing.slnx`, `node --test tools/release/*.test.mjs`
(unquoted on Node 20).

## Risks / Trade-offs

- [Command Prompt and Windows PowerShell 5.1 run in the legacy console host; live redraws can
  flicker or leave lines behind] → Spectre enables virtual-terminal processing on Windows 10 and
  later; the observed checks cover both hosts; the list prompt redraws only its own lines.
- [Esc on macOS and Linux arrives as the start of an escape sequence, so .NET may wait briefly to
  tell Esc from an arrow key] → acceptable delay; covered by the observed checks.
- [A Spectre release changes rendering or detection] → exact pin; transcripts are asserted on
  words, not on borders.
- [Spectre reads `NO_COLOR`, `TERM` and similar] → display only; amends ADR-0017; the
  passphrase-from-environment scenario still passes.
- [A vendor value containing markup] → one escaping helper, unit and session tests (Escaping
  every value).
- [The tool package grows by one dependency and its size] → the only one added; pinned; MIT.
- [Copying a key from inside a panel would pick up border characters] → key strings never sit in
  a panel or table (Plain output for copying).
- [Vendors who learned the numbered menus] → the guide changes in this change.
- [About 1,670 tests answer menus by number] → the scripted console accepts positions, so only
  Back/Quit answers and the changed scenarios move; the worker reports the count of edited tests.

## Migration Plan

- Starts after `add-vendor-tool` is archived (Q1). No data migration: the database, backups,
  exports and key strings are unchanged.
- Released as the next tool version from the same release run (ADR-0011).
- Rollback: install the previous tool version; folders and backups are compatible both ways
  because nothing stored changes.

## Open Questions

None that change the specs or tasks. The exact Spectre.Console version is fixed in task 1.1.
