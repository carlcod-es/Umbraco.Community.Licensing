# PDR-0013: Reissue from the presented key

- Status: Amended by PDR-0021
- Date: 2026-10-09
- Source: `add-license-core` design.md R3, Q2
- Personas: vendor (sell add-ons and renewals with no records), site owner (gets one updated
  key).

## Decision

The library builds a reissue request from a verified license: product, reference, expiry
(perpetual stated explicitly), display name, vendor tag and features. The vendor edits any of
them except the product, then issues. Building reads no clock and checks nothing; issuing
checks as usual, so a prefilled past expiry is rejected, naming the expiry. A key that failed
verification offers no license to build from.

The risk of reissuing from an old key is handled by guidance only (Q2, decided 2026-10-09):
reissue from the key installed on the site, not one from an old email.

## Reasons

- The key is a bearer token: its holder already holds everything it grants. A reissue from it
  gives only that plus what was just bought. The reference alone is not proof.
- The library keeps no records, so it cannot know a presented key is outdated. Records belong to
  the deferred issuing add-on, which closes the gap.

## Rejected options

| Option | Why rejected |
|---|---|
| Reissue from the reference alone | The reference is not secret; anyone could claim any license |
| Detect an outdated key in the core | Needs records; the core keeps none |

## Consequences

A vendor reissuing from an old key (forwarded from an old email) produces a key without features
bought since; once installed it replaces the newer key. Vendor documentation states the
guidance.
