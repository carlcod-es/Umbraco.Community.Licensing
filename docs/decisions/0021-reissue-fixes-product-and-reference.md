# PDR-0021: A reissue keeps its product and reference

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q16
- Personas: implementor (compares identifiers across environments), site owner (one license
  history), vendor (records keyed by reference).
- Amends: PDR-0013

## Context

PDR-0013 let a vendor change every field of a reissue request except the product. The reference
names one license across its reissues (PDR-0004); implementors rely on that when comparing
environments:

```
  prod     LIC-8F3AK-M7RXB-H8RC   same reference, other key part --> staging is behind
  staging  LIC-8F3AK-M7RXB-7Q2D
  staging  LIC-QW7NP-3KD9T-7Q2D   other reference --> looks like another license
```

## Decision

A request built from a verified license keeps its product and its reference; neither can be
changed on it. Every other field can. A new reference is a new license, started from a fresh
request.

This guards the reissue path only. The library keeps no records, so a vendor can still build a
fresh request with any product and reference.

## Reasons

- Protects the identifier comparison across environments and the vendor's history by reference.
- No persona needs a new reference while keeping everything else; merging duplicate purchases
  keeps one existing reference (`docs/license-examples.md` example 8).

## Rejected options

| Option | Why rejected |
|---|---|
| Product fixed only (PDR-0013) | A changed reference silently breaks the link between environments |
| Nothing fixed; the request is a prefill | Also lets one reference span two products |
