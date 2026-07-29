# Step 4 — Operate the Generated Agent System

Use this guide after Step 3 returns `PROMPT_PACK_READY` or `PROMPT_PACK_READY_WITH_DOCUMENTED_LIMITATIONS` and you have saved its proposed files.

## 1. Choose an operating model

### Option A — Native multi-agent platform

Use this when your AI platform can invoke named sub-agents.

- Install `agents/00-principal-architect.md` as the main agent.
- Install every other file under `agents/` as a named sub-agent.
- Give the Architect access to the governance and protocol files.
- Configure sub-agent names to match `governance/agent-registry.yaml`.
- Submit all project work to the Architect first.

This is the preferred model when true delegation is supported.

### Option B — Separate AI chats

Use this when your platform does not support native sub-agents.

- Maintain one persistent chat for the Principal Architect.
- Open one persistent chat for each agent only when the Architect invokes it.
- Copy the Architect's delegation envelope into the matching agent chat.
- Copy the resulting handoff back into the Architect chat.
- Let the Architect perform integration review.

Do not ask implementation agents to coordinate the whole fleet themselves.

### Option C — Single AI chat

Use this only for small or low-risk tasks.

- Initialize the Architect.
- Ask it to simulate one agent at a time.
- Require a complete delegation before each role transition.
- Require a handoff before returning to the Architect role.
- Do not simulate parallel work.

This option has weaker isolation and is easier to confuse on complex work.

---

# 2. Create the Architect session

Open a new chat dedicated to the project.

Use the contents of:

`agent-system/agents/00-principal-architect.md`

as system instructions.

Then paste:

```text
Initialize this project’s agent system.

Authoritative artifacts:

- agent-system/manifest.yaml
- agent-system/governance/shared-context.yaml
- agent-system/governance/agent-registry.yaml
- agent-system/governance/ownership-matrix.md
- agent-system/governance/contract-registry.yaml
- agent-system/governance/approval-gates.md
- agent-system/governance/quality-gates.md
- agent-system/governance/integration-policy.md
- agent-system/governance/conflict-resolution.md
- agent-system/governance/change-management.md
- agent-system/protocols/task-delegation.yaml
- agent-system/protocols/agent-handoff.yaml
- agent-system/protocols/validation-report.yaml
- agent-system/protocols/execution-workflow.md

Read all accessible artifacts before accepting work.

Do not implement anything yet.

Report:

1. Whether every required artifact is accessible.
2. The recognized project mode.
3. The recognized agent fleet.
4. Missing, contradictory, or unresolved governance information.
5. Whether the system is ready to accept a task.

Finish with exactly one:

AGENT SYSTEM READY
AGENT SYSTEM READY WITH LIMITATIONS
AGENT SYSTEM BLOCKED
```

If the AI cannot access local files, paste or attach the relevant artifacts. Never accept a claim that files were read when the platform could not access them.

---

# 3. Submit a project request

After the Architect reports readiness, paste:

```text
NEW PROJECT REQUEST

Request ID: <REQUEST-ID>

Title:
<SHORT TITLE>

Business objective:
<WHY THIS WORK IS NEEDED>

Requested outcome:
<WHAT SHOULD EXIST OR CHANGE>

Acceptance criteria:
- <CRITERION 1>
- <CRITERION 2>

Known constraints:
- <SECURITY, COMPATIBILITY, BUDGET, DEADLINE, OR OTHER CONSTRAINT>

Known affected areas:
- <AREA OR UNKNOWN>

Explicitly out of scope:
- <EXCLUSION>

Authorization:
- Discovery and read-only repository analysis: <AUTHORIZED | NOT AUTHORIZED>
- File modifications: <AUTHORIZED | NOT AUTHORIZED>
- Dependency installation: <AUTHORIZED | NOT AUTHORIZED>
- Build execution: <AUTHORIZED | NOT AUTHORIZED>
- Test execution: <AUTHORIZED | NOT AUTHORIZED>
- External-system access: <AUTHORIZED | NOT AUTHORIZED>
- Deployment: <AUTHORIZED | NOT AUTHORIZED>

Required approval before high-risk changes: YES

First analyze and delegate. Do not begin implementation until the delegation plan is complete and all blocking approvals are resolved.
```

Delete authorization rows that are irrelevant, but never imply authorization that was not granted.

---

# 4. Review the Architect’s delegation

The Architect should return:

- Request interpretation.
- Verified context.
- Impacted capabilities.
- Selected agents and rationale.
- Contracts involved.
- Blocking and parallel tasks.
- Approval gates.
- Quality gates.
- Delegation envelopes.

Before invoking agents, check:

- Every task has one primary agent.
- Scope and exclusions are explicit.
- Repository paths are bounded.
- Acceptance criteria are measurable.
- Contracts are named.
- Missing approvals are visible.
- Validation is assigned.
- No irrelevant agent was invoked.

