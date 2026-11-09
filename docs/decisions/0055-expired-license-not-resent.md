# PDR-0055: An expired license is not resent

- Status: Decided
- Date: 2026-11-09
- Source: `add-vendor-tool` design.md Q74. Amends PDR-0034.
- Personas: vendor (told at once what to do instead, before typing a passphrase). Site owner
  (primary): never sent a key that is already expired. Implementor, backoffice editor, site
  visitor: unaffected.

## Context

Resend reissues a license from its record with nothing changed (PDR-0034). The library refuses
to issue a key whose expiry has passed (PDR-0007), so a resend of an expired license cannot be
signed. The specs did not say what the tool does then; the section 8 apply of `add-vendor-tool`
raised it.

## Decision

- Licenses > Resend of a license whose expiry has passed is refused when the license is chosen,
  before the summary: the tool says the license cannot be resent and to renew or reissue it
  with a later expiry instead. No passphrase is asked, nothing is stored, and the Licenses menu
  is shown.
- A perpetual license, or one not yet expired, is resent as before.

## Reasons

- An expired key is of no use to the site owner; sending one would only cause a second contact.
- Resend asks no questions by design; turning it into a renewal would change what the vendor
  chose. Renew and Reissue already set a new expiry.

## Rejected options

| Option | Why rejected |
|---|---|
| Offer to renew instead | Blurs Resend (no questions) into Renew; the vendor can choose Renew in one step |
| Let the library refuse at signing | The vendor would type the passphrase and confirm the summary for nothing |

## Consequences

- The guide says Resend is for a license still in force, and Renew for one that has lapsed.
