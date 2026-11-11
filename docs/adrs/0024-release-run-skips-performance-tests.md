# ADR-0024: Release run skips the performance tests

- Status: Decided
- Date: 2026-11-11
- Source: `add-vendor-tool` design.md Q81 (follow-ups row 189). Amends ADR-0005, ADR-0008.

## Context

PDR-0060 leaves the timing and memory checks of PDR-0022 out of the release run and keeps them
in the pull-request check. ADR-0005 put those tests in `PerformanceTests` with the trait
`Category=Performance` so they can be filtered on a loaded machine. ADR-0008 runs
`dotnet test --solution ... -c Release --no-build` in `release.yml`, `ci.yml` (GitHub-hosted
`ubuntu-latest`) and `all-platforms.yml`, on Microsoft.Testing.Platform with xUnit (ADR-0006).

## Decision

- The `release.yml` Test step becomes
  `dotnet test --solution Umbraco.Community.Licensing.slnx -c Release --no-build --filter-not-trait "Category=Performance"`.
  The xUnit option leaves out the three `PerformanceTests` tests and runs every other test.
- `ci.yml`, `all-platforms.yml` and the local validation commands are unchanged: they still run
  the performance tests.
- A new performance test carries `Category=Performance`, so the release run leaves it out too.

## Alternatives Considered

| Option | Why not |
|---|---|
| `--max-parallel-test-modules 1` in the release run | Addresses competition between test projects, not a slow machine; the performance collection already runs after the parallel collections |
| Tolerances in the test (e.g. 1.5 ms on the release machine) | Changes what PDR-0022's 1 ms means |
| A separate step that runs only the performance tests on the release machine | Same machine, same failures |
| An environment variable that makes the tests skip themselves | Hides the filter in test code; the trait already exists for this (ADR-0005) |

## Consequences

- The release run executes 3 fewer tests. The run summary shows the total, so a filter that
  matches more than intended is visible.
- The timing gate is the GitHub-hosted pull-request check only. Its machines vary a little from
  run to run; a failure there is investigated, not re-run until green.

## Reversal Cost

Low: one option on one step.
