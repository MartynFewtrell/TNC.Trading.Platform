---
title: Account Details Daily Capture and History Validation
description: Validation of the account-details daily capture and historical cursor proposal against current workspace patterns.
author: GitHub Copilot
ms.date: 2026-08-07
ms.topic: research
keywords:
  - account details
  - daily capture
  - trading schedule
  - history cursor
estimated_reading_time: 4
---

## Research Scope

Validate the automatic daily account capture and historical cursor requirements in
`.copilot-tracking/research/2026-08-07/account-details-research.md` against the
current workspace. Locate the exact successful-logon completion and reconciliation
flow, trading schedule or time-zone calculation, and existing snapshot-history,
cursor, and daily-uniqueness practices. User requirements are authoritative.

## Status

Complete.

## Findings

### Trading Schedule and Time Zone

The configured schedule and time zone exist. `TradingScheduleConfiguration` has
start/end times, trading days, weekend behavior, bank-holiday exclusions, and a
time zone. Bootstrap configuration defaults that zone to UTC, and the schedule
gate converts UTC to the configured zone before calculating the local date.

* `src/TNC.Trading.Platform.Application/Configuration/TradingScheduleConfiguration.cs:3-9`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/PlatformConfigurationBootstrapParser.cs:21-39`
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:28-50`

The existing login-snapshot flows nevertheless calculate `TradingDay` from UTC,
not the configured zone.

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:638-661`
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:186-205`

**Correction:** retain the primary proposal's configured-time-zone requirement,
but add one shared Application operation such as
`TradingScheduleGate.GetTradingDay(TradingScheduleConfiguration, DateTimeOffset)`.
It must use the same time-zone resolution as `Evaluate`, returning the local
`DateOnly`. Persist this `TradingDay` on every account-details retrieval header.
Do not use UTC midnight or independently implement time-zone conversion.

### Successful Logon Completion

There are two implementations of successful authentication, comprising three
success paths:

* Scheduled reconciliation authenticates and persists a snapshot in
  `TransitionToActiveAsync`.
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:389-431`
* Reconciler-owned manual recovery authenticates and persists the same snapshot
  in `AttemptImmediateRecoveryAsync`.
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:218-275`
* The Operator manual-retry endpoint calls a separate
  `TriggerManualAuthRetryHandler`, which persists its successful runtime state,
  login snapshot, and proof as a committed intent. It bypasses the reconciler.
  `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:40-41`
  `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:82-130`
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/ManualAuthRetryCommitter.cs:16-58`

Within the reconciler, the two branches converge at
`CaptureSuccessfulLoginSnapshotAsync`.

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:638-680`

**Correction:** do not add daily capture only to the reconciler or only to its
common snapshot method. That would omit direct manual retry. Use a shared,
best-effort post-success collaborator, or invoke the same daily-capture operation
after durable success in every path: after reconciler state persistence and after
the direct manual-retry commit. Capture failure must be recorded and contained so
it cannot change a successful logon outcome.

The current `sp_getapplock` guards reconciliation only. A dedicated
account-details lease remains necessary because direct manual retry and manual
account refresh do not rely on that reconciler lease.

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlPlatformReconciliationLease.cs:8-42`

### Daily Snapshot Uniqueness

The login snapshot store is the correct behavioral precedent: it retains a first
successful snapshot by broker environment and trading day while maintaining a
latest row.

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:13-47`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:62-72`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfPlatformIgLoginSnapshotStoreTests.cs:14-39`

It is not a database uniqueness precedent: the store reads then inserts, and the
model/migration define a non-unique index only.

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:73-81`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260727202238_InitialPlatformSchema.cs:202-205`

**Correction:** do not copy this read-then-insert approach. For account details,
use a dedicated lease plus a filtered SQL Server unique index on
`(BrokerEnvironment, TradingDay)` where `TriggerSource = 'Automatic'`. An
equivalent nullable automatic-capture date key with a filtered unique index is
also valid. A regular unique index including `TriggerSource` would incorrectly
limit manual refreshes to one per day. Treat an automatic unique-key collision as
an already-captured success; the unique index is the final cross-replica guarantee.

### Historical Cursor Strategy

No cursor-history pattern exists. The closest feature reads every retained login
snapshot, maps one list response, and renders every entry on one page.

* `src/TNC.Trading.Platform.Application/Features/GetIgLoginHistory/GetIgLoginHistoryHandler.cs:6-18`
* `src/TNC.Trading.Platform.Api/Features/GetIgLoginHistory/GetIgLoginHistoryMapping.cs:5-18`
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/IgLoginHistory.razor:29-82`

**Correction:** replace the primary proposal's raw
`PreviousRetrievedAtUtc`/`NewerRetrievedAtUtc` fields with opaque keyset cursors
containing `(RetrievedAtUtc, AccountDetailsRetrievalId)`. Order deterministically
by both fields, and return `OlderCursor` and `NewerCursor`. Timestamp-only cursors
can skip or repeat records when manual refreshes have equal timestamps. The older
query selects strictly below the pair; the newer query selects strictly above it.

The primary document selects historical browsing, then later says it is deferred.
The user requirement is authoritative: implement initial cursor navigation now;
only bulk-list and page-number history views may be deferred.

## Required Proposal Amendments

* Calculate persisted retrieval `TradingDay` through a shared configured-zone
  method.
* Run best-effort daily capture after durable successful authentication in both
  reconciler and direct-manual-retry flows.
* Use a dedicated account-details lease and a filtered SQL unique index for the
  automatic daily capture.
* Use opaque composite keyset cursors, not timestamp-only next/previous values.
* Keep historical cursor navigation in the initial delivery.

## Follow-On Questions

None. The authoritative user requirement resolves the only product ambiguity:
historical browsing is in scope for the initial delivery.