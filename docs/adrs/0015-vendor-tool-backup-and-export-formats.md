# ADR-0015: Vendor tool backup and export formats

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q22

## Context

PDR-0031: backup export to one zip and import into a new folder, refusing anything that is not a
complete backup or comes from a newer tool. PDR-0035: export by signing key to a file readable
by spreadsheet tools.

## Decision

- **Snapshot.** `VACUUM INTO` a temp file: a consistent copy with no free pages, taken without
  stopping other readers.
- **Zip** (BCL `System.IO.Compression`): `manifest.json` (`format` 1, `toolVersion`,
  `lastMigration`, `takenUtc`) and `ucl.db`. Not encrypted.
- **Import checks.** Known `format`; both entries present; `lastMigration` known to the running
  tool; target folder has no `ucl.db`; after extracting, `PRAGMA integrity_check`, then pending
  migrations.
- **Signed-key export.** CSV, RFC 4180, UTF-8, header
  `reference,key_identifier,product,issued_utc,expires_utc`; times ISO 8601 UTC; `perpetual` for
  no expiry.
- Neither export overwrites an existing file.

## Alternatives Considered

| Option | Why not |
|---|---|
| Copy the live database file | Can capture a half-written state |
| SQLite online backup API | Same result as `VACUUM INTO`; keeps free pages |
| Zip of the whole folder | Same content; the folder holds only `ucl.db` |
| JSON export of records | A second format to keep in step with the schema |
| Excel file | A dependency; CSV opens in every spreadsheet tool |

## Consequences

- An older tool cannot import a newer backup; the message says to update.

## Reversal Cost

Low: the manifest's `format` allows a later format beside this one.
