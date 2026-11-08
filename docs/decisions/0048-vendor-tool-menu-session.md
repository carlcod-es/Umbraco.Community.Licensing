# PDR-0048: The vendor tool runs as a menu session

- Status: Amended by PDR-0050
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q46 to Q48 and Q52 to Q58 (`docs/feature-requests.md`
  FR-23, taken into this change). Amends PDR-0030, PDR-0031, PDR-0034, PDR-0038, PDR-0039,
  PDR-0041, PDR-0044, PDR-0045.
- Personas: vendor (sets up products, features and several licenses in one sitting without knowing
  command names; a typing mistake costs one answer, not the action). Site owner (primary): a
  summary before signing catches a wrong expiry or missing feature before the key reaches them;
  keys unchanged. Implementor, backoffice editor, site visitor: unaffected.

## Context

PDR-0039 made the first version guided: a person at a terminal runs one command per action,
giving inputs on the command and being asked for the rest. A small vendor's usual sitting is
several jobs in a row (a product, its features, a few licenses), each a separate command with
its own passphrase entry. FR-23 asked for a startup menu instead (Carl, 2026-11-08).

## Decision

- **Menu session only.** `ucl` opens a session with a menu: Products, Licenses, Signing keys,
  Backup, Settings, Quit. The session returns to the menu after each action until the vendor
  quits. The tool takes no command other than help and version, and no input on the command
  (amends PDR-0039 "Arguments allowed" and "Redirected result").
- **Plain prompts.** Numbered menus and line-by-line questions. Selection lists, colour and a
  full-screen interface stay FR-13.
- **Nobody present.** The tool refuses, as in PDR-0039; help and the version still work.
- **No installation found.** The session names the folders checked and offers setup, restore
  from a backup, or quit. Setup or restore continues into the menu (amends PDR-0041 "None found"
  and "No command").
- **Second installation.** Settings offers a new installation in the current folder, by setup or
  restore, refused in the home folder or a folder holding one; the session then continues with
  it (amends PDR-0031).
- **Questions.** Each question says what it accepts and shows a proposed or current value, which
  an empty answer accepts. A broken rule is named and that question alone is asked again; the
  action's earlier answers are kept. Every question can cancel the action, storing nothing.
- **Optional inputs.** Each is asked in turn; an empty answer skips it. Features are asked one by
  one from the product's definitions, with their type. At reissue each shows its current value;
  empty keeps it. A feature cannot be given twice, so there is no contradiction to refuse (amends
  PDR-0044).
- **Suggested expiry.** It is the proposed answer to the expiry question (amends PDR-0034 and
  PDR-0039 "Suggested expiry").
- **Summary before signing.** Issue, reissue, renew and resend show a summary and ask whether to
  sign, yes proposed. No stores nothing.
- **Key out.** The key string is shown once on screen, then the tool offers to save it to a file
  the vendor names, never replacing a file (amends PDR-0045). Redirecting output is gone, so the
  Windows PowerShell 5.1 encoding note goes too (amends PDR-0038).

## Reasons

- One sitting, one passphrase entry (PDR-0049), and no command names to learn.
- Asking again rather than refusing keeps a long issue from being lost to one typo.
- The summary is the last point where a wrong key can be stopped before it reaches the site
  owner.
- Saving the key from inside the tool gives the same plain file on every terminal, which a
  redirect in Windows PowerShell 5.1 did not.

## Rejected options

| Option | Why rejected |
|---|---|
| Keep one command per run alongside the session | Two entry points to specify and test for every action (Carl, 2026-11-08) |
| Selection lists or a full-screen interface now | A new dependency and more terminal risk; FR-13 |
| Refuse the action on a bad answer | One typo loses every earlier answer |
| Ask whether to add optional details | Easier to forget the customer email or order reference |
| No confirmation before signing | A wrong key reaches the site owner and needs a reissue |
| Setup only at startup; restore from the Backup menu | Restore needs a folder with no installation, which an open session has not got |
| Key on screen only | Copying a long key from some terminals is error-prone |

## Consequences

- Scripted use is further away: FR-22 must add commands back as well as a passphrase source.
- The tool guide describes the menu, not commands; its command reference goes.
- Help lists what the menu offers rather than commands.
