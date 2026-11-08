# Vendor guide

For vendors who issue keys and check them in their products. Concepts and states:
[`how-it-works.md`](how-it-works.md).

> **Beta: do not issue beta keys to paying customers.** Reissue every key when moving to 1.0
> ([PDR-0024](decisions/0024-beta-without-stability-promise.md)).

## Create a signing key pair (once)

```csharp
using Umbraco.Community.Licensing;

using var pair = SigningKeyPair.Create();

string privatePem = pair.PrivateKey.ExportPem();   // store securely; never ship it
string publicKey  = pair.PublicKey.Export();       // "Nb_sm4Fxh5c.MFkwEwYH..." ship in the product
```

## Issue a license

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

### What a key can hold

| Field | Rule |
|---|---|
| Product ID | Two dot-separated parts of `a-z 0-9 -`, each starting with a letter; at most 64 characters (`acme.commerce`) |
| Display name | 1 to 64 characters, text rule |
| Vendor tag | 1 to 64 characters of `A-Z a-z 0-9 - _ . # /` |
| Feature name | 1 to 64 characters of `a-z 0-9 -`, starting with a letter; at most 50 features |
| Number value | Zero or positive, at most 4 decimal places and 15 digits |
| Text value | 1 to 256 characters, text rule |

**Text rule** ([PDR-0042](decisions/0042-text-rule-allows-characters-by-kind.md)): every
printable ASCII character, plus letters, accents, digits and punctuation from any script
(`Müller`, `Ακρόπολη`, `コマース`). Refused: emoji and other symbols outside ASCII (`⭐ ™ € ±`),
invisible characters, spaces other than the ordinary space, and leading or trailing
whitespace. The full table: [`key-format.md`](key-format.md#payload). A refused value is
rejected naming the field, with the first refused character's code and position, for example
`A display name contains a character that is not allowed: U+2B50 at position 5.`, followed by
what is allowed.

- **Beta keys:** releases up to 0.1.0-beta.6 refused only control characters, line breaks and
  leading or trailing whitespace. A key they issued holding a character now refused (an emoji, a
  symbol such as `™`, an invisible character, an unusual space such as the no-break space)
  reads as *not supported* on a site with a later release. Reissue it with the text changed
  (PDR-0024).
- **Allowed is not displayed:** a font or terminal without a script shows boxes or question
  marks. The key is still correct; check the text where the site owner will read it.

## Check a license in the product

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

## Reissue: add a feature or extend expiry

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

## Rotate a signing key

Trust the new public key alongside the old one in the next product release, then issue with the
new private key. Existing keys keep working.

```csharp
var trusted = TrustedSigningKeys.Create(oldPublicKey, newPublicKey);
```

Keep the old public key trusted after routine rotation; it costs nothing while its private key is
safe, and perpetual keys signed with it depend on it for good. Remove it
(`trusted.Without(oldKeyId)`) only if its private key is compromised. Every key it signed then
reports `SigningKeyNotRecognised`, including perpetual ones, and has to be reissued with the new
key ([PDR-0015](decisions/0015-signing-keys-and-rotation.md)).

## Vendor notes

- **No personal data in a key.** Keys are pasted into emails, tickets and configuration, and
  anyone can decode them. Keep customer names, emails and company names out of the display name
  and text features; the library cannot detect them. Use the vendor tag for your own customer ID
  (it refuses `@` and spaces) and keep the customer details in your own records
  ([PDR-0014](decisions/0014-personal-data-by-guidance.md)).
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
  `NotVerified` ([PDR-0023](decisions/0023-key-identifier-prefix.md)).
- **Grace after expiry is yours to add.** The library has no grace period (FR-9 in
  [`feature-requests.md`](feature-requests.md)). An `Expired` result still carries the verified
  license, but its lookups answer "not granted", so read `License.Features` directly:

  ```csharp
  var grace = TimeSpan.FromDays(14);
  var inGrace = result.State == LicenseState.Expired
      && result.License!.Expires is { } expires
      && timeProvider.GetUtcNow() <= expires + grace;

  bool HasSwitch(string name) => result.HasSwitch(name)
      || (inGrace && result.License!.Features.Any(f => f.Name == name && f.Value is SwitchFeature { IsOn: true }));
  ```

  Tell the site owner while a key is in grace; it is still `Expired`.
- **Evaluate on every request; no cache is needed.** A result is a snapshot: a cached result does
  not expire on its own, so a product that caches must re-evaluate once the clock passes the
  reported expiry.
- **Show the first action for every state**, including `NotSupported`, which a key issued by a
  newer library version reports on an older product. Render the state and its first action from
  the [states table](how-it-works.md#states); the library supplies the state, not message text.
