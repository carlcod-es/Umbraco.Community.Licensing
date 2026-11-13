# PDR-0068: What the sample product shows each persona

- Status: Amended by PDR-0071
- Date: 2026-11-13
- Source: `add-sample-package` design.md Q6, Q8, Q9, Q10, Q15, Q16, Q18
- Personas: site visitor (never affected); backoffice editor (warned, never shown key details);
  site owner (primary: sees what they have, what expires, what is missing; the key never shown);
  implementor (which key fails, why, where to fix it); vendor (a pattern to copy). None harmed:
  the visitor-first choice costs the vendor enforcement, which is the vendor's call to make
  differently (personas: site visitor).

## Context

The library reports a state; what each person sees is the vendor's decision. The sample has to
show one sound set of choices for every persona and state, consistent with
`docs/personas.md`.

## Decision

- **Visitor.** The form works in every state. Unlicensed extras (uploads) are absent with no
  explanation. No key, identifier, state, expiry, feature or licensing text reaches the public
  page or any visitor response.
- **Over the monthly limit, or on an unlisted domain**: visitors' submissions are still
  accepted; the administrator dashboard and the editor banner report it.
- **Administrator** (Settings section users only): a dashboard with the state, its first
  action, the key identifier (verified, or claimed and marked unverified), the claimed product
  when another, the expiry or "perpetual", each feature granted or "not included" (and, when
  expired, what lapsed), usage against the limit, the domain check and the host checked, and
  the setting the key is read from with its environment variable form.
- **First actions** match the library's documented first action for each state; `Valid` has
  none.
- **Expiring soon**: a `Valid` license expiring in 30 days or fewer; never a perpetual one.
- **Editor** (the product's Content-section screen): a banner when the license is not `Valid`,
  is expiring soon, is over its limit or does not cover this domain; each case in its own words,
  each telling the editor to contact their administrator. The banner shows the expiry date or
  usage numbers where relevant and nothing else: no key, identifier, state name or claimed
  product.
- **Never the key string**, on any screen or in any response, for any user.
- **Evaluated on use**: a key changed in configuration takes effect without a restart.
- **Log**: one warning per state change, naming the state and the identifier; never the key. (Levels amended by PDR-0071.)
- **Domains** are compared ignoring case, spaces and port; with no `licensed-domains`, any
  domain is covered.

## Reasons

- The persona rules: a visitor never sees licensing, an editor may see warnings but never a
  key, and the key is a bearer token.
- The site owner needs what they have, what is about to expire and what is missing; the
  implementor needs which key, why and where to fix it. The dashboard answers both.
- An editor cannot act on an identifier or a state name; "contact your administrator" is the
  action they can take.
- Thirty days gives time to renew through a purchase and a deploy.
- One log line per change helps the implementor without flooding the log on every request.

## Rejected options

| Option | Why rejected |
|---|---|
| A visitor-facing notice when unlicensed | Breaks the persona rule that visitors never see licensing (Carl, 2026-11-13) |
| Refusing submissions over the limit | Visitor affected by licensing; contradicts Q6 |
| Editors see the full dashboard | Editors have no role in licensing |
| Identifier in the editor banner | Nothing an editor can do with it |
| Log every evaluation | Floods the log; evaluation runs on every request |

## Consequences

- A vendor wanting enforcement (refuse over the limit, hide the form) changes the sample's
  gating, not the library.
- The sample's wording is its own; FR-7 and FR-8 stay open.
