# ADR-0025: Interview Prep brief (durable study artifact)

## Status

Accepted (2026-08-05 — grill-with-docs + `/operate` A/B; glossary in `CONTEXT.md`)

**Amended** (2026-08-05 — grill-with-docs nested **Coverage item** body; `/operate` D + `CONTEXT.md`)

Additive to [ADR-0021](0021-interview-prep-v2-bounded-module.md). Does **not** change Interview Prep session lifecycle, Full loop, or session AI turns.

## Context

Seekers need a **study** surface before or beside live practice: a preparation outline grounded in Structured CV and optional saved job. Practice already lives as an **Interview Prep session** (ADR-0021). Merging study into the session loop would couple regenerate/staleness to interview state and blur product jobs.

A flat topic list (name + gap only) is a label set, not a study plan. Seekers need each prioritized topic to expand into concrete syllabus lines, with sample questions and CV talking points kept with that topic—not as orphan brief-level piles.

Grill decisions locked vocabulary: **Interview Prep brief**, **Brief topic gap**, and **Coverage item** (`CONTEXT.md`).

## Decision

1. **New durable product object — Interview Prep brief.** Independent of Interview Prep sessions: sessions never require or read a brief in v1. Same bounded module / REST prefix `/api/interview-prep/*` (e.g. `/briefs`), not under `cv-documents`.

2. **Inputs.** Required Structured CV (same adapters as sessions). Optional owned saved job (`scrapeResultId`). Optional free-text **focus note** on first generate and on regenerate. Generate uses the same **Language** and **Market** choices as practice sessions (ADR-0021 / ADR-0022).

3. **Cardinality.** At most one brief per `(user, scrapeResultId)` binding, plus at most one CV-only brief (`scrapeResultId` null). Regenerate **replaces** that row — no version history.

4. **Body shape.** Structured parts (not a single markdown blob): prioritized **topics** (profession-agnostic — skills/tools/domains/methods; avoid software-only “technologies”), each with:
   - a **Brief topic gap** (`alreadyStrong` | `mustStudy` | `niceToHave` | `unclear`) and numeric priority on the topic;
   - **≥1 Coverage item** (leaf: text + optional note; no gap/priority; no deeper nesting);
   - optional **sample questions** and **CV talking points** under that topic (each text + optional note; lists may be empty);
   - the three child lists are **independent siblings** (no links from questions/talking points to a Coverage item).
   Short notes allowed on the topic and on each child. No brief-level sample-question or talking-point lists.

5. **Read-only.** Body is view/copy/regenerate only — no seeker edit of stored brief content; focus note is generate input only.

6. **Snapshot + outdated (no auto-regen).** Persist generate-time source fingerprints (at minimum Structured CV change token + bound job presence). Label **outdated** when the Structured CV has changed since generate, or when the bound saved job is missing/deleted. Never auto-regenerate.

7. **AI.** Application owns persistence and outdated computation. AI proposes structured brief JSON via the Interview Prep AI gateway (ADR-0008 HTTP Gemini pattern); validate schema before save (including ≥1 Coverage item per topic). Profession-agnostic prompting (same constraint family as sessions).

8. **Entry points (product).** Sibling study surface under Interview Prep **and** deep-link/action from a saved job (`jobId` → `scrapeResultId`), mirroring session targeting.

## Consequences

- New EF entity/table (e.g. `InterviewPrepBriefs`) with unique index on `(UserId, ScrapeResultId)` (null-safe CV-only).
- Contract / AI JSON: topics nest `coverageItems`, `sampleQuestions`, and `talkingPoints` (not brief-root lists).
- Frontend: brief UI as topic cards with nested Coverage items + optional Q/talking points; job-card / deep-link entry; outdated banner + regenerate with optional focus note.
- Out of v1: brief → session seeding, edit-in-place, version history, auto-regen, progress checklists (Coverage items are not checkable).
- Plan / mapping: `agent-system/implementation-plan-interview-prep-brief.md`, `agent-system/repository-task-mapping-interview-prep-brief.md` — amend when implementing the nested body (code still flat until that follow-up).
- Rejected for this amendment: flat topics only; brief-level Q/talking points; per–Coverage-item gaps; linked Q→Coverage refs; multi-level syllabus trees.
