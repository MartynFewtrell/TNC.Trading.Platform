<!-- markdownlint-disable-file -->
# Release Changes: Resolve Failing Web Tests

**Related Plan**: resolve-failing-web-tests-plan.instructions.md
**Implementation Date**: 2026-09-15

## Summary

Repair the Web functional test provider retry lifecycle and isolate Keycloak state in the browser E2E fixture.

## Changes

### Added

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/ControllableIgProviderTests.cs` - Added deterministic replacement-listener regression coverage.

### Modified

* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Creates and owns a fresh listener for each candidate-port bind attempt and exposes an internal selector seam for deterministic coverage.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Disables persistent Keycloak state only for browser E2E fixtures.

### Removed

## Additional or Deviating Changes

* The focused account-preferences E2E journey reached its feature assertion but failed because exact `Unconfigured` text was not visible. Full suite validation is deferred pending product/UI investigation.

## Release Summary

Phase 1 is complete and the Phase 2 fixture change is complete. Phase 2 browser validation is partial because one journey exposed a product/UI assertion failure after fixture startup.
