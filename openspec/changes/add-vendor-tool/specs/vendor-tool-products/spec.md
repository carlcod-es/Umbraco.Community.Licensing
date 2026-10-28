## Purpose

Holds each of the vendor's products and its feature definitions, so every key the tool issues is
checked against what the product reads.

## ADDED Requirements

### Requirement: Adding a product
The vendor SHALL add a product with a product ID (by the library's product ID rule) and a display
name. A product ID SHALL be unique in the installation and SHALL never change. In per-product
signing-key mode, adding a product SHALL create its signing key, and its standby key when
standby keys are on; doing so needs the passphrase. In shared mode the product SHALL use the
shared signing key.

#### Scenario: Product added in per-product mode
- **WHEN** the vendor adds `acme.commerce` with display name "Acme Commerce" and gives the
  passphrase, with standby keys on
- **THEN** the product is stored with a new signing key and a new standby key of its own

#### Scenario: Duplicate product ID
- **WHEN** the vendor adds `acme.commerce` a second time
- **THEN** the tool refuses, naming the product ID, and changes nothing

#### Scenario: Invalid product ID
- **WHEN** the vendor adds `Acme Commerce`
- **THEN** the tool refuses, naming the product ID rule

### Requirement: Feature definitions are add-only
The vendor SHALL add feature definitions to a product: a feature name (by the feature name rule)
and a type (switch, number, text). A feature definition SHALL never be removed, renamed or
change type. A product's display name SHALL be editable.

#### Scenario: Feature added
- **WHEN** the vendor adds number feature `max-orders` to `acme.commerce`
- **THEN** new and reissued keys for `acme.commerce` may carry `max-orders` as a number

#### Scenario: Feature already defined
- **WHEN** the vendor adds text feature `max-orders` to `acme.commerce`, which already defines it
  as a number
- **THEN** the tool refuses, naming the feature and its existing type

#### Scenario: Display name edited
- **WHEN** the vendor changes the display name of `acme.commerce` to "Acme Commerce Pro"
- **THEN** the product shows the new display name; its product ID and features are unchanged

### Requirement: Listing products
The vendor SHALL list products, each with its product ID, display name, feature definitions and
number of licenses.

#### Scenario: Products listed
- **WHEN** the installation holds `acme.commerce` and `acme.shipping`
- **THEN** the list shows both with their display names, features and license counts
