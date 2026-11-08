# PDR-0039: Vendor tool runs in guided mode only

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q33. Amends PDR-0030, PDR-0032, PDR-0034, PDR-0038.
  Scripted mode moved to `docs/feature-requests.md` FR-22.
- Personas: vendor (one way to run the tool in its first version; loses automation until
  FR-22). Site owner and implementor: unaffected; keys are the same however they were issued.
  Backoffice editor and site visitor never meet the tool.

## Context

PDR-0030 let every action except setup run from a vendor's script with nobody present, the
passphrase supplied from an environment setting or standard input. Carl chose to build only the
mode where a person is at the terminal for the first version, and to take the scripted use case
up later (Carl, 2026-11-08).

## Terms

- **Guided mode.** A person is at a terminal and answers the tool. The person may give inputs
  on the command; the tool asks for any it still needs. The passphrase is always typed at a
  hidden prompt.
- **Scripted mode.** Nobody is present. Every input, including the passphrase, comes from the
  command, an environment setting or standard input, and the tool asks nothing.

## Decision

- **Guided mode only.** The first version of the tool offers guided mode only. Scripted mode is
  FR-22 (amends PDR-0030 "By hand or batched").
- **Arguments allowed.** Any input may be given on the command; the tool asks only for those
  missing.
- **Passphrase typed.** The passphrase is asked at a hidden prompt each time an action needs it,
  never taken from an argument, an environment setting or standard input (amends PDR-0032 and
  PDR-0038).
- **Nobody present.** Every action, not only setup, refuses when no person is present to answer,
  saying the tool is run by a person at a terminal, and changes nothing. Listing the commands,
  help and the version are not actions and work with nobody present (Carl, 2026-11-08).
- **Redirected result.** The result of an action alone goes to standard output, so a person may
  redirect it to a file; prompts and messages stay on screen. Windows PowerShell 5.1 writes a
  redirected file in its own encoding (UTF-16), which the tool cannot control; the guide tells
  its users to copy the key from the screen, use PowerShell 7, or write the file with an
  explicit plain-text encoding (Carl, 2026-11-08).
- **Suggested expiry.** The vendor takes the suggested renewal expiry by answering the prompt or
  by asking for it on the command (amends PDR-0034 "Scripts take the suggestion").
- **Guide.** The tool guide gives no scripting examples (amends PDR-0038).

## Reasons

- One mode is less to build, test and document in the first version, and the vendors it serves
  issue by hand.
- A passphrase always typed is never left in an environment setting, a shell history or a
  script file.
- Allowing inputs on the command keeps repeat use quick for a vendor who knows the command, and
  keeps commands in the shape scripted mode would use, so FR-22 adds a mode without changing how
  commands look.
- Refusing with nobody present is the clear failure for a script that calls the tool by mistake.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep scripted mode (PDR-0030) | Carl chose guided only for the first version |
| Prompts only, no inputs on the command | Slower for repeat use; commands would change shape when scripted mode is added |
| Help and version refused with nobody present | The release run checks the packed tool's version with no terminal; help harms nothing |
| The tool writes the key file itself (an output-file option) | New option and scope; documenting the PowerShell 5.1 behaviour is enough (Carl, 2026-11-08) |
| Passphrase from an environment setting in guided mode | Leaves the passphrase where other processes and shell profiles can see it, for no gain when a person is present |

## Consequences

- A vendor issuing many licenses types the passphrase once per license until FR-22 (and FR-20).
- PDR-0030's rejected option "By hand only" ("any later automation means reworking every
  command") is answered by keeping inputs on the command.
- Setup's own rules (PDR-0030, design.md Q28) are unchanged: setup takes no answers from the
  command.
