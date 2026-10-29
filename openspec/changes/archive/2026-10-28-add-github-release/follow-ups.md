# Follow-ups: add-github-release

Minor and nit findings from reviews. Read at the start of each apply session.

## Section 1 (tasks 1.1 to 1.3), reviewer, 2026-10-28

- Minor, `tools/release/github-release.test.mjs:139-164`: tag URL-encoding in the GET path is untested (`release-yyyymmdd` needs none). Add a case with tag `a/b` asserting `a%2Fb`.
- Minor, `tools/release/github-release.mjs:88,103,109`: a rejected `fetch` or an unparseable 201 body escapes without saying which request failed. Prefix "Looking up…" / "Creating…".
- Minor, `tools/release/github-release.mjs:84`: an unreadable `RELEASES.md` in `create` gives a raw ENOENT; use `check.mjs` wording ("Cannot read RELEASES.md at the repository root"). `Version.props` fixed in the section 1 rework.
- Nit, `tools/release/github-release.test.mjs`: `main()` usage/argument errors and the `create` repo-root path are not exercised through the CLI.
- Nit, `tools/release/github-release.mjs:57-62`: a JSON error body without a string `message` shows the raw JSON (matches the raw-text fallback contract).

## Section 1 rework (tasks 1.2, 1.3), reviewer, 2026-10-28

- Nit, `tools/release/github-release.mjs:131-136`: outside a git checkout, `repoRoot` surfaces git's raw error (same as `check.mjs`).
- Nit: `readVersion` reports a malformed `Version.props` as "Cannot read Version.props…" (same pattern as `check.mjs`).

## Section 2 (task 2.1), reviewer, 2026-10-28

- Nit, `.github/workflows/release.yml:19-20`: `release` job repeats `contents: read` from the workflow level; kept, ADR-0016 has each job state its own.

## Section 2, supervisor, 2026-10-28

- Minor, `.github/workflows/release.yml:64-67`: a **Package IDs** failure after a successful push fails the `release` job; **Re-run failed jobs** then fails at the duplicate push, so the Release cannot be created by re-run (breaks PDR-0037 re-run for this step). Unlikely (`runner.temp` emptied per job). Fix (a): move the step before **NuGet login** with no `if` (`needs: release` already gates the Release job; rehearsals then exercise it); needs design.md, ADR-0016, task 2.1 update. Fix (b): document manual recovery in `docs/releasing.md`.
- Minor, `.github/workflows/release.yml:85` (and `:67`): `${{ github.ref_name }}` and `${{ needs.release.outputs.package-ids }}` are interpolated into the script in the job holding the write token; a crafted tag could inject shell. Only pushers can tag. Fix: pass via `env` (`TAG`, `IDS`) and use `"$TAG" "$IDS"`. Same pattern at line 40 (`check.mjs`, self-hosted, shell varies by OS) is out of scope.
- Note for `add-vendor-tool` section 11 brief: keep **Package IDs**, the job output and `github-release`; the install-and-run step must not write a `.nupkg` into the pack folder; a partial two-package push cannot be re-run; that row goes into `docs/releasing.md` in `add-vendor-tool`, not here (section 3 reviewer, 2026-10-28).

The two section 2 supervisor minors above were taken into `add-github-release` task 2.2 (Carl, 2026-10-28).

## Final review, supervisor, 2026-10-28

Fixed before archive: beta.5/beta.6 drift in proposal.md, design.md and PDR-0037; ADR-0008 permissions line. Open, for later changes:

- `add-vendor-tool` via `opsx:update` after this archive: task 11.1 and design.md must keep **Package IDs** before **NuGet login**, the `package-ids` job output and job `github-release`; the install-and-run step writes no `.nupkg` into the pack folder; a row in `docs/releasing.md` for a partial two-package push; refresh its design.md "`0.1.0-beta.4` is on nuget.org" line.
- `${{ github.ref_name }}` in **Release check** (`release.yml:40`, self-hosted) still interpolated into the command; pass through `env` in a later CI change.
