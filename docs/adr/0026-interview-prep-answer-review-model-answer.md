# ADR-0026: Interview Prep Answer review Model answer

## Status

Accepted (2026-08-06 — grill-with-docs + `/operate` D; glossary in `CONTEXT.md`)

Additive to [ADR-0021](0021-interview-prep-v2-bounded-module.md). Does **not** change Interview Prep session lifecycle, Full loop, Study brief (ADR-0025), or session-level report feedback.

## Context

Practice **Answer review** previously surfaced free-text coaching prose plus Strengths, Gaps, and Tips. Tips often restated Gaps, the question appeared twice (header + prose), and seekers lacked a concrete example reply—only hints and directions.

Grill locked vocabulary: **Answer review**, **Answer gap**, **Model answer**, **Coaching tip** (`CONTEXT.md`).

## Decision

1. **Answer review shape.** Sectioned panel only, in this order: question once → Strengths → Gaps → **Model answer** → Coaching tips → optional retry. No free-text coaching blurb; coaching prose must not restate the question.

2. **Model answer.** Primary “what good looks like” artifact: full spoken-prose example reply to that question (not labeled STAR blocks, not tip-shaped hints). Grounded only in Structured CV and optional saved-job facts—no invented roles, projects, or metrics. When evidence is thin, still emit a shorter honest Model answer (do not omit the section).

3. **Answer gaps vs Coaching tips.** Gaps = content/evidence holes in **this** answer (not Brief topic gap tags). Coaching tips = delivery/technique only (structure, STAR, language, length)—must not restate Gaps. The Model answer carries the content fix.

4. **AI / persistence.** Answer-review coaching generation must produce (and persist) Model answer alongside Strengths, Gaps, and Coaching tips. Session-level `GenerateFeedback` used for reports may remain separate unless implementers share a safe seam without changing report semantics.

## Consequences

- Wire DTOs and Angular models gain `modelAnswer` (or equivalent); UI stops rendering overall coaching blurb as the hero.
- AI prompt/schema for answer-review feedback must enforce tip≠gap and CV-grounded spoken Model answer.
- Rejected: tips-only coaching; inventing CV facts for richer samples; labeled STAR script as the Model answer; dual question display.
