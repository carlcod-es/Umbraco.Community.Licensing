# PDR-0032: Private keys protected by a passphrase

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q7, Q8, Q14, Q15, Q24 (`docs/feature-requests.md` FR-4
  issue 2)
- Personas: vendor (a copied folder or backup cannot sign). Site owner (primary): no forged keys
  for products they bought. Implementor: unaffected.

## Context

Whoever holds a private signing key can issue valid licenses for every product that trusts it
(FR-2 leak case). The tool keeps keys in a folder that is copied for moves and backups, and runs
in scripts.

## Decision

- Every private key, active, standby or replaced, is stored encrypted with one passphrase set at
  setup, in a standard format other tools can open with it. Nothing else is encrypted.
- The passphrase is at least 12 characters (design.md Q24, Architect assumption), typed twice at
  setup.
- Asked once per action that signs or creates a key; never for reading, exports or backups.
  Scripts supply it without a prompt, never as a plain command argument. Not kept after an
  action ends.
- No way to export a private key from the tool.
- The standby key is kept with the active key under the same passphrase.
- Changing the passphrase is not offered in this change.

## Reasons

- The simplest protection that stops a copied folder or backup from becoming a signing leak,
  keeps the folder portable and works in scripts.
- A standard format avoids lock-in: a vendor leaving the tool opens their keys with the
  passphrase.
- The passphrase already separates both keys from a copied folder; storing the standby apart
  adds a second thing to lose for a small vendor.

## Rejected options

| Option | Why rejected |
|---|---|
| Plain key files, file permissions and guidance | A backup zip in email or cloud storage is a leak |
| Encrypt the whole database too | More moving parts; customer data is the vendor's responsibility, covered by disk encryption |
| Encrypted zip on top | Keys already encrypted; customer data as above |
| Remember the passphrase across actions | Needs a cache on disk or a background process |
| Export private key for larger vendors | Out of this change's audience; FR-11 |
| Standby exported to a separate file stored elsewhere | One more file to keep; FR-15 |
| Passphrase change | Not needed for the first release; FR-12 |

## Consequences

- A lost passphrase is a lost key (FR-2 loss case). The guide: keep the passphrase in a password
  manager, apart from backups.
- Folder copied and passphrase known: both active and standby keys leak.
- A leaked zip exposes customer emails and sales records, not signing keys.
