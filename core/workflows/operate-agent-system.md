# Operate Agent System

Operate a generated prompt pack through Architect → agents → validation → integration.

Deep playbook: [../../references/source-prompts/04-operate-agent-system.md](../../references/source-prompts/04-operate-agent-system.md)
**If the deep playbook conflicts with this file, this file wins.**

Cursor host details (AskQuestion, Task tool names, `subagent_type` values): see
[`.cursor/skills/operate-agent-system/SKILL.md`](../../.cursor/skills/operate-agent-system/SKILL.md)
and [`.cursor/rules/operate-native-subagents.mdc`](../../.cursor/rules/operate-native-subagents.mdc).

## Entry menu

When `/operate` is invoked without a concrete request, do not assume the user
wants implementation.

### Choice UI

- Prefer the host IDE’s native structured-question or multiple-choice UI so
  options are clickable. Do not print the text menu first when that UI works.
- Put the recommended option first and label it `(Recommended)` only when the
  saved project state supports the recommendation.
- If no native choice tool is available, use the text fallback below.

### Text fallback

```text
What would you like the agent system to do?

A. Create an implementation plan only
   Produces milestones, dependencies, risks, and approval gates. Does not modify
   application files.

B. Create a repository task mapping
   Connects each implementation task to its owning agent, affected projects,
   folders or files, required contracts, dependencies, execution order, and
   validation gates. For Greenfield projects it proposes the initial repository
   structure; for Brownfield/Hybrid projects it maps work to verified existing
   paths. It does not implement features or modify application files.

C. Implement the next approved milestone
   The Architect selects the next milestone, prepares delegations, and performs
   authorized repository changes through the relevant agents.

D. Execute a specific request
   You describe the feature, defect, migration, or review. The Architect
   analyzes impact and delegates only the required work.

E. Resume or review existing work
   Reads current handoffs, blockers, validation reports, and approval status,
   then recommends the next action.

Reply with A, B, C, D, or E.
```

Mark one option as `Recommended` with one short reason only when project state
supports it:

- New Greenfield project with no implementation plan: recommend **A**.
- Existing repository with an approved specification but no task mapping:
  recommend **B**.
- Approved plan and no blocking decisions: recommend **C**.
- User already stated a concrete request: skip the menu and use **D**.
- Existing handoffs or interrupted work: recommend **E**.

If the context does not support a recommendation, omit it.

## Context resolution (for non-standard locations)

Before executing any option, the Architect must resolve where the
agent-system context artifacts live in the current repository.

Priority:

1. Prefer refreshed mapping if present:
   `agent-system/context-index.yaml`.
   Use it to locate the approved specification, governance artifacts,
   agent prompts, and protocols.

2. Prefer canonical paths if they exist:
   `agent-system/` with `project-specification.md`, `governance/`, `agents/`,
   and `protocols/`.
3. Otherwise, use the evidence-based path mapping recorded by discovery
   (from the final specification) to locate:
   - the approved project specification,
   - governance/contract artifacts,
   - agent prompt files.
4. If discovery mapping is missing or incomplete, scan the repository
   read-only for likely artifacts (by filename and directory patterns),
   and auto-select matches when unambiguous. Ask the user only if a required
   artifact cannot be found confidently or multiple candidates exist.

Then proceed using an in-memory “context index”:
`spec_path`, `shared_context_path`, `agents_dir`, and any required
governance/protocol paths.

## Library vs pack version check

Before options A–E do substantive work:

1. Resolve library `VERSION` (policy `library_root`, resolved `core/` sibling,
   or app-root `VERSION` — same order as `/upgrade-architect`).
2. Read `library_version` from `agent-system/manifest.yaml` when present.
3. If the library version is **newer** than the pack’s `library_version`, or
   the pack omits `library_version` while a library `VERSION` exists, warn once
   and recommend **`/upgrade-architect`** before large implementation (C/D).
4. Do not block read-only options A/B/E solely for a version mismatch; still
   surface the warning.

## Existing operating procedures (before plan or implement)

After context resolution and before options A–E do substantive work, resolve
adopted project procedures per
[existing-operating-procedures.md](existing-operating-procedures.md):

1. Read `existing_operating_procedures` from shared context when present.
2. If missing, re-scan using the reference signal catalog (read-only).
3. If adaptation mode is `FOLLOW`, `COMPOSE`, or `BRIDGE`, require specialist
   agents to honor the project's workflow chain, domain memory paths, and
   tracker binding before applying Architect defaults.
