# ADR-0017: Vendor tool guided console and startup

- Status: Amended by ADR-0020
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q32, Q33, Q34, Q35, Q36. Amends ADR-0012, ADR-0013.

## Context

PDR-0039 keeps the first version to guided mode: a person at a terminal, inputs allowed on the
command, the passphrase always typed, every action refused with nobody present, help and version
excepted. PDR-0038 adds Windows (PowerShell, Windows Terminal, Command Prompt) to macOS and
Linux. PDR-0040 moves an installation between operating systems. PDR-0041 checks only the
current folder and `.ucl` in the home folder, notes an unused second installation, ends after
setup started from the offer, and lists the commands when run with no command. ADR-0012 and
ADR-0013 were written for scripted use (`--no-input`, `--passphrase-stdin`, `UCL_PASSPHRASE`,
`--folder`).

## Decision

- **Person present** (amends ADR-0012): standard input is not redirected
  (`Console.IsInputRedirected` is false). Redirected standard output or standard error does not
  count. Checked once, before any command that is an action; on failure the tool writes the
  refusal to stderr and exits 1.
- **Not actions.** No command, `--help` and `--version` skip the check. The root command's action
  prints help to stdout and exits 0.
- **Passphrase** (amends ADR-0012): only from the hidden prompt, `Console.ReadKey(intercept:
  true)`, nothing echoed. Removed: `--passphrase-stdin`, `--no-input`, `UCL_PASSPHRASE`. Removed
  options are unknown options: a usage error, exit 2.
- **Folder** (amends ADR-0013): first of the current directory (`Environment.CurrentDirectory`)
  when it holds `ucl.db`, then `<home>/.ucl` with home from `Environment.SpecialFolder.UserProfile`
  (`%USERPROFILE%` on Windows, `$HOME` elsewhere). `--folder` removed. Both are checked on every
  action; when the current directory is used and `<home>/.ucl` holds `ucl.db`, a note naming it
  follows the `Using` line on stderr. Paths are shown with `Path.GetFullPath`, never `~`.
- **Setup from the offer.** After the closing message, one generic retry line; exit 0; the
  original command is not run.
- **Backup import target**: offered as at setup (current directory or `<home>/.ucl`).
- **Cross-OS portability.** No stored value holds an absolute path, a path separator or anything
  OS-specific. Encrypted PEMs are stored with `\n` line endings. Backup zip entry names use `/`.
  The SQLite file format is the same on every OS.
- **Console encoding.** Key strings, references and identifiers are ASCII. The tool does not set
  `Console.OutputEncoding`: on Windows that changes the console code page, which outlives the
  process in the Command Prompt session.
- **`ToolEnvironment`** carries the console (with its person-present flag), `TimeProvider`, home
  folder, current directory and PBKDF2 iteration count; no environment variables.

## Alternatives Considered

| Option | Why not |
|---|---|
| Person present also requires stdout not redirected | Refuses a person who redirects the key to a file (PDR-0039) |
| `Console.ReadLine` for the passphrase | Echoes it |
| Set `Console.OutputEncoding` to UTF-8 | Leaves the Command Prompt code page changed after the tool exits; only non-ASCII paths gain |
| Keep `--no-input` and the passphrase sources, unused | Untested surface for a mode not offered (FR-22) |
| An `--out` option writing the key as plain text | New scope; Carl chose to document the PowerShell 5.1 encoding (PDR-0039) |
| Re-enable `.github/workflows/all-platforms.yml` for the Windows suite | Disabled by Carl (2026-10-18); he runs the suite on each OS by hand. Cheapest way to automate it later |

## Consequences

- A command with stderr redirected still prompts, but the prompts are not visible; the guide
  says not to redirect stderr.
- A non-ASCII home path may show as `?` in a Command Prompt using a legacy code page; checked by
  Carl on Windows (tasks.md 13.4).
- The release run's `ucl --version` step runs with no terminal; it relies on the version being
  exempt.
- FR-22 brings back passphrase sources and a no-prompt option; FR-23 replaces the root help.

## Reversal Cost

Low: detection and passphrase reading sit behind `ToolConsole`; folder resolution is one
function. Adding scripted mode later adds options without changing existing ones.
