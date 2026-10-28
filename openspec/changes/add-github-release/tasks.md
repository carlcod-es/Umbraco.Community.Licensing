Owners per CLAUDE.md: sections 1 and 2 worker-backend; section 3 Architect; section 4 Carl.
Validation for every worker block: `node --test "tools/release/*.test.mjs"`,
`dotnet build Umbraco.Community.Licensing.slnx -c Release`. Bar: production (design.md). Test
names follow spec scenario names. Spec: `specs/release-process/spec.md`. Records: PDR-0037,
ADR-0016, ADR-0008, ADR-0009.

## 1. GitHub Release script

Depends on: none. Owner: worker-backend. Path: `tools/release/github-release.mjs` and
`tools/release/github-release.test.mjs`. Node 22+, ES modules, no npm packages; reuse
`release-rules.mjs` parsing.

- [ ] 1.1 Implement the pure functions: package IDs from `.nupkg` file names and a version (refusing an empty list or a name not ending `.<version>.nupkg`), the Release body (note, blank line, one nuget.org link per ID naming the version), and prerelease from the version, and verify by tests for "Page created", "One link per published package", "Release from 1.0", a dotted package ID and a mismatched file name
- [ ] 1.2 Implement the `package-ids <folder> <version>` command, printing the IDs and appending `package-ids=<ids>` to `$GITHUB_OUTPUT` when set, and verify by a test running it against a temp folder with and without `GITHUB_OUTPUT`
- [ ] 1.3 Implement the `create <tag> <ids>` command: read `Version.props` and the `RELEASES.md` row for the version, GET the Release for the tag, POST it on 404 with `tag_name`, `name`, `body`, `prerelease` and `make_latest`, leave an existing Release unchanged, and fail with status and GitHub's message on any other response; `fetch` injectable; and verify by tests for "Page already exists", "Release log note missing", the 404-then-POST payload, and a 403 reported with its message

## 2. Release workflow

Depends on: 1. Owner: worker-backend. Shared file assigned in the brief:
`.github/workflows/release.yml`. Records: ADR-0016, ADR-0008.

- [ ] 2.1 Move permissions to job level (`release`: `contents: read, id-token: write`; workflow level `contents: read`), add the `package-ids` step after **Push to NuGet** (tag push only, in the pack folder) and the `release` job output, and add the `github-release` job (`needs: release`, tag push only, `ubuntu-latest`, `contents: write`, checkout with `persist-credentials: false`, setup-node 22 at the pinned SHAs, `create` with `GITHUB_TOKEN` and `github.ref_name`), and verify by reading the workflow against ADR-0016 and running `package-ids` locally on a `dotnet pack` output folder; the live run is verified in section 4

## 3. Documentation

Depends on: 2. Owner: Architect.

- [ ] 3.1 Update `docs/releasing.md`: the Release in "How it fits together", the `release.yml` row and the release steps; a one-time setup note that the read-only default for workflow permissions stays and the job grants its own `contents: write`; rehearsal expects `github-release` skipped; a row in "Failed releases and re-runs" for a failed Release job (use **Re-run failed jobs**; "Re-run all jobs" fails at the push), and verify every statement against ADR-0016 and the workflow
- [ ] 3.2 Update the FR-10 entry in `docs/feature-requests.md` to closed by `add-github-release` once section 4 passes, and verify the index row and the entry agree

## 4. Verification

Depends on: 2, 3. Owner: Carl.

- [ ] 4.1 Run a rehearsal, and verify it passes with `github-release` skipped ("Rehearsal has no page")
- [ ] 4.2 Release `0.1.0-beta.5` with the release command, and verify the Releases page shows `0.1.0-beta.5` on its tag, marked prerelease, with the note and the nuget.org link, and nuget.org shows the package icon ("Page created")
