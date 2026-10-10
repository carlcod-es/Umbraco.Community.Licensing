# Personas

The people who touch, or are touched by, a license built with this library. Use them to test
requirements and designs: every behaviour should serve at least one persona and harm none.

**Primary customer: the site owner.** When personas' interests conflict, the site owner's
experience wins, then the implementor's, then the vendor's.

```
  VENDOR --issues key--> SITE OWNER --hands key--> IMPLEMENTOR --installs--> SITE
  (builds product)       (buys, pays)              (builds site, configures)    |
                                                                                v
                                              BACKOFFICE EDITOR, SITE VISITOR
                                              (use the product; never manage keys)
```

| Persona | Relationship to the license |
|---|---|
| Vendor | Issues keys, builds products, gates features |
| Site owner (primary) | Buys licenses, receives keys, needs to know what they have |
| Implementor | Installs keys, configures the site, fixes licensing problems |
| Backoffice editor | Uses the product in the backoffice; may see warnings, never manages keys |
| Site visitor | Uses the product's public output; must never see licensing |

## Vendor

Builds products on this library and sells licenses for them.

- **Issues licenses** when a site owner buys one. Keys are sent to the site owner, typically by
  email, so a key must survive being pasted into an email and back out.
- **Builds products** that use the library for licensing. Signs with a private key per product,
  or one shared across their products, which the vendor must keep safe. It is never shipped inside the product.
- **Gates features at runtime.** Uses license features as feature flags, including limits such
  as `max-orders`, and decides what the product does when a license is missing or invalid.

## Site owner (primary customer)

Buys licenses for products, from any source: vendor store, marketplace or reseller. The
library must not assume where a purchase happened.

A license can be for:

- a full product
- an add-on for a product
- extra capacity for a product

Needs to know: what am I licensed for, what is about to expire, and what is missing. Is often
not the person who installs the key; they pass it to the implementor.

## Implementor

Builds and develops the site owner's website using the vendor's product, and installs the
licenses the site owner supplies. May be an agency or a freelance developer, and may look after
many sites.

Needs to know: which configured key is failing, why, and where to fix it. Works across
environments (local, staging, production), where keys are deployed through configuration
rather than entered by hand.

## Backoffice editor

Uses the product day to day inside the Umbraco backoffice. Has no role in licensing.

May see licensing warnings (e.g. "expires in 14 days") if the product or a licensing screen
shows them. Must never be able to read a full license key, because keys are bearer tokens.

## Site visitor

Uses the public website and the product's public output (forms, checkout, search). Has no
interaction with licensing and no awareness of it.

- Must never see license keys, license status or licensing messages.
- A licensing problem (expired, malformed, missing key) must never crash a page. The library
  reports problems; it does not throw in the request path.
- What the visitor experiences when a license fails is the vendor's decision (e.g. keep taking
  orders and warn the owner, or disable checkout), not the library's.
