# Follow-ups: rename-to-bare-package-name

| # | Source | Severity | Item | Disposition |
|---|---|---|---|---|
| 1 | Section 2 supervisor | minor | Before the next release Carl confirms on nuget.org that `Umbraco.Community.Licensing` is still unclaimed and the Trusted Publishing policy (owner `Carlcod-es`, repo, `release.yml`) is active. The policy is not tied to a package ID; no repo change. | Carl, before release |
| 2 | Section 2 supervisor | minor | `add-release-pipeline` task 7.2 names `.Core` `0.1.0-beta.2`. | Done (task 3.3) |
| 3 | Section 1 supervisor | nit | Other local clones keep untracked `src/Umbraco.Community.Licensing.Core/{bin,obj}` after pulling; delete by hand. | Note for Carl |
| 4 | Section 2 supervisor | nit | Push glob `*.nupkg` does not match `.snupkg`; predates this change, no symbols package today. | Deferred |
| 5 | Task 4.1 | minor | ADR-0005 line 51: the example-key generator command still names `tests/Umbraco.Community.Licensing.Core.Tests`. ADR-0005 is amended by ADR-0010, but the command is copied as is. Fix: append "(Amended 2026-10-20, ADR-0010: project `tests/Umbraco.Community.Licensing.Tests`.)" with the corrected command. | Done: amendment note with corrected command |
| 6 | Sections 3–4 supervisor | minor | `add-release-pipeline` tasks.md section 7 "Depends on" and task 7.2 do not require `rename-to-bare-package-name` merged to `main`; releasing before the merge would use `0.1.0-beta.3` as `.Core` (PDR-0027: not reused). Fix: add the merge to section 7 "Depends on" via `opsx:update`. | Done: section 7 "Depends on" names the rename merge |
| 7 | Sections 3–4 supervisor | nit | `0.1.0-beta.2` outcome worded "failed before publishing" (`add-release-pipeline` proposal.md) and "attempted as `.Core`, not published" (design.md, tasks.md). Fix: one wording once Carl confirms whether the run failed or was dropped. | Done: "attempted as `.Core`, not published" throughout |
