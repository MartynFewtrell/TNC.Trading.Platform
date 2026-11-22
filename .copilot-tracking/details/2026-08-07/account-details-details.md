<!-- markdownlint-disable-file -->
# Implementation Details: Account Details

## Context Reference

Sources: user request, `.copilot-tracking/research/2026-08-07/account-details-research.md`, and `.copilot-tracking/research/subagents/2026-08-07/account-details-architecture-research.md`.

## Implementation Phase 1: Application Operations and Durable Login Hooks

<!-- parallelizable: false -->

### Step 1.1: Define the Account Details operations and ports

Create one request, response, handler, and focused records per read, manual refresh, and automatic daily capture operation beneath `src/TNC.Trading.Platform.Application/Features/AccountDetails/`. Define inward-owned `IAccountDetailsGateway`, `IAccountDetailsSnapshotStore`, and `IAccountDetailsRefreshLease` ports. The read handler must only query the store. The refresh handler acquires/coalesces work by environment, invokes the gateway, and writes a validated immutable snapshot. Model typed outcomes for saved, in-progress, allowance-limited, unavailable, malformed-provider data, timeout, and conflict results.

Make lease outcomes trigger-specific: automatic capture that cannot acquire the lease returns immediately as deferred and does not delay or fail the completed login; a manual request receives `RefreshInProgress` with the latest saved retrieval timestamp. Coalesce concurrent same-instance manual callers onto one in-flight operation.

Files:
* src/TNC.Trading.Platform.Application/Features/AccountDetails/ - new contracts, records, handlers, and ports
* src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs - shared configured-time-zone trading-day operation

Discrepancy references:
* DD-01: Do not expand the proof contract or store

Success criteria:
* Application records contain only persisted account details and no provider credentials or session headers
* The cursor uses both `RetrievedAtUtc` and retrieval identifier for deterministic navigation
* Read execution cannot call the provider gateway

Context references:
* .copilot-tracking/research/2026-08-07/account-details-research.md (Lines 101-119 and 251-267)

Dependencies:
* Revalidate the current IG account response before finalizing private Infrastructure DTOs

### Step 1.2: Hook daily capture after durable successful authentication

Calculate `TradingDay` with the configured trading-schedule time zone. In the reconciler, detect a newly successful login, save its runtime state, then call automatic capture outside the durable login save. In the direct handler, invoke automatic capture only after `ManualAuthRetryCommitter` has completed its final success commit. Catch automatic-capture failure so login success, notification, and previously saved Account Details remain intact.

Write a bounded, redacted `AccountDetailsCaptureFailed` operational event through the existing `IPlatformEventStore`/authentication side-effects path. Include environment, trigger source, outcome category, and correlation identifier, but never credentials, session headers, or raw provider data. Do not make this diagnostic write part of the successful-login transaction.

Files:
* src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs - post-save automatic capture invocation
* src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs - post-commit automatic capture invocation
* src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs - day calculation extension

Discrepancy references:
* DR-02: Existing login snapshots use UTC dates; Account Details intentionally uses schedule-zone day

Success criteria:
* No gateway call occurs when an automatic snapshot already exists for the environment/day
* A capture error cannot make either otherwise-successful authentication path fail
* Manual refresh bypasses the once-per-day check
* An automatic lease miss yields without delaying login, while manual contention includes the latest retrieval timestamp in its typed result
* Each automatic failure is visible through a redacted operational event

Context references:
* .copilot-tracking/research/subagents/2026-08-07/account-details-architecture-research.md (Lines 20-38)

Dependencies:
* Step 1.1 completion

### Step 1.3: Test the Application slice

Add unit tests beside existing Application feature tests. Use fakes to prove complete input validation, no provider reads, same-process joining, automatic lease-yield versus manual in-progress behavior, daily de-duplication, failed automatic-capture retry, success-path ordering, bounded diagnostic emission, and cursor results.

Files:
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/ - new operation tests
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs - durable-success hook tests
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/ReconcilePlatformAuthentication/ - post-save capture tests

Success criteria:
* Every validation rejection leaves the snapshot store unchanged
* Tests distinguish reconciler success from direct HTTP manual retry success

