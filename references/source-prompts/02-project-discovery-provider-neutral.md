# Step 2 - Run Provider-Neutral Project Discovery

> **Source of truth:** `core/workflows/`. If this playbook conflicts with `core/`, **core wins**. This file is progressive disclosure / paste legacy.

**Preferred path:** follow `core/workflows/project-discovery.md` (and `brownfield-research.md` for B/C) via `/discover`, `/brownfield`, `/hybrid`, or `/architect`. Slash map also includes `/upgrade-architect` and `/update-context` (`core/slash-commands.md`). Prefer the host IDE native choice UI; ask exactly one question at a time. Detect existing operating procedures and record adaptation mode `FOLLOW` / `COMPOSE` / `BRIDGE` / `NONE`.

**After approval:** automatically run generate-prompt-pack in `SAVE` mode (write under `agent-system/`). Do not wait for the user to paste Step 3.

**Fallback:** after Step 1 responds with `PROMPT FACTORY READY`, paste the entire prompt below into the same AI chat.

```markdown
# Command: DISCOVER_PROJECT

Conduct a structured, provider-neutral discovery process before generating any Architect, agent, sub-agent, implementation plan, or code.

Your objective is to produce an evidence-based project specification that can later be used to execute:

`GENERATE_COMPLETE_PROMPT_PACK`

The process must support:

- `GREENFIELD` - a future project without an existing implementation.
- `BROWNFIELD` - an existing project that must be researched first.
- `HYBRID` - an existing project receiving significant new functionality.

Do not generate any agents during this step.

---

# 1. Discovery Principles

## 1.1 Capability First

Discover required capabilities before discussing providers or products.

Examples:

- Ask whether identity is required before discussing identity providers.
- Ask whether billing is required before discussing payment providers.
- Ask about persistence needs before selecting a database.
- Ask about deployment constraints before selecting a cloud.
- Ask about intelligent behavior before selecting an AI model or platform.
- Ask about delivery requirements before selecting CI/CD tooling.

## 1.2 Provider Neutrality

Do not assume any provider, platform, framework, language, architecture, or tool.

A provider or technology becomes part of the project only when it is:

- Verified from accessible project evidence.
- Explicitly required by the user.
- Required by a user-confirmed organizational standard.
- Approved after comparison and recommendation.
- Recorded as an unapproved proposal.

Examples in the Prompt Factory are not project defaults.

## 1.3 Minimal Necessary Complexity

Prefer the simplest design that satisfies confirmed requirements and constraints.

Do not assume the need for:

- Microservices.
- Event-driven architecture.
- Kubernetes.
- Serverless functions.
- Multiple databases.
- Distributed caching.
- Multi-cloud deployment.
- Artificial intelligence.
- Authentication.
- Payments.
- Realtime behavior.
- Native mobile applications.

## 1.4 Evidence Over Assumption

Maintain a discovery ledger using:

- `VERIFIED_PROJECT_FACT`
- `USER_CONFIRMED_REQUIREMENT`
- `USER_REPORTED_FACT`
- `CONSTRAINT`
- `PREFERENCE`
- `APPROVED_DECISION`
- `PROPOSED_DECISION`
- `ASSUMPTION`
- `RISK`
- `OPEN_QUESTION`
- `BLOCKER`

Never present an assumption, proposal, or user report as repository-verified evidence.

---

# 2. Interview Rules

1. Ask exactly one question per message.
2. Wait for the user's answer before continuing.
3. Do not combine several questions into one sentence or bullet list.
4. Prefer the host IDE native structured-question / multiple-choice UI when available so options are clickable; use plain-text multiple-choice only as fallback.
5. Always permit a custom answer.
6. Explain unfamiliar terminology briefly.
7. Do not ask for information already established by reliable evidence.
8. Record the answer internally using the discovery classifications.
9. If the user does not know, recommend a sensible option only after enough context exists.
10. Mark recommendations as `PROPOSED_DECISION` until approved.
11. Detect contradictions and ask the user to resolve material ones.
12. Do not generate agents, prompts, plans, or implementation code during discovery.
13. Do not edit an existing repository during discovery.
14. Do not request secrets, credentials, tokens, private keys, production connection strings, or real customer data.
15. Ask no more than 25 primary discovery questions without requesting permission to continue.
16. Use follow-up questions only when the answer materially affects architecture, security, cost, scope, or agent selection.
17. Adapt later questions to prior answers and project evidence.
18. Skip irrelevant capability areas.
19. Do not ask the user to select technologies prematurely.
20. End with an approval gate, not prompt generation.

---

# 3. First Required Question

Your first response must contain exactly:

Is this:

A. GREENFIELD - a future project without an existing implementation

B. BROWNFIELD - an existing project that should be researched first

C. HYBRID - an existing project receiving significant new functionality

Reply with A, B, or C.

Wait for the user's answer before doing anything else.

---

# 4. Greenfield Discovery Protocol

If the user selects `GREENFIELD`, discover the project progressively.

Do not mechanically ask every possible question. Ask only what is relevant, but cover all material unknowns before producing the specification.

## 4.1 Product and Business Context

Discover:

- The problem being solved.
- The desired business outcome.
- Target users or customers.
- Stakeholders and decision-makers.
- Current alternatives or manual processes.
- Success criteria and measurable outcomes.
- Initial scope and explicitly excluded scope.
- Delivery expectations and important deadlines.
- Budget or cost sensitivity.

## 4.2 Users, Roles, and Journeys

Discover:

- User categories.
- Internal and external users.
- Anonymous versus authenticated access.
- Administrative or support roles.
- Tenant or organization boundaries.
- Primary user journeys.
- Failure and recovery journeys.
- Accessibility, localization, and regional needs.

Do not define a role model before understanding required actions and data boundaries.

## 4.3 Functional Capabilities

Discover:

- Core workflows.
- Data creation, reading, updating, deletion, export, and retention.
- Search, filtering, reporting, and analytics.
- Notifications and communications.
- File or media handling.
- Collaboration or realtime behavior.
- Scheduling, queues, jobs, or automation.
- Third-party integrations.
- Administrative and support tooling.

## 4.4 Identity and Access

First determine whether authentication is required.

If required, discover:

- User populations.
- Login methods.
- Social or enterprise federation.
- Passwordless or MFA requirements.
- Session behavior.
- Roles, permissions, or attributes.
- Multi-tenancy and organization membership.
- Service-to-service access.
- User provisioning and lifecycle.
- Audit requirements.
- Existing organizational standards.

Only discuss provider options after capability requirements are understood.

## 4.5 Payments and Billing

First determine whether the system requires payments, subscriptions, invoicing, marketplace transactions, or internal billing.

If required, discover:

- One-time, recurring, or usage-based charging.
- Products, plans, prices, and entitlements.
- Trials, discounts, credits, taxes, and refunds.
- Currencies and countries.
- Customer self-service.
- Failed-payment recovery.
- Reconciliation and financial reporting.
- Marketplace or split-payment needs.
- Compliance and organizational provider constraints.

Do not select a payment provider before these requirements are clear.

## 4.6 Data and Persistence

Discover:

- Core business entities and relationships.
- Source of truth for each major entity.
- Transactional consistency needs.
- Expected volume and growth.
- Read/write patterns.
- Search and analytical needs.
- File and object storage.
- Data import, export, and synchronization.
- Retention and deletion.
- Backup and recovery.
- Data residency.
- Tenant isolation.
- Sensitive or regulated data.

## 4.7 AI and Automated Decision-Making

First determine whether AI behavior is required.

If required, discover:

- User-facing use case.
- Inputs and expected outputs.
- Required determinism.
- Model or provider restrictions.
- Sensitive-data exposure.
- Retrieval or external knowledge needs.
- Tool use and action execution.
- Human-review requirements.
- Quality evaluation.
- Safety risks.
- Latency and cost limits.
- Logging and retention constraints.

Do not add AI merely because it could be useful.

## 4.8 User Experience and Client Applications

Discover:

- Required application types: web, mobile, desktop, API-only, embedded, or other.
- Supported devices and browsers.
- Responsive behavior.
- Accessibility target.
- Localization.
- SEO and public discoverability.
- Offline behavior.
- Realtime interaction.
- Design-system or branding constraints.
- Existing design assets or standards.

## 4.9 Backend and Integration Needs

Discover:

- Public and internal APIs.
- Synchronous and asynchronous processing.
- Background jobs.
- External systems.
- Import/export contracts.
- Webhooks or callbacks.
- Rate limits.
- Idempotency needs.
- Error and retry behavior.
- Versioning and backward compatibility.

## 4.10 Hosting, Delivery, and Operations

Discover:

- Existing organizational hosting standards.
- Required deployment regions.
- Availability and disaster-recovery targets.
- Expected traffic and growth.
- Latency targets.
- Environment needs.
- Source-control requirements.
- CI/CD requirements.
- Infrastructure ownership.
- Secret-management requirements.
- Logging, metrics, tracing, and alerting.
- Operational support model.
- Rollback and incident response.

## 4.11 Quality, Security, and Compliance

Discover:

- Testing expectations.
- Release-quality evidence.
- Threat profile.
- Security review requirements.
- Privacy obligations.
- Applicable compliance frameworks.
- Auditability.
- Retention and deletion rules.
- Dependency and supply-chain controls.
- Performance and load validation.
- Accessibility validation.

## 4.12 Technology Selection

Only after capabilities and constraints are sufficiently understood:

1. Record required or preferred technologies.
2. Identify undecided capability providers.
3. For each material undecided choice, present no more than three suitable options.
4. Compare only criteria relevant to this project.
5. Recommend one when evidence supports it.
6. Ask the user to approve or reject the recommendation.

Use:

| Option | Advantages | Disadvantages | Cost Considerations | Operational Complexity | Project Fit |
|---|---|---|---|---|---|

---

# 5. Brownfield Discovery Protocol

If the user selects `BROWNFIELD`, analyze the existing system before recommending agents or changes.

## 5.1 Repository Access Check

Ask whether direct access to the repository is available in the current environment.

### Direct Access Available

If tools can access the repository:

1. State the accessible repository root.
2. Confirm read-only analysis.
3. Inspect high-signal files incrementally.
4. Use targeted searches rather than reading the whole repository.
5. Ask questions only for information that evidence cannot establish.

### Direct Access Unavailable

If direct access is unavailable:

1. State that the repository cannot be verified directly.
2. Do not imply that it was inspected.
3. Ask for one safe artifact at a time, choosing from:
   - A sanitized directory tree.
   - Project or package manifests.
   - README and architecture documentation.
   - Selected configuration templates.
   - API or schema definitions.
   - Pipeline and infrastructure files.
   - Relevant implementation files.
4. Explain that secrets must be removed before sharing.
5. Classify supplied descriptions as `USER_REPORTED_FACT`.

## 5.2 Repository Safety

During Brownfield discovery:

- Remain read-only.
- Do not modify, format, migrate, or generate repository files.
- Do not install dependencies.
- Do not build or run tests.
- Do not start applications or services.
- Do not deploy.
- Do not contact external systems.
- Do not access production data.
- Do not expose secret values.
- Do not claim runtime behavior from source inspection alone.

Additional actions require separate explicit authorization.

If a likely secret is found:

1. Do not reproduce its value.
2. Report only the path and secret category.
3. Mark it as a security risk.
4. Recommend appropriate revocation or rotation when warranted.

## 5.3 Inspection Order

Inspect in stages, stopping when enough evidence exists for the current question.

### Stage 1 - Orientation

- Root structure.
- README and contribution guides.
- Solution, workspace, and project manifests.
- Dependency manifests and lock files.
- Build configuration.
- Architecture documents and decision records.

### Stage 2 - Application Topology

- User-facing applications.
- APIs and services.
- Shared libraries.
- Domain modules.
- Workers, jobs, and schedulers.
- Data-access layers.
- External integrations.

### Stage 3 - Contracts and Data

- API specifications.
- Request and response models.
- Event schemas.
- Database entities and migrations.
- Validation and serialization.
- Error conventions.
- Sources of truth.

### Stage 4 - Identity and Authorization

- Identity SDKs and middleware.
- Login and callback flows.
- Token or session validation.
- Roles, permissions, and policies.
- Tenant isolation.
- User lifecycle and synchronization.
- Frontend guards and backend enforcement.

### Stage 5 - Payments and Billing

- Provider SDKs.
- Checkout or payment creation.
- Subscription or invoice lifecycle.
- Webhook verification.
- Idempotency.
- Internal entitlement synchronization.
- Failed-payment and reconciliation logic.

### Stage 6 - AI

- Model clients.
- Prompt definitions and versions.
- Structured-output schemas.
- Retrieval, embeddings, and vector search.
- Untrusted-content boundaries.
- Output validation.
- Evaluation, latency, rate-limit, and cost controls.

### Stage 7 - Infrastructure and Delivery

- Container definitions.
- Infrastructure as Code.
- Hosting manifests.
- CI/CD pipelines.
- Environment approvals.
- Secret references.
- Identity and access for workloads.
- Rollback strategy.

### Stage 8 - Configuration

- Local, test, staging, and production configuration.
- Build-time, deployment-time, and runtime values.
- Public versus secret values.
- Feature flags.
- Configuration validation.

### Stage 9 - Quality

- Unit, integration, contract, end-to-end, security, performance, and AI-evaluation tests.
- Test data and mocking.
- Coverage configuration.
- Pipeline quality gates.

Reading tests does not prove they pass.

### Stage 10 - Observability and Operations

- Structured logging.
- Correlation and tracing.
- Metrics and health checks.
- Alerting and dashboards.
- Runbooks.
- Backup, recovery, and incident procedures.

## 5.4 Evidence Format

For material findings use:

| Finding | Classification | Evidence | Confidence | Impact |
|---|---|---|---|---|

Evidence should cite safe:

- File paths.
- Component or symbol names.
- Configuration key names without values.
- API routes.
- Schema or migration names.
- Pipeline stage names.
- Test-suite names.

## 5.5 Current-State Reconstruction

Reconstruct only evidence-supported elements:

1. Repository structure.
2. Applications and services.
3. Frontend architecture.
4. Backend architecture.
5. Data architecture.
6. API and event boundaries.
7. Identity and authorization.
8. Billing.
9. AI.
10. Infrastructure.
11. CI/CD.
12. Configuration and secret flow.
13. Quality strategy.
14. Observability.
15. Deployment topology.
16. External dependencies.

Label unknown and proposed elements clearly.

## 5.6 Gap Analysis

Compare current state with confirmed requirements and relevant engineering standards.

For each gap record:

- Severity: `CRITICAL`, `HIGH`, `MEDIUM`, `LOW`, or `INFORMATIONAL`.
- Finding.
- Evidence.
- Impact.
- Recommended owner.
- Recommended remediation.
- Whether it blocks prompt generation.
- Whether approval is required.

Separate:

- Current state.
- Risk.
- Recommended future state.

Do not automatically redesign the project.

---

# 6. Hybrid Discovery Protocol

If the user selects `HYBRID`:

1. Perform Brownfield access and current-state research first.
2. Ask about the planned functionality after the current system is understood.
3. Separate verified current behavior from proposed future behavior.
4. Identify compatibility constraints.
5. Identify data, API, identity, billing, AI, infrastructure, and operational migrations.
6. Identify regression and rollout risks.
7. Classify each material area:
   - `PRESERVE`
   - `EXTEND`
   - `REFACTOR`
   - `REPLACE`
   - `REQUIRES_DECISION`
8. Recommend agents for both current-system understanding and future implementation.
9. Prefer incremental modernization.
10. Require explicit approval for replacement or breaking changes.

---

# 7. Capability and Provider Matrix

Maintain this matrix during discovery for relevant capabilities:

| Capability | Required? | Current Implementation | Preferred Provider/Technology | Decision Status | Evidence | Source of Truth | Owner Candidate |
|---|---:|---|---|---|---|---|---|

Potential capability areas include:

- Product and requirements management.
- User experience and design.
- Frontend.
- Backend.
- Identity and access.
- Authorization.
- Database and persistence.
- Search.
- Files and media.
- Realtime communication.
- Messaging and background jobs.
- Payments and billing.
- AI and model inference.
- Retrieval and vector search.
- External integrations.
- Hosting and networking.
- CI/CD.
- Infrastructure as Code.
- Secrets and configuration.
- Security and compliance.
- Testing.
- Observability and operations.
- Documentation.

Include only relevant rows in the final specification.

---

# 8. Agent Selection During Discovery

Do not generate prompts yet. Identify candidate agents only after requirements and evidence justify them.

For every candidate agent determine:

1. Required capability.
2. Why a separate owner is justified.
3. Generic or provider-specialized role.
4. Primary responsibilities.
5. Explicit non-responsibilities.
6. Required inputs.
7. Expected outputs.
8. Collaborators.
9. Quality owner.
10. Approval authority.

Prefer generic capability agents when providers remain undecided.

Use provider-specialized agents only when the provider is verified or approved.

Do not create duplicate generic and provider-specific ownership.

Potential agents are examples, not defaults:

- Principal Architect or Orchestrator.
- Product Manager.
- Business Analyst.
- UI/UX Designer.
- Accessibility Engineer.
- Frontend Engineer.
- Backend Engineer.
- Mobile Engineer.
- Database Engineer.
- Data Engineer.
- Identity and Access Engineer.
- Payment and Billing Engineer.
- AI/LLM Engineer.
- Integration Engineer.
- Cloud Platform Engineer.
- CI/CD Engineer.
- Security Engineer.
- QA Engineer.
- Performance Engineer.
- Observability/SRE Engineer.
- Technical Writer.

Exclude agents that are not justified.

---

# 9. Discovery Completion Criteria

Discovery is complete only when:

- Project mode is confirmed.
- Business goals and scope are understood.
- Core users and journeys are identified.
- Material functional requirements are recorded.
- Material non-functional requirements are recorded.
- Relevant capabilities are identified.
- Existing providers are verified or marked unknown.
- Required providers and technologies are confirmed.
- Important undecided choices are proposed or left visibly open.
- Security and compliance needs are recorded.
- Data boundaries and sources of truth are understood sufficiently.
- Deployment and operational constraints are understood sufficiently.
- Brownfield evidence and access limitations are documented.
- Major contradictions are resolved or marked as blockers.
- Candidate agents can be justified.
- Remaining assumptions and risks are visible.

If these criteria cannot be met, classify readiness as:

`NOT_READY - BLOCKING_INFORMATION_REQUIRED`

---

# 10. Required Final Specification

When discovery is complete, produce:

# PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION

Include:

## 1. Executive Summary

## 2. Project Mode

State `GREENFIELD`, `BROWNFIELD`, or `HYBRID`.

## 3. Business Goals and Success Measures

## 4. Scope

Separate included, excluded, and deferred scope.

## 5. Target Users and Stakeholders

## 6. User Roles and Access Needs

## 7. Core User Journeys

## 8. Functional Requirements

Use stable requirement identifiers.

## 9. Non-Functional Requirements

Include measurable targets where confirmed.

## 10. Current Repository and System

For Greenfield state:

`Not applicable - Greenfield project.`

For Brownfield and Hybrid include evidence-supported structure and topology.

## 11. Current Technology Stack

Separate:

- Verified.
- User reported.
- Unknown.

## 12. Capability and Provider Matrix

Use the required matrix and decision states.

## 13. Current Architecture

For Brownfield and Hybrid only. Distinguish evidence from inference.

## 14. Proposed Target Architecture

Separate proposals from approvals. Avoid unnecessary replacement.

## 15. Domain and Data Requirements

Include entities, sources of truth, ownership, lifecycle, residency, retention, backup, and recovery as applicable.

## 16. API and Event Contracts

Identify existing, required, proposed, and unknown contracts.

## 17. Identity and Authorization

Separate authentication, authorization, application profile, tenant membership, and service access.

## 18. Payments and Billing

Separate provider state, internal billing state, and application entitlements.

## 19. AI and Automated Decisions

Separate trusted instructions, untrusted inputs, model output, deterministic validation, and human approval.

## 20. Client Applications and User Experience

## 21. External Integrations

## 22. Hosting, Infrastructure, and Networking

## 23. Source Control, CI/CD, and Release Management

## 24. Environment, Secrets, and Configuration

Separate build-time, deployment-time, runtime, public, and secret values.

## 25. Security, Privacy, and Compliance

## 26. Testing and Quality Engineering

## 27. Observability and Operations

## 28. Environments and Deployment Strategy

## 29. Confirmed Decisions

## 30. Proposed Decisions Requiring Approval

## 31. Assumptions

## 32. Open Questions

## 33. Risks and Technical Debt

## 34. Contradictions Detected

## 35. Recommended Agent Fleet

For each candidate include:

- Agent ID and title.
- Generic or provider-specialized status.
- Why it is needed.
- Primary ownership.
- Explicit non-responsibilities.
- Repository or capability scope.
- Required inputs.
- Expected outputs.
- Collaborators.
- Approval authority.

## 36. Excluded Agents

State why each plausible but unnecessary agent is excluded.

## 37. Preliminary Ownership Matrix

Use:

| Concern | Primary Owner | Supporting Agents | Approval Authority |
|---|---|---|---|

## 38. Proposed Execution Order

Identify:

- Blocking dependencies.
- Parallel discovery or delivery work.
- Approval gates.
- Final integration owner.

## 39. Evidence Index

For Brownfield and Hybrid, list primary evidence.

For Greenfield state:

`Not applicable - no existing repository was analyzed.`

## 40. Repository Access and Validation Limitations

State:

- What was accessible.
- What was not accessible.
- What was not inspected.
- Which commands, if any, were executed.
- Whether builds or tests were run.
- What could not be verified.

## 41. Readiness Recommendation

Choose exactly one:

- `READY_FOR_PROMPT_PACK_GENERATION`
- `READY_WITH_DOCUMENTED_ASSUMPTIONS`
- `NOT_READY - BLOCKING_INFORMATION_REQUIRED`

---

# 10.1 Existing Agent / Skills / Documentation Procedures

Include a specification section with this title. Record:

1. Detection status: `FOUND` | `PARTIAL` | `NOT_FOUND`
2. Adaptation mode: `FOLLOW` | `COMPOSE` | `BRIDGE` | `NONE`
3. Procedure inventory (path, type, purpose, confidence)
4. Canonical workflow chain if any
5. Domain memory locations
6. Work-tracking binding
7. Conflicts with Architect defaults and proposed resolutions
8. Adoption decisions after approval

For Greenfield with no external skills pack: `NOT_FOUND` / mode `NONE`. Follow `core/workflows/existing-operating-procedures.md`. Slash map reminders: `/upgrade-architect`, `/update-context`.

---

# 11. Approval Gate

End the specification with exactly:

APPROVAL REQUIRED

Then stop and wait.

Accept only:

- `APPROVED`
- `APPROVED WITH CHANGES: <changes>`
- `REVISE: <requested revisions>`

Do not generate the prompt pack before approval.

After `APPROVED` or `APPROVED WITH CHANGES`:

1. Persist `agent-system/project-specification.md` with approved status.
2. Immediately run `GENERATE_COMPLETE_PROMPT_PACK` / `core/workflows/generate-prompt-pack.md` in `SAVE` mode.
3. Write real files under `agent-system/`; do not dump the complete pack into chat; return a short summary.
4. Bind `existing_operating_procedures` into shared context when adopted (`FOLLOW` / `COMPOSE` / `BRIDGE` / `NONE`).
5. Do not ask the user to paste Step 3 or choose a generation format.
6. Do not implement the application until the user starts `/operate`.

If file-writing tools are unavailable, report `PROMPT_PACK_BLOCKED` and offer `PREVIEW` instead of pretending chat output is a saved pack.

---

# 12. Begin

Begin now by asking only the exact project-mode question from Section 3.
```

Expected response:

```text
Is this:

A. GREENFIELD - a future project without an existing implementation

B. BROWNFIELD - an existing project that should be researched first

C. HYBRID - an existing project receiving significant new functionality

Reply with A, B, or C.
```

**Preferred next step after approval:** auto-`SAVE` generate under `agent-system/` (see `core/workflows/generate-prompt-pack.md`), then `/operate`.

**Fallback:** continue answering one question at a time in the same chat until the specification ends with `APPROVAL REQUIRED`.
