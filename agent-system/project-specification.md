# PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION

**Status:** APPROVED  
**Mode:** BROWNFIELD  
**Research depth:** Standard (A)  
**Fleet focus:** Full product evolution (A)  
**Generated:** 2026-07-29  
**Approved:** 2026-07-29  
**Library:** The Architect (see VERSION / .architect/library-version)

## 1. Executive Summary

ApplyVault is an existing job-capture workspace: a Chrome MV3 extension scrapes listings; an ASP.NET Core API persists and enriches results; an Angular dashboard covers saved jobs, public search (EURES / Work in Denmark), structured CV editing/export, GitHub project summaries, and Gmail/calendar integrations. Authentication is Supabase JWT end-to-end. The repository already has a Matt Pocock–style agent skills pack (`.agents/skills`) and domain docs; The Architect was copy-installed for multi-agent orchestration. This specification reconstructs current state from read-only workspace evidence and recommends a fleet for **full product evolution** across extension, API, and Angular UI.

## 2. Project Mode

`BROWNFIELD`

## 3. Business Goals and Success Measures

| Goal | Classification | Notes |
|---|---|---|
| Evolve the full ApplyVault product across extension, API, and dashboard | `USER_CONFIRMED_REQUIREMENT` | User chose fleet focus **A** |
| Preserve existing agent delivery procedures (spec → tickets → implement → review) | `VERIFIED_PROJECT_FACT` | `.agents/skills`, `docs/agents/` |
| Keep capability changes evidence-based and tenancy-safe (per-user data) | `PROPOSED_DECISION` | Matches existing JWT + user-scoped entities |

**Success measures (proposed):** features land with CI green (`api-ci` + `frontend-ci`); domain language in `CONTEXT.md` / ADR-0001 respected for CV work; no secret leakage in agent outputs.

## 4. Scope

### Included

- Chrome extension scrape/save flows
- ASP.NET Core API (jobs, CV, search, integrations, auth session)
- Angular dashboard (jobs, search, CV Builder / CV Projects, settings)
- Structured CV catalog, import/export, GitHub CV summaries
- Gmail sync, calendar interview events, GitHub OAuth
- EURES and Jobnet search/cache
- Agent procedures: GitHub Issues, triage labels, domain docs, Architect orchestration

### Excluded

- Payments / billing product work (`VERIFIED_PROJECT_FACT`: no payment code)
- Replacing Supabase identity without an explicit approved decision
- Blind rewrite of `.agents/skills` or overwriting `CONTEXT.md` / ADRs

### Deferred

- Extension tests in CI (present locally; not in `.github/workflows/api-ci.yml`)
- Deeper background GitHub repo sync (noted as pending in README / plans)
- Multi-instance Redis/Gmail concerns when running single-replica only

## 5. Target Users and Stakeholders

| Actor | Classification | Evidence |
|---|---|---|
| Job seeker / applicant (primary end user) | `ASSUMPTION` inferred from product purpose | README capabilities |
| Repository owner / solo or small team operator | `ASSUMPTION` | GitHub `PrimusInterParess/ApplyVault` |
| AI coding agents (consumers of skills + Architect) | `VERIFIED_PROJECT_FACT` | `AGENTS.md`, `.agents/skills`, `core/` |

## 6. User Roles and Access Needs

| Role | Access | Classification | Evidence |
|---|---|---|---|
| Authenticated user | Own scrape results, CV docs/sections/entries, connections, summaries | `VERIFIED_PROJECT_FACT` | JWT Bearer; `SupabaseUserId` on users; tenancy integration tests |
| Unauthenticated | Login / guest routes only on dashboard | `VERIFIED_PROJECT_FACT` | `authGuard` / `guestGuard` in Angular routes |
| Admin / multi-tenant org roles | Not evidenced | `OPEN_QUESTION` | No org RBAC found in standard pass |

## 7. Core User Journeys

