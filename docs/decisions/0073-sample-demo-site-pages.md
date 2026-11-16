# PDR-0073: The sample demo site has Home and Contact pages in a simple layout

- Status: Decided
- Date: 2026-11-16
- Source: `add-sample-package` design.md Q26. Amends PDR-0067. Raised by Carl's run of task
  10.1: the demo site was a bare page with only the form.
- Personas: vendor and implementor (a demo that looks like a site, so the product is seen in
  context; a layout small enough to copy or remove). Site visitor: sees an ordinary site, with
  licensing absent everywhere, not only on the form. Backoffice editor: edits the pages' text as
  on a real site. Site owner (primary): unaffected; the sample is never published.

## Context

PDR-0067 gave the sample a runnable demo site; it was built as one page holding only the contact
form, with no header or footer. Carl found it too spartan to show the product in context
(2026-11-15).

## Decision

- **Two pages**: Home at the site root, and Contact.
- **One plain, neutral layout** with no branding colours: a header with the site name and a
  navigation link to each page, and a footer.
- **Home** shows a heading, intro text and a link to Contact. **Contact** shows a heading,
  intro text and the contact form.
- **Editable in the backoffice**: each page's heading and intro text, the site name and the
  footer text. Each navigation link shows its page's name.
- **The footer starts as "Demo site for Acme Contact Form"**, so the site says it is a demo.
- **No licensing outside the form.** The header, the footer and Home show no licensing
  information in any license state.
- **A site that ran an earlier version** does not gain the pages by itself; the sample
  documentation says how to delete the site's local data to start fresh.

## Reasons

- A product shown inside an ordinary site is easier to judge than a form on a blank page.
- Editable text shows the content editing an implementor's editors will do, and shows the
  editor banner beside real content work.
- Keeping licensing out of the whole site, not only the form, demonstrates PDR-0068's visitor
  promise more convincingly.
- A minimal layout keeps attention on the product and is quick for a vendor to strip.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep the single bare page | Too spartan to show the product in context |
| One page with a header and footer, no navigation | Carl chose Home and Contact |
| Heading and intro fixed in the page | Carl chose editable text, as on a real site |
| Light Acme branding with an accent colour | More to strip out; neutral keeps attention on the product |
| Footer with no demo wording | The site should say it is a demo |

## Consequences

- The "First run" scenario shows Home at the site root and the form on Contact.
- Visitor-facing requirements speak of every page a visitor can open, not "the public page".
- A site with data from an earlier version needs its local data deleted to get the pages.
