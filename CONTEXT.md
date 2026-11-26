# ApplyVault

ApplyVault helps seekers build a Structured CV and prepare for interviews with documents tailored to their background and target role.

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
Whether a Section appears on the Template, as a content choice separate from Section placement (ADR-0033). An omitted Section leaves the edit canvas and the Template, and still feeds Evaluate CV and Interview Prep; a visibility change alone does not mark an Interview preparation pack outdated. It returns to the canvas and the Template only when included from the section list — adding Entries does not include it. Any Section except Contact may be omitted; Contact is always included when it has content, a Section with no Entry content is already omitted, and a new Structured CV starts with every Section included.
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

**Interview preparation pack**:
A saved collection of preparation documents tailored to a seeker's target role and supplied context: their Structured CV, optional saved job, selected project summaries, and additional information. The generator works across seekers, professions, and projects; each pack's subject matter, examples, and depth reflect the individual seeker's needs.
_Avoid_: Generic study pack (when meaning identical content for every seeker), question bank, practice session, Interview Prep brief (retired outline)

**Preparation document**:
One connected teaching or practice document within an Interview preparation pack, with a clear purpose and any prerequisite documents. It develops explanations, mechanisms, examples, choices, and application appropriate to the seeker's target.
_Avoid_: Answer script, coverage item (retired syllabus leaf)

**Preparation inputs**:
The seeker's target, available study time, language, expected formats, self-reported knowledge, additional information, and selected sources used to personalize a pack. Additional information may supply facts beyond the CV; user-reported facts remain distinct from inferred or hypothetical material.
_Avoid_: Focus note (retired steering-only input), verified experience (when meaning a user report)

**Document discovery**:
The short clarification step for one Preparation document, using its purpose, the seeker's sources, and context already saved elsewhere in the pack. It gathers information and learning needs before that document is written or rewritten.
_Avoid_: Practice interview, assessment, mandatory quiz

**Clarification answer**:
A seeker's explicitly saved response to a Document discovery question, including an answer, uncertainty, a skip, or an unfinished response. Saved answers are shared context for the pack; reported uncertainty about knowledge and missing evidence about past work have different meanings.
_Avoid_: Verified achievement, model answer, competency score

**Answer guidance**:
Explanations, blank starting structures, and clearly hypothetical examples that help a seeker answer a clarification question, including further help on request. Guidance and unconfirmed drafts do not become saved personal facts automatically.
_Avoid_: Suggested personal achievement, auto-filled experience, answer review

**Study plan**:
A sequence of concrete preparation activities that fits the seeker's chosen number of days and daily study time. Completing an activity is separate from demonstrating readiness.
_Avoid_: Interview stages, Full loop, equal-time syllabus

**Preparation readiness**:
The seeker's own record of having read, explained without notes, demonstrated, or adapted a document's material. A CV skill mention or a completed study day does not establish readiness.
_Avoid_: Competency score, AI-assessed mastery, already-strong (inferred from CV presence)

**Review log**:
The seeker's saved observations about weaknesses, corrections, evidence from attempts, and next practice tasks for a preparation pack.
_Avoid_: Answer review, coach chat

## Cover letter

**Cover letter**:
A saved application letter for one saved job, written from that job’s description and the seeker’s Structured CV. An optional seeker note steers emphasis and is not a source of new facts. One job keeps one letter; writing again replaces it (ADR-0042, ADR-0044).
_Avoid_: Motivation letter (when meaning a separate document), application essay

## Job search

**Work in Denmark search query**:
A keyword search for one Work in Denmark search, with at least one keyword and at most one optional Place. A Place narrows that search (ADR-0041).
_Avoid_: Jobnet request, search string, country filter, place-only search

**Place**:
An optional Danish city or town the seeker wants jobs near, within the Commute distance. A query has at most one. The seeker chooses it from Copenhagen, Aarhus, Odense, Aalborg, or Randers, and each town has one center. With none chosen, the search covers all of Denmark (ADR-0041).
_Avoid_: Region, country, free-text city, postal code (when meaning what the seeker types), radius (when meaning a distance the seeker sets)

**Commute distance**:
The fixed distance around a Place that still counts as near that town. It is 50 km for every Place, and the seeker does not choose it (ADR-0041).
_Avoid_: Radius, search area, region
