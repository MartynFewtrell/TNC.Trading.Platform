<!-- markdownlint-disable-file -->
# Release Changes: Environment Model Rationalisation

**Related Plan**: environment-model-rationalisation-plan.instructions.md
**Implementation Date**: 2026-09-21

## Summary

Implementation tracking for the environment model rationalisation delivery.

### Phase 6.1/6.2 completion (2026-09-21)

* Added Infrastructure EF bundle build/run scripts under `infra/migrations`.
  The run script requires an explicit Test/Live deployment environment and SQL
  connection and never falls back to application configuration or invented
  credentials.
* Updated `PlatformStartupInitializer` so Desktop/Development retain local
  migration while Test/Live replicas skip `MigrateAsync`; release deployment
  owns the one-shot bundle execution.
* Added migration bundle deployment integration coverage against the owned SQL
  upgrade fixture and focused AppHost portable-contract assertions.
* Documented Desktop migration, Test/Live migration ownership, the external SQL
  and secret-reference contract, per-environment database isolation, health
  endpoints, and the Azure Container Apps portability boundary in the Wiki.
* Focused validation passed: migration bundle build, five filtered
  `MigrationBundleDeploymentTests`, eight startup initializer tests, and twelve
  AppHost environment tests. The requested `pwsh` command was unavailable on
  this Windows host; the same script completed successfully with Windows
  PowerShell 5.1.

## Changes

### Added

* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentOptions.cs` - Added validated deployment-owned platform options.
* `src/TNC.Trading.Platform.Application/Configuration/IPlatformEnvironmentContext.cs` - Added the immutable platform context contract.
* Focused platform-context regression tests - Added validation and AppHost Desktop wiring coverage.
* Broker catalog application contracts, EF entities, and migration - Added catalog, selection, defaults, and broker-scoped profile schema with fixed bootstrap data.

### Modified

* `docs/wiki/README.md` - Reconciled Wiki navigation with the delivered environment model pages.
* `docs/wiki/application-overview.md` - Documented immutable platform ownership, broker catalog persistence, partitioning, defaults, selection, and retention.
* `docs/wiki/architecture.md` - Documented ownership boundaries, catalog relationships/defaults, capability fail-closed behavior, portability, and migration topology.
* `docs/wiki/runtime-behavior.md` - Documented startup validation, restart-applied selection, lifecycle/retirement, purge boundaries, retention, and migration behavior.
* `docs/wiki/operator-guide.md` - Documented operator and administrator workflows, roles, acknowledgements, selection conflicts, credentials, and retirement safeguards.
* `docs/wiki/api-reference.md` - Reconciled catalog, credential, status, selection, and retirement routes with authorization, conflict, acknowledgement, and secrecy behavior.
* `docs/wiki/local-development.md` - Documented Desktop platform wiring, Aspire topology, bootstrap behavior, local secrets, and migration ownership.
* Application configuration contracts - Added `Desktop` and `Development` and removed platform mutation inputs.
* API and Web startup - Added startup validation for `Platform:Environment`.
* AppHost composition - Injected `Platform__Environment=Desktop` into API and Web.
* Infrastructure and API configuration paths - Removed platform mutation from bootstrap and generic update handling.
* `PlatformDbContext` and SQL integration tests - Added restrictive catalog relationships, normalized-name uniqueness, concurrency mapping, and migration/backfill assertions.
* Infrastructure integration test contracts - Updated fixtures for the immutable platform context and current update contract.
* Broker lifecycle application/API/Web slices - Added catalog creation, write-only credentials, restart-applied selection, authorization, acknowledgement, secret-free projections, Settings selection, pending-aware shell state, and administrator Broker Environments UI.
* Web unit fixtures - Updated status/catalog request sequences and pending/applied shell expectations.
* Applied broker runtime boundary - Added applied catalog context resolution, catalog-scoped credential lookup support, and capability-first IG gateway guards.
* Infrastructure unit tests - Updated stale Phase 1 contract usage; 123 tests pass.
* Catalog-ID persistence migration - Added canonical backfill, required broker-scoped IDs, restrictive foreign keys, and catalog-specific indexes while retaining legacy broker columns.
* IG gateway tests - Added unavailable-catalog coverage for credential and HTTP suppression.
* Runtime catalog-ID routing - Converted observations, Account Details snapshots, IG proof data, Account Preferences current state/audits, and same-kind isolation paths to valid applied catalog IDs.
* SQL migration and fixtures - Corrected dependency ordering, historical index guards, restrictive FK argument binding, cleanup order, and canonical catalog-ID fixture usage.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260921140905_AddCatalogIdsToBrokerScopedDataModel.cs` - Guarded historical index removal with an existence check.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260921154503_AddBrokerEnvironmentRetirement.cs` - Prevented the retirement migration from repeating catalog-ID schema alterations owned by the preceding migration.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Added migration-chain assertions for the operations index and retirement migration history.
* Retirement policy documentation - Approved and documented the table-by-table purge/retain boundary, including purging both current and retained login snapshots without cascade deletes.
* Retirement implementation - Added administrator-only preview and retirement endpoints with normalized-name confirmation, rowversion/preview binding, one-time server tokens, selected/applied safeguards, transactional targeted purge, retained-history counts, and retirement audit persistence.
* Retirement validation - Added migration-schema assertions and a SQL integration regression for one-time token replay protection.
* Retirement confirmation hardening - Required the submitted confirmation text to equal the server-issued normalized name exactly, including casing, at both the Web control and transactional server guard.
* Retirement confirmation strictness - Removed implicit whitespace trimming so the server and Web control require the typed value to equal the issued normalized name byte-for-byte.
* WI-05 migration-chain correction - Added existence-checked handling for the historical `AccountPreferencesOperations` index and removed duplicate catalog-column/index alteration work from the retirement migration.
* WI-05 Infrastructure regression coverage - Asserted the canonical operations index and successful retirement migration application; rerunning the full Infrastructure integration suite passed 33 tests.
* `test/Shared/Authentication/ManagedAppHostFixture.cs` - Updated the Aspire authentication fixture to use the seeded `IG DEMO` catalog ID for broker-scoped cleanup and inserts, and to default fixture-owned SQL/Keycloak state to session lifetime.

### Removed

## Additional or Deviating Changes

* Existing platform fields remain in read-only snapshots and historical persistence models for compatibility; they are no longer mutable configuration inputs or authoritative startup values.
* Existing nullable warnings remain in unrelated API/Web tests.
* The existing obsolete parameterless `MsSqlBuilder` warning remains in the SQL fixture.
* Aspire-backed broad validation was affected by transient disposed-service-provider/timeout behavior; deterministic API and Web unit coverage passed.
* Phase 4.2/4.3 remains open: dependent catalog-ID persistence migration and dual-catalog isolation/gateway-spy coverage were not completed.
* No Phase 4 blockers remain. Existing unrelated warnings remain.
* Phase 5.3 distributed validation remains blocked by Aspire/authenticated-navigation fixture failures in API integration and Web E2E Release runs; the WI-05 Infrastructure migration-chain blocker is resolved.
* WI-06 focused validation (2026-09-21) reproduced the existing fixture failures without changing product behavior:
  * `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --configuration Release --no-restore` failed during `SyntheticTokenIntegrationTestFixture.InitializeAsync` while `WaitForApiReadinessAsync` polled `/health/ready`; the failures surfaced as `TaskCanceledException`.
  * `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PlatformDashboardAuthenticationE2ETests"` failed after authenticated navigation reached `/status`; the expected `Platform status` heading was not rendered.
  * `test/Shared/Authentication/ManagedAppHostFixture.cs` now defaults fixture-owned SQL and Keycloak resources to non-persistent session lifetime. This was supported by fixture-owned Docker state: stale three-hour-old persistent `sql-1d2c2e33`, `keycloak-1d2c2e33`, and `mailpit-1d2c2e33` containers were present after their creator process had exited. The stale containers were removed before the isolated rerun.
  * The isolated API rerun still failed 26/26 with `TaskCanceledException` during `/health/ready` polling (duration 1m22s before the correction and 2m36s after the attempted extended readiness window; the 120-second experiment was reverted because it did not correct the failure).
  * The isolated Web E2E rerun still failed 1/1 after Keycloak sign-in reached `/status` without rendering `Platform status`.
  * WI-06 remains blocked. No authentication assertions were weakened, no tests were skipped, and no product behavior was changed.
  * Phase 7.1 intentionally changed Wiki source-of-truth pages only; governance assets and Phase 8 validation were not started.
