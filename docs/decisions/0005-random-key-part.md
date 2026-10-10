# PDR-0005: Random key part at every issue

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R2 (`CLEAN-PROJECT-PROMPT.md` section 12, decision 7)
- Personas: implementor (tell reissues apart across environments).

## Decision

At every issue the library generates a random 4-character key part from the reference alphabet.
It is never a caller input. It completes the key identifier.

## Reasons

It tells reissues of one license apart in the identifier, the only part people can see and
search: staging on `...-7Q2D`, production on `...-H8RC` shows staging is behind. Two reissues of
one license matching by chance: about 1 in 923,000, harmless because their issue times differ.

## Rejected options

| Option | Why rejected |
|---|---|
| Sequence (`-0001`, `-0002`) from the presented key | No records; reissuing from an old key repeats a number and gives a false "newer" signal |
| Derived from the issue time | Longer; implies an order people may trust over the issue time |
| None (reference only) | Reissues indistinguishable by identifier |
