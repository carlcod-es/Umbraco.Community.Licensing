# PDR-0040: An installation moves between operating systems

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q34. Amends PDR-0031, PDR-0038.
- Personas: vendor (changes machine or operating system without starting again). Site owner
  (primary): the records of what they bought survive the move, and their keys are unchanged.
  Implementor: unaffected; the trusted public keys stay the same.

## Context

PDR-0031 makes an installation portable by copying its folder or restoring a backup "to another
machine". PDR-0038 makes the tool run on Windows, macOS and Linux. Neither said whether a folder
or backup made on one operating system works on another.

## Decision

- An installation moves between any of Windows, macOS and Linux, in either direction, by
  copying its folder or by restoring a backup made on the other operating system (Carl,
  2026-11-08).
- After the move every action works as before, and actions using a private key accept the same
  passphrase.
- Nothing in the installation is tied to the machine or operating system it was made on.

## Reasons

- Vendors change laptops and sometimes operating systems; a move that needs a new installation
  would mean new signing keys and a release of every product trusting them.
- The folder and backup are already self-contained (PDR-0031) and the private keys use a
  standard format opened with the passphrase alone (PDR-0032), so the same promise holds across
  operating systems.

## Rejected options

| Option | Why rejected |
|---|---|
| Moves only within the same operating system | A vendor changing operating system would start a new installation, with new signing keys and a release of every product |
| Keys tied to the machine (OS key store) | Cannot be moved by copying the folder (PDR-0031) |

## Consequences

- Verification includes a move between operating systems as well as within one.
- The tool guide says a folder or backup can be taken to any supported operating system.
