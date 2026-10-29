## ADDED Requirements

### Requirement: Release page for each published release
After every package of a release run is published, the run SHALL create a release page on the
repository's GitHub Releases list, attached to the release tag that started the run. The page
SHALL be titled with the version, SHALL hold the release log note for that version and one
nuget.org link per published package, naming that version, and SHALL be marked as a prerelease
exactly when the version is below 1.0. No other tag SHALL be created. A release attempt that
publishes nothing, and a rehearsal, SHALL create no page.

If the page cannot be created after the packages are published, the run SHALL report the
failure and the packages SHALL stay published. Carl SHALL be able to re-run the release run's
page creation alone, which creates the page without publishing again. When the page for the tag
already exists, creating it SHALL change nothing and SHALL succeed.

#### Scenario: Page created
- **WHEN** tag `release-20261028` publishes `Umbraco.Community.Licensing` `0.1.0-beta.5`, whose
  release log row has the note "Adds package icon"
- **THEN** the Releases list shows a page titled `0.1.0-beta.5` on tag `release-20261028`,
  marked as a prerelease, holding "Adds package icon" and a link to
  `https://www.nuget.org/packages/Umbraco.Community.Licensing/0.1.0-beta.5`

#### Scenario: One link per published package
- **WHEN** a release publishes two packages, `Umbraco.Community.Licensing` and
  `Umbraco.Community.Licensing.Tool`, both `0.2.0-beta.1`
- **THEN** the page holds a nuget.org link for each, both naming `0.2.0-beta.1`

#### Scenario: Release from 1.0
- **WHEN** a release publishes `1.0.0`
- **THEN** its page is not marked as a prerelease

#### Scenario: Failed release has no page
- **WHEN** a release run fails a check or a test and publishes nothing
- **THEN** no page is created for its tag

#### Scenario: Rehearsal has no page
- **WHEN** Carl runs a rehearsal
- **THEN** no page is created

#### Scenario: Page creation fails after publishing
- **WHEN** the packages are published and creating the page fails
- **THEN** the run reports the failure, the packages stay published, and re-running the page
  creation alone creates the page without publishing again

#### Scenario: Page already exists
- **WHEN** page creation runs for a tag that already has a page
- **THEN** no second page is created, the existing page is unchanged, and the step succeeds

#### Scenario: Release log note missing
- **WHEN** page creation runs and the release log at the tagged commit has no row for the
  version
- **THEN** no page is created and the run reports the missing row
