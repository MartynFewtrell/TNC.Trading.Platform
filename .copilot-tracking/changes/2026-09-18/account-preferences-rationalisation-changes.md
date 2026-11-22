<!-- markdownlint-disable-file -->
# Release Changes: Account Preferences Rationalisation

**Related Plan**: account-preferences-rationalisation-plan.instructions.md
**Implementation Date**: 2026-09-18

## Summary

Phase 1 is complete. API, UI, and higher-level test changes can now consume the durable command and persistence boundary.

## Changes

### Added

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesOperation.cs` and related command/result contracts - Added journal and explicit Save/Check status application contracts.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260918164317_AddAccountPreferencesOperations.cs` - Added operation-journal schema and unique idempotency key.

### Modified

* Account-preferences application handlers and persistence ports - Added IG-first save, compare-and-converge Check status, atomic finalisation, recovery, and bounded failure outcomes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Added journal mapping and DbSet.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` - Reset journal table during fixture setup to keep migration initialization repeatable.
* API and Web account-preferences surfaces - Migrated to IG-first idempotent Save, body-free Check status, server-derived ownership, consolidated Trailing stops terminology, and projection refresh.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Updated bUnit expectations for the rationalised workflow.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` and gateway contract coverage - Aligned the controllable provider with production routes, payload, session headers, and fixture-owned account state.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - Migrated scenarios to Save and body-free Check status contracts; clean focused validation passes 3/3.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Stabilized tab activation and provider reset behavior; focused validation reaches 8/9.
* `docs/wiki/api-reference.md`, `docs/wiki/application-overview.md`, `docs/wiki/architecture.md`, `docs/wiki/local-development.md`, `docs/wiki/operator-guide.md`, `docs/wiki/runtime-behavior.md`, and `docs/wiki/testing-and-quality.md` - Aligned wiki documentation with the implemented workflow.

### Removed

* None recorded yet.

## Additional or Deviating Changes

* SQL integration fixture emitted the existing SSH.NET NU1903 vulnerability warning.
* Added dedicated handler tests for IG-first save and Check status convergence.
* API Release tests passed 73; Web Release tests passed 133. Existing AngleSharp and nullable warnings remain.
* Provider-focused contract test passed. AppHost and Keycloak startup are healthy; one E2E warning-rendering scenario remains failing after 8/9 focused tests passed.

## Release Summary

Phases 1 and 2, Phase 3 Steps 3.1 and 3.2, and Phase 4 Step 4.1 are complete and validated. Phase 3 Step 3.3 remains partial with one E2E failure. Final repository validation is not yet complete.
