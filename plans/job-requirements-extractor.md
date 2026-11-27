# Job requirements extractor — design discussion

Status: Design confirmed; specification published as
[GitHub issue #87](https://github.com/PrimusInterParess/ApplyVault/issues/87)
with the `ready-for-agent` label.

Implementation and validation are recorded in
[the validation notes](job-requirements-extractor-validation.md).

## Agreed purpose

Extract explicit facts from a saved job posting as Job requirements for future
interview preparation. Preserve required versus preferred qualifications and leave
unstated information unknown. Predicted interview topics and assessment against
the seeker's Structured CV belong to future preparation, outside this extractor.
The extracted requirements are for internal use only, with no requirements display
or editing surface on the saved-job detail page. Supporting quotes are retained
internally for grounding future preparation.

## Agreed first-version scope

- Responsibilities and expected outcomes
- Required and preferred skills, tools, and knowledge
- Experience and seniority
- Education, certifications, and languages
- Team, product, or industry context
- Interview process, if stated

## Agreed evidence rules

Every extracted fact includes a short supporting quote from the saved posting.
Qualifications retain an explicitly stated required or preferred level; when the
posting does not distinguish the level, it remains unspecified.

## Agreed execution

Extraction runs automatically after a job is saved, through a background worker.
Changing the saved description automatically triggers extraction again. Extraction
uses the latest saved description; a worker result is discarded if its source
description changed while processing.

Existing saved jobs are also extracted automatically when the feature launches,
processed gradually by the background worker. Jobs without a usable description
are marked internally as needing a description; adding a description triggers
extraction. Processing states do not introduce a user-facing extraction interface.

Extraction failures trigger limited automatic retries, then retain an internal
failed state. Saving the job succeeds independently of extraction.

When a description changes, its previous extraction becomes unavailable to future
interview preparation immediately. Only a successful extraction from the current
description can be consumed; failure does not make the previous extraction valid
again.

## Existing boundaries

ADR-0049 retired the previous interview preparation feature. This extractor is
input for a future design; the retired preparation designs are not requirements
for this work.
