## Purpose

Lets a vendor create and hold the signing keys that issue licenses, and lets a product trust
the matching public keys, including while a vendor rotates from one signing key to the next.

## ADDED Requirements

### Requirement: Create a signing key pair
The library SHALL create a signing key pair made of a private key, a public key and a signing
key ID. Each pair SHALL be new and independent of any earlier pair.

#### Scenario: New pair
- **WHEN** a vendor creates a signing key pair
- **THEN** the library returns a private key, a public key and a signing key ID

#### Scenario: Pairs are independent
- **WHEN** a vendor creates two signing key pairs
- **THEN** their public keys and their signing key IDs differ

### Requirement: Signing key ID is derived from the public key
A signing key ID SHALL be 11 characters from `A-Z a-z 0-9 - _`, computed from the public key
alone. The same public key SHALL always give the same ID. No caller SHALL be able to choose or
assign an ID. The private key SHALL report the same ID as its public key.

#### Scenario: Same key, same ID
- **WHEN** the ID of a public key is computed after exporting and re-importing that key
- **THEN** it equals the ID returned when the pair was created

#### Scenario: Private key knows its ID
- **WHEN** a private key is imported on its own
- **THEN** it reports the signing key ID of its public key

### Requirement: Export and import each half separately
The private key and the public key SHALL each export to text and import from that text. The
exported public key SHALL be one line of printable characters and SHALL carry its signing key
ID, so a product can trust it with no other input. Importing a public key whose carried ID does
not match the ID computed from the key SHALL fail with an error. Importing text that is not a
key of the supported kind SHALL fail with an error. Errors here are configuration errors, not
key-string content, and are raised to the caller.

#### Scenario: Public key round trip
- **WHEN** a public key is exported and the text is imported
- **THEN** the imported key has the same signing key ID and verifies licenses signed by the
  matching private key

#### Scenario: Private key round trip
- **WHEN** a private key is exported and the text is imported
- **THEN** licenses it signs verify with the original public key

#### Scenario: Carried ID altered
- **WHEN** an exported public key's ID part is changed and the text is imported
- **THEN** the import fails with an error naming the ID mismatch

#### Scenario: Not a key
- **WHEN** the text `Hunter2!` is imported as a public key or as a private key
- **THEN** the import fails with an error

#### Scenario: Public export holds no private material
- **WHEN** a public key is exported
- **THEN** the text cannot be imported as a private key

### Requirement: Trusted set of public keys
A product SHALL hold its trusted public keys as a set addressed by signing key ID. The set MAY
hold several keys. It MAY be empty while it is built; setting up evaluation with an empty set is
a configuration error (see license-validation). Adding a key already held SHALL leave the set unchanged and
raise no error. Adding a different key under an ID already held SHALL raise an error and leave
the set unchanged. Withdrawing an ID SHALL remove its key; withdrawing an ID not held SHALL
leave the set unchanged.

#### Scenario: Several keys
- **WHEN** a product trusts public keys A and B
- **THEN** licenses signed by either private key can be verified

#### Scenario: Same key twice
- **WHEN** public key A is added to a set that already holds A
- **THEN** the set still holds A once and no error is raised

#### Scenario: Different key under a held ID
- **WHEN** a key is added whose signing key ID is already held for a different key
- **THEN** an error is raised and the set is unchanged

### Requirement: Rotation
A vendor SHALL be able to rotate signing keys by trusting the new public key alongside the old
and switching issuing to the new private key. Routine rotation SHALL NOT require withdrawing the
old ID: its public key MAY stay trusted indefinitely. A vendor MAY withdraw an ID; the vendor
documentation SHALL reserve this for a compromised private key, followed by reissuing every key
it signed. Adding a key SHALL NOT change the evaluation of any license signed with a key
already trusted. A license signed by a withdrawn key SHALL evaluate as *signing key not
recognised*.

#### Scenario: Old and new trusted together
- **WHEN** a product trusts old key A and new key B
- **THEN** a license signed by A and a license signed by B both evaluate as valid (if not
  expired and for this product)

#### Scenario: Perpetual key after routine rotation
- **WHEN** a vendor has rotated from key A to key B without withdrawing A, and a perpetual license
  signed by A is evaluated
- **THEN** the state is *valid*

#### Scenario: Old key withdrawn
- **WHEN** key A is withdrawn and a license signed by A is evaluated
- **THEN** the state is *signing key not recognised*

### Requirement: Private key stays with the vendor
Nothing a product needs in order to validate licenses SHALL contain private key material. The
issuer SHALL NOT retain, cache or expose a private key after an issue call returns.

#### Scenario: Validation needs only public keys
- **WHEN** a product evaluates licenses
- **THEN** it does so holding only public keys
