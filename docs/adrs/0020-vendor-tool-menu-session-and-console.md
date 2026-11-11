# ADR-0020: Vendor tool menu session and console

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q59, Q61, Q63, Q65. Supersedes ADR-0012. Amends ADR-0013, ADR-0017,
  ADR-0019.

## Context

PDR-0048 makes the tool a menu session: `ucl` opens a numbered menu, a person answers
line-by-line questions, the menu returns after each action until Quit. Only help and version
remain as commands; no input is given on the command. A bad answer names its rule and asks that
question again; every question shows how to cancel the action; a key string is shown on screen
and may be saved to a file instead of being redirected. PDR-0039 still refuses with nobody
present.

ADR-0012 chose System.CommandLine for about 20 subcommands with options, and split output so
stdout could be redirected. ADR-0017 kept the split, exempted "no command" from the
person-present check (it printed help), ended setup started from the startup offer with a retry
line, and wrote the `Using` line to stderr. None of that fits a session.

## Decision

- **Arguments, by hand.** No arguments: open the session. Exactly one of `--help`, `-h`, `-?`:
  help on stdout, exit 0. Exactly `--version`: the version on stdout, exit 0. Anything else
  (a removed option such as `--folder`, a command such as `license issue`, any extra argument):
  a refusal on stderr saying the tool takes no command other than `--help` and `--version` and
  is used from its menu, exit 2, nothing read or written. Arguments are checked before the
  person-present check, so help and version work with nobody present.
- **Help** describes the menu (its six entries and what each holds), not commands. **Version**
  is the assembly's informational version with any `+` build metadata removed, so it equals
  `Version.props`.
- **Person present** (amends ADR-0017, PDR-0050): neither standard input nor standard output
  redirected (`Console.IsInputRedirected` and `Console.IsOutputRedirected` both false). A
  redirected stderr does not count. Checked once, after the arguments and before the folder is
  resolved. On failure: refusal on stderr, exit 1.
- **Output.** Inside the session everything (menus, questions, messages, key strings, lists)
  goes to stdout. stderr carries only refusals written before the session opens (arguments,
  nobody present, a database the tool cannot open). Amends ADR-0013 and ADR-0017: the `Using`
  line and the unused-installation note go to stdout.
- **Answers** are read with `Console.ReadLine`, so the terminal's own line editing, paste and
  input methods work in every supported terminal. Leading and trailing white space is removed
  before the answer's rule is checked, except for the passphrase, which is taken as typed.
- **Reserved answers.** `/cancel` at any question, the hidden passphrase prompt included,
  cancels the action: nothing is stored and the menu it was chosen from is shown. `/remove` at
  reissue removes the display name, the vendor tag, or a number or text feature (PDR-0050). Both are matched as the whole
  answer, ignoring case. Every question's hint names `/cancel`; a question that accepts
  `/remove` also names it. A value equal to a reserved answer cannot be entered.
- **Question shape.** `<question> (<what it accepts>; Enter for <proposed or current>; /cancel
  to cancel): `. A refused answer writes one line naming the rule, then the same question again;
  earlier answers of the action are kept. Lists (products, features, signing keys) are shown
  numbered; the answer is the number or the ID. Yes/no questions take `y` or `n` with the
  proposed one in capitals (`[Y/n]`).
- **Menus.** Numbered entries; `0` is Back in a submenu and Quit in the main menu. A refused
  menu answer shows the menu again.
- **Hidden passphrase** read with `Console.ReadKey(intercept: true)`: nothing echoed, Backspace
  removes the last character, Enter ends. The characters are read straight into a pinned buffer
  (ADR-0021).
- **End of input.** `ReadLine` returning null (the terminal closed) ends the session as Quit
  does, except that the action in progress stores nothing and the exit code is 1.
- **Ctrl+C** is not intercepted: the process ends as the terminal makes it. Each action writes
  in one transaction after its last question, so an action interrupted before then stores
  nothing.
- **Actions** collect their answers first and write in one database transaction at the end; an
  action is cancelled by an exception the menu loop catches, which then shows its menu.
- **Exit codes.** 0 Quit, help, version, Quit at the startup offer. 1 nobody present, end of
  input, a database the tool cannot open (ADR-0013). 2 any argument other than help or version.
- **Named files** (amends ADR-0019): the path is always asked at a question; the checks and
  `FileMode.CreateNew` are unchanged. A key saved to a file is written as ASCII bytes, the key
  string alone, no byte order mark and no line ending.
- **Testability.** `ToolApp.Run(args, ToolEnvironment)` runs the whole session in-process.
  `ToolConsole` is the only console access: read a line, read hidden, write, write error, person
  present. Tests use a scripted console: a queue of answers (each optionally preceded by a clock
  advance, ADR-0021) and a transcript of everything written.
- **No System.CommandLine.** The dependency is removed from the tool.

## Alternatives Considered

| Option | Why not |
|---|---|
| Keep System.CommandLine for `--help` and `--version` | One package to parse two flags. It also accepts response files (`@file`) and directives by default, more input surface than the tool allows |
| Esc to cancel, through a line reader built on `Console.ReadKey` | Loses the terminal's line editing, history and input-method handling in four Windows terminals; echo and erase of wide characters (a CJK display name) needs width handling. Esc is FR-13 territory |
| Ctrl+C cancels the action (`Console.CancelKeyPress` with `Cancel = true`) | What `ReadLine` returns after an intercepted Ctrl+C differs between Windows and Unix in .NET; untestable through the scripted console |
| Empty answer cancels | An empty answer already accepts the proposed value or skips an optional input (PDR-0048) |
| A reserved word without `/` (`cancel`, `remove`) | Plausible as a display name, vendor tag or text feature value |
| Person present judged by input only (ADR-0017) | A redirected output leaves a session waiting at questions nobody sees (PDR-0050) |
| Keep the stdout/stderr split inside the session | Its only purpose was redirecting the result, which PDR-0048 removed |
| Spectre.Console prompts | A dependency; FR-13 |

## Consequences

- One fewer runtime dependency; the tool's runtime references are the library and EF Core with
  SQLite.
- Help and the refusal for arguments are a few lines of hand-written code with their own tests.
- A display name, vendor tag, order reference, customer email or text feature value of exactly
  `/cancel` or `/remove` cannot be entered. The guide says so.
- `ucl > file` is refused; `ucl --version > file` still works, so the release run's version step
  is unaffected.
- Ctrl+C ends the session with a non-zero code from the OS; the passphrase is lost with the
  process (ADR-0021). Ctrl+C cancelling only the current action is FR-27: additive, `/cancel`
  stays, and needs the `ReadLine` behaviour after an intercepted Ctrl+C settled per OS first.
- FR-22 (scripted mode) adds commands back; the session code is untouched by that, the argument
  check gains entries. FR-13 can replace `ToolConsole`'s question rendering without changing
  actions.

## Reversal Cost

Low: argument handling is one function; question rendering and the reserved answers sit in the
question helper behind `ToolConsole`. Bringing System.CommandLine back for FR-22 is additive.
