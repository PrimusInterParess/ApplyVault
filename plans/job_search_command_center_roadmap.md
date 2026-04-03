# Product Roadmap: From Job Archive to Job-Search Command Center

## Product Goal

Make the app dependable enough to trust with capture, then valuable enough to use every day. The roadmap starts with extraction reliability because every downstream workflow depends on having clean, structured, timely job data.

## Working Assumptions

- Small team with limited engineering bandwidth.
- Existing app already stores jobs, but capture quality and workflow depth are uneven.
- Goal is to increase daily active usage, saved-to-applied conversion, and long-term retention.
- Prioritization favors everyday utility over advanced AI.

## Success Criteria

- Users trust saved jobs to be complete and accurate.
- Users can manage application status, reminders, and notes without leaving the app.
- The app surfaces what needs attention today.
- AI summaries help decision-making instead of compensating for weak core workflows.

## Phase 1: Make Capture Quality Extremely Reliable

### Objective

Create a dependable ingestion layer that works across major ATS and job-board sources with minimal cleanup.

### Why First

If capture is flaky, every later feature inherits bad data. Reliable ingestion is the foundation for pipeline tracking, reminders, artifacts, and AI summaries.

### Scope

- Normalize job title, company, location, remote/hybrid/on-site, compensation when available, job URL, source, posted date, and description.
- Detect and deduplicate the same job captured from multiple sources.
- Add confidence scoring for extracted fields.
- Flag low-confidence captures for review instead of silently saving bad data.
- Build source-specific handling for the most important ATS/job sites first.

### Detailed Plan

1. Define the canonical job schema.
   - Lock down the fields required for every saved job.
   - Separate required fields from optional enrichments.
   - Define how unknown, missing, or conflicting values are represented.
2. Build an extraction reliability score.
   - Score completeness and confidence per job and per field.
   - Record why a field is low confidence: missing selector, malformed date, ambiguous location, truncated description.
   - Use this score to drive QA, monitoring, and UX warnings.
3. Add source-level observability.
   - Track success rate by source domain and ATS platform.
   - Measure extraction failure types, retries, and cleanup rates.
   - Create a weekly leaderboard of the worst-performing sources.
4. Create a review-and-recovery workflow.
   - Add a lightweight "needs review" state for low-confidence imports.
   - Let users quickly fix key fields instead of editing raw content.
   - Save corrections as signals that improve future extraction rules.
5. Prioritize source coverage strategically.
   - Start with the highest-volume ATS/site families.
   - Improve breadth only after the top sources are stable.
   - Maintain a compatibility matrix: fully supported, partial, unreliable.
6. Strengthen normalization.
   - Normalize location formats, remote labels, employment type, seniority hints, and salary ranges.
   - Standardize company naming to reduce duplicates.
   - Parse dates consistently across sources and time zones.
7. Build a regression harness for ingestion.
   - Save representative examples from major sites.
   - Re-run extraction against this corpus whenever parsers change.
   - Treat extraction regressions like production bugs.

### MVP Deliverables

- Canonical schema and validation rules.
- Confidence scoring and low-confidence queue.
- Deduplication rules.
- Source health dashboard.
- Regression test corpus for top sources.

### Exit Criteria

- Top sources have consistently high structured-field completion.
- Manual cleanup rate drops meaningfully.
- Duplicate-job complaints are rare.
- Users can trust imported jobs enough to start tracking applications inside the app.

### Risks

- Long-tail site variability can consume too much time.
- Overfitting to selectors creates fragile scrapers.
- Weak observability makes failures invisible until users complain.

## Phase 2: Add a Real Application Pipeline

### Objective

Turn each job into a trackable application record with clear stage history, dates, and next actions.

### Why Second

Once jobs are captured reliably, the highest-value workflow is tracking where each opportunity stands.

### Scope

- Default stages: saved, applied, screening, interview, offer, rejected.
- Stage timestamps and stage history.
- Next step field with owner and due date.
- Simple board/list filtering by stage.

### Detailed Plan

1. Separate job data from application state.
   - Keep imported job content distinct from user-managed pipeline status.
   - Support one application workflow per saved job initially.
