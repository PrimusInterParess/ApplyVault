# ADR-0015: Interview Prep optional model answer (client reveal)

## Status

Accepted (2026-08-03 — operate `interview-prep-reveal-answer-2026-08-03`, human-approved option 1)

## Context

Interview Prep (`POST /api/interview-prep/turns`, ADR-0012) already returns `coachMessage`, tip-style `followUps`, optional `scorecard`, and `debriefBullets` on each ephemeral turn. Seekers practicing on `/interview-prep` still get stuck on the current coach question and want a **model / sample answer** they can consult without ending the round.

Product constraints already locked:

- Ephemeral turns only — no session store (ADR-0012).
- Profession-agnostic grounding — no coding-default banks (ADR-0012).
- Existing `followUps` / tips remain; the sample answer is an **additional** optional aid.
- Reveal is **client-only UX** (hidden by default; button when stuck).
- Gemini via raw HTTP only (ADR-0008); auth/tenancy unchanged (ADR-0009 / ADR-0010).
- `hiringMarket` remains orthogonal (ADR-0013).

Frozen baseline: `agent-system/handoffs/archive/interview-prep-ip-01-2026-08-02/frozen-contracts.md` §5 response table.

## Decision

1. **Additive response field `modelAnswer`.** Type: `string | null`. Same turn payload as today (`InterviewPrepTurnResponseDto` / AI turn result). **No** new endpoint, **no** request field, **no** durable store.
2. **Semantics (distinct from `followUps`).**
   - `followUps`: short tips the candidate can use **before** answering (structure cues, angles) — remain visible tip chips.
   - `modelAnswer`: one concise **sample spoken answer** for the **current** coach question — coaching aid only, not a second chat message.
3. **When populated vs null.**
   - Prefer non-null when `phase` is `interview` and `coachMessage` poses or continues a question the candidate should answer.
   - Must be `null` when `phase` is `debrief`.
   - May be `null` on setup / acknowledgment / score-only turns with no answerable question, or when the model has no useful sample.
   - Server normalizes missing / whitespace-only → `null` (same spirit as optional `scorecard` / trimmed strings).
4. **AI schema + prompts.** Extend Gemini `responseSchema` with nullable `modelAnswer` (not required). System prompt must: keep profession-agnostic samples grounded in Structured CV ± optional job + inferred role; honor `languageMix` / hiring-market rules; never invent employers or visa facts; keep the sample short (spoken-interview length, not an essay); never conflate tips with the full sample.
5. **`priorTurns` unchanged.** Do **not** add `modelAnswer` to prior-turn items or re-send it on the next request. Chat history continues to carry only `role` / `text` / `phase` (`coachMessage` text only).
6. **FE reveal UX (client-only).** Hold `modelAnswer` in the facade for the latest turn; default `modelAnswerRevealed = false`; show a reveal control only when `modelAnswer` is non-null and phase is not debrief; toggle shows the text in the existing tips/aid panel (not as a coach chat bubble). Reset reveal state on each successful turn and on session reset. Do **not** auto-insert the sample into the composer (practice remains the user’s answer).
7. **Contract evidence.** Treat as additive delta to `interview-prep-turns` / frozen-contracts §5. Older clients that ignore unknown JSON remain compatible; typed clients add the field as optional/nullable.

## Consequences

- Implementers extend AI raw/result contracts, Gemini schema, system prompt, public response DTO mapping, FE models/facade/page — without sessions or CV mutation.
- Slightly larger Gemini JSON and token use on interview turns that include a sample.
- Model quality risk: empty/weak samples → `null` is acceptable; tips still help.
- Operators overriding `InterviewPrepAi:SystemPrompt` must merge the `modelAnswer` rules.
- Does **not** reopen durable history (IP-13), streaming, or mode-catalog changes.
