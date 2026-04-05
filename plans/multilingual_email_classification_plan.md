---
name: Multilingual Email Classification Plan
overview: Improve recruiter email classification beyond English and Danish with a purely rule-based, language-aware design that minimizes regression risk through phased rollout, parity checks, locale-aware schedule extraction, and corpus-based tests.
todos: []
isProject: false
---

# Multilingual Email Classification Plan

## Recommendation

Keep the current rule-based design, but make it language-aware instead of continuing to grow one shared pool of phrases.

This is the best fit for the current codebase because it:

- keeps classification deterministic and explainable
- avoids jumping straight to ML or translation services
- works well with the existing `EmailJobStatusClassifier` structure
- makes it easier to add new languages one at a time
- supports better debugging and safer testing

This plan intentionally does not use:

- LLM-based classification
- hosted AI classification APIs
- translation-first classification pipelines

The goal is to keep the behavior deterministic, testable, and easy to reason about.

## Change Size And Regression Risk

This is not a tiny change.

If limited to:

- localized rule definitions
- global vs localized rule separation
- parity tests for English and Danish

then this is a medium refactor with relatively low regression risk.

If it also includes:

- language detection
- weighted scoring
- suppression logic
- locale-aware schedule extraction
- multilingual corpus infrastructure

then it becomes a large behavior-sensitive change with moderate regression risk if phased carefully, and high risk if done all at once.

Highest regression-risk areas:

- interview schedule extraction
- overly broad regex or pattern matching
- language detection routing a message to the wrong rule set
- changes to precedence between rejection, acknowledgement, and interview

Practical recommendation:

- start with a safe parity-preserving refactor
- prove no English/Danish regressions
- only then add new decision logic

## Current Limitations

The current classifier works for English and Danish, but it will get harder to maintain as more languages are added.

Main issues:

- all language rules are scored together
- matching is based on exact substring checks
- there is no language detection
- it is hard to tell which rule caused a classification
- schedule extraction is also only partially multilingual

## Target Outcome

The system should be able to:

- detect the likely language of each email
- score only the relevant localized rule set plus global rules
- classify rejection, acknowledgement, interview, or unknown with clearer reasoning
- extract interview schedules across more locales
- support new languages without changing classifier branching logic
- validate behavior with realistic multilingual test cases

## Proposed Architecture

### 1. Split rules into localized and global groups

Introduce a localized rule model so each language has its own rule set.

Suggested shape:

- `LanguageCode`
- `StrongRejectionPatterns`
- `SoftRejectionPatterns`
- `AcknowledgementPatterns`
- `ProcessDescriptionPatterns`
- `InterviewInvitationPatterns`
- `InterviewAvailabilityPatterns`
- `InterviewGeneralPatterns`

Keep language-neutral signals separate in a global rules area:

- meeting links
- calendar invite markers
- machine-generated event metadata

### 2. Add language detection before classification

Use the combined message text:

- subject
- snippet
- body

Return:

- primary detected language
- confidence
- optional fallback language

Classification should then evaluate:

- the detected language rule set
- global rules
- optionally English fallback rules if confidence is low

### 3. Move from exact phrase lists to weighted patterns

Replace raw substring-only matching with a mix of:

- exact phrases
- regex-based phrase families
- optional token-based weak signals

Each rule should carry:

- pattern
- category
- weight
- optional explanation key

This will make the classifier more resilient to:

- wording variation
- verb tense changes
- formal vs informal phrasing
- minor ATS template differences

### 4. Add suppression rules

Introduce rules that reduce false positives when signals conflict.

Examples:

- acknowledgement language should suppress weak interview-general matches
- strong rejection should override meeting-link noise
- process-description-only emails should not count as interview intent
- generic use of words like `interview` or `next step` should not be enough on their own

### 5. Make schedule extraction locale-aware

Refactor `InterviewScheduleExtractor` so locale-specific date and label handling is configurable.

Externalize:

- month names
- weekday names
- location prefixes
- timezone prefixes
- time separators and connectors

Keep support for global machine-generated formats such as:

