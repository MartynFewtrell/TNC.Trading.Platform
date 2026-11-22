<!-- markdownlint-disable-file -->
# Implementation Details: Trailing-Stops Remediation

## Context Reference

Sources: user request; `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md`; `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-plan-verification.md`; repository instructions under `.github/instructions/`.

Implementation must preserve the current uncommitted worktree and avoid automated calls to real IG. Focused diagnostic, fixture, and startup checks use an external process-tree watchdog capped below 60 seconds. The complete serial repository gate retains finite hang detection but may exceed 60 seconds because the healthy reproduced baseline required 134.9 seconds.

## Implementation Phase 0: Resolve Migration and Contract Gates

<!-- parallelizable: true -->

### Step 0.1: Inventory deployed migration history

Query `__EFMigrationsHistory` read-only in every non-disposable CI, shared development, staging, and deployed database. Record whether `20260830112227_SnapshotRepair` is present. Do not delete, rename, or regenerate discoverable migration history until every owner responds.

Files:
* `.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md` - Record sanitized environment inventory and the selected migration strategy during implementation

Discrepancy references:
* DR-01

Success criteria:
* Every known non-disposable environment has an explicit present, absent, or inaccessible result
* Inaccessible environments block destructive migration-history edits
* No credentials, connection strings, or sensitive database values are recorded

Context references:
* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md` (Lines 216-231) - Migration-history risk and preferred strategy
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-plan-verification.md` (Lines 143-179) - Current migration artifacts and decision gate

Dependencies:
* Read-only access supplied by each environment owner

### Step 0.2: Confirm externally visible behavior defaults

Record the accepted API and retention decisions before their implementation phases. Use the research-backed defaults if no owner overrides them: a typed not-applied result mapped to HTTP 409 when authoritative GET differs from the requested value, and shared `Retention:OperationalRecordsDays` for trailing-stops observations.

Files:
* `.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md` - Record decision owners and outcomes

Discrepancy references:
* DR-02
* DR-03

Success criteria:
* Mismatch response status and Problem Details shape are explicit
* Exactly one effective retention setting is named
* Deferred approval uses the documented defaults and remains visible in delivery records

Context references:
* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md` (Lines 190-214) - Confirmed update semantics and retention recommendation

Dependencies:
* API and product owner review, or acceptance of the documented defaults

## Implementation Phase 1: Restore API Composition

<!-- parallelizable: true -->

### Step 1.1: Register account-preferences application services

Extend the API-owned explicit registration list. Register `GetAccountPreferencesHandler`, `UpdateAccountPreferencesValidator`, `UpdateAccountPreferencesHandler`, and `GetTrailingStopsPreferenceObservationsHandler` as scoped services. Keep `Program.cs` limited to invoking the registration extension.

Files:
* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs` - Add four scoped registrations

Discrepancy references:
* None; directly addresses the reproduced startup root cause

Success criteria:
* All four endpoint dependencies resolve from a built service provider scope
* Minimal API endpoint construction no longer infers a request body for handler parameters

Context references:
* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs` (Lines 18-46) - Owning composition list
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` (Lines 51-61, 129-147) - Routes and required services

Dependencies:
* None

### Step 1.2: Add route-composition regression coverage

Add an API unit test using the existing direct `WebApplication` pattern. Invoke the real `AddPlatformApplication`, build the app, map `MapPlatformEndpoints`, force endpoint data-source metadata construction, and resolve all four services. Fully document the test and use repository naming conventions.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApplicationServiceCollectionExtensionsTests.cs` - Add composition and resolution regression tests

Success criteria:
* The test fails when any route dependency registration is removed
* The test fails on the previously reproduced inferred-body route construction error
* No Aspire host, container, network, or database is required

Context references:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs` (Lines 14-44) - Existing direct application construction pattern
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs` (Lines 9-32) - Existing helper tests that do not construct routes

Dependencies:
* Step 1.1

### Step 1.3: Validate API composition

Run the API build and only the new composition tests through the shared sub-60-second watchdog. Capture bounded output tails and terminate the complete process tree on timeout.

Validation commands:
* `dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-restore` - API compilation
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~PlatformApplicationServiceCollectionExtensionsTests" --blame-hang --blame-hang-timeout 45s` - Route composition

## Implementation Phase 2: Make Distributed Failures Bounded and Diagnostic

<!-- parallelizable: false -->

### Step 2.1: Propagate one bounded fixture cancellation chain

