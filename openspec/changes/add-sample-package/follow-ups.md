# Follow-ups: add-sample-package

Minors and nits from reviews. Read at the start of each apply session.

## Section 1 (supervisor, 2026-11-13)

- **minor** (fixed in CLAUDE.md; design.md still to align via `opsx:update`): design.md:122
  says `Client/**` first creation is template output and every later edit is worker-frontend's,
  but design.md:228-229 has section 2 (worker-backend) apply the template patches. CLAUDE.md
  now says the patches are part of worker-backend's scaffold output; mirror that in
  design.md:122. Section 2 brief states it explicitly.
- **nit**: CLAUDE.md UI row mixes the generic rule with a change-specific provenance note.
  Move the note to design.md once a second UI project exists.
- **risk for section 7**: worker-frontend edits `Client/package.json` and the lockfile; the
  brief must repeat that any new npm package needs Carl's approval.
