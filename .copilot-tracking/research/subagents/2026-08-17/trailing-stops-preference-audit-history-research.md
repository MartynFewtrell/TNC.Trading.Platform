---
title: Trailing Stops Preference Audit History Research
description: Research of local SQL persistence conventions and a proposed history design for TrailingStopsEnabled observations.
ms.date: 2026-08-17
ms.topic: concept
---

## Research Scope

Investigate local SQL conventions for account history, configuration and audit events,
migrations and entities, database context, application event models, retention, and IG
update or reconciliation flows. Evaluate a SQL-backed retrospective history for every
observed or confirmed `TrailingStopsEnabled` state while IG remains authoritative.

## Status

Complete. No product implementation was modified. The current source tree has no
`TrailingStopsEnabled` implementation; the local occurrences are prior research
artifacts and this research record.

## Relevant Local Instructions

* `.github/instructions/architecture.instructions.md:42-52` requires inward-owned
	ports and persistence/provider translation in adapters. A preference-history feature
	should therefore own simple application records and ports, with EF Core and IG
	representations confined to Infrastructure.
* `.github/instructions/architecture.instructions.md:77-99` requires a cohesive
	`Features/<FeatureName>/` operation shape, explicit request/response contracts, thin
	endpoints, and direct handler tests.
* `.github/instructions/configuration.instructions.md:19-30` requires
	operator-managed configuration to reside in SQL Server and favors auditable update
	flows, while prohibiting secret disclosure.
* `.github/instructions/csharp.instructions.md:18-62` requires one top-level type per
	file, null-safe boundaries, intentional error handling, and tests for behavior
	changes.
* `.github/instructions/tests.instructions.md:20-78` requires xUnit tests under
	`test/`, deterministic isolated unit tests where possible, and documented test
	methods using the repository naming convention.

## Existing Persistence Conventions

### Account Details History

Account details use immutable successful-retrieval snapshots, not a mutable current
row. `PlatformDbContext` exposes header and child sets at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:27-29`.
The model scopes deterministic history reads by broker environment, timestamp, and
retrieval identifier at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:138`.

`AccountDetailsRetrievalEntity` records retrieval time, trading day, trigger source,
and optional actor, while its children carry the provider values at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountDetailsRetrievalEntity.cs:3-13`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountDetailsAccountEntity.cs:3-23`.
The store inserts the complete graph in a transaction at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:24-39`
and orders latest or adjacent reads by `RetrievedAtUtc` plus identifier at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:46-56`.

