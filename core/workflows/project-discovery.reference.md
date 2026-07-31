# Project Discovery — Reference

## Question format

Explain each option in plain language. Then add:

```markdown
**Recommended:** <option suggested by the agent>

**Why:** <one short reason based on known project context>
```

Include this block only when the known project context supports a recommendation.
Otherwise omit it. Recommendations are proposals until the user approves them.

## Greenfield topic checklists

### Product and business

- Problem, business outcome, users, stakeholders
- Current alternatives / manual process
- Success criteria, deadlines, budget sensitivity
- In-scope vs explicitly excluded

### Users, roles, journeys

- User categories; anonymous vs authenticated
- Admin / support roles; tenancy
- Primary and failure journeys
- Accessibility, localization, regions

### Functional capabilities

- Core workflows; CRUD / export / retention
- Search, reporting, notifications, media
- Collaboration / realtime; jobs / automation
- Third-party integrations; admin tooling

### Identity (only if required)

- Populations, login methods, MFA, federation
- Sessions; RBAC/ABAC/permissions; multi-tenancy
- Provisioning; audit; org standards
- Providers only after capability clarity

### Payments (only if required)

- One-time / recurring / usage; plans; entitlements
- Trials, tax, refunds, currencies, portal
- Failure recovery; reconciliation; compliance
- Providers only after capability clarity

### Data

- Entities, sources of truth, consistency
- Volume, read/write patterns, search/analytics
- Files, residency, retention, backup, isolation

### AI (only if required)

- Use case, inputs/outputs, determinism
- Data sensitivity, retrieval, tools, human review
- Latency/cost; do not add AI 'because we can'

### Clients and UX

- Web / mobile / desktop / API-only
- Devices, a11y target, SEO, offline, design system

### Backend and integrations

- Public/internal APIs; sync/async; webhooks
- Idempotency, retries, versioning

### Hosting and delivery

- Org standards, regions, availability, growth
- Environments, CI/CD, secrets, observability, rollback

### Quality and compliance

- Test expectations; threat profile; privacy; frameworks

### Technology selection

When undecided and material:

| Option | Advantages | Disadvantages | Cost | Ops complexity | Fit |
|---|---|---|---|---|---|

Present ≥3 options; recommend; wait for approval.

## Specification section list

1. Executive Summary  
2. Project Mode (`GREENFIELD`)  
3. Business Goals and Success Measures  
4. Scope (included / excluded / deferred)  
5. Target Users and Stakeholders  
6. User Roles and Access Needs  
7. Core User Journeys  
8. Functional Requirements (stable IDs)  
9. Non-Functional Requirements  
10. Current Repository and System  
11. Current Technology Stack (verified / user-reported / unknown)  
12. Capability and Provider Matrix  
13. Current Architecture (N/A or brief for greenfield)  
14. Proposed Target Architecture  
15. Domain and Data Requirements  
16. API and Event Contracts  
17. Identity and Authorization  
18. Payments and Billing  
19. AI and Automated Decisions  
20. Client Applications and User Experience  
21. External Integrations  
22. Hosting, Infrastructure, and Networking  
23. Source Control, CI/CD, and Release Management  
24. Environment, Secrets, and Configuration  
25. Security, Privacy, and Compliance  
26. Testing and Quality Engineering  
27. Observability and Operations  
28. Environments and Deployment Strategy  
29. Confirmed Decisions  
30. Proposed Decisions Requiring Approval  
31. Assumptions  
32. Open Questions  
33. Risks and Technical Debt  
34. Contradictions Detected  
35. Existing Agent / Skills / Documentation Procedures  
    (`NOT_FOUND` for pure greenfield with no pasted templates; otherwise follow
    [existing-operating-procedures.md](existing-operating-procedures.md))  
36. Recommended Agent Fleet (id, why, ownership, non-responsibilities, I/O, collaborators, approval)  
37. Excluded Agents (with reasons)  
38. Preliminary Ownership Matrix  
39. Proposed Execution Order  
40. Evidence Index (`Not applicable — no existing repository was analyzed.`)  
41. Repository Access and Validation Limitations  
42. Readiness Recommendation  

Readiness values:

- `READY_FOR_PROMPT_PACK_GENERATION`
- `READY_WITH_DOCUMENTED_ASSUMPTIONS`
- `NOT_READY — BLOCKING_INFORMATION_REQUIRED`

## After approval

Approval automatically runs `core/workflows/generate-prompt-pack.md` in `SAVE`
mode. The selected fleet is written as separate files under `agent-system/` and
verified before completion is reported.

Do not ask the user to select a generation format. Implementation planning,
repository task mapping, and application implementation happen later through
`/operate` or a separate explicit request.

## Example agent IDs (not defaults)

`principal-software-architect`, `architecture-engineer`, `product-manager`, `ui-ux-designer`, `frontend-engineer`, `backend-engineer`, `database-engineer`, `identity-access-engineer`, `payment-billing-engineer`, `ai-llm-engineer`, `cloud-platform-engineer`, `cicd-engineer`, `security-engineer`, `qa-engineer`, `code-review-engineer`, `technical-writer`

Specialize only when a provider is verified or approved (e.g. `auth0-identity-engineer`).
