<!-- markdownlint-disable-file -->
# Release Changes: Account Preferences Load Performance Mitigation

**Related Plan**: account-preferences-load-performance-mitigation-plan.instructions.md
**Implementation Date**: 2026-08-31

## Summary

Phase 1 is complete. Application now represents configured and unconfigured SQL state explicitly.

## Changes

### Added

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesQueryState.cs` - Added explicit configured and unconfigured query-state semantics.

### Modified

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs` - Added query-state data to the Application response contract.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` - Treats missing SQL state as successful unconfigured state and avoids gateway calls for SQL-backed reads.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` - Added missing and populated SQL-state tests with gateway call-count assertions.
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs` - Maps explicit configured and unconfigured query states to successful responses.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Added exhaustive account-preferences failure status and Problem Details mappings.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Corrected generic provider failure classifications.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` - Added exhaustive API mapping, payload-safety, unknown-category, and retry-signaling tests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs` - Added provider transport and HTTP classification regression tests.
* `src/TNC.Trading.Platform.Web/DependencyInjection/PlatformApiClientServiceCollectionExtensions.cs` - Added specialized, non-stacked Platform API resilience registration.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientResilienceTests.cs` - Added physical-attempt resilience coverage.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Added successful unconfigured rendering and request-order coverage.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - Added fresh-install HTTP contract coverage and provider-ledger reset usage.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Strengthened loading, unconfigured-state, and negative-alert assertions.

### Removed

## Additional or Deviating Changes

* The legacy provider-backed branch remains available when no current-state store is registered, as specified by PD-01.

* The handler required an explicit `AccountPreferencesGatewayOutcome` type in a conditional expression to resolve a compiler typing error; behavior is unchanged.

* The repository's existing API test organization was used because the planned `GetAccountPreferencesEndpointHandlerTests.cs` file does not exist.
* Positive retry-delay cases remain deferred because `AccountPreferencesGatewayOutcome` has no validated delay metadata; no production metadata was invented for tests.

* Phase 5 distributed validation is blocked. The functional test calls the API route through the Web host and receives 404; targeting the API host directly receives 401 because the Web authentication session is not accepted by the separate API host. E2E reaches the page but observes configured SQL state despite session-mode annotations. Resolving these requires deliberate routing/authentication and Aspire runtime-state investigation beyond this mitigation scope.

## Release Summary

Phases 1 through 4 are complete. Phase 5 unit coverage is complete, but its distributed functional and E2E validation is blocked by the documented harness and runtime-state issues. Phases 6 and 7 remain pending until those blockers are resolved or explicitly accepted as follow-on work.

## Release Summary

Pending completion of implementation phases.
