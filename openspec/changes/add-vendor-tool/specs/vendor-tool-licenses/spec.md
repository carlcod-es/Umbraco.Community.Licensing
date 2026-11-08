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
installation's current prefix, and need the passphrase. The tool SHALL ask for each optional
input in turn, an empty answer leaving it out, and for each of the product's feature
definitions in turn, showing its type. Before signing an issue, reissue, renewal or resend, the
tool SHALL show a summary (product, reference when known, expiry, display name, vendor tag,
features, customer email, order reference) and ask whether to sign it, yes proposed; no SHALL
store nothing and return to the menu. The key string SHALL be shown once on screen; the tool
SHALL then offer to save it to a file the vendor names (vendor-tool-backup: Files named by the
vendor), the file holding the key string alone.

#### Scenario: License issued
- **WHEN** the vendor issues `acme.commerce` expiring 2027-03-01T23:59:59Z with `ecommerce` on and
  `max-orders` 500, order reference `ORD-1042` and customer email `owner@example.com`
- **THEN** the key string is shown, and a license record is stored with its reference, product,
  customer email and one issue entry holding the issue time, expiry, features, order reference,
  key identifier and signing key ID

#### Scenario: Optional inputs skipped
- **WHEN** the vendor issues `acme.commerce` and gives an empty answer to every optional input and
  every feature
- **THEN** the summary shows no display name, vendor tag, features, customer email or order
  reference, and the license is issued without them

#### Scenario: Feature value outside its type
- **WHEN** the vendor answers `lots` for number feature `max-orders`
- **THEN** the tool names the number rule and asks for `max-orders` again

#### Scenario: Not signed at the summary
- **WHEN** the vendor answers no at the summary
- **THEN** nothing is signed or stored and the Licenses menu is shown

#### Scenario: Key saved to a file
- **WHEN** after the key string is shown the vendor saves it to `~/keys/acme-1042.txt`
- **THEN** the file holds the key string alone, and an existing file of that name is never
  replaced

#### Scenario: Wrong passphrase
- **WHEN** the passphrase given does not open the signing key
- **THEN** the tool says the passphrase is wrong and asks again (vendor-tool-signing-keys:
  Private keys encrypted with the passphrase), and nothing is stored until it is right

### Requirement: Choosing a license
Every question that asks for a license SHALL accept its reference, typed under the prefix rule,
or a whole key string. A key string SHALL be accepted when signed by any of the installation's
signing keys, expired or not; its reference chooses the license and nothing else from it is used
or kept. A key string not signed by the installation, or with no record, SHALL be refused and
the question asked again. Choosing a license SHALL NOT need the passphrase.

#### Scenario: Key pasted to reissue
- **WHEN** the vendor reissues and, at the license question, pastes the key string of
  `LIC-8F3AK-M7RXB` that the site owner sent
- **THEN** `LIC-8F3AK-M7RXB` is chosen, and the reissue's questions show the values from its
  record

#### Scenario: Key from before a prefix change
- **WHEN** after the prefix changed from `LIC` to `ACME`, the vendor shows a license by pasting a
  key string whose key identifier starts `LIC-8F3AK-M7RXB`
- **THEN** license `8F3AK-M7RXB` is shown

#### Scenario: Expired key pasted to renew
- **WHEN** the vendor renews by pasting a key string that expired last month and was signed by a
  key since replaced (routine)
- **THEN** the license is chosen and the renewal continues with its suggested expiry

#### Scenario: Key from another vendor
- **WHEN** the vendor pastes a key string signed by a key the installation does not hold
- **THEN** the tool says the key was not issued by this installation and asks again

#### Scenario: Key with no record
- **WHEN** the vendor pastes a key string signed by one of the installation's keys whose reference
  has no record
- **THEN** the tool says there is no record of that license and asks again

### Requirement: Key strings are not stored
The tool SHALL NOT store any license key string, in the database or elsewhere in the folder. A
record SHALL hold everything needed to reissue the license.

#### Scenario: Copied folder
- **WHEN** someone reads every file in the folder after licenses were issued
- **THEN** no license key string is found

### Requirement: Reissuing from the record
The vendor SHALL reissue a license from its record, keeping product and reference, and changing
any of expiry, display name, vendor tag and features. The display name and vendor tag SHALL
each be asked showing the current value: an empty answer SHALL keep it, a new value SHALL change
it, and a remove answer SHALL remove it, so the new key carries none. The reissue SHALL be
checked and signed as an issue, with the product's current active signing key, and SHALL add an
issue entry to the same license record. An order reference MAY be given for the new issue entry,
and the customer email MAY be changed at the same time. A renewal SHALL be recorded as a
renewal.

#### Scenario: Add-on bought
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB` adding switch `shipping`
- **THEN** a new key string with the same reference, the earlier features plus `shipping` and the
  current expiry is shown, and the record gains an issue entry

#### Scenario: Display name removed at reissue
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB`, which carries display name "Acme Shop EU", and
  answers remove for the display name
- **THEN** the new key carries no display name, and its features, expiry and vendor tag are
  unchanged

