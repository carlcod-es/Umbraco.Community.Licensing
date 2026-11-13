# Spec Delta

## Purpose

Shows, in a working Umbraco product, how a vendor uses the library: where trust and the key
come from, when a key is evaluated, how features gate behaviour, and what the administrator,
editor and visitor each see for every license state.

## ADDED Requirements

### Requirement: Product and trusted keys
The sample product SHALL have product ID `acme.contact-form` and SHALL trust only the demo
signing public key, which is part of the product itself and not read from site configuration.
The product's known features SHALL be the switch `file-uploads`, the number
`max-submissions-per-month` and the text `licensed-domains`.

#### Scenario: Demo key trusted
- **WHEN** a key for `acme.contact-form` signed with the demo signing key is configured
- **THEN** the product reports it `Valid` if it is in date

#### Scenario: Other signer refused
- **WHEN** a key for `acme.contact-form` signed with any other signing key is configured
- **THEN** the product reports `SigningKeyNotRecognised`

### Requirement: Key read from configuration
The sample product SHALL read its key from the site setting `ContactForm:LicenseKey`, which an
implementor can also set as the environment variable `ContactForm__LicenseKey`. No setting, or
an empty one, SHALL be reported as `Missing`.

#### Scenario: Key set by environment variable
- **WHEN** `ContactForm__LicenseKey` holds a valid key and the settings file holds none
- **THEN** the product reports `Valid`

#### Scenario: No key configured
- **WHEN** neither the settings file nor the environment holds `ContactForm:LicenseKey`
- **THEN** the product reports `Missing`

### Requirement: Evaluated when used
The sample product SHALL evaluate the key each time it needs the license (rendering the form,
taking a submission, showing the dashboard or the banner), so a key changed in configuration
takes effect without restarting the site.

#### Scenario: Renewed key installed while running
- **WHEN** the configured key is `Expired` and the implementor replaces it in the settings file
  with a valid key while the site runs
- **THEN** the next dashboard view reports `Valid`, with no restart

### Requirement: Visitor never blocked by licensing
In every license state, the public contact form SHALL render and SHALL accept a valid
submission. A licensing problem SHALL never cause an error page or a refused submission.

#### Scenario: Submission with a missing key
- **WHEN** no key is configured and a visitor submits the form with a name, email and message
- **THEN** the submission is accepted and the visitor sees the normal thank-you message

#### Scenario: Submission with a damaged key
- **WHEN** the configured key is `Unreadable` and a visitor opens the page
- **THEN** the page renders with the form, with no error

### Requirement: No licensing in public output
The public page and every response to a visitor SHALL contain no key string, key identifier,
license state, expiry, feature name or licensing message, in any license state.

#### Scenario: Visitor page with an expired key
- **WHEN** the configured key is `Expired` and a visitor opens the form page
- **THEN** the page holds no key identifier, no state name, no expiry and no text about
  licensing

#### Scenario: Thank-you response over the limit
- **WHEN** the license is over its monthly submission limit and a visitor submits the form
- **THEN** the response holds the normal thank-you message only

### Requirement: File uploads only when granted
The form SHALL offer a file upload only when the license is `Valid` and grants `file-uploads`.
Otherwise the upload field SHALL be absent, with no message saying why. An upload sent when not
offered SHALL be ignored and the rest of the submission accepted.

#### Scenario: Uploads granted
- **WHEN** the license is `Valid` with `file-uploads` on
- **THEN** the form shows an upload field

#### Scenario: Uploads not granted
- **WHEN** the license is `Valid` without `file-uploads`, or is not `Valid`
- **THEN** the form shows no upload field and no text about uploads

#### Scenario: Upload sent anyway
- **WHEN** the upload field is absent and a request still carries a file
- **THEN** the submission is accepted without the file

### Requirement: Monthly submission limit
When the license is `Valid` and grants `max-submissions-per-month`, the product SHALL count
accepted submissions per calendar month in UTC and report the license as over its limit once
the count exceeds that number. Submissions over the limit SHALL still be accepted. With no such
feature, or a license that is not `Valid`, no limit SHALL apply.

#### Scenario: Fourth submission on a three-submission plan
- **WHEN** the license grants `max-submissions-per-month` 3 and a fourth submission arrives in
  the same UTC month
- **THEN** the submission is accepted and the dashboard and the editor banner report the
  license over its monthly limit, with 4 of 3 used

#### Scenario: New month
- **WHEN** the count was over the limit in one UTC month and the next month starts
- **THEN** the count starts again at 0 and the over-limit report clears

### Requirement: Licensed domains
When the license is `Valid` and grants `licensed-domains`, the product SHALL read it as a
comma-separated list of host names and report the license as not covering this domain when the
site's request host is not in the list. Host names SHALL be compared ignoring case, surrounding
spaces and port. Without the feature, any domain SHALL be covered. The form SHALL keep working.

#### Scenario: Listed host with a port
- **WHEN** `licensed-domains` is `www.example.com, localhost` and the site is reached at
  `LOCALHOST:44321`
