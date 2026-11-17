# PDR-0075: The sample demo site says what it is and links to the project

- Status: Decided
- Date: 2026-11-17
- Source: `add-sample-package` design.md Q29. Amends PDR-0073.
- Personas: vendor and implementor (the demo explains itself and leads to the library's
  repository). Site visitor: reads what the demo is; still sees nothing about this site's
  license. Backoffice editor: edits the About text like the other page text. Site owner
  (primary): unaffected; the sample is never published.

## Context

PDR-0073 gave the demo site Home and Contact pages with no licensing outside the form. A
visitor to the demo cannot tell what the product is or where the project lives. Carl wants
product information on Home and a link to the repository (2026-11-17).

## Decision

- **Home shows an About text**, editable in the backoffice, starting as: "Acme Contact Form is
  a fictional Umbraco package: a contact form whose features are switched on by a license key.
  It is the sample for Umbraco.Community.Licensing, an open-source .NET library for offline
  license keys. Send a message from the Contact page, then sign in to the back office to see
  what the license gives you."
- **Every page's footer links to the project**: "Umbraco.Community.Licensing on GitHub",
  pointing at https://github.com/carlcod-es/Umbraco.Community.Licensing.
- **PDR-0073's "no licensing outside the form" is narrowed** to license state: the header, the
  footer and Home show no key identifier, state name, expiry, or text about this site's license
  or its problems, in any license state. Describing the product and the library is allowed.
- **A site that ran the previous version** does not gain the About text by itself; as in
  PDR-0073, the sample documentation says to delete the site's local data.

## Reasons

- A demo that says what it is and where it comes from is easier to evaluate and share.
- The visitor promise (PDR-0068) is about this site's license state; a fixed description of
  the product reveals nothing about it.
- The footer is on every page, so the link is always one click away.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep the strict "no text about licensing" rule and avoid the word | Weakens the explanation for no gain to the visitor |
| Repo link on Home only | Carl chose the footer on every page |
| About text fixed in the template | Other page text is editable (PDR-0073) |

## Consequences

- The "No licensing in the site layout" requirement speaks of license state information.
- A working copy from the previous version deletes its local demo data once more.
