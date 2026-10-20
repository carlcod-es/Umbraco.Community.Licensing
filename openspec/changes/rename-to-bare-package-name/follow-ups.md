# Follow-ups: rename-to-bare-package-name

| # | Source | Severity | Item | Disposition |
|---|---|---|---|---|
| 1 | Section 2 supervisor | minor | Before the next release Carl confirms on nuget.org that `Umbraco.Community.Licensing` is still unclaimed and the Trusted Publishing policy (owner `Carlcod-es`, repo, `release.yml`) is active. The policy is not tied to a package ID; no repo change. | Carl, before release |
| 2 | Section 2 supervisor | minor | `add-release-pipeline` task 7.2 names `.Core` `0.1.0-beta.2`. | Task 3.3 |
| 3 | Section 1 supervisor | nit | Other local clones keep untracked `src/Umbraco.Community.Licensing.Core/{bin,obj}` after pulling; delete by hand. | Note for Carl |
| 4 | Section 2 supervisor | nit | Push glob `*.nupkg` does not match `.snupkg`; predates this change, no symbols package today. | Deferred |
