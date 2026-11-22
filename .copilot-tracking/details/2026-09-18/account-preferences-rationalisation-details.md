<!-- markdownlint-disable-file -->
# Implementation Details: Account Preferences Rationalisation

## Context Reference

Sources: user request; `.copilot-tracking/research/2026-09-18/account-preferences-rationalisation-research.md`; `.copilot-tracking/research/subagents/2026-09-18/account-preferences-implementation-path-research.md`.

## Implementation Phase 1: Durable command contracts and persistence

<!-- parallelizable: false -->

### Step 1.1: Define journal, save, and Check status application contracts

Create cohesive account-preferences types for the operation journal and explicit commands. The save command accepts requested Boolean, expected revision, and idempotency key. The Check status command accepts no browser data. Resolve target account through `IPlatformIgLoginSnapshotStore.GetLatestSnapshotAsync` and actor through `PlatformAuthAuditEventResolver.ResolveUserName` at the API boundary or command adapter.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesOperation.cs` - Journal entity/value types and operation phase.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesOperationStore.cs` - Journal port with idempotency lookup/start/finalisation/recovery operations.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesCommand.cs` - Minimal IG-first save contract/result.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusCommand.cs` - Body-free compare-and-converge contract/result.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs` - New handler replacing active durable SQL-first save ownership.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs` - New handler replacing browser-visible retry/remediate orchestration.

Discrepancy references:
* DD-01: Use `GetLatestSnapshotAsync`, the verified current port name, rather than the earlier research shorthand.

Success criteria:
* Public contracts contain no browser-supplied account ID or actor.
* Matching idempotency input resolves the existing operation; incompatible reuse returns a typed conflict before IG I/O.
* Unconfigured state bypasses IG I/O; mismatch, held lease, and stale revision bypass gateway I/O.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` (Lines 14-75) - Existing durable and compatibility save branches.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs` (Lines 12-29) - Existing lease, reread, revision, and failure persistence control flow.
* `src/TNC.Trading.Platform.Application/Services/IPlatformIgLoginSnapshotStore.cs` (Lines 5-12) - Server-side snapshot contract.

Dependencies:
* Existing gateway, current-state store, observation store, account lease, snapshot store, and clock abstractions.

### Step 1.2: Add journal persistence and atomic finalisation

Map a new operation-journal entity with a unique idempotency key plus account/environment, baseline revision, requested value, actor, correlation, phase, and timestamps. Add a migration. Extend the state persistence transaction so confirmed save writes desired state/audit, confirmed observation, `InSync` projection, and completed journal together. Persist `RemoteApplied` before finalisation so unknown outcomes can be recovered. Keep journal retention configurable through maintenance, not command behavior.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Add DbSet and mapping beside current state/audit.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesOperationStore.cs` - EF operation-store implementation.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs` - Add atomic journal-aware completion and successful observation persistence.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<timestamp>_AddAccountPreferencesOperations.cs` - Generated EF migration and model snapshot update.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` - Register journal store.

Discrepancy references:
* DR-01: Journal retention is deliberately deferred to the existing database maintenance policy; command behavior must not assume a retention duration.

Success criteria:
* Unique-key contention produces deterministic replay/conflict behavior.
* SQL transaction cannot expose a completed journal without its desired/audit/observation/projection records.
* Remote-applied entries remain recoverable after finalisation failure.

Context references:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` (Lines 165-194) - Existing persistence mappings.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs` (Lines 11-47) - Existing atomic desired write and conditional reconciliation.

Dependencies:
* Step 1.1 contracts.

### Step 1.3: Implement confirmed save and convergence semantics

In Save preferences, start/find the journal, acquire the account lease, reread and validate revision, then invoke the existing account-bound gateway read/conditional-write/readback flow. Mark the journal remote-applied after confirmed readback, atomically finalise SQL, and return the persisted projection. Return `503` with `Save outcome unknown` when the remote outcome or SQL finalisation remains indeterminate; do not retry a remote write.

In Check status, return unconfigured state without IG I/O. Verify snapshot account and state account match before acquiring the lease and rereading revision. Observe IG: on equality append confirmed observation and complete `InSync`; on drift call read/conditional-write/readback, then append available successful observations and finish `InSync`. On any unconfirmed result retain desired/last-confirmed state, persist `VerificationFailed` with category-level bounded warning/retry data, and return `200` plus that projection. Resolve a prior remote-applied journal through observation before normal comparison.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs` - Save sequence.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs` - Check sequence and recovery.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs` - Extend only if a projection field is required to represent unresolved journal state.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs` - Register handlers and ports.

