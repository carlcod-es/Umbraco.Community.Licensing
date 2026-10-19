# PDR-0025: Issue refuses a key too long to deploy

- Status: Decided
- Date: 2026-10-12
- Source: `add-license-core` design.md Q26
- Personas: implementor and site owner (every issued key reads and fits an environment
  variable), vendor (a rare refusal that says what to shorten; no input restricted).
- Amends: PDR-0017

## Context

PDR-0017 put the worst case at about 22,400 characters, assuming text values of plain letters.
Text values and display names accept any characters, and most take more room in a key than a
plain letter. At the limits a license can be over 70,000 characters. No way of writing a key can
fit every license the rules allow: 50 text values of 256 arbitrary characters carry more
information than 32,767 characters hold.

## Decision

When every other rule passes, issue builds the key. If the key string is longer than 32,767
characters, the request is rejected naming `features`, stating how far over it is:

> The license is too large for a key (1,234 characters over the limit of 32,767). Shorten or
> remove text features.

Per-field limits (PDR-0017) are unchanged. Reissue is checked the same way.

| License at the limits (50 features, 64-character names, 256-character text) | Fits |
|---|---|
| Switches and numbers only | yes |
| Text of letters and digits | yes, about 22,200 |
| Accented text (`é`) | up to about 41 full-length text values |
| CJK text | up to about 28 |
| Emoji | up to about 21 |

Typical keys (250 to 500 characters) never reach the check.

## Reasons

- Keeps PDR-0017's promise: any key the library issues can be read and deployed.
- Restricts no input. Guaranteeing a fit through input rules alone would need limits about three
  times tighter for every vendor, to cover a case no realistic product reaches.
- `features` is an existing problem field and the only part that can grow this large; the
  display name adds at most a few hundred characters.

## Rejected options

| Option | Why rejected |
|---|---|
| Text values ASCII only | Restricts input; Carl chose not to (2026-10-12) |
| Tighter per-field limits | Cripples ordinary text to cover a rare edge |
| Raise the 32,767 read cap | The cap is the environment-variable ceiling (PDR-0017) |
| Drop fields (`issued`, display name, vendor tag) | Saves at most about 1,200 characters; the overrun is in text features |
| No check; rely on limits | Issue could return a key that reads as *unreadable* |

## Consequences

A request can be refused although each field passes on its own. Vendor documentation states the
total limit and that text values are the part to shorten.
