# PDR-0052: Menu session review refinements

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q67 to Q70. Amends PDR-0031, PDR-0034, PDR-0048,
  PDR-0049. Builds on PDR-0050.
- Personas: vendor (a pasted key finds its license; the idle lock does not fire while they are
  working; no installation lost in the home folder). Site owner (primary): the key they send back
  is enough for the vendor to help; an installation restored from a backup cannot be signed with
  a passphrase held from another one. Implementor, backoffice editor, site visitor: unaffected.

## Context

An `opsx:explore` review of the menu session (PDR-0048) found four gaps: a session that moved to
another installation by restore said nothing about the passphrase it already held; the idle lock
counted "answers" without saying whether a menu choice is one; an installation directly in the
home folder was warned about at the startup offer but refused from Settings and Backup, with no
reason for the difference; and the tool asked for a license's reference although the usual
request from a site owner is their key, pasted into an email.

## Decision

- **Another installation.** When the session continues with an installation restored from a
  backup (startup offer, Backup, Settings), it forgets the passphrase it holds; the next action
  that needs one asks for it. A new installation made by setup keeps the passphrase just set
  (PDR-0050). Amends PDR-0048 "Second installation" and PDR-0049 "Kept".
- **Idle lock.** Anything typed restarts the idle time, menu choices included: the session locks
  after the set time with nothing typed. Amends PDR-0049 "Idle lock".
- **Home folder.** An installation never sits directly in the home folder. When the session
  starts in the home folder, the startup offer proposes only `.ucl` there, for setup and for
  restore. Settings and Backup keep refusing the home folder. Replaces PDR-0031's home-folder
  warning.
- **Reference or key.** Every question that asks for a license (reissue, renew, resend, show,
  customer details, and the reference filter of find and list) accepts the reference, typed under
  the prefix rule (PDR-0023), or a whole key string.
  - A key string is accepted when it is signed by one of the installation's signing keys:
    active, standby or replaced for any reason, expired or not. Only its reference is used: the
    license is reissued from the record (PDR-0034), and nothing from the key is kept (PDR-0033).
  - It is refused when not signed by any of the installation's keys ("not issued by this
    installation"), or when signed by one but its reference has no record (for example a license
    issued before the tool, FR-16).
  - No passphrase is needed. Amends PDR-0034.

## Reasons

- Another installation may use a different passphrase; holding the old one only causes a
  failure at the next signing.
- The lock is for an unattended terminal; someone choosing from menus is at it.
- Files loose in the home folder are easy to delete or sweep into a synced folder, and a session
  started there would use them instead of `.ucl`. `.ucl` is always on offer, so refusing costs
  nothing.
- Site owners send their key, not a reference; picking the reference out by hand is slow and,
  after a prefix change, the old prefix is refused (PDR-0023). A signed key proves its reference,
  so accepting it whole needs no relaxation of the typed-reference rule.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep the held passphrase after a restore | The restored installation may use another passphrase; the next signing fails for a reason the vendor cannot see |
| Only answers to an action's questions restart the idle time | A vendor reading menus and lists is locked out while at the terminal; harder to explain |
| Warn, then allow, an installation in the home folder everywhere | Allows something no persona needs |
| Keep warning at startup and refusing elsewhere | Two rules for one risk, with no reason |
| Reference only | The vendor extracts it from the key by hand; trips over a prefix change |
| Pasted key as a later feature request | Leaves the proposal's "reissue from the key a site owner sends back" unmet in this change |

## Consequences

- The guide says that pasting the key the site owner sent is enough to find the license.
- A key issued by the library outside the tool and signed with a key the tool holds is refused
  as having no record until FR-16.
- A vendor who wants an installation beside other files in their home folder uses a subfolder.
