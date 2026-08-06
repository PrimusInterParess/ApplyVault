# ApplyVault

ApplyVault helps seekers build a Structured CV and practice interviews grounded in that CV and optional saved jobs.

## CV structuring

**CV document**:
The single per-user container for the original PDF (when uploaded), Structured CV, and profile photo.
_Avoid_: Resume file (when meaning the whole product record), My CV (as a separate product surface)

**CV builder**:
The sole product surface for creating, editing, structuring, and exporting a Structured CV—including Template choice, Blank CV, PDF import, AI assist, profile photo, and project-summary import.
_Avoid_: My CV, CV editor page (legacy surfaces)

**Structured CV**:
The canonical, sectioned representation of a person's CV stored against a CV document—not the raw PDF bytes.
_Avoid_: Parsed CV, JSON CV

**Blank CV**:
A Structured CV created without PDF import, from starter Sections the user fills in. Offered only when the user has no Structured CV yet—not as a wipe of existing content.
_Avoid_: Empty CV, new CV from scratch, template CV (when meaning the structured content, not the export layout)

**Template**:
The export layout applied when previewing or downloading a Structured CV—not the Structured CV content itself. Choosing a Template with an existing Structured CV keeps that content and only changes presentation.
_Avoid_: Theme (when meaning layout), resume design (when meaning Structured CV content)

**PDF import**:
Creating or replacing the Structured CV by uploading a PDF into the user's CV document. A later upload replaces the existing Structured CV after explicit confirmation.
_Avoid_: Parse CV, OCR CV (when meaning the product action)

**Starter Entry**:
An Entry shaped for filling (including labeled Contact field slots with empty values)—not sample prose saved as content.
_Avoid_: Placeholder entry, demo entry, sample entry

**Section**:
A titled block in a structured CV (for example Experience or Education), kept in display order.
_Avoid_: Block, chunk

**Entry**:
One logical item inside a section (one role, one degree, one skill group).
_Avoid_: Row, item (when meaning entry)

**Section type**:
The kind of section, which determines which fields each entry may carry—not merely the heading text from the PDF.
_Avoid_: Category, normalized key

**Typed entry fields**:
The field set allowed for entries in a given section type (for example role + employer + dates for Experience).
_Avoid_: Columns, properties bag

**Section schema catalog**:
The single versioned definition of section types and their entry fields, maintained as declarative data in the repository—not duplicated in prompts or UI code.
_Avoid_: Hardcoded section rules, prompt-only schema

**Project summary import**:
Adding saved project summaries from the Projects surface into the Structured CV as Projects Entries, tracked by source summary identity so the same summary is not imported twice.
_Avoid_: Merge projects, paste README (when meaning this product action)

## Interview Preparation

**Interview Prep brief**:
A durable, profession-agnostic study artifact for a seeker, generated from their Structured CV and optional saved job. Its body is structured parts—not a single prose blob: prioritized topics (skills, tools, domains, methods—not software-only “technologies”) each with a Brief topic gap, at least one nested Coverage item, and optional sample questions and CV talking points under that topic—each of those children may carry a short note. It is not a practice Interview Prep session and not an input to one. At most one brief per saved-job binding (and at most one CV-only brief); regenerating replaces that brief—no version history. The brief body is read-only (view/copy/regenerate only). First generate and regenerate may each take an optional free-text seeker focus note (what to emphasize or what a prior brief missed); the note steers generation and is not editable brief content. Generate uses the same language-mix and optional hiring-market choices as practice Interview Prep sessions. The brief is a snapshot at generate time; it is labeled outdated if the Structured CV has changed since then, or if its bound saved job is missing/deleted—never auto-regenerated.
_Avoid_: Conspect, prep section, study notes (when meaning this product record), coach chat, brief history, session seed, technologies (when meaning the topic list as a whole), auto-refresh brief, editable brief, markdown-only brief, brief-level question list (when meaning sample questions or talking points outside a topic)

**Coverage item**:
One concrete leaf under a topic in an Interview Prep brief—a syllabus line to study, not a second topic, not a nested tree, and not a progress checklist.
_Avoid_: Subtopic, extended topic, study point, checklist item (when meaning this brief child)

**Brief topic gap**:
The tag on a topic in an Interview Prep brief: already-strong, must-study, nice-to-have, or unclear (not enough CV/job evidence)—not a numeric priority alone.
_Avoid_: Priority (when meaning this tag), strength score, gap score

**Interview Prep session**:
One durable practice interview for a seeker, with turns, Stages, and configuration (mode, persona, language, market).
_Avoid_: Coach chat (MVP), ephemeral turn thread

**Full loop**:
A single Interview Prep session that runs multiple Stages in one continuous interview day, advanced by the interviewer—not by the seeker clicking between Stages.
_Avoid_: Multi-session panel, manual next-stage interview

**Stage**:
One interviewer block inside a session (mode × persona plan, coverage, and turns). In Full loop, a Stage ends with a Stage handoff to the next Stage, not with Close—except the final Stage of the loop.
_Avoid_: Round (when meaning Stage), child session

**Stage handoff**:
The interviewer-led transition from a completed Full-loop Stage to the next: outgoing persona acknowledgment, private factual Stage assessment for later context, then the next Stage opening—without seeker orchestration.
_Avoid_: Next stage button (happy path), per-stage wrap-up

**Main question**:
An interviewer Ask-question turn that advances assessment coverage for a competency.
_Avoid_: Turn (when meaning only mains), prompt

**Probe**:
An interviewer clarification or follow-up under the current intent; it does not by itself end a Stage or the Full loop.
_Avoid_: Follow-up as Stage end, clarification as Close

**Soft question target**:
The intended band of Main questions per Stage (~8–12) that the interviewer aims for when coverage allows.
_Avoid_: Hard cap, MaxQuestions (when meaning the soft band)

**Hard question safety**:
The Main-question ceiling (~15–18) that forces Stage end (handoff or, for standalone, toward Close)—not probes alone.
_Avoid_: Soft target, default budget (legacy four-question force close)

**Candidate questions**:
The beat where the seeker may ask the interviewer. In Full loop, once at the end of the whole loop; in standalone modes, at session end before Close.
_Avoid_: Per-stage Q&A close, wrap-up questions (when meaning Close)

**Close**:
The terminal interviewer turn that ends the practice interview (or the whole Full loop). The Stage or session completes after the seeker’s reply to Close—not when Close is emitted.
_Avoid_: Handoff, Candidate questions
