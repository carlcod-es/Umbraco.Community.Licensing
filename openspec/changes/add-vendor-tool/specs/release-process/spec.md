## MODIFIED Requirements

### Requirement: Release run publishes only tested packages
Pushing a release tag SHALL start a release run. The run SHALL take the version only from the
version file at the tagged commit, build and test that commit once, and publish two packages,
the library and the vendor tool, both carrying that version, for every operating system, to the
public package feed only when every check passes. The published packages SHALL carry that
version and name the tagged commit. A release whose checks fail SHALL publish nothing, and the
run SHALL report which check failed. Carl SHALL be able to rehearse a release run on one of his
test machines without a tag: it builds, tests and packages the chosen commit and publishes
nothing.

#### Scenario: Successful release
- **WHEN** the release command pushes tag `release-20261020` on a commit whose version file holds
  `0.1.0-beta.1` and every check passes
- **THEN** the library and the vendor tool, both version `0.1.0-beta.1` built from that commit,
  are published, listed as prereleases

#### Scenario: Tests fail
- **WHEN** one test fails during the release run
- **THEN** nothing is published and the run reports the failing test

#### Scenario: Tool tests fail
- **WHEN** a vendor tool test fails during the release run and every library test passes
- **THEN** neither package is published and the run reports the failing test

#### Scenario: Rehearsal
- **WHEN** Carl starts a rehearsal on `main`
- **THEN** the build, tests and packaging run on one of his test machines and nothing is
  published
