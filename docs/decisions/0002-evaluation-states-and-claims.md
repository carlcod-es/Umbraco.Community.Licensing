# PDR-0002: Evaluation states and what a result reports

- Status: Amended by PDR-0018, PDR-0020
- Date: 2026-10-09
- Source: `add-license-core` design.md R4, Q10
- Personas: implementor (which key, why, first action), site owner (what lapsed), site visitor
  (a bad key never crashes a page), backoffice editor (never sees a full key).

## Context

A product must decide whether it is licensed, and people must learn why not, from a key that
may be cut off, mistyped, for another product, signed by an unknown key, edited or expired.

## Decision

One result per evaluation, one state, first failing check wins:

```
     no key supplied                       -> missing
  1  cannot be read                        -> unreadable
  2  claims another product                -> wrong product
  3  signing key ID not trusted            -> signing key not recognised
  4  signature does not verify             -> not verified
  ---- verified from here: claims are facts ----
  5  past its expiry                       -> expired
     otherwise                             -> valid
```

Licensed only when valid. A verified key breaking a content rule (a faulty vendor tool) is
unreadable. A product sets up evaluation once with its product ID; an invalid product ID is the
product's own configuration error and raises an error at setup (design.md Q10). An evaluation
never throws, whatever the key string holds.

| State | Reports |
|---|---|
| Missing | The state only |
| Failed (1 to 4) | State; claimed product and claimed key identifier when readable, as claims. Never issue time, expiry, display name, vendor tag or features |
| Expired, valid | As facts: product, reference, identifier, signing key ID, issue time, expiry or perpetual, display name, vendor tag, features |

An expired key's features are reported so the site owner sees what lapsed; lookups still answer
not granted. No result contains any part of a key string other than its identifier.

| Reason | First action | Who |
|---|---|---|
| Missing | Install the key; ask the site owner for it | Implementor, site owner |
| Unreadable | Paste the key again | Implementor |
| Wrong product | Move the key to the product it names | Implementor |
| Signing key not recognised | Update the product; then ask the vendor | Implementor, vendor |
| Not verified | Paste again; then ask the vendor | Implementor, vendor |
| Expired | Renew | Site owner |

## Reasons

- An unverified claim may identify a key, so people can find it; it may never describe or grant
  an entitlement, so an edited key's claims appear nowhere.
- Reasons name what was observed, never a presumed cause: "tampered" is not used, because a
  lost character from a paste looks the same.
- The key is a bearer token; keeping it out of results keeps it out of logs and screens.
- Wrong product is checked before the signature so a key in the wrong slot is named as such.

## Rejected options

| Option | Why rejected |
|---|---|
| Throw on an invalid product ID at each evaluation | A shipped vendor bug would break every page that checks the license |
| A state for an invalid product ID | Every host must handle a state only a vendor bug produces |
| Several flags per result | More than one answer to "why not licensed?"; one state with a first action is simpler |
| Report all claims of a failed key | Lets an edited key show a fake expiry or features on a licensing screen |

## Consequences

Hosts render the state and its first action; the library supplies the state, not message text.
