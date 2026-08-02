# ADR-0013: Interview Prep optional hiring-market bias (independent of languageMix)

## Status

Accepted (2026-08-02 — operate `interview-prep-danish-market-2026-08-02` / IP-DK-05)

## Context

ADR-0012 froze Interview Prep `languageMix` as `en` | `da` | `mixed`. A later prompt-only Danish hiring-market bias (plan M1) activates when a linked job clearly signals Denmark **or** when `languageMix` is `da` / `mixed`.

That coupling made **English interviews for the Danish market** awkward: seekers had to link a clear Denmark job, or switch away from English-only practice. Product need: language of the mock and hiring-market coaching cues must be independently selectable.

ADR-0012 (ephemeral turns, profession-agnostic modes/scorecard), ADR-0008 (Gemini HTTP), ADR-0009 / ADR-0010 remain in force.

Plan: `agent-system/implementation-plan-interview-prep-danish-market.md` (IP-DK-05).

## Decision

1. **Optional request field `hiringMarket`.** Allowed values: `general` | `dk`. Omitted or null → treat as `general`. Invalid values → `400` validation (same style as `languageMix`).
2. **Orthogonal to `languageMix`.** `hiringMarket` does not change spoken/written language. Supported combination: `languageMix=en` + `hiringMarket=dk` (English mock with Danish hiring-market coaching cues). `da` / `mixed` remain language choices and may still imply DK-relevant bilingual practice.
3. **Danish hiring-market bias activation** (system prompt + server-resolved value passed into the user prompt template):
   - Apply when **any** of: (a) `hiringMarket=dk`, (b) optional job context clearly indicates Denmark/Danish market, (c) `languageMix` is `da` or `mixed`.
   - When `hiringMarket=general`, `languageMix=en`, and no clear DK job signal → stay market-agnostic.
   - For `languageMix=en` with bias applied: keep English; allow DK-market content cues (motivation/culture/process). Never invent visa/work-permit facts.
4. **No new mode id.** Do not add `danish_market` to the mode catalog. UI exposes a Hiring market control (chips) alongside Language mix.
5. **Default.** Server default `general` (options may document `InterviewPrepAi:DefaultHiringMarket` if needed; recommended `general`). UI default `general`.
6. **Contract evidence.** Extend `interview-prep-turns` request table and frozen-contracts note; code on `InterviewPrepTurnRequest` / AI turn request / prompt placeholders.

## Consequences

- Frontend can offer English + Danish market without forcing Mixed/Danish language or a DK job link.
- Prompt operators who override `InterviewPrepAi:SystemPrompt` must keep the updated activation rules (include `hiringMarket=dk`).
- Existing clients that omit `hiringMarket` keep prior behavior (job signal + da/mixed only).
- Supersedes the MVP “no new API field” non-goal from the Danish-market plan for IP-DK-05 only; does not reopen mode catalog or durable sessions.