Create one linked cancellation token with a total deadline below 60 seconds and pass it through Aspire create, build, start, health, and resource-discovery operations. Replace independent multi-minute waits. For direct processes, race readiness against `WaitForExitAsync` or check `HasExited` and include buffered stdout/stderr. For Aspire-managed processes, observe terminal API state through `ResourceNotifications`; the managed `AppHostProcessHandle.Process` is null. Confirm the exact Aspire 13.2.3 terminal state names during implementation. Keep cleanup in `finally` or initialization failure paths and stop only resources created by the fixture.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` - Own bounded lifecycle token and cleanup
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTestRuntime.cs` - Accept cancellation and bound readiness polling
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` - Fail fast when the API process exits
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Preserve diagnostics and bounded discovery for shared Web/AppHost callers
* `test/Shared/Authentication/KeycloakPortLease.cs` - Bound lease acquisition through caller cancellation
* `test/Shared/Authentication/AppHostProcessHandle.cs` - Reuse direct-process readiness, exit detection, and bounded diagnostic buffers

Discrepancy references:
* DR-04

Success criteria:
* Fixture initialization completes or fails in under 60 seconds
* An exited API resource ends polling immediately
* Failure output includes captured API/AppHost diagnostics and terminal resource state
* Cleanup does not stop pre-existing persistent SQL, Mailpit, or Keycloak resources

Context references:
* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md` (Lines 101-120) - Failure cascade and current wait budgets
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTestRuntime.cs` (Lines 7-35) - Current API readiness loop
* `test/Shared/Authentication/AppHostProcessHandle.cs` (Lines 75-120, 178-199, 387-447) - Stronger probe, direct-process exit, cleanup, and diagnostics
* `.github/instructions/aspire-tests.instructions.md` - Aspire lifecycle and cleanup requirements

Dependencies:
* Phase 1 passes

### Step 2.2: Bound SQL fixture operations

Thread one linked sub-60-second token through Docker discovery process wait/output, SQL connection open, explicit reset SQL, migration, query, and disposal. Set finite `SqlCommand.CommandTimeout` values and an EF migration command timeout at the shared `CreateDbContext` boundary. Pass the fixture token to each current `MigrateAsync` call. The fixture discovers an existing Aspire SQL container, creates and drops a disposable database, and resets with explicit SQL; do not add Respawn or Testcontainers.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` - Bound Docker discovery, connection, context creation, reset, and disposal
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Pass fixture cancellation to all migration calls

Success criteria:
* Docker and SQL operations have explicit finite cancellation or timeout behavior
* Timeout diagnostics identify the operation and preserve a bounded output tail
* Existing fixture resource ownership and cleanup semantics remain intact

Context references:
* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md` (Lines 109-120) - Unbounded SQL risk
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` (Lines 13-121) - Exact SQL and Docker ownership
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` (Lines 18-50) - Current migration calls

Dependencies:
* None beyond existing Docker-backed integration-test prerequisites

### Step 2.3: Repair the independent date-drift test

Replace `TimeProvider.System` in the affected account-details test fixture with a fixed `TimeProvider` aligned to the hard-coded trading day. Do not change production date behavior.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs` - Make the test clock deterministic

Discrepancy references:
* DR-05

Success criteria:
* `CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure` passes regardless of wall-clock date
* Production code remains unchanged

Context references:
* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md` (Lines 122-127) - Independent date drift

Dependencies:
* None

### Step 2.4: Validate one fact per distributed fixture

Run the canonical synthetic-token API fact and AppHost-backed Web functional fact below. Invoke each through the 50-second `Invoke-TrailingStopsWatchdog` defined in `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-validation-anchors.md` (Lines 125-181). Stop at the first failure.

Validation commands:
* `Invoke-TrailingStopsWatchdog 'api-synthetic-smoke' @('test', 'test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj', '--no-restore', '--no-build', '--filter', 'FullyQualifiedName=TNC.Trading.Platform.Api.IntegrationTests.Authentication.PlatformAuthenticationSyntheticTokenIntegrationTests.IgLoginHistoryEndpoint_ShouldReturnOkWithSecretSafeResponse_WhenViewerTokenIsProvided', '--blame-hang', '--blame-hang-timeout', '45s', '--results-directory', 'artifacts/quality-gates/trailing-stops-remediation/api-synthetic-smoke/trx', '--logger', 'trx;LogFileName=api-synthetic-smoke.trx')`
* `Invoke-TrailingStopsWatchdog 'web-apphost-smoke' @('test', 'test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj', '--no-restore', '--no-build', '--filter', 'FullyQualifiedName=TNC.Trading.Platform.Web.FunctionalTests.Authentication.PlatformAuthenticationFunctionalTests.RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry', '--blame-hang', '--blame-hang-timeout', '45s', '--results-directory', 'artifacts/quality-gates/trailing-stops-remediation/web-apphost-smoke/trx', '--logger', 'trx;LogFileName=web-apphost-smoke.trx')`

Retained artifacts:
* `artifacts/quality-gates/trailing-stops-remediation/<gate>/stdout.log`
* `artifacts/quality-gates/trailing-stops-remediation/<gate>/stderr.log`
* `artifacts/quality-gates/trailing-stops-remediation/<gate>/timeout.txt` when timed out
* `artifacts/quality-gates/trailing-stops-remediation/<gate>/trx/<gate>.trx` plus blame-hang attachments

Success criteria:
* Each smoke check finishes within 60 seconds
* API terminal failure is reported directly rather than multiplied across a test class
* No automated check calls real IG

Dependencies:
* Steps 2.1 through 2.3

## Implementation Phase 3: Correct Provider and Command Semantics

<!-- parallelizable: false -->

### Step 3.1: Correct and type the IG wire contract

Replace reflective `trailingStops` serialization/deserialization with typed provider request and response DTOs using `trailingStopsEnabled`. Send one PUT without blind retry. Classify a malformed successful PUT acknowledgement as indeterminate so the caller performs one authoritative GET. Preserve caller cancellation and sanitized diagnostics.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Correct typed wire contract and outcome classification
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs` - Assert exact JSON, indeterminate acknowledgement, and cancellation

