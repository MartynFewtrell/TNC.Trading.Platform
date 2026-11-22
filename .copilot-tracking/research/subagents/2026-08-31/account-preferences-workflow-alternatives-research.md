---
title: Account preferences workflow alternatives research
description: Repository research for a database-first account-preferences display and IG verification workflow
ms.date: 2026-08-31
ms.topic: concept
---

## Status

Complete.

## Research questions

* Which architecture conventions govern a database-first account-preferences display and IG verification workflow?
* How do neighboring implementations distinguish persisted desired state from observed external state?
* What conventions exist for reconciliation, warning and status presentation, startup gates, audit history, and retry behavior?
* Which implementation approaches are viable, what trade-offs do they carry, and which approach is the preliminary recommendation?

## Scope and method

Research is limited to applicable repository instructions and the nearest production and test implementations. Workspace evidence uses exact repository-relative paths and 1-based line ranges.

The research reflects the working tree as inspected on 2026-08-31. Pre-existing uncommitted changes were present in the authentication reconciler, Account Preferences UI, and related tests; this research did not modify product or test code.

## Executive findings

* The current Account Preferences read is provider-first, not database-first. Every current-value request calls IG and appends a successful observation. Existing observations are explicitly historical evidence and are not a cache or fallback.
* The current update is safe against ambiguous writes: it sends one `PUT`, then performs an authoritative `GET`; it never blindly retries an indeterminate `PUT`. The requested value exists only in request memory and is not durable desired state.
* Account Details and Platform Status provide the closest database-first precedents. Their queries read persisted projections only, while separate commands or supervisors own external calls and writes.
* A database-first redesign changes the documented authority contract. It needs explicit desired, observed, reconciliation, and freshness semantics in SQL and in API/UI contracts.
* The preliminary recommendation is a dedicated Account Preferences reconciliation slice with a current-state row plus append-only history. Reads remain SQL-only; update commands commit desired state and audit atomically, then trigger best-effort reconciliation. A background worker retries due work without blocking application readiness.

## Applicable repository conventions

### Architecture

* Use-case code must remain independent of transport, UI, persistence, provider, and hosting representations. Outbound dependencies are inward-owned ports implemented by adapters: `.github/instructions/architecture.instructions.md:59-79`.
* Product operations belong in feature folders with one request, response, and handler per operation. Handlers own business decisions while endpoints remain thin: `.github/instructions/architecture.instructions.md:82-108`.
* The likely operations are therefore separate `GetAccountPreferences`, `UpdateAccountPreferences`, and `ReconcileAccountPreferences` slices. A hosted service may schedule reconciliation, but must not decide desired-versus-observed policy.

### Configuration and audit

* Operator-managed settings belong in SQL Server and must be written through explicit application services or repositories. Audit-friendly flows are preferred: `.github/instructions/configuration.instructions.md:23-36`.
* The existing configuration flow validates, commits, then reconciles: `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:9-17`. Tests require the commit to precede reconciliation and require no reconciliation after a failed commit: `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/UpdatePlatformConfigurationHandlerTests.cs:25-48` and `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/UpdatePlatformConfigurationHandlerTests.cs:104-123`.
* Configuration persistence adds an actor-aware, redacted audit row before the same `SaveChangesAsync` call as the configuration mutation: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:53-119`. This is the repository precedent for atomically recording desired-state changes and audit history.

### UI

* Radzen is the preferred component library; forms should use typed Blazor validation, preserve explicit labels, focus order, and visible status text, and receive behavior tests: `.github/instructions/radzen-blazor.instructions.md:35-56`.
* Existing Account Preferences UI distinguishes editable selection from the last confirmed value and exposes a pending cue: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:18-30` and `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:5-25`.
* Failures use `role="alert"`; successful, dismissible confirmation uses `role="status"` with `aria-atomic="true"`: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-16` and `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:32-38`.
* Platform Status already presents explicit operational vocabulary and retry context. It exposes blocked reason, retry phase, attempt, next retry, and a blocking warning: `src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor:74-105`. Its presenter normalizes backend states to `Active`, `Retrying`, `Blocked`, `Failed`, and `Out of schedule`: `src/TNC.Trading.Platform.Web/Components/Pages/Status/StatusPagePresenter.cs:32-78`.

### Tests

* Tests are unit-first, use xUnit, require descriptive method names and requirement-focused comments, and reserve integration/E2E tests for real boundaries: `.github/instructions/tests.instructions.md:29-59`.
* UI functional tests should use Playwright web-first assertions and accessibility-first locators without hard-coded waits: `.github/instructions/playwright.instructions.md:22-51`.
* Aspire tests are closed-box, exercise HTTP or exposed ports, clean up resources, and avoid hard-coded waits: `.github/instructions/aspire-tests.instructions.md:22-50`.

## Neighboring implementation evidence

### Current Account Preferences behavior

* `GetAccountPreferencesHandler` checks the Test/Demo environment policy, calls `gateway.GetAsync`, appends `ReadObserved` only on success, and writes bounded warning events for failed or indeterminate reads: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:13-35` and `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:38-51`.
* `UpdateAccountPreferencesHandler` sends one provider update, records indeterminate status, then always reads IG for confirmation. Matching reads become `UpdateConfirmed` or `ReconciliationObserved`; mismatches become `NotApplied`; failed confirmation produces no observation: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:13-60`.
* The IG adapter creates a fresh session for each operation. It retries a `GET` once after `401`, but does not replay a `PUT`: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-48`. It maps timeouts and transport failures on writes to `Indeterminate`, and requires a strict Boolean read and `SUCCESS` write acknowledgement: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:29-48` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:65-91`.
* The formal runtime contract says IG is authoritative, observations must never serve as a cache or fallback, updates are `PUT -> GET`, and unknown values are not stored as false: `docs/wiki/runtime-behavior.md:728-742`. The API reference likewise says the current `GET` performs a live provider read: `docs/wiki/api-reference.md:551-579`. Database-first display therefore requires an approved documentation and API contract change.

