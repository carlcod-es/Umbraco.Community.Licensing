# PDR-0003: An empty or whitespace-only key is missing

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q1
- Personas: implementor (placeholder settings read as "install the key"), site owner.

## Context

Configuration often holds an empty placeholder, for example an environment variable set to
`""`. The reading rules remove all whitespace first, leaving nothing.

## Decision

A key string that is empty or contains only whitespace gives the state *missing*, the same as
no key supplied.

## Reasons

- Nothing is left to read once whitespace is removed; no key was in fact installed.
- *Missing* gives the right first action: install the key. *Unreadable* would say "paste the key
  again" when nothing was pasted.

## Rejected options

| Option | Why rejected |
|---|---|
| Unreadable | Misleading first action; implies a damaged key |

## Consequences

A host cannot tell an empty setting from an absent one through the result. It can check its own
settings if it needs to.
