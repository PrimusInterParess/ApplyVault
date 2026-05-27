---
name: Step 17 — Gmail Sync Multi-Instance
overview: Ensure only one API instance polls Gmail per connected mailbox when running multiple replicas.
todos:
  - id: document-current-sync
    content: Review GmailMailSyncBackgroundService poll loop and scope creation
    status: pending
  - id: leader-election
    content: Choose strategy (distributed lock, hosted service on single worker role, queue consumer)
    status: pending
  - id: implement-lock
    content: Acquire lock per poll cycle before MailSyncProcessor runs (Redis Redlock or SQL app lock)
    status: pending
  - id: lock-ttl
    content: Set lock TTL greater than poll interval; release on completion
    status: pending
  - id: defer-single-instance
    content: Document deferral when MailIntegration enabled on single replica only
    status: pending
  - id: verify-no-duplicate
    content: Two instances running → only one processes same mailbox per interval
    status: pending
isProject: false
---

# Step 17 — Gmail Sync Multi-Instance

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-07-deployment-and-hosting.md](prod-07-deployment-and-hosting.md)

## Problem

[`GmailMailSyncBackgroundService`](../../api/ApplyVault.Api/Services/Mail/GmailMailSyncBackgroundService.cs) runs on every API instance when `MailIntegration:Enabled` is true (registered in [`ServiceCollectionExtensions`](../../api/ApplyVault.Api/Infrastructure/ServiceCollectionExtensions.cs)).

With **multiple replicas**, each instance polls Gmail independently → duplicate processing, duplicate job updates, API quota waste.

## Risk

| Risk | Impact |
|------|--------|
| Duplicate polls | Duplicate interview/rejection updates |
| Gmail quota exhaustion | Sync failures for all users |
| Race on same scrape result | Inconsistent status metadata |

## Goal

Exactly **one** active poller per deployment (or per mailbox) per poll interval.

## Defer if

- Single API replica, or
- `MailIntegration:Enabled` false in production

Document in runbook.

## Implementation options

| Option | Pros | Cons |
|--------|------|------|
| **Dedicated worker** | Simple mental model | Extra deploy unit |
| **Redis distributed lock** | Works with scaled API | Requires Redis (synergy with step 16) |
| **SQL sp_getapplock** | No new infra | DB dependency |
| **Leader-only replica** | No code change | Manual ops / k8s singleton |

## Implementation tasks

### 1. Recommended: distributed lock around sync

In `GmailMailSyncBackgroundService.ExecuteAsync`:

```csharp
await using var handle = await lockProvider.TryAcquireAsync("gmail-sync", ttl, ct);
if (handle is null) return; // another instance owns this cycle
await RunSyncAsync(ct);
```

### 2. Lock TTL

Set TTL > `PollIntervalSeconds` from [`MailIntegrationOptions`](../../api/ApplyVault.Api/Options/MailIntegrationOptions.cs).

### 3. Alternative: worker service

Extract mail sync to a separate console/worker deployment; API instances disable `AddHostedService<GmailMailSyncBackgroundService>`.

### 4. Integration tests

Background service already disabled in test host; add unit test for lock skip path.

## Verification

1. Run two API instances with mail enabled and shared Redis lock.
2. Logs show only one instance per interval executes sync.
3. Job status updates happen once per email.

## Production-grade notes

- Step 4 may gate mail sync to a `MailIntegration:RunBackgroundSync` flag for worker-only deployments.
- Combine with step 13 logging to trace which instance held the lock.
