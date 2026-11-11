# ADR-0023: Vendor tool backup version check

- Status: Decided
- Date: 2026-11-09
- Source: `add-vendor-tool` design.md Q77. Amends ADR-0015.

## Context

PDR-0031 and the vendor-tool-backup spec refuse a backup taken by a newer tool version than the
one running. ADR-0015 checks only that the manifest's `lastMigration` is known to the running
tool, so a backup from a newer tool that made no schema change would be imported.

## Decision

- Import refuses a backup as from a newer tool when either holds:
  - the manifest's `toolVersion` has higher precedence than the running tool's version, by
    SemVer 2.0.0 precedence: major, minor and patch compared as numbers; a release above any of
    its prereleases; prerelease identifiers compared dot by dot (numeric identifiers as numbers
    and below alphanumeric ones, alphanumeric ones ordinally, fewer identifiers lower when all
    before are equal); build metadata ignored;
  - the manifest's `lastMigration` is not a migration the running tool knows.
- The refusal names both versions and says to update the tool, and changes nothing.
- A `toolVersion` that is not a valid SemVer version means the file is not a backup.
- The comparison is written in the tool; no package.

## Alternatives Considered

| Option | Why not |
|---|---|
| `lastMigration` only (ADR-0015 as decided) | Accepts a backup from a newer tool with the same schema, which the spec refuses |
| `toolVersion` only | A manifest from a build with an unexpected version could carry an unknown schema |
| `System.Version` comparison | Cannot read prerelease versions such as `0.2.0-beta.1` |
| NuGet.Versioning package | A dependency for one comparison |

## Consequences

- A tool built from a version lower than the one that took the backup refuses it, even when the
  schema is the same; the vendor updates the tool.
- Tests write a manifest with a higher `toolVersion` and a known `lastMigration`.
- If the running tool's own version is not a valid SemVer version (a local or unversioned
  build), the `toolVersion` comparison is skipped and only the `lastMigration` check applies;
  refusing every backup would make such a build unusable. Release builds carry the
  `Version.props` version, which the release run checks (`ucl --version`).

## Reversal Cost

Low: one check in import; no stored format changes.