1. Sign in (dashboard password / extension OTP patterns) → authenticated API calls with Bearer JWT.
2. Scrape active job tab in extension → review fields → save to API → optional Google AI enrichment → appear in Jobs dashboard.
3. Browse/filter saved jobs; mark rejected; schedule interview; calendar event when providers connected.
4. Search EURES or Work in Denmark → inspect listing → save into ApplyVault.
5. Upload CV PDF → structured sections/entries → edit/reorder → export formatted PDF.
6. Connect GitHub → browse repos → generate CV project summaries.
7. Connect Gmail → background sync updates job rejection/interview state.

## 8. Functional Requirements

| ID | Statement | Classification |
|---|---|---|
| FR-01 | Capture job details from supported sites via MV3 extension and persist via API | `VERIFIED_PROJECT_FACT` |
| FR-02 | Review, edit, filter, and reject saved scrape results in the Angular Jobs UI | `VERIFIED_PROJECT_FACT` |
| FR-03 | Search public listings from EURES and Jobnet with shareable URL state | `VERIFIED_PROJECT_FACT` |
| FR-04 | Authenticate users with Supabase; authorize API with JWT | `VERIFIED_PROJECT_FACT` |
| FR-05 | Store and edit a Structured CV (sections/entries) validated against the section schema catalog | `VERIFIED_PROJECT_FACT` + ADR-0001 |
| FR-06 | Export Structured CV to PDF via selectable HTML templates | `VERIFIED_PROJECT_FACT` |
| FR-07 | Optional Google AI for scrape enrichment, CV import/update/suggestions/export, GitHub summaries | `VERIFIED_PROJECT_FACT` |
| FR-08 | Connect Gmail, Google/Microsoft calendar, GitHub OAuth; sync mail-driven job updates | `VERIFIED_PROJECT_FACT` |
| FR-09 | Health endpoints for liveness/readiness including DB check | `VERIFIED_PROJECT_FACT` |

## 9. Non-Functional Requirements

| ID | Statement | Classification |
|---|---|---|
| NFR-01 | Per-user data isolation for scrape/CV/integration data | `VERIFIED_PROJECT_FACT` |
| NFR-02 | Untrusted HTML sanitized in job/external listing views | `VERIFIED_PROJECT_FACT` (README) |
| NFR-03 | Rate limiting on API (global, scrape ingest, EURES, OAuth callback) | `VERIFIED_PROJECT_FACT` |
| NFR-04 | CI on `main` push/PR: .NET build + unit + integration tests; Angular `test:ci` | `VERIFIED_PROJECT_FACT` |
| NFR-05 | Optional Redis for distributed cache/locks; in-memory fallback for single replica | `VERIFIED_PROJECT_FACT` |
| NFR-06 | Secrets never logged or returned to browser (e.g. GitHub tokens server-side) | `VERIFIED_PROJECT_FACT` (README) |

## 10. Current Repository and System

Monorepo layout (verified):

| Path | Role |
|---|---|
| `extension/` | Chrome MV3 scrape/save client |
| `api/ApplyVault.Api/` | ASP.NET Core API + EF Core |
| `api/ApplyVault.Api.Tests/` | Unit tests |
| `api/ApplyVault.Api.IntegrationTests/` | HTTP/tenancy/CV integration tests |
| `frontend/applyvault-jobs-ui/` | Angular 19 dashboard |
| `shared/cv-section-catalog/` | Declarative CV section schema catalog |
| `plans/` | Product and production-readiness plans |
| `docs/agents/`, `CONTEXT.md`, `docs/adr/` | Agent + domain memory |
| `.agents/skills/` | Matt Pocock skills pack |
| `core/`, `.cursor/commands/`, `.cursor/skills/` | The Architect library (copy-install) |
| `agent-system/` | This specification (Architect pack not yet generated) |

## 11. Current Technology Stack

### Verified

