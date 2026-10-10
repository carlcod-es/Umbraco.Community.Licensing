# PDR-0014: Personal data kept out by vendor guidance

- Status: Decided
- Date: 2026-10-09
- Source: `add-license-core` design.md Q4
- Personas: site owner (a forwarded key reveals nothing about them), vendor.

## Context

Keys are pasted into emails, tickets and configuration, and are readable once decoded. Customer
names, emails and company names must not be in them. The library cannot recognise a name in a
display name or a text feature.

## Decision

No personal data in a key is a vendor rule, stated in the vendor documentation. The library
enforces it only through field rules: the vendor tag refuses `@` and spaces; feature names are
restricted. Display names and text features are not scanned.

## Reasons

Any detector would be partial and would reject legitimate text. The simplest option meeting the
criterion is to make the rule explicit and keep the fields where personal data is most likely
(the vendor tag, used for customer IDs) restricted.

## Rejected options

| Option | Why rejected |
|---|---|
| Reject email-shaped text in display names and text features | Partial; can reject legitimate values; gives false assurance |
