# PDR-0070: The sample demo site has a committed demo administrator

- Status: Decided
- Date: 2026-11-13
- Source: `add-sample-package` design.md Q23. Amends PDR-0067.
- Personas: vendor and implementor (the demo site runs from a clone with one command and no
  setup). Site owner (primary): a site built from the sample could keep a known administrator
  login; the sample is never published, runs on the developer's own machine, and its README
  says the login is demo-only and must be changed, so no real site is given a known login by
  this repository. Backoffice editor, site visitor: unaffected.

## Context

PDR-0067 gives the sample a runnable demo site. As first designed, the developer copied an
example local settings file and typed an administrator email and password before the first run,
so no password was ever committed. Carl judged that step needless friction for a demo that only
ever runs locally (2026-11-13).

## Decision

- **A fixed demo administrator** is committed in the demo site's settings: email
  `demo@acme.example`, password `Acme-Demo-Only-1`. The first run creates it with no setup step.
- **Documented as demo-only.** The sample README gives the login and states that it is for the
  local demo only and must be changed in any site built from the sample.
- A developer who wants their own login can still override it locally without committing it.

## Reasons

- One command from a clone to a logged-in backoffice is what makes the sample quick to try.
- The demo site holds no real data, is never published and listens only on the developer's
  machine, so a known login there exposes nothing.
- The same treatment as the demo signing keys (PDR-0069): public, demo-only, labelled.

## Rejected options

| Option | Why rejected |
|---|---|
| Copy an example local settings file and type a login (the earlier design) | A manual step before the first run |
| The start command generates a random password and prints it once | Carl preferred a known login; a missed print means reading a settings file |
| The start command prompts for a login | Interactive; awkward for scripted runs |

## Consequences

- The repository holds a demo administrator password; the README and the settings file say it
  is demo-only.
- The "no administrator password committed" check is replaced by a check that the login is
  documented as demo-only.
