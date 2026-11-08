## MODIFIED Requirements

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

#### Scenario: Text breaking the text rule
- **WHEN** a key signed by a trusted key holds a display name `Pro ⭐`, or a text feature with
  a zero-width space, such as a key issued under an earlier beta
- **THEN** the state is *not supported*, with its claimed product and identifier

#### Scenario: Text in any script
- **WHEN** a key signed by a trusted key holds the display name `コマース Pro`
- **THEN** the display name is reported unchanged