4. Do not invent a competing clarify/spec/ticket loop when the project already
   defines one.
5. Do not overwrite project skills, host instructions, `CONTEXT.md`, or ADRs
   unless the user explicitly authorizes that change.

## Result of each option

### A — Implementation plan only

1. The Architect reads the approved specification and governance files.
2. It creates `agent-system/implementation-plan.md` with milestones,
   dependencies, risks, parallel work, approval gates, and completion criteria.
3. It does not modify application files.
4. It marks the plan `PROPOSED — APPROVAL REQUIRED` and waits for approval.
5. After approval:
   - recommend **B** when repository locations are unknown or need confirmation;
   - recommend **C** when the next milestone and repository scope are already
     clear.

### B — Repository task mapping

1. The Architect inspects the repository read-only.
2. It creates `agent-system/repository-task-mapping.md`.
3. Each task is mapped to an owning agent, actual/proposed paths, contracts,
   dependencies, execution order, validation, and approval gates.
4. It does not implement features.
5. It marks the mapping `PROPOSED — APPROVAL REQUIRED` and waits for approval.
6. After approval, recommend **C**.

### C — Implement the next approved milestone

1. Requires an approved implementation plan or another clearly approved
   milestone definition.
2. Resolve the task id. If `handoffs/active/<task-id>/` (or that task’s
   scratch) already has operate data, run the **Same-task restart gate**
   before any resume or new delegation. Do not assume resume or wipe.
3. The Architect selects the next unblocked milestone and creates task
   delegations (one envelope per primary agent). Every C/D envelope **must**
   set `scratch_dir: agent-system/scratch/<TASK-ID>/` and write handoffs under
   `agent-system/handoffs/active/<TASK-ID>/` (see **Handoffs and scratch**).
4. Before trusting prior `READY` handoffs (only if the user chose Resume),
   run **READY reconciliation**.
5. Specialist work runs through the **delegation runtime** below — not by the
   Architect implementing those roles itself.
6. Agents return thin YAML/MD handoffs; reviewers validate required evidence.
7. The Architect performs integration review, then **Close** (cleanup gate)
   and records one final status: `REQUEST COMPLETE`,
   `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS`, or `REQUEST BLOCKED`.
8. It then offers **C** again for the next milestone or **E** to review status.

If no approved milestone exists, do not implement; direct the user to **A**.

### D — Execute a specific request

1. If the request was not supplied, ask the user to describe it.
2. The Architect analyzes scope, affected contracts, agents, and approvals,
   and resolves the task id.
3. If that task id already has data under `handoffs/active/<task-id>/` or
   `scratch/<task-id>/`, run the **Same-task restart gate** before continuing.
   A new chat/thread alone does **not** imply wipe or resume.
4. It delegates only the requested work via the **delegation runtime**, with
   mandatory `scratch_dir` and active handoff path for the task id.
5. Implementation occurs only when the request authorizes file changes.
6. Handoffs, READY reconciliation (when resuming), validation, integration
   review, **Close**, and final status use the same rules as option C.

### E — Resume or review existing work

1. Read implementation plans, task mappings, and operate state from:
   - `agent-system/handoffs/active/<task-id>/` (current handoffs / blockers)
   - `agent-system/handoffs/archive/<task-id>/summary.yaml` (closed requests)
   - validation reports referenced by those packages
2. Do **not** treat `agent-system/scratch/` as resume input.
3. If active data exists and the user wants to continue implementation (not
   status-only), run the **Same-task restart gate** — do not silently resume.
4. Reconcile any `READY` handoff against the working tree (see
   **READY reconciliation**) before recommending resume or skip-diagnosis.
5. Return a concise status: completed, in progress, blocked, and next eligible
   action.
6. Do not modify application files (except Close wipe when the user chose
   Fresh start).
7. Present the relevant next choice (usually **C**, **D**, or an approval action).

## Authorization behavior

- A and B must **not** modify application source. They **may** write proposed
  planning artifacts under `agent-system/` (`implementation-plan.md`,
  `repository-task-mapping.md`) marked `PROPOSED — APPROVAL REQUIRED`.
- C and D may modify application files only when the user's request clearly
  authorizes implementation.
- E is read-only unless the user subsequently authorizes a resumed change.
- Installing dependencies, running builds/tests, accessing external systems,
  and deploying require the authorization rules of the host environment.

## Delegation runtime (how each agent gets purpose + task)