Success criteria:
* The selected IG value is read back before SQL considers it confirmed.
* A successful status check with drift returns an `InSync` projection and no transient drift warning.
* Session-account mismatch returns exactly `IG session account did not match the configured target account` and never invokes preference I/O.

Context references:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` (Lines 12-55 and 105-176) - Account binding and readback adapter behavior.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs` (Lines 7-24) - Existing explicit remediation ownership.

Dependencies:
* Steps 1.1 and 1.2.

### Step 1.4: Validate durable behavior

Add isolated handler tests using fakes/fixed time and SQL integration tests using the existing database fixture. Include traceability comments and `MethodName_StateUnderTest_ExpectedResult` names.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs` - IG-first save, replay, conflict, equal-value, and unknown-outcome tests.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs` - Unconfigured, mismatch, lease/revision, equality, drift/remediation, failure, and history-order tests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Journal uniqueness, recovery, atomic finalisation, and concurrent writes.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --configuration Release --no-restore` - Application command tests.
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release --no-restore` - SQL durability tests.

## Implementation Phase 2: API and operator UI migration

<!-- parallelizable: false -->

### Step 2.1: Migrate API contracts and endpoint mappings

Keep the projection GET unchanged and SQL-only. Change PUT to require `Idempotency-Key` and accept only `trailingStopsEnabled`/`expectedRevision`. Add body-free `POST /api/platform/account-preferences/check-status`. Resolve the account from the latest successful snapshot and actor from claims. Remove browser-facing retry/remediation routes and their DTOs after all callers migrate. Map conflicts to `409`, indeterminate save to `503`, malformed input to Problem Details, and processed verification failure to `200` plus its projection.

Files:
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Route mapping and dispatch.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - DTOs, projection mapping, and typed responses.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs` - Reuse existing actor resolution without client fallback.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs` - Header, DTO, ownership, response, and route tests.

Success criteria:
* No account or actor field is accepted from page JSON.
* Check status has no request body and cannot make broker choices from user-controlled input.

Context references:
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` (Lines 54-75) - Current route owner.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` (Lines 8-19 and 105-107) - Current projection and browser-authoritative DTOs.

Dependencies:
* Phase 1 handlers and persistence.

### Step 2.2: Rationalise the Blazor operator workflow

Replace the upper page surface with `Trailing stops`, the existing red warning region directly below it, desired `Trailing stops`, and `Last confirmed` only when IG has confirmed a value. Format `VerificationFailed` as `Verification failed`. Remove target-account and standalone status displays. Replace Retry verification and remediation with Check status. Render Enabled, Disabled, and Save preferences horizontally in `Trailing stops configuration`; remove `Last confirmed: Disabled`. Apply both command projections through the existing presenter refresh path, retain command-local feedback separately, and disable only the active command controls.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Layout, labels, warning position, and controls.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` - Display state/command activity as needed.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Save and Check status projection application.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - Idempotency header, expected revision, and body-free check client call.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Rendering and interaction coverage.

Success criteria:
* Both commands refresh the displayed projection without page reload.
* Observed-history paging remains independent and read-only.
* UI recognizes canonical `Pending` rather than the obsolete `PendingVerification` value.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` (Lines 14-125) - Existing UI and callbacks.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 54-114) - Existing projection and legacy-action flow.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` (Lines 149-176) - Existing client calls.

Dependencies:
* Step 2.1 API shape.

### Step 2.3: Validate API and UI contracts

Execute endpoint and bUnit partitions after the coordinated migration. Assert the new headings, warning placement, human-readable verification state, absence of old account/status text, horizontal configuration controls, active-command disabling, and projection updates.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --configuration Release --no-restore` - Endpoint contracts.
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --configuration Release --no-restore` - bUnit page behavior.

## Implementation Phase 3: Provider, functional, and browser coverage

<!-- parallelizable: false -->

### Step 3.1: Correct the controllable IG provider contract

Change the double to serve `GET` and `PUT /gateway/deal/accounts/preferences`, use `trailingStopsEnabled`, and create the production-required session response including `currentAccountId`, `CST`, and `X-SECURITY-TOKEN`. Add a contract test that exercises the real `IgAccountPreferencesGateway` against the double before higher-level tests depend on it.

Files:
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Provider routes, payload, session, controls, and reset hook.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/Integrations/IgAccountPreferencesGatewayTests.cs` - Adapter/double contract test, or the nearest existing IG gateway test file.

