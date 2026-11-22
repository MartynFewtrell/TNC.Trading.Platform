<!-- markdownlint-disable-file -->
# Release Changes: Account Details

**Related Plan**: account-details-plan.instructions.md
**Implementation Date**: 2026-08-07

## Summary

Implementation is substantially complete. Phases 1 through 4 are complete; Phase 5 is partial because hosted AppHost/Playwright coverage was not available in the current test surface.

## Changes

### Added

* `src/TNC.Trading.Platform.Application/Features/AccountDetails/` - Application-owned Account Details contracts, handlers, records, outcomes, cursors, and ports.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsContractTests.cs` - contract and trading-day tests.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs` - handler, coalescing, lease, cursor, validation, and diagnostic tests.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs` - dedicated fresh-session IG accounts adapter and response validation.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountDetailsRetrievalEntity.cs` - immutable retrieval header.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountDetailsAccountEntity.cs` - immutable retrieval child account.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260807103334_AddAccountDetailsSnapshots.cs` - additive schema migration.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs` - provider adapter coverage.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfAccountDetailsSnapshotStoreTests.cs` - persistence, query, index, and retention coverage.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - protected Account Details history page.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs` - page state and refresh preservation.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsViewModels.cs` - Web presentation models.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - bUnit page and state coverage.
* `docs/wiki/api-reference.md` - Account Details API and configured IG test-account wording.
* `docs/wiki/architecture.md` - immutable IG test-account snapshot terminology.

### Modified

* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs` - configured-time-zone trading-day calculation.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - post-durable-success automatic capture hook.
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs` - post-commit automatic capture hook.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs` - durable-success ordering and failure isolation test.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs` - reconciler durable-success ordering and failure isolation test.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountDetailsEndpointMappingTests.cs` - typed Account Details outcome and conflict mapping coverage.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Account Details mappings and DbSets.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs` - Account Details retention.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs` - SQL lease, rollback, migration, and index coverage.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountDetailsEndpointMapping.cs` - API response and HTTP outcome mappings.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - typed Account Details GET/POST calls.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` - Viewer navigation item.

### Removed

* None

## Additional or Deviating Changes

* Phase 1 completed with 116 Application tests passing. Phase 2 completed with 102 Infrastructure unit tests and 14 SQL integration tests passing. Phase 3 completed with 40 API tests passing. Phase 4 completed with 93 Web tests passing.
* Final solution build passed. Focused Application, Infrastructure, API, Web, and Infrastructure SQL integration validation passed. The repository has no Account Details-specific AppHost or Playwright coverage to execute, so Step 5.1 remains partial. Existing package vulnerability, nullable, and preview-SDK warnings remain.

## Release Summary

Pending completion of the implementation phases.