### Persisted observed state and history

* The observation record stores value, observed and recorded timestamps, platform and broker environments, kind, source, actor, and correlation ID: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/TrailingStopsPreferenceObservation.cs:5-15`.
* SQL persistence is append-only and history reads are partitioned by platform and broker, ordered by observed time and ID, and keyset-paged: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:8-59`.
* The EF entity contains a nullable `AccountId`, but the application observation and store mapping do not populate or return it: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/TrailingStopsPreferenceObservationEntity.cs:3-27` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:10-23`. Account identity and partitioning therefore remain unresolved for an account-specific desired state.
* Operational retention deletes observations by age: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:51-54` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:83-97`. Durable desired state must not live only in this retained history.

### Database-first read precedent

* `GetAccountDetailsHandler` reads only the snapshot store and returns persisted latest or historical snapshots: `src/TNC.Trading.Platform.Application/Features/AccountDetails/GetAccountDetailsHandler.cs:6-39`.
* `RefreshAccountDetailsHandler` separately owns provider I/O and persistence. It coalesces in-process requests, acquires a cross-replica lease, deduplicates successful automatic daily capture, and returns typed `Deferred` or `RefreshInProgress` outcomes: `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:8-76`.
* Status follows the same query-purity rule. The handler delegates only to a projection reader: `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs:5-12`. The EF reader returns missing state explicitly and composes status from persisted runtime state and snapshots, including `LastValidatedAtUtc`: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformStatusProjectionReader.cs:14-68`.

### Reconciliation, startup gates, and concurrency

* The authentication reconciliation command serializes writers in process, while the SQL adapter uses an exclusive `sp_getapplock` to prevent concurrent replica work: `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandler.cs:5-27` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlPlatformReconciliationLease.cs:8-45`.
* API startup initializes persistence and synchronously runs authentication reconciliation before `RunAsync`: `src/TNC.Trading.Platform.Api/Program.cs:26-40` and `src/TNC.Trading.Platform.Api/Program.cs:84-84`.
* The hosted supervisor also completes an initial tick in `StartAsync`, then runs every second and logs transient failures without stopping later ticks: `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:32-57` and `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:68-87`.
* Account Details is captured as best-effort work only after authentication succeeds; its failure does not invalidate durable login success: `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:264-278`. This is the closest precedent for preference verification that should depend on IG availability without redefining auth health.

### Retry and failure semantics

* Authentication retry delays use bounded exponential growth: `src/TNC.Trading.Platform.Application/Services/RetryTimingPolicy.cs:5-27`.
* The auth state engine waits until `NextRetryAtUtc`, detects expired sessions, and otherwise chooses active or degraded transitions from credential readiness: `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:29-58`.
* Missing credentials generate one degraded warning without pretending a retry is scheduled: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:111-157`. Timeouts schedule retry while preserving a safe failure summary: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:647-687`.
* The account-preference gateway tests verify one read replay after unauthorized, no replay for rate limiting, no write replay after unauthorized, and indeterminate classification for malformed acknowledgements: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:35-83`.

### Audit and operator history

* Preference history already records repeated equal successful operations, preserving an audit-like evidence trail instead of deduplicating them: `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:81-101`.
* Failures are intentionally separate bounded operational events and do not create false observations: `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:61-79` and `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:103-120`.
* The current page labels the audit surface `Observed history` and renders recorded time, value, kind, and environment: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:41-43`.

