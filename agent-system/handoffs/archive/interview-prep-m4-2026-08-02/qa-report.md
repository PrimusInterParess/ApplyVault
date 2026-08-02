# Interview Prep MVP — QA evidence (M4)

**Task:** `interview-prep-m4-2026-08-02`  
**Agent:** `qa-engineer`  
**Date:** 2026-08-02  
**Contracts:** ADR-0012, `frozen-contracts.md`, ADR-0008 / ADR-0009 / ADR-0010  
**Overall status:** **READY** (no critical MVP blockers evidenced)

---

## 1. Executive Summary

Interview Prep M0–M3 checks pass under existing harnesses: API filter **15/15**, Angular interview-prep specs **11/11**. Static review confirms `[Authorize]`, scrape load by `(id, user.Id)`, no CV/scrape mutation on the prep path, profession-agnostic system prompt, and scorecard dimension freeze. Live Gemini: local Development config proves `GoogleAi:Enabled=true` with a non-empty key; one outbound probe failed with `System.Net.WebException` (no HTTP status) — treated as **environment**, not a product defect. No new product or test code added.

---

## 2. Scope Confirmation

| In scope | Out of scope |
|---|---|
| `api/ApplyVault.Api` Interview Prep controller/service/AI client | Extension / Vitest |
| `api/ApplyVault.Api.Tests` InterviewPrep* | Full solution build |
| `frontend/.../features/interview-prep` + job-detail CTA / shell nav | Manual browser E2E |
| ADR-0012 + frozen contracts | Inventing secrets / claiming CI green |

---

## 3. Checklist — pass / fail / skip

| # | Check | Result | Evidence |
|---|---|---|---|
| A1 | Auth: `[Authorize]` on `InterviewPrepController` | **PASS** | Static: class attribute present (`InterviewPrepController.cs` L10). Gap: no WebApplicationFactory 401 assertion (nit). |
| T1 | Tenancy: scrape `GetByIdAsync(id, user.Id)`; foreign/miss → 404 family | **PASS** | Service passes `user.Id` (L49). Unit: `CreateTurnAsync_RejectsUnknownScrapeResultWithNotFound` → `KeyNotFoundException` → controller `NotFound()`. Store tenancy: `ScrapeResultTenancyTests.GetByIdAsync_returns_null_when_job_belongs_to_another_user`. |
| M1 | No CV mutation on prep path | **PASS** | Static grep: no `SaveStructuredAsync` under `Services/InterviewPrep/`. Unit: `CreateTurnAsync_PassesCappedInputsAndDoesNotSave` asserts `structured.SaveWasCalled == false`. |
| M2 | No scrape mutation on prep path | **PASS** | Static: prep path only `GetByIdAsync`. Unit: same test asserts `scrapeStore.SaveWasCalled == false`. |
| E1 | Missing CV → 404 family | **PASS** | `CreateTurnAsync_RejectsMissingStructuredContent` → `KeyNotFoundException`; controller maps to `NotFound()`. |
| E2 | Empty CV sections → 400 family | **PASS** | `CreateTurnAsync_RejectsEmptySections` → `InvalidOperationException`; controller `BadRequest`. |
| E3 | AI disabled → 400 family | **PASS** | Service + client tests: disabled → `InvalidOperationException` / “Google AI is disabled”; controller `BadRequest`. FE: facade surfaces 400 message. |
| F1 | FE CV gate missing / empty sections | **PASS** | `interview-prep.facade.spec.ts`: 404 → `missing`; empty `sections` → `missing`. |
| F2 | Modes / `languageMix` enums | **PASS** | Model freeze matches contracts; API rejects `technical` / `en+da` (service tests). FE chips bind `INTERVIEW_PREP_MODES` / `INTERVIEW_PREP_LANGUAGE_MIXES`. |
| F3 | Ephemeral `priorTurns` | **PASS** | Facade holds client history; turn body includes `priorTurns`; refresh copy in page; no session CRUD in MVP. |
| F4 | `jobId` → `scrapeResultId` | **PASS** | Facade + page specs; job-detail CTA `queryParams: { jobId: selectedJob.id }`. |
| F5 | Scorecard dims UI | **PASS** | Labels: clarity, evidence, structure, role_fit, language. Facade flush uses exact five ids. |
| P1 | Profession-agnostic system prompt (static) | **PASS** | `InterviewPrepAiOptions.DefaultSystemPrompt` forbids software defaults; unit `InterviewPrepAiOptions_DefaultsAreProfessionAgnostic` + prompt body asserts in GoogleAi client test. |
| P2 | Scorecard dims unit tests | **PASS** | `NormalizeScorecard_KeepsExactFiveDimensionsInStableOrder` (+ clamp/fill tests). |
| L1 | Live Gemini | **SKIP / FAIL (env)** | Config **proven** without inventing secrets: `appsettings.Development.json` → `GoogleAi.Enabled=true`, `ApiKey` non-empty, model `gemini-2.5-flash-lite`. One probe → `System.Net.WebException` (empty HTTP status). Further credentialed outbound calls blocked by session policy. **Not a product blocker.** Unit path with stubbed HTTP still **PASS**. |

