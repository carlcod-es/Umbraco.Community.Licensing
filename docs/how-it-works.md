# How it works

What the library does, what a key holds, what a product learns from it, and what offline
licensing cannot do. Code examples: [`vendor-guide.md`](vendor-guide.md). The format byte for
byte: [`key-format.md`](key-format.md).

## Why

Umbraco package vendors have no shared way to license their work. Each vendor builds its own
implementation of licensing keys or ships unprotected, and every site owner and agency learns a
different one per product. This library gives every package the same scheme:

- **Offline.** A key is checked entirely on the site. No licensing server, no phone-home, no
  network call, nothing to go down.
- **Signed.** A key cannot be edited or forged without the vendor's private signing key.
- **One key per product.** Each key names the product it unlocks
  ([PDR-0001](decisions/0001-one-key-per-product.md)).
- **Same answers everywhere.** Every product reports the same set of states with the same first
  action, so a site owner holding keys from several vendors fixes them all the same way.
- **Never breaks a page.** Checking a key never throws on key content. A bad key is a state, not
  an exception.

Built first for the **site owner** who buys licenses, then the implementor who installs them,
then the vendor who issues them ([personas](personas.md)).

## The flow

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

## The key string

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

Worked scenarios with real key strings: [`license-examples.md`](license-examples.md).

## Features

A key carries zero or more named features. The product decides what each one means.

| Type | Example | Lookup |
|---|---|---|
| Switch | `ecommerce` | `HasSwitch("ecommerce")` |
| Number | `max-orders: 2500` | `GetNumber("max-orders")` |
| Text | `licensed-domains: "example.com,shop.example.com"` | `GetText("licensed-domains")` |

Lookups answer only on a **valid** key, and only for a feature of the matching type. Anything
else answers "not granted": `false` for a switch, `null` for a number or text.

## States

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
