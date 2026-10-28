# PDR-0033: License records and customer data

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q10, Q12, Q26
- Personas: site owner (primary: a copied backup holds no usable key; erasure honoured; a lost
  key is resent). Vendor: answers "what did this customer buy". Implementor: unaffected.

## Context

The tool records what the vendor issued, so it can reissue, find licenses and export by signing
key. Records may hold an order reference and customer email (FR-2). Key strings are bearer
tokens: anyone holding one can install it on any site.

## Decision

- **No key strings stored.** A record holds product, reference, and per issue: issue time,
  expiry, display name, vendor tag, features, key identifier, signing key ID. A key string is
  shown once when issued.
- **Resend is reissue unchanged.** Same reference and contents, new key string, a new issue
  entry. The earlier string keeps working (keys cannot be revoked).
- **Customer fields.** Order reference and customer email, each optional, held once per license
  (design.md Q26, Architect assumption); editable at any time and at reissue. Clearing leaves no
  copy of the earlier value in the installation.
- **Never deleted.** License records and issue entries cannot be deleted.
- **Find and list.** By product, reference, order reference, customer email. One license shows
  every issue entry, newest first.

## Reasons

- Without stored key strings, a copied database or backup cannot be used to install licenses.
- Keeping every record keeps the leak export complete (PDR-0035).
- Clearing the fields answers an erasure request without losing the record of which key signed
  what.

## Rejected options

| Option | Why rejected |
|---|---|
| Store key strings to resend the original | A copied database holds every customer's working key |
| Delete a license record | Breaks "record the signing key of every issued license"; a leak export would miss it |
| Delete every record for one email at once | Clearing each license does the same |
| Customer fields per issue entry | More to clear; per license meets the need |

## Consequences

- Backups taken before a field was cleared still hold it; the guide says so, and that backups
  are the vendor's responsibility.
- A renewal with a new order reference replaces the earlier one on the license.
