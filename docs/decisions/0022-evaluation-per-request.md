# PDR-0022: Evaluation on every request

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q17
- Personas: site visitor (license checks do not slow pages), vendor (no cache to build), site
  owner (a key stops granting at its expiry).

## Context

Vendors gate features at runtime, often on page requests. The requirements said evaluation never
throws but nothing about its cost or how often it may be called. Without a requirement, vendors
cache results, and a cached result is a snapshot:

```
  10:00     evaluate --> valid (expires 12:00:00)    product caches the result
  12:00:01                                            cached result still says valid
```

## Decision

Evaluating on every request is supported:

- An evaluation of a typical key (250 to 500 characters) completes in under 1 ms.
- Repeated evaluations hold no memory that grows with the number of calls.

A result is a snapshot at the time of evaluation. The vendor documentation states, in one line,
that a cached result does not expire on its own: a product that caches re-evaluates once the
clock passes the reported expiry.

## Reasons

- Vendors need no cache, so the snapshot trap does not arise for most products.
- 1 ms is small next to a page request and does not dictate the design.

## Rejected options

| Option | Why rejected |
|---|---|
| No cost requirement; guidance to cache and re-evaluate at expiry | Every vendor builds the same cache; those who skip the expiry step keep features on after expiry |

## Consequences

The cost target is tested. Measured times depend on the machine; the test states where it ran.
