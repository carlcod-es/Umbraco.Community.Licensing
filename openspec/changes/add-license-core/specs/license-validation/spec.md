## Purpose

Lets a product decide at runtime, entirely offline, whether it is licensed, and tells the site
owner and implementor which key is failing and why, without ever granting or describing an
entitlement from an unverified key.

## ADDED Requirements

### Requirement: Evaluate one key for one product
A product SHALL set up evaluation once with its product ID, a trusted set of public keys and a
clock. A product ID that breaks the product ID rule, or an empty trusted set, is the product's
own configuration error and SHALL raise an error at setup. Each evaluation SHALL take zero or one key string and SHALL
return exactly one result. It SHALL work with no network access. An evaluation SHALL NOT raise
an error, whatever the key string holds.

#### Scenario: Garbage never throws
- **WHEN** the key string is `Hunter2!`, a cut-off key, a key with one character changed, or
  one million random characters
- **THEN** evaluation returns a result and raises no error

#### Scenario: Invalid product ID
- **WHEN** a product sets up evaluation with product ID `Acme Commerce`
- **THEN** an error is raised at setup, before any key is evaluated

#### Scenario: Empty trusted set
- **WHEN** a product sets up evaluation with a trusted set holding no keys
- **THEN** an error is raised at setup, before any key is evaluated

### Requirement: Missing key
When no key string is supplied, or the supplied string is empty or contains only whitespace,
the state SHALL be *missing*.

#### Scenario: No key
- **WHEN** no key string is supplied
- **THEN** the state is *missing*

#### Scenario: Empty placeholder
- **WHEN** the key string is `""`, `"   "` or a single line break
- **THEN** the state is *missing*

### Requirement: One state, first failing check wins
A supplied key SHALL be checked in this order, and the first failing check SHALL set the state:
1. cannot be read → *unreadable*
2. claims another product → *wrong product*
3. its signing key ID is not in the trusted set → *signing key not recognised*
4. its signature does not verify → *not verified*
5. verified, but its contents cannot be read → *not supported*
6. verified, but past its expiry → *expired*

Otherwise the state SHALL be *valid*. The product SHALL be *licensed* only when the state is
*valid*. State names SHALL describe what was observed, never a presumed cause such as
tampering.

#### Scenario: Wrong product before signature
- **WHEN** a key for `zenith.commerce-shipping`, signed by an untrusted key, is evaluated for
  `acme.commerce`
- **THEN** the state is *wrong product*

#### Scenario: Unknown signing key
- **WHEN** a key for `acme.commerce` signed by a key not in the trusted set is evaluated
- **THEN** the state is *signing key not recognised*

#### Scenario: Edited contents
- **WHEN** a valid key's contents are edited to expire in 2099 with `max-orders: 999999`
- **THEN** the state is *not verified*

#### Scenario: Verified but not readable by this product
- **WHEN** a key for `acme.commerce` signed by a trusted key holds a field this product does not
  know
- **THEN** the state is *not supported*, not *unreadable*

#### Scenario: Valid
- **WHEN** a trusted, unexpired key for `acme.commerce` is evaluated for `acme.commerce`
- **THEN** the state is *valid* and the product is licensed

### Requirement: Reading a supplied string
All whitespace SHALL be removed before reading, so a key wrapped across lines or with a
trailing line break reads as the original. The text before the first `.` SHALL be the key
identifier and SHALL exactly match the identifier format: 5 + `-` + 5 + `-` + 4 characters of
the reference alphabet, uppercase, preceded by nothing or by a prefix of 1 to 16 uppercase
`A`-`Z` and `0`-`9` followed by `-`. Evaluation SHALL accept any prefix fitting this rule and
SHALL NOT be configured with one. When it does not match, the state SHALL be
*unreadable* with no claimed identifier and no claimed product, and no further check SHALL run. A string longer than 32,767 characters after whitespace removal
SHALL be *unreadable* without its contents being decoded; its identifier is still reported when
it matches. A string that is not three non-empty segments of the expected encoding, whose
signature part is not the expected length, or whose contents do not name a product and a
signing key ID SHALL be *unreadable*.

#### Scenario: Wrapped by an email client
- **WHEN** a valid key is split across three lines with a trailing line break
- **THEN** the state is *valid*

#### Scenario: Cut off
- **WHEN** the key string is `LIC-8F3AK-M7RXB-7Q2D.eyJzaWdu`
- **THEN** the state is *unreadable* and the claimed identifier is `LIC-8F3AK-M7RXB-7Q2D`

#### Scenario: No identifier
- **WHEN** the key string is `LIC-8F3AK-M7R`, `Hunter2!`, or a valid key lowercased
- **THEN** the state is *unreadable* with no claimed identifier

#### Scenario: Other prefix accepted
- **WHEN** a key for `acme.commerce` with identifier `ACME-8F3AK-M7RXB-7Q2D`, signed by a trusted
  key and unexpired, is evaluated for `acme.commerce`
- **THEN** the state is *valid* and the key identifier is `ACME-8F3AK-M7RXB-7Q2D`

#### Scenario: No prefix
- **WHEN** a key with identifier `8F3AK-M7RXB-7Q2D`, signed by a trusted key and unexpired, is
  evaluated for its product
- **THEN** the state is *valid*

#### Scenario: Relabelled key
- **WHEN** a valid key `ACME-8F3AK-M7RXB-7Q2D` has its prefix changed to `BETA` and its other
  parts left intact
- **THEN** the state is *not verified*, claiming `BETA-8F3AK-M7RXB-7Q2D`

#### Scenario: Identifier typo
- **WHEN** a valid key's identifier is changed to `LIC-8F3AK-M7RXB-7Q2O` and its other parts are
  left intact
