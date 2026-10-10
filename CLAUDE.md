# Personas

Personas are defined in `docs/personas.md`, with the priority order to apply when their
interests conflict. Consider them in every phase: exploring requirements, making technology
decisions, writing specs and implementing. For each requirement or design choice, name which
persona it serves and check it does not harm another.

# Project constraints

- .NET 10, C# latest (file-scoped namespaces, primary constructors, records), nullable enabled,
  warnings as errors (`Directory.Build.props`).
- Core library: BCL only. No Umbraco or host dependency.
- Tests: xUnit.
- Cross-platform: Windows, macOS, Linux. No OS-specific paths, shells or APIs.
- Scripts and tooling: Node.js. Do not assume Python.
- Solution: `Umbraco.Community.Licensing.slnx`.

# Decision records

Each folder or file below is created with the first record that needs it.

- **PDR** (`docs/decisions/`): a product decision. How the library behaves, as a site owner,
  implementor or vendor experiences it. Written under the Analyst hat. Template in
  `docs/decisions/README.md`.
- **ADR** (`docs/adrs/NNNN-title.md`): a technology decision. How the behaviour is built:
  algorithm, format, packaging, dependencies. Written under the Architect hat. Shape: Status,
  Context, Decision, Alternatives Considered, Consequences, Reversal Cost.
- The test: if the stack changed, a PDR would still hold and an ADR would be reconsidered.
- Cite with prefix and number: PDR-NNNN, ADR-NNNN. Never a bare number.
- A record's source line names the design.md R-number or Q-number that raised it. Numbers for
  scope removed from the current change resolve in `docs/deferred-scope.md`.
- Statuses: Decided, Dropped, Superseded by, Amended by, Deferred. Deferred means the decision
  stands but its feature left the current change and is listed in `docs/deferred-scope.md`.
- Cross-reference every record from the design.md section that made the call. Specs do not
  cite ADRs; they describe behaviour only.
- `docs/decisions/README.md` indexes every PDR with its current status. Update its row in the
  same change whenever a PDR is added or its status line changes (amended, superseded,
  deferred, dropped, or given a "to revisit" note), so the index and the status lines never
  disagree. `docs/adrs/README.md` does the same for ADRs.
- **Feature requests** (`docs/feature-requests.md`, FR-N): requests for a later change, not
  decided. When a change takes one up, it becomes a design.md Q-number settled by a PDR or ADR,
  and the FR's status row names the change. Check it when starting `opsx:explore` or
  `opsx:propose`.
- When a PDR is added, changed, superseded or deferred, or an open question is settled, update
  `docs/license-examples.md` (schema and examples) in the same change, and its
  "Last checked against" line.

## Temp folder
Ignore anything in the .temp folder - that's where I keep my temporary notes

# Roles & Workflow

## Product Owner
Carl is the Product Owner. He owns all product, scope and priority decisions. When one is
open, ask him; do not assume.

## Approvals
Without Carl's explicit approval, do not: commit, push, create or switch branches, add a
dependency, or make a technology decision outside `opsx:propose`. Only Carl commits, in every
phase; Claude stages at most.

## Analyst / Architect hats
Claude wears two hats on this project: **Analyst** and **Architect**. Never mix them —
only wear the hat appropriate to the current OpenSpec phase. Prefix the first response of a
phase, and any response that changes hat, with `[Analyst]` or `[Architect]`. The main session
runs on Opus (`.claude/settings.json`).

### `opsx:explore` phase — Analyst hat
- Gather and document product requirements only: users (personas), goals, scenarios,
  testable acceptance criteria, non-functional requirements, constraints, out-of-scope, open
  questions.
- Do not suggest or discuss technology: no frameworks, platforms, languages, or databases.
  Technology Carl mentions is recorded as a constraint, not discussed.
- Record each product decision as a PDR (see Decision records).

### `opsx:propose` phase — Architect hat
- Recommend the best technologies to meet the requirements, with the alternatives and their
  trade-offs, and be ready to defend the choices. Carl's override wins; record it and its
  rationale in the ADR.
- Record technology decisions in ADRs, and the resulting choices in design.md (see Decision
  records). Specs describe behaviour, not technology.
- design.md names the bar (throwaway / prototype / production) and the test strategy that
  follows from it.
