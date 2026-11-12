# PDR-0065: Review before signing: Sign, Change answers or Cancel

- Status: Decided
- Date: 2026-11-12
- Source: `add-vendor-tool-rich-console` design.md Q2, Q8. Amends PDR-0048.
- Personas: site owner (primary): the summary is still the last stop before a wrong key reaches
  them, and now a wrong answer is fixed rather than abandoned. Vendor: fixes one answer without
  answering the whole action again. Implementor, backoffice editor, site visitor: unaffected.

## Context

PDR-0048 shows a summary before an issue, reissue, renewal or resend and asks whether to sign,
yes proposed; no stores nothing. A vendor who spots a wrong expiry must cancel and answer every
question again. `psw` offers "Edit configuration / Generate / Cancel" at the same point.

## Decision

- After the summary of an issue, reissue or renewal the vendor chooses **Sign** (proposed),
  **Change answers** or **Cancel**.
- **Change answers** shows the action's answers as a list where several can be ticked; only the
  ticked ones are asked again, each proposing the earlier answer; then the summary is shown
  again. The product (issue) and the license (reissue, renewal) are not offered: changing those
  is a different action, so Cancel and start again.
- **Cancel**, or Esc, stores nothing and returns to the menu.
- **Resend** has nothing to change: Sign or Cancel only.
- Nothing is signed or stored before Sign.

## Reasons

- Fixing a typo at the summary is quicker and less error-prone than answering everything again.
- Re-asking only what was ticked keeps the other answers exactly as reviewed.
- Excluding the product keeps feature answers valid; a different product has different
  features.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep yes/no at the summary | Carl asked for the review loop (2026-11-12) |
| Re-ask every answer on Change | Same cost as starting again |
| Allow changing the product | Every feature answer would need asking again |

## Consequences

- The guide describes the review step.
