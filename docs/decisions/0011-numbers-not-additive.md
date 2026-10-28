# PDR-0011: Numbers are not additive-only

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R2 (`CLEAN-PROJECT-PROMPT.md` section 12, decision 4)
- Personas: vendor (any count, limit, amount or rate).

## Decision

A number is zero or positive, with at most 4 decimal places and at most 15 digits in total. It
is reported exactly as issued (no binary rounding) and signed without trailing fractional zeros
(`2.50` → `2.5`). Any count, limit, amount or rate is allowed (`discount-rate: 0.15`): nothing
is summed.

## Reasons

Numbers were restricted to additive quantities only because several keys were summed. With one
key per product (PDR-0001) nothing is summed, so the restriction has no purpose.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep additive-only | Restriction without a reason after PDR-0001 |
| Negative numbers | No known use; a negative limit invites mistakes |
