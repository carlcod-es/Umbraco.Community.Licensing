# ADR-0008: CI host, runners and package publishing

- Status: Decided
- Date: 2026-10-14
- Source: `add-release-pipeline` design.md Q3, Q4, Q6, Q9, Q10, Q12
- Revised: 2026-10-18 (Carl): `all-platforms.yml` disabled; Q6. 2026-10-19 (Carl): re-run after a
  publish failure; Q12

## Context

The repository is public on GitHub, in Carl's organisation `carlcod-es` (transferred from his
personal account by Carl, 2026-10-14, before the first release). PDR-0027 publishes from a `release-yyyymmdd` tag on `main`;
PDR-0028 tests each release once on whichever test machine is available, and the full suite on
Windows, Linux and macOS once before 1.0. Pull requests may come from anyone. Tests run on
Microsoft.Testing.Platform and need `dotnet test --solution` (ADR-0006).

## Decision

- **Repository location**: `https://github.com/carlcod-es/Umbraco.Community.Licensing`. Runners,
  policy and variables below are set up on it, and the first package names it.
- **CI host**: GitHub Actions. Three workflows under `.github/workflows/`:

  | Workflow | Trigger | Runner | Does |
  |---|---|---|---|
  | `ci.yml` | `pull_request` to `main`, `push` to `main` | GitHub-hosted `ubuntu-latest` | Tool tests, build, test |
  | `release.yml` | `push` of tags `release-*`; `workflow_dispatch` (rehearsal) | Self-hosted, labels `self-hosted`, `licensing-release` | Checks, tool tests, build, test, pack, publish (tag only) |
  | `all-platforms.yml` | `workflow_dispatch` | GitHub-hosted matrix `windows-latest`, `ubuntu-latest`, `macos-latest`, `fail-fast: false` | Tool tests, build, test; publishes nothing. **Disabled** (Carl, 2026-10-18): `if: false` on the job, with a comment saying it was disabled on purpose |

- **Runners** (Carl, 2026-10-14): pull requests on GitHub-hosted runners; releases on Carl's own
  machines registered as self-hosted runners with the label `licensing-release`. Any online
  machine with the label takes the job ("whichever test machine is available", PDR-0028). Every
  workflow prints the runner's operating system, version and architecture before testing.
- **Pull request trust**: fork pull requests run on GitHub-hosted runners with a read-only token
  and no secrets. The repository setting "Require approval for all external contributors" is on,
  because a fork pull request runs its own copy of the workflow files and could retarget a job
  to the self-hosted label. Carl reads any workflow change in a pull request before approving
  its run.
- **Publishing**: nuget.org Trusted Publishing (Carl, 2026-10-14). `release.yml` has
  `permissions: { contents: read, id-token: write }`; the `NuGet/login` action exchanges the
  run's OIDC token for a short-lived API key, used once by `dotnet nuget push` to
  `https://api.nuget.org/v3/index.json`. No `--skip-duplicate`: a duplicate must fail the run.
- **Package owner on nuget.org** (Carl, 2026-10-14): the nuget.org organisation `Carlcod-es`,
  created for this purpose. Carl signs in as his nuget.org user `CarlCod_es` and creates the
  Trusted Publishing policy with owner `Carlcod-es`, GitHub repository owner `carlcod-es`,
  repository `Umbraco.Community.Licensing`, workflow `release.yml`. The repository variable
  `NUGET_USER` holds `CarlCod_es`, the user who created the policy.
- **Release job order**: checkout with full history and tags; release checks (PDR-0027 refusals
  1, 3, 4, PDR-0029, and "on `main`" by `git merge-base --is-ancestor` against `origin/main`);
  tool tests; `dotnet build -c Release -p:ContinuousIntegrationBuild=true`;
  `dotnet test --solution ... -c Release --no-build`; `dotnet pack -c Release --no-build`;
  login and push. Checks and push run only on a tag push; a `workflow_dispatch` rehearsal builds,
  tests and packs on a release machine and publishes nothing. One job, so the package pushed is
  the one tested. `concurrency: release`, not cancelled in progress.
