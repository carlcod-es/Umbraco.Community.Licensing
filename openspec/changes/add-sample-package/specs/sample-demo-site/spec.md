# Spec Delta

## Purpose

Lets a vendor or implementor run the sample product from a clone of this repository and see
every license state it can show, using committed demo keys and a demo-only key picker, without
the sample ever being released.

## ADDED Requirements

### Requirement: Runs from a clone
The repository SHALL hold a demo site with the sample product installed that starts with one
documented command after cloning, with no database server, creates its first administrator from
demo credentials committed in the site's settings and documented as demo-only, and contains the
published Home and Contact pages of "Requirement: Demo site pages".

#### Scenario: First run
- **WHEN** a developer clones the repository, trusts the local development certificate as the
  sample documentation describes, and runs the documented start command
- **THEN** the site starts at the documented secure address, the Home page is shown at the site
  root, the Contact page shows the contact form, and the developer can log in to the backoffice
  with the documented demo administrator

#### Scenario: Demo administrator documented as demo-only
- **WHEN** a reader looks up the demo site's administrator login
- **THEN** the sample documentation gives the email and password and states that they are for
  the local demo only and must be changed in any site built from the sample

### Requirement: Served at a secure local address
The demo site SHALL be served at a secure (https) local address. The only setup step SHALL be
trusting the local development certificate once, which the sample documentation describes. The
plain (http) local address SHALL forward to the secure one.

#### Scenario: Plain address forwards
- **WHEN** the site is running and the developer opens the plain (http) local address, at the
  site root or the backoffice
- **THEN** the browser is sent to the same path at the secure address

#### Scenario: Certificate step documented
- **WHEN** a reader follows the sample documentation's steps to run the demo
- **THEN** it names trusting the local development certificate as the one setup step, gives the
  command for it, and gives the secure address to browse to

### Requirement: Demo site pages
The demo site SHALL hold two published pages, Home at the site root and Contact, sharing one
plain, neutral layout with no branding colours. Every page SHALL show a header with the site
name and a navigation link to each page, and a footer with the footer text and a link to the
project's repository. Home SHALL show
a heading, intro text, an About text saying what the demo is, and a link to Contact. Contact
SHALL show a heading, intro text and the contact form.

#### Scenario: Home page
- **WHEN** a visitor opens the site root on a fresh clone
- **THEN** the page shows the header with the site name and links to Home and Contact, a
  heading, intro text, the About text, a link to Contact, and the footer "Demo site for Acme
  Contact Form" with the link "Umbraco.Community.Licensing on GitHub" to
  https://github.com/carlcod-es/Umbraco.Community.Licensing

#### Scenario: Contact page
- **WHEN** a visitor follows the link to Contact
- **THEN** the page shows the same header and footer, a heading, intro text and the contact form

#### Scenario: Site that ran an earlier version
- **WHEN** a developer's demo site already holds data from a version without the Home and
  Contact pages, or without the About text
- **THEN** the sample documentation says how to delete the site's local data so the next start
  creates the pages

### Requirement: Demo site content editable
The heading and intro text of each page, Home's About text, the site name and the footer text
SHALL be editable in the backoffice; each navigation link SHALL show its page's name. The footer
text SHALL start as "Demo site for Acme Contact Form". The About text SHALL start as
the text in the scenario "About text on a fresh clone".

#### Scenario: About text on a fresh clone
- **WHEN** a visitor opens Home on a fresh clone
- **THEN** the About text reads "Acme Contact Form is a fictional Umbraco package: a contact form
  whose features are switched on by a license key. It is the sample for
  Umbraco.Community.Licensing, an open-source .NET library for offline license keys. Send a
  message from the Contact page, then sign in to the back office to see what the license gives
  you."

#### Scenario: Editor changes the content
- **WHEN** an editor changes the site name, the footer text, Home's About text, and the Contact
  page's name, heading and intro text, and publishes
- **THEN** a visitor sees the new site name and footer on every page, the new About text on Home,
  the new heading and intro on Contact, and the navigation link to Contact shows the new name

### Requirement: No licensing in the site layout
The header, the footer and the Home page SHALL show no information about this site's license
in any license state: no key identifier, state name, expiry, or text about the license or its
problems. Describing the product and the library, as the About text and the repository link
do, is allowed.

#### Scenario: No licensing outside the form
- **WHEN** the configured key is `Expired` and a visitor opens Home and Contact
- **THEN** the header, the footer and the Home page hold no key identifier, no state name, no
  expiry and no text about this site's license, and show the same About text and repository
  link as with a valid key

### Requirement: Demo signing keys
The repository SHALL hold a demo signing key pair, both halves committed, used to sign the demo
keys and trusted only by the sample product, and a second demo pair used only to sign the
untrusted-signer key. Every place either private key is committed SHALL state that it is public,
for demonstration only, and must never be trusted by a real product.

#### Scenario: Warning beside the private key
- **WHEN** a reader opens any file holding a demo private key
- **THEN** the file states that the key is public, demo-only and must never be trusted by a
  real product

#### Scenario: Not the library's example key
- **WHEN** the demo signing key ID is compared with the library's example signing key IDs
- **THEN** it matches neither

### Requirement: Demo key set
The repository SHALL hold one committed demo key for each entry in the table of the scenario
"The ten entries", each documented with what it shows. `NotSupported` SHALL be documented as
not demonstrated.

#### Scenario: The ten entries
- **WHEN** a reader lists the demo key set
- **THEN** it holds exactly these entries, each showing what the table says

