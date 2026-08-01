# ADR-0006: CV export uses shared HTML → Puppeteer

## Status

Accepted (2026-07-31 — implementation-plan D2; operate `cv-builder-v1-m1-2026-07-31`)

## Context

v1 briefly mixed Template renderers: Classic historically used QuestPDF while preview fidelity needed a shared document. Dual pipelines made preview-vs-PDF drift hard to reason about and blocked a single “what you download” HTML source.

ADR-0003 decides the **edit desk** uses an Angular approximation with on-demand export HTML. This ADR records the **export pipeline** decision ADR-0003 cites as plan D2.

## Decision

1. Supported Templates (Classic / Modern / Minimal, ids 1–3) share **one HTML document builder**.
2. **Export-faithful preview** (`GET …/current/export/preview`) and **PDF download** both use that HTML; PDF is produced via **PuppeteerSharp** (hosted Chromium).
3. **QuestPDF is retired** from the supported v1 Template set — PDF export always goes through HTML.
4. Unknown / legacy Template ids fall back to Classic (1).

## Consequences

- Ops must run Chromium for PDF; unit/integration suites may stub the PDF exporter.
- Edit-canvas visual drift vs PDF remains expected (ADR-0003); mitigate with Check export.
- Adding a Template means an HTML layout (+ CSS), not a parallel native PDF renderer.
- Plan: `agent-system/implementation-plan.md` (D2).
