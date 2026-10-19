# PDR-0026: A claimed product is reported only when it is a valid product ID

- Status: Decided
- Date: 2026-10-13
- Source: `add-license-core` design.md Q27
- Personas: site owner (logs and screens cannot be flooded or forged through a pasted key),
  implementor (a claimed product is safe to log as is), backoffice editor (no junk on the
  licensing screen). Vendor: no loss; their issuer never produces a product breaking the rule.
- Amends: PDR-0002

## Context

A failed result reports the product a key claims, read before verification. Anyone can write
any text there: up to about 24,000 characters, including line breaks and control characters.
Hosts log failed results and show them on licensing screens.

## Decision

A failed result reports the claimed product only when it meets the product ID rule (PDR-0004,
PDR-0017); otherwise it reports no claimed product. The state is unchanged: a key claiming an
invalid product is still *wrong product*. The claimed identifier is unaffected; it is reported
only when it matches the identifier pattern (PDR-0020).

## Reasons

- A genuine key always names a valid product, so nothing useful is lost.
- Cleaning moves from every host into the library, once.

## Rejected options

| Option | Why rejected |
|---|---|
| Report as is (PDR-0002) | Every host must sanitise it before logging or display |
| Truncate or escape it | Still forgeable text; shows a product no issuer made |
| A state for an invalid product claim | An extra state for what only an attacker or a broken string produces |

## Consequences

`ClaimedProduct` is null on a failed result whose product text breaks the product ID rule.