### Requirement: Switch values and features at reissue
At issue and at reissue, a switch answered `on` SHALL be granted and a switch answered `off`
SHALL NOT be: at issue it is left out of the key, as if not given; at reissue it is removed, the
same as removing the feature. Each feature SHALL be asked once per action, so a feature cannot be
given twice or both given and removed. At reissue each defined feature SHALL be asked showing its
current value; an empty answer SHALL keep it, a new value SHALL change it, and a remove answer
SHALL remove a number or text feature.

#### Scenario: Switch given as off
- **WHEN** the vendor issues `acme.commerce` with `ecommerce` off and `max-orders` 500
- **THEN** the key carries `max-orders` and no `ecommerce`, and the issue entry records the
  same features

#### Scenario: Features kept at reissue
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB`, which grants `ecommerce` and `max-orders` 500,
  giving an empty answer for each feature
- **THEN** the new key grants `ecommerce` and `max-orders` 500

#### Scenario: Number feature removed at reissue
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB` and answers remove for `max-orders`
- **THEN** the new key does not carry `max-orders`

#### Scenario: Switch turned off at reissue
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB`, which grants `ecommerce` and `shipping`, with
  `shipping` off
- **THEN** the new key grants `ecommerce` and not `shipping`, the same as removing `shipping`

### Requirement: Suggested expiry
When the vendor reissues a time-limited license as a renewal, the tool SHALL suggest as the new
expiry the current expiry plus the length of the last term. The last term SHALL be the length
the last renewal granted: from the later of the expiry before that renewal and the renewal's
issue time, to the expiry the renewal set. For a license never renewed it SHALL be from the
first issue's issue time to the expiry it set. Reissues that are not renewals SHALL NOT change
the term. The length SHALL be counted in calendar months when the last term was a whole number
of months (same day of month and time of day; a day past the end of a month counts as its last
day), and in exact time otherwise. For a reissue that is not a renewal it SHALL suggest the
current expiry. A perpetual license SHALL be suggested as perpetual. The suggestion SHALL be the
proposed answer to the expiry question; the vendor SHALL accept it with an empty answer or
replace it.

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
- **THEN** after the summary is confirmed, a new key string with the same reference and contents
  is shown, and the record gains an issue entry

### Requirement: Customer email and order reference values
A customer email SHALL be 3 to 254 characters of the characters the key's text rule allows, with
exactly one `@` that has text before and after it, and no spaces. An order reference SHALL be 1
to 64 characters of the characters the key's text rule allows. Each SHALL be stored exactly as
typed. A value outside its rule SHALL be refused naming the field and its rule, storing nothing.

#### Scenario: Accented email address
- **WHEN** the vendor issues a license with customer email `jürgen@müller.de`
- **THEN** the email is stored exactly as typed

#### Scenario: Email outside the rule
- **WHEN** the vendor gives customer email `owner`, `a@b@example.com` or `owner @example.com`
- **THEN** the tool refuses, naming the customer email rule, and stores nothing

#### Scenario: Order references from shops
- **WHEN** the vendor gives order reference `#1042`, `INV-2026-0042`, `ORD 1042`, `UMB:1042` or
  `pi_3NqXk2LkdIwHu7ix0yJ8`
- **THEN** each is accepted and stored exactly as typed

#### Scenario: Order reference outside the rule
- **WHEN** the vendor gives order reference `ORD-1042 ⭐` or one of 65 characters
- **THEN** the tool refuses, naming the order reference rule, and stores nothing

### Requirement: Finding and listing licenses
The vendor SHALL list licenses, filtered by any of product, reference (Choosing a license), order
reference (matching any issue entry) and customer email, each row showing
reference, product, current expiry, customer email and the latest issue entry's order reference.
The vendor SHALL show one license with every issue entry, newest first. Finding by customer email or
order reference SHALL follow Matching when finding.

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

### Requirement: Matching when finding
Finding by customer email or order reference SHALL match the whole value, ignoring case, and
SHALL treat the two forms of an accented letter (one character, or a letter and a combining
mark) as the same. Spaces in an order reference SHALL match exactly. A part of a value SHALL NOT
match.

#### Scenario: Email found ignoring case
- **WHEN** a license holds customer email `Owner@Example.com` and the vendor lists licenses for
  `owner@example.com`
- **THEN** that license is shown

#### Scenario: Accented letter typed in the other form
- **WHEN** a license holds `jürgen@müller.de` typed with `ü` as one character, and the vendor
  lists licenses for the same address typed with `u` and a combining mark
- **THEN** that license is shown

#### Scenario: Order reference found ignoring case
- **WHEN** a license was issued with order reference `ORD-1042` and the vendor lists licenses for
  `ord-1042`
- **THEN** that license is shown

#### Scenario: Part of an order reference
- **WHEN** a license was issued with order reference `ORD-1042` and the vendor lists licenses for
  `1042`
- **THEN** that license is not shown

#### Scenario: Spaces in an order reference
- **WHEN** a license was issued with order reference `ORD 1042` and the vendor lists licenses for
  `ORD  1042` (two spaces)
- **THEN** that license is not shown

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
