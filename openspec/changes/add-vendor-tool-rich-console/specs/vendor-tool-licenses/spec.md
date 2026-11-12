## MODIFIED Requirements

### Requirement: Issuing a license
The vendor SHALL issue a license for a product, giving expiry (a date and time, or perpetual),
and optionally display name, vendor tag, features, order reference and customer email. Features
SHALL be checked against the product's feature definitions; the license's other fields by the
library's issue rules. The customer email SHALL be stored on the license and the order
reference on the issue entry. Issuing SHALL sign with the product's active signing key, use the
installation's current prefix, and need the passphrase. The tool SHALL ask for each optional
input in turn, an empty answer leaving it out; the product's switch features SHALL be chosen
together (Switch values and features at reissue), and each number and text feature definition
SHALL be asked in turn, showing its type. Before signing an issue, reissue or renewal, the tool
SHALL show a summary (product, reference when known, expiry, display name, vendor tag,
features, customer email, order reference) and offer Sign, proposed, Change answers and Cancel.
Change answers SHALL let the vendor choose several of the answers given, ask only those again
with the given value proposed, and show the summary again. Cancel SHALL store nothing and return
to the menu. The key string SHALL be shown once on screen; the tool
SHALL then offer to save it to a file the vendor names (vendor-tool-backup: Files named by the
vendor), the file holding the key string alone.

#### Scenario: License issued
- **WHEN** the vendor issues `acme.commerce` expiring 2027-03-01T23:59:59Z with `ecommerce` on and
  `max-orders` 500, order reference `ORD-1042` and customer email `owner@example.com`
- **THEN** the key string is shown, and a license record is stored with its reference, product,
  customer email and one issue entry holding the issue time, expiry, features, order reference,
  key identifier and signing key ID

#### Scenario: Optional inputs skipped
- **WHEN** the vendor issues `acme.commerce`, ticks no switch and gives an empty answer to every
  other optional input and feature
- **THEN** the summary shows no display name, vendor tag, features, customer email or order
  reference, and the license is issued without them

#### Scenario: Feature value outside its type
- **WHEN** the vendor answers `lots` for number feature `max-orders`
- **THEN** the tool names the number rule and asks for `max-orders` again

#### Scenario: Cancelled at the summary
- **WHEN** the vendor chooses Cancel at the summary
- **THEN** nothing is signed or stored and the Licenses menu is shown

#### Scenario: Answers changed at the summary
- **WHEN** at the summary of an issue expiring 2027-03-01 with no order reference, the vendor
  chooses Change answers, ticks expiry and order reference, answers 2027-06-01 and `ORD-1042`,
  then chooses Sign
- **THEN** only the expiry and order reference questions are asked again, each proposing its
  earlier answer, the summary shows the new values before signing, and the license is issued
  with them and every other answer unchanged

#### Scenario: Product not changed at the summary
- **WHEN** the vendor chooses Change answers at the summary
- **THEN** the product is not among the answers offered for change

#### Scenario: Key saved to a file
- **WHEN** after the key string is shown the vendor saves it to `~/keys/acme-1042.txt`
- **THEN** the file holds the key string alone, and an existing file of that name is never
  replaced

#### Scenario: Wrong passphrase
- **WHEN** the passphrase given does not open the signing key
- **THEN** the tool says the passphrase is wrong and asks again (vendor-tool-signing-keys:
  Private keys encrypted with the passphrase), and nothing is stored until it is right

### Requirement: Switch values and features at reissue
At issue and at reissue, the product's switch features SHALL be chosen together, several at once
(vendor-tool-setup: Several choices from a list): a ticked switch SHALL be granted and an
unticked switch SHALL NOT be: at issue it is left out of the key, as if not given; at reissue it
is removed, the same as removing the feature. At reissue the switches the license grants SHALL
start ticked. Each feature SHALL be asked once per action, so a feature cannot be given twice or
both given and removed. At reissue each number and text feature SHALL be asked showing its
current value; an empty answer SHALL keep it, a new value SHALL change it, and a remove answer
SHALL remove it.

#### Scenario: Switch left unticked
- **WHEN** the vendor issues `acme.commerce` leaving `ecommerce` unticked and giving `max-orders`
  500
- **THEN** the key carries `max-orders` and no `ecommerce`, and the issue entry records the
  same features

#### Scenario: Features kept at reissue
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB`, which grants `ecommerce` and `max-orders` 500,
  accepting the switches as ticked and giving an empty answer for `max-orders`
- **THEN** the new key grants `ecommerce` and `max-orders` 500

#### Scenario: Number feature removed at reissue
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB` and answers remove for `max-orders`
- **THEN** the new key does not carry `max-orders`

#### Scenario: Switch unticked at reissue
- **WHEN** the vendor reissues `LIC-8F3AK-M7RXB`, which grants `ecommerce` and `shipping`, and
  unticks `shipping`
- **THEN** the new key grants `ecommerce` and not `shipping`, the same as removing `shipping`

#### Scenario: Product with no switches
- **WHEN** the vendor issues a product that defines no switch feature
- **THEN** no switch list is shown, and the number and text features are asked in turn

### Requirement: Resending a license
The vendor SHALL resend a license, which reissues it from its record with nothing changed: same
reference, expiry, display name, vendor tag and features, new key string. The resend SHALL add
an issue entry with no order reference. Before signing a resend the tool SHALL show the summary
and offer Sign, proposed, and Cancel only. A license whose expiry has passed SHALL NOT be resent:
the tool SHALL refuse when it is chosen, before the summary, pointing to renew or reissue,
asking no passphrase and storing nothing.

#### Scenario: Site owner lost the key
- **WHEN** the vendor resends `LIC-8F3AK-M7RXB`
- **THEN** after Sign is chosen at the summary, a new key string with the same reference and
  contents is shown, and the record gains an issue entry

#### Scenario: Nothing to change at a resend
- **WHEN** the vendor resends `LIC-8F3AK-M7RXB` and reaches the summary
- **THEN** only Sign and Cancel are offered

#### Scenario: Resend of an expired license
- **WHEN** the vendor resends a license that expired last month
- **THEN** the tool says it cannot be resent and to renew or reissue it with a later expiry, asks
  no passphrase, stores nothing and shows the Licenses menu
