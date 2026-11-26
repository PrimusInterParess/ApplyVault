# ADR-0047: Guided discovery before writing each preparation document

Accepted 2026-11-26 after the user requested meaningful clarification questions per document and guidance when they do not know how to answer. Amends ADR-0046's single-call generation flow.

Create the pack outline first. Prepare 1–3 targeted questions before writing each document; questions may be supplied with the outline or collected when that document is opened. Questions gather missing context and learning needs; they are not interview questions or a scored assessment. Ask one at a time and support written answers, explicit uncertainty, skipping, and saving partial progress. Avoid asking for facts already present in the CV, job, projects, or saved answers elsewhere in the pack.

Each question includes practical help, an empty answer structure, and a clearly hypothetical example. The seeker can ask for more guidance in their own words. Help is read-only: generated examples, suggested wording, and unconfirmed drafts never enter the saved answer ledger automatically. Using a blank structure is an explicit user action; saving the edited answer is separate.

Distinguish uncertainty about knowledge from uncertainty about past experience. Knowledge uncertainty should lead to foundations, worked examples, and small practice tasks. Missing project evidence stays unknown or tentative and gets investigation guidance; it must not produce invented accomplishments. Skipping every question is valid and still permits a useful document based on the remaining sources.

Save the complete per-document question set, response statuses, and user text inside the existing versioned pack body. No SQL schema migration is needed. All writes use the expected pack revision and ownership checks. The answer ledger is shared across document generation. Later unanswered question sets can refresh from the current shared context; do not replace a user's draft or resolved responses merely because another document changed.

Writing or rewriting one document validates that replacement before saving and preserves other documents. Reset only that document's readiness and study-day completions that reference it. Saved answer changes advance a context revision: previously written documents show a review notice without automatic regeneration. Sources keep separate fingerprints so source changes remain visible even as individual documents are rewritten.

Existing packs remain readable. They can acquire questions one document at a time without discarding content or progress. Creating a new outline remains an explicit whole-pack replacement. Failed planning, help, or writing must preserve saved material. A write failure after a successful answer save keeps the saved answers available for retry.

Continue the explicit demo/live distinction and the accuracy boundaries from ADR-0046. The AI has no live browsing or code execution, and guidance must not claim verification or assessed mastery.

Live-response hardening: retain application validation, but constrain day/minute values and question kinds in a compact provider schema. Avoid large nested array length bounds that exceed Gemini's schema complexity limits. Give planning sufficient output budget; enlarge the budget on a token-limit retry and supply specific repair instructions for failed contract rules. Log only operation names, safe finish/failure codes, and token counts, never response/source text or credentials. Warn before navigation or reload cancels an active generation request.

Provider limits and schema choices were checked against [Gemini 2.5 Flash model limits](https://ai.google.dev/gemini-api/docs/models/gemini-2.5-flash) and [Google's structured-output documentation](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/capabilities/control-generated-output).

Independent recall, reflection, rehearsal, and rest/preparation activities may have an empty documentIds list. A study session must still have a concrete activity and stay within the time budget; every supplied document reference must exist, and every document must be scheduled somewhere. Requiring a reading link for every activity rejected otherwise valid live-model plans. The provider schema and application validation must agree on that distinction.

A draft outline may contain documents whose questions are not prepared yet. Opening an unwritten document prepares those questions on demand. The write transition still requires a non-empty question set with each response answered, unknown, or skipped. Rejecting the whole draft because a rehearsal/review document deferred its questions prevented otherwise useful plans from being saved.