- numeric dates
- Google Calendar / Meet patterns
- Teams / Zoom invite structures

## Implementation Phases

The phases below are intentionally split into low-risk and higher-risk work so rollout can stop after the safer steps if needed.

### Phase 1: Localize rule definitions without behavior change

- Replace the current anonymous `RuleSets` collection with explicit localized rule sets.
- Keep English and Danish as the first two migrated languages.
- Move meeting-link indicators into global rules.
- Keep classification outputs identical for the current supported languages.
- Do not change thresholds or precedence yet.

Deliverable:

- language-aware rule model in place without changing classification behavior yet

Regression profile:

- low risk if parity is enforced with tests

### Phase 2: Add parity and regression coverage first

- Expand direct classifier tests for current English and Danish behavior.
- Add parity-focused tests for rejection, acknowledgement, interview, and null outcomes.
- Add regression cases for recruiter templates, ATS messages, and meeting-link invites already known to work.
- Add tests that prove current schedule extraction still works for existing supported formats.

Deliverable:

- stronger safety net before classifier behavior changes

Regression profile:

- very low risk and high value

### Phase 3: Add language detection conservatively

- Introduce a lightweight `IMailLanguageDetector`.
- Detect language from normalized message content.
- Update the classifier to score only the detected language plus global rules.
- Add a safe fallback strategy for low-confidence detection.
- Fall back to the existing English/Danish path when confidence is weak.

Deliverable:

- localized scoring path with fallback behavior

Regression profile:

- low-to-moderate risk if fallback behavior is conservative

### Phase 4: Add weighted scoring

- Replace simple rule counts with weighted scores per category.
- Define category thresholds and a minimum margin between competing outcomes.
- Keep the final output categories unchanged.
- Introduce this only after parity and language routing are stable.

Deliverable:

- score-based classifier with more flexible multilingual behavior

Regression profile:

- moderate risk because this changes decision behavior

### Phase 5: Add suppression and precedence logic

- Model contradictory signals explicitly.
- Preserve the existing high-level ordering:
  - rejection wins when strong enough
  - acknowledgement should not mask interview intent
  - interview still requires schedule extraction
- Keep these precedence rules explicit and test-backed rather than implied by score alone.

Deliverable:

- fewer false positives in mixed-signal emails

Regression profile:

- moderate risk if suppression rules are too broad

### Phase 6: Refactor schedule extraction by locale

- Extract locale-specific date tokens into configuration objects.
- Add locale-aware support for labels such as `location` and `time zone`.
- Preserve support for existing English and Danish formats while preparing for German, French, Spanish, and others.
- Treat this as a separate high-attention track because interview creation is one of the most regression-sensitive paths.

Deliverable:

- locale-aware interview schedule extraction

Regression profile:

- moderate-to-high risk because it can silently affect interview creation

### Phase 7: Build multilingual corpus tests

- Add corpus-style tests grouped by language and expected outcome.
- Include real recruiter wording, ATS-generated templates, and meeting invites.
- Cover:
  - rejection
  - acknowledgement
  - interview
  - unknown
  - mixed-signal emails

Deliverable:

- reusable multilingual test corpus with expected outcomes

Regression profile:

- low risk and strongly recommended before adding each new language

### Phase 8: Add diagnostics

- Capture which language was detected.
- Capture which rules matched and how much score they contributed.
- Record whether suppression rules were applied.
- Surface schedule extraction success or failure.

Deliverable:

- easier debugging of false positives and false negatives

## Safe First Scope

If the goal is to improve the architecture without taking much regression risk, stop after these steps first:

1. Localize the rule definitions without behavior change.
2. Separate global vs localized rules.
3. Add stronger English/Danish parity tests.
4. Add regression tests for current schedule extraction behavior.

This gives a cleaner foundation while keeping risk relatively low.

## Practical Implementation Checklist

This section turns each phase into concrete work items with likely file touch-points.

The file list is intentionally pragmatic rather than exhaustive. It reflects the current mail-classification structure in the API and test project.

### Phase 1 Checklist: Localize rule definitions without behavior change

Goal:

- change the internal rule structure without changing current English and Danish outputs

