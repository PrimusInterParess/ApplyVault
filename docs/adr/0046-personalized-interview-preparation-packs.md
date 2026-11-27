# ADR-0046: Personalized interview preparation documents replace simulation

Retired by [ADR-0049](0049-remove-interview-preparation.md). Kept as historical context only.

Amended by [ADR-0047](0047-guided-document-discovery.md) to plan first and clarify/write each document separately.

Accepted 2026-11-26 following a docs-based design discussion and the user's instruction to rework the whole feature and remove redundant code. Supersedes ADR-0021 through ADR-0027 for the active Interview Prep experience; existing ownership, CV context, and raw HTTP AI integration patterns continue.

Interview preparation becomes a collection of connected teaching documents personalized from a Structured CV, an optional saved job, explicitly selected owned project summaries, and user-provided information. A role and level are required without a posting. The generator applies across professions and projects, while each output reflects its user's context. The reference study pack informs teaching depth and navigation, not a fixed stack, employer, syllabus, or duration.

We replace the question/answer runtime rather than maintain a parallel simulation product. Retire session endpoints, planning, stages, question banks, scoring, coaching, retry/review operations, and their UI. Practical exercises and timed rehearsals remain documents, with solutions after briefs; they do not reintroduce an interviewer chat.

A pack includes an overview, evidence-aware topic map, prerequisite-ordered Markdown documents, explicit unknowns, and concrete study sessions bounded by available days and minutes. User-reported facts, supplied project facts, inferred relevance, and hypothetical examples must remain distinct. A CV skill mention cannot establish mastery. Track reading, explaining, demonstrating, and adapting separately, and persist a review log.

Retain the existing study-brief table and unique bindings: one pack per user/job and one general pack. Store the versioned pack, complete inputs, and progress together in its JSON body. Old brief rows remain visible with an explicit regeneration path. Keep historical simulation entities/mappings and migration history to preserve existing data without destructive table drops; they have no active runtime. There is no new SQL schema requirement. `UpdatedAt` is an optimistic concurrency token; callers submit the expected revision on regeneration and progress saves.

Generation captures sources and fingerprints CV content, selected project content, job content, and generation inputs. Changed or missing sources produce a notice, never automatic regeneration. Regeneration validates the replacement before saving and resets progress; failures preserve the previous pack. Progress changes do not change the source fingerprint. CV export visibility does not change candidate context.

Use a dedicated structured JSON AI operation with cancellation, a 180-second default per-attempt timeout, bounded retries, and application validation of document ids, links, prerequisite order, and study-time limits. Do not silently replace failures with demo content. The explicit local demo option is clearly labeled and does not pretend to have analyzed inputs. Generation has no live browsing or code execution tools: prompts require honest uncertainty, authoritative suggested references, and explicit verification steps for unexecuted examples. Live content quality still needs evaluation with representative roles and real provider output.
