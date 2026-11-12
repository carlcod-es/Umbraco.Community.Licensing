Owners per CLAUDE.md and design.md (Ownership): sections 1 to 6 worker-backend; section 7
Architect; section 8 Carl. Starts only after `add-vendor-tool` is archived (design.md Q1).
Validation for every worker block: `dotnet build Umbraco.Community.Licensing.slnx`,
`dotnet test --solution Umbraco.Community.Licensing.slnx`, `node --test tools/release/*.test.mjs`
(unquoted on Node 20). Bar: production (design.md). Test names follow spec scenario names; every
scenario runs as a whole session through the scripted console (design.md Test strategy). Specs:
`specs/vendor-tool-setup/spec.md`, `specs/vendor-tool-licenses/spec.md`. Records: PDR-0063 to
PDR-0066, ADR-0025; amended PDR-0044, PDR-0048, ADR-0017, ADR-0020.

## 1. Package

Depends on: `add-vendor-tool` archived. Owner: worker-backend. Records: ADR-0025.

- [ ] 1.1 Add `Spectre.Console` to `src/Umbraco.Community.Licensing.Tool` only, pinned exactly at the latest stable version (approved by Carl, date recorded here and in ADR-0025 with the version), and no other package (no `Spectre.Console.Cli`, no `Spectre.Console.Testing`), and verify `dotnet build` passes with no warnings, the library project has no new package reference, the tool references only EF Core Sqlite, EF Core Design and `Spectre.Console`, and the packed tool installs from the pack folder and `ucl --version` prints `Version.props`

## 2. Console seam and list prompts

Depends on: 1. Owner: worker-backend. Spec: vendor-tool-setup (Choosing from a list; Esc at a list;
Several choices from a list; Screen presentation; Values shown as typed; Colour can be turned off). Records: PDR-0063,
PDR-0066, ADR-0025, ADR-0021.

- [ ] 2.1 Add `Render(IRenderable)`, `Choose(ListView)`, `ChooseMany(ListView)` and `Working(string, Func<T>)` to `ToolConsole` (design.md Console seam), with `TerminalConsole` on a Spectre `IAnsiConsole` and a key-source seam, and the scripted console rendering through a Spectre console with no ANSI, no colour and width 120, answering a list by label, 1-based position or `<esc>` (many: comma-separated, or empty for none) and recording each `Working` status line, and verify unit tests for each member in both consoles and that `Choose` and `ChooseMany` raise `AnswerRead` on Enter and on Esc
- [ ] 2.2 Implement the single and multi list prompts in `TerminalConsole` (design.md Spectre.Console renders; the tool keeps its own prompts): highlight starts on the proposed entry, Up, Down, Page Up, Page Down, Home, End, Enter, Space (multi), Esc as cancel or ignored per list, paging with a "more entries follow" line, a key hint naming Esc, and verify by key-source unit tests for "Proposed entry highlighted", "Long product list", "Two ticked", "None ticked", Esc cancelling, Esc ignored, and each key
- [ ] 2.3 Add the one styling helper (design.md Escaping every value; Colour, Unicode and the terminal): colour roles, `Markup.Escape` or `Text` for every value not written in the tool's source, `✓` only when the profile reports Unicode, safe borders, and verify unit tests with `[`, `]`, `[red]` and `[/]` in values, and that a no-colour profile renders the same words ("Refusal in words", "No colour asked for" at the rendering level)

## 3. Questions and menus

Depends on: 2. Owner: worker-backend. Spec: vendor-tool-setup (Menu session; Answers at a
prompt; Choosing from a list). Records: PDR-0063, PDR-0066, ADR-0020, ADR-0025.

- [ ] 3.1 Move `Questions.AskChoice` and `AskChoiceOrSkip` onto `Choose` (proposed entry highlighted; a leave-out entry for optional choices; Esc throws `ActionCancelledException`), keep `AskText`, `AskYesNo` and `AskHidden` on `ReadLine` and the hidden reader with a markup-rendered question line, and verify `QuestionsTests` cover a list answered by label and by position, the proposed entry, the leave-out entry ("Optional filter left out"), Esc ("Action cancelled at a list"), and that `/cancel` and `/remove` still work at typed and hidden questions
- [ ] 3.2 Render `MenuLoop`, the startup offer and Settings > New installation with `Choose`: Back as the last entry of each submenu, Quit as the last of the main menu, Esc as Back in a submenu and ignored at the main menu and the startup offer; update every session test whose script answered `0`, and verify "Back from a submenu", "Esc in a submenu", "Esc at the main menu", "Several jobs in one session", and that the full suite passes, reporting how many test files changed
- [ ] 3.3 Show the banner when the session opens (FigletText `UCL`, tool name and version; one-line heading when the terminal is narrower), not for `--help` or `--version`, and update `--help` menu wording only, and verify "Banner at the start", that `--help` and `--version` output carry no banner and no ANSI codes when redirected, and that "Help and version with nobody present" still passes

