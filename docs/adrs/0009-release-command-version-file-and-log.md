# ADR-0009: Release command, version file and release log

- Status: Decided
- Date: 2026-10-14
- Source: `add-release-pipeline` design.md Q7, Q8. Amends ADR-0006.

## Context

PDR-0027 puts the version line, the current version and the release log in repository files,
and starts a release with a command held in the repository. ADR-0006 typed `Version` by hand in
the library project. Project constraint: scripts and tooling in Node.js, cross-platform.

## Decision

- **Version file**: `Version.props` at the repository root, an MSBuild props file:

  ```xml
  <Project>
    <PropertyGroup>
      <VersionLine>0.1-beta</VersionLine>
      <Version>0.1.0-beta.1</Version>
    </PropertyGroup>
  </Project>
  ```

  The library project imports it (`<Import Project="../../Version.props" />`) and drops its own
  `Version`. Carl edits `VersionLine` by hand; the release command writes `Version`. Local builds
  carry the last released (or about-to-be-released) version.
- **Version form**: three parts everywhere: `0.M.0-beta.N`, `X.N.0`. The feed normalises `1.0`
  to `1.0.0`; PDR-0027's `1.0`, `1.1` are these versions. Accepted lines: `0.M-beta` (M ≥ 1) and
  `X` (X ≥ 1), no leading zeros.
- **Release log**: `RELEASES.md` at the repository root. A fixed heading and one Markdown table,
  newest row first:

  ```markdown
  | Version | Tag | Date | What changed |
  |---|---|---|---|
  | 0.1.0-beta.1 | release-20261020 | 2026-10-20 | First beta |
  ```

  A note is one line; `|` is written as `\|`. The table ends at the first line not starting with
  `|`.
- **Tooling**: Node.js 22 or later, ES modules (`.mjs`), no npm packages, no `package.json`.
  Under `tools/release/`:

  | File | Role |
  |---|---|
  | `release-rules.mjs` | Pure functions: line parsing, next version, beta-status check, tag format and next tag, log parse and row insert, version file read and write, release checks |
  | `release.mjs` | The release command. CLI entry refuses unless stdin and stdout are a TTY, then calls an exported `runRelease` that takes the working directory, a prompt function and a clock |
  | `check.mjs` | Release-run checks (PDR-0027 refusals 1, 3, 4, 5; PDR-0029). Tag from its argument; prints the platform; exit code 1 with every reason on refusal |
  | `*.test.mjs` | `node:test` tests |

  Git is called with `execFileSync('git', [...])`, never through a shell. Run as
  `node tools/release/release.mjs`.
- **Release command flow**: `git fetch origin --tags`; refuse unless on `main`, `main` equals
  `origin/main`, and `Version.props` and `RELEASES.md` have no uncommitted changes; read the line
  and log; work out version and tag (local date, as Carl's release-tag command); refuse a version
  already in the log; ask for the note (re-ask on empty); show version, tag, note; confirm; write
  both files; `git commit --only Version.props RELEASES.md -m "Release <version>"`;
  `git push origin main`; `git tag <tag>` (lightweight); `git push origin <tag>`. A failure after
  the commit stops with the state and the command to finish by hand; nothing is retried or forced.
- **Release-run check of "on main"**: `git merge-base --is-ancestor <tagged commit> origin/main`
  after a full-history checkout (ADR-0008).

## Alternatives Considered

| Option | Why not |
|---|---|
| Version in `Directory.Build.props` | Shared by every project, including tests and the later vendor tool (FR-2) |
| `version.json` | MSBuild cannot read JSON without inline tasks or string parsing |
| Version passed by CI (`-p:Version`) | PDR-0027: the version comes from the file at the tagged commit |
| MinVer or Nerdbank.GitVersioning | Version from tags or commit height, not from a file; a dependency |
| Next number from the version file alone | No row for a new line gives no base; the log already holds every attempt |
| `CHANGELOG.md` (Keep a Changelog sections) | Section format is harder to parse and suggests an "Unreleased" section; the log lists attempts, not changes per area |
| Release command in C# (.NET 10 file-based app) | Project constraint: tooling in Node.js |
| npm test runner or packages | `node:test` is built in; no dependency to approve or update |
| Command refuses a dirty working tree | Unneeded: it commits only its two files |

## Consequences

- Node.js 22 or later is needed on Carl's machine and on each runner (`actions/setup-node` in CI).
- The release command cannot be run by an agent or script: no TTY, refused.
- A commit that changes `VersionLine` is an ordinary commit by Carl before a release.
- ADR-0006's "each later release bumps `Version`" is replaced: the release command writes it.

## Reversal Cost

Low: file formats are read by one module. Renaming either file needs the release command, the
library import and this ADR changed together.