Likely files to touch:

- `api/ApplyVault.Api/Services/Mail/EmailClassificationRules.cs`
- `api/ApplyVault.Api/Services/Mail/EmailJobStatusClassifier.cs`
- `api/ApplyVault.Api/Services/Mail/MailContracts.cs` if new small rule metadata types or interfaces are needed there
- `api/ApplyVault.Api.Tests/EmailJobStatusClassifierTests.cs`

Possible new files:

- `api/ApplyVault.Api/Services/Mail/LocalizedMailClassificationRules.cs`
- `api/ApplyVault.Api/Services/Mail/GlobalMailClassificationRules.cs`

Implementation tasks:

1. Introduce explicit localized rule containers for English and Danish.
2. Move meeting-link indicators into a global rule area.
3. Keep the current phrase sets exactly the same.
4. Keep the current branch logic and thresholds unchanged.
5. Update tests only to assert parity, not new behavior.

Definition of done:

- current tests still pass
- no change to current classification output for existing English and Danish fixtures
- rules are organized by locale and global/shared signals

Regression risk:

- low

### Phase 2 Checklist: Add parity and regression coverage first

Goal:

- build a strong safety net before any behavior-changing refactor

Likely files to touch:

- `api/ApplyVault.Api.Tests/EmailJobStatusClassifierTests.cs`
- `api/ApplyVault.Api.Tests/EmailDrivenJobUpdateServiceTests.cs`
- `api/ApplyVault.Api/Services/Mail/InterviewScheduleExtractor.cs` only if minor testability hooks are needed

Possible new files:

- `api/ApplyVault.Api.Tests/InterviewScheduleExtractorTests.cs`
- `api/ApplyVault.Api.Tests/MailClassificationParityTests.cs`

Implementation tasks:

1. Add explicit parity tests for current English and Danish behavior.
2. Add regression tests around interview extraction for already-supported formats.
3. Add tests for precedence:
   rejection over interview noise,
   acknowledgement not hiding real interview intent,
   interview still requiring a valid schedule.
4. Add more realistic recruiter and ATS message fixtures for supported languages.

Definition of done:

- current supported behavior is covered by direct unit tests
- existing supported invite formats are protected by tests
- the team can refactor internals with higher confidence

Regression risk:

- very low

### Phase 3 Checklist: Add language detection conservatively

Goal:

- route messages to the most relevant localized rule set while keeping safe fallback behavior

Likely files to touch:

- `api/ApplyVault.Api/Services/Mail/EmailJobStatusClassifier.cs`
- `api/ApplyVault.Api/Services/Mail/MailContracts.cs`
- `api/ApplyVault.Api/Program.cs`
- `api/ApplyVault.Api.Tests/EmailJobStatusClassifierTests.cs`

Possible new files:

- `api/ApplyVault.Api/Services/Mail/MailLanguageDetection.cs`
- `api/ApplyVault.Api/Services/Mail/SimpleMailLanguageDetector.cs`

Implementation tasks:

1. Introduce a small `IMailLanguageDetector` abstraction.
2. Detect language from subject, snippet, and body together.
3. Add a result model such as language code plus confidence.
4. Update the classifier to use:
   detected locale,
   global rules,
   fallback to current-safe behavior when confidence is low.
5. Register the detector in `Program.cs`.

Definition of done:

- language detection is wired in
- low-confidence messages still take a safe fallback path
- existing English and Danish scenarios still classify correctly

Regression risk:

- low to moderate

### Phase 4 Checklist: Add weighted scoring

Goal:

- replace raw match counts with scoring flexible enough for language variation

Likely files to touch:

- `api/ApplyVault.Api/Services/Mail/EmailJobStatusClassifier.cs`
- `api/ApplyVault.Api/Services/Mail/EmailClassificationRules.cs`
- `api/ApplyVault.Api/Services/Mail/MailContracts.cs`
- `api/ApplyVault.Api.Tests/EmailJobStatusClassifierTests.cs`

Possible new files:

- `api/ApplyVault.Api/Services/Mail/MailClassificationScoring.cs`
- `api/ApplyVault.Api/Services/Mail/MailClassificationPattern.cs`

