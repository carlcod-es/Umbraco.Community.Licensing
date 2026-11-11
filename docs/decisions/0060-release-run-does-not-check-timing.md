# PDR-0060: The release run does not check timing

- Status: Decided
- Date: 2026-11-11
- Source: `add-vendor-tool` design.md Q81 (raised by the 13.3 release rehearsal; follow-ups
  row 189; `pre-1.0-considerations.md` C13). Amends PDR-0028.
- Personas: site visitor (the per-request cost is still checked on every change). Implementor: a
  release is still built and tested from the tagged commit; only the timing and memory checks
  are left to the pull-request check. Vendor, site owner, backoffice editor: unaffected.

## Context

PDR-0022 sets the cost of an evaluation: under 1 ms for a typical key, and no memory that grows
with the number of calls. Tests check both. The release run (PDR-0028) runs every test on
whichever of Carl's test machines is available. The release machine is much slower than typical
hardware and may be busy: at the `add-vendor-tool` rehearsal it measured 1.05 ms and 1.16 ms
where Carl's development machine measures about 0.13 ms. A timing failure there says nothing
about the library, yet it fails the release and uses up a version number (PDR-0027).

## Decision

- The release run runs every test except the timing and memory checks of *Evaluation on every
  request* (PDR-0022).
- Those checks run on every pull request and every push to `main` in the pull-request check, and
  in local runs. PDR-0022's targets are unchanged.
- Before releasing, Carl checks that the latest pull-request check on `main` passed.

## Reasons

- PDR-0022's 1 ms means "small next to a page request" on typical hardware, not on any machine
  that happens to run the release.
- The pull-request check runs on the same kind of machine every time, so its timing is
  comparable from one change to the next.
- A failed release costs a version number; a flaky timing check should not be able to cause one.

## Rejected options

| Option | Why rejected |
|---|---|
| Name the hardware in PDR-0022 ("under 1 ms on typical server hardware") | The release machine is below that hardware, so the release run would still have to skip the check |
| Move releases to a faster machine | Any machine can be busy at release time; C13 stays open |
| Leave it, and re-run until it passes | Each failed release uses up a version number |

## Consequences

- A release can be published from a commit whose timing or memory checks failed in the
  pull-request check, if Carl releases without looking. The release command does not check it.
- The release guide's rehearsal steps say the timing tests are not run in a release.
- `pre-1.0-considerations.md` C13 is closed.
