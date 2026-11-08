# Implementor guide

For whoever installs license keys on a site and fixes licensing problems. Every state and its
first action: [`how-it-works.md`](how-it-works.md#states).

- **Install the key in configuration**, in every environment (local, staging, production). A
  renewal or add-on takes effect only when the new key is installed.
- **One state, one first action.** Act on the state, not a guessed cause. A character lost in a
  paste can show as `Unreadable` or `NotVerified`; both say paste the key again first.
- **A failed key shows only claims.** For any state other than `Valid` and `Expired`,
  `ClaimedKeyIdentifier` and `ClaimedProduct` are what the key says about itself, unverified,
  and either can be null. `WrongProduct` does not guarantee a claimed product: a broken or forged
  key may claim a product that is not a valid product ID, and then none is reported. An edited key
  cannot fake an expiry or features on a licensing screen.
- **`NotSupported` right after upgrading past 0.1.0-beta.6, on a beta key.** Those releases
  refuse emoji, symbols outside ASCII, invisible characters, unusual spaces such as the no-break
  space, and other characters (table: [`key-format.md`](key-format.md#payload)) in the display
  name and text features. The product is already updated, so go straight to the second action:
  ask the vendor to reissue the key.
- **`Expired` reports what lapsed.** `result.License` holds the verified expiry and features, so
  the site owner can see what to renew. Lookups still answer "not granted".
- **The key identifier is safe to log and quote.** The full key string is not: it is a bearer
  token. `LicenseResult.ToString()` and `VerifiedLicense.ToString()` print the identifier only.
- **Compare key identifiers across environments** to find one running an older key; after a
  prefix change, compare the reference part (`LIC-8F3AK-M7RXB` and `ACME-8F3AK-M7RXB` are the
  same license).
- **Keep keys out of source control** and out of screens shown to editors; whoever holds a key
  can use it.
