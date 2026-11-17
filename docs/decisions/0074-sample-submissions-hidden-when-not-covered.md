# PDR-0074: The sample hides submissions the license does not cover

- Status: Decided
- Date: 2026-11-17
- Source: `add-sample-package` design.md Q28. Amends PDR-0068.
- Personas: vendor (a pattern for gating what the product gives back, not only what it
  accepts). Backoffice editor and site owner: see a masked email and a note on each hidden
  submission, and get everything back once the license covers it. Site owner (primary): this
  withholds their own submissions while the license does not cover them; accepted by Carl for
  the demo because nothing is lost (see Consequences). Site visitor: unaffected; the form keeps
  working in every state. Implementor: unaffected.

## Context

PDR-0068 shows licensing problems to the administrator (dashboard) and the editor (banner)
while the product keeps working. Carl wants the demo to also show a vendor gating the
product's value: when the license is not valid, the submissions screen shows little more than
who wrote (2026-11-17).

## Decision

- **When the license is not `Valid`** (any other state), every submission on the submissions
  screen shows only a masked email address. The name, message, date and upload are hidden.
- **When the license is `Valid` but over its monthly limit**, this month's submissions within
  the limit, in the order they arrived, show in full; each later one shows only the masked
  email. With a limit of 5, the sixth submission onwards is hidden.
- **A domain not covered by the license hides nothing.** It is reported as before.
- **Masking**: the first character of each part is kept and every other character is replaced
  by `*`, one per character. The part before `@` is one part; the part after it is split at
  each `.`. A one-character part stays as it is; a value with no `@` keeps its first character.
  `carl@sargunar.com` shows as `c***@s*******.c**`; `first.last@example.co.uk` as
  `f*********@e******.c*.u*`.
- **Each hidden submission carries a short note**: "Details hidden until the license covers
  this submission." The banner (PDR-0068) still tells the editor to contact their
  administrator.
- **Everyone sees the same**, administrators included, and the data behind the screen holds
  only the masked email for a hidden submission, so the details cannot be read another way.
- **Nothing is lost**: submissions are still taken and kept in full. A hidden submission shows in
  full again as soon as the license covers it.

## Reasons

- The demo shows a vendor's lever beyond warnings: the product keeps collecting, but gives
  back less until the license is sorted.
- Keeping the email partly visible tells the site owner that real enquiries are waiting,
  which prompts them to fix the license without exposing the enquirer's details.
- Masking in the data, not only on screen, makes the gate real rather than cosmetic.
- Hiding only what is over the limit matches what the plan pays for.

## Rejected options

| Option | Why rejected |
|---|---|
| Hide nothing (warnings only, as PDR-0068) | Carl wants the demo to show gating of what the product gives back |
| Hide every submission once over the limit | Carl chose to hide only those beyond the limit |
| Also hide when the domain is not covered | Carl chose not-valid states and over-limit only |
| Fixed number of stars per part | Carl chose one star per hidden character, as in his example |
| Hide on screen only, full data in the response | Anyone with the screen's access could read the details through the API |
| Delete or refuse submissions over the limit | Would lose the site owner's enquiries and block visitors (PDR-0068) |

## Consequences

- A site owner with a broken or exceeded license cannot read new enquiries until it is fixed;
  the sample documentation says so, and that a real vendor should weigh this against the site
  owner's interests.
- Fixing the key, or a new month starting, shows the hidden submissions in full.
- Tests cover the masking rule, each state, and the boundary at the monthly limit.
