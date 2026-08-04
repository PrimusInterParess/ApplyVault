# ADR-0018: Interview Prep `modelAnswer` as orientation answer guide

## Status

Accepted (2026-08-04 — product decision: orientation outline only, not fabricated spoken scripts)

## Context

ADR-0015 added optional `modelAnswer` on Interview Prep turns as a **sample spoken answer** the seeker could reveal when stuck. Field plumbing (nullable string, client-only reveal, exclusion from `priorTurns`, null in debrief) worked well.

In practice, requiring a first-person STAR-like paragraph caused the model to ground real CV employers/projects and then **invent** missing tech, metrics, timelines, and outcomes (e.g. claiming gRPC on a project that never used it). That is unsafe for interview rehearsal.

Product decision: `modelAnswer` content must be an **orientational answer guide**, not a concrete script to memorize.

Constraints that remain locked:

- JSON field name stays `modelAnswer` (contract-stable; no DTO rename).
- Reveal UX, nullability rules, and exclusion from AI `priorTurns` stay as in ADR-0015.
- Profession-agnostic grounding, `languageMix`, `hiringMarket` (ADR-0012 / ADR-0013) unchanged.
- Gemini via raw HTTP only (ADR-0008); auth/tenancy unchanged (ADR-0009 / ADR-0010).

## Decision

1. **Supersede ADR-0015 content semantics only.** ADR-0015 decisions on field presence, nullability, reveal UX, and priorTurns exclusion remain in force. The meaning of the string when populated changes as below.

2. **`modelAnswer` = answer guide (orientation outline).** When populated, the value is a short coaching brief in **second-person / imperative** voice (e.g. “Use STAR. Pick a real disagreement from your CV… Cover: …”). It must include:
   - recommended structure (STAR when behavioral, or another fit for the mode/question);
   - 3–5 points the seeker should cover;
   - optional hooks that appear in Structured CV and/or optional job JSON only (employer, project theme, role cues, culture phrases from the JD).

3. **Distinct from `followUps`.** `followUps` remain short tip chips (structure/angle cues). `modelAnswer` is the fuller orientation brief for the same question — still not a second chat message and not auto-inserted into the composer.

4. **Hard bans in guides.** Do **not**:
   - write a first-person “In my previous role…” spoken narrative;
   - invent employers, credentials, projects, tech stacks/protocols, metrics, timelines, PoC outcomes, visa, or work-permit facts;
   - name tools or stacks that are not present in the Structured CV / job JSON for that evidence;
   - use markdown (`**`, `*`, `#`, backticks, fences, `-` / `1.` bullet lists). Plain text only — one short paragraph or sentences joined with “→” / commas.

5. **Sparse CV fallback.** If the CV lacks a concrete example for the question, instruct the seeker to choose a real one from their experience and keep missing tech/outcome slots generic (e.g. `[your stack]`, `[outcome]`) rather than inventing details.

6. **Populate / null rules unchanged.** Prefer non-null when `phase` is `interview` and `coachMessage` poses or continues an answerable question; must be `null` in debrief; may be `null` on setup / acknowledgment / score-only turns. Server still normalizes missing / whitespace-only → `null`.

7. **UI copy.** Client labels refer to this aid as **Answer guide** (reveal/hide), not “Sample answer”, so seekers do not treat it as a script.

8. **Prompts.** Default `InterviewPrepAi` system and user prompts must encode the guide shape and bans. Operators overriding `InterviewPrepAi:SystemPrompt` must merge these rules.

## Consequences

- New turns produce safer rehearsal aids; quality depends on prompt adherence (no server-side tech inventiveness validation in this decision).
- Stored session messages may still hold older spoken-script `ModelAnswer` text until regenerated.
- ADR-0015 Status notes this partial supersession; implementers update prompts and FE copy without API renames.
- Does not reopen streaming, mode catalog, or durable-history contracts.

## Links

- Supersedes (semantics only): `docs/adr/0015-interview-prep-model-answer-reveal.md`
- Related: ADR-0012, ADR-0013, ADR-0016
