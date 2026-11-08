# PDR-0049: Passphrase kept for a session, with an idle lock

- Status: Amended by PDR-0050, PDR-0052
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q49 to Q51. Amends PDR-0032, PDR-0039.
- Personas: vendor (one passphrase entry for a sitting of several signing actions; their own
  choice of how long an unattended terminal stays unlocked). Site owner (primary): an unattended,
  unlocked session is the one new route to forged keys, and the idle lock closes it by default.
  Implementor, backoffice editor, site visitor: unaffected.

## Context

PDR-0032 asks for the passphrase once per action and keeps it no longer. It rejected remembering
the passphrase across actions because that needs a cache on disk or a background process. In a
menu session (PDR-0048) the actions run in one process, so the passphrase can be held in memory
for the session with neither. The new risk is a session left open at an unattended terminal.

## Decision

- **When asked.** At the first action in a session that signs or creates a key. Reading, exports,
  backups and settings still need none.
- **Kept.** In the session only, never on disk, and forgotten at Quit (amends PDR-0032 "Not kept
  after an action ends" and PDR-0039 "Passphrase typed": still typed at a hidden prompt, now once
  per session).
- **Idle lock.** After a set time with no answer typed, the session forgets the passphrase and
  every decrypted private key; the next action that needs it, including one in progress, asks
  again. Setup asks for the time: whole minutes from 1 to 1440, or `never`, with 15 proposed.
  Settings shows and changes it without the passphrase; a change takes effect from the next
  session.
- **Wrong passphrase.** Said to be wrong and asked again, up to three tries, keeping the action's
  other answers. After the third the action is cancelled, nothing is stored, and the menu is
  shown.

## Reasons

- The passphrase per action was the price of one command per run; a session removes the reason.
- The vendor knows their own setting: a home office and a shared space need different times.
- Three tries covers mistyping without inviting guessing at the prompt; offline guessing of a
  copied folder is unaffected either way (PDR-0032).

## Rejected options

| Option | Why rejected |
|---|---|
| Ask at session start | A session that only lists licenses or takes a backup would need it |
| Fixed idle time | Vendors' settings differ (Carl, 2026-11-08) |
| `never` not allowed | The vendor's choice; 15 minutes stays the proposed value (Carl, 2026-11-08) |
| Idle time fixed at setup | Changing it needs no secret and harms no one |
| Cancel the action on one wrong try | A typo loses every answer of the action |

## Consequences

- With `never`, an unlocked session at an unattended terminal can sign until it is closed. The
  guide says so and advises quitting when leaving the terminal.
- A decrypted private key is held in memory for as long as the session is unlocked.
