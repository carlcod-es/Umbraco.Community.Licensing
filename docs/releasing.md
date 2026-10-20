# Releasing

How `Umbraco.Community.Licensing` is released to nuget.org. Only Carl starts a release.
Decisions: PDR-0027 (versioning, log, refusals), PDR-0028 (test coverage), PDR-0029 (tag must
match the log), ADR-0008 (CI, runners, publishing), ADR-0009 (release command and files).

## How it fits together

| Piece | Where | Does |
|---|---|---|
| `Version.props` | Repository root | `VersionLine` (Carl edits) and `Version` (the release command writes) |
| `RELEASES.md` | Repository root | One row per release attempt, newest first. Rows added only by the release command |
| Release command | `node tools/release/release.mjs` | Works out the next version and tag, asks for a note, commits both files, pushes `main`, pushes the tag |
| `ci.yml` | GitHub-hosted `ubuntu-latest` | Tool tests, build, test on every pull request to `main` and push to `main` |
| `release.yml` | Any self-hosted runner available to this repository | On a `release-*` tag: checks, tool tests, build, test, pack, publish. On manual dispatch: rehearsal, publishes nothing |
| `all-platforms.yml` | GitHub-hosted Windows, Linux, macOS | Disabled. See [Pre-1.0 all-platform step](#pre-10-all-platform-step) |

The package version comes only from `Version.props` at the tagged commit. One job builds, tests,
packs and pushes, so the published package is the tested one.

## One-time setup

### Release runner

Any online self-hosted runner available to this repository takes a release job (ADR-0008). The
current runner is the organisation-wide runner MINIDEV. Every self-hosted runner the repository
can use is a release machine: limit the runner group's repository access to machines you trust
to publish.

1. In the `carlcod-es` organisation: **Settings → Actions → Runner groups**. The runner's group
   must allow `Umbraco.Community.Licensing`. If the group is limited to private repositories,
   tick **Allow public repositories**.
2. Runner prerequisites:
   - A runner version that runs `node24` actions. All four pinned actions use it. Keep the
     runner's auto-update on.
   - Git on the `PATH` of the account the runner service runs as.
   - A .NET install directory that account can write. `actions/setup-dotnet` installs the SDK
     named by `global.json` into the machine-wide directory by default, which a service account
     (Windows: `NETWORK SERVICE`) usually cannot write. Add
     `DOTNET_INSTALL_DIR=<writable directory>` to the `.env` file in the runner's folder and
     restart the runner service.
   - Node.js is installed by `actions/setup-node`; nothing to preinstall.

### Fork pull requests

Repository **Settings → Actions → General → Approval for running fork pull request workflows
from contributors**: **Require approval for all external contributors**. A fork pull request
runs its own copy of the workflow files and could retarget a job to the self-hosted label. Read
every workflow change in a pull request before approving its run.

### nuget.org Trusted Publishing

1. Sign in to nuget.org as `CarlCod_es`.
2. **Trusted Publishing → Create policy**:

   | Field | Value |
   |---|---|
   | Policy owner | organisation `Carlcod-es` |
   | Repository owner | `carlcod-es` |
   | Repository | `Umbraco.Community.Licensing` |
   | Workflow file | `release.yml` |
   | Environment | empty |

3. Repository **Settings → Secrets and variables → Actions → Variables**: add `NUGET_USER` with
   value `CarlCod_es`, the user who created the policy.

No API key is stored. Each release run exchanges its OIDC token for a short-lived key. Renaming
`release.yml` breaks publishing until the policy is updated.

### Tag ruleset (optional)

Any writer can push a `release-*` tag; the release checks are the gate. To limit who can start a
release: repository **Settings → Rules → Rulesets → New ruleset → New tag ruleset**, target
`release-*`, enable **Restrict creations**, enforcement **Active**, and add Carl (repository
admin) to the bypass list. This also keeps unexpected tag names out of the release run.

## Rehearsal

Run before the first release, after changing the runner, and whenever a release run is in doubt.

1. **Actions → Release → Run workflow**, branch `main`.
2. Check the run:
   - **Print platform** names the runner machine's operating system.
   - Tool tests and the .NET tests pass.
   - **Pack** produces `Umbraco.Community.Licensing.<version>.nupkg`.
   - **Release check**, **NuGet login** and **Push to NuGet** are skipped.

A rehearsal never publishes, whichever branch or ref it runs on: the publish steps run only on a
tag push. `workflow_dispatch` lists only workflows on `main`.

## Releasing

Before you start:
- On `main`, in step with `origin/main` (pulled and pushed).
- No uncommitted changes in `Version.props` or `RELEASES.md`. Other uncommitted files are left
  alone: the command commits only its two files.
- Node.js 22 or later, in an interactive terminal. With no terminal attached the command refuses.
- A release runner online, or the run queues.

Run from anywhere in the repository:

```
node tools/release/release.mjs
```

The command:
1. fetches `origin` and its tags, and checks the conditions above;
2. works out the next version from the line and the log, and the tag `release-yyyymmdd` (local
   date; `-2`, `-3` for further releases that day, counting remote tags);
3. asks **What changed (one line)?** Write it for vendors: say when a beta changes the key format
   or the public API, since keys then need reissuing (PDR-0024);
4. shows version, tag and note, and asks to confirm. Anything but `y` or `yes` prints
   `Declined. Nothing changed.`;
5. writes `Version.props` and the `RELEASES.md` row, commits them as `Release <version>`, pushes
   `main`, creates the tag and pushes it.

The tag push starts `release.yml`. Watch it in **Actions → Release**. The release is done when
the run passes and nuget.org lists the version as a prerelease (before 1.0).

Never push a package built on your own machine. A local pack carries the released version but is
not the tested build from the tagged commit.

## Changing the version line

`VersionLine` in `Version.props` is the only thing edited by hand. Change it in an ordinary
commit, push it, then release.

| Line | Versions |
|---|---|
| `0.M-beta` (M ≥ 1) | `0.M.0-beta.1`, `0.M.0-beta.2`, ... |
| `X` (X ≥ 1) | `X.0.0`, `X.1.0`, `X.2.0`, ... |

- Going to 1.0: change the line to `1` (PDR-0024). Do the [pre-1.0 all-platform
  step](#pre-10-all-platform-step) first, and change the README test-coverage line to the
  from-1.0 wording in PDR-0028.
- Breaking API change after 1.0: change the line to the next major version. Forgetting ships it
  as a minor release.
- No leading zeros. Numbering on a new line starts at its first version.

## Refusals

### Release command

Nothing is written unless the message says so.

| Message starts with | Cause | Fix |
|---|---|---|
| `No person present` | No interactive terminal | Run it yourself in a terminal |
| `Not on main` | Another branch or a detached HEAD | `git switch main` |
| `Out of step with the remote` | Local `main` differs from `origin/main` | Pull or push as the message says |
| `Uncommitted changes in` | `Version.props` or `RELEASES.md` edited | Commit or discard them |
| `Version line '...' is not valid` | `VersionLine` is not `0.M-beta` or `X` | Fix the line in a commit |
| `Version '...' is already in the release log` | The next version already has a row | The log was edited by hand; fix it in a commit |
| `Release log ...` | `RELEASES.md` table damaged | Restore the table format |
| `Cancelled. Nothing changed.` | Input closed (Ctrl-D) at a prompt | Run again |
| `git fetch origin --tags failed ... would clobber existing tag` | A local tag differs from the remote tag of the same name | Delete the local tag (`git tag -d <tag>`) and run again. Nothing was written |

If the commit itself fails, both files are written but not committed, and the message gives the
`git checkout` command to discard them.

If a step after the commit fails (push of `main`, creating or pushing the tag), the command stops
and prints the state and the exact `git` commands to finish by hand. Nothing is retried or
forced. Until the tag is on the remote the release has not started and nothing is published.

### Release run

The **Release check** step prints `Release refused for tag '<tag>':` and every reason. The run
stops before building; nothing is published.

| Reason | Cause |
|---|---|
| `Tag '...' does not match 'release-yyyymmdd'` ... | Tag not made by the release command |
| `Version line '...' is not valid` / `Version '...' ... is not of the form` | `Version.props` edited by hand |
| `Commit ... is not on main.` | Tagged commit is not on `origin/main` |
| `Version '...' appears N times in the release log.` | Duplicate row, edited by hand |
| `Version '...' is below 1.0 but is not a prerelease` / `is 1.0 or above but is a prerelease` | Version and line disagree, edited by hand |
| `Version '...' does not belong to version line '...'` | Version and line disagree, edited by hand |
| `The release log has no row for version '...'` | Tag pushed without the release command, for example by Carl's shared release-tag command (PDR-0029) |
| `The release log row for version '...' names tag '...', but the pushed tag is '...'` | Same as above, on a version already released |
| Any other reason | The message names the file (`Version.props`, `RELEASES.md`) or the `git` command that failed. Repair the file in a commit, or the checkout, and release again |

A refused tag stays on the remote. Delete it (`git push origin :refs/tags/<tag>` and
`git tag -d <tag>`) and start a release with the release command.

## Failed releases and re-runs

| What happened | What to do |
|---|---|
| Build or tests failed | A failed release (PDR-0027). Its log row stays and its number is not reused. Fix the cause in a commit, then run the release command again; it takes the next number. Do not re-run the run |
| Release check refused | See [Release run](#release-run) refusals |
| Job queued, no runner online | GitHub fails it after 24 hours. Bring a runner online and re-run the run: same tag, same version |
| Run failed before its tests, in a step other than **Release check** (checkout, toolchain setup) | Fix the runner and re-run the run. A re-run uses the workflow files at the tagged commit: if the fix is a workflow change, commit it and release again with the release command |
| Tests passed, **NuGet login** or **Push to NuGet** failed, and nuget.org does not list the version | Fix the cause (policy, `NUGET_USER`) and re-run the run. It rebuilds and retests the same commit (ADR-0008) |
| nuget.org refuses the very first push of the package ID under Trusted Publishing | Publish that version once from a short-lived API key scoped to the package, then delete the key; record it in the change's `follow-ups.md` |

Re-running a tag run re-checks against the current `origin/main`. Commits added to `main` since
do not affect it: the tagged commit is still on `main`, so the check still passes.

A published version cannot be deleted from nuget.org, only unlisted.

## Pre-1.0 all-platform step

PDR-0028: before the 1.0 release, the full suite runs once on each of Windows, Linux and macOS.
Carl runs it by hand on his own machines. `all-platforms.yml` is disabled (`if: false` on its job,
Carl, 2026-10-18): dispatching it gives a run with a skipped job, not a check. To re-enable it,
delete the `if: false` line.

On each operating system, from a clean checkout of the commit to be released as 1.0:

```
node tools/release/platform.mjs
node --test "tools/release/*.test.mjs"
dotnet build Umbraco.Community.Licensing.slnx -c Release
dotnet test --solution Umbraco.Community.Licensing.slnx -c Release --no-build
```

Record one result per operating system in the `follow-ups.md` of the change that goes to 1.0:
the platform line, and pass or fail with any failing test names. This closes the
`add-release-pipeline` "All-platform check" scenario and the open Windows and Linux follow-ups
from `add-license-core` (`add-release-pipeline` `follow-ups.md` item 31). Then change the README
line and the version line (see [Changing the version line](#changing-the-version-line)).