- .NET `net10.0`, ASP.NET Core, EF Core SQL Server, LocalDB (Development), optional InMemory for testing
- JWT Bearer + Supabase JWKS (ES256)
- Redis optional (`ConnectionStrings:Redis`)
- Azure.Storage.Blobs or local filesystem for CV PDFs
- Gemini via HTTP (`generativelanguage.googleapis.com`) — no Google SDK package
- PdfPig, PDFsharp, QuestPDF, PuppeteerSharp for PDF/HTML export
- Angular 19, `@supabase/supabase-js`, Karma/Jasmine
- Chrome Manifest V3, Vitest (extension)
- Node 22 in CI; GitHub Actions

### User-reported

- None beyond workspace research answers (depth A, fleet focus A)

### Unknown

- Production hosting provider / exact cloud region (plans claim readiness; runtime target not pinned in this pass)
- Whether multi-replica Redis is required in the user’s current deploy

## 12. Capability and Provider Matrix

| Capability | Required | Current implementation | Preferred provider/tech | Decision status | Evidence | Source of truth | Owner candidate |
|---|---|---|---|---|---|---|---|
| Identity / auth | YES | Supabase Auth + API JWT validation | Supabase | `VERIFIED_EXISTING` | `ConfigureSupabaseJwtBearerOptions.cs`, Angular `auth.service.ts` | API + FE auth | identity-aware backend-engineer |
| Persistence | YES | EF Core + SQL Server / LocalDB | SQL Server | `VERIFIED_EXISTING` | `ApplyVaultDbContext`, csproj | API Data | backend-engineer |
| Job scrape capture | YES | MV3 extension → `api/scrape-results` | Chrome + ASP.NET | `VERIFIED_EXISTING` | `extension/`, `ScrapeResultsController` | extension + API | browser-extension-engineer / backend-engineer |
| Public job search | YES | EURES + Jobnet API clients + cache | EURES, Jobnet | `VERIFIED_EXISTING` | `Services/Eures`, `Services/Jobnet` | API | backend-engineer |
| Structured CV | YES | Sections/entries + catalog JSON | Catalog + EF | `VERIFIED_EXISTING` | ADR-0001, `shared/cv-section-catalog` | catalog + API + FE | frontend-engineer / backend-engineer |
| CV file storage | YES | Local or Azure Blob | `CvDocumentStorage:Provider` | `VERIFIED_EXISTING` | DI registration | config | backend-engineer / platform-engineer |
| AI enrichment | YES (optional at runtime) | Gemini HTTP clients + feature toggles | Google AI / Gemini | `VERIFIED_EXISTING` | `GoogleAi*`, options sections | API | ai-llm-engineer |
| Email sync | YES (feature-flagged) | Gmail OAuth + background worker | Gmail | `VERIFIED_EXISTING` | `Services/Mail` | API | backend-engineer |
| Calendar | YES (feature-flagged) | Google + Microsoft calendar providers | Google, Microsoft | `VERIFIED_EXISTING` | `CalendarConnectionsController` | API | backend-engineer |
| GitHub portfolio | YES (feature-flagged) | GitHub OAuth + AI summaries | GitHub | `VERIFIED_EXISTING` | `Services/GitHub` | API + FE | backend-engineer / frontend-engineer |
| Distributed cache | OPTIONAL | Redis or in-memory | Redis when multi-instance | `VERIFIED_EXISTING` | `DistributedInfrastructureExtensions` | config | platform-engineer |
| Payments | NO | None | N/A | `NOT_APPLICABLE` | grep + csproj | — | — |
| CI/CD | YES | GitHub Actions CI | GitHub Actions | `VERIFIED_EXISTING` | `.github/workflows/api-ci.yml` | CI | platform-engineer |

## 13. Current Architecture

```text
[Chrome MV3 extension] --Bearer JWT--> [ASP.NET Core API] <--> [SQL Server / LocalDB]
[Angular dashboard]    --Bearer JWT-->        |              <--> [Redis optional]
        |                                     |              <--> [Local disk | Azure Blob]
   Supabase Auth                              +--> Gemini HTTP
                                              +--> Gmail / Google / Microsoft / GitHub / EURES / Jobnet
```

