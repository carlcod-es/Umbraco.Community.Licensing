## Purpose

Lets a vendor turn a license's contents into a signed key string that survives email, after
checking the contents against the issue rules, and reissue a license from the key a site owner
presents without keeping records.

## ADDED Requirements

### Requirement: Issue a license
Given a request and a private signing key, the library SHALL produce a key string, the license
reference, the key identifier and the issue time. A request SHALL hold: product ID, optional
reference, a stated expiry, optional display name, optional vendor tag, and zero or more
features. The library SHALL keep no record of anything it issued.

#### Scenario: Typical issue
- **WHEN** a vendor issues `acme.commerce`, expiry on the date `2027-03-01`, display name
  `Commerce Standard`, vendor tag `CUST-0042`, features `ecommerce` and `max-orders: 500`
- **THEN** the library returns a key string, a reference such as `LIC-8F3AK-M7RXB`, a key
  identifier such as `LIC-8F3AK-M7RXB-7Q2D` and the issue time

#### Scenario: Minimal issue
- **WHEN** a vendor issues `acme.seo-toolkit`, perpetual, with no display name, vendor tag or
  features
- **THEN** a key string is produced

### Requirement: Reject a request with every problem listed
A request breaking any issue rule SHALL be rejected with one error listing every problem found,
each naming its field: `product`, `reference`, `expires`, `displayName`, `vendorTag`,
`features`, or `features.<name>` for a single feature. No key string SHALL be produced.

#### Scenario: Several problems
- **WHEN** a request has no expiry, vendor tag `INV 2026 04` and feature `max-orders: -200`
- **THEN** one error lists three problems, naming `expires`, `vendorTag` and
  `features.max-orders`, and no key is produced

### Requirement: Product ID rule
A product ID SHALL be two dot-separated parts, each made of lowercase `a-z`, digits and
hyphens and starting with a letter, and SHALL be at most 64 characters in total.

#### Scenario: Valid product ID
- **WHEN** the product is `acme.commerce` or `zenith.commerce-shipping`
- **THEN** the product ID is accepted

#### Scenario: Invalid product ID
- **WHEN** the product is `commerce`, `Acme.Commerce`, `acme.commerce.pro`, `acme.9shop` or
  absent
- **THEN** the request is rejected naming `product`

#### Scenario: Product ID too long
- **WHEN** the product ID is 65 characters, otherwise valid
- **THEN** the request is rejected naming `product`; at 64 characters it is accepted

### Requirement: Key identifier prefix
A vendor MAY set a prefix once when setting up issuing; it SHALL apply to every key issued with
that setup. When no prefix is stated the prefix SHALL be `LIC`. A vendor MAY state that keys
have no prefix. A prefix SHALL be 1 to 16 characters of uppercase `A`-`Z` and `0`-`9`; a prefix
breaking this rule is the vendor's configuration error and SHALL raise an error at setup, before
any request is checked. The prefix is branding only: the vendor documentation SHALL state that it
grants no protection, and that a license is protected by the private signing key and the
product's trusted set.

#### Scenario: Default prefix
- **WHEN** issuing is set up with no prefix stated
- **THEN** issued key identifiers look like `LIC-8F3AK-M7RXB-7Q2D`

#### Scenario: Vendor prefix
- **WHEN** issuing is set up with prefix `ACME`
- **THEN** issued key identifiers look like `ACME-8F3AK-M7RXB-7Q2D` and references like
  `ACME-8F3AK-M7RXB`

#### Scenario: No prefix
- **WHEN** issuing is set up stating no prefix
- **THEN** issued key identifiers look like `8F3AK-M7RXB-7Q2D` and references like `8F3AK-M7RXB`

#### Scenario: Longest prefix
- **WHEN** issuing is set up with prefix `ACMECOMMERCE2026` (16 characters)
- **THEN** setup succeeds