Dependencies:
* Steps 1.1 and 1.2 completion

### Step 1.4: Validate the Application phase

Before beginning Phase 2, build the affected Application project and run its unit tests. Also rerun this gate after any significant Application change in this phase. Do not proceed on a failure; repair the current Application slice and rerun both commands.

Validation commands:
* `dotnet build src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj` - compile Application contracts and login hooks
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj` - validate Application behavior

Success criteria:
* Both commands succeed before Phase 2 begins

Dependencies:
* Steps 1.1 through 1.3 completion

## Implementation Phase 2: Infrastructure Provider, Persistence, and Retention

<!-- parallelizable: false -->

### Step 2.1: Implement the dedicated IG accounts gateway

Add Infrastructure-private nullable wire DTOs and a dedicated `IAccountDetailsGateway` adapter. Establish a new session for the configured IG test account on each refresh, keep CST and X-SECURITY-TOKEN in local request flow only, and call `/accounts` with version `1` and `Accept: application/json; charset=UTF-8`. The test endpoint's `demo-api` hostname identifies IG's test environment; the response remains real test-account data. On an authenticated `/accounts` `401`, establish one replacement session and replay the accounts request once; classify a second `401` as unavailable. Reject empty lists, duplicate account IDs, malformed JSON, and missing required values before persistence. Normalize provider errors without logging secrets or raw payloads.

Files:
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/ - new accounts gateway, private DTOs, and optional private session helper
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs - gateway registration

Discrepancy references:
* DD-01: retain `IgBrokerAuthenticationGateway` proof-only semantics
* DR-01: provider response contract must be revalidated immediately before implementation

Success criteria:
* Fresh sessions never enter Application, API, Web, SQL, or diagnostic messages
* Only fully valid account lists produce Application records
* A `401` causes at most one fresh-session replay and never persists a partial result

Context references:
* .copilot-tracking/research/2026-08-07/account-details-research.md (Lines 101-119 and 212-239)

Dependencies:
* Phase 1 port contracts

### Step 2.2: Persist immutable retrieval snapshots and enforce refresh ownership

Add `AccountDetailsRetrieval` and `AccountDetailsAccount` EF entities and mappings in `PlatformDbContext`, then create an additive migration and update the model snapshot. Persist one header and all child rows in a local transaction after provider validation. Add deterministic retrieval, composite keyset, and filtered automatic-day uniqueness indexes. Implement an environment-scoped `sp_getapplock` adapter whose automatic contention yields immediately and whose manual contention loads the newest retrieval timestamp for the typed in-progress result. Register all adapters through Infrastructure DI.

Files:
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs - DbSets and mappings
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/ - retrieval entities, store, and account-details lease
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/ - additive migration and model snapshot
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs - store and lease registrations

Success criteria:
* One failed child insertion rolls back its retrieval header and all siblings
* Filtered automatic index permits manual snapshots on the same day
* Keyset results are broker-environment scoped and stable when timestamps match

Context references:
* .copilot-tracking/research/2026-08-07/account-details-research.md (Lines 137-176)

Dependencies:
* Phase 1 contracts

### Step 2.3: Apply safe retention and preserve migration baseline behavior

Extend both the SQL set-based and non-SQL fallback retention paths for Account Details headers. Delete expired headers and rely on cascade deletion for children, except for the newest successful retrieval in each environment. Keep `PlatformStartupInitializer.RequiredPlatformTables` unchanged because it is an original-schema migration-baseline guard, not an all-current-tables inventory.

Files:
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs - retention paths
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs - no feature-table list change; validate additive migration behavior

Discrepancy references:
* DD-02: Reject the primary research's proposed startup table-list update based on architecture verification

Success criteria:
* Retention keeps one newest retrieval after its normal retention age
* Pre-feature database baseline behavior remains unchanged

Context references:
* .copilot-tracking/research/subagents/2026-08-07/account-details-architecture-research.md (Lines 75-101)

Dependencies:
* Step 2.2 completion

### Step 2.4: Test Infrastructure adapters and schema behavior

Add fake HTTP and EF-backed tests for headers, one-time `401` reauthentication/replay, response validation, error classification, transaction rollback, retrieval queries, daily uniqueness, trigger-specific lease contention, retention, migration, and startup initialization. Keep real IG test-account calls opt-in rather than part of standard tests.

Files:
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ - Account Details gateway, store, lease, retention, and startup tests
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/ - SQL-specific migration and concurrency coverage when existing project conventions support it

Success criteria:
* Tests prove no partial snapshot and no token disclosure
* Tests prove an automatic uniqueness collision is an already-captured success

Dependencies:
* Steps 2.1 through 2.3 completion

### Step 2.5: Validate the Infrastructure phase

Before beginning Phase 3, build the affected Infrastructure project and run its unit tests. Rerun this gate after each significant gateway, persistence, migration, lease, or retention change. Stop to repair the current Infrastructure slice when either command fails.

Validation commands:
* `dotnet build src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj` - compile provider, EF Core, and retention adapters
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj` - validate Infrastructure behavior