- Controllers under `api/*` (auth, scrape-results, cv-documents, cv-projects, mail, calendar, github, eures, jobnet, health).
- Feature modules in Angular under `src/app/features/` with shell + guards.
- Extension: popup / background / content / application / infrastructure layers.

## 14. Proposed Target Architecture

**Preserve current architecture** for full-product evolution (`PROPOSED_DECISION`). Prefer incremental change at existing seams (controllers/services/facades/catalog). Do not propose a rewrite. Architect `/operate` should map tasks onto verified paths.

## 15. Domain and Data Requirements

- **CV domain language** in `CONTEXT.md`: Structured CV, Section, Entry, Section type, Typed entry fields, Section schema catalog.
- **ADR-0001**: catalog at `shared/cv-section-catalog/cv-section-catalog.json`; entry content primarily in `FieldsJson`.
- Core entities (verified): users, connected accounts, scrape results/contacts, interview events, calendar links, CV documents/sections/entries, CV project summaries.

## 16. API and Event Contracts

- REST controllers under `api/*` (no separate OpenAPI artifact required for this pass; OpenAPI package present).
- Scrape ingest: `POST` scrape-results with Bearer token.
- Background: Gmail sync worker; no message-bus evidenced in standard pass.
- Frontend environments point at `http://localhost:5173/api` in local configs.

## 17. Identity and Authorization

- Supabase issues tokens; API validates via JWKS; audience default `authenticated`.
- User identity keyed by Supabase user id claims; tenancy enforced in API (integration tests exist).
- OAuth for calendar, mail, GitHub stored server-side.

## 18. Payments and Billing

**Not applicable.** No payment/billing implementation found.

## 19. AI and Automated Decisions

- Gemini `generateContent` for scrape enrichment, CV import/update/suggestions/export, GitHub project summaries.
- Feature/config sections: `GoogleAi`, `ScrapeResultEnrichment`, `CvImportAi`, `CvUpdateAi`, `CvSuggestionsAi`, `CvExportAi`, `GitHubProjectAi`.
- Gmail job-status classification is rules-based (not LLM) per research.

## 20. Client Applications and User Experience

- Angular 19 SPA: Jobs, Search, CV Builder, CV Projects, Settings, OAuth callbacks.
- Chrome MV3 extension popup + content scripts for major ATS/job sites.
- Existing Cursor UI rule for job-results: `.cursor/rules/job-results-ui-ux.mdc`.

## 21. External Integrations

| Integration | Status |
|---|---|
| Supabase Auth | Verified |
| Google AI (Gemini) | Verified |
| Gmail | Verified |
| Google Calendar | Verified |
| Microsoft Calendar | Verified |
| GitHub | Verified |
| EURES | Verified |
| Jobnet (Work in Denmark) | Verified |
| Azure Blob Storage | Verified (optional provider) |

## 22. Hosting, Infrastructure, and Networking

- Local: LocalDB + local CV storage; optional Redis.
- Staging/Production appsettings present (`appsettings.Staging.json`, `appsettings.Production.json`).
- Production-readiness tracker marks prod-01–17 completed (`plans/production-readiness-tracker.md`) — treat as **project-claimed** readiness; this pass did not re-validate live deploy.

## 23. Source Control, CI/CD, and Release Management

- GitHub repo `PrimusInterParess/ApplyVault` (per `docs/agents/issue-tracker.md`).
- Issues/PRDs via `gh`; triage labels in `docs/agents/triage-labels.md`.
- CI: `.github/workflows/api-ci.yml` — API unit + integration; frontend `test:ci`. Extension tests not in CI.

## 24. Environment, Secrets, and Configuration

Key **names only** (no values inspected as secrets): `ConnectionStrings:ApplyVault`, `ConnectionStrings:Redis`, `Supabase:Url`, `Supabase:Audience`, `GoogleAi:*`, OAuth client fields under Calendar/Mail/GitHub integrations, `CvDocumentStorage:*`, EURES/Jobnet integration sections, rate limiting, CORS. Dev uses user secrets id `applyvault-api-dev`. Example/template configs exist (`appsettings.Development.example.json`, `appsettings.example.json` patterns per research).

