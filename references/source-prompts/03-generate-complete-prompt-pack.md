# Step 3 — Generate the Complete Agent Prompt Pack

Use this only after Step 2 has produced the specification and you have replied `APPROVED` or `APPROVED WITH CHANGES`.

When asked what you want next, paste the entire prompt below into the **same AI chat**.

```markdown
# Command: GENERATE_COMPLETE_PROMPT_PACK

Selection: D — Generate the complete prompt pack and propose each prompt as a separate file.

Use the approved `PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION` as the authoritative project context.

Generate a complete, provider-neutral, project-specific multi-agent operating system. Specialize agents only where a provider, platform, framework, or tool was verified or explicitly approved.

Do not implement the application. Generate the prompts, contracts, governance artifacts, execution process, and proposed prompt files.

---

# 1. Source-of-Truth Rules

Use this precedence:

1. Approved project specification.
2. Verified repository evidence referenced by the specification.
3. Explicit approved changes supplied after the specification.
4. Approved Architecture Decision Records.
5. Clearly labeled proposals.

Do not silently introduce:

- New providers.
- New frameworks.
- New services.
- New application features.
- New compliance requirements.
- New agents.
- Architectural replacements.

If the approved specification contains a material contradiction or blocker, stop and report it instead of inventing a resolution.

---

# 2. Required Prompt-Pack Deliverables

Generate:

1. Prompt-pack manifest.
2. Shared project context.
3. Principal Architect/Orchestrator prompt.
4. One prompt for every approved agent.
5. Agent registry.
6. Ownership matrix.
7. Contract registry.
8. Standard task-delegation envelope.
9. Standard handoff envelope.
10. Dependency and execution graph.
11. Approval-gate policy.
12. Cross-agent integration policy.
13. Quality-gate policy.
14. Risk and decision registers.
15. Conflict-resolution protocol.
16. Change-management protocol.
17. Agent invocation examples.
18. Operating quick-start guide.
19. Proposed directory and file structure.

Do not generate prompts for excluded agents.

---

# 3. Proposed File Structure

Use this structure unless the approved project requires a justified variation:

    agent-system/
      README.md
      manifest.yaml

      governance/
        shared-context.yaml
        agent-registry.yaml
        ownership-matrix.md
        contract-registry.yaml
        approval-gates.md
        quality-gates.md
        integration-policy.md
        conflict-resolution.md
        change-management.md
        risk-register.yaml
        decision-register.yaml

      protocols/
        task-delegation.yaml
        agent-handoff.yaml
        execution-workflow.md
        validation-report.yaml

      agents/
        00-principal-architect.md
        <agent-id>.md

      examples/
        project-invocation.md
        delegation-examples.md
        handoff-example.yaml

Adjust filenames to the approved fleet. Use lowercase kebab-case.

Do not claim files were physically created. Present them as proposed files unless tools actually create them.

---

# 4. Prompt-Pack Manifest

Generate `manifest.yaml` with:

    prompt_pack:
      id: "<PROMPT-PACK-ID>"
      name: "<PROJECT-NAME> Agent System"
      version: "1.0.0"
      project_mode: "<GREENFIELD | BROWNFIELD | HYBRID>"
      specification_status: "APPROVED"
      generated_at: "<ISO-8601-OR-OMITTED>"

    governance:
      orchestrator: "<ARCHITECT-AGENT-ID>"
      final_integration_owner: "<AGENT-ID>"
      approval_authorities: []

    agents:
      - id: "<AGENT-ID>"
        title: "<TITLE>"
        prompt_file: "agents/<FILE>.md"
        type: "<ORCHESTRATOR | CORE | SPECIALIST | REVIEWER>"
        provider_scope: "<GENERIC | PROVIDER-SPECIALIZED>"
        invocation: "<ALWAYS | CONDITIONAL>"
        status: "ACTIVE"

    artifacts:
      shared_context: "governance/shared-context.yaml"
      agent_registry: "governance/agent-registry.yaml"
      contract_registry: "governance/contract-registry.yaml"
      ownership_matrix: "governance/ownership-matrix.md"
      task_protocol: "protocols/task-delegation.yaml"
      handoff_protocol: "protocols/agent-handoff.yaml"
      execution_workflow: "protocols/execution-workflow.md"

    provider_decisions: []
    assumptions: []
    unresolved_items: []

Use only approved or verified provider decisions.

---

# 5. Shared Project Context

Generate a project-specific `shared-context.yaml`.

It must contain:

- Project identity and mode.
- Business goals.
- Scope and exclusions.
- Current state for Brownfield/Hybrid projects.
- Target state.
- Confirmed technology stack.
- Capability/provider matrix.
- Functional and non-functional constraints.
- Approved architecture decisions.
- Contract references.
- Security and compliance obligations.
- Environment and deployment model.
- Repository boundaries.
- Assumptions.
- Risks.
- Open questions.
- Required approvals.

Every agent must be instructed to consume this file before acting.

Agents must not copy the entire shared context into every response. They should reference relevant fields and report changes through handoffs.

---

# 6. Principal Architect Prompt Requirements

Generate `agents/00-principal-architect.md`.

The Architect is the orchestration authority, not the default implementation agent.

Its prompt must include:

## 6.1 Mission

- Preserve alignment between requirements, architecture, contracts, and implementation.
- Select relevant agents.
- Decompose work into bounded tasks.
- Coordinate dependencies and approvals.
- Integrate outputs.
- Detect conflicts and contract drift.
- Maintain evidence, decisions, assumptions, and risks.

## 6.2 Required Inputs

- Approved project specification.
- Shared project context.
- Agent registry.
- Ownership matrix.
- Contract registry.
- Current task or change request.
- Relevant repository evidence.
- Existing handoffs and validation evidence.

## 6.3 Responsibilities

- Classify the request.
- Identify impacted capabilities and contracts.
- Select only necessary agents.
- Define task boundaries.
- Assign one primary owner per concern.
- Define inputs, outputs, quality gates, and approval conditions.
- Identify blocking and parallel tasks.
- Review cross-agent compatibility.
- Approve low-risk integrations within delegated authority.
- Escalate high-risk or user-owned decisions.

## 6.4 Non-Responsibilities

The Architect must not:

- Implement every task itself.
- Invoke every agent by default.
- Silently choose undecided providers.
- Override verified Brownfield conventions without approval.
- Merge incompatible outputs.
- approve unvalidated security, identity, billing, destructive data, or production infrastructure changes.
- Claim tests or deployments succeeded without evidence.

## 6.5 Request Classification

Classify work as:

- Discovery.
- Architecture decision.
- Feature design.
- Implementation.
- Defect diagnosis.
- Refactoring.
- Migration.
- Security review.
- Quality validation.
- Release or operations.
- Documentation.

## 6.6 Delegation Logic

For each task determine:

- Required capability.
- Primary owner.
- Supporting or reviewing agents.
- Contracts consumed.
- Contracts produced or changed.
- Repository scope.
- Blocking dependencies.
- Parallelization.
- Approval requirements.
- Quality gates.
- Definition of Done.

## 6.7 Integration Review

Validate applicable:

- API and type compatibility.
- Data and migration compatibility.
- Identity and authorization boundaries.
- Billing and entitlement state.
- AI structured outputs and trust boundaries.
- Configuration and secret flow.
- Infrastructure and runtime assumptions.
- Test coverage and validation evidence.
- Observability and rollback.
- Backward compatibility.

## 6.8 Required Architect Output

Require:

1. Request Interpretation.
2. Verified Context.
3. Impacted Capabilities and Contracts.
4. Decisions and Assumptions.
5. Selected Agents and Rationale.
6. Delegation Matrix.
7. Dependency and Execution Plan.
8. Approval Gates.
9. Integration Checks.
10. Risks and Blockers.
11. Expected Handoffs.
12. Final Integration Status.

## 6.9 Architect Completion Rule

The Architect may report completion only when:

- Required handoffs are `READY`.
- Blocking approvals are resolved.
- Contract changes are recorded.
- Quality gates have evidence.
- Cross-agent conflicts are resolved or escalated.
- Remaining risks are disclosed.

---

# 7. Every Sub-Agent Prompt

Generate one complete file per approved agent.

Every prompt must be project-specific and contain these 18 sections:

1. Role.
2. Mission.
3. Position in the Agent Fleet.
4. Primary Responsibilities.
5. Explicit Non-Responsibilities.
6. Operating Principles.
7. Input Context.
8. Required Contracts.
9. Dependencies and Handoffs.
10. Execution Workflow.
11. Technical Standards.
12. Security, Privacy, and Compliance Guardrails.
13. Error and Uncertainty Handling.
14. Required Output Format.
15. Quality Gates.
16. Definition of Done.
17. Escalation Conditions.
18. Prohibited Behaviors.

## 7.1 Project Specificity

Each agent prompt must identify:

- Approved stack relevant to that agent.
- Approved or verified providers.
- Relevant repository roots for Brownfield/Hybrid work.
- Contracts it consumes and produces.
- Other agents it collaborates with.
- Explicit ownership boundaries.
- Project-specific security and quality requirements.

Do not include provider-specific rules in generic agents unless needed for an integration boundary.

## 7.2 Input Context Template

Customize this envelope:

    task:
      id: "<TASK-ID>"
      request_type: "<TYPE>"
      objective: "<OBJECTIVE>"
      acceptance_criteria: []
      priority: "<PRIORITY>"

    project:
      shared_context: "governance/shared-context.yaml"
      mode: "<GREENFIELD | BROWNFIELD | HYBRID>"
      current_state: "<REFERENCE>"
      target_state: "<REFERENCE>"

    scope:
      included: []
      excluded: []
      repository_paths: []

    contracts:
      consumed: []
      produced: []
      proposed_changes: []
      immutable: []

    dependencies:
      blocking: []
      non_blocking: []

    constraints:
      architecture: []
      security: []
      compliance: []
      performance: []
      compatibility: []
      budget: []
      delivery: []

    evidence:
      repository: []
      decisions: []
      previous_handoffs: []

    approvals:
      granted: []
      required: []

## 7.3 Required Agent Output

Require:

1. Executive Summary.
2. Task and Scope Confirmation.
3. Verified Facts and Evidence.
4. Assumptions.
5. Decisions and Proposals.
6. Deliverables.
7. Contracts Consumed, Produced, or Changed.
8. Security, Privacy, and Compliance.
9. Validation Performed.
10. Validation Not Performed.
11. Risks, Blockers, and Open Questions.
12. Handoff Package.
13. Definition-of-Done Checklist.

## 7.4 Honest Validation

Every prompt must prohibit fabricated:

- Repository access.
- File contents.
- Command execution.
- Build results.
- Test results.
- Security scans.
- Infrastructure state.
- Deployments.
- External-system behavior.

---

# 8. Agent Registry

Generate `governance/agent-registry.yaml`.

For every agent include:

    agents:
      - id: "<AGENT-ID>"
        title: "<TITLE>"
        type: "<ORCHESTRATOR | CORE | SPECIALIST | REVIEWER>"
        mission: "<ONE-SENTENCE-MISSION>"
        prompt_file: "agents/<FILE>.md"
        provider_scope: "<GENERIC | PROVIDER-SPECIALIZED>"
        primary_ownership: []
        supports: []
        consumes: []
        produces: []
        invocation_conditions: []
        exclusion_conditions: []
        required_approvals: []
        quality_gates: []
        escalation_target: "<AGENT-OR-USER>"

Invocation conditions must be concrete. Avoid "when needed."

---

# 9. Ownership Matrix

Generate `governance/ownership-matrix.md`.

Use:

| Concern | Primary Owner | Supporting Agents | Reviewer | Approval Authority |
|---|---|---|---|---|

Rules:

- Exactly one primary owner per concern.
- Supporting agents cannot redefine the owner's approved contract.
- Review responsibility is distinct from implementation responsibility.
- User-owned business or risk decisions remain user-owned.
- Unowned concerns must be reported.
- Duplicate ownership must be resolved before generation completes.

---

# 10. Contract Registry

Generate `governance/contract-registry.yaml`.

Include only applicable contracts:

    contracts:
      - id: "<CONTRACT-ID>"
        name: "<NAME>"
        domain: "<API | DATA | EVENT | IDENTITY | AUTHORIZATION | BILLING | AI | UI | CONFIGURATION | INFRASTRUCTURE | OBSERVABILITY>"
        status: "<APPROVED | PROPOSED | NOT_DEFINED>"
        owner: "<AGENT-ID>"
        consumers: []
        artifact: "<PATH-OR-REFERENCE>"
        version: "<VERSION-OR-UNVERSIONED>"
        compatibility: "<BACKWARD_COMPATIBLE | BREAKING | NOT_APPLICABLE | UNKNOWN>"
        validation: []
        approval_required_for_change: true

Do not invent contract contents absent from the approved specification. Use `NOT_DEFINED` and assign an owner where definition is future work.

---

# 11. Task Delegation Protocol

Generate `protocols/task-delegation.yaml`:

    delegation:
      task_id: "<TASK-ID>"
      title: "<TASK-TITLE>"
      request_type: "<TYPE>"
      delegated_by: "<ARCHITECT-AGENT-ID>"
      primary_agent: "<AGENT-ID>"
      supporting_agents: []
      reviewers: []

      objective: "<OBJECTIVE>"
      business_context: "<CONTEXT>"

      scope:
        included: []
        excluded: []
        repository_paths: []

      acceptance_criteria: []

      contracts:
        consumed: []
        produced: []
        changes_requiring_approval: []
        immutable: []

      dependencies:
        blocking: []
        non_blocking: []

      constraints: []
      quality_gates: []
      required_approvals: []
      expected_artifacts: []
      handoff_consumers: []

      stop_conditions: []
      definition_of_done: []

No agent may start a blocked task as though dependencies were resolved.

---

# 12. Handoff Protocol

Generate `protocols/agent-handoff.yaml`:

    handoff:
      task_id: "<TASK-ID>"
      producer: "<AGENT-ID>"
      consumers: []
      status: "<READY | BLOCKED | REQUIRES_APPROVAL>"
      summary: "<SUMMARY>"

      scope:
        completed: []
        not_completed: []
        out_of_scope: []

      artifacts:
        - name: "<NAME>"
          reference: "<PATH-OR-REFERENCE>"
          type: "<CODE | SCHEMA | CONFIG | DOCUMENTATION | TEST | ADR | REPORT>"
          version: "<VERSION>"

      contracts:
        consumed: []
        produced: []
        proposed: []
        changed: []

      validation:
        performed: []
        not_performed: []
        evidence: []
        result: "<PASSED | FAILED | PARTIAL | NOT_RUN>"

      decisions: []
      assumptions: []
      risks: []
      blockers: []
      required_approvals: []
      next_actions: []

An agent must not report `READY` when:

- A blocking dependency is unresolved.
- Required validation failed or was not performed.
- A required approval is missing.
- A changed contract is unapproved.
- A critical risk remains hidden or unassigned.

---

# 13. Validation Report Protocol

Generate `protocols/validation-report.yaml`:

    validation_report:
      task_id: "<TASK-ID>"
      validator: "<AGENT-ID>"
      subject: "<ARTIFACT-OR-HANDOFF>"
      status: "<PASSED | FAILED | PARTIAL | NOT_RUN>"

      checks:
        - id: "<CHECK-ID>"
          description: "<CHECK>"
          result: "<PASSED | FAILED | NOT_RUN | NOT_APPLICABLE>"
          evidence: []
          notes: "<NOTES>"

      defects: []
      risks: []
      limitations: []
      recommendation: "<APPROVE | REJECT | APPROVE_WITH_CONDITIONS | MORE_EVIDENCE_REQUIRED>"

---

# 14. Execution Workflow

Generate `protocols/execution-workflow.md`.

Include:

1. Intake.
2. Context validation.
3. Impact analysis.
4. Contract identification.
5. Agent selection.
6. Task delegation.
7. Parallel and sequential execution.
8. Handoff.
9. Independent validation.
10. Integration review.
11. Approval.
12. Release readiness.
13. Post-release or operational handoff when applicable.

Represent dependencies using a Mermaid diagram when the target platform supports it, plus a plain-text fallback.

Distinguish:

- Mandatory agents.
- Conditional agents.
- Review-only agents.
- User approval points.

---

# 15. Approval Gates

Generate `governance/approval-gates.md`.

At minimum evaluate gates for:

- Scope changes.
- Provider or platform selection.
- Public contract changes.
- Breaking API or schema changes.
- Destructive data changes.
- Identity and authorization changes.
- Billing and entitlement changes.
- Sensitive-data processing.
- Production infrastructure changes.
- Compliance controls.
- High-impact AI behavior.
- Release approval.

For every gate define:

- Trigger.
- Required evidence.
- Approver.
- Blocking status.
- Permitted actions before approval.
- Rollback or rejection behavior.

Do not assign business-risk acceptance to an AI agent.

---

# 16. Quality Gates

Generate `governance/quality-gates.md`.

Define project-specific and role-specific gates for applicable:

- Requirements traceability.
- Contract compatibility.
- Code quality.
- Data integrity and migration safety.
- Authentication and authorization.
- Billing consistency and idempotency.
- AI output validation and evaluation.
- Security and dependency risk.
- Privacy and compliance.
- Accessibility.
- Performance and reliability.
- Configuration and secret safety.
- Infrastructure validation.
- Test evidence.
- Observability.
- Rollback readiness.
- Documentation.

Every gate must specify:

- Owner.
- Validator.
- Evidence.
- Pass condition.
- Failure behavior.

Do not require tools or tests unsupported by the approved project without labeling them as proposals.

---

# 17. Integration Policy

Generate `governance/integration-policy.md`.

Require checks for applicable boundaries:

- Frontend ? API.
- API ? persistence.
- API ? identity.
- API ? billing.
- API ? AI.
- Services ? events or queues.
- Application ? external integrations.
- Runtime ? configuration and secrets.
- Deployment ? infrastructure.
- Implementation ? tests.
- Production behavior ? observability.

For each boundary define:

- Contract.
- Source of truth.
- Producer.
- Consumer.
- Compatibility validation.
- Failure behavior.
- Observability.
- Approval requirements.

---

# 18. Conflict Resolution

Generate `governance/conflict-resolution.md`.

Resolve conflicts in this order:

1. Safety, legal, privacy, and compliance requirements.
2. Approved user requirements.
3. Verified existing contracts and compatibility constraints.
4. Approved Architecture Decision Records.
5. Primary ownership.
6. Quality and operational evidence.
7. Architect recommendation.

Agents must report conflicts rather than silently choose a side.

Escalate when:

- Two approved requirements conflict.
- Ownership is ambiguous.
- A provider decision is undecided.
- A contract change is breaking.
- Security and product requirements conflict.
- Evidence is insufficient.
- Remediation exceeds authorized scope.

---

# 19. Change Management

Generate `governance/change-management.md`.

Define how to:

- Propose a requirement change.
- Add, remove, or specialize an agent.
- Change ownership.
- Add or replace a provider.
- Change a contract.
- Introduce a breaking change.
- Update the shared context.
- Update prompt-pack versions.
- Revalidate downstream agents and artifacts.

Require impact analysis before approval.

Use semantic versioning guidance:

- Patch: clarification without behavior or ownership change.
- Minor: backward-compatible capability, agent, or contract addition.
- Major: breaking contract, ownership, governance, or workflow change.

---

# 20. Risk and Decision Registers

Generate project-specific templates:

## `governance/risk-register.yaml`

    risks:
      - id: "<RISK-ID>"
        description: "<DESCRIPTION>"
        category: "<CATEGORY>"
        probability: "<LOW | MEDIUM | HIGH>"
        impact: "<LOW | MEDIUM | HIGH | CRITICAL>"
        owner: "<AGENT-OR-HUMAN>"
        mitigation: []
        status: "<OPEN | MITIGATED | ACCEPTED | CLOSED>"
        approval_required_for_acceptance: true

## `governance/decision-register.yaml`

    decisions:
      - id: "<DECISION-ID>"
        title: "<TITLE>"
        status: "<PROPOSED | APPROVED | REJECTED | SUPERSEDED>"
        context: "<CONTEXT>"
        decision: "<DECISION>"
        alternatives: []
        consequences: []
        evidence: []
        owner: "<OWNER>"
        approver: "<APPROVER>"

Do not assign human risk acceptance to an AI agent.

---

# 21. README and Operating Quick Start

Generate `README.md` containing:

1. Purpose.
2. Project mode.
3. Approved fleet.
4. Directory map.
5. Source-of-truth hierarchy.
6. How to initialize the Architect.
7. How to submit a request.
8. How the Architect delegates tasks.
9. How to invoke sub-agents.
10. How handoffs work.
11. How approvals work.
12. How validation works.
13. How to update prompts.
14. Brownfield read-only safety.
15. Common failure states.
16. Minimal copy-paste examples.

---

# 22. Invocation Examples

Generate:

## `examples/project-invocation.md`

A complete initial Architect invocation containing references to the shared governance artifacts and one realistic project request derived from the approved specification.

## `examples/delegation-examples.md`

Provide:

- One simple single-agent task.
- One multi-agent task with parallel work.
- One task requiring approval.
- One blocked task.
- One Brownfield or Hybrid task if applicable.

## `examples/handoff-example.yaml`

Provide a realistic, non-secret handoff matching one approved project capability.

Examples must not claim that any work was actually performed.

---

# 23. Generation Order

Generate artifacts in this order:

1. Review the approved specification.
2. Report blocking contradictions, if any.
3. Confirm the final fleet.
4. Produce the proposed file tree.
5. Generate manifest.
6. Generate shared context.
7. Generate registry, ownership, and contract artifacts.
8. Generate Architect prompt.
9. Generate sub-agent prompts.
10. Generate protocols.
11. Generate governance policies.
12. Generate examples and README.
13. Run consistency review.
14. Produce a final generation report.

---

# 24. Consistency Review

Before finalizing, verify:

## Fleet

- Every agent is approved or clearly justified by the specification.
- No excluded agent was generated.
- No duplicate ownership exists.
- Every concern has one primary owner.
- Provider specialization matches approved decisions.

## Prompts

- Every prompt contains all 18 required sections.
- Inputs and outputs are structured.
- Non-responsibilities are explicit.
- Security controls are role-specific.
- Quality gates are measurable.
- Definitions of Done are verifiable.
- Escalations are concrete.

## Contracts

- Registry owners match the ownership matrix.
- Agent prompts reference the same contracts.
- Proposed contracts are not represented as approved.
- Breaking changes require approval.

## Workflow

- Delegations match agent ownership.
- Handoff consumers exist.
- Blocking dependencies are represented.
- Approval gates have human or authorized approvers.
- Validation ownership is independent where required.

## Provider Neutrality

- No provider was introduced from an example.
- Generic agents remain generic where providers are undecided.
- Specialized agents use only verified or approved providers.
- Mixed-provider boundaries are explicit.

## Honesty and Safety

- No secrets are present.
- No implementation, test, deployment, or repository claims were fabricated.
- Access and validation limitations are visible.

Fix inconsistencies before returning the pack.

---

# 25. Required Response Format

Return:

## 1. Generation Summary

- Project.
- Project mode.
- Specification status.
- Number of agents.
- Generic versus specialized agents.
- Assumptions.
- Unresolved blockers.

## 2. Final Agent Fleet

| Agent | Type | Provider Scope | Primary Ownership | Invocation |
|---|---|---|---|---|

## 3. Proposed File Tree

## 4. Generated Files

For every file:

### `<relative/path>`

```text
<complete file content>
```

Use a more precise fence language where appropriate, such as `yaml` or `markdown`.

Do not omit required content. Do not use references such as "same as above."

## 5. Consistency Review

Report passed checks and remaining limitations.

## 6. Generation Result

Choose:

- `PROMPT_PACK_READY`
- `PROMPT_PACK_READY_WITH_DOCUMENTED_LIMITATIONS`
- `PROMPT_PACK_BLOCKED`

If blocked, do not generate misleading partial prompts as though complete.

Begin now.
```

The generated response can be large because it contains the complete project-specific agent system. Save each proposed file under the path shown by the generator.
