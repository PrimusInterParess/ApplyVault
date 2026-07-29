# Operate Agent System

> **CRITICAL — Cursor users:** When presenting choices (A/B/C/D/E), you MUST
> use the `AskQuestion` tool so options are clickable. Do NOT print a plain
> text menu. This applies to the entry menu and every subsequent choice point.

Operate a generated prompt pack through Architect → agents → validation → integration.

Deep playbook: [../../references/source-prompts/04-operate-agent-system.md](../../references/source-prompts/04-operate-agent-system.md)

## Entry menu

When `/operate` is invoked without a concrete request, do not assume the user
wants implementation.

### Choice UI

- If the host IDE provides a native structured-question or multiple-choice
  tool, use it so the options are clickable. Do not print the text menu first.
- In Cursor, use the native `AskQuestion` tool.
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
2. The Architect selects the next unblocked milestone and creates task
   delegations (one envelope per primary agent).
3. Specialist work runs through the **delegation runtime** below — not by the
   Architect implementing those roles itself.
4. Agents return handoffs; reviewers validate required evidence.
5. The Architect performs integration review and records one final status:
   `REQUEST COMPLETE`, `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS`, or
   `REQUEST BLOCKED`.
6. It then offers **C** again for the next milestone or **E** to review status.

If no approved milestone exists, do not implement; direct the user to **A**.

### D — Execute a specific request

1. If the request was not supplied, ask the user to describe it.
2. The Architect analyzes scope, affected contracts, agents, and approvals.
3. It delegates only the requested work via the **delegation runtime**.
4. Implementation occurs only when the request authorizes file changes.
5. Handoffs, validation, integration review, and final status use the same rules
   as option C.

### E — Resume or review existing work

1. Read implementation plans, task mappings, handoffs, validation reports,
   blockers, and approval state.
2. Return a concise status: completed, in progress, blocked, and next eligible
   action.
3. Do not modify application files.
4. Present the relevant next choice (usually **C**, **D**, or an approval action).

## Authorization behavior

- A and B are read-only.
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

Do not invent new Cursor Task *kinds*. Reuse host Task types and **inject** the
fleet agent prompt + delegation envelope into each run.

### Cursor (required when Task is available)

For options **C** and **D** (and any authorized specialist work under `/operate`):

1. Parent chat = `principal-software-architect` only: plan, select agents,
   write/save delegation envelopes, integrate handoffs, run approval gates.
2. Parent must **not** implement work owned by another registered agent
   (backend, frontend, UX, PM, QA, etc.) inside the parent turn.
3. For each selected primary agent, call Cursor **`Task`**:
   - `subagent_type`: `generalPurpose` for implementation/design; `explore`
     only for read-only research.
   - `description`: short title including the fleet agent id
     (e.g. `backend-engineer: M2 domain model`).
   - `prompt` must include, in order:
     1. Exact text (or path + instruction to read) of that agent’s
        `agents/<file>.md`
     2. The filled `task-delegation.yaml` for this task (unchanged bounds)
     3. Paths to `shared-context.yaml` and any required contracts
     4. Authorization bounds from the user
     5. Required return: complete agent-handoff package + status
4. Launch independent agents in parallel when the plan marks them unblocked
   and non-conflicting; otherwise run sequentially.
5. After each Task returns, the Architect validates the handoff, then
   continues (next agent, QA reviewer Task, or integration review).
6. If `Task` is unavailable in the session, say so once, then fall back to
   **Separate chats** (preferred) or **Single chat** (last resort), and record
   the fallback in the final status notes.

### Non-Cursor hosts

1. **Native multi-agent** — use the host’s equivalent of Task/subagents with
   the same inject pattern (agent prompt + delegation YAML).
2. **Separate chats** — one chat per agent; paste the same two artifacts.
3. **Single chat** — Architect simulates one agent at a time (weakest
   isolation; allowed only when 1–2 are impossible).

## Loop

1. Initialize Architect with governance artifacts.
2. Submit a project request with explicit authorization bounds.
3. Review delegation plan before implementation.
4. Invoke only selected agents via the delegation runtime with unchanged
   delegation YAML (Cursor: `Task` + agent prompt inject).
5. Return complete handoffs to the Architect.
6. Run independent validation when required (Cursor: separate QA `Task`
   when a reviewer is required).
7. Final integration review with status:
   - `REQUEST COMPLETE`
   - `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS`
   - `REQUEST BLOCKED`

## Hard rules

- Start with the Architect.
- Do not broaden agent authority independently.
- Never paste real secrets.
- Never claim tests/builds/deploys that were not executed.
- Record approvals explicitly.
- In Cursor, do not role-play fleet specialists in the parent chat when `Task`
  is available.
