# Umbraco Licensing

Offline license keys for paid Umbraco packages: one library to issue keys, check them inside a
product, and manage the signing keys behind them.

> **Beta: no stability before 1.0.** Any 0.x release may change the key format, the public API,
> or both. A key issued under a beta may read as `NotSupported` or `Unreadable` under a later
> version. **Do not issue beta keys to paying customers. Reissue every key when moving to 1.0.**
> From 1.0 the key format is permanent.
> ([PDR-0024](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/decisions/0024-beta-without-stability-promise.md))

## Why

Umbraco package vendors have no shared way to license their work. Each vendor builds its own
implementation of licensing keys or ships unprotected, and every site owner and agency learns a
different one per product. This library gives every package the same scheme:

- **Offline.** A key is checked entirely on the site. No licensing server, no phone-home, no
  network call, nothing to go down.
- **Signed.** A key cannot be edited or forged without the vendor's private signing key.
- **One key per product.** Each key names the product it unlocks
  ([PDR-0001](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/decisions/0001-one-key-per-product.md)).
- **Same answers everywhere.** Every product reports the same set of states with the same first
  action, so a site owner holding keys from several vendors fixes them all the same way.
- **Never breaks a page.** Checking a key never throws on key content. A bad key is a state, not
  an exception.

Built first for the **site owner** who buys licenses, then the implementor who installs them,
then the vendor who issues them
([personas](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/personas.md)).

The package is called `Umbraco.Community.Licensing.Core` because its audience is Umbraco
package vendors. It has no Umbraco or host dependency and uses only the .NET base class library.

## How it works

```
  VENDOR (own tooling)                          PRODUCT (on the site owner's site)
  ---------------------------                   ----------------------------------
  signing key pair                              public key(s) compiled into product
    private key: kept secret  ---public key---> trusted signing keys
                                                       |
  LicenseRequest --Issue--> key string --email--> site owner --> implementor
                            (signed)                           puts it in config
                                                                     |
                                                                     v
                                                  LicenseEvaluator.Evaluate(key)
                                                       |
                                                       v
                                                  state + features
                                                  (Valid, Expired, Missing, ...)
```

1. **The vendor creates a signing key pair once.** The private key stays with the vendor. The
   public key ships inside the product.
2. **The vendor issues a key** for a product with an expiry and optional features. The library
   signs it (ECDSA P-256) and returns a single-line key string.
3. **The site owner receives the key** and hands it to the implementor, who places it in the
   site's configuration.
4. **The product evaluates the key** on startup or per request, against its own product ID and
   the public keys it trusts, and gates features on the result.

### The key string

```
LIC-8F3AK-M7RXB-7Q2D.eyJrIjoiTmJfc200RnhoNWMiLCJwIjoiYWNtZS5jb21tZXJjZSIs....qp39tt3VnjxW0cO1...
|------------------| |---------------------------------------------------|   |--------------|
 key identifier       payload (base64url JSON)                                signature
 prefix-reference-key part
```

- **Key identifier:** readable and safe to show, log and quote in support emails.
  - `LIC` is the default prefix. A vendor can set its own prefix, such as `ACME`, or none.
  - `8F3AK-M7RXB` is the reference part; with the prefix it forms the reference
    (`LIC-8F3AK-M7RXB`). It stays the same when the license is reissued.
  - `7Q2D` is the key part. It is new at every issue.
- **Payload:** the signing key ID, the product, issue time, expiry, an optional display name and
  vendor tag, and the features. No personal data: no customer name, email or company.
- **Signature:** covers the identifier and the payload together.

The string survives being pasted into an email and fits in an environment variable. Whitespace
is removed before the key is read, including Unicode whitespace such as non-breaking spaces
(U+00A0) and line separators (U+2028, U+0085) that editors and mail clients insert.

Full schema and worked examples, with real key strings:
[`docs/license-examples.md`](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/license-examples.md).

### Features

A key carries zero or more named features. The product decides what each one means.

| Type | Example | Lookup |
|---|---|---|
| Switch | `ecommerce` | `HasSwitch("ecommerce")` |
| Number | `max-orders: 2500` | `GetNumber("max-orders")` |
| Text | `licensed-domains: "example.com,shop.example.com"` | `GetText("licensed-domains")` |

Lookups answer only on a **valid** key, and only for a feature of the matching type. Anything
else answers "not granted": `false` for a switch, `null` for a number or text.

### States

An evaluation returns exactly one state. The first check that fails decides it.

