# PDR-0017: Length limits

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q12
- Personas: implementor (any key the library reads can be deployed through an environment
  variable), site visitor (bounded work on hostile input), vendor (limits no realistic product
  reaches).

## Context

Display name (64), vendor tag (64) and text values (256) were bounded; product ID, feature name
and feature count were not, so neither was a key's length. Keys are deployed through
configuration, environment variables and vault secrets; Windows caps an environment variable at
32,767 characters, and some hosting panels allow far less.

## Decision

Applied at issue (rejected, naming the field) and at read (a verified key over a limit is
unreadable):

| Item | Limit |
|---|---|
| Product ID | 64 characters in total |
| Feature name | 64 characters |
| Features per key | 50 |
| Key string | Longer than 32,767 characters after whitespace removal: unreadable, not decoded |

Worst case at the limits: 50 features with 64-character names and 256-character text values,
about 22,400 characters. Typical keys stay at 250 to 500.

## Reasons

- Any key the library issues fits the Windows environment-variable ceiling, and the read cap
  equals it, so what is read can be deployed.
- Settled before the first key ships: a read-side limit added later would make issued keys
  unreadable.

## Rejected options

| Option | Why rejected |
|---|---|
| No limits | Unbounded keys and unbounded reads |
| Limits at issue only | The reader stays unbounded against hostile input |

## Consequences

A vendor needing more than 50 features groups them (a `pro` switch for a bundle; PDR-0010) or
splits the product.
