# PDR-0033: License records and customer data

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q10, Q12, Q26
- Personas: site owner (primary: a copied backup holds no usable key; erasure honoured; a lost
  key is resent; past contacts not kept). Vendor: answers "what did this customer buy" and
  "which key did this order produce". Implementor: unaffected.

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
- **Customer fields** (design.md Q26, Carl, 2026-10-28). Each optional.
  - Customer email: held once per license; editable at any time and at reissue.
  - Order reference: held per issue entry, given at issue, reissue or renewal. An entry with no
    order (a resend, a free add-on) has none. Only the latest entry's order reference can be
    corrected; the order references of every entry of a license are cleared in one action.
  - Clearing leaves no copy of the earlier value in the installation.

  ```
  LIC-8F3AK-M7RXB   email owner@example.com
    2025-11-30  issue     order ORD-1001
    2026-11-20  renewal   order ORD-1874
    2027-03-02  resend    -
  ```
- **Never deleted.** License records and issue entries cannot be deleted.
- **Find and list.** By product, reference, order reference (any entry), customer email. A row
  shows the email and the latest entry's order reference. One license shows every issue entry,
  newest first, each with its order reference.

## Reasons

- Without stored key strings, a copied database or backup cannot be used to install licenses.
- Keeping every record keeps the leak export complete (PDR-0035).
- Clearing the fields answers an erasure request without losing the record of which key signed
  what.
- An order reference belongs to a sale and a renewal is a new sale, so it sits on the issue
  entry: a refund, chargeback or "which key did order X produce" finds the exact entry, and a
  vendor's script can see an order was already fulfilled. An email belongs to the customer, so
  one per license: when it changes, the earlier contact is not kept.
- Correcting only the latest order reference needs no way to name an entry; a typo is almost
  always caught before the next renewal.

## Rejected options

| Option | Why rejected |
|---|---|
| Store key strings to resend the original | A copied database holds every customer's working key |
| Delete a license record | Breaks "record the signing key of every issued license"; a leak export would miss it |
| Delete every record for one email at once | Clearing each license does the same |
| Both fields once per license | A renewal overwrites the earlier order reference: a refund on an earlier order, or "which key did this order produce", cannot be answered |
| Both fields per issue entry | Keeps every past email. A resent or reissued key may go to a former contact, such as an agency after handing the site over; a former contact finding licenses by email learns about a license now held by someone else; erasing one person on a shared license means finding their entries; a corrected typo stays on earlier entries; a leaked folder exposes every past contact |
| Correct any entry's order reference | Needs a way to name an entry in commands; a late typo is rare and the order stays traceable by email |

## Consequences

- Backups taken before a field was cleared still hold it; the guide says so, and that backups
  are the vendor's responsibility.
- An issue entry with no order (a resend, a free add-on) shows no order reference.
- A wrong order reference on an earlier entry can only be cleared, together with the others.
- Warning on an order reference already used is FR-18.