* Phase 7.2 governance updates:
  * `.github/instructions/work-packages.instructions.md` now requires a scoped Wiki-impact assessment for environment, authentication, configuration, accounts, topology, operator workflow, local development, and testing changes, including per-work-item decisions and lifecycle retention/audit coverage.
  * `.github/prompts/generate-delivery-plan.prompt.md` and `.github/templates/delivery-plan.template.md` make the assessment input, per-work-item decision, lifecycle retention/audit review, and validation evidence mandatory.
  * `.github/work-package-workflow-usage.md` now points to maintained prompt assets instead of stale, missing `.github/agents` references.
  * Validation passed: `git diff --check`, all 33 workflow links resolve, and no stale `.github/agents` references remain in the updated workflow assets. No Wiki pages or Phase 8 validation were changed.

* Phase 8 final validation (2026-09-21):
  * `dotnet restore TNC.Trading.Platform.slnx` passed; `dotnet build TNC.Trading.Platform.slnx --no-restore --configuration Release` passed; `pwsh infra/migrations/build-migration-bundle.ps1 -Configuration Release` could not start because `pwsh` is unavailable, and the same script completed successfully with Windows PowerShell 5.1.
  * `MigrationBundleDeploymentTests` passed 5/5; Application, Infrastructure, API, Web, and AppHost unit suites passed 144/144, 129/129, 76/76, 141/141, and 43/43 respectively.
  * Architecture integration passed 13/13 and Infrastructure integration passed 38/38.
  * API integration remained blocked at 0/26 by the known Aspire API readiness fixture (`TaskCanceledException` while polling `/health/ready`); no product change was made.
  * The plan-caused `ManagedAppHostFixture` catalog-ID correction was applied. A post-correction Web Functional rerun still timed out during real authentication waiting for the platform session cookie, and the previously executed Web E2E rerun still failed after sign-in when the protected `Platform status` heading did not render. These remain distributed fixture/authentication blockers, not reasons to weaken tests.
  * The seven affected Wiki pages were reviewed against final immutable platform/catalog-ID behavior, retirement/purge boundaries, migration ownership, API contracts, and local topology. Research acceptance criteria are evidenced by implementation, tests, and documentation; executable IG Live/Training adapter semantics and distributed fixture stabilization remain follow-on work.

## Release Summary

Phase 8 Final Validation is complete for the previously delivered implementation. Deterministic validation passed; distributed Aspire-dependent suites remain blocked by known fixture failures. Earlier plan items remain marked according to the plan and are not re-scoped by this Phase 8 execution.

**Unit and Deterministic Integration Tests**: All passed
* Application UnitTests: 144/144
* Infrastructure UnitTests: 129/129
* Api UnitTests: 76/76
* Web UnitTests: 141/141
* AppHost UnitTests: 43/43
* Deterministic validation total: 589/589 passed
* Architecture IntegrationTests: 13/13 passed
* Infrastructure IntegrationTests: 38/38 passed
* MigrationBundleDeploymentTests: 5/5 passed

**Distributed Fixture Tests**: Partial results
* Api IntegrationTests: 0/26 passed - pre-existing Aspire fixture blocker (resource 'api' failed to become healthy); documented in DD-05
* Web FunctionalTests: Blocked after the isolated BrokerEnvironmentId fixture correction by real-authentication/session-cookie timeout
* Web E2ETests: Blocked after sign-in when the protected `Platform status` heading did not render

**Isolated Plan-Caused Corrections Applied**:
* `test/Shared/Authentication/ManagedAppHostFixture.cs::ResetAccountPreferencesAsync()` - Updated INSERT statement to include required `BrokerEnvironmentId` foreign key, looked up from BrokerEnvironments catalog by NormalizedName 'IG DEMO'. No production code changes.

**Build Results**: All successful
* `dotnet restore TNC.Trading.Platform.slnx` - All 16 projects restored
* `dotnet build TNC.Trading.Platform.slnx --no-restore --configuration Release` - All projects built
* Migration bundle: `artifacts/migrations/tnc-trading-platform-migrations.exe` created successfully (Windows PowerShell fallback, pwsh not installed)

Phase 8 validation is complete with deterministic suites all passing (589/589 tests). API integration and Web distributed suites remain blocked by Aspire readiness/authentication fixtures and are tracked as WI-06; no deprecated generic environment mutation was detected. The seven affected Wiki pages and research acceptance criteria were reviewed in Step 8.3.
