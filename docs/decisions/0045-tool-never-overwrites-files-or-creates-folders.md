# PDR-0045: The tool never overwrites a file or creates a folder it was not asked for

- Status: Decided
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q42. Amends PDR-0031.
- Personas: site owner (primary: an older backup can be the only copy of the records needed to
  reissue their key). Vendor: one predictable rule; a leak export or backup is never lost to a
  typo. Implementor: unaffected.

## Context

Three actions use a file the vendor names: the export by signing key and the backup export
write one; the backup import reads one. A person is always present (PDR-0039), so the tool can
always ask again.

## Decision

- **Output file exists:** the tool never replaces it. It says so, showing the full path, and
  asks for another name. When the name was given on the command, the action says the same and
  asks for another name at the prompt.
- **Output folder missing:** the tool refuses to create it, showing the full path, and asks for
  the path again.
- **Import file:** when the file does not exist or the path is a folder, the tool says so,
  showing the full path, and asks again. A file that is not a backup, or is from a newer tool
  version, is refused as already decided (PDR-0031).
- Setup's own folder (`.ucl` in the home folder, created when missing) is unchanged: setup is
  asked to create it.

## Reasons

- A backup taken before a mistake must not be replaced by one taken after it.
- During a leak the export is the list of customers to contact; losing it mid-incident is the
  worst time.
- A typo in a folder name (`Dropbx`) is caught instead of creating a folder nobody looks in, or
  one inside a synced folder the vendor did not mean.

## Rejected options

| Option | Why rejected |
|---|---|
| Ask "overwrite? y/N" | One keystroke replaces an older backup or a leak export |
| Overwrite | A backup or export can be lost without warning |
| Create a missing folder | Hides typos; may create a folder in a synced location |

## Consequences

- The vendor names a new file for each backup; the tool guide suggests the date in the name
  (`ucl-2026-11-08.zip`).
