# PDR-0044: A switch typed as off means not granted

- Status: Amended by PDR-0048, PDR-0064
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q41. Amends PDR-0034. Builds on PDR-0010.
- Personas: vendor (the obvious answer to "on or off?" works at the prompt and on the command).
  Site owner: unaffected; the key is the same as when the feature is left out. Implementor:
  unaffected; a switch is still present or absent.

## Context

A switch feature is granted when present in a key and not granted when absent; a key has no
"off" (PDR-0010). The tool takes switch values as `on` or `off`. At reissue, features carry over
from the stored record, and `--remove-feature` removes one.

## Decision

- `off` for a switch means not granted. At issue, the license is issued without that feature,
  as if it were left out. At reissue, `off` removes it, the same as `--remove-feature`.
- `--remove-feature` stays: it also removes number and text features, which have no `off`.
- One command giving the same feature twice, or giving a feature and removing it, is refused
  naming the feature, and nothing is issued or stored.

## Reasons

- Guided mode asks "on or off?" for a switch; refusing `off` would make the prompt's natural
  answer an error.
- The key is identical either way, so nothing differs for the site owner or implementor.
- A contradictory command has no safe reading; refusing it avoids guessing which the vendor meant.

## Rejected options

| Option | Why rejected |
|---|---|
| Refuse `off`; removal only with `--remove-feature` | A prompt whose obvious answer is refused; a refusal every time `off` is typed |
| Ignore `off` at reissue | The vendor asked for the feature to be off and the new key would still grant it |
| Last value wins when a feature is given twice | Silently picks one of two contradictory instructions |

## Consequences

- The tool guide says that `off` leaves the feature out of the key.
