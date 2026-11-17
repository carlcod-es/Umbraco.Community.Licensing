# PDR-0076: The sample's default demo key allows five submissions a month

- Status: Decided
- Date: 2026-11-17
- Source: `add-sample-package` design.md Q30. Amends PDR-0069.
- Personas: vendor and implementor (the monthly limit and the hidden submissions of PDR-0074
  can be seen on the configured key, without the picker). Site owner (primary), backoffice
  editor, site visitor: unaffected; the sample is never published.

## Context

The demo site starts on the Valid full-plan key, which allowed 1000 submissions a month, so the
limit was only seen by choosing "Valid: small plan" (3 a month, no uploads). With PDR-0074, the
over-limit case also hides submissions; Carl wants it reachable on the default key
(2026-11-17).

## Decision

- **The Valid full-plan demo key allows 5 submissions a month.** Its other features are
  unchanged: uploads, `localhost`, perpetual.
- **"Valid: small plan" stays at 3 a month** with no uploads.
- The demo key set is re-issued, so every demo key identifier changes; the demo site's
  configured key is updated to the new full-plan key.

## Reasons

- Five submissions is quick to reach by hand and still shows uploads working first.
- Keeping the small plan different shows two plans side by side.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep 1000 and use the picker | Carl wants the limit reachable on the default key |
| Change the small plan to 5 | Carl chose the default key |

## Consequences

- The demo key table and the fresh-clone dashboard show 5 a month for the full plan.
- Restarting the site clears the count (submissions are kept in memory only).
