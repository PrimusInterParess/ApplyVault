# ApplyVault

ApplyVault helps seekers build a Structured CV and practice interviews grounded in that CV and optional saved jobs.

## CV structuring

**CV document**:
The single per-user container for the original PDF (when uploaded), Structured CV, and profile photo.
_Avoid_: Resume file (when meaning the whole product record), My CV (as a separate product surface)

**CV builder**:
The sole product surface for creating, editing, and exporting a Structured CV—including Blank CV, PDF import, Unplaced content, AI assist (Improve this section, Evaluate CV), profile photo, and project-summary import.
_Avoid_: My CV, CV editor page (legacy surfaces)

**Structured CV**:
The canonical, sectioned representation of a person's CV stored against a CV document—not the raw PDF bytes.
_Avoid_: Parsed CV, JSON CV

**Blank CV**:
A Structured CV created without PDF import, from starter Sections the user fills in. Offered only when the user has no Structured CV yet—not as a wipe of existing content.
_Avoid_: Empty CV, new CV from scratch, template CV (when meaning the structured content, not the export layout)

**Template**:
The single export layout applied when previewing or downloading a Structured CV—not the Structured CV content itself. One-column: name and contact in a header with the profile photo; Summary and every other Section span the full row below (ADR-0038). One page is the aim and two is the maximum the layout tries to meet; a longer CV still downloads with a warning (ADR-0028). There is no user-facing Template choice.
_Avoid_: Theme (when meaning layout), resume design (when meaning Structured CV content)

**PDF import**:
Creating or replacing the Structured CV by uploading a PDF into the user's CV document. A later upload replaces the existing Structured CV after explicit confirmation. PdfPig text first; if that is empty and Gemini is on, the PDF itself is sent to the model (ADR-0032). Failed import shows an error instead of an empty canvas.
_Avoid_: Parse CV, OCR CV (when meaning the product action)

**Starter Entry**:
An Entry shaped for filling (including labeled Contact field slots with empty values)—not sample prose saved as content.
_Avoid_: Placeholder entry, demo entry, sample entry

**Section**:
One of the seven fixed, ordered blocks of a Structured CV: Contact, Summary, Skills, Experience, Projects, Education, Certifications & Languages (ADR-0029). Headings and order are fixed; users edit Entries and set Section visibility, not the Section list.
_Avoid_: Block, chunk, custom section (when meaning a user-added block)

**Section visibility**:
Whether a Section appears on the Template, as a content choice separate from Section placement (ADR-0033). An omitted Section leaves the edit canvas and the Template, and still feeds Evaluate CV and Interview Prep; a visibility change alone does not mark an Interview Prep brief outdated. It returns to the canvas and the Template only when included from the section list — adding Entries does not include it. Any Section except Contact may be omitted; Contact is always included when it has content, a Section with no Entry content is already omitted, and a new Structured CV starts with every Section included.
_Avoid_: Remove section, delete section, disable section (when meaning the Entries are gone)

**Section placement**:
How a Section occupies the pages of the CV builder and the Template (ADR-0034, ADR-0037). A Section continues onto the next page between Entries, and an Entry stays whole. The space between content stays even, with the same inset on both sides. A taller Entry continues between bullets, with each bullet kept whole.
_Avoid_: Page break, compact level

**Fixed section layout**:
The catalog-declared list of the seven Sections and their headings that every Structured CV is normalized to on save, read, Blank CV and import.
_Avoid_: Section template, structure (when meaning the Section list)

**Unplaced content**:
Imported text the parser could not place into a Section, kept on the CV document and shown in a side drawer for copy/paste or dismissal. Never exported.
_Avoid_: Additional information (legacy catch-all Section), residual, leftovers (in UI copy)

**Improve this section**:
The single AI assist action that proposes a rewritten Section (optionally guided by instructions) for review and approval. The Summary uses the same action.
_Avoid_: Update with AI, Regenerate summary, suggestions (when meaning this action)

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
A durable, profession-agnostic study artifact for a seeker, generated from their Structured CV and optional saved job. Its body is structured parts—not a single prose blob: prioritized topics (skills, tools, domains, methods—not software-only “technologies”) each with a Brief topic gap, at least one nested Coverage item, and optional sample questions and CV talking points under that topic—each of those children may carry a short note. It is not a practice Interview Prep session and not an input to one. At most one brief per saved-job binding (and at most one CV-only brief); regenerating replaces that brief—no version history. The brief body is read-only (view/copy/regenerate only). First generate and regenerate may each take an optional free-text seeker focus note (what to emphasize or what a prior brief missed); the note steers generation and is not editable brief content. Generate uses the same language-mix and optional hiring-market choices as practice Interview Prep sessions. The brief is a snapshot at generate time; it is labeled outdated if the Structured CV's Entries have changed since then, or if its bound saved job is missing/deleted—never auto-regenerated. A Section visibility change alone does not mark it outdated.
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

**Answer review**:
The coaching panel for one seeker answer in an Interview Prep session, in this order: question once, Strengths, Gaps, Model answer, Coaching tips, optional retry—no free-text coaching blurb and no restated question.
_Avoid_: Coach chat, feedback dump, suggested answer panel

**Answer gap**:
On Answer review, a content or evidence hole in this specific answer (missing outcome, weak structure claim, ignored part of the question)—not a Brief topic gap tag.
_Avoid_: Brief topic gap (when meaning this), tip, coaching tip

**Model answer**:
On Answer review, the coach’s full example reply to that question as spoken prose (what the seeker could say out loud)—the primary “what good looks like” artifact, distinct from Gaps and Coaching tips. It uses only Structured CV and optional saved-job facts; it does not invent roles, projects, or metrics. When evidence is thin, it stays short and honest rather than omitting the section.
_Avoid_: Suggested answer, sample answer (when meaning this), ideal answer, STAR script (when meaning labeled blocks)

**Coaching tip**:
On Answer review, delivery or technique advice only (structure, STAR, language, length)—not a restatement of content Gaps.
_Avoid_: Tip (when meaning a Gap), practice recommendation (when meaning this)