2. Design stage transitions.
   - Define default transitions and edge cases.
   - Preserve stage history rather than overwriting state.
   - Capture when and why a stage changes.
3. Add core pipeline UX.
   - Let users update stage in one click.
   - Show latest status, last update date, and next planned action.
   - Support stage filters and sorting by urgency.
4. Add activity timeline.
   - Record meaningful events: imported, applied, interview scheduled, note added, reminder completed.
   - Use the timeline to give each job a clear story.
5. Add pipeline reporting basics.
   - Show counts by stage.
   - Track conversion from saved to applied and applied to interview.
   - Surface stale opportunities with no activity.

### MVP Deliverables

- Stage model and history.
- Pipeline board/list views.
- Next-step field and date.
- Activity timeline.

### Exit Criteria

- Users can manage active applications without external spreadsheets.
- Pipeline state feels fast and lightweight to update.
- Counts by stage are accurate enough for weekly review.

## Phase 3: Add Reminders and Follow-Up Tracking

### Objective

Make the app useful every day by telling users what needs attention now.

### Why Third

This is the habit-forming layer. Once users track stages, reminders create daily return value.

### Scope

- Follow-up reminders.
- Interview prep reminders.
- Application deadline reminders.
- Today/This Week task views.

### Detailed Plan

1. Build a reminder object tied to a job.
   - Include type, due date, status, and optional note.
   - Allow reminders to be created from stage changes and manual actions.
2. Add smart defaults.
   - After "applied," suggest a follow-up reminder.
   - Before "interview," suggest prep reminders.
   - For stale jobs, suggest a close-the-loop action.
3. Create an attention dashboard.
   - Show overdue, due today, and upcoming items.
   - Make completion fast and satisfying.
   - Link directly into the relevant job record.
4. Build notification strategy carefully.
   - Start with in-app reminders and optional digest.
   - Add email/push later only if users act on in-app reminders consistently.
   - Avoid noisy alerts that reduce trust.
5. Measure actionability.
   - Track reminder completion rate, snooze behavior, and ignored reminders.
   - Tune defaults based on actual user follow-through.

### MVP Deliverables

- Reminder model.
- Today view.
- Auto-suggested reminders from key events.
- Basic notification settings.

### Exit Criteria

- Users open the app to see what to do today.
- Reminder completion becomes a repeatable engagement loop.
- Reminder volume feels helpful, not spammy.

## Phase 4: Add Per-Job Notes and Application Artifacts

### Objective

Consolidate all role-specific material so users can run their process in one place.

### Why Fourth

After users trust the data and workflow, they need the app to hold the supporting assets that usually spill into docs and spreadsheets.

### Scope

- Resume version used.
- Cover letter notes.
- Portfolio links.
- Recruiter contacts.
- Interview notes.

### Detailed Plan

1. Create a per-job workspace.
   - Group notes, contacts, links, and artifacts under one job.
   - Make it obvious what was used for this role.
2. Start with structured metadata before full file complexity.
   - Resume version name or identifier.
   - Contact names and channels.
   - URLs for portfolio/GitHub/case studies.
   - Rich text notes for interview prep and debriefs.
3. Add templates for speed.
   - Interview prep checklist.
   - Follow-up note template.
   - Recruiter outreach note template.
4. Decide attachment depth pragmatically.
   - MVP can reference files/links rather than handling heavy document management.
   - Add uploads only if users strongly need in-product storage.
5. Connect artifacts to workflow moments.
   - Show the resume used when an application reaches interview.
   - Surface interview notes alongside reminders and timeline events.

### MVP Deliverables

- Job notes.
- Contact cards.
- Resume-version association.
- Link storage for portfolio and external docs.

### Exit Criteria

- Users no longer need a parallel spreadsheet/doc for active roles.
- Interview prep and recruiter context are easy to recover.
- Important context is visible from the job detail page.

## Phase 5: Improve Extraction and Normalization Beyond MVP

### Objective

Upgrade from "reliable enough" to "product-grade across diversity of sources and edge cases."

### Why Fifth

Once core workflows exist, better normalization compounds their usefulness and makes downstream automation stronger.

### Scope

