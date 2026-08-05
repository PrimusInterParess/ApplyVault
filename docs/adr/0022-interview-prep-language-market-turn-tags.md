# ADR-0022: Interview Prep language, market, and turn language tags

## Status

Accepted (M9 operational delivery, 2026-08-05)

Extends [ADR-0021](0021-interview-prep-v2-bounded-module.md).

## Context

Interview Prep v2 initially shipped English + General market only. Product requires Danish professional interviews, mixed English–Danish allocation, Danish-market communication guidance, and LanguagePractice mode, while keeping language fluency separate from role competence.

## Decision

1. **Operational language values:** `English`, `Danish`, `MixedEnglishDanish` (camelCase JSON wire names).

2. **Operational market values:** `General`, `Danish`.

3. **LanguagePractice mode** is operational with supported persona pairs; it evaluates language separately from role-depth competencies in reporting.

4. **MixedEnglishDanish:** use a **planned language allocation** stored in session plan JSON (`languageAllocation`); turns record a **language** field. Do not switch languages ad hoc mid-stage.

5. **Turn DTOs** expose nullable `language` on interviewer/candidate turns when set.

6. **Candidate report** may include `languageFeedback`; public competency results must not treat language fluency as professional competence (exclude `languageFluency` from `/competencies` role-competency mapping).

7. **Danish market** guidance in prompts/catalogs: direct, respectful, practical ownership and collaboration — no rigid cultural stereotypes; job/company context overrides broad market hints.

8. **FullLoop** is operational per [ADR-0023](0023-interview-prep-full-loop-orchestration.md) (M10).

## Consequences

- Migration adds turn `Language` column where required.
- Prompt registry versions track language/market behavior (e.g. post-M9 bump).
- Frontend can show per-turn language and report language feedback without conflating with hiring-style role scores.
