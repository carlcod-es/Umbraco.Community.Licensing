# PDR-0038: Vendor tool operating systems and terminals

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q32. Amends PDR-0031.
- Personas: vendor (installs and runs the tool on whichever operating system they develop on).
  Site owner and implementor: unaffected; keys are the same whichever operating system issued
  them. Backoffice editor and site visitor never meet the tool.

## Context

The vendor tool is installed as a global .NET tool (ADR-0011). The project targets Windows,
macOS and Linux, but the change named only macOS and Linux for verification (FR-2: "Windows is
not covered by these machines"), and its setup text used `~/.ucl`, a form Windows does not show.

## Decision

- **Operating systems.** The tool runs on Windows, macOS and Linux (Carl, 2026-11-07).
- **Home folder.** `~/.ucl` means `.ucl` in the user's home folder as each operating system
  defines it; on Windows, the profile folder, for example `C:\Users\carl\.ucl` (Carl,
  2026-11-07).
- **Paths shown.** Every path the tool shows is the full path in the operating system's own
  form, never `~` (Carl, 2026-11-07).
- **Windows terminals.** On Windows the tool works in PowerShell, Windows Terminal and Command
  Prompt: prompts, hidden passphrase entry, redirected output, passphrase from the environment
  or standard input, and exit codes. The tool guide gives scripting examples for each shell
  (Carl, 2026-11-07).
- **Verification.** Before the change is archived, the automated suite runs on Windows as well
  as macOS and Linux, and Carl checks the tool by hand in all three Windows terminals (Carl,
  2026-11-07).
- **Sync warning** (amends PDR-0031). Setup's warning to keep the folder out of cloud-synced
  folders names a few services as examples, such as OneDrive, iCloud Drive or Dropbox, and does
  not present them as a complete list (Carl, 2026-11-07).

## Reasons

- Vendors build .NET products on any of the three operating systems; a tool that works on only
  two would push some of them back to issuing by hand.
- A path shown as the operating system writes it can be pasted into that system's file manager
  or shell as is.
- Windows vendors use all three terminals, and each handles hidden input, piping, environment
  settings and exit codes differently; a vendor meets those differences first when scripting.
- Automated tests prove the logic; only a person at a real terminal sees what typing a
  passphrase or copying a key string is like there.
- Naming examples helps a vendor who does not realise their profile is synced (common with
  OneDrive on Windows). A full list would go out of date.

## Rejected options

| Option | Why rejected |
|---|---|
| macOS and Linux only | Leaves Windows vendors out; conflicts with the project's cross-platform constraint |
| Windows checked by automated tests only | Does not show behaviour at a real terminal |
| Windows checked by hand only | Leaves the suite unproven on Windows |
| Windows support stated, check deferred | Ships an unverified promise |
| Generic sync warning with no examples | A vendor may not recognise their own synced folder |
| Exhaustive list of sync services | Goes out of date; implies unlisted services are safe |

## Consequences

- The change's verification grows to a Windows machine, run by Carl.
- The tool guide carries per-shell scripting examples.
- FR-2's note that Windows is not covered is answered by this change.