- **THEN** the domain is covered

#### Scenario: Unlisted host
- **WHEN** `licensed-domains` is `www.example.com` and the site is reached at `localhost`
- **THEN** the dashboard and the editor banner report that the license does not cover
  `localhost`, and the visitor can still submit the form

### Requirement: Administrator dashboard
The product SHALL show a licensing dashboard in the Settings section with the state, the first
action for it, the key identifier (verified, or claimed and marked as unverified), the claimed
product when another, the expiry or "perpetual", each known feature with what it grants or "not
included", usage against the monthly limit, the domain check and where the key is configured.

#### Scenario: Valid small plan
- **WHEN** the license is `Valid`, grants `max-submissions-per-month` 3 and `licensed-domains`
  `localhost`, does not grant `file-uploads`, and 1 submission was taken this month
- **THEN** the dashboard shows `Valid`, the key identifier, the expiry, `file-uploads` not
  included, 1 of 3 submissions used, `localhost` covered, and the setting name
  `ContactForm:LicenseKey` with its environment variable form

#### Scenario: Wrong product
- **WHEN** the configured key is a valid key for `acme.commerce`
- **THEN** the dashboard shows `WrongProduct`, its first action, the claimed product
  `acme.commerce` and the claimed key identifier marked as unverified

#### Scenario: Expired shows what lapsed
- **WHEN** the license is `Expired`
- **THEN** the dashboard shows the expiry that passed and the features the license held, marked
  as no longer in effect

### Requirement: First action for each state
The dashboard SHALL show, for each of the eight states, a first action that matches the
library's documented first action for that state, worded for the person reading the
dashboard. `Valid` SHALL show no action.

#### Scenario: Every state has its action
- **WHEN** the dashboard is shown for each of `Missing`, `Unreadable`, `WrongProduct`,
  `SigningKeyNotRecognised`, `NotVerified`, `NotSupported`, `Expired` and `Valid`
- **THEN** each shows the first action documented for that state, and `Valid` shows none

### Requirement: Expiring soon
A `Valid` license with an expiry 30 days or fewer away SHALL be reported as expiring soon on the
dashboard and in the editor banner, with the expiry date. A perpetual license SHALL never be.

#### Scenario: Expires in 14 days
- **WHEN** the license is `Valid` and expires in 14 days
- **THEN** the dashboard and the editor banner say the license expires on that date

#### Scenario: Expires in 31 days
- **WHEN** the license is `Valid` and expires in 31 days
- **THEN** neither the dashboard nor the banner reports it as expiring soon

### Requirement: Key string never shown
No backoffice screen and no response from the product SHALL contain the configured key string,
for any user and any state.

#### Scenario: Dashboard data with a valid key
- **WHEN** an administrator opens the dashboard with a valid key configured
- **THEN** neither the screen nor the data sent to it contains the key string

#### Scenario: Dashboard data with a damaged key
- **WHEN** the configured key is `NotVerified`
- **THEN** neither the screen nor the data sent to it contains the key string

### Requirement: Dashboard for Settings users only
The licensing dashboard and its data SHALL be available only to backoffice users with access to
the Settings section. Other backoffice users SHALL not see the dashboard and SHALL be refused
its data.

#### Scenario: Editor asks for the dashboard data
- **WHEN** a backoffice user with Content but not Settings access requests the dashboard data
- **THEN** the request is refused

### Requirement: Editor banner
The product's submissions screen in the Content section SHALL show a banner when the license is
not `Valid`, is expiring soon, is over its monthly limit or does not cover this domain. With
none of these, no banner SHALL show. Each case SHALL have its own wording, and every banner SHALL
tell the editor to contact their administrator.

#### Scenario: Not valid
- **WHEN** the license is `Missing`
- **THEN** the submissions screen shows a banner saying the contact form's license needs
  attention and to contact the administrator

#### Scenario: Valid and within limits
- **WHEN** the license is `Valid`, not expiring soon, within its limit and covering this domain
- **THEN** the submissions screen shows no banner

### Requirement: Editor sees no license details
The editor banner and the data behind it SHALL contain no key string, key identifier, state
name, claimed product or feature value other than the expiry date and the monthly usage
numbers.

#### Scenario: Banner data for a wrong-product key
- **WHEN** the configured key is `WrongProduct` and an editor opens the submissions screen
- **THEN** the banner and its data contain neither the claimed product nor the key identifier

### Requirement: Licensing changes logged
The product SHALL write one warning to the site log when the license state it evaluates differs
from the last state it logged, naming the new state and the key identifier (or claimed
identifier, or none). The log SHALL never contain the key string. A repeat of the same state
SHALL not be logged again.

#### Scenario: Key expires
- **WHEN** the state changes from `Valid` to `Expired`
- **THEN** one warning naming `Expired` and the key identifier is logged, and none on later
  evaluations while it stays `Expired`

#### Scenario: Log holds no key
- **WHEN** any state change is logged
- **THEN** the log entry does not contain the key string
