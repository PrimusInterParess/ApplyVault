# Brownfield Research (Brownfield / Hybrid)

Research an **existing** project before recommending agents. Remain **read-only** unless the user separately authorizes changes.

For pure Greenfield interviews, use [project-discovery.md](project-discovery.md).

## Mode behavior

| Mode | Behavior |
|---|---|
| `BROWNFIELD` | Repository research ? specification ? approval |
| `HYBRID` | Brownfield research first ? interview for new work ? classify PRESERVE / EXTEND / REFACTOR / REPLACE / REQUIRES_DECISION ? specification ? approval |

## Access check

When asking the user to choose an access method, file source, research depth, or
Hybrid change strategy, use the detailed question format from
[project-discovery.reference.md](project-discovery.reference.md):

Explain each option clearly, then mark the agent's suggested option as
`Recommended` with one short reason.

For repository access, recommend direct read-only workspace access when
available because it provides stronger evidence and requires less manual
copying. Do not recommend it when access or confidentiality constraints make it
inappropriate.

### Direct access available

1. State the accessible repository root.
2. Confirm read-only analysis.
3. Inspect high-signal paths incrementally; prefer search over reading everything.
4. Ask questions only when evidence cannot establish the answer.

### Direct access unavailable

1. Say clearly that the repo was not inspected.
2. Do not pretend otherwise.
3. Request **one** safe artifact at a time (sanitized tree, manifests, README, config templates, API schemas, pipelines, selected files).
4. Treat user summaries as `USER_REPORTED_FACT`.

## Safety rules

- No modify, format, migrate, install, build, test, start, deploy, or external calls without explicit authorization.
- Never display secret values; report path + category only if a likely secret appears.
- Source inspection ? runtime verification; reading tests ? tests pass.
- The only automatic write allowed during discovery is the governance artifact
  `agent-system/project-specification.md`, following the persistence rules in
  [project-discovery.md](project-discovery.md). This does not authorize changes
  to application, infrastructure, configuration, or test files.

## Inspection stages

Stop early when enough evidence exists. Details: [brownfield-research.reference.md](brownfield-research.reference.md).

1. Orientation (README, manifests, docs)
2. **Existing agent / skills / documentation procedures** (mandatory when any
   host instructions, `SKILL.md` packs, domain memory, or agent config docs
   appear — see [existing-operating-procedures.md](existing-operating-procedures.md))
3. Application topology
4. Contracts and data
5. Identity and authorization
6. Payments and billing (if present)
7. AI (if present)
8. Infrastructure and CI/CD
9. Configuration (no secret values)
10. Quality / tests
11. Observability and operations

## Evidence table

For material findings:

| Finding | Classification | Evidence | Confidence | Impact |
|---|---|---|---|---|

Classifications: `VERIFIED_PROJECT_FACT` | `USER_REPORTED_FACT` | `PROPOSED_DECISION` / proposed finding | `ASSUMPTION` | `RISK` | `OPEN_QUESTION` | `BLOCKER`

Cite paths, symbols, config **key names**, routes, schema names � never secret values.

Align ledger entries with [../../schemas/discovery-ledger.schema.json](../../schemas/discovery-ledger.schema.json).

## Reconstruct current state

Document only evidence-supported: structure, apps, FE/BE/data, APIs/events, identity, billing, AI, infra, CI/CD, config/secrets flow, quality, observability, deployment, externals. Label unknowns.

## Agent-system context artifact mapping (when present)

If the repository already contains an agent-system-like setup (or partial
artifacts), detect and record where those artifacts live even if they are
not under `agent-system/`.

Look for evidence of:

- a project specification file (e.g., `project-specification.md`,
  `*-specification*.md`, or similarly named spec documents)
- governance artifacts (e.g., `governance/*` or `contract-registry*`,
  `shared-context*`, `ownership-matrix*`)
- agent prompt files (e.g., `agents/*`)

Record a path mapping in the evidence section of the final specification so
`/operate` can use it later.

## Existing operating procedures (mandatory check)

During Orientation, also run
[existing-operating-procedures.md](existing-operating-procedures.md):

1. Detect host instructions, skills packs, domain memory, agent config docs,
   and any prescribed delivery workflow chain.
2. Choose adaptation mode: `FOLLOW` | `COMPOSE` | `BRIDGE` | `NONE`.
3. Record the inventory and conflicts in the specification section
   **Existing Agent / Skills / Documentation Procedures**.
4. Prefer project procedures over Architect defaults; do not overwrite skill
   files, `CONTEXT.md`, ADRs, or host instruction files during research.

If procedures are found, recommended agents and the later prompt pack **must**
adapt to them (see generate-prompt-pack binding rules).

## Gap analysis

Severity: `CRITICAL` | `HIGH` | `MEDIUM` | `LOW` | `INFORMATIONAL`

Per gap: finding, evidence, impact, owner candidate, remediation, blocks prompt generation?, needs approval?

Separate current state / risk / recommended future state. Do not auto-redesign.

## Hybrid extras

- Separate verified current vs proposed future behavior
- Compatibility and migration risks
- Prefer incremental modernization
- Require approval for replacement or breaking changes
- After research, use the interview and recommendation rules from
  [project-discovery.md](project-discovery.md) for new functionality (still one
  question at a time, with explained options and contextual recommendations)

## Capability matrix and agents

Maintain matrix per [../../schemas/capability-matrix.schema.json](../../schemas/capability-matrix.schema.json).

Recommend agents from verified capabilities. Prefer generic agents when providers are undecided; specialize only when verified/approved. Do not generate prompts.

## Final deliverable

Same document as greenfield:

`# PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION`

Full section list in [project-discovery.reference.md](project-discovery.reference.md) and [../../schemas/requirements-spec.schema.json](../../schemas/requirements-spec.schema.json).

Fill current-repo, architecture, evidence index, and access limitations from research.

When workspace file-writing tools are available, automatically save and update
the specification at `agent-system/project-specification.md` using the proposed,
approved, approved-with-changes, and revise status rules in
[project-discovery.md](project-discovery.md).

End with:

```text
APPROVAL REQUIRED
```

Then stop. Accept `APPROVED` | `APPROVED WITH CHANGES: ...` | `REVISE: ...`.

After approval, automatically run [generate-prompt-pack.md](generate-prompt-pack.md)
in `SAVE` mode. Do not ask whether to create the selected agents. Prompt-pack
generation is authorized by specification approval; application implementation
is not.

## Begin

If mode is already B or C, start with the repository access check (or confirm accessible root if already in the workspace).

If mode is unknown, ask A/B/C via [agent-system-builder.md](agent-system-builder.md).
