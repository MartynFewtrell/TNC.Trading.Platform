<!-- markdownlint-disable-file -->
# Release Changes: IG market collection failures

**Related Plan**: ig-market-collection-failures-plan.instructions.md
**Implementation Date**: 2026-10-04

## Summary

Following the incident plan to preserve fail-closed market collection, improve regression coverage, and expose safe saved diagnostics to operators. Live IG allowance state and credential readiness are not assumed.

## Changes

### Added

* `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCollectionFailureMetrics.cs` - added bounded category collection failure metrics.
* `src/TNC.Trading.Platform.Application/Features/MarketDetails/MarketDetailProviderErrorCodes.cs` - defined the detail-provider error-code allow-list.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20261004110000_PersistMarketCollectionFailureDiagnostics.cs` - added nullable failure-diagnostic columns without backfilling historical rows.

### Modified

* `.copilot-tracking/plans/2026-10-04/ig-market-collection-failures-plan.instructions.md` - recorded completed implementation/validation and the gated live/reset steps.
* `.copilot-tracking/plans/logs/2026-10-04/ig-market-collection-failures-log.md` - recorded reset constraints, AppHost test serialization, and repository-wide formatting debt.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketCategoryInstrumentsGatewayTests.cs` - covered coherent short-page replay, inconsistent replay failure, and the separate later-page 70-versus-42 mismatch.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketDetailsGatewayTests.cs` - added entitlement-style 403 and strengthened response-body redaction coverage.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/MarketDetails/MarketDetailCollectionCoordinatorTests.cs` - covered eligibility for a later scheduled run after an allowance-denied attempt.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/MarketCategoryInstrumentSqlIntegrationTests.cs`, `MarketDetailSqlIntegrationTests.cs` - verified saved diagnostics and legacy null compatibility across SQL reloads; retained existing retry semantics.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/MarketCategoryInstrumentEndpointTests.cs`, `MarketDetailEndpointMappingTests.cs` - verified API diagnostic mappings and saved-status response behavior.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinatorTests.cs` - recorded the safe category failure through the test store.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MarketCategoriesTests.cs`, `MarketDetailsTests.cs` - asserted saved category diagnostics and separate market-detail coverage.
* `src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentEndpointMapping.cs`, `src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentStatusResponse.cs`, `src/TNC.Trading.Platform.Api/Features/Platform/MarketDetailAvailabilityResponse.cs`, `src/TNC.Trading.Platform.Api/Features/Platform/MarketDetailResponse.cs` - propagated safe category and detail diagnostics through API contracts.
* `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/IMarketCategoryInstrumentCycleStore.cs`, `MarketCategoryInstrumentCategoryStatus.cs`, `MarketCategoryInstrumentCycleCoordinator.cs`, `src/TNC.Trading.Platform.Application/Features/MarketDetails/MarketDetailAvailability.cs`, `MarketDetailCollectionCoordinator.cs`, `MarketDetailGatewayFailure.cs`, `MarketDetailReadResult.cs` - carried typed diagnostics through collection and read workflows and emitted classified metrics.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketDetailsGateway.cs`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentCycleStore.cs`, `EfMarketCategoryInstrumentStatusReader.cs`, `EfMarketDetailReadStore.cs`, `EfMarketDetailRunStore.cs`, `Entities/InstrumentCollectionCategoryAttemptEntity.cs`, `Entities/MarketDetailRunTargetEntity.cs`, `PlatformDbContext.cs`, `Migrations/PlatformDbContextModelSnapshot.cs` - persisted, allow-listed diagnostics and loaded them from SQL.
* `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs` - registered collection-failure metrics.
* `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor`, `MarketDetails.razor`, `MarketCategoryCollectionStatusViewModel.cs`, `MarketDetailAvailabilityViewModel.cs`, `MarketDetailViewModel.cs` - rendered saved diagnostics without conflating category failures and detail coverage.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/MarketCategoryInstrumentEndpointTests.cs`, `MarketDetailEndpointMappingTests.cs`, `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinatorTests.cs`, `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/MarketCategoryInstrumentSqlIntegrationTests.cs`, `MarketDetailSqlIntegrationTests.cs`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MarketCategoriesTests.cs`, `MarketDetailsTests.cs` - added persistence, API, and UI regression coverage.
* `docs/wiki/operator-guide.md` - documented saved diagnostic meanings, safe evidence collection, and operator response.

### Removed

* None.

## Additional or Deviating Changes

* Recorded allowance counters/reset and live request timeline as unavailable; existing sanitized research supplies the error code and independent page-shape evidence.
  * No live provider calls were made, and credential/key handling remains outside the implementation scope.
* Full solution build passed with 0 warnings and 0 errors; all 1,040 tests passed with `dotnet test TNC.Trading.Platform.slnx --no-build -m:1`.
* Focused category/detail gateway, coordinator, SQL, API, and UI suites passed; SQL migration tests covered fresh creation, upgrades, diagnostics reload, and legacy nullable fields.
* EF reported no pending model changes; idempotent migration script generation succeeded.
* `git diff --check` passed. Targeted formatting verification passed for the three new C# files. Whole-solution `dotnet format` verification reports broad existing diagnostics and was not used to reformat unrelated code.
* The initial parallel full-solution test run timed out during concurrent Aspire AppHost startup. The affected API integration, web functional, and web E2E projects passed in isolation, then the entire solution passed serially.
* Deferred local application-data reset because the database is not confirmed disposable and the documented local infrastructure reset deletes both SQL and Keycloak resources. No backup, database reset, credential change, or live Demo request was performed.
* After the user authorized a data-only retry, created and checksum-verified a copy-only local `platformdb` backup outside the SQL container. Ran the existing guarded slot-retry script for IG Demo 2026-10-04 slot 0. Verified the cycle and its two attempts are Pending, the slot has no full-run coverage, and the seven consumed requests and historical full run remain intact. No credential/configuration rows were touched. Startup and provider calls remain unrun pending in-house readiness.

## Release Summary

Updated 40 product/documentation files: added 3 files (two safe diagnostic contracts and one EF migration) and modified 37 files across Application, Infrastructure, API, Web, ServiceDefaults, tests, and the operator guide. No dependencies were added. The nullable SQL columns preserve historical rows; deploy by applying the normal EF migration. Full build and 1,040 tests passed. New files pass targeted formatting; whole-repository formatting diagnostics remain. The local Demo slot was reopened after a recoverable backup without destroying the database or falsifying the request budget; startup and controlled Demo verification remain pending in-house readiness.