Success criteria:
* GET and PUT use only `trailingStopsEnabled`
* A malformed 2xx PUT acknowledgement cannot be reported as definite rejection
* Tests prove no blind PUT retry and no sensitive payload logging

Context references:
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-plan-verification.md` (Lines 88-100) - Current gateway defects
* `https://labs.ig.com/reference/accounts-preferences.html` - Provider contract

Dependencies:
* Phase 2 provides bounded test behavior

### Step 3.2: Add typed not-applied application behavior

Extend inward-owned response/outcome contracts so an authoritative GET value different from the requested Boolean returns typed not-applied behavior. Persist the valid observed state even on mismatch. Keep successful confirmation limited to exact equality.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesGatewayOutcome.cs` - Represent indeterminate provider acknowledgement as needed
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesResponse.cs` - Add typed not-applied result without transport dependencies
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` - Reconcile and compare authoritative state
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` - Replace mismatch-as-success coverage and add reconciliation cases

Discrepancy references:
* DR-02

Success criteria:
* Exact observed/requested equality is required for success
* A mismatch persists the valid observation and returns not-applied
* PUT is never blindly retried
* Cancellation propagates through PUT, reconciliation GET, persistence, and event recording

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` (Lines 20-38) - Existing confirmation behavior

Dependencies:
* Step 3.1
* Step 0.2 decision or documented default

### Step 3.3: Emit complete bounded telemetry

Use the injected event store in GET and update handlers. Emit one sanitized operational event for each failed or indeterminate provider operation, while avoiding duplicate success events. Keep event-write failures bounded according to the existing operational-event policy.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` - Record GET failures through the required event store
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` - Record PUT and reconciliation failures/indeterminate outcomes
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` - Verify event count, category, sanitization, and cancellation

Success criteria:
* Every provider failure or indeterminate operation produces exactly one expected event
* Events do not contain credentials, tokens, or raw provider bodies
* Successful operations do not produce failure telemetry

Dependencies:
* Step 3.2

### Step 3.4: Map the API mismatch contract

Translate typed not-applied behavior to HTTP 409 Problem Details using the approved extension fields. Keep endpoints transport-only and do not move comparison policy into the API.

