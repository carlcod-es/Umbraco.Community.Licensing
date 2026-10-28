# ADR-0016: GitHub Release job in the release run

- Status: Decided
- Date: 2026-10-28
- Source: `add-github-release` design.md Q3, Q5, Q6. Amends ADR-0008.

## Context

PDR-0037 adds a GitHub Release after publishing, re-runnable without publishing again.
`release.yml` is one job on a self-hosted runner with `contents: read, id-token: write`
(ADR-0008). Creating a Release needs `contents: write`. Release tooling is Node.js with no npm
packages (ADR-0009).

## Decision

- **Second job.** `release.yml` gains a job `github-release`: `needs: release`, runs only on a
  tag push, `runs-on: ubuntu-latest` (GitHub-hosted). Job-level permissions `contents: write`
  only. The `release` job keeps `contents: read, id-token: write`; workflow-level permissions
  drop to `contents: read`, and each job states its own.
- **Package list.** After the push, the `release` job runs
  `node tools/release/github-release.mjs package-ids <pack folder> <version>` and exposes the
  space-separated package IDs as job output `package-ids`. IDs come from the pushed file names,
  so the vendor tool's package (ADR-0011) is listed with no change here.
- **Create.** The `github-release` job checks out the tag (no full history, no persisted
  credentials), sets up Node 22 and runs
  `node tools/release/github-release.mjs create <tag> <package-ids>`. The script reads the
  version from `Version.props` and the note from the `RELEASES.md` row (`release-rules.mjs`
  parsing), builds the body, and calls the GitHub REST API with `fetch` and `GITHUB_TOKEN`:
  `GET /repos/{repo}/releases/tags/{tag}`; on 404, `POST /repos/{repo}/releases` with
  `tag_name`, `name` (version), `body`, `prerelease` (version below 1.0), `make_latest`
  (`false` for a prerelease). An existing Release is left unchanged and the step succeeds. Any
  other response fails the step with the status and GitHub's message.
- **Re-run.** A failed `github-release` job is re-run with **Re-run failed jobs**, which re-runs
  only that job; the push is not repeated. The push keeps no `--skip-duplicate` (ADR-0008).
- **Dependencies.** None new: `actions/checkout` and `actions/setup-node` at the SHAs already
  pinned.

## Alternatives Considered

| Option | Why not |
|---|---|
| A step in the `release` job with `contents: write` | Write access for the whole job, including build and tests on Carl's machine; `actions/checkout` persists the token in `.git/config` by default |
| `gh release create` | `gh` is not a stated prerequisite of the self-hosted runner; on hosted runners it would work, but the body and idempotency logic would live in shell, which differs across runner operating systems and is untested |
| `softprops/action-gh-release` or similar action | A new third-party dependency with write access; logic not covered by the tool tests |
| `--skip-duplicate` on push, so the whole run can be re-run | Reverses ADR-0008: a republish of a released version would pass silently. Re-running one job gives the same outcome |
| Package IDs fixed in the script | `add-vendor-tool` would have to change the script as well as the workflow |
| Raise the repository's default workflow permissions to read and write | Disabled by the organisation's read-only default (Carl, 2026-10-28), and not needed: a job's `permissions` key grants write for that job only |
| Fine-grained personal access token or GitHub App token as a secret | A long-lived secret to store and rotate; fallback only if job-level `contents: write` is ever refused |
| Release created by hand, or by the release command after the run | Not automatic; the release command would have to wait for the run |
| Self-hosted runner for the Release job | No build happens in it; a hosted runner keeps the write token off Carl's machines |

## Consequences

- A tag run uses a GitHub-hosted runner for a short job; free for a public repository.
- "Re-run all jobs" on a published release fails at the push (duplicate) and skips the Release
  job. `docs/releasing.md` says to use **Re-run failed jobs**.
- The Release job runs the script from the tagged commit: a fix to the script needs a new
  release.
- A Release created with `GITHUB_TOKEN` starts no other workflow. None listens for it.

## Reversal Cost

Low: one job and one script. Removing it leaves existing Releases in place.