Success criteria:
* Both commands succeed before Phase 3 begins

Dependencies:
* Steps 2.1 through 2.4 completion

## Implementation Phase 3: API Contract and Authorization

<!-- parallelizable: false -->

### Step 3.1: Publish read and refresh routes

Register the Account Details handlers and map a Viewer-protected `GET /api/platform/account-details?cursor={opaqueCursor}` and an Operator-protected `POST /api/platform/account-details/refresh`. Return `200` with `retrieval: null` for no data, `200` for saved refresh, `409` for in-progress/conflict with the newest known retrieval timestamp, `429` only for recognized provider allowance errors, `502` for malformed upstream data, `503` for unavailable session/upstream, and `504` for timeout.

Files:
* src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs - handler registrations
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs - thin route mappings
* src/TNC.Trading.Platform.Api/Features/Platform/ - response mapping types if existing layout requires them

Success criteria:
* GET never triggers provider I/O
* Endpoint policy, not Web visibility, denies Viewer refresh
* The response includes `OlderCursor` and `NewerCursor`

Context references:
* .copilot-tracking/research/subagents/2026-08-07/account-details-architecture-research.md (Lines 43-62)

Dependencies:
* Phases 1 and 2 completion

### Step 3.2: Test endpoint authorization and mappings

Extend endpoint handler tests and API integration tests for policy enforcement, read response/cursors, manual `409` timestamp payload, refresh mappings, and all typed failure outcomes.

Files:
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs - unit mapping tests
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/ - authorization and persistence-backed route tests

Success criteria:
* Viewer POST is denied even when the Web refresh control is hidden
* Existing retrieval remains available through GET after a failed refresh

Dependencies:
* Step 3.1 completion

### Step 3.3: Validate the API phase

Before beginning Phase 4, build the API project and run its endpoint unit tests. Rerun this gate after each significant API contract, route, authorization, or outcome-mapping change. Do not advance while the current API slice fails either command.