- **Toolchain**: `actions/setup-dotnet` with `global-json-file: global.json`; `actions/setup-node`
  at Node 22. Run steps are single `dotnet` or `node` commands, so they work in the default shell
  on every runner operating system.
- **Action dependencies**: `actions/checkout`, `actions/setup-dotnet`, `actions/setup-node`,
  `NuGet/login`. Each pinned to a full commit SHA with its version in a comment; latest stable
  release at least two weeks old at apply time. Approved by Carl with this change.
- **Workflow permissions**: `contents: read` everywhere; `id-token: write` only in `release.yml`.

## Alternatives Considered

| Option | Why not |
|---|---|
| GitHub-hosted runners for releases too | Recommended; Carl chose his own machines for releases |
| Self-hosted runners for pull requests | Fork pull requests would run code on Carl's machines |
| Azure Pipelines | A second service and project for a GitHub repository; Carl's other repositories use tag-triggered GitHub Actions |
| Publish from Carl's machine with a local script | No clean checkout guaranteed; the API key would live on the machine |
| nuget.org API key as a repository secret | A long-lived secret to store and rotate (max 365 days) |
| `--skip-duplicate` on push | Hides a republish of a released version as success |
| Separate build and publish jobs | The package would cross jobs as an artifact; one job keeps tested and pushed the same file |
| All-platform matrix on every release | PDR-0028 chose one test run per release |
| Leave the all-platform check to the 1.0 change | Carl chose to build it now (2026-10-14); disabled 2026-10-18, so the `add-license-core` Windows/Linux follow-ups close on Carl's manual run instead |
| Delete `all-platforms.yml` | Carl chose to keep it, disabled, so it can be re-enabled by removing one line |
| Actions pinned by tag | A moved tag changes the code the release runs |
| Package owned by Carl's nuget.org user `CarlCod_es` | Carl created the organisation `Carlcod-es` to own it |
| Keep the repository under Carl's personal account | Carl chose his organisation |
| Move after the first release | The first package would name the old URL, kept only by GitHub's redirect |

## Consequences

- The release tooling's tests (ADR-0009), which drive real Git in temporary repositories, run on
  Linux in `ci.yml` and on Carl's runner at each release. With `all-platforms.yml` disabled, they
  run on Windows and macOS only when Carl runs the suite by hand (Carl, 2026-10-18). Re-enabling
  the workflow is deleting its `if: false` line.
- Manual all-platform runs use Carl's own machines: Windows, a Linux machine and macOS (Carl,
  2026-10-19). Dispatching `all-platforms.yml` while disabled gives a run with a skipped job, not
  a check.
- A release needs one of Carl's runner machines online. A queued job waits and GitHub fails it
  after 24 hours. Re-running a release run that never reached its tests is allowed (same tag,
  same version); a run that failed its tests is a failed release (PDR-0027), and the next release
  takes a new number. A run whose tests passed but whose NuGet login or push failed may be re-run
  (same tag, same version) while nuget.org does not list the version: nothing was published, and
  the re-run rebuilds and retests the same commit (Carl, 2026-10-19).
- Self-hosted runners keep state between jobs (tool cache, NuGet cache); `actions/checkout` cleans
  the working copy.
- Trusted Publishing policies on nuget.org are set by Carl outside the repository.
  Renaming `release.yml` breaks publishing until the policy is updated.
- GitHub redirects the old URL; other clones of the repository switch their remote to the new
  URL.
- SourceLink in the .NET SDK records the repository and commit in the package;
  `ContinuousIntegrationBuild` makes paths deterministic.

## Reversal Cost

Low: workflow files and repository settings. Moving releases to hosted runners is a one-line
`runs-on` change.
