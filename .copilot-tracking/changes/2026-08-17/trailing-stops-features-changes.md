<!-- markdownlint-disable-file -->
# Release Changes: Trailing Stops Features

**Related Plan**: trailing-stops-features-plan.instructions.md
**Implementation Date**: 2026-08-17

## Summary

Implementation tracking for the Operator-only Account Preferences and trailing-stops observation feature.

## Changes

### Added

* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Operator-only live and history route mapping.
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs` - Live read endpoint handler.
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs` - Confirmed update endpoint handler.
* `src/TNC.Trading.Platform.Api/Features/Platform/GetTrailingStopsPreferenceObservationsEndpointHandler.cs` - Paged history endpoint handler.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Operator account-preferences page.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Page orchestration.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` - Page state model.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` - Mapping and failure tests.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs` - HTTP-boundary tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - bUnit page-state and paging tests.

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - IG account-preferences adapter.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs` - Append-only observation store.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs` - Additive observation migration.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs` - Gateway contract tests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfTrailingStopsPreferenceObservationStoreTests.cs` - Store ordering and cursor tests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - SQL migration, restart, index, and retention tests.

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/` - Application contracts, typed outcomes, handlers, validation, and observation abstractions.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/UpdateAccountPreferencesValidatorTests.cs` - Validator coverage.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` - Handler, reconciliation, observation, cursor, and telemetry coverage.

* `src/TNC.Trading.Platform.Application/Services/TrailingStopsPreferenceObservationRetentionPolicy.cs` - Explicit 90-day retention policy.
* `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentPresentation.cs` - Legacy Demo-to-Test presentation mapping.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs` - Test-only feature guard.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/TrailingStopsPreferenceObservationEntity.cs` - Minimal persistence foundation for retention.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesEnvironmentPolicyTests.cs` - Environment guard coverage.

### Modified

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Registered Account Preferences routes.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - Added live and history client calls.
* `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs` - Registered page presenter dependencies.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` - Added navigation entry.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs` - Registered presenter for bUnit.

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs` - Synchronized EF model snapshot.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Restricted 401 replay to GET requests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` - Reset observation table between SQL tests.

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Registered observation entity.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs` - Added 90-day observation cleanup.
* `src/TNC.Trading.Platform.Api/appsettings.json` - Declared observation retention configuration.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionProcessorTests.cs` - Added retention coverage.
* `docs/wiki/operator-guide.md` - Documented Operator/Test-only behavior and deferred archive/export.
* `docs/wiki/runtime-behavior.md` - Documented provider authority and observation retention.

### Removed

## Additional or Deviating Changes

* Phase 1 introduced the minimal observation entity ahead of Phase 3 because retention cleanup requires a compilable persistence target.
* Full solution validation exposed two distributed AppHost/service-discovery failures; no unrelated runtime changes were applied.

## Release Summary

The 2026-08-17 delivery established the Operator-only Account Preferences
vertical slice and its focused automated coverage. It did not constitute a
fully released or fully validated solution: later remediation identified API
composition, distributed startup, provider-contract, migration-lineage,
partitioning, retention, and UI-cancellation corrections. Automated tests do
not call real IG. Archive/export, verified Test endpoint evidence, allowance
confirmation, and Live enablement remain follow-on work.

Known follow-on constraints remain: archive/export beyond the approved 90-day
online retention needs a separate decision; non-secret Test endpoint and
allowance behavior are covered with controlled automated handlers but require
operator-confirmed environment evidence; and Live enablement requires a
separate safety delivery for credentials, authorization, allowance handling,
and monetary-risk controls.