If corrections are needed, paste:

```text
REVISE DELEGATION

Request ID: <REQUEST-ID>

Required changes:
- <CHANGE>

Reason:
<WHY>

Do not begin implementation. Return the complete revised delegation plan.
```

---

# 5. Initialize a sub-agent chat

Skip this section when native sub-agents are available.

Open a new chat for the invoked agent. Use its complete file under `agent-system/agents/` as system instructions.

Then paste:

```text
Initialize as agent:

Agent ID: <AGENT-ID>

Authoritative project artifacts:

- agent-system/manifest.yaml
- agent-system/governance/shared-context.yaml
- agent-system/governance/agent-registry.yaml
- agent-system/governance/ownership-matrix.md
- agent-system/governance/contract-registry.yaml
- agent-system/governance/approval-gates.md
- agent-system/governance/quality-gates.md
- agent-system/protocols/agent-handoff.yaml

Read accessible artifacts relevant to your role.

Do not perform project work yet.

Report:

1. Recognized role and primary ownership.
2. Explicit non-responsibilities.
3. Contracts you may consume and produce.
4. Missing or inaccessible context.
5. Readiness to receive a delegation.

Finish with exactly one:

AGENT READY
AGENT READY WITH LIMITATIONS
AGENT BLOCKED
```

Keep the chat for future tasks assigned to that same agent and project. Start a new chat if its system prompt or project context changes materially.

---

# 6. Delegate work to the sub-agent

Copy the complete delegation produced by the Architect. Do not paraphrase it.

Paste:

```text
EXECUTE DELEGATED TASK

The following delegation was issued by the Principal Architect:

<PASTE COMPLETE TASK-DELEGATION YAML>

Instructions:

1. Validate the delegation before acting.
2. Stop if a blocking dependency, contract, approval, or required artifact is missing.
3. Stay within included scope and repository paths.
4. Do not perform unauthorized operations.
5. Produce all required artifacts.
6. Perform only authorized validation.
7. Finish with a complete handoff using the project handoff protocol.
```

If the agent reports a blocker, return it to the Architect. Do not independently broaden its authority.

---

# 7. Return the handoff to the Architect

Copy the sub-agent's full handoff into the Architect chat:

```text
AGENT HANDOFF RECEIVED

Request ID: <REQUEST-ID>
Task ID: <TASK-ID>
Producer: <AGENT-ID>

<PASTE COMPLETE HANDOFF YAML>

Review this handoff against:

- The original delegation.
- The ownership matrix.
- The contract registry.
- Required approval gates.
- Required quality gates.
- Downstream dependencies.

Choose one:

- ACCEPT HANDOFF
- ACCEPT WITH CONDITIONS
- RETURN FOR REVISION
- BLOCK AND ESCALATE

Provide the reason and next action.
```

Do this separately for every delegated task.

---

# 8. Handle proposed contract changes

If an agent proposes a contract change, send it to the Architect:

```text
CONTRACT CHANGE REVIEW

Request ID: <REQUEST-ID>
Task ID: <TASK-ID>
Proposed by: <AGENT-ID>
Contract ID: <CONTRACT-ID>

Current version:
<REFERENCE>

Proposed change:
<DESCRIPTION OR REFERENCE>

Compatibility:
<BACKWARD_COMPATIBLE | BREAKING | UNKNOWN>

Reason:
<RATIONALE>

Evidence:
- <EVIDENCE>

Review impact, affected consumers, migration requirements, validation needs, and approval authority.

Do not treat this change as approved until the required approval is recorded.
```

The Architect must notify all affected consumers after approval.

---

# 9. Resolve conflicts

When agents disagree, do not choose based on writing confidence.

Paste into the Architect chat:

```text
CROSS-AGENT CONFLICT

Request ID: <REQUEST-ID>

Agents:
- <AGENT-A>
- <AGENT-B>

Concern:
<CONFLICTED CONCERN>

Position A:
<SUMMARY AND EVIDENCE>

Position B:
<SUMMARY AND EVIDENCE>

Relevant requirements, contracts, and decisions:
- <REFERENCE>

Apply the conflict-resolution policy.

Return:

1. Conflict classification.
2. Primary owner.
3. Governing evidence.
4. Decision or required escalation.
5. Required downstream changes.
6. Approval status.
```

---

# 10. Request independent validation

When the workflow requires QA, security, architecture, performance, or operational validation, the Architect should issue a separate validation delegation.

Use:

```text
VALIDATION REQUEST

Request ID: <REQUEST-ID>
Subject Task ID: <TASK-ID>
Validator: <VALIDATOR-AGENT-ID>

Artifacts to validate:
- <REFERENCE>

Required quality gates:
- <GATE>

Constraints:
- Do not modify implementation artifacts unless separately authorized.
- Do not trust producer claims without evidence.
- Record checks that were not run.

Return a complete validation report using:

agent-system/protocols/validation-report.yaml
```

