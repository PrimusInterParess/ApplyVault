# ADR-0017: Interview Prep already-asked digest (anti-repeat)

## Status

Accepted (2026-08-03 — operate `interview-prep-anti-repeat-2026-08-03`)  
**Amended** (2026-08-03 — operate `interview-prep-repeat-stall-2026-08-03`; Principal choice P: A+B+C+D)  
**Amended** (2026-08-03 — prompt-first simplify: digest back to outside-window only; stronger session-pacing prompts; keep exact-duplicate retry)

## Context

Durable Interview Prep (ADR-0016) persists the **full** session transcript (abuse-capped by `MaxMessagesPerSession`, default **200**) while Gemini context continues to use only the **tail** of prior turns (`MaxPriorTurns`, default **12**; per-turn char cap `MaxPriorTurnChars`). Evidence:

- `InterviewPrepService.NormalizePriorTurns` truncates oldest turns via `Skip(count - maxPriorTurns)`.
- Durable turns build AI `priorTurns` from all stored messages, then apply that truncate.
- `modelAnswer` stays out of AI `priorTurns` (ADR-0015).

**Original symptom:** long sessions re-ask questions that fell out of the Gemini window.

**Follow-up symptom:** short sessions looped on thematic then exact duplicate `coachMessage`. A brief full-transcript digest amend increased overlap with `priorTurnsJson` and did not help; reverted to **outside-window digest** + stronger session-pacing prompts + server exact-duplicate retry.

ADR-0012 §2–8 (profession-agnostic coach, dedicated `/api/interview-prep`, no hardcoded profession question banks), ADR-0008 (Gemini HTTP), and ADR-0010 (per-user tenancy) remain in force. Ephemeral `POST /turns` without `sessionId` remains for API compatibility (ADR-0016).

## Decision

1. **Server-built already-asked digest (no new tables).** On each coach turn, before (or independently of) `MaxPriorTurns` truncation, the server derives a compact **already-asked questions** list from the **full** prior transcript available to that request path:
   - **Durable** (`sessionId` set): from stored session messages (same source ADR-0016 uses for AI priorTurns).
   - **Ephemeral** (`sessionId` omitted): from the client-supplied `priorTurns` array **before** tail truncation (best-effort; product path is durable).

2. **Digest contents (deterministic, profession-agnostic).** Include `role=coach` + `phase=interview` texts that are **outside** the retained `MaxPriorTurns` tail (what the model can no longer see in `priorTurnsJson`). Exclude user turns, debrief coach, and **never** `modelAnswer` (ADR-0015). Prefer **newest** fallen-out items under caps. Empty when nothing fell out of the window.

3. **Inject into the Gemini user prompt; keep truncated priorTurns.** Placeholder `{{alreadyAskedJson}}` as a **BLOCKLIST** (not inspiration), alongside `{{priorTurnsJson}}`. Do not duplicate in-window questions into the digest.

4. **Session-pacing prompts (primary in-window control).** System + user templates: one new competency per turn; at most one deepen/reframe; next turn must switch theme; treat priorTurns + already-asked as forbidden. Remain profession-agnostic (ADR-0012).

5. **Exact-duplicate coach gate (server).** After AI normalize, before persist/return: if interview-phase `coachMessage` equals any prior coach+interview text under normalize (trim + collapse whitespace + ordinal ignore-case), perform up to `MaxCoachDuplicateRetries` (default **1**, clamp 0–2) silent regenerates with a corrective nudge. If still duplicate after retries, accept last result and log a warning. Thematic / near-duplicate similarity is **not** server-enforced.

6. **Caps via options (defaults).**
   - `MaxAlreadyAskedItems` — default **40**
   - `MaxAlreadyAskedItemChars` — default **240**
   - `MaxAlreadyAskedTotalChars` — default **4_000**
   - `MaxCoachDuplicateRetries` — default **1**
   - Keep `MaxPriorTurns=12` and `MaxMessagesPerSession=200` unchanged unless a later ADR revisits budgets.

7. **No public API / DTO change.** Digest and retry are server-only inside the AI client request pipeline. Public turn/session DTOs stay as ADR-0015 / ADR-0016. FE may fix tip-list track hygiene independently (companion only).

8. **Out of scope.** New DB tables or persisted question indexes; embedding / similarity search; second Gemini “summarizer” call; raising `MaxPriorTurns` to match transcript length; changing `modelAnswer` priorTurns policy; thematic server similarity thresholds.

## Consequences

- Long sessions get a compact outside-window blocklist; short sessions rely on priorTurns + session-pacing prompts.
- Exact duplicate `coachMessage` is hard-gated with one retry; thematic loops are prompt-gated (soft).
- Digest does not overlap the recent priorTurns window (avoids double-listing that can reinforce themes).
- One retry adds latency/cost only on duplicate hits.
- Operators who override `InterviewPrepAi:SystemPrompt` or `UserPromptTemplate` must merge the anti-repeat rule and `{{alreadyAskedJson}}` semantics.
- Ephemeral clients that already send a truncated `priorTurns` still cannot recover dropped questions — acceptable; FE product path is durable (ADR-0016).

## Rejected alternatives

| Option | Why rejected |
| --- | --- |
| Raise `MaxPriorTurns` to ~200 | Token cost / context pressure with CV + job JSON; does not scale |
| Extra Gemini summarize-history call | Latency + cost; second failure mode; larger than needed |
| Persist `AlreadyAsked` table / question bank | New store; risk of hardcoded banks; unnecessary vs transcript scan |
| Client-supplied `alreadyAsked` field | Public contract change; durable path already session-wins |
| Embedding / thematic similarity de-dupe | New infra; overkill; keep thematic soft |
| Prompt-only in-window fix | Already failed with priorTurns visible and exact duplicate emitted |
| FE-only stall fix | NG0956 tip track is companion noise, not stall root |

## Links

- Related: ADR-0012, ADR-0015, ADR-0016, ADR-0008, ADR-0010
- Evidence: `InterviewPrepAlreadyAskedDigest.cs`, `InterviewPrepService.cs`, `InterviewPrepAiOptions.cs`, `GoogleAiInterviewPrepClient.cs`
- Operate: `interview-prep-anti-repeat-2026-08-03`, `interview-prep-repeat-stall-2026-08-03`