Validation commands:
* `dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj` - compile endpoint and handler registration changes
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj` - validate API mappings and policies

Success criteria:
* Both commands succeed before Phase 4 begins

Dependencies:
* Steps 3.1 and 3.2 completion

## Implementation Phase 4: Web History and Operator Refresh

<!-- parallelizable: false -->

### Step 4.1: Add Account Details client and presentation state

Add authorized GET and POST methods to `PlatformApiClient` and a page presenter/view-model that handles initial load, opaque previous/next cursor navigation, pending refresh, refresh results, and failures without clearing the active retrieval.

Files:
* src/TNC.Trading.Platform.Web/PlatformApiClient.cs - Account Details HTTP methods
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/ - new presenter and view models

Success criteria:
* The client requests stored data only during page load/navigation
* Failed refresh preserves current rendered account data

Dependencies:
* Phase 1 public response contract; coordinate route shape with Phase 3

### Step 4.2: Build the protected Account Details page and navigation

Create `/account-details` with the Viewer protection and load pattern used by IG login history. Add Viewer navigation adjacent to that history. Render retrieval timestamp/trading day, every account identity and state field, transfer flags, currency, and the four balances. Use accessible icon controls for older/newer navigation, disabling unavailable directions. Render the refresh command only for Operator/Administrator access, and disable it during the request.

Files:
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor - new route and states
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css - scoped styling if needed
* src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor - Viewer navigation

Success criteria:
* Viewer can inspect saved history but cannot invoke the provider refresh API
* Navigation never requests an IG session
* Empty, error, and in-progress states are visible without layout shifts or data loss

Context references:
* .copilot-tracking/research/2026-08-07/account-details-research.md (Lines 251-267)

Dependencies:
* Step 4.1 completion

### Step 4.3: Test Web states and access boundaries

Add bUnit tests for Viewer page access, empty/newest/history rendering, disabled cursor buttons, pending/failed refresh preservation, and Operator-only command visibility and disablement.

Files:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/Components/Pages/AccountDetails/ - new component and presenter tests
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs - navigation test

Success criteria:
* Both representative accounts and all balance labels render
* UI command visibility does not replace API authorization coverage

Dependencies:
* Steps 4.1 and 4.2 completion

### Step 4.4: Validate the Web phase

Before beginning hosted validation, build the Web project and run its bUnit project. Rerun this gate after each significant client, presenter, page, navigation, or authorization-visibility change. Repair failures in the current Web slice before moving to Phase 5.

Validation commands:
* `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` - compile Web client and Razor component changes
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - validate Web presentation and access states

Success criteria:
* Both commands succeed before Phase 5 begins

Dependencies:
* Steps 4.1 through 4.3 completion

## Implementation Phase 5: Integration, Documentation, and Final Validation

<!-- parallelizable: false -->

### Step 5.1: Verify distributed behavior

Add SQL-backed and hosted coverage for migration application, Keycloak Viewer/Operator boundaries, automatic first-successful-login capture, concurrent refresh ownership across replicas, manual refresh, and keyset history. Use fake provider endpoints for deterministic CI; retain an opt-in real IG test-account smoke check only where credentials are explicitly configured.

Files:
* test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/ - distributed integration coverage
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/ - browser journey coverage
* test/TNC.Trading.Platform.Infrastructure/ - SQL-backed coverage per existing test layout

Success criteria:
* Competing replicas make no more than one upstream accounts request for the same acquisition
* Keycloak policies distinguish Viewer read and Operator refresh

Dependencies:
* Phases 1 through 4 completion; Docker-backed AppHost infrastructure

### Step 5.2: Update implementation documentation

Update the appropriate owning `docs/wiki/` pages, rather than duplicate work-package documentation. State that the feature retrieves real data from the configured IG test account, distinguish the test endpoint hostname from synthetic data, document the future Live boundary and isolation, and cover protected credentials, schedule-zone automatic capture, retention/migration operations, Viewer versus Operator policy, GET cursor/POST contract, and concurrency/error behavior.

Files:
* docs/wiki/ - existing owning local-development, testing, architecture, and API/operator pages identified during implementation

Success criteria:
* Documentation reflects final route responses and retention semantics
* No documentation claims that Web/Application receive IG session tokens

Dependencies:
* Final API and persistence contracts

### Step 5.3: Run final validation and handle minor corrections

The prior phase gates are mandatory and must already be green. Run the final suite after each significant integration, hosted-test, or documentation change, then build the solution and run all affected projects. Run hosted and browser tests only with Docker-backed dependencies available. Repair only isolated failures; return larger design changes to a new planning pass.

Validation commands:
* `dotnet build TNC.Trading.Platform.slnx` - full compilation
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj` - Application behavior
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj` - adapters and retention
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj` - endpoint mappings
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - bUnit states
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - API integration
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - UI journey
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` - distributed authorization and cross-replica coverage

Success criteria:
* All changed-slice tests pass before full build/test reporting
* Hosted test prerequisites and any skipped opt-in real IG test-account checks are explicitly recorded
* Each Phase 1 through 4 build-and-test gate completed successfully before the next phase began

## Dependencies

* Phase 1 contracts precede Infrastructure adapters and endpoint registration
* Phase 3 route contract and Phase 4 client/page work may proceed concurrently once response types are frozen
* Docker-backed SQL Server and Keycloak are required for final distributed validation

## Success Criteria

* The delivered slice satisfies all plan criteria without exposing provider credentials or session tokens
* All documented focused and final validation steps complete or record a concrete environment blocker