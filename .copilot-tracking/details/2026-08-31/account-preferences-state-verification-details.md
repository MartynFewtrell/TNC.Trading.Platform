<!-- markdownlint-disable-file -->
# Implementation Details: Account Preferences State Verification

## Context Reference

Sources: `.copilot-tracking/research/2026-08-31/account-preferences-state-verification-research.md` and `.copilot-tracking/research/subagents/2026-08-31/account-preferences-implementation-planning-research.md`.

## Selected Defaults

Use the defaults in the primary research Implementation Planning Addendum. In particular, require explicit target-account configuration, keep trailing stops configurable, retain `200 OK` for desired-state `PUT`, use separate retry/remediation commands, reject account mismatch, derive staleness at five minutes, and use bounded retry delays of 5 seconds, 30 seconds, 2 minutes, and 10 minutes. Persist no override. Do not implement an order-submission gate in this task.

## Implementation Phase 1: Diagnostic Baseline

<!-- parallelizable: true -->

### Step 1.1: Isolate current-state and history load failures

Refactor initial page loading so the current-state request and observation-history request have independent outcomes. A successful current state remains rendered when history fails. Add separate current-state and history error properties; do not retain the generic all-or-nothing state.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Split request handling and preserve state.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` - Represent independent error states.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Prove `200` current state plus failed history keeps the state visible.

Discrepancy references:
* Addresses DR-01 by improving diagnostics before the schema redesign.

Success criteria:
* Current-state success is not erased by history failure.
* The page identifies whether current state or history failed.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 19-66) - Current all-or-nothing load and independent save refresh.

Dependencies:
* None.

### Step 1.2: Preserve API failure detail and provider telemetry

Parse sanitized Problem Details in the Web API adapter and surface an operation-specific message without exposing provider payloads or secrets. Pass the injected event store into ordinary provider-failure recording in the current handler. Keep this correction covered while the provider-first handler still exists; preserve equivalent telemetry after Phase 4 replaces it.

Files:
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - Preserve status and safe Problem Details.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` - Forward the event store.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` - Assert ordinary failures append one sanitized event.

Success criteria:
* `429`, `502`, `503`, and `504` remain distinguishable to the presenter.
* A normal provider failure appends the expected sanitized event.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` (Lines 25-48) - Omitted optional event-store argument.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` (Lines 139-169) - Current `EnsureSuccessStatusCode` behavior.

Dependencies:
* Step 1.1 for final presenter wording only.

### Step 1.3: Validate diagnostic changes

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"` - Provider-failure telemetry.
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"` - Independent load rendering.

## Implementation Phase 2: Durable State Foundation

<!-- parallelizable: true -->

### Step 2.1: Define current-state application contracts

Create one top-level type per file for `AccountPreferencesCurrentState`, `AccountPreferencesDesiredStateChange`, `AccountPreferencesVerificationStatus`, `IAccountPreferencesCurrentStateStore`, and `IAccountPreferencesReconciliationLease`. The state carries broker environment, target account, nullable desired value and revision, desired actor/change time, nullable observed value/account/time, authentication snapshot ID, attempt identifier, verification status, last verified time, next retry time, retry count, sanitized failure summary, correlation ID, and concurrency evidence. Define store methods for SQL-only read, one atomic desired-state plus audit commit, idempotent authentication nudge, due-work claim/read, and revision-guarded reconciliation completion.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs` - Domain-neutral persisted projection.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesDesiredStateChange.cs` - Expected revision, actor, correlation, account, and value for atomic commit.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesVerificationStatus.cs` - Six factual statuses.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs` - Atomic and compare-and-set persistence port.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesReconciliationLease.cs` - Account-partitioned lease port.

Discrepancy references:
* Implements DD-01 defaults for unresolved policy values.

Success criteria:
* `Unconfigured` requires no inferred desired value.
* Store contracts can reject stale completion by desired revision.
* Failure summaries cannot carry credentials or raw provider bodies.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsRefreshLease.cs` (Lines 5-12) - Lease abstraction precedent.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs` (Lines 5-11) - Existing observation-only limitation.

Dependencies:
* Accepted defaults recorded in the Planning Log.

### Step 2.2: Implement EF state, audit, migration, and lease

Create the current-state entity/store and configure a unique partition by broker environment and explicit target account. Add a dedicated append-only `AccountPreferencesDesiredStateAuditEntity`; do not reuse the platform-specific `ConfigurationAuditEntity` and do not introduce a separately saving audit store. The audit stores state-row ID, immutable environment/account partition, previous and new values/revisions, occurrence time, actor, change type, and correlation ID. Use a monotonically increasing desired revision and conditional update for reconciliation completion. Stage the state and audit entities and call `PlatformDbContext.SaveChangesAsync` once so failure rolls back both. Configure a restricted relationship, unique state/revision audit index, and account/revision history index. Exclude desired-state audits from generic operational retention until a product retention policy is approved. Populate `AccountId` in observation application records and EF mappings. Implement the SQL lease with session-owned `sp_getapplock`, keyed by environment plus account. Generate and inspect the migration; do not backfill desired state from observations.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesCurrentStateEntity.cs` - Durable state row.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesDesiredStateAuditEntity.cs` - Typed append-only desired revision history.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs` - SQL read, commit, due work, and compare-and-set.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlAccountPreferencesReconciliationLease.cs` - Replica exclusion.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Entity configuration and indexes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs` - Account mapping.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/TrailingStopsPreferenceObservation.cs` - Account-bound observation contract.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` - Adapter registration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs` - Verify desired-state audits are intentionally excluded.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<timestamp>_AddAccountPreferencesCurrentState.cs` - Upgrade schema.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<timestamp>_AddAccountPreferencesCurrentState.Designer.cs` - Generated migration metadata.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs` - Updated model snapshot.