- **THEN** the state is *unreadable* with no claimed identifier and no claimed product

#### Scenario: Too long
- **WHEN** the key string is `LIC-8F3AK-M7RXB-7Q2D.` followed by 32,800 further characters
- **THEN** the state is *unreadable* with claimed identifier `LIC-8F3AK-M7RXB-7Q2D`

### Requirement: Strict reading of verified contents
A key that verifies SHALL be *not supported* when its contents break any issue rule, have
an unknown field, repeat a field or a feature name, hold a date and time not in the exact
signed format, hold a feature value of an unsupported type (`false`, null, a list or a nested
object), or hold a number outside the number rule. Unknown feature names SHALL be accepted.
Such a key SHALL report its claimed product and identifier only.

#### Scenario: Faulty vendor tool
- **WHEN** a key signed by a trusted key holds a repeated feature name
- **THEN** the state is *not supported*, with its claimed product and identifier

#### Scenario: Unknown field
- **WHEN** a key signed by a trusted key holds a field the library does not know
- **THEN** the state is *not supported*

#### Scenario: Over a limit
- **WHEN** a key signed by a trusted key holds 51 features, or a feature name of 65 characters
- **THEN** the state is *not supported*

### Requirement: Expiry boundary
A key SHALL be valid through its stated second and *expired* once the clock is past it. A
perpetual key SHALL never expire.

#### Scenario: Last second
- **WHEN** a key expiring `2027-03-01T12:00:00Z` is evaluated at `2027-03-01T12:00:00.900Z`
- **THEN** the state is *valid*

#### Scenario: Next second
- **WHEN** the same key is evaluated at `2027-03-01T12:00:01Z`
- **THEN** the state is *expired*

### Requirement: What a result reports
A *missing* result SHALL report the state only. A result in *unreadable*, *wrong product*,
*signing key not recognised*, *not verified* or *not supported* SHALL report the state, and the claimed product
and claimed key identifier when they could be read, presented as claims. It SHALL NOT report an
issue time, expiry, display name, vendor tag or features. A *valid* or *expired* result SHALL
report as facts: product, reference, key identifier, signing key ID, issue time, expiry or
perpetual, display name, vendor tag and features. No result SHALL contain any part of the key
string other than its identifier.

#### Scenario: Unverified claims stay claims
- **WHEN** an edited key claiming `LIC-8F3AK-M7RXB-P6TY`, `acme.commerce`, expiry 2099 and
  `max-orders: 999999` is evaluated
- **THEN** the result is *not verified*, reports the claimed product and identifier, and reports
  no expiry and no features

#### Scenario: Expired reports what lapsed
- **WHEN** an expired key with `ecommerce` and `max-orders: 500` is evaluated
- **THEN** the result reports its features, expiry and identifier as facts, and the product is
  not licensed

#### Scenario: No key text in results
- **WHEN** any result is turned into text for a log
- **THEN** the text contains no part of the key string other than its identifier

### Requirement: Feature lookup
A product SHALL look up a feature by name and type: switch, number or text. A lookup SHALL
answer only when the state is *valid*; in every other state it SHALL answer not granted. Names
SHALL be matched ignoring the case of `a`-`z` only, the same in every culture. A switch lookup
SHALL be granted only when the feature holds a switch. A number SHALL return the value as
issued, exactly. A text SHALL return the value as issued, exactly, never split or interpreted.
An absent or unknown name, or a lookup of a different type than the feature holds, SHALL answer
not granted. There SHALL be no lookup that answers for a feature of any type; the features a key
holds are those reported in the result.

#### Scenario: Switch
- **WHEN** a valid key has `ecommerce` and the product asks for `Ecommerce`
- **THEN** the feature is granted

#### Scenario: Number exact
- **WHEN** a valid key has `discount-rate: 0.15`
- **THEN** the number lookup returns exactly `0.15`

#### Scenario: Text unchanged
- **WHEN** a valid key has `licensed-domains: "example.com,shop.example.com"`
- **THEN** the text lookup returns `example.com,shop.example.com`

#### Scenario: Wrong type
- **WHEN** a valid key has `max-orders: "500"` as text and the product looks up a number
- **THEN** the number lookup answers not granted

#### Scenario: Switch lookup on text
- **WHEN** a valid key has `pro: "false"` as text and the product looks up the switch `pro`
- **THEN** the switch lookup answers not granted

#### Scenario: Expired key grants nothing
- **WHEN** the key is expired and has `ecommerce`
- **THEN** the lookup of `ecommerce` answers not granted

#### Scenario: Culture-independent
- **WHEN** the current culture is Turkish and the product asks for `INVOICING` on a key with
  `invoicing`
- **THEN** the feature is granted

#### Scenario: Look-alike name
- **WHEN** the product asks for `licensed-dоmains` written with a Cyrillic `о`
- **THEN** the lookup answers not granted

### Requirement: Evaluation on every request
Evaluating on every request SHALL be supported. An evaluation of a typical key (250 to 500
characters) SHALL complete in under 1 ms. Repeated evaluations SHALL NOT hold memory that grows
with the number of calls. A result SHALL be a snapshot at the time of evaluation; the vendor
documentation SHALL state that a cached result does not expire on its own.

#### Scenario: Per-request cost
- **WHEN** a valid 500-character key is evaluated 10,000 times
- **THEN** the average evaluation takes under 1 ms

#### Scenario: No memory growth
- **WHEN** a key is evaluated 100,000 times
- **THEN** the memory held after the calls does not grow with the number of calls

#### Scenario: A result is a snapshot
- **WHEN** a product keeps a *valid* result for a key expiring `2027-03-01T12:00:00Z` and reads it
  at `2027-03-01T12:00:01Z`
- **THEN** the kept result still reads *valid*, and a new evaluation reads *expired*