The prompt pack defines **who** each agent is. The Architect generates **what**
each agent must do for this request. The host must inject both into an isolated
run whenever the host supports it.

### Purpose vs task

| Artifact | Role |
|---|---|
| `agents/<agent>.md` + `governance/agent-registry.yaml` | Standing **purpose**: mission, ownership, exclusions, gates |
| `protocols/task-delegation.yaml` instance | This-request **task**: objective, scope, contracts, DoD |
| `protocols/agent-handoff.yaml` instance | Result returned to the Architect |

Registry **agent id** (e.g. `principal-software-architect`) may differ from
**prompt filename** (e.g. `agents/00-principal-architect.md`). Use both from
the registry. See [../glossary.md](../glossary.md).

Do not invent new host Task *kinds*. Reuse the host’s generic subagent/Task
types and **inject** the fleet agent prompt + delegation envelope into each run.

### Hosts with native isolated subagents (required when available)

For options **C** and **D** (and any authorized specialist work under `/operate`):

1. Parent chat = `principal-software-architect` only: plan, select agents,
   write/save delegation envelopes, integrate handoffs, run approval gates.
2. Parent must **not** implement work owned by another registered agent
   (architecture-engineer, backend, frontend, UX, PM, QA, etc.) inside the
   parent turn. When the request is architecture design, redesign of an
   existing feature, or a new feature that needs unsettled structural /
   contract choices, select `architecture-engineer` (or the pack's equivalent
   design owner) **before or alongside** feature implementers; Principal still
   orchestrates and integrates.
3. For each selected primary agent, launch an isolated subagent/Task run:
   - Prefer a general-purpose implementation/design runner; use an explore /
     read-only runner only for research.
   - Title/description should include the fleet agent id
     (e.g. `backend-engineer: M2 domain model`).
   - Prompt must include, in order:
     1. Exact text (or path + instruction to read) of that agent’s
        `agents/<file>.md`
     2. The filled `task-delegation.yaml` for this task (unchanged bounds),
        including mandatory `scratch_dir`
     3. Paths to `shared-context.yaml` and any required contracts
     4. Authorization bounds from the user
     5. Required return: thin agent-handoff package under
        `handoffs/active/<task-id>/` + status (`READY` / `BLOCKED` / …)
     6. Scratch rule: all probes, builds, temp publish dirs, and disposable
        files go under `scratch_dir` — never under `handoffs/`
4. Launch independent agents in parallel when the plan marks them unblocked
   and non-conflicting; otherwise run sequentially.
5. After each run returns, the Architect validates the handoff (and runs
   READY reconciliation when status is `READY`), then continues (next agent,
   QA reviewer run, or integration review).
6. After integration status is set, run **Close**.
7. If isolated subagents are unavailable, say so once, then fall back to
   **Separate chats** (preferred) or **Single chat** (last resort), and record
   the fallback in the final status notes.

Host-specific tool names and parameters belong in IDE adapters (e.g. Cursor
`.cursor/`), not as hard requirements of this portable workflow.

### Fallback hosts

1. **Native multi-agent** — use the host’s equivalent of Task/subagents with
   the same inject pattern (agent prompt + delegation YAML).
2. **Separate chats** — one chat per agent; paste the same two artifacts.
3. **Single chat** — Architect simulates one agent at a time (weakest
   isolation; allowed only when 1–2 are impossible).

## Handoffs and scratch (durable fleet vs ephemeral operate)

Separate **durable** prompt-pack material from **per-task operate** working
files. Never mix build trees into handoffs.

### Layout

```text
agent-system/
  agents/ governance/ protocols/ …   # durable fleet — keep
  handoffs/
    active/<task-id>/                # thin YAML/MD handoffs only
    archive/<task-id>/summary.yaml   # after Close (short retention record)
  scratch/<task-id>/                 # probes, builds, temp — disposable
```

| Kind | Location | Lifecycle |
|---|---|---|
| Prompt pack / governance | `agent-system/` (agents, contracts, templates, approved spec) | Keep |
| Active handoffs | `handoffs/active/<task-id>/` | Stay while `READY` / `BLOCKED` |
| Scratch / probes / builds | `scratch/<task-id>/` | Delete on Close |
| Closed request record | `handoffs/archive/<task-id>/summary.yaml` | Keep short summary only |

Do **not** put operate scratch under `docs/architecture/handoffs/` — that path
is for intentional migration notes, not disposable debug trees.

### Legacy operate piles (pre-layout / unclean trees)