Files:
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs` - Map application outcome to HTTP 409
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Add response mapping only if shared transport mapping owns it
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs` - Assert status and Problem Details
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` - Assert mapping details

Success criteria:
* Mismatch returns HTTP 409 with stable, documented Problem Details
* Success and provider-failure mappings remain compatible
* API code performs translation, not business decisions

Dependencies:
* Steps 3.2 and 3.3

### Step 3.5: Validate provider and command slices

Run the IG gateway, account-preferences Application, and API endpoint tests separately through the watchdog. Stop at the first failed slice.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~IgAccountPreferencesGatewayTests" --blame-hang --blame-hang-timeout 45s`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~AccountPreferences" --blame-hang --blame-hang-timeout 45s`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~AccountPreferences" --blame-hang --blame-hang-timeout 45s`

## Implementation Phase 4: Reconcile Migration and History Persistence

<!-- parallelizable: false -->

### Step 4.1: Apply the approved migration-history strategy

Default path: preserve discoverable `20260830112227_SnapshotRepair`, delete only the undiscoverable duplicate source, remove duplicate model configuration, regenerate the snapshot, and add a later corrective migration only when the resulting model differs from the applied schema. Do not edit this phase until Step 0.1 is complete.

If every owner proves `SnapshotRepair` is disposable and unapplied outside local databases, replace both artifacts with one canonical generated migration and reset only explicitly disposable databases. Record the alternate selection before edits.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs` - Preserve by default
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs` - Preserve metadata by default
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs` - Remove undiscoverable duplicate on the default path
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Keep one complete entity configuration
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs` - Regenerate from the deduplicated model

Discrepancy references:
* DR-01
* DD-01

Success criteria:
* `dotnet ef migrations list --no-connect` shows one intentional migration lineage
* Empty-database migration succeeds
* Upgrade from an already-applied `SnapshotRepair` succeeds without duplicate `CREATE TABLE`
* The table and intended indexes match the model snapshot

Dependencies:
* Step 0.1 is complete
* Phase 3 data contracts are stable

### Step 4.2: Partition history and validate cursor membership

Extend the inward store port to require platform and broker environment. Return a typed invalid-cursor result for malformed GUIDs, missing cursor rows, and cursors outside the selected partition. Query and seek within the selected partition using deterministic timestamp-plus-ID ordering.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs` - Add partition inputs and typed paging result
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsResponse.cs` - Represent invalid cursor if owned by the use case
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs` - Pass configured platform and broker environment
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs` - Apply partition and strict seek semantics
* `src/TNC.Trading.Platform.Api/Features/Platform/GetTrailingStopsPreferenceObservationsEndpointHandler.cs` - Validate syntax and map invalid cursor to HTTP 400
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStoreTests.cs` - Cover partition and cursor cases
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Cover SQL ordering, isolation, and cursor behavior

Success criteria:
* History never mixes platform or broker partitions
* Malformed, unknown, and cross-partition cursors return HTTP 400 and never restart page one
* Equal timestamps page deterministically without duplicates or omissions

Context references:
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-plan-verification.md` (Lines 112-125) - Current global query and cursor fallback

Dependencies:
* Step 4.1 schema/model state is stable

### Step 4.3: Add migration identity and upgrade tests

Extend SQL integration coverage to assert applied migration IDs, empty creation, already-applied `SnapshotRepair` upgrade, intended indexes, partition paging, and non-null cursors. Use isolated disposable databases and retain enough diagnostics to distinguish migration from query failures.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Add focused schema and history facts
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs` - Reuse shared conflict/recovery patterns only where needed

Success criteria:
* Tests fail if the intended migration is undiscoverable
* Both fresh and in-place upgrade paths pass
* Every SQL fact completes within the external deadline

Dependencies:
* Steps 4.1 and 4.2

### Step 4.4: Validate persistence slices

Run EF migration listing, Infrastructure unit slices, then one SQL fact at a time. Stop at the first failure and retain TRX output.

Validation commands:
* `dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext --no-build --no-connect`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~EfTrailingStopsPreferenceObservationStoreTests" --blame-hang --blame-hang-timeout 45s`
* Run each new `AccountPreferencesSqlIntegrationTests` fact with `FullyQualifiedName=<exact name>`, `--blame-hang --blame-hang-timeout 45s`, and a unique TRX filename

## Implementation Phase 5: Complete Environment, Retention, and UI Behavior

<!-- parallelizable: true -->

### Step 5.1: Enforce Test/Demo policy and external presentation

