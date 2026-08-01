# ADR-0005: PDF import Structure is AI-first

## Status

Accepted (2026-08-01 — operate `cv-pdf-section-match-2026-08-01`, operator choice 2)

## Context

Earlier PDF import designs used **heuristic-first** structuring (catalog Sectionize → heuristic place → confidence-gated Gemini). SoftHeading, the confidence gate, and `ForceAi` often skipped AI when headings “looked fine” while typed sections were wrong or empty. Older design notes still describe that shape and conflict with the shipped pipeline.

ADR-0001 (catalog) and ADR-0002 (CV builder sole surface) remain in force.

## Decision

1. After extract succeeds and `GoogleAi:Enabled`, send **full ordered extracted text** to Gemini with the ADR-0001 catalog system prompt and response schema so the model **fills the Structured CV**. Do not send SoftHeading / pre-sectionized chunks as the primary AI payload.
2. **Heuristic + catalog-alias Sectionize** run only when AI is off or AI fails/returns empty sections — not first-then-maybe-AI.
3. Remove SoftHeading, the minimize-AI confidence gate, and `ForceAi` from the happy path.
4. **Extract bindings that stay in force:**
   - Digital text via PdfPig only — **OCR is out of default scope**; empty extract hard-fails (never invent body text).
   - **P1:** unmatched lines must persist as `Custom` (“Additional information”) — never silent drop.
   - **Link integrity:** URLs and emails remain atomic tokens through extract → structure → persist.
5. This **supersedes** heuristic-first Structure happy-path guidance in `design-cv-pdf-import-pipeline.md` and `design-cv-pdf-section-match.md`. Extract / P1 / link-integrity / Gemini-HTTP / catalog bindings from those notes remain.

## Consequences

- Import quality depends on Gemini when AI is enabled; offline/`GoogleAi:Enabled=false` uses the thinner heuristic fallback (may bury unknown mid-CV headings — P1 residual still required).
- Scan-only PDFs fail with a clear empty-extract notice until a separate OCR capability is explicitly approved.
- Design note: `agent-system/design-cv-pdf-ai-first-import.md`.
