# Ownership Matrix — Sample Catalog API

| Concern | Primary Owner | Supporting Agents | Reviewer | Approval Authority |
|---|---|---|---|---|
| Request orchestration | principal-software-architect | — | user | user |
| Architecture design / redesign | architecture-engineer | principal-software-architect | principal-software-architect | principal-software-architect |
| Catalog HTTP API | backend-engineer | architecture-engineer, principal-software-architect | principal-software-architect | principal-software-architect |
| Catalog data model | backend-engineer | architecture-engineer, principal-software-architect | principal-software-architect | user (breaking) |
| Provider selection | user | principal-software-architect, architecture-engineer | principal-software-architect | user |
| Integration review | principal-software-architect | backend-engineer, architecture-engineer | user | principal-software-architect |

Rules: one primary owner per concern; no duplicate ownership.
