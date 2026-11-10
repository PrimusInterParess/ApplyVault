# ADR-0030: PDF import is a strict-schema AI parser with a contact-only fallback

## Status

Accepted (2026-10-18 — human-approved plan "CV path restructure", Milestone 2)

Supersedes [ADR-0005](0005-cv-pdf-import-ai-first-structure.md) decisions 2 and 4-P1 (heuristic/catalog-alias Sectionize fallback; "Additional information" catch-all). ADR-0005 decision 1 (AI-first, full ordered text), the no-OCR default and link integrity remain in force. Empty-extract hard fail when Gemini is on is superseded by [ADR-0032](0032-cv-import-gemini-reads-empty-text-pdf.md).

## Context

The import pipeline grew to roughly 2.8k lines across thirteen classes (heuristic section parser, entry support, residual placement, normalizer, notices, coverage audit, link integrity, AI gate …) because it had to synthesise an arbitrary Section structure from any PDF and guarantee that every line landed somewhere. With a fixed seven-slot layout ([ADR-0029](0029-fixed-cv-section-layout-and-unplaced-content.md)) and an Unplaced content drawer, most of that machinery has no job left.

## Decision

1. **`ICvParser` seam.** Import = extract text → `ICvParser` → `FixedSectionLayout.Normalize` → persist Sections + Unplaced content + photo. Nothing downstream knows which parser ran.
2. **`AiCvParser`** (when `GoogleAi:Enabled`): one Gemini call with a strict response schema that has exactly the seven named slots (Entries per catalog fields) plus `unplaced: string[]`. The prompt is built from `fixedLayout`; the model is told to place what fits and return everything else verbatim in `unplaced`. A coverage check afterwards appends any extracted source line that appears in no slot to `unplaced`. Contact grounding (no invented channels) is kept as the only post-filter.
3. **`ContactOnlyCvParser`** (AI disabled or failed): regex contact extraction (name, email, phone, URLs) into the Contact slot; every remaining line becomes Unplaced content. No heuristic Section parsing.
4. **Retired:** `CvStructuredImportHeuristic`, `CvStructuredImportResidualPlacement`, `CvStructuredImportEntrySupport`, `CvStructuredImportNormalizer`, `CvStructuredImportSectionTypeMapping`, `CvStructuredImportAiGate`, `CvStructuredImportNotices`, `CvPdfSectionDetector`. Import notices reduce to two fixed messages ("imported with AI" / "AI unavailable — contact placed, the rest is in Unplaced").
5. Upload response summary gains `unplacedCount`; the SPA opens the Unplaced drawer when it is greater than zero.

## Consequences

- Imports are more honest and less clever: fewer guessed placements, more text in the drawer for the user to place by hand.
- With Gemini off, an imported CV is mostly empty plus a full drawer. This is accepted; the fallback exists to keep the product usable, not to compete with the AI parser.
- Import code shrinks by roughly two thirds and has two test surfaces (`AiCvParserTests`, `ContactOnlyCvParserTests`) instead of eight.