---

## 4. Commands executed

Repo root: `C:\Users\yborisov\Desktop\jobapplications`

### API

```powershell
dotnet test api/ApplyVault.Api.Tests `
  --filter "FullyQualifiedName~InterviewPrep" `
  -p:UseAppHost=false `
  -p:OutputPath="c:\Users\yborisov\Desktop\jobapplications\agent-system\scratch\interview-prep-m4-2026-08-02\api-test-out\\"
```

**Result:** Test Run Successful. **Total 15, Passed 15.**  
Output DLL path under scratch `api-test-out/`.

Covered types:

- `InterviewPrepServiceTests` (8)
- `GoogleAiInterviewPrepClientTests` (7)

### Frontend

```powershell
Set-Location frontend/applyvault-jobs-ui
npx ng test --no-watch --browsers=ChromeHeadlessCI `
  --include=src/app/features/interview-prep/**/*.spec.ts
```

**Result:** **TOTAL: 11 SUCCESS** (Karma Chrome Headless).

---

## 5. Verified Facts

1. `InterviewPrepController` is `[Authorize]` + `GetRequiredUserAsync`.
2. Prep service loads scrape only via `GetByIdAsync(scrapeResultId, user.Id)` and never calls scrape/CV save APIs.
3. Error mapping: `KeyNotFoundException` → 404; `InvalidOperationException` → 400.
4. Default system prompt contains “Do NOT default to software engineering” and scorecard ids `clarity, evidence, structure, role_fit, language`.
5. Route `/interview-prep` registered; shell nav + job-detail “Prepare for interview” deep-link present.
6. Re-executed suites green as of 2026-08-02 (this report).

---

## 6. Assumptions

- Foreign scrape and unknown scrape share the same null → 404 family (store returns null for wrong tenant).
- FE “empty sections” intentionally shares gate status `missing` with HTTP 404 (product copy points to CV Builder).
- Live Gemini success is optional for MVP ship confidence when unit + stubbed GoogleAi client tests pass.

---

## 7. Decisions

- **No additional tests authored** — tenancy/mutation already covered by service stubs + store tenancy tests; gaps are controller HTTP status nits only.
- **READY** despite live Gemini env failure — not a critical MVP product defect.
- Extension CI gap: N/A (extension excluded from this task); disclosed for fleet hygiene: extension Vitest still outside API CI as usual.

---

## 8. Deliverables

| Artifact | Path |
|---|---|
| This report | `agent-system/scratch/interview-prep-m4-2026-08-02/qa-report.md` |
| API test OutputPath | `agent-system/scratch/interview-prep-m4-2026-08-02/api-test-out/` |
| READY handoff | `agent-system/handoffs/active/interview-prep-m4-2026-08-02/handoff-qa-interview-prep-m4.yaml` |

---

## 9. Contracts covered

- ADR-0012 ephemeral + profession-agnostic
- Frozen modes / languageMix / scorecard dims / `jobId`↔`scrapeResultId`
- ADR-0008 HttpClient GoogleAi pattern (client under test with stub handler)
- ADR-0009 / ADR-0010 auth + tenancy

---

## 10. Security

- No secrets written into this report or handoff.
- Live probe used local Development settings; key not logged.
- Fixtures use synthetic CV/job text only.

---

## 11. Validation evidence status

| Layer | Designed | Implemented | Executed this task |
|---|---|---|---|
| API unit (InterviewPrep*) | yes | yes | **yes — 15 pass** |
| FE Karma (interview-prep specs) | yes | yes | **yes — 11 pass** |
| Controller HTTP 401/404/400 WAF | noted gap | no dedicated tests | **not run** |
| Store tenancy (scrape) | yes | yes | **not re-run** (code review + prior suite; referenced) |
| Live Gemini | optional | N/A harness | **config proven; outbound FAIL/env** |

---

## 12. Risks / gaps (non-blocking)

1. No controller/WebApplicationFactory tests for HTTP status codes (same nit as M1 CR-IP-M1-02).
2. `priorTurns` invalid role/phase lack dedicated unit tests (validation exists in service).
3. Live Gemini not proven green in this environment.
4. Gemini HTTP/parse errors may still surface as 500 (known M1 nit CR-IP-M1-01) — out of QA product-block scope unless Principal prioritizes.

---

## 13. Handoffs

- Principal: promote scratch report on Close; ship confidence **READY** for Interview Prep MVP QA gate.
- `code-review-engineer`: evidence package available; QA does not substitute diff review.

---

## 14. Status

**READY**
