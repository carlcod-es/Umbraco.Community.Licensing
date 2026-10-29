# ADR-0013: Vendor tool database and folder

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q6, Q20, Q29

## Context

PDR-0031: one self-contained folder, portable by copying, backed up as one zip. PDR-0033: a
cleared customer email leaves no copy. Later tool versions must upgrade a vendor's existing
database. Carl prefers EF Core.

## Decision

- **Database.** EF Core 10 with SQLite (`Microsoft.EntityFrameworkCore.Sqlite`), one file
  `ucl.db` in the folder. A folder holds an installation when `ucl.db` exists.
- **Schema.** EF migrations, created with `dotnet-ef` (local tool, pinned) and
  `Microsoft.EntityFrameworkCore.Design` (build only). Applied on open, each in a transaction. A
  database holding a migration the running tool does not know is refused: "update the tool".
- **Connections.** `PRAGMA secure_delete = ON` and `journal_mode = DELETE` on every connection, so
  overwritten and deleted values do not survive in free pages or a WAL file.
- **Folder resolution**, first match: `--folder`; the current directory
  (`Environment.CurrentDirectory`) when it holds `ucl.db`; `<home>/.ucl`, home being
  `Environment.SpecialFolder.UserProfile`. Parent directories are not searched. No pointer file,
  no folder environment variable. Every command that opens an installation writes
  `Using <full path>` to stderr first (design.md Q29, Carl, 2026-10-29).
- Tables as in `add-vendor-tool` design.md (Database and folder). No column holds a key string.

## Alternatives Considered

| Option | Why not |
|---|---|
| SQL Server (FR-2 constraint) | Not in the folder; dropped by Carl, 2026-10-28 (PDR-0031) |
| Raw `Microsoft.Data.Sqlite` | Hand-written SQL and schema upgrades |
| `EnsureCreated` without migrations | Cannot upgrade an existing database |
| WAL journal mode | A cleared value can remain in the WAL file until checkpoint |
| Pointer file in the per-user config folder (`ApplicationData`), with `UCL_FOLDER` | The original choice. Replaced by Carl's current-directory rule (design.md Q29): a second installation or a restore no longer repoints later commands, and nothing outside the folder holds state |
| Search parent directories, as git does | More rules to explain; the `Using` line already shows the result |

## Consequences

- Running in the wrong directory falls back to `~/.ucl`, or uses an unintended installation;
  the `Using` line on stderr is the safeguard (the risk that first rejected the current
  directory).
- Scripts pass `--folder` or set their working directory.

- Dependencies: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`
  (approved by Carl 2026-10-28); `dotnet-ef` local tool (approved by Carl 2026-10-28).
- `secure_delete` makes writes slightly slower; negligible at a small vendor's volume.

## Reversal Cost

Medium: data access is confined to the tool's data layer; a vendor's existing databases would
need a migration path.