Require both application `PlatformEnvironmentKind.Test` and legacy broker `BrokerEnvironmentKind.Demo` before provider access. Continue storing the broker value needed for compatibility, but present `Test` through the existing `BrokerEnvironmentPresentation` mapper in API history responses.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs` - Enforce both environment dimensions
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` - Supply policy inputs if needed
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` - Supply policy inputs if needed
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Use presentation mapper
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesEnvironmentPolicyTests.cs` - Cover both dimensions
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` - Assert external Test presentation

Success criteria:
* Provider access is rejected unless platform Test and broker Demo are both true
* Persisted compatibility data remains Demo
* API responses present Test, not Demo

Dependencies:
* Phase 3 contracts are stable

### Step 5.2: Use one effective retention setting

Remove `Retention:TrailingStopsPreferenceObservationsDays` and the fixed trailing-stops cutoff. Use the validated `OperationalRecordsDays` plan for both SQL and in-memory deletion. Make unit and SQL tests discriminate with a one-day setting and boundary records.

Files:
* `src/TNC.Trading.Platform.Api/appsettings.json` - Remove the inert setting
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs` - Apply the bound operational cutoff
* `src/TNC.Trading.Platform.Application/Services/TrailingStopsPreferenceObservationRetentionPolicy.cs` - Remove or adapt fixed policy if no longer needed
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` - Update the owning retention tests
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Add discriminating one-day SQL coverage

Discrepancy references:
* DR-03

Success criteria:
* One configuration key controls trailing-stops retention
* One-day tests distinguish configured behavior from the former 90-day constant
* SQL and in-memory paths calculate the same cutoff

Dependencies:
* Step 0.2 decision or documented default
* Phase 4 model state is stable

### Step 5.3: Cancel Blazor operations on disposal

Give the account-preferences component an owned lifetime `CancellationTokenSource`, pass its token to load, save, and paging requests, and cancel/dispose it when the component is disposed. Treat owned teardown cancellation as non-error control flow.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Own and dispose request lifetime
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Preserve cancellation without rendering an error
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Add delayed request disposal coverage

Success criteria:
* Disposing the component cancels an in-flight load, save, or page request
* Owned cancellation does not render an error banner or update disposed state
* Existing successful UI behavior remains unchanged

Context references:
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` (Lines 58-80) - Local owned-token disposal pattern

Dependencies:
* Phase 3 API behavior is stable

### Step 5.4: Validate independent completion slices

Run environment/API mapping tests, retention unit and SQL facts, and Web unit tests as separate watchdog invocations. These can execute in parallel only when they do not share the SQL fixture or build output.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~AccountPreferencesEnvironmentPolicyTests" --blame-hang --blame-hang-timeout 45s`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~OperationalRecordRetentionProcessorTests" --blame-hang --blame-hang-timeout 45s`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~AccountPreferencesTests" --blame-hang --blame-hang-timeout 45s`

## Implementation Phase 6: Documentation and Delivery Records

<!-- parallelizable: false -->

### Step 6.1: Update wiki behavior after implementation passes

Update all affected wiki pages to describe the corrected wire contract, mismatch behavior, partitioned cursor semantics, environment presentation, retention setting, migration lineage, bounded testing, and operator implications. Do not document intended behavior before the corresponding executable gate passes.

Files:
* `docs/wiki/api-reference.md` - HTTP 409 and cursor error contracts
* `docs/wiki/application-overview.md` - Corrected use-case behavior
* `docs/wiki/architecture.md` - Composition, gateway, and persistence boundaries
* `docs/wiki/operator-guide.md` - Test environment and retention operation
* `docs/wiki/runtime-behavior.md` - Reconciliation, telemetry, and cancellation behavior
* `docs/wiki/testing-and-quality.md` - Composition regression and bounded validation strategy

Success criteria:
* Wiki content matches implemented behavior and verified tests
* No page claims automated real-IG coverage
* Cross-page terminology consistently uses Test externally and Demo only for compatibility internals

Dependencies:
* Phases 1 through 5 pass

### Step 6.2: Correct durable delivery records

Create the remediation changes record and correct prior release claims without rewriting original requirement history. Resolve the known startup work item with the verified cause, preserve manual IG evidence as open, and remove any claim that final validation passed before it actually does.

Files:
* `.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md` - New implementation evidence and decisions
* `.copilot-tracking/changes/2026-08-17/trailing-stops-features-changes.md` - Correct overstated release claims and duplicate heading
* `.copilot-tracking/plans/logs/2026-08-17/trailing-stops-features-log.md` - Resolve WI-04 and retain WI-05

Success criteria:
* Delivery records distinguish historical intent, failed validation, remediation, and remaining manual evidence
* Every completed claim links to retained command/test evidence
* The prior plan/details remain historical records rather than being rewritten around defects

Dependencies:
* Step 6.1

## Implementation Phase 7: Final Validation

<!-- parallelizable: false -->

### Step 7.1: Run ordered focused gates

Run the bounded gates in this order and stop at the first failure: project builds; route composition; Application account preferences; IG gateway; EF store; retention; each SQL fact; direct API readiness; the exact synthetic-token API fact from Step 2.4; the exact AppHost-backed Web functional fact from Step 2.4; Web unit tests. Use the same 50-second retained-artifact watchdog for every command. Use `--no-restore`, and use `--no-build` only after a successful build.

Success criteria:
* Every command has an external process-tree deadline below 60 seconds
* Timeout is a failed gate, not grounds to increase the budget
* TRX files and bounded output tails are retained under `artifacts/quality-gates/trailing-stops-remediation/`
* No automated test calls real IG

Dependencies:
* Phases 1 through 6

### Step 7.2: Run solution validation