#### Scenario: Invalid prefix
- **WHEN** issuing is set up with prefix `acme`, `ACME_SHOP`, `ACME-SHOP`, an empty prefix or
  `ACMECOMMERCE20261` (17 characters)
- **THEN** an error is raised at setup and no request is checked

### Requirement: License reference and key identifier
A reference SHALL be 10 characters from the alphabet of uppercase letters and digits without
`0 O 1 I L`, shown as the prefix, `-` and two groups of five (`LIC-XXXXX-XXXXX`), or as the
two groups alone when there is no prefix. When the request has no reference the library SHALL
generate a random one. When the request has one, the library SHALL accept it ignoring case,
hyphens and spaces when what remains is either 10 characters, or the configured prefix followed
by 10 characters, and SHALL reject anything else naming `reference`. With no prefix configured,
only the 10-character form SHALL be accepted. At every issue the library SHALL generate a random
4-character key part from the same alphabet; a request SHALL have no way to supply it. The key
identifier SHALL be the reference as shown + `-` + key part.

#### Scenario: First issue generates a reference
- **WHEN** a request has no reference
- **THEN** a new random reference is generated and returned

#### Scenario: Reissue keeps the reference, new key part
- **WHEN** two keys are issued with reference `LIC-8F3AK-M7RXB`
- **THEN** both identifiers start `LIC-8F3AK-M7RXB-` and each has its own randomly generated key
  part

#### Scenario: Reference accepted loosely
- **WHEN** the reference is given as `lic-8f3ak m7rxb` or `8F3AK-M7RXB`
- **THEN** it is accepted as `LIC-8F3AK-M7RXB`

#### Scenario: Reference with a vendor prefix
- **WHEN** issuing is set up with prefix `ACME` and the reference is given as `acme 8f3ak m7rxb`
  or `8F3AK-M7RXB`
- **THEN** it is accepted as `ACME-8F3AK-M7RXB`

#### Scenario: Reference with another prefix or a key part
- **WHEN** issuing is set up with prefix `ACME` and the reference is given as `LIC-8F3AK-M7RXB`
  or `ACME-8F3AK-M7RXB-7Q2D`
- **THEN** the request is rejected naming `reference`

#### Scenario: Invalid reference
- **WHEN** the reference contains `O` or has 9 or 11 characters
- **THEN** the request is rejected naming `reference`

### Requirement: Issue time
The library SHALL set the issue time from the clock at signing, in UTC, truncated to the
second. A request SHALL have no way to supply it.

#### Scenario: Issue time from the clock
- **WHEN** a key is issued at `2026-10-01T08:00:00.734Z`
- **THEN** its issue time is `2026-10-01T08:00:00Z`

### Requirement: Expiry is stated
Every request SHALL state an expiry: a date, a UTC date and time to the second, or perpetual.
A request with no expiry stated SHALL be rejected naming `expires`. A date alone SHALL mean
23:59:59 UTC on that day. A date and time SHALL be accepted in any offset and stored as the same
instant in UTC; one with a fraction of a second SHALL be rejected naming `expires`. An expiry
earlier than the current second SHALL be rejected naming `expires`; an expiry equal to the
current second, or a date alone of today, SHALL be accepted.

#### Scenario: Date alone
- **WHEN** the expiry is stated as the date `2027-03-01`
- **THEN** the key expires at `2027-03-01T23:59:59Z`

#### Scenario: No expiry stated
- **WHEN** a request states neither a date, a date and time nor perpetual
- **THEN** it is rejected naming `expires`

#### Scenario: Expiry in the past
- **WHEN** at `2026-10-01T08:00:00Z` the expiry is the date `2026-09-30` or the time
  `2026-10-01T07:59:59Z`
- **THEN** the request is rejected naming `expires`

#### Scenario: Expiry today
- **WHEN** at `2026-10-01T08:00:00Z` the expiry is the date `2026-10-01` or the time
  `2026-10-01T08:00:00Z`
- **THEN** the request is accepted

