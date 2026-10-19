# PDR-0028: Test coverage per release

- Status: Decided
- Date: 2026-10-14
- Source: FR-6 explore (Carl, 2026-10-14); `add-release-pipeline` design.md Q2
- Personas: vendor (told plainly what each release was tested on). Implementor: may develop on
  one operating system and deploy to another, and is told the same. Site owner protected: betas
  are not for paying customers (PDR-0024), and all three operating systems are checked before
  1.0, when keys reach paying customers.

## Context

The library had only been tested on macOS. One package serves every operating system, but
signing and verification rely on each operating system's own cryptography, so behaviour could
differ between them (`add-license-core` follow-ups, section 3). The README said nothing about
which operating systems are tested.

## Decision

- Every release is built and tested once, on whichever test machine is available, and the one
  package is published for every operating system. This holds before and after 1.0.
- Before the 1.0 release, the full test suite is run once on each of Windows, Linux and macOS.
- The README states the promise in one line:
  - before 1.0: "Runs on Windows, Linux and macOS. Each release is tested on one of them; all
    three will be checked before 1.0."
  - from 1.0: "Runs on Windows, Linux and macOS. Each release is tested on one of them; all
    three were checked before 1.0."
- Testing on more operating systems per release may be added later without changing any other
  part of releasing.

## Reasons

- One build serves every operating system, so building per operating system adds nothing.
- Testing once per release is enough for Carl's needs. A full check across all three before 1.0
  covers the point at which the key format becomes permanent (PDR-0024).
- Vendors decide whether to ship a library based on what was checked. Stating it plainly follows
  PDR-0024's approach: state the risk and let them decide.

## Rejected options

| Option | Why rejected |
|---|---|
| Every release tested on Windows, Linux and macOS | Carl chose a single test run per release |
| Every release tested on Windows and Linux; macOS not checked | Carl chose whichever test machine is available |
| Windows and Linux confirmed automatically, macOS confirmed by Carl by hand | A manual step at every release |
| A one-time check on all three before the first beta | Carl chose before 1.0; betas are not for paying customers |
| README says nothing about operating systems | Vendors may assume every operating system is tested at every release |

## Consequences

- Accepted risk: a break that shows on only one operating system can ship if that release was
  tested on another one.
- Accepted risk: betas may be published before the library has ever been tested on Windows or
  Linux.
- The README line changes at 1.0.
- The pre-1.0 check on all three operating systems is a step in the 1.0 release.
