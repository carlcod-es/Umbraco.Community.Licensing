# PDR-0009: Vendor tag

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md R2
- Personas: vendor (link a key to their records without the library keeping any), site owner
  (no personal data in the key).

## Decision

Optional. 1 to 64 characters from `A-Z a-z 0-9 - _ . # /`; no spaces; case kept; signed exactly
as supplied; an invalid value is rejected, never cleaned. Not unique, never compared, no effect
on evaluation or gating. Reported for verified keys only.

| Input | Result |
|---|---|
| `#1001`, `SHOP-2026-000123`, `pi_3NkX9a2eZvKYlo2C1`, `INV/2026/04` | accepted |
| `INV 2026 04` | rejected: space |
| `jane@acme.com` | rejected: `@` (and personal data) |
| empty | rejected: omit it instead |
| 65+ characters | rejected |

Guidance: hold a stable customer or account ID, so every reissue keeps the same tag; the
reference links the license's history in the vendor's records.

## Reasons

The character set covers common order, invoice and payment IDs, and keeps out spaces and `@`, so
email addresses and most names cannot be used.

## Rejected options

| Option | Why rejected |
|---|---|
| Free text | Invites customer names and emails |
| Clean invalid input | The signed value would differ from what the vendor entered |
