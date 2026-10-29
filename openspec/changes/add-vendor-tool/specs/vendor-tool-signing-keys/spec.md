## Purpose

Keeps the vendor's private signing keys safe, supports standby keys and switching to a new
signing key, and tells the vendor which public keys each product release must trust.

## ADDED Requirements

### Requirement: Private keys encrypted with the passphrase
Every private signing key, active, standby or replaced, SHALL be stored encrypted with the
installation's passphrase, in a standard encrypted private key format that other tools can open
with the passphrase. Nothing else SHALL be encrypted. An action that signs or creates a private
key SHALL ask for the passphrase once per run; no other action SHALL ask for it. The tool SHALL
NOT keep the passphrase or a decrypted private key after an action ends. The tool SHALL offer no
way to export a private key.

#### Scenario: No passphrase for reading
- **WHEN** the vendor lists licenses or prints trusted keys
- **THEN** no passphrase is asked

#### Scenario: Opened by another tool
- **WHEN** a stored private key is opened with a standard cryptography tool and the passphrase
- **THEN** it yields the private key; without the passphrase it does not

### Requirement: Standby keys
When standby keys are on, each signing key SHALL have a standby key pair that signs nothing.
The vendor SHALL be able to add a standby key later to a signing key that has none. A standby key
SHALL be marked "not yet in a release" until the vendor confirms a product release trusts it.
The vendor SHALL list products with their signing key, whether it has a standby key, and whether
that standby key is in a release, and SHALL be able to list only products without a standby key.

#### Scenario: Standby added later
- **WHEN** the vendor adds a standby key to the signing key of `acme.commerce`, which has none
- **THEN** a standby key is created, marked "not yet in a release", and the product no longer
  appears in the list of products without a standby key

#### Scenario: Products without a standby key
- **WHEN** `acme.commerce` has a standby key and `acme.shipping` has none
- **THEN** the list of products without a standby key shows `acme.shipping` only

### Requirement: Switching to a new signing key
The vendor SHALL switch a signing key, giving a reason: routine, loss or leak. The standby key,
when there is one, SHALL become the active key and a new standby key SHALL be created; otherwise
a new key pair SHALL become the active key. The replaced key SHALL stay in the records with its
reason and date and SHALL sign nothing more. In shared mode the switch SHALL apply to every
product. When the key becoming active is a standby key not yet in a release, the tool SHALL warn
that sites on releases that do not trust it will report keys signed by it as signed by a key not
recognised, and ask for confirmation. The switch needs the passphrase.

#### Scenario: Routine rotation with standby
- **WHEN** the vendor switches `acme.commerce` from active key A with standby S, reason routine
- **THEN** S becomes active, a new standby key T is created, A is recorded as replaced (routine)
  and new keys are signed with S

#### Scenario: Standby not yet in a release
- **WHEN** the vendor switches to a standby key marked "not yet in a release"
- **THEN** the tool warns and asks for confirmation before switching

### Requirement: Trusted public keys per product
The vendor SHALL print, for a product, the public key lines its next release must trust, one per
line, each preceded by a comment line naming its role (active, standby, replaced with reason). The
list SHALL hold the active key, the standby key, and every replaced key except one replaced
after a leak whose reissues the vendor has confirmed sent. A key replaced after a leak SHALL stay
listed, marked as leaked, until the vendor confirms the reissues are sent. The vendor SHALL
confirm that a product release trusts the printed list, which marks every key in it as in a
release.

#### Scenario: After routine rotation
- **WHEN** `acme.commerce` has active S, standby T and A replaced (routine)
- **THEN** the list holds A, S and T

#### Scenario: During a leak
- **WHEN** A was replaced after a leak and the reissues are not confirmed sent
- **THEN** the list holds A (marked leaked), S and T

#### Scenario: After the reissues are sent
- **WHEN** the vendor confirms the reissues of licenses signed by A are sent
- **THEN** the list holds S and T only

#### Scenario: Release confirmed
- **WHEN** the vendor confirms a release of `acme.commerce` trusts the printed list holding
  standby T
- **THEN** T is no longer marked "not yet in a release"

### Requirement: Export by signing key
The vendor SHALL export, for a chosen signing key, one row per issue entry signed by it: reference,
key identifier, product, issue time, expiry. No key strings. Rows SHALL include issues since
superseded by a reissue and issues already expired. The export SHALL be a file the vendor names,
readable by spreadsheet tools.

#### Scenario: Leak export
- **WHEN** the vendor exports for leaked key A, which signed two issues of `LIC-8F3AK-M7RXB` and
  one of `LIC-2V5C9-HKD3P`
- **THEN** the file holds three rows with reference, key identifier, product, issue time and
  expiry, and no key string
