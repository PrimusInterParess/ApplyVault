---
name: Step 16 — EURES Cache Multi-Instance
overview: Replace in-memory EURES ranked-result cache with a distributed cache safe for multiple API replicas.
todos:
  - id: document-current-cache
    content: Map in-memory cache TTL and key structure in Eures job search services
    status: pending
  - id: choose-backend
    content: Select Redis, SQL cache table, or platform cache (Azure Cache for Redis)
    status: pending
  - id: idistributed-cache
    content: Refactor cache to IDistributedCache with same semantics (5-minute ranked set)
    status: pending
  - id: cache-key-design
    content: Namespace keys by search session/keywords/location; avoid cross-tenant leakage
    status: pending
  - id: fallback-single-instance
    content: Document deferral if running one replica (tracker allows defer)
    status: pending
  - id: load-test
    content: Verify two instances share cache for paging/load-more requests
    status: pending
isProject: false
---

# Step 16 — EURES Cache Multi-Instance

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-07-deployment-and-hosting.md](prod-07-deployment-and-hosting.md) · **Next:** [prod-17-gmail-sync-multi-instance.md](prod-17-gmail-sync-multi-instance.md)

## Problem

EURES search ranks results and caches the ranked list **in memory** for ~5 minutes so paging and load-more reuse the same ordering ([`EuresJobClient`](../../api/ApplyVault.Api/Services/Eures/EuresJobClient.cs) / search service layer).

With **multiple API replicas**:

- User hits instance A for page 1, instance B for page 2 → inconsistent results or cache miss behavior
- Memory pressure duplicated per node

## Risk

| Risk | Impact |
|------|--------|
| Split cache | Broken pagination / duplicate jobs in UI |
| Stale cache | Outdated listings shown |
| Defer on single node | None until scale-out |

## Goal

All API instances read/write the same distributed cache for a given search session key.

## Defer if

Running **one API replica** only — document in deploy runbook and skip implementation until scale-out.

## Implementation tasks

### 1. Inventory current cache

Locate `IMemoryCache` usage in EURES services under [`Services/Eures/`](../../api/ApplyVault.Api/Services/Eures/).

### 2. Introduce IDistributedCache

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});
```

Or SQL-backed distributed cache if Redis unavailable.

### 3. Serialization

Cache ranked job id list + metadata as JSON with explicit TTL (5 minutes).

### 4. Key format

Example: `eures:ranked:{userId}:{sessionId}` or `{keywordHash}:{location}:{pageSession}` — ensure authenticated searches cannot leak across users if keys include user scope where needed.

### 5. Config

Add `ConnectionStrings:Redis` or `EuresIntegration:DistributedCache` section (step 4).

## Verification

1. Two local API instances pointed at same Redis → load-more consistent across instances.
2. TTL expiry refreshes ranking.
3. Single-instance mode still works with in-memory fallback (optional feature flag).

## Production-grade notes

- Required before horizontal autoscaling EURES-heavy workloads.
- EURES external API rate limits still apply — distributed cache does not replace step 14.
