# ADR-0012: Interview Prep coach turns are ephemeral and profession-agnostic

## Status

Accepted (2026-08-02 — operate `interview-prep-ip-01-2026-08-02` / GitHub [#67](https://github.com/PrimusInterParess/ApplyVault/issues/67) IP-01)

**Supersession:** Decision point **1** (ephemeral turns only) is superseded by [ADR-0016](0016-interview-prep-durable-session-history.md) for IP-13 durable session history (Accepted 2026-08-03). Decision points **2–8** remain in force. Ephemeral `POST /turns` without `sessionId` remains for API compatibility.

## Context

ApplyVault will ship an **Interview Prep** coach so authenticated seekers can practice interviews grounded in their **Structured CV**, with an optional owned scrape (saved job) for targeting.

Sibling AI features (ADR-0007 evaluation, ADR-0004 / ADR-0011 propose paths) already use ephemeral Gemini HTTP turns with no durable session store. Interview Prep needs the same class of decision, plus an explicit product constraint: the coach is **profession-agnostic** — it must infer role, seniority, and interview style from CV ± job and must **never** default to software / coding interviews.

ADR-0001 (section catalog), ADR-0002 (CV builder sole Structured CV surface), ADR-0008 (Gemini raw HTTP only), ADR-0009 / ADR-0010 (auth + tenancy) remain in force.

Approved plan: `agent-system/implementation-plan-interview-prep.md`.  
Prior design: `agent-system/handoffs/archive/interview-prep-2026-08-02/architecture-design.md`.

## Decision

1. **Ephemeral turns only (MVP).** There is no `InterviewPrepSessions` table and no session create/end CRUD. The sole coach endpoint is `POST /api/interview-prep/turns`. The Angular client holds chat history and sends compact `priorTurns` on each request. Refresh loses history (same trade-off as ADR-0007). Durable history is Later (IP-13) and requires a future ADR.
2. **Profession-agnostic grounding.** System prompts and schemas must adapt to any profession inferred from Structured CV ± optional job context. Do not hardcode developer / full-stack / coding / “system design for engineers” defaults. Process & systems may specialize to technical system design **only when** inference marks `isTechnicalContext: true`.
3. **Dedicated API surface.** Coach lives on `InterviewPrepController` at `/api/interview-prep/*` — **not** under `cv-documents`. Reuse `ICvStructuredDocumentService` and `IScrapeResultStore` for reads; never call `SaveStructuredAsync` or mutate scrape entities from prep.
4. **Required / optional inputs.** Authenticated AppUser + Structured CV presence required each turn. Optional `scrapeResultId` loads an owned scrape by `(id, user.Id)` (404 on miss / foreign). Deep-link query param on `/interview-prep` is `jobId` (= scrape result GUID) mapped to API `scrapeResultId`.
5. **Frozen mode catalog** (role-agnostic ids): `screening`, `behavioral`, `role_domain`, `problem_solving`, `process_systems`, `language_practice`, `full_loop`. Display labels map from the approved plan; `full_loop` is in contract even if IP-12 ships later.
6. **Frozen `languageMix`:** `en` | `da` | `mixed` (Danish+English practice). Not `en+da`.
7. **Scorecard:** fixed profession-agnostic dimension ids (`clarity`, `evidence`, `structure`, `role_fit`, `language`) plus model-written notes and optional narrative; overall score 0–100. Scorecard may be null on setup / pure interview turns.
8. **AI stack:** new `GoogleAiInterviewPrepClient` + `InterviewPrepAiOptions` via raw Gemini HTTP only (ADR-0008); gate on `GoogleAi:Enabled` like other clients.

Frozen request/response/error tables: `agent-system/handoffs/archive/interview-prep-ip-01-2026-08-02/frozen-contracts.md`.

## Consequences

- M1 implementers (`backend-engineer`, `ai-llm-engineer`) implement against ADR-0012 + frozen contracts; FE binds the same enums after M1.
- Users lose prep chat/scorecards on refresh until IP-13.
- Nesting under `cv-documents` or adding a sessions table without a superseding ADR is out of scope.
- Contract registry entry `interview-prep-turns` is design-APPROVED; code evidence lands with M1.
- Plan: `agent-system/implementation-plan-interview-prep.md`. Issue: [#67](https://github.com/PrimusInterParess/ApplyVault/issues/67).
