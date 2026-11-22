<!-- markdownlint-disable-file -->
# Release Changes: Keycloak Testing Improvements

**Related Plan**: keycloak-testing-improvements-plan.instructions.md
**Implementation Date**: 2026-07-27

## Summary

Implementation tracking for layered Keycloak readiness, exception-complete cleanup, cross-process port ownership, and test documentation improvements.

## Changes

### Added

* `test/Shared/Authentication/KeycloakReadinessPolicy.cs` - Added bounded, secret-safe OIDC issuer readiness policy with injected HTTP and delay seams.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/KeycloakReadinessPolicyTests.cs` - Added 33 focused readiness classification and deadline tests.
* `artifacts/quality-gates/keycloak-testing-hardening/20260727-141245/before/` - Captured secret-safe pre-lease build, concurrent process, port ownership, manifest, and timing evidence.

### Modified

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Aligned `Aspire.Hosting.Testing` to 13.2.3 and linked shared readiness infrastructure.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` - Linked shared readiness infrastructure.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Linked shared readiness infrastructure.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Linked shared readiness infrastructure.
* `test/Shared/Authentication/KeycloakPortLease.cs` - Added a portable five-minute machine-local exclusive lease for fixed Keycloak port 8080 and conservative recovery for unlocked stale lock files.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/KeycloakPortLeaseTests.cs` - Added real child-process contention, timeout, cancellation, and release/reacquisition coverage.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` - Added lease acquisition before environment and AppHost startup with failure-safe release.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` - Added lease lifecycle around managed Web fixture startup and cleanup.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Added lease lifecycle around managed browser fixture startup and cleanup.
* `docs/wiki/testing-and-quality.md` - Documented layered readiness, failure classification, cleanup, lease scope, callback proof, and focused/concurrent validation.
* `docs/wiki/local-development.md` - Documented session-scoped test state, readiness troubleshooting, lease limits, and external port conflicts.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Added bounded managed startup, Aspire Keycloak health gating, exact issuer readiness, exception-complete initialization cleanup, and direct use of imported wildcard Web callbacks.
* `test/Shared/Authentication/AppHostProcessHandle.cs` - Made process, application, builder, environment, and output cleanup attempts exception-complete while preserving the first failure.

### Removed

## Additional or Deviating Changes

* Phase 1 package alignment was completed before runtime readiness integration, as accepted by DD-01.
* The existing managed Admin API callback mutation was removed after focused real Web functional validation proved the checked-in wildcard localhost callbacks support the sign-in path. This also removes the stale `keycloak-admin` credential dependency that failed with HTTP 401.
* A failed multi-process validation run left an unlocked zero-byte lease file. Acquisition now probes stale files with an exclusive open and removes them only when no process holds the file; active leases remain protected and continue to enforce the bounded timeout.

## Validation Evidence

* AppHost unit tests: 33 passed, 0 failed.
* API integration project build: succeeded with pre-existing warnings only.
* AppHost readiness policy tests: 8 selected tests passed.
* Web authentication functional tests: 5 selected tests passed after removing obsolete Admin bootstrap.
* API authentication integration tests: 15 selected tests passed.
* Phase 3 topology: one solution-level runner-local quality gate; no committed workflows; lease scope limited to processes sharing the same machine, filesystem, and host network.
* Pre-lease baseline: three test processes overlapped for approximately 3 minutes 26 seconds; port 8080 was owned by `com.docker.backend`, with no test-owned listener observed.
* Provisional lease deadline: 5 minutes, based on the longest observed baseline run plus margin; no evidence supports changing the existing 60-second DCP timeout.
* Post-lease evidence: `artifacts/quality-gates/keycloak-testing-hardening/20260727-142452/after/`; API 15/15, Web functional 6/6, and Web E2E 1/1 passed concurrently. Docker remained the single observed host owner for port 8080.
* Wildcard callback proof: real Web functional sign-in, authenticated navigation, POST sign-out, callback redirect, and post-sign-out challenge passed without Admin API mutation.
* Documentation validation: no diagnostics, broken relative links, trailing whitespace, or diff-check errors in changed wiki files.
* Focused lease validation: 3 passed, 0 failed, 0 skipped.
* Final quality gate `keycloak-testing-hardening-final4`: 288 passed, 0 failed, 0 skipped across 9 TRX files. Evidence: `artifacts/quality-gates/keycloak-testing-hardening-final4/20260727-151312`.

## Release Summary

All five implementation phases completed. The final gate passed after conservative stale-lock recovery was added to handle abandoned unlocked lease files without weakening active cross-process ownership.
