# PDR-0061: A typed path may start with `~` for the home folder

- Status: Decided
- Date: 2026-11-12
- Source: `add-vendor-tool` design.md Q82 (follow-ups row 173, raised while writing the vendor
  guide).
- Personas: vendor (types `~/Backups/…` as they would anywhere else on macOS and Linux, and gets
  the folder they meant instead of a folder named `~`). Site owner, implementor, backoffice
  editor, site visitor: unaffected.

## Context

Four questions take a path the vendor types: the backup export, the export by signing key, a key
string saved to a file, and the backup import. A relative path is read from the current folder.
A path typed as `~/Backups/ucl.zip` was read as a folder named `~` under the current folder, so
the export was refused (no such folder) or the import not found. The vendor guide had to tell
the vendor to type full paths.

## Decision

- A typed path that is `~` alone, or `~` followed by a path separator (`/`, and on Windows also
  `\`), stands for the home folder: the same home folder the tool uses for `.ucl` (on Windows,
  the user's profile folder).
- Any other `~` is part of a name: `~backups/x.zip` and `a~b.zip` are read as typed. `~user`
  forms are not supported.
- A relative path is still read from the current folder.
- The tool still shows every path in full, in the operating system's own form, never with `~`.
- Applies to every question that takes a typed path. Folders offered as numbered choices are
  unaffected.

## Reasons

- `~` is how vendors on macOS and Linux write their home folder; reading it as a folder name is
  a surprise that costs a retyped answer.
- One rule on every operating system: Windows users who type `~\Documents` get what they meant.
- Showing the full path afterwards still tells the vendor exactly which file was used.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep `~` as a plain character | Surprising on macOS and Linux; the guide had to warn about it |
| Support `~user` | Needs another user's home folder; no use for one vendor at their own terminal |
| Expand environment variables (`$HOME`, `%USERPROFILE%`) | A second, per-OS syntax for the same thing |

## Consequences

- A folder actually named `~` in the current folder is reached as `./~`.
- The vendor guide says `~` works in typed paths.