## 25. Security, Privacy, and Compliance

- JWT auth required for protected APIs; health endpoints excluded from rate limit/logging noise.
- Request logging middleware logs 4xx/5xx with trace id; avoids Authorization headers/bodies.
- Untrusted HTML sanitization in UI (README).
- Formal compliance framework (GDPR DPIA, etc.) not evidenced → `OPEN_QUESTION` / low for fleet generation.

## 26. Testing and Quality Engineering

| Layer | Evidence |
|---|---|
| API unit | `api/ApplyVault.Api.Tests` (~44 test files) |
| API integration | `api/ApplyVault.Api.IntegrationTests` (tenancy, CV upload/export) |
| Frontend | ~14 `*.spec.ts` (Karma), run in CI via `test:ci` |
| Extension | ~4 Vitest specs; **not** in CI workflow |

Reading tests ≠ proving they currently pass (not executed in this research).

## 27. Observability and Operations

- `/health`, `/health/live`, `GET api/health`
- `ILogger<T>` + request error logging
- Production logging plans referenced as completed in tracker

## 28. Environments and Deployment Strategy

- Development, Staging, Production config files present.
- Exact hosting topology: `ASSUMPTION` / `OPEN_QUESTION` for live environment; agents must not invent deploy targets.

## 29. Confirmed Decisions

| Decision | Source |
|---|---|
| Project mode BROWNFIELD | `/brownfield` |
| Research depth Standard | User **A** |
| Fleet focus full product evolution | User **A** |
| Prefer existing Matt Pocock delivery skills over replacing them | `VERIFIED_EXISTING` procedures + Architect adapt rules |
| Preserve architecture; incremental evolution | Proposed from brownfield evidence; pending approval |
| CV section schema catalog | [ADR-0001](../docs/adr/0001-cv-section-schema-catalog.md) |
| CV builder sole surface | [ADR-0002](../docs/adr/0002-cv-builder-sole-surface.md) |
| Edit canvas vs export preview | [ADR-0003](../docs/adr/0003-cv-builder-edit-canvas-vs-export-preview.md) |
| Summary regenerate propose-then-approve | [ADR-0004](../docs/adr/0004-cv-summary-regenerate-propose-approve.md) |
| PDF import AI-first Structure | [ADR-0005](../docs/adr/0005-cv-pdf-import-ai-first-structure.md) |
| HTML → Puppeteer export | [ADR-0006](../docs/adr/0006-cv-export-html-puppeteer-pipeline.md) |
| Ephemeral CV evaluation | [ADR-0007](../docs/adr/0007-cv-quality-evaluation-ephemeral.md) |
| Gemini HTTP clients only (no SDK) | [ADR-0008](../docs/adr/0008-google-gemini-http-clients-no-sdk.md) |
| Supabase Auth + local AppUser | [ADR-0009](../docs/adr/0009-supabase-auth-local-app-user.md) |
| Strict per-user tenancy | [ADR-0010](../docs/adr/0010-strict-per-user-tenancy.md) |

## 30. Proposed Decisions Requiring Approval

1. Adaptation mode **`BRIDGE`**: Architect orchestrates; delivery skills (`to-spec` → `to-tickets` → `implement` → `/tdd` → `/code-review`) remain the implementation procedure of record.
2. Recommended agent fleet listed in §36 (approve by approving this spec).
3. Do not generate a payment-billing agent.
4. Keep Supabase as identity provider unless a future Hybrid discovery says otherwise.

## 31. Assumptions

| ID | Assumption |
|---|---|
| A-01 | Primary end user is an individual job seeker (not multi-org SaaS). |
| A-02 | GitHub Issues remain the system of record for specs/tickets. |
| A-03 | Production-readiness tracker “completed” reflects intended posture; live deploy not re-validated here. |
| A-04 | Full-product fleet should include a dedicated extension specialist (not only “frontend”). |