After all focused gates pass, build and test the solution serially with finite hang detection and process cleanup. Do not fail or split a healthy aggregate run solely because it exceeds 60 seconds; the focused project and fixture checks remain the bounded diagnostic gates.

Validation commands:
* `dotnet build TNC.Trading.Platform.slnx --no-restore`
* `dotnet test TNC.Trading.Platform.slnx --no-restore --no-build -m:1 --blame-hang --blame-hang-timeout 45s --logger "trx"`

Success criteria:
* Solution build succeeds
* All bounded automated tests pass, or unrelated/pre-existing failures are separately evidenced and do not mask this feature
* API and Web readiness complete within the bounded smoke checks

Dependencies:
* Step 7.1

### Step 7.3: Perform controlled manual IG Test verification

Using operator-supplied credentials entered outside logs and automation, capture sanitized evidence for GET and PUT account preferences in IG Test. Verify endpoint availability, `trailingStopsEnabled`, allowance behavior, confirmation readback, and rollback to the original preference. Do not expose secrets or run this check in default CI.

Discrepancy references:
* DR-06

Success criteria:
* Evidence is sanitized and identifies environment, timestamp, operation result, and rollback result
* Credentials, tokens, account identifiers, and raw sensitive payloads are absent
* Failure remains an explicit release limitation rather than being replaced by automated real-broker calls

Dependencies:
* Automated gates pass
* Approved operator window and credentials

### Step 7.4: Fix minor validation issues and report blockers

Apply only isolated corrections discovered by validation and rerun the failed focused gate before broadening. For schema, contract, or architecture failures that require a new direction, stop and create follow-on research/planning instead of widening remediation inline.

Success criteria:
* Final delivery record contains exact passed, failed, skipped, and manual gates
* Remaining blockers have owners and next actions

## Implementation Phase 8: Resolve Residual Migration Decision Gate

<!-- parallelizable: true -->

### Step 8.1: Inventory candidate migration deployment and release history

Query `__EFMigrationsHistory` read-only in every non-disposable CI, shared development, staging, and production database for both `20260830112227_SnapshotRepair` and `20260830120000_AddTrailingStopsPreferenceObservations`. Inspect every released Infrastructure assembly since Account Preferences development began for either migration ID and generated migration metadata. Record only sanitized present, absent, or inaccessible results. Treat any inaccessible environment or unavailable release artifact as blocking for migration cleanup.

Files:
* `.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md` - Record sanitized database and released-assembly evidence during implementation

Discrepancy references:
* DR-01
* DR-09

Success criteria:
* Every known non-disposable database has a present, absent, or inaccessible result for both candidate IDs
* Every relevant released Infrastructure assembly has a migration-metadata result
* No credentials, connection strings, account identifiers, or raw environment details enter source control
* Migration source files remain unchanged while any inventory result is inaccessible or unknown

Context references:
* `.copilot-tracking/research/2026-08-30/failing-tests-root-cause-research.md` (Lines 225-260) - Conflicting migration artifacts and deployment-history constraint
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-plan-verification-research.md` (Lines 176-215) - Verified decision gate and conditional lineage paths

Dependencies:
* Read-only access or sanitized evidence supplied by each environment and release owner

### Step 8.2: Select and record the canonical lineage path

Choose exactly one migration path from the inventory evidence. If neither candidate was applied or released, remove both candidates through the EF workflow and scaffold one canonical `AddTrailingStopsPreferenceObservations` migration. If `SnapshotRepair` was applied or released, preserve its ID and historical files, remove the inert duplicate only when release evidence permits, and add a forward corrective migration if the broker-only index is unintended. If the standalone ID appears in history or a released assembly, recover its exact released migration triplet before changing the lineage. Record whether the broker-only index serves an approved query or must be corrected forward.

Files:
* `.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md` - Record selected branch, evidence, index decision, and responsible owner

Discrepancy references:
* DR-09
* DD-04

Success criteria:
* One conditional path is selected from evidence rather than workspace assumptions
* Historical migration IDs and source are preserved whenever deployed or released
* The intended index set is explicit before EF artifacts are modified

Context references:
* `.copilot-tracking/research/2026-08-30/failing-tests-root-cause-research.md` (Lines 265-293) - Canonical, deployed, and recovery branches
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-plan-verification-research.md` (Lines 199-215) - Verified lineage decision matrix

Dependencies:
* Step 8.1 complete with no unknown or inaccessible evidence

## Implementation Phase 9: Correct Independent Test Contracts

<!-- parallelizable: true -->

### Step 9.1: Model the complete Application page result in the fake store

