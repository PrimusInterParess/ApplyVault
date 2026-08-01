# ADR-0009: Supabase Auth with local AppUser authorization

## Status

Accepted (hosted auth plan; production auth adoption)

## Context

The product needed hosted login without running an identity server. Auth0, Entra External ID, Firebase, and self-hosted Keycloak were considered; calendar Google/Microsoft OAuth must stay separate from app login.

## Decision

1. **Supabase Auth** is the identity provider for signup/signin/session.
2. The Angular app and browser extension attach Supabase **Bearer JWTs**; the ASP.NET Core API validates them (issuer, audience, expiry, JWKS/signature).
3. Application data and authorization stay in **SQL Server** via a local **`AppUser`** (and related ownership) — Supabase proves identity; the API decides access.
4. Google/Microsoft **calendar** (and similar) connections remain **linked accounts**, not a replacement IdP.
5. Do not replace Supabase or move business data into Supabase without an explicit superseding decision.

## Consequences

- Tenancy and CV/job ownership keys are ApplyVault `AppUser` ids, not raw Supabase subjects alone.
- IdP swaps are high-cost; agents treat identity-provider change as out of scope by default.
- Plan: `plans/hosted_auth_plan.md`.
