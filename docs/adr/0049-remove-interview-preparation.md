# ADR-0049: Remove interview preparation before redesign

Accepted 2026-11-27 at the user's request to remove the entire feature and start over.

Supersedes ADR-0021 through ADR-0027 and ADR-0046 through ADR-0048 for interview preparation. Those records describe retired designs, not requirements for a replacement.

Remove the preparation UI, routes, navigation, API, generation services, background worker, configuration, entities, and feature tests. Move the CV and saved-job context capture still needed by cover letters into the cover-letter module, with its own validation errors and rate limit.

Keep historical database migrations so existing installations can upgrade. A new migration removes the old preparation tables, including simulation sessions and saved packs; applying it deletes their data. It does not affect interview scheduling, job applications, CV documents, project summaries, or cover letters. No replacement interview preparation design is selected yet.
