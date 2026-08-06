# Repository task mapping — Interview Prep brief

**Status:** `APPROVED` (human 2026-08-05 — “approve go”)  
**Operate:** B — 2026-08-05; C in progress  
**Plan:** `agent-system/implementation-plan-interview-prep-brief.md`  
**ADR:** `docs/adr/0025-interview-prep-brief.md`  
**Domain:** `CONTEXT.md` — Interview Prep brief / Brief topic gap  
**Task id:** `interview-prep-brief-2026-08-05`  
**Mode:** BROWNFIELD / BRIDGE  

Does **not** modify application files until implement is authorized.

---

## Verified anchors

| Area | Path |
|---|---|
| API controller | `api/ApplyVault.Api/Controllers/InterviewPrepController.cs` (`/api/interview-prep`) |
| Contracts | `api/ApplyVault.Api/Models/InterviewPrep/InterviewPrepContracts.cs` |
| Session entity (pattern) | `api/ApplyVault.Api/Data/InterviewPrepSessionEntity.cs` |
| Session service | `api/ApplyVault.Api/Services/InterviewPrep/InterviewPrepSessionService.cs` |
| AI gateway | `api/ApplyVault.Api/Services/InterviewPrep/Ai/InterviewPrepAiGateway.cs` |
| Fake AI | `api/ApplyVault.Api/Services/InterviewPrep/Ai/FakeDeterministicInterviewPrepAiProvider.cs` |
| Language/Market enums | `api/ApplyVault.Api/Services/InterviewPrep/Domain/InterviewPrepEnums.cs` |
| DI | `api/ApplyVault.Api/Infrastructure/ServiceCollectionExtensions.cs` |
| FE feature | `frontend/applyvault-jobs-ui/src/app/features/interview-prep/` |
| FE route | `frontend/applyvault-jobs-ui/src/app/app.routes.ts` (`interview-prep`) |
| Contract registry | `agent-system/governance/contract-registry.yaml` (`interview-prep-v2-sessions`) |
| Domain memory | `CONTEXT.md`, `docs/adr/` |

---

## Task map

| Task | Milestone | Owner agent | Affected paths | Contracts | Depends on | Validation gate |
|---|---|---|---|---|---|---|
| T0 ADR + plan + mapping | M0 | principal-software-architect | `docs/adr/0025-*.md`, `agent-system/implementation-plan-interview-prep-brief.md`, this file | — | Grill/`CONTEXT.md` | Human approve plan/mapping |
| T1 Freeze brief DTOs + OpenAPI-style tables | M1 | architecture-engineer | handoff under `handoffs/active/interview-prep-brief-2026-08-05/`; propose registry `interview-prep-briefs` | new `interview-prep-briefs` | T0 approved | Principal accepts frozen contracts |
| T2 EF entity + migration + unique cardinality | M1 | backend-engineer | `api/ApplyVault.Api/Data/*`, DbContext, migrations | persistence | T1 | Unique CV-only + per-job; tenancy |
| T3 Brief service + controller routes | M1 | backend-engineer | `Services/InterviewPrep/` (new Brief*), `InterviewPrepController` or peer, `Models/InterviewPrep/` | REST | T2 | CRUD + regenerate replace; no session coupling |
| T4 Outdated computation | M1 | backend-engineer | Brief service + CV fingerprint source | DTO `outdated` | T3 | CV change + missing job cases |
| T5 AI operation + schema + prompts | M2 | ai-llm-engineer | `Ai/*`, options/appsettings.example, fake provider | AI JSON schema | T1, T3 | Schema validate; profession-agnostic |
| T6 Wire generate to gateway | M2 | backend-engineer | Brief service DI | — | T5 | Generate/regenerate persist validated body |
| T7 FE models + API + facade | M3 | frontend-engineer | `features/interview-prep/models`, `data-access/*` | client ↔ T1 | T1 | Typed client |
| T8 Study UI sibling + outdated/regenerate | M3 | frontend-engineer (+ ui-ux-designer if layout contested) | `pages/interview-prep-page/*` | — | T7, T6 | Structured render; focus note; no edit body |
| T9 Job deep-link / action | M3 | frontend-engineer | jobs UI entry + `app.routes` / query params | `jobId` → scrape | T8 | Opens brief generate/view for that job |
| T10 Delete/list polish + AI-disabled UX | M4 | frontend + backend | same feature folders | errors | T8–T9 | READY |
| T11 Integration review + Close | M4 | principal + code-review / qa as authorized | archive summary | registry APPROVED | T10 | Close wipe scratch |

---

## Execution order

```text
T0 → (approve) → T1 → T2 → T3 → T4
                ↘ T5 → T6
T4+T6 → T7 → T8 → T9 → T10 → T11 Close
```

Parallel after T1: backend M1 (T2–T4) can start while AI (T5) designs schema against frozen DTOs; T6 waits on T5+T3.

---

## Handoff / scratch (when C/D starts)

| Kind | Path |
|---|---|
| Active handoffs | `agent-system/handoffs/active/interview-prep-brief-2026-08-05/` |
| Scratch | `agent-system/scratch/interview-prep-brief-2026-08-05/` |
| Archive on Close | `agent-system/handoffs/archive/interview-prep-brief-2026-08-05/summary.yaml` |

---

## BRIDGE delivery note

After plan+mapping approval: file GitHub Issue via project `/to-spec` (do not invent Issue content until authorized), then `/to-tickets` → implement. Do not overwrite `.agents/skills`, `CONTEXT.md`, or ADRs except additive ADR already written.

---

## Approval

**Approved** 2026-08-05. Proceed M1: T1 contracts → T2–T4 backend.