| State | Meaning | First action |
|---|---|---|
| `Missing` | No key configured | Install the key; ask the site owner for it |
| `Unreadable` | Not a key, or damaged | Paste the key again |
| `WrongProduct` | Key is for another product | Move the key to the product it names |
| `SigningKeyNotRecognised` | Signed by a key this product does not trust | Update the product; then ask the vendor |
| `NotVerified` | Signature does not match | Paste again; then ask the vendor |
| `NotSupported` | Genuine key this product version cannot read | Update the product; then ask the vendor |
| `Expired` | Genuine, past its expiry | Renew |
| `Valid` | Genuine, in date, for this product | None |

Only `Valid` is licensed.

## Examples

### Create a signing key pair (vendor, once)

```csharp
using Umbraco.Community.Licensing.Core;

using var pair = SigningKeyPair.Create();

string privatePem = pair.PrivateKey.ExportPem();   // store securely; never ship it
string publicKey  = pair.PublicKey.Export();       // "Nb_sm4Fxh5c.MFkwEwYH..." ship in the product
```

### Issue a license (vendor)

```csharp
using var signingKey = SigningPrivateKey.FromPem(privatePem);
var issuer = new LicenseIssuer();                  // prefix LIC; or KeyPrefix.Of("ACME"), or KeyPrefix.None

var request = new LicenseRequest
{
    Product     = "acme.commerce",
    Expiry      = LicenseExpiry.On(new DateOnly(2027, 3, 1)),   // valid through 2027-03-01T23:59:59Z
    DisplayName = "Commerce Standard",
    VendorTag   = "CUST-0042",
    Features    = FeatureList.Empty
        .With("ecommerce", true)
        .With("max-orders", 500m),
};

try
{
    IssuedLicense issued = issuer.Issue(request, signingKey);
    // issued.KeyString     -> send to the customer
    // issued.KeyIdentifier -> "LIC-8F3AK-M7RXB-7Q2D", store for support
    // issued.Reference     -> "LIC-8F3AK-M7RXB", reuse to reissue
}
catch (LicenseIssueException ex)
{
    foreach (var problem in ex.Problems)            // every problem at once, e.g. "features.max-orders: ..."
        Console.WriteLine($"{problem.Field}: {problem.Message}");
}
```

The issuer keeps no records. Storing what was issued, and to whom, is up to the vendor.

### Check a license in the product

Create the evaluator once at startup. It throws there if the product ID is invalid or the
trusted set is empty, never later.

```csharp
var trusted = TrustedSigningKeys.Create(
    SigningPublicKey.Parse("Nb_sm4Fxh5c.MFkwEwYH..."));

var evaluator = new LicenseEvaluator("acme.commerce", trusted);
```

Evaluate per request or on startup. The call is cheap (under 1 ms for a typical key) and never
throws.

```csharp
LicenseResult result = evaluator.Evaluate(configuration["Acme:Commerce:LicenseKey"]);

if (result.HasSwitch("ecommerce"))
{
    var maxOrders = result.GetNumber("max-orders") ?? 50;   // not granted -> product's free limit
    // ...
}

if (!result.IsLicensed)
{
    // Show result.State and its first action to the site owner or implementor, for example
    // "Expired: renew". result.ToString() gives state and identifier, never the key text.
    logger.LogWarning("Commerce license: {Result}", result);
}

if (result.State == LicenseState.Valid && result.License!.Expires is { } expires)
{
    // e.g. warn in the backoffice when fewer than 14 days remain
}
```

What a site visitor sees when a license fails, such as checkout off or a backoffice warning only,
is the vendor's decision. The library reports the state and does not act on it.

### Reissue: add a feature or extend expiry (vendor)

The site owner sends the key installed on the site. The vendor evaluates it with the vendor's own
evaluator, then issues a replacement. The product and reference stay the same and the key part
changes.

```csharp
var current = vendorEvaluator.Evaluate(keyFromCustomer);   // Valid or Expired carries License

var reissue = ReissueRequest.From(current.License!) with
{
    Expiry   = LicenseExpiry.On(new DateOnly(2028, 3, 1)),
    Features = current.License!.Features.With("ai-assist", true),
};

IssuedLicense replacement = issuer.Issue(reissue, signingKey);   // LIC-8F3AK-M7RXB-<new key part>
```

The reissued key takes the issuer's prefix, so a vendor who moves from `LIC` to `ACME` gets
`ACME-8F3AK-M7RXB-...` with the same reference part.

### Rotate a signing key (vendor)

Trust the new public key alongside the old one in the next product release, then issue with the
new private key. Existing keys keep working.

```csharp
var trusted = TrustedSigningKeys.Create(oldPublicKey, newPublicKey);
```