Users who already generated handoffs before this layout (or mixed builds into
operate folders) are **not** stuck. On `/operate` or `/upgrade-architect`,
detect legacy material and handle it with an explicit user choice — never
silent delete of thin YAML the user may still want.

**Scan for (examples):**

- Loose `*handoff*.yaml`, `*delegation*.yaml`, validation notes at
  `agent-system/` root or `agent-system/handoffs/*.yaml` (not under
  `active/<task-id>/`)
- `agent-system/debug/`, `debug/<issue-id>/`, or similarly named disposable
  trees
- `bin/`, `obj/`, `node_modules/`, coverage, `build-out/`, publish dirs under
  any handoff/debug path
- Orphan `scratch/<old-id>/` or active folders for issues the user is no
  longer driving

**Do not treat as legacy operate junk:** durable pack files (`agents/`,
`governance/`, protocol *templates*, approved spec, plans/mappings), or
intentional notes under `docs/architecture/handoffs/`.

**Ask once (group by task/issue id when possible):**

```text
Found existing operate data for <task-id or "unscoped pile">.

M1. Keep as active (migrate into handoffs/active/<task-id>/)
    Move thin YAML/MD only; then Same-task restart gate (Resume vs Fresh)
    applies on the next run for that id.

M2. Archive + clean (recommended if the issue is done or abandoned)
    Write handoffs/archive/<task-id>/summary.yaml (status + PR + limitations);
    delete scratch/debug/build dumps for that id; clear migrated active.

M3. Delete without archive (only if user confirms nothing useful remains)
```

Always **delete build trees and large dumps** regardless of M1–M3 (they never
belong under handoffs). Promote any still-useful finding into Jira / ADR / PR
before M2/M3 when the user cares about it.

After migration, new runs use only `handoffs/active|archive` + `scratch/` and
the Same-task restart gate.

### Handoff package rules

- YAML/MD packages only under `handoffs/`.
- No `bin/`, `obj/`, `node_modules/`, coverage dumps, `build-out/`, or other
  large binary/tree dumps under `handoffs/`.
- Nest versions under `active/<task-id>/` (e.g. `handoff-backend-v2.yaml`).
  Do not leave a pile of sibling `*-v2.yaml` at the `handoffs/` root.
- `artifacts:` entries are **paths or URLs outside handoffs** (repo paths,
  `scratch/<task-id>/…`, PR URLs). Never embed build trees in the handoff body.
- Prefer promoting one lasting finding into Jira / ADR / PR description, then
  delete disposable material — do not keep forever “for investigation” under
  `handoffs/`.

### Status → retention

| Status | Retention |
|---|---|
| Agent handoff `READY` / `BLOCKED` | Stay in `handoffs/active/<task-id>/` |
| Agent handoff `STALE` | Stay in active until replaced / Close; do not INTEGRATE |
| Architect integrated + request closed (or abandoned) | Write archive `summary.yaml`, wipe `scratch/<task-id>/`, clear `active/<task-id>/` |

Request-level close tokens: `REQUEST COMPLETE`,
`REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS`, or abandoned / superseded run.
`REQUEST BLOCKED` may leave active handoffs for resume (**E**) unless the user
abandons the request (then Close as abandoned).

### Ignore and size guards

Recommended `.gitignore` (pack README / sample):

```gitignore
agent-system/scratch/
**/build-out/
agent-system/handoffs/active/
```

Archive summaries may be committed when useful. Scratch must never be committed.

**Close size guard:** fail Close if any file under `handoffs/` is binary or
larger than **64 KB**, except tiny scripts the Architect explicitly keeps and
documents in the archive summary. Prefer moving large evidence to `scratch/`
(then deleted) or to a real `scripts/` / PR path.

## Same-task restart gate (mandatory)

When a new `/operate` run (including a new chat/thread) targets a **task id**
that already has packages under `handoffs/active/<task-id>/` or files under
`scratch/<task-id>/`, the Architect **must stop and ask** before any
delegation, integrate, or diagnosis skip. Do not auto-resume. Do not
auto-wipe.

### Choice UI

Prefer the host’s native structured-question UI (`AskQuestion` on Cursor).
Put the recommended option first only when state clearly supports it
(e.g. recent reconciled `READY` → recommend Resume). Text fallback:

```text
This task already has operate data under handoffs/active/<task-id>/
(and possibly scratch/<task-id>/).

How do you want to continue?

R. Resume from existing handoffs
   Keep active packages; reconcile READY against the working tree; continue
   from the last honest status (do not skip diagnosis on STALE).

F. Fresh start — wipe and restart
   Close as ABANDONED/SUPERSEDED for this run: write a short archive summary,
   delete scratch/<task-id>/, clear handoffs/active/<task-id>/, then start
   clean delegations for the same task id.

Reply with R or F.
```

### After the choice

| Choice | Architect action |
|---|---|
| **R — Resume** | Keep `active/` + existing scratch; run READY reconciliation; continue C/D/E from reconciled state. Never treat unreconciled READY as done. |
| **F — Fresh start** | Run **Close** as abandoned/superseded for that task id (archive summary + wipe scratch + clear active), then start C/D with empty folders. |

Rules:

- A new thread on the same issue is **not** an implicit Fresh start.
- Saying only `/operate` or restating the issue is **not** enough to wipe.
- If the user already said clearly “fresh start”, “clean run”, “wipe and
  restart”, or “abandon and redo”, treat that as **F** without re-asking.
- If they said clearly “resume”, “continue where we left off”, treat as **R**.
- When intent is ambiguous and active data exists → **always ask R vs F**.

## READY reconciliation

Handoff truth ≠ code truth. Before options **C** / **D** resume, **E**
next-action, or Integrate of a `READY` handoff:

1. Verify each claimed path / symbol / artifact reference still exists in the
   working tree (or matches the stated PR/URL evidence).
2. On mismatch → mark the handoff `STALE` (or write a versioned superseding
   package under `active/<task-id>/`). Do **not** INTEGRATE. Do **not** skip
   diagnosis.
3. Prefer versioned / `SUPERSEDED` packages over silent overwrite of stale
   `READY` claims.

Cleanup of `active/<task-id>/` + `scratch/<task-id>/` after Close makes stale
claims harder to resurrect; reconciliation remains mandatory while active
packages exist.

## Loop

1. Initialize Architect with governance artifacts.
2. Submit a project request with explicit authorization bounds.
3. If the task id already has active/scratch data → **Same-task restart gate**
   (Resume vs Fresh start). On Fresh start, Close-abandon first.
4. Review delegation plan before implementation (include `scratch_dir` +
   active handoff path per task).
5. Invoke only selected agents via the delegation runtime with unchanged
   delegation YAML (host: isolated subagent + agent prompt inject).
6. Return thin handoffs under `handoffs/active/<task-id>/` to the Architect.
7. Run independent validation when required (separate QA subagent when a
   reviewer is required).
8. READY reconciliation → Integrate → Approve → **Close**.
9. Final request status after Close:
   - `REQUEST COMPLETE`
   - `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS`
   - `REQUEST BLOCKED` (active may remain for a later Resume unless abandoned)

## Close (cleanup gate)

Close runs after integration status is `REQUEST COMPLETE`,
`REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS`, or the user abandons /
supersedes the request. **Cleanup is part of COMPLETE** — do not leave
scratch or active piles behind.

1. **Size / type guard** — fail Close if `handoffs/` contains binaries or
   files > 64 KB (see above). Move or delete offenders first.
2. **Archive summary** — write at most one short
   `handoffs/archive/<task-id>/summary.yaml` with: final status, PR link(s)
   if any, open limitations / follow-ups. Do not copy build trees or full
   handoff histories into the archive.
3. **Wipe scratch** — delete `agent-system/scratch/<task-id>/` entirely
   (including `build-out/`, test publish dirs, copied `bin/`).
4. **Clear active** — delete `handoffs/active/<task-id>/`, or move only the
   thin final handoff YAML into archive if a future **E** truly needs it
   (default: summary only; delete the rest).
5. Durable findings that still matter belong in Jira / ADR / PR description —
   not as a permanent dump under `handoffs/`.

Optional checkable DoD: pack `protocols/task-close.yaml`.

## Hard rules

- Start with the Architect.
- Do not broaden agent authority independently.
- Never paste real secrets.
- Never claim tests/builds/deploys that were not executed.
- Record approvals explicitly.
- Do not role-play fleet specialists in the parent chat when isolated
  subagents/Tasks are available.
- Every C/D delegation sets `scratch_dir`; probes/builds never go under
  `handoffs/`.
- Handoffs are metadata-only YAML/MD; artifacts are external paths/URLs.
- Do not INTEGRATE `STALE` or unreconciled `READY` handoffs.
- Close includes archive summary + scratch wipe + active clear.
- Same task id with existing active/scratch data → ask Resume vs Fresh start;
  never silent reuse and never silent wipe.
