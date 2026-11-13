# PDR-0067: A sample product package shows the library in use

- Status: Decided
- Date: 2026-11-13
- Source: `add-sample-package` design.md Q1, Q2, Q5, Q14, Q17. Takes up FR-3.
- Personas: vendor (a working product to read and copy); implementor (a site to try keys in).
  Site owner (primary): released packages are unchanged, as the sample is never published.
  Backoffice editor, site visitor: met only in the demo site.

## Context

Vendors had documentation and API reference but no working product showing where trust and the
key come from, when a key is evaluated, how features gate behaviour and what each persona
should see. FR-3 asked for one.

## Decision

- **An Umbraco 17 package for a fictional product**, Acme Contact Form (product ID
  `acme.contact-form`), using the library the way a real vendor would, with a runnable demo
  site.
- **Three features**, one of each type: switch `file-uploads`, number
  `max-submissions-per-month`, text `licensed-domains` (a comma-separated list of host names).
- **In this repository only.** Never published to NuGet or the Umbraco Marketplace, never part
  of a release. Built and checked on every change to the sample or the library.
- **Submissions kept only while the site runs**, uploads as file name and size only. The sample
  says a real product needs real storage.

## Reasons

- An Umbraco package is what vendors build; a sample in that shape answers their questions
  directly, including the backoffice screens.
- One feature of each type covers every lookup the library offers.
- A published demo package would carry a fake product into real sites and the Marketplace.
- Checking the sample on every library change shows the public API still serves a real product.
- Real storage would add code unrelated to licensing.

## Rejected options

| Option | Why rejected |
|---|---|
| A host-agnostic web app sample | Does not show the Umbraco backoffice experience |
| Umbraco package without a backoffice client | Cannot show a dashboard and editor banner as vendors build them |
| A shop or checkout product | Much more product code unrelated to licensing |
| A backoffice-only product | No visitor-facing output, so cannot show that visitors never see licensing |
| Publishing the sample | A fictional product in real feeds |

## Consequences

- The repository gains a `samples/` folder with its own owner rows.
- A library change that breaks the sample fails its check before release.