Implementation tasks:

1. Introduce weighted rules per category.
2. Replace simple count aggregation with per-category scoring.
3. Keep the output model the same unless explanation/debug metadata is added.
4. Define conservative thresholds before enabling new languages.
5. Re-check all precedence tests after the scoring change.

Definition of done:

- weighted scoring works for current English/Danish behavior
- confidence thresholds are explicit and test-backed
- no large drift in already-supported scenarios

Regression risk:

- moderate

### Phase 5 Checklist: Add suppression and precedence logic

Goal:

- reduce false positives in mixed-signal emails

Likely files to touch:

- `api/ApplyVault.Api/Services/Mail/EmailJobStatusClassifier.cs`
- `api/ApplyVault.Api/Services/Mail/EmailClassificationRules.cs`
- `api/ApplyVault.Api.Tests/EmailJobStatusClassifierTests.cs`
- `api/ApplyVault.Api.Tests/EmailDrivenJobUpdateServiceTests.cs`

Possible new files:

- `api/ApplyVault.Api/Services/Mail/MailClassificationPrecedence.cs`

Implementation tasks:

1. Model explicit suppression rules for weak conflicting signals.
2. Make rejection precedence explicit.
3. Keep acknowledgement and interview conflict handling explicit and test-backed.
4. Avoid letting generic terms like `interview` or `next step` dominate on their own.

Definition of done:

- mixed-signal false positives are reduced
- precedence is encoded clearly, not only implied by score order
- risky scenarios have dedicated tests

Regression risk:

- moderate

### Phase 6 Checklist: Refactor schedule extraction by locale

Goal:

- make interview schedule extraction support more locales without breaking current formats

Likely files to touch:

- `api/ApplyVault.Api/Services/Mail/InterviewScheduleExtractor.cs`
- `api/ApplyVault.Api/Services/Mail/MailTextNormalizer.cs`
- `api/ApplyVault.Api/Services/Mail/MailContracts.cs` if locale-specific extractor contracts are introduced
- `api/ApplyVault.Api/Program.cs` only if extractor collaborators are added
- `api/ApplyVault.Api.Tests/EmailDrivenJobUpdateServiceTests.cs`

Possible new files:

- `api/ApplyVault.Api/Services/Mail/InterviewScheduleLocaleRules.cs`
- `api/ApplyVault.Api.Tests/InterviewScheduleExtractorTests.cs`

Implementation tasks:

1. Move month, weekday, label, and separator data into locale-aware structures.
2. Preserve current English and Danish parsing first.
3. Add tests around existing supported formats before adding new ones.
4. Expand locale handling one language at a time.
5. Keep numeric-date and platform-generated invite support global where possible.

Definition of done:

- current English and Danish interview extraction still works
- locale-specific parsing data is configurable
- at least one new locale can be added without rewriting extractor logic

Regression risk:

- moderate to high

### Phase 7 Checklist: Build multilingual corpus tests

Goal:

- validate the system against realistic messages rather than only idealized phrases

Likely files to touch:

- `api/ApplyVault.Api.Tests/EmailJobStatusClassifierTests.cs`
- `api/ApplyVault.Api.Tests/EmailDrivenJobUpdateServiceTests.cs`
- test data files if the corpus is stored outside code

Possible new files:

- `api/ApplyVault.Api.Tests/MailClassificationCorpusTests.cs`
- `api/ApplyVault.Api.Tests/TestData/MailClassification/...`

Implementation tasks:

1. Define a stable corpus format.
2. Group examples by language and expected outcome.
3. Include recruiter-written emails, ATS templates, and invite/platform messages.
4. Add edge cases such as mixed-language emails and low-confidence detection cases.

Definition of done:

- the corpus is easy to extend
- new languages can be validated with representative examples
- regressions can be caught without rewriting many individual unit tests

Regression risk:

- low

### Phase 8 Checklist: Add diagnostics

Goal:

- make future tuning and debugging practical

Likely files to touch:

