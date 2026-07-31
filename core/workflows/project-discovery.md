# Project Discovery (Greenfield)

Conduct structured discovery for a **GREENFIELD** project. Produce an evidence-based specification. Do not generate agents or implementation code.

For Brownfield/Hybrid, use [brownfield-research.md](brownfield-research.md) instead (or first).

## Principles

1. Capability before provider.
2. Provider-neutral until verified or approved.
3. Minimal necessary complexity.
4. Evidence over assumption — classify every material statement.
5. One question per message; wait for the answer.
6. Max 25 primary questions unless the user authorizes more.
7. End with `APPROVAL REQUIRED`, not prompt generation.

## Interview rules

- Prefer multiple-choice with an `Other / custom` option.
- Skip irrelevant capability areas.
- Do not ask for tech selection prematurely.
- If the user does not know, propose only after enough context; mark `PROPOSED_DECISION`.
- Resolve material contradictions before finishing.
- Never request secrets or real customer data.
- Do not edit repositories.

## Question format

For each question:

1. Ask one question.
2. Use the host IDE's native structured-question or multiple-choice tool when
   Prefer the host's native choice UI when available so options are
   clickable. Cursor `AskQuestion` details live under `.cursor/`.
3. Use plain-text options only as a fallback when no choice tool is available.
4. Explain each option clearly enough for the user to understand the difference.
5. Put the option the agent suggests first, mark it `(Recommended)`, and briefly
   explain why.
6. If there is not enough information to support a recommendation, omit the
   recommendation entirely.
7. Include `Other / custom answer`.

A recommendation is a proposal, not an approved decision. Do not recommend a
provider before the required capability is understood.

## Classification ledger

Record answers using schemas in [../../schemas/discovery-ledger.schema.json](../../schemas/discovery-ledger.schema.json):

`VERIFIED_PROJECT_FACT` | `USER_CONFIRMED_REQUIREMENT` | `USER_REPORTED_FACT` | `CONSTRAINT` | `PREFERENCE` | `APPROVED_DECISION` | `PROPOSED_DECISION` | `ASSUMPTION` | `RISK` | `OPEN_QUESTION` | `BLOCKER`

Never present assumptions as verified facts.

## Discovery topics (ask only what is relevant)

Order is a guide, not a script:

1. Business problem, outcome, success measures
2. Target users and stakeholders
3. Roles and access needs (whether auth is required comes first)
4. Core user journeys and scope (include / exclude / defer)
5. Functional capabilities
6. Identity capability (only if needed) — then providers if undecided
7. Payments / billing capability (only if needed)
8. Data / persistence needs
9. AI / automated decisions (only if needed)
10. Client apps and UX constraints
11. Backend / integration needs
12. Hosting, delivery, environments, ops
13. Security, privacy, compliance
14. Quality / testing expectations
15. Technology preferences and undecided providers (compare ≥3 options)

Maintain a capability matrix per [../../schemas/capability-matrix.schema.json](../../schemas/capability-matrix.schema.json).

Detailed topic checklists: [project-discovery.reference.md](project-discovery.reference.md).

## Completion criteria

Discovery is complete when project mode, goals, scope, users, journeys, material FR/NFR, capabilities, providers (confirmed or visibly open), security, data boundaries, ops constraints, candidate agents, assumptions, and risks are clear enough for a prompt pack later.

Otherwise readiness is:

`NOT_READY — BLOCKING_INFORMATION_REQUIRED`

## Final deliverable

Emit Markdown titled:

`# PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION`

Include all sections listed in [project-discovery.reference.md](project-discovery.reference.md) (aligned with [../../schemas/requirements-spec.schema.json](../../schemas/requirements-spec.schema.json)).

For Greenfield section 10 (Current Repository):

`Not applicable — Greenfield project.`

For section **Existing Agent / Skills / Documentation Procedures**: use
`NOT_FOUND` / adaptation mode `NONE` unless the user pasted or pointed at an
external skills pack, host instructions, or documentation workflow — then run
[existing-operating-procedures.md](existing-operating-procedures.md) and record
adoption decisions before recommending the fleet.

## Automatic specification persistence

When workspace file-writing tools are available:

1. Create `agent-system/` if it does not exist.
2. Save the complete specification to:
   `agent-system/project-specification.md`
3. Before approval, include this metadata near the top:

   ```text
   Status: PROPOSED — APPROVAL REQUIRED
   ```

4. When the user replies `APPROVED`, update it to:

   ```text
   Status: APPROVED
   ```

5. For `APPROVED WITH CHANGES`, apply the stated changes, regenerate the affected
   sections, save the updated file, and mark it `APPROVED WITH CHANGES`.
6. For `REVISE`, update and save it as `PROPOSED — APPROVAL REQUIRED`.
7. Do not overwrite a different existing project specification silently. If the
   path already contains an unrelated specification, save a clearly named
   proposed copy and report the conflict.

Saving this governance artifact does not authorize implementation changes.

If file-writing tools are unavailable, provide the complete specification in
the response and state that it could not be saved automatically.

End with exactly:

```text
APPROVAL REQUIRED
```

Then stop.

Accept only: `APPROVED` | `APPROVED WITH CHANGES: ...` | `REVISE: ...`

After `APPROVED` or `APPROVED WITH CHANGES`:

1. Save the approved specification status.
2. Immediately run [generate-prompt-pack.md](generate-prompt-pack.md) in `SAVE`
   mode.
3. Create and verify the selected agent fleet under `agent-system/`.
4. Return a short generation summary and tell the user the next command is
   `/operate`.

Do not ask whether to generate the agents. Specification approval authorizes
prompt-pack generation, but it does not authorize application implementation.

After `REVISE`, update the proposed specification and return to
`APPROVAL REQUIRED`; do not generate agents.

## Agent candidates

Recommend agents only after justification. Prefer generic capability agents when providers are undecided. Exclude unjustified agents with reasons. Do not generate prompts.

## Begin

If project mode is already `GREENFIELD`, ask the first business/problem question only.

If mode is unknown, ask the A/B/C mode question first (see [agent-system-builder.md](agent-system-builder.md)).