#### Scenario: Fraction of a second
- **WHEN** the expiry is `2027-03-01T12:00:00.5Z`
- **THEN** the request is rejected naming `expires`

#### Scenario: Offset converted
- **WHEN** the expiry is `2027-03-01T14:00:00+02:00`
- **THEN** the key expires at `2027-03-01T12:00:00Z`

### Requirement: Display name rule
A display name, when given, SHALL be 1 to 64 characters with no leading or trailing
whitespace, no line breaks and no control characters. It SHALL be signed exactly as supplied
and never trimmed or cleaned; a breaking value is rejected naming `displayName`.

#### Scenario: Valid display name
- **WHEN** the display name is `Commerce Pro` or `Commerce Pro – Édition`
- **THEN** it is accepted and signed unchanged

#### Scenario: Invalid display name
- **WHEN** the display name is empty, `" Pro"`, contains a line break, or has 65 characters
- **THEN** the request is rejected naming `displayName`

### Requirement: Vendor tag rule
A vendor tag, when given, SHALL be 1 to 64 characters from `A-Z a-z 0-9 - _ . # /`, case kept,
signed exactly as supplied. A breaking value SHALL be rejected naming `vendorTag`, never
cleaned.

#### Scenario: Valid vendor tags
- **WHEN** the vendor tag is `#1001`, `SHOP-2026-000123`, `pi_3NkX9a2eZvKYlo2C1` or
  `INV/2026/04`
- **THEN** it is accepted

#### Scenario: Invalid vendor tags
- **WHEN** the vendor tag is `INV 2026 04`, `jane@acme.com`, empty, or 65 characters
- **THEN** the request is rejected naming `vendorTag`

### Requirement: Feature rules
A feature SHALL be a name and a typed value: switch, number or text. A request SHALL hold at
most 50 features. A name SHALL be 1 to 64 characters of lowercase `a-z`, digits and hyphens,
starting with a letter, and SHALL appear at most once per request. A
switch SHALL be granted by being present; an explicit `false` SHALL be rejected. A number SHALL
be zero or positive, with at most 4 decimal places and at most 15 digits in total; it SHALL be
signed without trailing fractional zeros. A text value SHALL be 1 to 256 characters with no
leading or trailing whitespace, no line breaks and no control characters; it is never
interpreted. Each problem SHALL name `features.<name>`, or `features` when the name itself is
invalid or repeated or when there are too many features.

#### Scenario: Valid features
- **WHEN** features are `ecommerce`, `max-orders: 2500`, `discount-rate: 0.15` and
  `licensed-domains: "a.com,A.com"`
- **THEN** they are accepted, and the text is signed unchanged

#### Scenario: Trailing zeros removed
- **WHEN** a number feature is `2.50`
- **THEN** it is signed as `2.5`

#### Scenario: Explicit false
- **WHEN** a feature is `pro: false`
- **THEN** the request is rejected naming `features.pro`

#### Scenario: Invalid numbers
- **WHEN** a number feature is `-200`, `2.12345`, or has 16 digits
- **THEN** the request is rejected naming the feature

#### Scenario: Invalid names
- **WHEN** a feature is named `Max Orders`, or `max-orders` appears twice, or a name has 65
  characters
- **THEN** the request is rejected naming `features`

#### Scenario: Too many features
- **WHEN** a request holds 51 features
- **THEN** the request is rejected naming `features`; with 50 it is accepted

### Requirement: Optional feature definitions
A vendor MAY pass feature definitions with an issue request: a list of feature names, each with
its type (switch, number or text). When definitions are passed, a feature whose name is not
defined SHALL be rejected naming `features.<name>`, and a feature whose type differs from its
definition SHALL be rejected naming `features.<name>`. A defined feature MAY be left out of the
request. An empty list of definitions SHALL mean the product has no features, so any feature is
rejected. When no definitions are passed, features SHALL be checked by the feature rules alone.
Definitions SHALL follow the feature name rule and name each feature at most once; invalid
definitions are the vendor's configuration error and SHALL raise an error before any request is
checked.