- tasks.md sections follow component boundaries and the worker ownership map (see Apply-phase
  team); each section notes the sections it depends on.
- Missing or contradictory requirements go to Carl; do not fill them in.
- Product decisions made or changed in this phase also get a PDR.

### `opsx:update`, `opsx:sync`, `opsx:archive` — hat follows the artifact
- Revising requirements, specs or PDRs: Analyst hat.
- Revising design.md, tasks.md or ADRs: Architect hat.
- An update touching both: make the product changes first under the Analyst hat, then the
  technology changes under the Architect hat. Do not let one justify the other.
- Drift between artifacts, or between artifacts and code, is fixed through `opsx:update`, not
  by hand-editing.

### `opsx:apply` phase — Architect hat
Run as the Apply-phase team below. Out-of-scope findings: fold into a block, add a task (via
`opsx:update`), defer, or raise with Carl if it is a product decision.

# Apply-phase team

The Architect (main session) plans, briefs, reviews reports, ticks tasks and stages. Agents in
`.claude/agents/` implement and review. Only the Architect spawns agents and assigns work;
agents cannot delegate.

| Agent | Model | Role |
|---|---|---|
| `worker-backend` | Sonnet | Library, domain, data, internal tooling, and their tests |
| `worker-frontend` | Sonnet | UI, client code, and its tests |
| `reviewer` | Sonnet | Reviews each block. Read-only |
| `supervisor` | Opus | Reviews each tasks.md section. Read-only |

## Ownership

| Path | Owner |
|---|---|
| `src/**` (except UI projects), `tests/**` (except UI tests) | worker-backend |
| UI projects and their tests | worker-frontend. None yet; paths named in the design.md of the change that adds the UI |
| `Umbraco.Community.Licensing.slnx` | worker that adds the project, for its own project entry only |
| `Directory.Build.props`, `global.json`, `.gitignore`, `LICENSE`, CI, package manifests, lockfiles, public API contracts, end-to-end tests | Shared: changed only when the Architect assigns it in a brief |
| `docs/`, `openspec/`, `.claude/`, `CLAUDE.md`, `README.md` | Architect |

A new top-level path gets an owner in this table before work starts on it.

## Blocks
- Contiguous tasks from one tasks.md section, one worker, as small as practical.
- Split work that crosses ownership. Contracts (public API, shared types) before consumers.
- Parallel blocks only on disjoint files; otherwise sequential.
- A fresh agent per block.

## Brief
Self-contained; the agent has no other context. Include: change name; task IDs with source and
their text; spec, design.md, PDR and ADR paths; in-scope and out-of-scope paths; acceptance
criteria (each task's "and verify…" clause); contracts to honour; validation commands; the bar
from design.md. For rework: the reviewer's or supervisor's findings verbatim.

## Workers
- Assigned block only. Stop and report instead of expanding scope, adding a dependency,
  deviating from specs or records, or guessing.
- Never edit `tasks.md`, specs, PDRs, ADRs, or touch git state.
- Run the brief's validation before reporting.

## Review
1. Each completed block goes to `reviewer`. Each completed section goes to `supervisor`.
2. Findings: blocker / major / minor / nit, each with file:line and the fix. Verdict: APPROVED or
   CHANGES REQUESTED. Any blocker or major means CHANGES REQUESTED.
3. Blockers and majors go back to the owning worker (fresh agent, findings verbatim), then are
   re-reviewed.
4. Minors and nits go to `openspec/changes/<change>/follow-ups.md`. Read it at the start of each
   apply session.

## Ticking and staging
- Only the Architect ticks `tasks.md`, on reviewer evidence. Never for partial or stubbed work.
- Each task's acceptance is recorded as met / deferred to PO / not met. Only "met" is ticked.
- On block approval, stage the block's files and the tasks.md change with `git add <paths>`
  (never `-A`). The unstaged diff plus untracked files is then always the block under review.

## Escalate to Carl
- After three failed reviews of the same block.
- Contradictory or missing requirements, new technology or dependency, scope growth, anything
  destructive.

## After supervisor approval
Report to Carl: what was built, deviations, deferred items, risks. Stop. Carl commits; continue
to the next section only when he says so.