Success criteria:
* Empty and upgraded databases produce `Unconfigured`, never inferred intent.
* Desired state and its typed audit row commit atomically and survive context restart.
* A late completion cannot overwrite a newer desired revision.
* Only one replica holds the environment/account lease.
* Generic operational retention does not delete desired-state audit history.

Context references:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlAccountDetailsRefreshLease.cs` (Lines 9-59) - SQL application-lock ownership.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs` (Lines 53-126) - Same-context state and audit transaction.

Dependencies:
* Step 2.1 contracts.

### Step 2.3: Validate durable state

Create focused integration tests for bootstrap, migration upgrade, account partitioning, restart durability, typed actor/correlation audit, revisions, compare-and-set, due work, lease contention, and account-bound observation paging. Add an audit-insert command interceptor and prove an audit insert failure leaves the prior desired row and revision unchanged. Prove generic operational retention preserves desired-state audit history. Update fixture cleanup to drop the audit table before the current-state table.

Validation commands:
* `dotnet ef migrations add AddAccountPreferencesCurrentState --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --context PlatformDbContext` - Generate migration once.
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"` - SQL behavior.

## Implementation Phase 3: Account-Bound Provider Boundary

<!-- parallelizable: true -->

### Step 3.1: Define account-bound provider operations

Replace independent `GetAsync` and `UpdateAsync` calls with operation contracts that accept the configured target account and return observed account, preference value, non-secret attempt identifier, observation time, and typed outcome. Keep observe and remediation semantically distinct. Model unsupported response separately from transient failure.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesGateway.cs` - Inward-owned provider contract.
* New matching record files under `src/TNC.Trading.Platform.Application/Features/AccountPreferences/` - One top-level request/result type per file.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Provider adapter.

Success criteria:
* Every result identifies the active IG account and attempt without storing tokens.
* Account mismatch returns a typed rejection and performs no preference write.

Context references:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs` (Lines 32-79) - `currentAccountId` and token-context evidence.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` (Lines 19-76) - Current per-method sessions.

Dependencies:
* Selected account-mismatch and attempt-identity defaults.

### Step 3.2: Implement one-session observation and remediation

Create one IG v2 session per operation, validate `currentAccountId` against the configured account, and reuse its CST and security token. Observation performs one version 1 preference `GET`. Remediation performs initial `GET`, returns immediately when already equal, otherwise sends at most one `PUT` and one authoritative confirming `GET`. Retain safe token-refresh behavior for reads; never blindly replay a write after an ambiguous response.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Bound request sequence and parsing.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs` - Request ordering, account mismatch, parsing, and replay safety.

Success criteria:
* Remediation request order is exactly `POST session`, `GET preference`, optional `PUT preference`, `GET preference`.
* The same tokens are used across the operation.
* No write occurs before account and current value are known.

Dependencies:
* Step 3.1.

### Step 3.3: Validate provider changes

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"` - IG boundary behavior.

## Implementation Phase 4: Application Reconciliation and Remediation

<!-- parallelizable: false -->

### Step 4.1: Make reads and desired updates SQL-first