#### Scenario: Wrong type
- **WHEN** `max-orders` is defined as a number and the request has `max-orders: "500"` as text
- **THEN** the request is rejected naming `features.max-orders`

#### Scenario: Misspelled name
- **WHEN** `max-orders` is defined and the request has `max-order: 500`
- **THEN** the request is rejected naming `features.max-order`

#### Scenario: Defined feature left out
- **WHEN** `ecommerce`, `max-orders` and `ai-assist` are defined and the request has only
  `ecommerce`
- **THEN** the request is accepted

#### Scenario: Empty definitions
- **WHEN** an empty list of definitions is passed and the request has `ecommerce`
- **THEN** the request is rejected naming `features.ecommerce`

#### Scenario: No definitions
- **WHEN** no definitions are passed and the request has `max-orders: "500"` as text and
  `max-order: 500`
- **THEN** the request is accepted

#### Scenario: Invalid definitions
- **WHEN** the definitions name `max-orders` twice, or name `Max Orders`
- **THEN** an error is raised and no request is checked

#### Scenario: Invalid text
- **WHEN** a text feature is empty, `" example.com"`, contains a line break, or has 257
  characters
- **THEN** the request is rejected naming the feature

### Requirement: Key string shape
A key string SHALL be one line of printable ASCII characters with no whitespace, beginning with
its key identifier. Editing any character of it, including changing the case of the
identifier, SHALL make it fail evaluation.

#### Scenario: Pasteable key
- **WHEN** a key is issued with a display name containing non-ASCII letters
- **THEN** the key string still contains only printable ASCII and no whitespace, and starts with
  its key identifier

### Requirement: Build a reissue request from a verified license
The library SHALL build a request from a verified license holding its product, reference,
expiry (perpetual stated explicitly), display name, vendor tag and features. The vendor SHALL be
able to change every field except the product and the reference, which the request keeps; a new
reference is a new license, started from a fresh request. The issued key SHALL take the prefix of
the issuing setup it is issued with, not the prefix of the license it was built from. Building SHALL read no clock and check nothing;
issuing the request SHALL apply every issue rule. A key that is not verified SHALL offer no
license to build from.

#### Scenario: Add a feature
- **WHEN** a vendor builds a request from a verified license with `max-orders: 500`, changes it
  to `max-orders: 1500` and issues
- **THEN** the new key has the same reference, a new key part, a new issue time and
  `max-orders: 1500`

#### Scenario: Prefilled past expiry
- **WHEN** at `2026-10-01T08:00:00Z` a request built from a license that expired
  `2026-09-01T23:59:59Z` is issued unchanged
- **THEN** it is rejected naming `expires`

#### Scenario: Product and reference kept
- **WHEN** a vendor builds a request from a verified license for `acme.commerce` with reference
  `LIC-8F3AK-M7RXB`
- **THEN** the request's product and reference cannot be changed, and every other field can

#### Scenario: Prefix changed by the vendor
- **WHEN** a request is built from a verified license `LIC-8F3AK-M7RXB-7Q2D` and issued with a
  setup whose prefix is `ACME`
- **THEN** the new key identifier starts `ACME-8F3AK-M7RXB-`

#### Scenario: Perpetual carried over
- **WHEN** a request is built from a perpetual license
- **THEN** its expiry is stated as perpetual

### Requirement: Personal data is kept out by the vendor
A key SHALL NOT be meant to hold customer names, emails, company names or other personal data.
The library SHALL enforce this only through the field rules above (the vendor tag, for example,
refuses `@` and spaces). It SHALL NOT attempt to detect personal data in display names or text
features; keeping it out is the vendor's responsibility, stated in the vendor documentation.

#### Scenario: Name in a text feature
- **WHEN** a text feature is `Jane Smith`
- **THEN** the library accepts it; the vendor documentation forbids it