Update the Account Preferences handler test fake to return a complete `TrailingStopsPreferenceObservationPage` rather than reconstructing a page from rows with an always-null cursor. Configure the full-page test with the expected cursor and preserve assertions for observations, next cursor, invalid-cursor state, requested partition, and clamped page size. Keep cursor generation in the persistence adapter; do not change the handler or inward contract.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` - Represent and configure the complete page contract

Discrepancy references:
* DR-10

Success criteria:
* The fake can independently represent observations, a non-null next cursor, and invalid-cursor state
* The focused full-page test proves unchanged handler forwarding behavior
* Test names and requirement comments continue to satisfy repository test conventions

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs` (Lines 5-10) - Inward page contract
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs` (Lines 5-16) - Forwarding behavior
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-plan-verification-research.md` (Lines 45-72) - Verified fake mismatch and implementation boundary

Dependencies:
* None

### Step 9.2: Synchronize the Web disposal test with observed cancellation

Make the delayed HTTP content fake override the serialization path that receives a `CancellationToken`, await its gate with that token, and complete an explicit cancellation-observed task when cancellation occurs. Dispose the component, await that signal with a finite timeout, and use bUnit's eventual assertion mechanism for `IsLoading == false`. Retain the null-error assertion. Do not change production disposal unless the corrected harness proves the presenter remains loading after its cancellation continuation completes.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Make delayed content cancellation-aware and await the actual continuation

Discrepancy references:
* DR-11

Success criteria:
* The test explicitly proves cancellation reaches the HTTP content operation
* Assertions run only after cancellation has been observed and presenter state has settled
* Ten consecutive focused executions pass without sleeps or uncancellable gates
* Owned cancellation renders no user-visible error

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` (Lines 28-44) - Owned request-lifetime cancellation
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 10-27) - Cancellation handling and `finally` state reset
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-plan-verification-research.md` (Lines 74-105) - Verified race and cancellation-aware harness

Dependencies:
* None

### Step 9.3: Validate independent Application and Web corrections

Run the exact failing facts first. Then run the Application and Web unit projects. Repeat the Web fact ten times and stop on the first failure. These checks may run in parallel because they use separate projects and no shared infrastructure.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~GetTrailingStopsPreferenceObservations_ShouldReturnDeterministicPageCursor_WhenPageIsFull" --verbosity minimal`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError" --verbosity minimal`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --verbosity minimal`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --verbosity minimal`

Dependencies:
* Steps 9.1 and 9.2 for their respective validation streams

## Implementation Phase 10: Reconcile EF Lineage and SQL Test Contracts

<!-- parallelizable: false -->

### Step 10.1: Reconcile migration files, metadata, and model snapshot

Apply only the lineage branch selected in Step 8.2. Use EF tooling for removal and scaffolding on the unpublished path. On a deployed path, preserve historical migration code and add a forward correction when required. Review the main migration, generated designer, current model snapshot, migration discovery list, pending-model result, and generated SQL as one unit. Require one observation table creation, only intended indexes, and one history insertion for the selected new migration.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs` - Preserve or remove according to verified history
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs` - Keep generated metadata aligned with the selected historical path
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs` - Remove or recover according to released evidence
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs` - Align the snapshot with the selected lineage and intended index set
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<generated-corrective-migration>.cs` - Add only when the selected path requires a newly scaffolded or forward corrective migration

Discrepancy references:
* DR-09
* DD-04

Success criteria:
* EF discovers one intentional, continuous migration sequence
* The main migration, designer target model, current snapshot, and intended model agree
* Generated SQL contains no duplicate table operation and no unexplained index
* `has-pending-model-changes` exits successfully

Dependencies:
* Step 8.2

### Step 10.2: Recreate historical recovery fixtures through EF migrations

