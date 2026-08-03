# ADR-0017: Interview Prep already-asked digest (anti-repeat across long sessions)

## Status

Accepted (2026-08-03 — operate `interview-prep-anti-repeat-2026-08-03`; Principal-authorized option D design with recommended defaults)

## Context

Durable Interview Prep (ADR-0016) persists the **full** session transcript (abuse-capped by `MaxMessagesPerSession`, default **200**) while Gemini context continues to use only the **tail** of prior turns (`MaxPriorTurns`, default **12**; per-turn char cap `MaxPriorTurnChars`). Evidence:

- `InterviewPrepService.NormalizePriorTurns` truncates oldest turns via `Skip(count - maxPriorTurns)`.
- Durable turns build AI `priorTurns` from all stored messages, then apply that truncate.
- `InterviewPrepAiOptions.DefaultUserPromptTemplate` injects `{{priorTurnsJson}}` only — no session-wide question memory.
- `DefaultSystemPrompt` does not forbid re-asking substantially the same coach question.
- `modelAnswer` stays out of AI `priorTurns` (ADR-0015).

Product symptom: long sessions (~beyond ~6 coach/user pairs in the AI window) re-ask questions that still exist in the durable transcript but have fallen out of the Gemini window.

ADR-0012 §2–8 (profession-agnostic coach, dedicated `/api/interview-prep`, no hardcoded profession question banks), ADR-0008 (Gemini HTTP), and ADR-0010 (per-user tenancy) remain in force. Ephemeral `POST /turns` without `sessionId` remains for API compatibility (ADR-0016).

## Decision

1. **Server-built already-asked digest (no new tables).** On each coach turn, before (or independently of) `MaxPriorTurns` truncation, the server derives a compact **already-asked questions** list from the **full** prior transcript available to that request path:
   - **Durable** (`sessionId` set): from stored session messages (same source ADR-0016 uses for AI priorTurns).
   - **Ephemeral** (`sessionId` omitted): from the client-supplied `priorTurns` array **before** tail truncation (best-effort; product path is durable).

2. **Digest contents (deterministic, profession-agnostic).** Include `role=coach` turns with `phase=interview` (default interview when phase omitted). Prefer texts that are **outside** the retained `MaxPriorTurns` tail — those are exactly what the model can no longer see. Exclude user turns, debrief coach turns, and **never** include `modelAnswer` (ADR-0015). No hardcoded question bank; no profession-specific catalogs.

3. **Inject into the Gemini user prompt; keep truncated priorTurns.** Add a template placeholder (e.g. `{{alreadyAskedJson}}`) to `InterviewPrepAi:UserPromptTemplate` alongside existing `{{priorTurnsJson}}`. Serialize digest as a small JSON string array (or equivalent compact list). Empty array when nothing fell out of the window or no coach interview turns exist.

4. **Strengthen system prompt (anti-repeat rule).** Instruct the coach: do **not** re-ask a question that is substantially the same as any item in the already-asked list or recent priorTurns; instead deepen, reframe from a new angle, advance to a new topic within mode, or move toward debrief when appropriate. Remain profession-agnostic (ADR-0012).

5. **Caps via options (defaults).** Add `InterviewPrepAi` knobs (names illustrative; implementers may match project naming):
   - `MaxAlreadyAskedItems` — default **40**
   - `MaxAlreadyAskedItemChars` — default **240** (truncate each digest line)
   - `MaxAlreadyAskedTotalChars` — default **4_000** (hard stop on serialized digest size)
   - Keep `MaxPriorTurns=12` and `MaxMessagesPerSession=200` unchanged unless a later ADR revisits budgets.

6. **No public API / DTO change.** Digest is server-only inside the AI client request pipeline (`InterviewPrepAiTurnRequest` / prompt build). Public `InterviewPrepTurnRequest` / response shapes, session CRUD, and FE contracts stay as ADR-0015 / ADR-0016. Frontend engineer not required for this change.

7. **Out of scope.** New DB tables or persisted question indexes; embedding / similarity search; second Gemini “summarizer” call; raising `MaxPriorTurns` to match transcript length; FE UI for the digest; changing `modelAnswer` priorTurns policy.

## Consequences

- Long durable sessions retain a compact memory of earlier coach questions without inflating full chat replay into Gemini.
- Slightly larger user-prompt tokens when the digest is non-empty; bounded by the new options.
- Operators who override `InterviewPrepAi:SystemPrompt` or `UserPromptTemplate` must merge the anti-repeat rule and `{{alreadyAskedJson}}` placeholder.
- Ephemeral clients that already send a truncated `priorTurns` still cannot recover dropped questions — acceptable; FE product path is durable (ADR-0016).
- Heuristic “substantially the same” remains model-judged; digest quality depends on including enough coach question text (item/char caps).

## Rejected alternatives

| Option | Why rejected |
| --- | --- |
| Raise `MaxPriorTurns` to ~200 | Token cost / context pressure with CV + job JSON; does not scale |
| Extra Gemini summarize-history call | Latency + cost; second failure mode; larger than needed |
| Persist `AlreadyAsked` table / question bank | New store; risk of hardcoded banks; unnecessary vs transcript scan |
| Client-supplied `alreadyAsked` field | Public contract change; durable path already session-wins and ignores client priorTurns |
| Embedding de-dupe | New infra; overkill for MVP anti-repeat |

## Links

- Related: ADR-0012, ADR-0015, ADR-0016, ADR-0008, ADR-0010
- Evidence: `api/ApplyVault.Api/Services/InterviewPrep/InterviewPrepService.cs` (`NormalizePriorTurns`), `api/ApplyVault.Api/Options/InterviewPrepAiOptions.cs`, `api/ApplyVault.Api/Services/InterviewPrep/GoogleAiInterviewPrepClient.cs`
- Design handoff: `agent-system/handoffs/active/interview-prep-anti-repeat-2026-08-03/`
