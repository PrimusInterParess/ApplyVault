# ADR-0032: Gemini reads the PDF when text extraction is empty

## Status

Accepted (2026-10-18 — empty import after uploading a no-text PDF)

Supersedes [ADR-0005](0005-cv-pdf-import-ai-first-structure.md) decision 4 empty-extract hard fail **when `GoogleAi:Enabled`**. Local OCR stays out of scope. Empty extract still hard-fails when Gemini is off or the PDF parse returns no sections.

## Context

PdfPig only sees selectable text. Designed or Chrome-printed CVs, and the empty `CV.pdf` our own exporter writes for a blank canvas, extract as 0 words. The pipeline then threw before Gemini ran. The SPA still hydrated seven empty fixed Sections, so upload looked successful with nothing filled.

## Decision

1. If PdfPig quality is empty and `GoogleAi:Enabled`, send the original PDF bytes to Gemini (`inline_data` / `application/pdf`) with the same section response schema.
2. If that call fills at least one Section, persist via `FixedSectionLayout.Normalize`. Skip contact-source grounding (there is no extracted text to ground against).
3. If Gemini is off, the call fails, or the model returns no Sections, keep the empty-extract hard fail and show that notice on the builder instead of an empty canvas.

## Consequences

- Image-only or designed CVs can import when Gemini is on, without a local OCR engine.
- Re-uploading our empty export (`CV.pdf`, no content stream) still fails, now with a visible error.
- Gemini request size grows (base64 PDF); the existing 6 MB upload cap bounds it.