Validation ownership should be independent when risk warrants it.

---

# 11. Final integration review

After all required handoffs and validation reports are available, paste:

```text
FINAL INTEGRATION REVIEW

Request ID: <REQUEST-ID>

Review:

- All delegations.
- All handoffs.
- All validation reports.
- Contract changes.
- Approval records.
- Remaining risks.
- Acceptance criteria.
- Rollback and operational readiness where applicable.

Return:

1. Delivered scope.
2. Excluded or incomplete scope.
3. Contract compatibility.
4. Quality-gate results.
5. Approval-gate results.
6. Validation performed and not performed.
7. Remaining risks and limitations.
8. Required follow-up.
9. Final status.

Choose exactly one final status:

REQUEST COMPLETE
REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS
REQUEST BLOCKED
```

Do not accept completion when required evidence or approvals are missing.

---

# 12. Add or replace an agent

Do not edit an agent prompt in isolation. Return to the Prompt Factory and use:

```text
EXTEND_FLEET

Project:
<PROJECT NAME>

Current prompt-pack version:
<VERSION>

Requested capability or agent:
<DESCRIPTION>

Reason:
<WHY IT IS NEEDED>

Known provider status:
<VERIFIED_EXISTING | USER_REQUIRED | ORGANIZATION_REQUIRED | USER_PREFERRED | ARCHITECT_PROPOSED | UNDECIDED | NOT_APPLICABLE | MIGRATION_PLANNED>

Current agents that may overlap:
- <AGENT-ID OR UNKNOWN>

Required output:

1. Necessity assessment.
2. Ownership-impact analysis.
3. New or revised agent prompt.
4. Architect prompt update.
5. Registry update.
6. Ownership-matrix update.
7. Contract-registry update.
8. Workflow and handoff updates.
9. Prompt-pack version recommendation.
10. Migration instructions.
```

Approve the extension before replacing production prompt files.

---

# 13. Change a provider or platform

Provider migration affects more than one agent. Use the Prompt Factory:

```text
UPDATE_ORCHESTRATOR

Change type: PROVIDER OR PLATFORM MIGRATION

Project:
<PROJECT NAME>

Capability:
<IDENTITY | BILLING | DATABASE | AI | HOSTING | CICD | OBSERVABILITY | OTHER>

Current provider:
<CURRENT>

Proposed provider:
<PROPOSED>

Reason:
<RATIONALE>

Compatibility and migration constraints:
- <CONSTRAINT>

Produce:

1. Impact analysis.
2. Required decision record.
3. Agent additions, removals, or specializations.
4. Updated ownership.
5. Contract changes.
6. Data and configuration migration concerns.
7. Security and compliance review.
8. Validation strategy.
9. Rollback strategy.
10. Updated prompt-pack artifacts.

Do not assume migration approval.
```

---

# 14. Audit the agent system

Periodically return to the Prompt Factory:

```text
AUDIT_PROMPTS

Audit this complete prompt pack:

<ATTACH OR PROVIDE THE AGENT-SYSTEM DIRECTORY>

Evaluate:

- Provider neutrality.
- Agent necessity.
- Duplicate or missing ownership.
- Prompt consistency.
- Contract alignment.
- Security and privacy guardrails.
- Approval gates.
- Handoff correctness.
- Quality-gate measurability.
- Hallucination controls.
- Brownfield compatibility.
- Obsolete provider or framework assumptions.

Return:

1. Findings by severity.
2. Evidence.
3. Recommended changes.
4. Complete replacement files for approved remediations.
5. Semantic-version recommendation.
```

Audit does not authorize repository or prompt-file changes unless explicitly stated.

---

# 15. Recommended conversation layout

For a native multi-agent platform:

```text
Prompt Factory
??? Creates and updates prompt packs

Principal Architect
??? Delegates to registered agents
??? Receives handoffs
??? Requests validation
??? Performs final integration
```

For separate chats:

```text
Chat 1 — Prompt Factory
Chat 2 — Principal Architect
Chat 3 — Invoked Agent A
Chat 4 — Invoked Agent B
Chat 5 — Validator, if required
```

You do not need to open every agent chat in advance.

---

# 16. Operating rules

- Start every project request with the Architect.
- Invoke only agents selected by the Architect.
- Keep delegation envelopes unchanged.
- Return complete handoffs.
- Record approvals explicitly.
- Never copy real secrets into prompts or chats.
- Never confuse source inspection with runtime verification.
- Never report tests, builds, scans, or deployments that were not executed.
- Stop blocked work instead of inventing missing context.
- Update governance artifacts when ownership or contracts change.
- Re-run integration review after material changes.
- Version the prompt pack.

This completes the reusable Prompt Factory and agent-operation workflow.
