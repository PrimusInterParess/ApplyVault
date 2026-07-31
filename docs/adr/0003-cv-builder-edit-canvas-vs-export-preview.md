# ADR-0003: CV builder edit canvas vs export-faithful preview

## Status

Accepted (2026-07-31 — Principal + user Option A)

## Context

M1 and later operate work treated **server export HTML** (`GET .../current/export/preview`, sandboxed iframe) as the **sole fidelity source for gallery, stage, and the edit desk canvas**. The Angular `cv-export-template-preview` (editable) lived in a **Content drawer**, so users edited one surface while the primary paper showed a saved-export preview.

That dual surface caused draft-vs-saved drift and conflicted with the product intent to edit on the main canvas. Implementation-plan **D2** (“single HTML source for preview + PDF”) correctly binds the **export pipeline**, but was over-applied to mean the edit desk must also be that HTML.

ADR-0002 remains: `/cv-builder` is the sole CV product surface (no `/my-cv` revival).

## Decision

1. **Edit desk primary canvas** is the **editable Angular template layout** (`app-cv-export-template-preview` with live Structured CV draft), not the sandboxed export HTML iframe.
2. **Export-faithful HTML preview** is **demoted** from the sole edit-canvas surface to **on-demand secondary “Check export”** (modal or panel) using the same preview endpoint. (Download-only was rejected; Option A.)
3. **Pick step** may continue to use server export HTML for gallery/stage when a Structured CV exists (template selection fidelity). Empty pick may keep illustrative Angular samples.
4. **PDF / formatted download** continues to use the **server HTML → Puppeteer** pipeline. That HTML remains the fidelity truth for export; the Angular edit canvas is an approximation for editing.
5. This **supersedes** the M1 operate “strategy A / sole fidelity canvas = server export HTML” decision **for the edit desk only**. It does **not** revoke single-HTML-source for preview-endpoint + PDF (plan D2 as export-pipeline scope).

## Consequences

- Content drawer as a second full template layout should be retired once the editable layout is on the desk.
- Expect visual drift between edit canvas and downloaded PDF; mitigate with Check export + clear copy.
- No required change to Structured CV REST contracts, ADR-0001 catalog, or ADR-0002 sole surface.
- FE owns the desk swap; BE contracts stay unless a later sample-preview or header/CORS task is separately approved.
