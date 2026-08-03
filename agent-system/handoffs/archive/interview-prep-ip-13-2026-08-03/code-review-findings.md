## Review summary

- Range: uncommitted IP-13 BE+FE scope (handoff file lists; not a git `base...HEAD`)
- Ticket: [#79](https://github.com/PrimusInterParess/ApplyVault/issues/79) / IP-13 `interview-prep-ip-13-2026-08-03`
- Recommendation: **APPROVE WITH NITS**

Reviewed against ADR-0016, frozen contracts (`frozen-contracts-session-history.md`), and ADR-0010 tenancy. Focus axes: tenancy, contracts, session-wins, 409 completed, hard delete, FE always-durable. No builds/tests run. Missing unit/Karma coverage treated as **documented limitation**, not a blocker (operate policy).

## Findings

### Blockers

_None._

### Should-fix

- **CR-IP-13-01** — `api/ApplyVault.Api/Services/InterviewPrep/InterviewPrepService.cs` (~L310–313)  
  **Confidence:** high  
  Durable turns always assign `session.LatestOverallScore = response.Scorecard?.Overall` and `LatestScorecardJson = coachEntity.ScorecardJson`. A later interview turn with a null scorecard **clears** list denorms, so history can lose a previously shown overall score even though message rows still hold earlier scorecards. Prefer “update only when scorecard non-null” (FE already does this for live UI).

- **CR-IP-13-02** — `api/ApplyVault.Api/Services/InterviewPrep/InterviewPrepService.cs` (`CreateDurableTurnAsync` sequence + unique index)  
  **Confidence:** medium  
  Two concurrent durable turns on the same session can compute the same `nextSequence` and hit `IX_InterviewPrepSessionMessages_SessionId_Sequence`. That surfaces as an unmapped `DbUpdateException` → HTTP 500 rather than a clean 409/400. Low likelihood for single-tab MVP; worth a concurrency guard or mapped conflict if multi-tab resume is expected.

### Nits

- **CR-IP-13-03** — `api/ApplyVault.Api/Options/InterviewPrepAiOptions.cs` (`DefaultSystemPrompt`)  
  **Confidence:** high  
  Prompt still says “Never … claim durable session storage. This turn is ephemeral.” Storage is server-side regardless, but the instruction is stale vs ADR-0016.

- **CR-IP-13-04** — `frontend/.../interview-prep.facade.ts` (`sendTurn`)  
  **Confidence:** high  
  Durable requests still send client `mode` / `languageMix` / `hiringMarket` / `scrapeResultId` / `priorTurns`. Harmless under session-wins; optional cleanup to stop implying client authority.

- **CR-IP-13-05** — `agent-system/handoffs/active/.../frozen-contracts-session-history.md` header  
  **Confidence:** high  
  Document status still “PROPOSED”; ADR-0016 is Accepted. Cosmetic contract-doc drift for Principal/registry Close.

### Questions

- None that block Close. Hand-written EF Designer/snapshot was not runtime-verified (`dotnet ef` / migrate) — already called out in backend residual risks; Principal/QA own migrate smoke.

### Documented limitations (non-blocking this pass)

- No new BE unit tests for session CRUD / durable turns / 409 / hard delete / tenancy filters.
- FE `*.spec.ts` not updated for sessionId / history (excluded from scope; may fail until QA).
- Create-then-bootstrap-turn failure leaves an empty `in_progress` session in history (resumable; FE residual risk).

### Praise

- Tenancy: `[Authorize]` + `GetRequiredUserAsync`; all session queries filter `UserId == user.Id`; foreign/missing → 404; create scrape ownership via `GetByIdAsync(id, user.Id)`.
- Session-wins + DB `priorTurns` (role/text/phase only — no `modelAnswer` in AI context); completed → `InterviewPrepSessionConflictException` → 409; debrief completes session.
- Hard delete: `Remove(session)` + FK cascade on messages; FE confirms then `DELETE` → 204 path.
- FE product path always-durable: `createSession` → turns with `sessionId`; history list/open/resume/read-only/delete match frozen §10; ephemeral turns unused by UI.
- Schema matches frozen model (indexes, scrape SET NULL, user cascade, `MaxMessagesPerSession` default 200).

## Focus-axis checklist

| Axis | Verdict |
|---|---|
| Tenancy (ADR-0010) | Pass |
| Contracts (endpoints/DTOs/errors) | Pass (additive; ephemeral kept) |
| Session-wins | Pass |
| 409 on completed | Pass |
| Hard delete | Pass |
| FE always-durable | Pass |
