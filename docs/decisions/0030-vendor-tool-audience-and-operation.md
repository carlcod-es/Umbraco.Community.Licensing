# PDR-0030: Vendor tool audience and operation

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q1, Q2, Q3, Q14, Q23, Q27, Q28 (`docs/feature-requests.md` FR-2)
- Personas: vendor (small: issues through the tool alone). Site owner, implementor: unaffected;
  keys are the library's keys. Larger vendors keep using the library directly.

## Context

The library keeps no records (PDR-0013, PDR-0014). Small vendors need somewhere to hold signing
keys, products and sales, and a guided way to set it up. Larger vendors issue from their own
store with the library.

## Decision

- **Audience.** Small vendors who issue keys only through the tool. Larger vendors are not served
  in this change.
- **Installation.** One vendor per installation: one folder, one database, one prefix at a time,
  one signing-key mode. A second issuing setup is a second installation in another folder.
- **One person.** No user accounts or permissions. Whoever can run the tool against the folder
  can do everything; signing also needs the passphrase (PDR-0032).
- **By hand or batched.** Every action except setup can be run with all its inputs on the command and asks
  nothing; a missing input with nobody present is a refusal naming it. The tool never connects to
  a store; a vendor's own script may call it. Setup is run only by a person at a terminal and
  takes no answer from options, environment settings or redirected input (design.md Q28, Carl,
  2026-10-29).
- **Guided setup.** Folder (PDR-0031), passphrase (PDR-0032), prefix (PDR-0023), one signing key
  per product (proposed) or one shared, standby key offered as recommended (PDR-0035). Standby
  keys follow the mode: one per product signing key, or one for the shared key (design.md Q27,
  Carl, 2026-10-29). Signing-key mode is
  fixed after setup; changing it is FR-17. The prefix can be changed later: new issues and
  reissues use the current prefix; existing licenses keep theirs until reissued (PDR-0023)
  (design.md Q23, Carl, 2026-10-28).
- **Jobs.** Setup; products and feature definitions (PDR-0036); issue, reissue, renew, resend
  (PDR-0034); find and list, customer fields (PDR-0033); standby, switch, trusted keys, export by
  signing key (PDR-0035); backup (PDR-0031).

## Reasons

- A small vendor gets one place for keys and records, with every value set once at setup.
- Batchable commands let a vendor automate without the tool taking on store integration and the
  exposure of a running service.
- One person per installation keeps the tool free of accounts; the folder and passphrase are the
  boundary.

## Rejected options

| Option | Why rejected |
|---|---|
| By hand only | Any later automation means reworking every command |
| Scriptable setup | Setup is run once, and its prefix and mode choices are best made by a person reading the questions and the closing advice; a script gains nothing and a scripted passphrase cannot be confirmed by retyping (design.md Q28) |
| Store-driven (webhooks, a listening service) | A running service with its own exposure; not a console tool for small vendors |
| Several operators, with or without roles | Needs accounts and a shared database; not small vendors' need |
| Serve larger vendors (key generation only, private-key export) | Adds a second product shape; FR-11 |
| Import licenses issued before the tool | Not needed by vendors starting with the tool; FR-16 |
| Prefix fixed after setup | A vendor who outgrows the default `LIC` needs a new installation and loses the history there; no import (FR-16) |
| No proposed signing-key mode, or shared proposed | Per-product limits a leak or rotation to one product, and the mode cannot change later (FR-17); a vendor pressing Enter gets the safer choice. Shared stays one answer away for single-product vendors (design.md Q27) |
| Signing-key mode changeable after setup | Key migration with release coordination for every product; FR-17 |

## Consequences

- The tool's guide states the vendor is responsible for the folder, its backups and the
  customer data in it (PDR-0014).
- A vendor wanting a different signing-key mode creates a new installation (FR-17).
- After a prefix change, licenses carry the earlier prefix until reissued. A reference typed with
  the earlier prefix is refused; the 10-character form is accepted (PDR-0023).