Keep the old public key trusted after routine rotation; it costs nothing while its private key is
safe, and perpetual keys signed with it depend on it for good. Remove it
(`trusted.Without(oldKeyId)`) only if its private key is compromised. Every key it signed then
reports `SigningKeyNotRecognised`, including perpetual ones, and has to be reissued with the new
key ([PDR-0015](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/decisions/0015-signing-keys-and-rotation.md)).

## Vendor notes

- **No personal data in a key.** Keys are pasted into emails, tickets and configuration, and
  anyone can decode them. Keep customer names, emails and company names out of the display name
  and text features; the library cannot detect them. Use the vendor tag for your own customer ID
  (it refuses `@` and spaces) and keep the customer details in your own records
  ([PDR-0014](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/decisions/0014-personal-data-by-guidance.md)).
- **One key per product.** Sell every change (add-on, capacity, upgrade, renewal, refund) as a
  reissue of that key. Merge a duplicate purchase into one key and refund the other.
- **An add-on sold separately** (for example through a channel that cannot reissue) is its own
  product with its own product ID and key.
- **Reissue from the key installed on the site**, not one from an old email. An older key may
  lack a feature or capacity sold since.
- **A renewal or add-on takes effect only when the new key is installed**, in every environment.
  Compare key identifiers across environments to find one running an older key; after a prefix
  change, compare the reference part.
- **Text values are never interpreted.** A list, such as licensed domains, is your convention
  (for example comma-separated), split by your product. Adding or removing a value is a reissue.
- **Expiry.** A date alone (`LicenseExpiry.On`) ends at 23:59:59 UTC that day. A key issued in
  error stays valid until its expiry wherever it is installed.
- **The prefix is branding only.** `ACME-` grants and protects nothing. A license is protected by
  your private signing key and the trusted set shipped in your product; an edited prefix reads
  `NotVerified`
  ([PDR-0023](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/decisions/0023-key-identifier-prefix.md)).
- **Evaluate on every request; no cache is needed.** A result is a snapshot: a cached result does
  not expire on its own, so a product that caches must re-evaluate once the clock passes the
  reported expiry.
- **Show the first action for every state**, including `NotSupported`, which a key issued by a
  newer library version reports on an older product. Render the state and its first action from
  the table above; the library supplies the state, not message text.

## Implementor notes

- **One state, one first action.** Act on the state, not a guessed cause. A character lost in a
  paste can show as `Unreadable` or `NotVerified`; both say paste the key again first.
- **A failed key shows only claims.** For any state other than `Valid` and `Expired`,
  `ClaimedKeyIdentifier` and `ClaimedProduct` are what the key says about itself, unverified,
  and either can be null. `WrongProduct` does not guarantee a claimed product: a broken or forged
  key may claim a product that is not a valid product ID, and then none is reported. An edited key
  cannot fake an expiry or features on a licensing screen.
- **`Expired` reports what lapsed.** `result.License` holds the verified expiry and features, so
  the site owner can see what to renew. Lookups still answer "not granted".
- **The key identifier is safe to log and quote.** The full key string is not: it is a bearer
  token. `LicenseResult.ToString()` and `VerifiedLicense.ToString()` print the identifier only.

## Limits of offline licensing

These come with checking keys entirely on the site:

- **No revocation.** A key that was issued stays valid until it expires. The only way to stop it
  is to withdraw the signing key that signed it, which stops every key signed with that key.
- **No binding.** A key is not tied to a domain or machine. A key can carry a `licensed-domains`
  text feature, but the product has to enforce it.
- **The site's clock is trusted.** Setting the clock back delays expiry.
- **A key is a bearer token.** Whoever holds it can use it. Keep it in configuration, out of
  source control and out of screens shown to editors.
- **The product is trusted.** Anyone who can modify the product's code can remove the check.

## Documentation

| Path | Contents |
|---|---|
| [`docs/license-examples.md`](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/license-examples.md) | Key schema and worked examples |
| [`docs/personas.md`](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/personas.md) | Who the library serves, in priority order |
| [`docs/decisions/`](https://github.com/carlcod-es/Umbraco.Community.Licensing/tree/main/docs/decisions) | Product decision records (PDR) |
| [`docs/adrs/`](https://github.com/carlcod-es/Umbraco.Community.Licensing/tree/main/docs/adrs) | Architecture decision records (ADR) |
| [`openspec/`](https://github.com/carlcod-es/Umbraco.Community.Licensing/tree/main/openspec) | Specifications and changes (source of truth for behaviour) |

## License

MIT. See [`LICENSE`](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/LICENSE).