Change `GetAccountPreferencesHandler` to return the persisted projection without provider I/O or observation writes. Change `UpdateAccountPreferencesHandler` to validate target account and desired Boolean, atomically increment revision and audit, set `PendingVerification`, and make work due immediately. Return the committed projection. Keep the application layer responsible for policy.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` - Pure SQL query.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs` - Expanded projection.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesRequest.cs` - Target account, desired value, expected revision.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesResponse.cs` - Committed state.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesValidator.cs` - Input validation.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` - Atomic desired-state commit only.

Success criteria:
* Current-state reads perform no IG calls or writes.
* Desired updates survive IG outage and detect stale expected revisions.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/GetAccountDetailsHandler.cs` (Lines 5-39) - SQL-only query precedent.

Dependencies:
* Phase 2 store contract.

### Step 4.2: Implement observe-only reconciliation

Create request/response/handler types. Load due state and revision, acquire the environment/account lease, re-read state, call the bound observe operation, append a factual account-bound observation when available, classify equality, drift, unsupported, or failure, calculate bounded retry time, and persist only when revision still matches. Clear retry metadata on `InSync` or `Drifted`; retain last observed value on failure. Emit sanitized operational events.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesRequest.cs` - Reconciliation input.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesResponse.cs` - Typed outcome.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs` - Observe-only orchestration.
* Focused new test file under `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/` - Equality, drift, failure, unsupported, retries, leases, and stale completion.

Success criteria:
* Reconciliation never calls provider mutation.
* Stale completion is discarded without changing newer intent.
* Failure never invents or erases a known observation.

Dependencies:
* Phases 2 and 3 contracts.

### Step 4.3: Implement manual retry and explicit remediation

Manual retry marks eligible state due without changing intent. Remediation requires target account, desired revision, authorization, and correlation identity; it acquires the lease, rechecks revision, invokes the bound remediation operation, appends factual observations, and conditionally stores the final status. Reject stale revision with a conflict and reject account mismatch without mutation.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RetryAccountPreferencesVerificationRequest.cs` - Retry request.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RetryAccountPreferencesVerificationHandler.cs` - Durable due-work update.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesRequest.cs` - Revision-bound authorization input.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesValidator.cs` - Request validation.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs` - Explicit provider mutation orchestration.
* Focused handler test files under `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/` - Retry and remediation policy.

Success criteria:
* Retry performs no immediate provider mutation.
* Remediation rejects stale authorization before provider write.
* Every attempt is auditable and correlation-bound.

Dependencies:
* Steps 4.1 and 4.2.

### Step 4.4: Validate application policy

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"` - SQL-only query, desired commit, reconciliation, retry, and remediation.

## Implementation Phase 5: API and Web Contracts

<!-- parallelizable: false -->

### Step 5.1: Evolve the API contract

Keep the existing `GET` and `PUT` routes. Return an expanded SQL projection containing nullable target/desired/revision, desired change time, nullable observed value/account/time, status, derived stale flag, next retry, and safe failure summary. During one compatibility interval retain legacy `trailingStopsEnabled` as the nullable desired alias, while `applicationStatus` maps from verification status. Add Operator-only `POST /api/platform/account-preferences/verification-retry` and `POST /api/platform/account-preferences/remediation`. Return Problem Details for validation, stale revision, account mismatch, unavailable provider, and unsupported behavior. Split top-level HTTP records into matching files.

Files:
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Route registration and authorization.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Outcome mapping only.
* New matching response/request files under `src/TNC.Trading.Platform.Api/Features/Platform/` - Public records.
* `src/TNC.Trading.Platform.Api/Features/Platform/RetryAccountPreferencesVerificationEndpointHandler.cs` - Retry adapter.
* `src/TNC.Trading.Platform.Api/Features/Platform/RemediateAccountPreferencesEndpointHandler.cs` - Remediation adapter.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` - Contract and Problem Details.
* Existing API endpoint authorization and DI tests - Route policy and construction.

Discrepancy references:
* Implements DD-02 compatibility interval instead of an immediate breaking replacement.

Success criteria:
* `GET` cannot fail because IG is unavailable.
* `PUT` returns `200 OK` with the committed desired projection and pending status.
* Retry and remediation remain separate authorized operations.

Dependencies:
* Phase 4 response semantics.

### Step 5.2: Present desired and observed state independently

Update the Web client contracts and page model. Always render the durable-state area, including `Unconfigured`. Label values `Desired setting` and `Last observed at IG`; include target/observed account, times, freshness, and status. Use non-urgent `role="status"` for pending verification and accessible warning/alert treatment for drift, failure, unsupported, stale evidence, and account mismatch. Keep history failure local. Add retry and explicit apply controls with disabled/in-progress states and revision-bound requests.

Files:
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - Request execution and commands.
* New matching account-preference request/response record files under `src/TNC.Trading.Platform.Web/` - Extract touched top-level Web contracts from the client file.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Save, retry, remediation, and operation-specific feedback.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` - Separate desired, observed, editor, and status state.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Accessible display and controls.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - Existing visual-language status styling.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Every status and interaction.