## 32. Open Questions

| ID | Question | Blocks prompt pack? |
|---|---|---|
| OQ-01 | Exact production host / region / multi-replica requirement | No |
| OQ-02 | Org-level admin roles needed in future? | No |
| OQ-03 | Should extension tests be added to CI soon? | No |

## 33. Risks and Technical Debt

| Risk | Severity | Notes |
|---|---|---|
| Dual agent systems (Matt Pocock skills + Architect) can conflict if precedence ignored | HIGH | Mitigate via BRIDGE + shared-context binding |
| `AGENTS.md` still Matt-Pocock-only; Architect `CLAUDE.md` points at Architect `AGENTS.md` semantics | MEDIUM | Copy-install skipped overwriting root `AGENTS.md` |
| Extension tests outside CI | MEDIUM | Quality gap for scrape pipeline |
| AI feature toggles / API keys misconfiguration | MEDIUM | Ops/config ownership |
| CV catalog drift if agents hardcode section types | HIGH for CV work | Must follow ADR-0001 + catalog |

## 34. Contradictions Detected

| Conflict | Resolution proposal |
|---|---|
| Root `AGENTS.md` (ApplyVault skills guide) vs Architect expectation that `AGENTS.md` is Architect entry | **BRIDGE**: keep ApplyVault `AGENTS.md`; agents load Architect via `core/` + `/` commands; optionally add a short Architect pointer after approval |
| `CLAUDE.md` says treat `AGENTS.md` + `core/` as Architect source of truth | Prefer `core/` for Architect workflows; ApplyVault `AGENTS.md` for skills/issue-tracker binding |

## 35. Existing Agent / Skills / Documentation Procedures

### Detection status

`FOUND`

### Adaptation mode

`BRIDGE`

### Procedure inventory

| Path | Type | Purpose | Confidence |
|---|---|---|---|
| `AGENTS.md` | Host instructions | Issue tracker, triage, domain doc pointers | `VERIFIED_PROJECT_FACT` |
| `CLAUDE.md` | Host instructions | Architect entry for Claude | `VERIFIED_PROJECT_FACT` |
| `.github/copilot-instructions.md` | Host instructions | Architect-oriented Copilot guidance | `VERIFIED_PROJECT_FACT` |
| `.agents/skills/*` (41 skills) | Skills pack | Delivery: `to-spec`, `to-tickets`, `implement`, `tdd`, `code-review`, `domain-modeling`, `triage`, `qa`, … | `VERIFIED_PROJECT_FACT` |
| `docs/agents/issue-tracker.md` | Agent config | GitHub Issues + `gh` | `VERIFIED_PROJECT_FACT` |
| `docs/agents/triage-labels.md` | Agent config | Triage vocabulary | `VERIFIED_PROJECT_FACT` |
| `docs/agents/domain.md` | Agent config | How to consume CONTEXT/ADRs | `VERIFIED_PROJECT_FACT` |
| `CONTEXT.md` | Domain memory | CV structuring glossary | `VERIFIED_PROJECT_FACT` |
| `docs/adr/0001-cv-section-schema-catalog.md` | Domain memory | Catalog decision | `VERIFIED_PROJECT_FACT` |
| `core/`, `.cursor/commands/`, `.cursor/skills/` | Architect library | Discovery / operate orchestration | `VERIFIED_PROJECT_FACT` |
| `.cursor/rules/job-results-ui-ux.mdc` | IDE rule | Jobs UI UX constraints | `VERIFIED_PROJECT_FACT` |
| `.architect/library-version` | Install stamp | Copy-install Architect version | `VERIFIED_PROJECT_FACT` |

### Canonical workflow chain (project)

1. Clarify / grill as needed (`grilling`, `grill-with-docs`, domain-modeling)
2. `/to-spec` → GitHub issue (`ready-for-agent`)
3. `/to-tickets` → tracer-bullet tickets
4. `/implement` with `/tdd` where applicable
5. `/code-review`
6. Triage/QA skills as needed