Replace the `EnsureCreatedAsync`-plus-table-drops setup with a database migrated to `20260727202238_InitialPlatformSchema`. Seed the data that the recovery scenario must preserve, then remove or recreate migration history according to the guarded-baseline case before invoking the startup initializer. Replace the hard-coded count of four with exact sequence equality between discovered and applied migrations and assert that the pending set is empty.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs` - Build authentic historical schema states and assert migration-set equality

Discrepancy references:
* DR-12

Success criteria:
* The no-history scenario begins from the actual initial migration schema
* Preserved seed data survives guarded baseline recognition and migration
* Successful recovery applies every discovered migration in order
* No migration remains pending after initialization
* Assertions do not contain a migration count that becomes stale when a migration is added

Context references:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs` (Lines 10-23, 44-119) - Guarded-baseline and migration behavior
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-plan-verification-research.md` (Lines 142-174) - Verified fixture mismatch and required assertions

Dependencies:
* Step 10.1 establishes the canonical sequence

### Step 10.3: Assert SQL Server GUID ordering and keyset continuity

Seed fixed GUIDs that sort differently under SQL Server and .NET. Query the expected IDs through SQL Server using the same timestamp-descending and `uniqueidentifier`-descending order as the adapter, then compare that sequence with concatenated keyset pages. Assert exact count, uniqueness, no omissions, no duplicates, and null cursor termination after the final page. Keep production ordering and seek predicates unchanged.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Replace .NET GUID ordering with provider-derived expected order and continuity assertions

Discrepancy references:
* DR-13

Success criteria:
* Expected order is produced by SQL Server rather than `Guid.CompareTo`
* Concatenated pages exactly match provider order
* Every seeded row appears once and final paging terminates
* No production query or cursor format changes

Context references:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs` (Lines 28-56) - Provider query and keyset predicate
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-plan-verification-research.md` (Lines 107-132) - Verified provider-order mismatch and test-only correction

Dependencies:
* Step 10.1 when migration IDs or fresh-database setup in the same test file change

### Step 10.4: Validate EF lineage and serialized Infrastructure integration tests

Run EF discovery, pending-model, and SQL-script checks before the exact migration recovery facts. Run the GUID paging fact separately, then all Account Preferences SQL tests and the entire Infrastructure integration project. Do not execute Infrastructure integration commands concurrently because the collection disables parallelization and shares fixture ownership.

Validation commands:
* `dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext --no-connect`
* `dotnet ef migrations has-pending-model-changes --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext`
* `dotnet ef migrations script 20260807103334_AddAccountDetailsSnapshots <selected-migration-id> --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext --no-transactions`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned|FullyQualifiedName~InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject" --verbosity minimal`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ListAsync_ShouldPageEqualTimestampsDeterministically_WhenRowsShareTimestamp" --verbosity minimal`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesSqlIntegrationTests" --verbosity minimal`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --verbosity minimal`

Dependencies:
* Steps 10.1 through 10.3

## Implementation Phase 11: Residual-Failure Final Validation

<!-- parallelizable: false -->

### Step 11.1: Run solution build and serial repository gate

After Phases 9 and 10 pass, build the solution once and run the complete suite serially. Preserve finite hang detection and process cleanup, but allow the healthy aggregate run to exceed 60 seconds because the reproduced baseline required 134.9 seconds. Use the focused project checks as the bounded diagnostic gates.

Validation commands:
* `dotnet build TNC.Trading.Platform.slnx --no-restore`
* `dotnet test TNC.Trading.Platform.slnx --no-restore --no-build -m:1 --verbosity minimal`

Success criteria:
* Solution build succeeds
* All 515 or more discovered tests pass with no skipped failures hidden by filtering
* No current failure remains in the Application, Web, or Infrastructure integration projects
* EF migration discovery and pending-model checks remain green after the full test run

Dependencies:
* Phase 9 focused and project tests pass
* Phase 10 EF and integration checks pass

### Step 11.2: Update evidence and report only genuine blockers

Record exact command outcomes and migration decisions in the existing remediation changes artifact. Correct the earlier completion claims for persistence, Web disposal validation, and final validation only after executable evidence exists. Apply isolated test-harness corrections discovered by validation and rerun the failed focused check before the repository gate. Escalate schema-history or production-behavior surprises into new research rather than widening this residual-failure phase.

Files:
* `.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md` - Record corrected validation evidence and remaining limitations

Success criteria:
* Durable status distinguishes prior completed implementation from reopened validation
* Every closed residual failure links to a passing focused and owning-project check
* Any unresolved external migration evidence retains a named owner and blocks release

## Dependencies

* .NET SDK 10.0.200 from `global.json`
* Docker with repository SQL Server and Keycloak prerequisites
* EF Core CLI compatible with the pinned SDK
* Read-only access to non-disposable migration histories
* External process-tree watchdog with bounded output capture
* API/product owner availability for contract decisions, with documented defaults when deferred

## Success Criteria

* API route construction and startup succeed with explicit account-preferences registrations
* Distributed fixture failures terminate with useful diagnostics in under 60 seconds
* IG payloads use `trailingStopsEnabled`, mismatches cannot report success, and telemetry is complete and sanitized
* Migration lineage supports both fresh and already-applied upgrade paths without duplicate table creation
* History is partitioned and rejects invalid cursors deterministically
* Environment, retention, and Blazor cancellation behavior match the approved contracts
* Wiki and delivery records match executable evidence
* Focused and solution validation pass without automated real-IG calls
