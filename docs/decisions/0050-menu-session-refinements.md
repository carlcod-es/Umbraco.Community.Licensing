# PDR-0050: Menu session refinements

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q62 to Q65. Amends PDR-0039, PDR-0047, PDR-0048,
  PDR-0049.
- Personas: vendor (a declined switch keeps the session; a hidden session cannot hang; one
  passphrase entry from setup to the first product; a display name or vendor tag can be
  dropped without a new license). Site owner (primary): unaffected in what keys carry, except
  that a reissue may now drop a display name or vendor tag the vendor chooses to drop.
  Implementor, backoffice editor, site visitor: unaffected.

## Context

The `opsx:update` Architect pass for the menu session (PDR-0048) found four places where the
settled behaviour did not fit the session or was not stated: a declined switch confirmation
still ended the tool with a failure code; a person was judged present by input alone, so a
session with its output redirected waited with its questions hidden; setup asked for the
passphrase but, in per-product mode, created no key, so it was not clear whether the session
kept it; and a reissue could change the display name and vendor tag but not remove them.

## Decision

- **Declined switch.** Declining the switch confirmation changes nothing and shows the Signing
  keys menu, like any cancelled action (amends PDR-0047).
- **Nobody present.** A person is present only when both the tool's input and its output are a
  terminal. Otherwise the tool refuses before reading or writing anything; help and the version
  still work (amends PDR-0039 and PDR-0048).
- **Passphrase from setup.** The passphrase typed at setup is kept for the session in both
  signing-key modes, under the idle lock. Setup's closing message says the first product's
  signing key is created under the passphrase just set, without asking again in this session
  unless the idle lock has passed (amends PDR-0049 "When asked").
- **Removing at reissue.** At reissue the display name and vendor tag, like a number or text
  feature, can be removed with the remove answer; the new key carries none (amends PDR-0048
  "Optional inputs").

## Reasons

- Every other cancelled or declined action returns to its menu; ending the session for one was
  an artefact of one command per run.
- Nothing needs the tool's output redirected any more (PDR-0048); a session nobody can see is a
  hang, not a use.
- A new vendor sets up and adds the first product in one sitting; asking for the passphrase
  again a minute after typing it twice protects nothing the idle lock does not.
- A display name or vendor tag can be wrong or outdated as easily as a feature; issuing a new
  license to drop one would lose the license's history and reference.

## Rejected options

| Option | Why rejected |
|---|---|
| Declined switch ends the session with a failure code | Unlike every other action; loses the session's passphrase for nothing |
| Present judged by input only, guide says not to redirect | A redirect by mistake leaves a session waiting for answers to questions nobody sees |
| Keep the setup passphrase only in shared mode | Same secret, same sitting; the difference would only be explained by how the tool is built |
| Never keep the setup passphrase | Asks again straight after setup |
| Display name and vendor tag changeable but not removable | Clearing one would need a new license |

## Consequences

- `ucl > file` is refused like a run with nobody present; the release run's `ucl --version`
  still works.
- The guide explains that the passphrase typed at setup stays unlocked for the idle lock time.
- The remove answer is offered at four kinds of question at reissue: number feature, text
  feature, display name, vendor tag.