- `api/ApplyVault.Api/Services/Mail/EmailJobStatusClassifier.cs`
- `api/ApplyVault.Api/Services/Mail/MailContracts.cs`
- `api/ApplyVault.Api.Tests/EmailJobStatusClassifierTests.cs`

Possible new files:

- `api/ApplyVault.Api/Services/Mail/MailClassificationDiagnostics.cs`

Implementation tasks:

1. Capture detected language and confidence.
2. Capture which rule groups matched.
3. Capture score contributions and suppression decisions.
4. Keep diagnostics internal or optional so public API contracts stay simple unless needed.

Definition of done:

- debugging a wrong classification no longer requires guesswork
- tests can assert on explanation details if desired

Regression risk:

- low if kept internal

## Higher-Risk Later Scope

These steps are useful, but should happen only after the safe first scope is stable:

1. Add language detection.
2. Introduce weighted pattern scoring.
3. Add suppression rules.
4. Refactor schedule extraction by locale.
5. Add one new language at a time.

## Recommended Rollout Order

1. Refactor rule definitions to be locale-aware.
2. Separate global vs localized rules.
3. Add parity and regression coverage for current English/Danish behavior.
4. Add lightweight language detection with conservative fallback.
5. Update the classifier to score only relevant locales.
6. Introduce weighted patterns.
7. Add suppression rules.
8. Refactor schedule extraction for locale support.
9. Add multilingual corpus tests.
10. Roll out one new language at a time.

## Language Expansion Strategy

Add languages incrementally based on expected incoming mail volume.

Suggested order:

1. German
2. French
3. Spanish
4. Dutch
5. Polish

For each new language:

- add localized classification rules
- add locale-specific schedule extraction tokens
- add at least a small real-world corpus
- validate classification precision before enabling broadly

Per-language practical checklist:

1. Add localized rule definitions.
2. Add schedule extraction locale tokens only if interview extraction is needed for that language.
3. Add a minimum regression corpus for:
   rejection,
   acknowledgement,
   interview,
   unknown.
4. Verify fallback behavior for mixed-language messages.
5. Do not enable the language broadly until false positives look acceptable.

## Testing Strategy

Extend the current unit test approach with two layers.

Layer 1:

- direct classifier unit tests for edge-case logic
- exact precedence and threshold checks

Layer 2:

- realistic corpus tests stored by language and intent
- anonymized recruiter and ATS message examples

The corpus should include:

- formal corporate rejection templates
- brief recruiter-written messages
- startup-style informal interview emails
- confirmation emails describing the recruitment process
- meeting-link-driven interview invites
- mixed-language and low-confidence messages

## Risks

- this is a medium-to-large change if all phases are completed
- regressions are likely if multiple behavior-changing phases ship together
- language detection may route some emails to the wrong rule set
- regex patterns can overmatch if too broad
- schedule extraction complexity can grow quickly across locales
- mixed-language emails will need fallback behavior
- existing English and Danish classifications may drift if parity is not locked down first
- interview creation can regress even if classification still appears correct

## Mitigations

- keep the first phase behavior-preserving
- add parity tests before decision-logic changes
- ship one riskier phase at a time
- use a fallback to English plus global rules on low-confidence detection
- prefer conservative thresholds for new languages
- keep unknown as a valid safe outcome
- require corpus coverage before adding a new language
- log or expose rule-match diagnostics to make tuning easier

## Success Criteria

The effort is successful if:

- new languages can be added without rewriting classifier flow
- false positives stay controlled
- interview classification remains gated by valid schedule extraction
- rule matches are explainable
- tests cover real multilingual recruiter messages instead of only ideal phrases

## Final Recommendation

Do not move to LLM-based classification for this feature.

The best next step for this codebase is:

- a safe first refactor that keeps current behavior stable
- stronger regression and parity tests for English and Danish
- language-aware rule sets
- conservative language detection with fallback
- weighted pattern scoring only after parity is proven
- locale-aware schedule extraction handled as a separate high-risk track
- corpus-based multilingual tests before each new language rollout

That path keeps the system rule-based, predictable, privacy-friendly, and maintainable while reducing the risk of breaking behavior that already works.
