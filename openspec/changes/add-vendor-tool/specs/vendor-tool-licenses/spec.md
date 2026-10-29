## Purpose

Issues and reissues license keys from the vendor's stored records, keeps a record of every key
issued without keeping the key strings, and lets the vendor find a license and correct or clear
its customer details.

## ADDED Requirements

### Requirement: Issuing a license
The vendor SHALL issue a license for a product, giving expiry (a date and time, or perpetual),
and optionally display name, vendor tag, features, order reference and customer email. Features
SHALL be checked against the product's feature definitions; the license's other fields by the
library's issue rules. The customer email SHALL be stored on the license and the order
reference on the issue entry. Issuing SHALL sign with the product's active signing key, use the
installation's current prefix, and need the passphrase. The key string SHALL be shown once; a refused
issue SHALL name every problem and store nothing.

#### Scenario: License issued
- **WHEN** the vendor issues `acme.commerce` expiring 2027-03-01T23:59:59Z with `ecommerce` on and
  `max-orders` 500, order reference `ORD-1042` and customer email `owner@example.com`
- **THEN** the key string is shown, and a license record is stored with its reference, product,
  customer email and one issue entry holding the issue time, expiry, features, order reference,
  key identifier and signing key ID

#### Scenario: Undefined feature
- **WHEN** the vendor issues with feature `max-order`, which `acme.commerce` does not define
- **THEN** the issue is refused naming `features.max-order`, and nothing is stored

#### Scenario: Wrong passphrase
- **WHEN** the passphrase given does not open the signing key
- **THEN** the issue is refused saying the passphrase is wrong, and nothing is stored

### Requirement: Key strings are not stored
The tool SHALL NOT store any license key string, in the database or elsewhere in the folder. A
record SHALL hold everything needed to reissue the license.

#### Scenario: Copied folder
- **WHEN** someone reads every file in the folder after licenses were issued
- **THEN** no license key string is found

### Requirement: Reissuing from the record
The vendor SHALL reissue a license from its record, keeping product and reference, and changing
any of expiry, display name, vendor tag and features. The reissue SHALL be checked and signed as
an issue, with the product's current active signing key, and SHALL add an issue entry to the
same license record. An order reference MAY be given for the new issue entry, and the customer
email MAY be changed at the same time. A renewal SHALL be recorded as a renewal.