Success criteria:
* Desired intent remains visible during provider failure and history failure.
* Drift displays both differing values and never silently applies one.
* Controls cannot submit stale revision or duplicate in-flight operations.

Dependencies:
* Step 5.1 JSON contract.

### Step 5.3: Validate API and Web contracts

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"` - HTTP mapping and authorization.
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"` - Status rendering and commands.

## Implementation Phase 6: Background and Closed-Box Integration

<!-- parallelizable: false -->

### Step 6.1: Process durable due work without blocking startup

Create a scoped tick runner and `BackgroundService` that waits for API startup, opens a DI scope, queries due configured state, and dispatches reconciliation. Register it beside existing supervisors without adding preference work to `Program.cs` blocking initialization. Desired updates become immediately due. Add an inward-owned `IAccountPreferencesVerificationNudge` operation carrying broker environment, account ID, authentication snapshot ID, and authentication time. It idempotently marks only matching configured state due and never calls IG or changes authentication health. Invoke it best effort after successful persistence in both live authentication paths: `PlatformAuthenticationReconciler.TransitionToActiveAsync` for automatic recovery and `TriggerManualAuthRetryHandler` after `IManualAuthRetryCommitter.CommitAsync` for HTTP manual recovery. If the dormant reconciler manual-retry path remains, invoke the same nudge there or remove that unused path in a separately reviewed refactor. Prevent tight provider polling and rely on persisted retry times plus replica lease.

Files:
* `src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs` - Hosted loop and scoped tick.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs` - Hosted service and operation registrations.
* `src/TNC.Trading.Platform.Api/Program.cs` - Confirm startup remains orchestration-only; modify only if registration composition requires it.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationRequest.cs` - Account and authentication-generation evidence.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationResponse.cs` - Typed no-state, mismatch, duplicate, or marked-due outcome.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesVerificationNudge.cs` - Shared post-authentication hook.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationHandler.cs` - Idempotent durable nudge.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - Automatic-success invocation.
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs` - Manual-success invocation after commit.
* Account-preference nudge, automatic-auth, and manual-auth unit test files - Matching, mismatch, duplicate, failure isolation, cancellation, and exact snapshot evidence.

Success criteria:
* API readiness does not wait for IG preference verification.
* Due state survives process restart and converges when IG recovers.
* Replica contention results in one provider attempt.
* Both automatic and HTTP manual authentication success nudge the exact persisted account/snapshot generation; nudge failure cannot reverse authentication success.

Context references:
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs` (Lines 8-75) - Scoped hosted-service pattern.
* `src/TNC.Trading.Platform.Api/Program.cs` (Lines 26-39) - Existing blocking startup boundary.

Dependencies:
* Phases 2 through 5.

### Step 6.2: Add closed-box convergence coverage

Introduce `Ig:AccountPreferencesBaseUrl` with production default `https://demo-api.ig.com/gateway/deal/`. Validate it as an absolute HTTP(S) URI with a trailing slash in Infrastructure, parse it through `AppHostSettings`, forward it from `AppHostEnvironmentWiring` as `Ig__AccountPreferencesBaseUrl`, and allow the shared process factory to merge that environment override before creating the distributed app. Add a shared WireMock provider that starts on a random loopback port and scripts v2 session creation plus preference `GET`/`PUT`, outage, drift, recovery, delay, and request ordering. Exercise: unconfigured SQL-only read, desired commit while provider is unavailable, API readiness, pending/failure persistence, provider recovery, eventual `InSync`, drift warning, manual retry, and stale remediation rejection. Use Aspire resource endpoints and public APIs; do not replace DI inside launched processes.