Architect `/operate` **composes** with this chain: plan/map/implement milestones should instruct specialists to invoke these skills rather than invent a parallel ticket system.

### Domain memory locations

- `CONTEXT.md`
- `docs/adr/`
- `docs/agents/domain.md`

### Work-tracking binding

- GitHub Issues on `PrimusInterParess/ApplyVault` via `gh` (`docs/agents/issue-tracker.md`)

### Conflicts with Architect defaults

| Conflict | Proposed resolution |
|---|---|
| Architect default “ownership matrix files under agent-system/governance” vs project GitHub Issues | Use Issues as work tracker; governance files may mirror ownership but must not replace Issues |
| Architect generic implement loop vs `/implement`+`/tdd`+`/code-review` | **FOLLOW** project skills for implementation |
| Root `AGENTS.md` content | Do not overwrite; BRIDGE documentation in shared context |

### Adoption decisions (after approval)

- Fleet prompts **must** cite `.agents/skills` delivery chain and `docs/agents/*`.
- CV work **must** use `CONTEXT.md` + ADR-0001 + catalog path.
- Architect owns orchestration (`/operate` delegation/handoffs); project skills own how code is specified and shipped.

## 36. Recommended Agent Fleet

| Agent ID | Why | Ownership | Non-responsibilities | Key I/O | Collaborators | Approval |
|---|---|---|---|---|---|---|
| `principal-software-architect` | Orchestrate BRIDGE fleet; preserve seams | Cross-cutting design, `/operate` plans, ADR awareness | Line-by-line feature coding by default | Spec, plans, handoffs | all | Pending |
| `product-manager` | Scope journeys; align with Issues | Problem/solution framing; prioritize | Deep infra coding | Specs via `/to-spec` norms | architect, engineers | Pending |
| `frontend-engineer` | Angular 19 dashboard | `frontend/applyvault-jobs-ui/**` | Extension MV3 internals; API persistence | UI PRs, specs | ui-ux, backend, qa | Pending |
| `backend-engineer` | ASP.NET API + EF + integrations | `api/ApplyVault.Api/**` (excl. pure AI prompt tuning may share) | Chrome extension UI | API/contracts/tests | ai-llm, platform, qa | Pending |
| `browser-extension-engineer` | MV3 scrape pipeline | `extension/**` | Angular feature modules | Extension PRs/tests | backend, qa | Pending |
| `ai-llm-engineer` | Gemini clients, prompts, CV/scrape AI toggles | `Services/GoogleAi*`, CV/GitHub AI clients, prompt options | Auth/token storage design | Prompt/config changes + tests | backend, frontend | Pending |
| `ui-ux-designer` | Dashboard UX consistency | UX specs; respect `.cursor/rules/job-results-ui-ux.mdc` | Backend schema | UX notes | frontend, product | Pending |
| `qa-engineer` | Test strategy across layers | Test plans; gap analysis (esp. extension CI) | Production secret management | Test PRs / matrices | all engineers | Pending |
| `platform-engineer` | CI, config, Redis, storage providers, health | `.github/workflows`, hosting/config seams | Product UX copy | CI/config changes | backend, security-minded reviews | Pending |

## 37. Excluded Agents

| Agent | Reason |
|---|---|
| `payment-billing-engineer` | Payments capability `NOT_APPLICABLE` |
| `database-engineer` (standalone) | EF/SQL ownership folded into `backend-engineer` for fleet size |
| `identity-access-engineer` (standalone) | Supabase JWT ownership folded into `backend-engineer` (+ architect oversight) |
| `cicd-engineer` + `cloud-platform-engineer` (separate) | Combined as `platform-engineer` |
| `technical-writer` | Docs exist; add later if doc debt becomes primary |
| `security-engineer` (standalone) | Security reviews via `/code-review` + platform; promote later if threat work dominates |

## 38. Preliminary Ownership Matrix

