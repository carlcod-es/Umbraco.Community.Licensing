# PDR-0006: Issue time is informational

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R2 (`CLEAN-PROJECT-PROMPT.md` section 12, decision 6)
- Personas: site owner and implementor (when was this key issued), vendor (support).

## Decision

The library sets the issue time from the clock at signing, UTC, truncated to the second. It is
never a caller input. It is reported for verified keys and plays no part in evaluation.

## Reasons

It shows when a key was issued and tells reissues apart by time. With one key per product
(PDR-0001) nothing needs to order keys, so it has no role in evaluation.

## Rejected options

| Option | Why rejected |
|---|---|
| Drop it | Loses the "when" that support and site owners ask for, at a cost of about 30 characters |
| Caller-supplied | Backdating would mislead; the clock is the only honest source |