Automatic capture is deliberately deduplicated only after a successful daily save:
the application checks first, obtains an environment-scoped SQL lease, checks again,
then calls IG at
`src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:19-39`.
Its unit test confirms a failed automatic capture remains retryable at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:103-120`.

### Operational and Configuration Events

The generic operational event entity is append-oriented and includes time, category,
event type, platform and broker environments, severity, redacted JSON details,
correlation ID, and retry-cycle ID at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/OperationalEventEntity.cs:3-27`.
The application contract has the same shape at
`src/TNC.Trading.Platform.Application/Configuration/PlatformEventRecord.cs:3-13`.
`EfPlatformEventStore` writes one row per record and the current event query returns a
latest-first, environment-filtered 50-row operational feed at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformEventStore.cs:14-60`.

Configuration updates instead persist an audit row alongside the changed configuration
in the same context and save at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:84-119`.
The audit entity includes configuration identity, actor, change type, scoped
environments, timestamp, summary, redacted details, and correlation ID at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/ConfigurationAuditEntity.cs:3-27`.

### Provider Failure and Reconciliation Pattern

The IG account-details adapter retries a provider GET only once after `401`, validates
the complete provider payload before constructing application records, and returns
typed failures at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:17-30`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:53-84`.
Automatic account capture translates failures into a bounded, redacted operational
event at
`src/TNC.Trading.Platform.Application/Features/AccountDetails/CaptureDailyAccountDetailsHandler.cs:13-48`.
Authentication reconciliation runs under a lease at
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:86-101`;
after a successful auth transition it invokes account capture as best effort and does
not invalidate durable auth success if that capture fails at
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:469-480`.

Prior local account-preferences research establishes the missing provider behavior:
IG GET returns the Boolean, PUT returns `SUCCESS`, and a timed-out or disconnected
write must be followed by a fresh GET rather than blindly retried. See
`.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:81-113`
and
`.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:197`.

### Retention and Migrations

Operational retention has one configurable setting with a 90-day default at
`src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPolicy.cs:5-21`.
The SQL path uses set-based deletes for events, configuration audits, notifications,
and old account snapshots, preserving the newest account retrieval per environment at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:17-52`.
The in-memory fallback mirrors this behavior at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:56-90`.

The account-details migration is additive and supplies parent/child constraints plus
the history and daily-success indexes at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260807103334_AddAccountDetailsSnapshots.cs:10-89`.
Startup runs `MigrateAsync` for SQL Server and fails closed when schema initialization
fails at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:35-63`.
Existing tests cover atomic child rollback at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:174-183`
and migration-created schema/indexes at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:66-97`.

## Selected Design

Use a dedicated, append-only `TrailingStopsPreferenceObservations` SQL table and a
matching preference-history vertical slice. Do not overload `OperationalEvents`,
`ConfigurationAudits`, or the Account Details snapshot tables. This records evidence
of what IG reported while preserving the provider as the source of truth for current
state and commands.

### Schema Shape

Create `TrailingStopsPreferenceObservationEntity` with these columns:

| Column | Type and constraints | Purpose |
| --- | --- | --- |
| `TrailingStopsPreferenceObservationId` | `bigint` identity primary key | Stable total-order tie breaker and simple cursor key |
| `ObservedAtUtc` | `datetimeoffset`, required | The application UTC instant at which a valid IG response was received |
| `RecordedAtUtc` | `datetimeoffset`, required | Persistence time, useful for delivery-lag diagnostics |
| `PlatformEnvironment` | `nvarchar(32)`, required | Application status effective for the operation |
| `BrokerEnvironment` | `nvarchar(32)`, required | Provider target, following current durable records |
| `ProviderAccountId` | `nvarchar(64)`, nullable | Account scope when the IG contract/session makes it available; do not invent an ID when it does not |
| `TrailingStopsEnabled` | `bit`, required | The exact Boolean returned by a validated IG GET |
| `ObservationKind` | `nvarchar(32)`, required | `ReadObserved`, `UpdateConfirmed`, or `ReconciliationObserved` |
| `OperationSource` | `nvarchar(32)`, required | `OperatorRead`, `OperatorUpdate`, `AutomaticReconciliation`, or `PostWriteReconciliation` |
| `Actor` | `nvarchar(128)`, nullable | Authenticated operator for a user-requested read or update; null for automation |
| `CorrelationId` | `nvarchar(64)`, required | End-to-end command/read correlation, consistent with audit/event conventions |
| `CommandId` | `uniqueidentifier`, nullable | Identifies the requested update when the observation is its post-write confirmation |
| `ProviderOutcome` | `nvarchar(32)`, required | `Success` for persisted evidence; retain a status field without treating failures as observations |

Define a non-unique index on `(BrokerEnvironment, ProviderAccountId,
ObservedAtUtc, TrailingStopsPreferenceObservationId)` for descending history and
keyset pagination. Use a second index on `CommandId` when update audit lookups are a
real UI requirement. Do not add a unique constraint on state or day: the requirement
is every successful observed/confirmed value, including repeated unchanged reads.

`ProviderAccountId` must remain nullable until the actual IG preferences contract
establishes a stable account identifier. The prior research explicitly notes that the
endpoint is account-related but does not propose account selection. See
`.copilot-tracking/research/subagents/2026-06-06/ig-account-preferences-api-and-ui-research.md:60-65`.

### Event Semantics

Persist an observation only after a strict, successful GET response containing the
Boolean. `ReadObserved` means an operator or UI loaded the live preference.
`UpdateConfirmed` means a PUT returned success and the same request then completed a
fresh GET. `ReconciliationObserved` means a scheduled or recovery read obtained a
valid value after a prior failed or indeterminate operation.

Do not write a separate mutable "current preference" row or an unverified "change"
row. A value transition is derived in the timeline by comparing an observation with
its immediate predecessor in the same broker/account scope, ordered by
`ObservedAtUtc` and identity. The UI may label it `Changed from off to on`, but that is
a local analysis of two IG observations, not a claim that the application caused the
provider change. Repeated matching values remain evidence and must remain visible.

### Transaction, Ordering, and Deduplication

For each completed GET, create the observation ID and correlation ID before the
provider call, but add the row only after response parsing succeeds. Use one EF Core
transaction to save the observation and any corresponding configuration-style command
audit entry, should an explicit command audit be required. The existing account
snapshot transaction is the nearest convention.

The dedicated history writer should receive a completed observation record through an
application-owned port. It should not call the gateway or infer a Boolean from an
exception. Concurrent reads may append independently because their ordering is
deterministic through timestamp plus identity. Avoid a process-static coalescer for
this feature: it could erase two distinct observations and violate the stated
evidence requirement.

Use `CommandId` as an idempotency key only for the specific post-update confirmation
workflow: if a handler repeats its own save after a local persistence uncertainty, it
may detect an existing `(CommandId, ObservationKind)` record and return it. It must
not deduplicate ordinary reads or state equality. A provider call retried after `401`
still creates one observation only after its final successful response.

### Failure and Reconciliation

Failure rows do not contain `TrailingStopsEnabled`; unknown is not false. Emit a
redacted `OperationalEvents` record such as `AccountPreferencesReadFailed`,
`AccountPreferencesUpdateIndeterminate`, or `AccountPreferencesReconciliationFailed`
with source, environment, failure category, and correlation ID. Do not store provider
tokens, request bodies, raw provider responses, or raw provider error values.

On PUT timeout, disconnect, malformed success response, or other uncertain completion:

1. Do not retry the PUT blindly.
2. Write the bounded operational failure event.
3. Issue a fresh GET using the usual one-time `401` session recovery behavior.
4. If that GET succeeds, append `ReconciliationObserved` with the same `CommandId`
	 and correlation ID, then return the actual IG value to the caller.
5. If it fails, return the typed failure and leave the latest successful observation
	 intact. A later read/reconciliation can append evidence when IG is reachable.

This preserves the selected live-only preference behavior in
`.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:113`
without creating a local desired-state shadow.

### Query and UI Implications

Keep `GET /account-preferences` a live IG query; it can append `ReadObserved` only
after obtaining the live response. Add a separate, read-only history query with
environment/account filtering, newest-first keyset pagination, `ObservationKind`,
source, actor, correlation ID, timestamp, value, and derived transition indicator.
The history page must state that each row is an observed IG value at that time, not a
guarantee of the value between observations. The current preference page should not
fall back to the last local row during an IG outage, because that would imply a second
authority.

Provide a small history panel or page for retrospective analysis, distinct from the
live account-preferences control and existing Account Details snapshots. The previous
research likewise rejects combining mutable preferences with Account Details at
`.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:116-132`.

### Retention, Migration, and Tests

Add the new table to `PlatformDbContext`, an additive EF Core migration, and both SQL
and in-memory branches of `OperationalRecordRetentionProcessor`. Unlike Account
Details, preserve no synthetic latest row after expiry by default: the retained
history has a clear 90-day operational evidence window. Make a longer,
separately-configured preference-audit retention period an explicit business decision
if retrospective trading analysis needs more than 90 days.

Migration tests should assert the table and indexes exist alongside the current
`MigrateAsync` test. Add tests for:

* Strict provider Boolean parsing and no persistence on malformed or failed GET.
* One `401` replay and no blind PUT retry after an indeterminate outcome.
* One append per successful observation, including repeated equal values.
* Derived transition correctness with equal timestamps, using identity as the tie
	breaker and never crossing broker/account scope.
* Atomic persistence of the confirmed observation and command audit when both are
	required.
* Redacted failure-event content and a successful follow-up reconciliation append.
* SQL migration/index creation, retention deletion, and context-restart history reads.

## Alternatives and Caveats

| Alternative | Assessment |
| --- | --- |
| Add `TrailingStopsEnabled` to `AccountDetailsAccountEntity` | Reject. The preferences endpoint is a separate live provider resource, and adding it to account snapshots would falsely imply the value was captured by the accounts response. |
| Reuse `OperationalEvents` only | Reject. JSON details and a 50-row operational feed cannot support typed, ordered, long-lived evidence queries efficiently. |
| Reuse `ConfigurationAudits` | Reject. IG state is observed external state, not platform configuration. `ChangedBy` would be absent for scheduled reads and would misrepresent external changes as local configuration changes. |
| Persist a local desired/current preference and reconcile it | Reject. It creates a second source of truth, stale-read behavior, and conflict rules without an offline-workflow requirement. |
| Persist only transitions | Reject. It loses repeated valid observations needed to establish what was observed before or after a trade decision. |
| Persist every failed attempt as an observation with nullable Boolean | Reject. It muddies evidence semantics. Use operational events for failure telemetry and reserve history rows for verified provider values. |

The principal caveat is observability: history can prove only what the platform
observed, not continuous provider state or the actor that changed it outside this
platform. The actual IG contract must also confirm whether a stable account identifier
is available for this preference before making `ProviderAccountId` required. Existing
records use `Demo` as a broker persistence key; earlier research requires user-facing
application terminology to be Test/Live and warns against silently reinterpreting
historical environment records at
`.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:122-130`.

## Follow-On Questions

* What retention duration satisfies retrospective trading-analysis and any regulatory
	obligations: the current 90-day operational default, a longer dedicated value, or
	an export/archive policy?
* Does the production IG account-preferences response or authenticated session expose
	a stable account ID that should scope history, and does the preference apply to the
	whole account or selected account context?
* Should every operator GET append evidence, or should only explicit history capture,
	successful updates, and scheduled reconciliation do so? The selected design follows
	the request literally and appends every valid observation.
* Is a scheduled reconciliation cadence required, and should it run only during the
	configured trading schedule to avoid unnecessary provider traffic and retention
	volume?
* Must a preference update create a separate operator command-audit row in addition to
	the post-write confirmed observation, or is `Actor`, `CommandId`, and
	`UpdateConfirmed` sufficient audit evidence?
