<!-- markdownlint-disable-file -->
# Release Changes: IG Login 403 Degraded Health

**Related Plan**: ig-login-403-degraded-health-plan.instructions.md
**Implementation Date**: 2026-08-06

## Summary

Implementation is complete at the code and focused-test level. Operational verification remains blocked by unavailable authorized key-ring and IG environments.

## Changes

### Added

### Modified

* `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs` - Added secret-free per-field usability, aggregate authentication readiness, and credential re-entry predicates while preserving existing construction compatibility.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs` - Derives usability from active-provider unprotect results and retains safe nonblank-field reprotection behavior.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs` - Added unreadable, partial-save, and complete three-secret re-save regression coverage.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs` - Gated authentication transitions on active credential readiness.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - Blocked unusable credentials with redacted remediation and mapped typed diagnostics only to failure-event details.
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs` - Applied the readiness guard after retry intent persistence.
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationFailure.cs` - Added the fixed two-field diagnostic contract.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs` - Added session-403 allowlist extraction and malformed-response handling.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs` - Covered manual retry zero-call remediation.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs` - Covered sentinel isolation and diagnostic length/status boundaries.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs` - Covered automatic unreadable-credential reconciliation.
* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/CredentialPresenceResponse.cs` - Projected safe credential usability and re-entry booleans in GET responses.
* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationMapping.cs` - Mapped the expanded safe credential projection.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatedCredentialPresenceResponse.cs` - Projected safe credential usability and re-entry booleans in save responses.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs` - Mapped the expanded save projection.
* `src/TNC.Trading.Platform.Web/CredentialPresenceViewModel.cs` - Carried safe readiness data into the Web view model.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModel.cs` - Preserved write-only blank secret fields while carrying remediation state.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor` - Added conditional all-three-secret re-entry guidance and stable test selectors.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs` - Covered conditional remediation and no-secret rendering.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs` - Added remediation-state test payload support.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformConfigurationMappingTests.cs` - Added safe GET projection tests.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationMappingTests.cs` - Added safe save projection tests.
* `src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs` - Granted the API unit test project access to internal application response records for mapper tests.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs` - Corrected the Demo session request to IG v2 with explicit JSON `Accept`, retaining CST/header-token proof calls.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs` - Added request-capture assertions for the v2 session contract and unchanged proof requests.

### Removed

## Additional or Deviating Changes

* `IProtectedCredentialService` and `PlatformConfigurationSnapshot` required no changes because the existing presence projection boundary carries the expanded record.
  * Phase 1 implementation report confirmed this preserves the current port shape without unnecessary changes.
* Focused Phase 1 validation passed: 29 tests passed, 0 failed, 0 skipped. Existing package vulnerability and xUnit analyzer warnings remain.
* Phase 2 initially exposed remediation-reason loss in automatic degraded-state handling; the reconciler now distinguishes present-but-unreadable credentials from genuinely missing credentials.
  * Final focused validation passed: infrastructure 40 tests and application 8 tests, all passing.
* Phase 3 API/Web focused validation passed: API mapper tests 6 passed; Web tests 7 passed; API and Web builds passed.
* Phase 3 operational re-save verification was not performed because active SQL-backed key-ring access and authorized operator credentials are unavailable in this session.
* Official IG Labs documentation confirms v1/v2 CST and `X-SECURITY-TOKEN`, while v3 uses OAuth tokens; the previous v3-plus-header-token combination was therefore corrected to v2.
  * Focused gateway validation passed: 20 tests passed, 0 failed, 0 skipped; `git diff --check` passed.

## Release Summary

Phases 1, 2, and 4 are complete. Phase 3 product code and automated tests are complete, with operational key-ring re-save verification blocked by environment access. Phase 5 code-level validation is complete: focused suites pass and the solution builds successfully. The full solution test run is operationally blocked by Aspire/Keycloak health and integration-run instability before a final summary. Live IG route confirmation, three-secret re-save, and provider diagnostics remain pending an authorized environment.

Focused validation passed across the credential/reconciliation, application retry/outcome, IG gateway, API mapper, and Web configuration suites. The solution build passed. Existing package vulnerability, SDK, nullable, and analyzer warnings remain; no build failure attributable to this work was identified.
