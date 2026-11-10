# PDR-0059: Clearing customer details asks first

- Status: Decided
- Date: 2026-11-10
- Source: `add-vendor-tool` design.md Q79 (raised by the section 8 supervisor review). Amends
  PDR-0033.
- Personas: vendor (a wrong menu number does not destroy a customer's email or order
  references). Site owner: an erasure they asked for is still honoured once the vendor
  confirms. Implementor, backoffice editor, site visitor: unaffected.

## Context

Customer details lets the vendor clear a license's customer email, or clear the order
references of every issue entry of a license at once (PDR-0033). Clearing leaves no copy in the
installation, so it cannot be undone; only backups taken earlier still hold the values. Clearing
asked no question, so one wrong menu number lost the vendor's only contact or order link for
that customer. Elsewhere the tool asks before steps it cannot undo: signing and restore.

## Decision

- Clear the customer email shows the license and the email, then asks
  `Clear this customer email? It cannot be undone. [y/N]`, no proposed.
- Clear every order reference shows the license and each issue entry's order reference, then
  asks `Clear these order references? They cannot be undone. [y/N]`, no proposed.
- No, or a cancel, changes nothing and returns to the menu.
- Yes clears as before.
- Changing the email and correcting the latest order reference ask nothing: the vendor types the
  new value.

## Reasons

- The values cannot be brought back from the installation, so the vendor sees them before they
  go.
- No is proposed so that an Enter typed out of habit does not erase anything.
- Typing a new value is already deliberate; a question there would only slow the vendor down.

## Rejected options

| Option | Why rejected |
|---|---|
| Clear with no question | One wrong menu number erases data the vendor cannot get back |
| Yes proposed | An Enter typed out of habit would erase |
| Ask on change and correct as well | The typed value is already deliberate |

## Consequences

- An erasure takes one more answer.
- The guide shows both questions and says that older backups still hold cleared values.
