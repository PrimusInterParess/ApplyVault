# Sample Project Requirements Spec (abbreviated)

This is an example of a completed discovery output for demos and tests. It is **not** a real project.

```markdown
# PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION

## 1. Executive Summary
A greenfield task-tracking API for small internal teams with optional SSO later.

## 2. Project Mode
GREENFIELD

## 3. Business Goals and Success Measures
- Replace shared spreadsheets for task assignment
- Success: 1 team using it weekly within 30 days

## 4. Scope
- Included: projects, tasks, assignees, basic comments
- Excluded: billing, mobile apps, AI assistants
- Deferred: SSO, notifications

## 5–7. Users / Roles / Journeys
- Users: team members, team lead
- Journeys: create project, create task, assign, complete

## 8. Functional Requirements
- FR-1 Create/list projects
- FR-2 Create/update/complete tasks
- FR-3 Assign tasks to users

## 9. Non-Functional Requirements
- Single region, <200ms p95 for list endpoints at low scale

## 12. Capability and Provider Matrix
| Capability | Required? | Current | Preferred | Status | Evidence | Source of Truth | Owner Candidate |
|---|---|---|---|---|---|---|---|
| Identity | YES | None | Undecided | UNDECIDED | User | TBD | identity-access-engineer |
| Payments | NO | None | None | NOT_APPLICABLE | User | N/A | — |
| AI | NO | None | None | NOT_APPLICABLE | User | N/A | — |
| Hosting | YES | None | Undecided | UNDECIDED | User | TBD | cloud-platform-engineer |

## 35. Recommended Agent Fleet
- principal-software-architect
- backend-engineer
- frontend-engineer
- qa-engineer
- cicd-engineer

## 36. Excluded Agents
- stripe-billing-engineer — payments not required
- ai-llm-engineer — AI not required

## 41. Readiness Recommendation
READY_WITH_DOCUMENTED_ASSUMPTIONS

APPROVAL REQUIRED
```
