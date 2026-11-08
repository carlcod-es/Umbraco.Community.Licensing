## ADDED Requirements

### Requirement: Text rule
A display name and a text feature value SHALL follow one text rule: printable ASCII, space
included, and outside ASCII only letters, combining marks, decimal digits and punctuation from
any script, as one fixed Unicode version defines them, less what it marks ignorable by default.
Every other character SHALL be refused, as SHALL leading or trailing whitespace. The same text
SHALL get the same answer on every machine.

#### Scenario: Any script accepted
- **WHEN** the display name is `Ακρόπολη Pro`, `コマース Pro`, `Łódź Edition` or
  `Commerce Pro – Édition`
- **THEN** it is accepted and signed unchanged

#### Scenario: Printable ASCII unchanged
- **WHEN** a text feature is `eu+uk`, `a=b`, `<none>` or `$100 | ~50%`
- **THEN** it is accepted and signed unchanged

#### Scenario: Emoji and symbols refused
- **WHEN** the display name is `Pro ⭐`, `Pro™`, `Pro ©`, `Pro €` or `Pro ±`
- **THEN** the request is rejected naming `displayName`

#### Scenario: Emoji building blocks refused
- **WHEN** the display name holds a keycap sequence such as `1️⃣`, a variation selector, or a
  skin-tone modifier
- **THEN** the request is rejected naming `displayName`

#### Scenario: Invisible characters refused
- **WHEN** a text feature holds a zero-width space, a zero-width joiner or non-joiner, a
  text-direction override (U+202E) or a soft hyphen
- **THEN** the request is rejected naming the feature

#### Scenario: Invisible letters and marks refused
- **WHEN** the display name holds a Hangul filler (U+3164) or a combining grapheme joiner
  (U+034F)
- **THEN** the request is rejected naming `displayName`

#### Scenario: Other spaces refused
- **WHEN** the display name holds a no-break space, an ideographic space, or an en or em space
- **THEN** the request is rejected naming `displayName`

#### Scenario: Repeated ASCII spaces accepted
- **WHEN** the display name is `Commerce  Pro` with two ASCII spaces
- **THEN** it is accepted and signed unchanged

#### Scenario: Numbers that are not digits refused
- **WHEN** a text feature is `①`, `m²`, `½` or `Ⅻ`
- **THEN** the request is rejected naming the feature

#### Scenario: Unassigned and private-use characters refused
- **WHEN** the display name holds a character the rule's Unicode version does not assign, or a
  private-use character
- **THEN** the request is rejected naming `displayName`

#### Scenario: Both forms of an accented letter accepted as typed
- **WHEN** one request has the display name `Café` with `é` as one character, and another has
  `e` followed by a combining accent
- **THEN** both are accepted, each signed exactly as supplied

#### Scenario: Same answer on every machine
- **WHEN** the same request is issued on Windows, macOS and Linux
- **THEN** it is accepted on all three, or rejected on all three with the same problems

## MODIFIED Requirements

### Requirement: Display name rule
A display name, when given, SHALL be 1 to 64 characters following the text rule. It SHALL be
signed exactly as supplied and never trimmed or cleaned; a breaking value is rejected naming
`displayName`.

#### Scenario: Valid display name
- **WHEN** the display name is `Commerce Pro`, `Commerce Pro – Édition` or `Ακρόπολη Pro`
- **THEN** it is accepted and signed unchanged

#### Scenario: Invalid display name
- **WHEN** the display name is empty, `" Pro"`, contains a line break, has 65 characters, or is
  `Pro ⭐`
- **THEN** the request is rejected naming `displayName`

### Requirement: Feature rules
A feature SHALL be a name and a typed value: switch, number or text. A request SHALL hold at
most 50 features. A name SHALL be 1 to 64 characters of lowercase `a-z`, digits and hyphens,
starting with a letter, and SHALL appear at most once per request. A
switch SHALL be granted by being present; an explicit `false` SHALL be rejected. A number SHALL
be zero or positive, with at most 4 decimal places and at most 15 digits in total; it SHALL be
signed without trailing fractional zeros. A text value SHALL be 1 to 256 characters following
the text rule; it is never interpreted. Each problem SHALL name `features.<name>`, or `features`
when the name itself is invalid or repeated or when there are too many features.

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

#### Scenario: Text value breaking the text rule
- **WHEN** a text feature is `licensed-domains: "shop.example.com ✓"`
- **THEN** the request is rejected naming `features.licensed-domains`

#### Scenario: Too many features
- **WHEN** a request holds 51 features
- **THEN** the request is rejected naming `features`; with 50 it is accepted

### Requirement: Key string shape
A key string SHALL be one line of printable ASCII characters with no whitespace, beginning with
its key identifier. Editing any character of it, including changing the case of the
identifier, SHALL make it fail evaluation. A key string SHALL be at most 32,767 characters.
When every other rule passes and the key string would be longer, the request SHALL be rejected
naming `features`, stating how many characters over the limit it is, and no key string SHALL be
produced. Reissue SHALL be checked the same way.

#### Scenario: Pasteable key
- **WHEN** a key is issued with a display name containing non-ASCII letters
- **THEN** the key string still contains only printable ASCII and no whitespace, and starts with
  its key identifier

#### Scenario: Key too long
- **WHEN** a request is otherwise valid and holds 50 text features, each 256 Japanese
  characters such as `コ`
- **THEN** the request is rejected naming `features`, stating how far over 32,767 characters the
  key would be, and no key is produced

#### Scenario: Large license fits
- **WHEN** a request has a 64-character product ID, display name and vendor tag, and 50 text
  features with 64-character names and 256-character values of letters and digits
- **THEN** a key string of at most 32,767 characters is produced, and it evaluates as *valid*
