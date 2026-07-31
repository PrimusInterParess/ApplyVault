# PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION

**Status:** APPROVED

## 1. Executive Summary

Fictional greenfield **Sample Catalog API** for listing and managing product catalog items. Demo-only; no production secrets or real providers.

## 2. Project Mode

GREENFIELD

## 3. Business Goals

- Expose CRUD-style catalog item APIs for a demo storefront.
- Keep the fleet provider-neutral and inspectable by The Architect validator.

## 4. Scope

- Included: catalog items (id, name, description, price), list/get/create/update
- Excluded: payments, identity provider, mobile clients, AI
- Deferred: search ranking, inventory sync

## 5. Recommended Agent Fleet

- principal-software-architect
- architecture-engineer
- backend-engineer

## 6. Excluded Agents

- payment-billing-engineer — payments out of scope
- ai-llm-engineer — AI out of scope
- frontend-engineer — API-only demo

## 7. Readiness

READY_WITH_DOCUMENTED_ASSUMPTIONS