| Area | Primary | Secondary |
|---|---|---|
| `extension/` | browser-extension-engineer | backend-engineer, qa-engineer |
| `frontend/applyvault-jobs-ui/` | frontend-engineer | ui-ux-designer, qa-engineer |
| `api/ApplyVault.Api/` | backend-engineer | ai-llm-engineer, platform-engineer, qa-engineer |
| `shared/cv-section-catalog/` | backend-engineer | frontend-engineer, ai-llm-engineer |
| `.github/workflows/` | platform-engineer | qa-engineer |
| `.agents/skills`, `docs/agents/` | principal-software-architect | product-manager |
| `CONTEXT.md`, `docs/adr/` | principal-software-architect | domain-aware engineers via `/domain-modeling` |
| GitHub Issues | product-manager | all (via skills) |

## 39. Proposed Execution Order

1. Approve this specification.
2. Generate prompt pack (`agent-system/` agents + governance) — automatic after approval.
3. `/operate` → Option A (implementation plan) for current priorities.
4. Map plan to repository paths (Option B) before coding.
5. Implement via project skills chain inside specialist Tasks.

## 40. Evidence Index

| Finding | Classification | Evidence | Confidence | Impact |
|---|---|---|---|---|
| Three-part product (extension, API, Angular) | `VERIFIED_PROJECT_FACT` | `README.md` | High | Topology |
| net10.0 API + Supabase JWT | `VERIFIED_PROJECT_FACT` | `ApplyVault.Api.csproj`, auth infrastructure | High | Identity |
| Angular 19 + Supabase client | `VERIFIED_PROJECT_FACT` | `frontend/.../package.json`, `auth.service.ts` | High | FE |
| CV catalog ADR | `VERIFIED_PROJECT_FACT` | `docs/adr/0001-*.md`, `shared/cv-section-catalog/` | High | CV domain |
| 41 skills in `.agents/skills` | `VERIFIED_PROJECT_FACT` | glob `SKILL.md` | High | Procedures |
| Architect copy-installed; no prior `agent-system/` fleet | `VERIFIED_PROJECT_FACT` | `core/`, `.architect/`, empty fleet before this file | High | Install style |
| CI API + frontend only | `VERIFIED_PROJECT_FACT` | `api-ci.yml` | High | Quality gap for extension |
| No payments | `VERIFIED_PROJECT_FACT` | grep/package inspection | High | Exclude payment agent |
| Prod readiness 01–17 marked completed | `VERIFIED_PROJECT_FACT` (tracker claim) | `plans/production-readiness-tracker.md` | Medium | Ops assumptions |
| Fleet focus full product | `USER_CONFIRMED_REQUIREMENT` | User answer A | High | Fleet shape |
| Research depth standard | `USER_CONFIRMED_REQUIREMENT` | User answer A | High | Coverage |

### Agent-system context artifact mapping

| Artifact kind | Path |
|---|---|
| Project specification | `agent-system/project-specification.md` (this file) |
| Generated agents (pending approval) | `agent-system/agents/` (not yet created) |
| Governance (pending) | `agent-system/governance/` (not yet created) |
| Prior Architect pack | None found before this discovery |

## 41. Repository Access and Validation Limitations

- Direct read-only workspace access to `c:\Users\yborisov\Desktop\jobapplications`.
- No builds, tests, or deploys were executed in this research.
- Secret values were not read or displayed.
- Staging/production runtime behavior not live-verified.
- Standard depth: not every service file was line-audited.

## 42. Readiness Recommendation

`READY_WITH_DOCUMENTED_ASSUMPTIONS`

Blocking information: **none** for prompt-pack generation. Assumptions A-01–A-04 and open questions OQ-01–OQ-03 are documented and non-blocking.

---

```text
APPROVAL REQUIRED
```

Reply with exactly one of:

- `APPROVED`
- `APPROVED WITH CHANGES: ...`
- `REVISE: ...`

After approval, the selected agent fleet will be generated automatically under `agent-system/` (SAVE mode). Application implementation will **not** start until you run `/operate` (or otherwise authorize it).