## Test conventions and evidence

The minimum test strategy for any implementation should preserve these repository-proven boundaries:

* Application unit tests for state comparison, read-before-write reconciliation, mismatch, indeterminate write, backoff, startup recovery, cancellation, environment gating, and commit-before-reconcile ordering. Existing account-preference tests demonstrate authoritative readback and no blind retry: `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:34-79`.
* Infrastructure unit tests for exact IG headers, strict payload parsing, sanitized failures, and asymmetric retry: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:10-83`.
* SQL integration tests for migration shape, current-state upsert/concurrency, durable restart readback, partition isolation, deterministic history, and retention. Existing coverage proves restart durability and deterministic equal-timestamp paging: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:45-105`.
* bUnit tests for desired/observed/drifted/verification-failed rendering, disabled save state, alerts, status announcements, cancellation, and stale history refresh. Existing tests guard against presenting an unconfirmed default as authoritative and verify pending-state semantics: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:17-77`.
* A small closed-box Aspire suite should prove restart recovery, SQL-only `GET`, durable pending work, and eventual convergence without contacting real IG. Real IG remains operator-controlled evidence, matching `docs/wiki/testing-and-quality.md:553-574`.

## Implementation alternatives

### Alternative 1: Dedicated current-state aggregate and reconciler

Add a durable Account Preferences current-state record keyed by the approved account/environment identity. Store desired value and revision separately from nullable observed value, observation timestamp, reconciliation status, attempt metadata, next retry, safe failure summary, actor, and correlation ID. Keep append-only observations for verification history.

The update command validates and atomically persists desired state plus an audit/history record, then requests reconciliation. The reconciler begins each attempt with `GET`; if observed equals desired it marks `InSync`, otherwise it sends one `PUT` and confirms with `GET`. After an indeterminate `PUT`, later retries begin with `GET`, never a blind repeated write. The query reads only SQL and exposes freshness and status.

Benefits:

* Cleanest desired-versus-observed model and strongest operator clarity
* SQL-only, deterministic reads with durable pending work across restarts
* Supports optimistic concurrency and replica-safe leasing without coupling to auth runtime state
* Preserves existing observation history and provider safety rules

Costs and risks:

* Requires a new current-state entity, migration, ports, reconciliation slice, scheduler, and expanded API/UI contracts
* Requires product decisions for account identity, stale thresholds, retry exhaustion, and whether drift should automatically reapply desired state

### Alternative 2: Event-log-derived state

Extend `TrailingStopsPreferenceObservations` with desired-change, attempt, observed, matched, and failure event kinds. Derive current desired and observed values from the latest events per partition, optionally backed by a SQL projection.

Benefits:

* Preserves a complete append-only chronology and builds on the existing history concept
* Can minimize new persistence types if the current table is generalized carefully
* Natural correlation across request, verification, and eventual outcome

Costs and risks:

* Current-state reads become more complex and vulnerable to ordering or projection errors
* Existing retention can erase the only durable desired state, so retention rules must change or a materialized current projection must be added
* The current table name and Boolean-only schema do not model failed attempts or nullable observed state well
* Concurrent desired updates require revision ordering and stale-worker protection, reducing the apparent simplicity

### Alternative 3: Extend platform configuration and auth reconciliation

Add desired trailing-stops state to `PlatformConfiguration`, store observed status in auth runtime state or a related projection, and invoke preference reconciliation from the existing authentication reconciler after a healthy login.

Benefits:

* Reuses SQL configuration, audit, UI update, startup, retry timing, and reconciliation infrastructure
* Naturally waits for valid credentials and an active IG session
* Has the smallest number of new orchestration components

Costs and risks:

* Couples account-specific broker state to global platform configuration and auth health
* Extends the already central authentication reconciler with an unrelated operational responsibility
* A one-second auth tick and blocking startup path may create unnecessary IG preference calls or make readiness depend on a secondary feature
* Existing configuration restart semantics and audit summaries are too coarse for independent account preference convergence
* Shared retry status would blur whether authentication or preference verification failed

### Alternative 4: Persist-first command with synchronous verification

Persist desired state and audit first, perform the existing `PUT -> GET` sequence within the request, store observed status, and let page reads use SQL. Failed verification leaves durable pending state for a later manual or scheduled retry.

Benefits:

* Smallest behavioral move from the current handler
* Immediate feedback often remains available while page reads become database-first
* Durable desired state survives request failure

Costs and risks:

* Operator requests still wait on session creation and multiple IG calls
* Cancellation after the database commit can look like failure even though desired state was accepted
* Requires the same background retry mechanism as Alternative 1 for reliable eventual convergence
* A synchronous fast path and asynchronous retry path can duplicate orchestration unless both delegate to one reconciler

## Preliminary recommendation

Use Alternative 1, with Alternative 4 only as an optional immediate trigger that delegates to the same reconciler.

Recommended shape:

1. Persist a current-state aggregate distinct from retained history. Use a revision or concurrency token so an older reconciliation result cannot overwrite a newer desired value.
2. Keep desired and observed values explicit. A suitable status vocabulary is `PendingVerification`, `InSync`, `Drifted`, `VerificationFailed`, `Blocked`, and `Unsupported`; observed value must remain nullable until verified.
3. Make `GET /api/platform/account-preferences` a pure SQL projection with desired value, observed value, status, desired-change metadata, last verification time, next retry, and safe failure summary. Do not call IG or append history during reads.
4. Make `PUT /api/platform/account-preferences` atomically commit desired state and an actor/correlation audit record before any provider work. Return the persisted state, preferably with `202 Accepted` while verification is pending unless the API contract requires waiting.
5. Implement `ReconcileAccountPreferences` as the sole external-effect workflow. Begin every attempt with an authoritative `GET`; issue `PUT` only for confirmed drift; confirm writes with another `GET`; after indeterminate writes, retry by reading first.
6. Use feature-local in-process coalescing and a SQL lease keyed by broker environment plus account identity. Reuse the bounded exponential timing calculation if policy is shared, but keep preference retry metadata and status separate from authentication retry state.
7. Trigger reconciliation after a desired-state commit, after successful IG authentication, and from a background due-work scan. Treat it as best effort relative to auth startup, following Account Details, so failed preference verification does not make API readiness unhealthy.
8. Present desired and observed values side by side with freshness. Show a warning only for drift, blocked, stale, or failed verification. Use a non-urgent status announcement for accepted/pending verification. Preserve the existing observed-history table and add desired-change/reconciliation events or a dedicated audit stream.

This design best matches the repository’s pure-query direction, database durability requirements, external-write safety, operator-status conventions, and clean feature boundaries. It also avoids the central risk in Alternative 3: turning a secondary account setting into part of authentication availability.

## Gaps and clarifying questions

* Account identity is unresolved. The IG endpoint operates on the session’s current account; the EF observation entity has `AccountId`, but the domain model and mapper omit it. The product must decide whether desired state is per account, per broker environment, or tied to the current preferred account.
* Drift policy is unresolved. Confirm whether observed mismatch should automatically cause a `PUT`, merely warn, or require operator confirmation after external changes.
* Startup semantics are unresolved. The recommendation assumes preference verification is non-blocking after SQL initialization and auth readiness; a requirement for verified preference before readiness would materially change the design.
* Retry limits and manual retry eligibility are unresolved for preferences. Authentication retry configuration may be reusable as timing policy, but its phases and operator messaging should not be reused without an explicit product decision.
* API compatibility is unresolved. The existing `GET` returns one confirmed Boolean and the existing `PUT` returns confirmed provider state. A database-first contract needs either versioning or an agreed response expansion and status-code change.
* History terminology is unresolved. Existing rows are observations, while desired changes and failed verification attempts need distinct audit semantics and potentially different retention.

## Recommended next research

* Confirm the IG account-selection contract and whether `AccountId` can be obtained and persisted safely for every preferences call.
* Confirm product semantics for drift, stale data, verification timeout, retry exhaustion, manual retry, and startup readiness.
* Inspect client consumers outside this repository before changing the current account-preferences response or `PUT` success semantics.
* Prototype the SQL current-state key and optimistic-concurrency behavior against SQL Server, including stale reconciliation completion after a newer desired update.
* Verify whether the existing IG authentication gateway can provide reusable session context. The current account-preferences adapter creates its own session per call, which may be undesirable for a periodic verifier.
* Define requirement identifiers before adding functional tests, then update the affected `docs/wiki/` pages because they currently declare IG live authority and live reads.
