<!-- markdownlint-disable-file -->
# Release Changes: Managed AppHost Fixture Cleanup

**Related Plan**: managed-app-host-fixture-cleanup-plan.instructions.md
**Implementation Date**: 2026-09-15

## Summary

Implementation tracking for failure-resilient AppHost fixture teardown and deterministic cleanup sequencing tests.

## Changes

### Added

* `test/Shared/Authentication/AppHostCleanup.cs` - Added dependency-free ordered cleanup helper with exception aggregation.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AppHostCleanupTests.cs` - Added four deterministic tests for cleanup continuation, aggregation, and ordering.

### Modified

* `test/Shared/Authentication/ManagedAppHostFixture.cs` - Made AppHost teardown attempt all owned cleanup operations and clear fields in `finally` blocks.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Linked the shared cleanup helper.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj` - Linked the shared cleanup helper for unit coverage.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Linked the shared cleanup helper.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Linked the shared cleanup helper.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` - Ensured provider cleanup runs after managed teardown failures.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Ensured provider cleanup runs after managed teardown failures.
* Focused API AppHost lifecycle validation - Baseline and post-run tunnel-proxy lists were empty; all 15 `PlatformAuthenticationIntegrationTests` passed.

### Removed

## Additional or Deviating Changes

* Phase 4 Web validation remains unresolved outside this cleanup scope.
	* The solution build and API integration suite passed. Web functional tests had 2 failures in account-preferences state assertions, and Web E2E had 1 corresponding visible-state failure. The failures reported `VerificationFailed` where the tests expected `Unconfigured` or `InSync`; no teardown failure or proxy leak was observed.
* The planned abrupt testhost termination experiment was not run because normal-completion teardown passed and no residual proxy required diagnosis.

## Release Summary

The AppHost cleanup path now attempts stop, application disposal, and builder disposal in order even when an earlier action fails, preserving the first failure and attaching later diagnostics. Functional and E2E wrapper fixtures also release their controlled IG providers after managed teardown failures. Four deterministic unit tests cover the sequencing and aggregation behavior. The focused API AppHost lifecycle run passed all 15 tests with no residual tunnel proxy, and the full API integration suite passed 26 tests. The solution build passed. Web functional and E2E validation remains blocked by unrelated account-preferences verification-state assertions, with no observed tunnel-proxy leak.