- Better parsing of compensation, seniority, location constraints, and qualifications.
- Expanded ATS/source support.
- Smarter duplicate detection and merged record handling.

### Detailed Plan

1. Expand compatibility matrix.
   - Add more ATS families and direct employer career sites.
   - Prioritize by user demand and traffic volume.
2. Improve semantic normalization.
   - Map varied wording into standard labels for seniority, work model, and job family.
   - Distinguish hard requirements from nice-to-haves when possible.
3. Harden merge logic.
   - Merge duplicate records without losing user notes, pipeline state, or reminders.
   - Prefer user-entered values over re-imported low-confidence fields.
4. Add backfill and reprocessing.
   - Allow improved extractors to re-run on existing jobs.
   - Keep user-edited fields protected from destructive overwrite.
5. Close the QA loop.
   - Use correction patterns and failure logs to decide where to invest next.
   - Review source health regularly as an operating rhythm.

### MVP Deliverables for This Phase

- Reprocessing framework.
- Better normalization dictionaries and rules.
- Expanded source support based on demand.

### Exit Criteria

- Import quality remains strong as source diversity grows.
- Historical jobs benefit from parser improvements.
- Downstream summaries and filters become more trustworthy.

## Phase 6: Add AI Role-Fit and Key-Requirement Summaries

### Objective

Transform raw job text into quick, actionable insight that helps users decide where to invest effort.

### Why Last

AI is most valuable after the app already captures dependable structured data and supports the actual workflow. Otherwise it becomes a thin layer over unreliable inputs.

### Scope

- Top skills required.
- Seniority summary.
- Location constraints.
- Compact "why this may fit you" explanation.
- Clear distinction between extracted facts and generated interpretation.

### Detailed Plan

1. Start with explainable summaries.
   - Extract top requirements and constraints from the job text.
   - Label what is directly evidenced in the posting.
   - Avoid overstating certainty.
2. Add fit reasoning carefully.
   - Base fit explanations on user profile, resume signals, or preferences only if those inputs are available and current.
   - Keep the output compact and decision-oriented.
3. Separate extraction from inference.
   - Facts: required skills, location, seniority, visa constraints.
   - Inference: likely fit, likely gaps, tailoring suggestions.
   - Display them differently so users know what to trust.
4. Add user feedback loops.
   - Let users rate summaries as helpful or off-base.
   - Use feedback to improve prompting and extraction heuristics.
5. Focus on decision support.
   - Help users answer: apply, tailor resume, follow up later, or archive.
   - Avoid long narrative summaries that do not change user behavior.

### MVP Deliverables

- Compact requirement summary.
- Seniority/location extraction display.
- Initial fit summary with clear confidence language.
- User feedback on summary usefulness.

### Exit Criteria

- Users can triage saved roles faster.
- Summaries reduce time spent reading repetitive job descriptions.
- Trust remains high because outputs are concise and clearly framed.

## Release Framing

## MVP Next

- Capture reliability foundation.
- Pipeline stages with dates and next steps.
- Basic reminders for follow-up and interview prep.

## Good V1

- Per-job notes, contacts, and linked artifacts.
- Better normalization and source coverage.
- Today dashboard that drives repeat use.

## Later / Nice-to-Have

- Advanced AI fit reasoning.
- Richer notifications and digests.
- Broader automation based on user behavior patterns.

## Suggested Operating Metrics

- Import success rate by source.
- Low-confidence import rate.
- Manual edit rate after import.
- Duplicate rate.
- Percent of saved jobs moved into a pipeline stage.
- Reminder completion rate.
- Weekly active users with at least one workflow action.
- Share of active jobs with notes/artifacts attached.
- AI summary helpfulness rating.

## Suggested Team Sequence

1. Reliability sprint: schema, scoring, observability, low-confidence review.
2. Workflow sprint: pipeline state and activity history.
3. Habit sprint: reminders and Today view.
4. Workspace sprint: notes, contacts, and artifacts.
5. Quality expansion sprint: broader source support and reprocessing.
6. Intelligence sprint: fit summaries and requirement extraction.

## Guiding Principle

Do not use AI to paper over weak ingestion or weak workflow design. First make saved jobs trustworthy, then make active jobs manageable, then make decisions faster.