Files:
* `src/TNC.Trading.Platform.Api/appsettings.json` - Production account-preferences provider URL default.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` - Validate and apply the account-preferences client URL.
* `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs` - Parse the optional override.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` - Forward `Ig__AccountPreferencesBaseUrl` to API.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostSettingsTests.cs` - Absent and valid setting coverage.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs` - Environment forwarding coverage.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Shared WireMock provider.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Merge and retain test environment overrides.
* E2E and Functional `Authentication/AppHostProcessFactory.cs` and real-auth fixture files - Start, route, and dispose the provider double.
* E2E and Functional test project files - Reference `WireMock.Net` and link the shared provider source.
* New account-preferences test fixture and tests under `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/` - Closed-box workflow.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` - New table cleanup where applicable.

Success criteria:
* Closed-box test proves SQL display and startup availability while IG is down.
* Eventual convergence occurs without a browser request causing provider I/O.
* Invalid base URLs fail composition, and only the account-preferences client is redirected.

Dependencies:
* Step 6.1 and controllable provider endpoint configuration.

### Step 6.3: Add Playwright operator-flow coverage

After API and UI wording stabilizes, automate desired/observed labels, drift warning, retry, explicit remediation confirmation, and successful convergence using accessible roles and labels. Keep this focused on browser behavior; leave concurrency proofs to application and SQL tests.

Files:
* New account-preferences test under `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/` - Browser workflow.

Success criteria:
* The operator can distinguish intent from observation and complete the approved retry/remediation flow.

Dependencies:
* Steps 5.2 and 6.2.

## Implementation Phase 7: Documentation Alignment

<!-- parallelizable: true -->

### Step 7.1: Update implemented behavior in the wiki

Replace live-IG-current-state claims with SQL intent and IG observation semantics. Document schema bootstrap, status vocabulary, HTTP contracts and compatibility alias, triggers, retry/remediation behavior, account mismatch, startup independence, operator diagnostics, provider-double setup, migration, and test coverage. Verify documentation against final code before closing the phase.

Files:
* `docs/wiki/api-reference.md` - Expanded projection, commands, status, conflicts, and compatibility.
* `docs/wiki/application-overview.md` - State store and reconciler ownership.
* `docs/wiki/architecture.md` - Authority split, session binding, leases, and revisions.
* `docs/wiki/local-development.md` - Migration, provider double, diagnostics, and manual checks.
* `docs/wiki/operator-guide.md` - Unconfigured, warnings, retry, and remediation.
* `docs/wiki/runtime-behavior.md` - Triggers, durable due work, startup, and transitions.
* `docs/wiki/testing-and-quality.md` - Unit, SQL, Aspire, and Playwright coverage.

Success criteria:
* No wiki page continues to claim the current page reads IG synchronously.
* Operator guidance identifies desired versus observed values and safe recovery actions.

Dependencies:
* Accepted API/UI contracts; drafting can proceed in parallel with Phase 6.

### Step 7.2: Preserve the future trading-gate boundary

Document that future order submission must reject work unless verification is fresh, `InSync`, and bound to the same desired revision, target account, and applicable session generation. State explicitly that startup verification alone is not trade readiness and this task adds no order gate or override.

Files:
* `docs/wiki/architecture.md` - Future application-boundary rule.
* `docs/wiki/runtime-behavior.md` - Invalidation and recheck triggers.

Success criteria:
* Documentation does not imply authentication-active means trade-ready.

Dependencies:
* None beyond selected research recommendation.

## Implementation Phase 8: Final Validation

<!-- parallelizable: false -->

### Step 8.1: Run full project validation

Execute in order from the repository root with Docker-backed infrastructure running:
* `dotnet build TNC.Trading.Platform.slnx`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test TNC.Trading.Platform.slnx --no-build`

Inspect generated migration SQL for empty database and existing-observation upgrades. Confirm no migration populates desired state from observations.

### Step 8.2: Fix minor validation issues

Apply isolated corrections for compile, formatting, migration, and test defects, then rerun the smallest failing command before repeating full validation.

### Step 8.3: Report blocking issues

Record provider-contract, product-policy, migration-safety, or broad regression blockers that require additional research. Do not widen into order submission, account switching, notification delivery, or override design.

## Dependencies

* .NET SDK from `global.json`.
* Docker-backed Aspire infrastructure.
* EF Core CLI.
* Test-only controllable IG HTTP endpoint.

## Success Criteria

* SQL intent remains readable during IG failure and all verification/remediation state transitions are account- and revision-safe.
* Focused suites, closed-box workflows, full build, and full solution tests pass.
* Wiki guidance matches the implemented system.