## 4. Presentation in actions

Depends on: 3. Owner: worker-backend. Spec: vendor-tool-setup (Screen presentation; Working
indicator; Values shown as typed; Copyable output stays plain). Records: PDR-0066, ADR-0025.

- [ ] 4.1 Render Products > List products, Licenses > Find and list, Show a license (issue entries) and Signing keys > Status as tables with the columns the specs name, and setup's closing message and the folder-in-use report as panels, and verify "Licenses as a table", "Products listed" and the setup scenarios still pass with the same words in the transcript
- [ ] 4.2 Write every key string and every Trusted keys line with `ToolConsole.Write` outside `Render`, with no colour, frame or table, and verify "Key copied from the screen" and "Trusted key lines copied": with a colour-on Spectre console, the transcript line holding a key string or a trusted line holds that text alone with no escape code, and the key verifies with `LicenseEvaluator`
- [ ] 4.3 Wrap the passphrase check and key decryption, key-pair creation, backup write and restore in `Working`, asking no question inside it, and verify "Working on a backup" and that the idle-lock tests (`SessionLockTests`, `SessionPassphraseTests`) pass unchanged
- [ ] 4.4 Route every value shown in actions (display names, vendor tags, emails, order references, feature values, paths, product IDs, failure messages) through the styling helper, and verify "Styling characters in a value" as a whole session (issue with display name `Acme [red]Shop[/]`, summary and verified key carry it exactly) and a failure message containing `[` shown intact

## 5. Switches and review before signing

Depends on: 4. Owner: worker-backend. Spec: vendor-tool-licenses (Issuing a license; Switch
values and features at reissue; Resending a license); vendor-tool-setup (Several choices from a
list). Records: PDR-0064, PDR-0065, PDR-0044.

- [ ] 5.1 At issue and reissue ask the product's switch features as one `ChooseMany` (granted ones ticked at reissue; no list when the product has none), then number and text features one by one as before, and verify "Switch left unticked", "Features kept at reissue", "Number feature removed at reissue", "Switch unticked at reissue", "Product with no switches", "Optional inputs skipped", "License issued", and that for the same choices the stored and signed features equal those of the `add-vendor-tool` behaviour
- [ ] 5.2 Add `SigningReview` for issue, reissue and renew (summary panel; Sign proposed, Change answers, Cancel; Change answers as a `ChooseMany` over the action's answers except product and license, re-asking only the ticked ones with the earlier answer proposed, then the summary again; Esc as Cancel) and Sign or Cancel only for resend, and verify "Answers changed at the summary", "Product not changed at the summary", "Cancelled at the summary", "Nothing to change at a resend", "Site owner lost the key", "Resend of an expired license", and that nothing is signed or stored before Sign (database unchanged after Cancel and Esc)

## 6. Every terminal, automated part

Depends on: 5. Owner: worker-backend. Spec: vendor-tool-setup (Operating systems and terminals;
Answers at a prompt). Records: PDR-0038, ADR-0025.

- [ ] 6.1 Create the terminal's Spectre console with Spectre's own detection (no forced colour system, ANSI or Unicode setting), and keep refusals written before the session opens as plain text on stderr, and verify by a unit test over the settings `TerminalConsole` uses, that "Nobody present", "Output redirected" and "Passphrase not taken from the environment" pass, and that the `all-platforms.yml` run passes on Windows, macOS and Linux

## 7. Documentation

Depends on: 3 to 6. Owner: Architect.

- [ ] 7.1 Update `docs/vendor-tool.md`: lists with arrow keys and Enter, Esc to cancel (Back at a submenu, nothing at the main menu), Back and Quit entries, typed `/cancel` and `/remove` at typed questions only, the switch list at issue and reissue, the review step (Sign, Change answers, Cancel; resend Sign or Cancel), tables and panels, the banner, the working indicator, key strings and trusted lines copied plain, colour off with `NO_COLOR`; remove every reference to menu numbers and `0`; and verify each named screen against the running tool after 6.1

## 8. Verification by Carl

Depends on: 7. Owner: Carl.

- [ ] 8.1 In Windows PowerShell 5.1, PowerShell 7, Windows Terminal and Command Prompt, and in macOS Terminal and a Linux terminal: move through the menus, tick a switch with Space while issuing, press Esc at a list and in a submenu, use Change answers at the summary, watch the working indicator on the first signing, copy the key from the screen and verify it, copy the trusted lines, and edit a typed answer with the arrow keys and Backspace; and verify each ("Lists in each Windows terminal", "Typed answer edited", "Key copied from the screen"); record the result per terminal here
- [ ] 8.2 Start `ucl` once with `NO_COLOR=1` and issue a license, and verify "No colour asked for": no colour shown, every action works; record the result here

## Workflow follow-up

- Archive `add-vendor-tool` before section 1 starts (design.md Q1).
- Archive this change after section 8 is recorded.