Success criteria:
* The controlled provider proves production route, payload, account binding, and session-header compatibility.

Context references:
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` (Lines 107-125) - Obsolete provider behavior.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` (Lines 126-176) - Production adapter contract.

Dependencies:
* None; this phase can proceed alongside Phase 4 after Phase 2 contract stabilisation.

### Step 3.2: Replace functional command scenarios

Use the corrected provider to verify IG-first save/readback and Check status equality, drift correction, failed remediation, account mismatch, projection responses, server-derived identity, `409`, `503`, history, and journal recovery. Do not post account/actor fields from test clients.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - Closed-box account-preferences scenarios.

Success criteria:
* Functional tests observe real endpoint/provider sequences and durable projections.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` (Lines 15-79) - Existing SQL-first client-authority scenarios.

Dependencies:
* Step 3.1 and Phase 2. After this provider contract is validated, Steps 3.2 and 3.3 may execute in parallel because they modify separate projects.

### Step 3.3: Stabilise the browser workflow

Give each scenario fixture-owned reset/setup state. Use `ClickAsync` for normal tab activation and assert `aria-selected` before history/pagination assertions. Retain one keyboard test only for documented Radzen behavior after ensuring it truly activates the tab. Add browser flows for confirmed save refresh, Check status equality, automatic drift correction, exact account-mismatch warning, failed-remediation warning, and read-only history paging.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Operator browser scenarios.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Scoped SQL reset/setup and provider controls.

Success criteria:
* Scenarios do not depend on SQL left by another test.
* Each activated tab is verified selected before content assertions.
* The focused AccountPreferences E2E filter passes.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` (Lines 22-116) - Failing tab and unconfigured assumptions.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` (Lines 16-22) - Shared fixture boundary.

Dependencies:
* Step 3.1 and Phase 2. This step may execute in parallel with Step 3.2 once the provider contract is stable.

## Implementation Phase 4: Wiki alignment

<!-- parallelizable: true -->

### Step 4.1: Update implementation documentation

Describe user-visible operation semantics and reproducible local behavior without exposing IG credentials or unnecessary provider protocol internals.

Files:
* `docs/wiki/operator-guide.md` - Trailing stops terminology, warnings, commands, and layout.
* `docs/wiki/runtime-behavior.md` - SQL projection, IG authority, convergence, IG-first save, and recovery.
* `docs/wiki/api-reference.md` - Minimal save, idempotency, Check status, ownership, `409`, and `503` contracts.
* `docs/wiki/architecture.md` - Operation ownership, lease/revision guard, history, and journal boundary.
* `docs/wiki/application-overview.md` - Explicit broker commands over SQL projection.
* `docs/wiki/local-development.md` - Correct controllable-provider contract and repeatable exercises.
* `docs/wiki/testing-and-quality.md` - Boundary-specific coverage and keyboard/fixture rules.

Success criteria:
* Wiki statements match implemented routes, state transitions, and page vocabulary.
* Local-development instructions can reproduce mismatch and recovery safely.

Context references:
* `docs/wiki/operator-guide.md` (Lines 565-589), `docs/wiki/runtime-behavior.md` (Lines 728-747), and `docs/wiki/api-reference.md` (Lines 551-574) - Primary behavior documentation anchors.

Dependencies:
* Final API and UI contracts from Phase 2; content drafting may begin in parallel with Phase 3.

## Implementation Phase 5: Final validation

<!-- parallelizable: false -->

### Step 5.1: Run full affected validation

Execute the repository-partition validation after all phases have merged. Use `-m:1` only if the documented local MSBuild child-node issue reoccurs.

Validation commands:
* `dotnet restore TNC.Trading.Platform.slnx`
* `dotnet build TNC.Trading.Platform.slnx --no-restore --configuration Release`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AccountPreferencesE2ETests"`

### Step 5.2: Fix minor validation issues

Apply only isolated corrections directly related to the planned feature and rerun the smallest failed partition.

### Step 5.3: Report blocking issues

Record infrastructure failures, unresolved product-policy questions, or nonlocal regressions in the planning log and create follow-on planning before undertaking broad remediation.

## Dependencies

* Docker-backed Aspire environment for SQL, Keycloak, and controlled-provider tests.
* Existing authenticated IG login snapshot for broker account resolution.
* Entity Framework migration generation and application workflow.

## Success Criteria

* All planned command, UI, provider, and wiki changes are implemented with the stated ownership boundaries.
* The focused browser E2E suite and affected project partitions pass in Release configuration.