#### Scenario: Add-on bought
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB` adding switch `shipping`
- **THEN** a new key string with the same reference, the earlier features plus `shipping` and the
  current expiry is shown, and the record gains an issue entry

### Requirement: Suggested expiry
When the vendor reissues a time-limited license as a renewal, the tool SHALL suggest as the new
expiry the current expiry plus the length of the last term. The last term SHALL be the length
the last renewal granted: from the later of the expiry before that renewal and the renewal's
issue time, to the expiry the renewal set. For a license never renewed it SHALL be from the
first issue's issue time to the expiry it set. Reissues that are not renewals SHALL NOT change
the term. The length SHALL be counted in calendar months when the last term was a whole number
of months (same day of month and time of day; a day past the end of a month counts as its last
day), and in exact time otherwise. For a reissue that is not a renewal it SHALL suggest the
current expiry. A perpetual license SHALL be suggested as perpetual. The vendor SHALL accept or replace
the suggestion; the suggestion SHALL be used without a prompt only when the vendor asks for it.

#### Scenario: Renewal of a one-year license
- **WHEN** a license first issued 2025-11-30T23:59:59Z expiring 2026-11-30T23:59:59Z, never
  renewed, is renewed on 2026-11-20
- **THEN** the suggested expiry is 2027-11-30T23:59:59Z

#### Scenario: Second renewal after an early renewal
- **WHEN** that license, renewed on 2026-11-20 to 2027-11-30T23:59:59Z, is renewed again
- **THEN** the suggested expiry is 2028-11-30T23:59:59Z (term 2026-11-30 to 2027-11-30, not from
  2026-11-20)

#### Scenario: Add-on before renewal
- **WHEN** a license first issued 2026-01-15T10:00:00Z expiring 2027-01-15T10:00:00Z, reissued on
  2026-09-01 to add a feature, is renewed
- **THEN** the suggested expiry is 2028-01-15T10:00:00Z

#### Scenario: Renewal after a lapse
- **WHEN** a license that expired 2026-11-30T23:59:59Z was renewed at 2027-02-01T09:00:00Z with
  the typed expiry 2028-02-01T09:00:00Z, and is renewed again
- **THEN** the suggested expiry is 2029-02-01T09:00:00Z

#### Scenario: Feature change
- **WHEN** the vendor reissues the license first issued 2025-11-30T23:59:59Z expiring
  2026-11-30T23:59:59Z, never renewed, to add a feature
- **THEN** the suggested expiry is 2026-11-30T23:59:59Z

#### Scenario: Perpetual license
- **WHEN** a perpetual license is renewed
- **THEN** the suggested expiry is perpetual

#### Scenario: Never reissued
- **WHEN** a license whose only issue entry was issued 2026-01-15T10:00:00Z expiring
  2026-07-15T10:00:00Z is renewed
- **THEN** the suggested expiry is 2027-01-15T10:00:00Z (six calendar months, not 181 days)

#### Scenario: Term not a whole number of months
- **WHEN** a license issued 2026-03-10T09:00:00Z expiring 2026-04-09T09:00:00Z (30 days) is
  renewed
- **THEN** the suggested expiry is 2026-05-09T09:00:00Z

### Requirement: Resending a license
The vendor SHALL resend a license, which reissues it from its record with nothing changed: same
reference, expiry, display name, vendor tag and features, new key string. The resend SHALL add
an issue entry with no order reference.

#### Scenario: Site owner lost the key
- **WHEN** the vendor resends `LIC-8F3AK-M7RXB`
- **THEN** a new key string with the same reference and contents is shown, and the record gains
  an issue entry

### Requirement: Finding and listing licenses
The vendor SHALL list licenses, filtered by any of product, reference (as typed under the prefix
rule), order reference (matching any issue entry) and customer email, each row showing
reference, product, current expiry, customer email and the latest issue entry's order reference.
The vendor SHALL show one license with every issue entry, newest first.

#### Scenario: Find by customer email
- **WHEN** the vendor lists licenses for customer email `owner@example.com`
- **THEN** every license with that email is shown

#### Scenario: Find by an earlier order reference
- **WHEN** `LIC-8F3AK-M7RXB` was issued with order reference `ORD-1001` and renewed with
  `ORD-1874`, and the vendor lists licenses for order reference `ORD-1001`
- **THEN** `LIC-8F3AK-M7RXB` is shown, with `ORD-1874` as its latest order reference

#### Scenario: License history
- **WHEN** the vendor shows `LIC-8F3AK-M7RXB`, issued once and reissued twice
- **THEN** three issue entries are shown, newest first, each with issue time, expiry, features,
  order reference, key identifier and signing key ID

### Requirement: Customer details editable and clearable
The customer email SHALL be optional, held once per license, and editable or clearable at any
time. The order reference SHALL be optional and held per issue entry; an entry given none has
none. Only the latest issue entry's order reference SHALL be correctable. The vendor SHALL clear
the order references of every issue entry of a license in one action. Clearing SHALL leave no
copy of the earlier value in the installation. License records and issue entries SHALL never be
deleted.

#### Scenario: Renewal keeps the earlier order reference
- **WHEN** `LIC-8F3AK-M7RXB`, issued with order reference `ORD-1001`, is renewed with `ORD-1874`
- **THEN** the first issue entry still holds `ORD-1001` and the renewal entry holds `ORD-1874`

#### Scenario: Correcting the latest order reference
- **WHEN** the vendor corrects the order reference of `LIC-8F3AK-M7RXB` to `ORD-1875`
- **THEN** the latest issue entry holds `ORD-1875`, earlier entries are unchanged, and no record
  holds `ORD-1874`

#### Scenario: Clearing order references
- **WHEN** the vendor clears the order references of `LIC-8F3AK-M7RXB`
- **THEN** no issue entry of that license holds an order reference, no record holds the earlier
  values, and the license and its issue entries remain

#### Scenario: Erasure request
- **WHEN** the vendor clears the customer email of `LIC-8F3AK-M7RXB`
- **THEN** no record in the installation holds the earlier email, and the license and its issue
  entries remain

#### Scenario: No delete
- **WHEN** the vendor looks for a way to delete a license record
- **THEN** none exists; only the customer fields can be cleared