| Entry | Shows |
|---|---|
| Valid: full plan | `Valid`, perpetual; uploads; 5 a month; `localhost` |
| Valid: small plan | `Valid`, expires 2099-12-31; no uploads; 3 a month; `localhost` |
| Valid: another domain | `Valid`; domain `www.example.com` only |
| Valid: expiring soon | `Valid`, expiring soon at its demo date |
| Expired | `Expired`; the full plan's features |
| No key | `Missing` |
| Damaged key | `Unreadable` |
| Another product's key | `WrongProduct`, claiming `acme.commerce` |
| Untrusted signer | `SigningKeyNotRecognised` |
| Edited key | `NotVerified` |

#### Scenario: Every entry yields its state
- **WHEN** each demo key is evaluated by the sample product, at its demo date where it has one
- **THEN** each yields the state and the features its entry documents

#### Scenario: Not supported not demonstrated
- **WHEN** a reader looks for a `NotSupported` demo key
- **THEN** the sample documentation says it is not demonstrated and why, and the dashboard's
  first action for it is still covered by the product's tests

### Requirement: Demo keys checked on every change
The repository's automated checks SHALL evaluate every committed demo key and fail when any
yields a state, product, expiry or feature other than its documented one, so a library change
that alters how a demo key reads is caught.

#### Scenario: Library change breaks a demo key
- **WHEN** a library change makes the full-plan demo key read as `NotSupported`
- **THEN** the sample's automated checks fail naming that entry

### Requirement: Demo keys regenerated by one command
The repository SHALL document one command that issues the whole demo key set again with the
committed demo signing keys, for when the library's key format changes during the beta. The
command SHALL not run as part of the normal checks.

#### Scenario: Regenerate after a format change
- **WHEN** a maintainer runs the documented regeneration command
- **THEN** it prints a new key for every entry that needs one, from which the committed demo
  keys can be replaced, and the normal checks then pass with them

### Requirement: Default configured key
The demo site's committed settings SHALL configure the Valid full-plan demo key in
`ContactForm:LicenseKey`, so a fresh clone shows a licensed product through the same setting an
implementor uses.

#### Scenario: Fresh clone
- **WHEN** the demo site is started for the first time
- **THEN** the dashboard reports `Valid` with the full plan, with "Configured key" as the
  source

### Requirement: Demo key picker
When the site's demo setting is on, the licensing dashboard SHALL offer a picker listing
"Configured key" and every demo key entry. Choosing an entry SHALL make the product evaluate
that key from the next use, for the dashboard, the banner and the form on the Contact page, until another
entry is chosen or the site restarts.

#### Scenario: Choose Expired
- **WHEN** an administrator chooses "Expired" in the picker
- **THEN** the dashboard shows `Expired`, an editor's submissions screen shows its banner, and
  the Contact page shows no upload field

#### Scenario: Restart
- **WHEN** an entry other than "Configured key" is chosen and the site restarts
- **THEN** the product evaluates the configured key again

### Requirement: Picker leaves configuration alone
Choosing a picker entry SHALL not change any settings file or environment value. While an
entry other than "Configured key" is chosen, the dashboard SHALL say the key shown is a demo
selection and name the entry.

#### Scenario: Settings unchanged
- **WHEN** an administrator chooses "Damaged key"
- **THEN** the site's settings files are unchanged, and the dashboard says it shows the demo
  selection "Damaged key"

### Requirement: Demo date
Only the "Valid: expiring soon" entry SHALL carry a demo date. While an entry with a demo date
is chosen, the product SHALL treat that date
as now when evaluating the key and checking expiring soon, and the dashboard SHALL show the demo
date. Other entries SHALL use the real clock.

#### Scenario: Expiring soon at its demo date
- **WHEN** an administrator chooses "Valid: expiring soon"
- **THEN** the dashboard shows the demo date and reports the license expiring in 14 days, and
  the editor banner reports the expiry date

### Requirement: Picker for administrators only, in demo mode only
The picker and the requests that list or choose entries SHALL be available only to users with
Settings access, and only while the demo setting is on. With the setting off, the dashboard
SHALL show no picker, any choose request SHALL be refused, and the configured key SHALL apply.
The setting SHALL be off unless a site turns it on.

#### Scenario: Demo setting off
- **WHEN** the demo setting is off and an administrator opens the dashboard
- **THEN** no picker is shown and a request to choose an entry is refused

#### Scenario: Editor tries to choose
- **WHEN** the demo setting is on and a user without Settings access requests to choose an
  entry
- **THEN** the request is refused and the evaluated key is unchanged

### Requirement: Submissions kept while running
The sample product SHALL keep submissions only while the site runs and SHALL keep no uploaded
file content, only the file name and size. The sample documentation SHALL say that a real
product needs real storage.

#### Scenario: Restart clears submissions
- **WHEN** three submissions were taken and the site restarts
- **THEN** the submissions screen lists none and the monthly count is 0

#### Scenario: Upload kept as name and size
- **WHEN** a visitor submits with a 20 KB file named `brief.pdf` under a license granting uploads
- **THEN** the submissions screen shows `brief.pdf` and its size, and no file content is kept

### Requirement: Outside the release
The sample SHALL never be packed or published by the release, and the release SHALL not build
it. The sample SHALL be built and its automated checks run on every pull request and push to
`main` that changes the sample or the library.

#### Scenario: Release run
- **WHEN** a release is published
- **THEN** no package or release asset contains the sample, and the release did not build it

#### Scenario: Library change
- **WHEN** a pull request changes only the library
- **THEN** the sample is built and its checks run for that pull request
