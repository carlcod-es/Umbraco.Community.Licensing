# Umbraco Licensing

A shared licensing library for paid Umbraco marketplace packages.

Umbraco package vendors have no common way to license their work: each vendor either rolls an
ad-hoc key scheme or ships unprotected. This library aims to give every package the same scheme:
a signed, product-scoped license key that is **verified entirely offline** (no phone-home, no
licensing server).

Built primarily for the **site owner** who buys licenses ([`docs/personas.md`](docs/personas.md)).

## Status

Restarted 2026-10-08. First change `add-license-core` (core library: issue, validate,
signing keys) is proposed in `openspec/changes/add-license-core/`; not implemented yet.

## Repository

Specification-driven: **OpenSpec artifacts are the source of truth**. Work is explored,
specified and agreed before it is implemented.

```
  openspec/changes/<change>/        proposal, design, specs (behaviour), tasks
  openspec/specs/                   current specs, synced from archived changes
  docs/personas.md                  who the library serves, and priority order
  docs/decisions/                   product decision records (PDR)
  docs/adrs/                        architecture decision records (ADR)
  docs/license-examples.md          license schema and worked examples
```

| Phase | Command | What happens |
|---|---|---|
| Explore | `/opsx:explore` | Requirements only; no technology |
| Propose | `/opsx:propose` | Proposal, design, specs and tasks; technology choices and ADRs |
| Apply | `/opsx:apply` | Implement the tasks |
| Archive | `/opsx:archive` | Finalise the change once shipped |

Conventions:

- Specs describe behaviour; how it is built belongs in `design.md` or an ADR.
- A product decision gets a PDR (`docs/decisions/`); a technology decision gets an ADR
  (`docs/adrs/`). Test: if the stack changed, a PDR would still hold.
- Cite records with prefix and number: PDR-NNNN, ADR-NNNN.

## License

MIT. See [`LICENSE`](LICENSE).
