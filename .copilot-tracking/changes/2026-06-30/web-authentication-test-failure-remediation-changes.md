<!-- markdownlint-disable-file -->
# Release Changes: Web Authentication Test Failure Remediation

**Related Plan**: web-authentication-test-failure-remediation-plan.instructions.md
**Implementation Date**: 2026-06-30

## Summary

Resolved the shared AppHost-backed authentication startup failure by moving the Web auth harness onto Aspire-managed AppHost startup, preserving strict OIDC probing, and fixing Keycloak callback/state handling for runtime Web listener ports.

## Changes

### Added

* None.

### Modified

* test/Shared/Authentication/AppHostProcessHandle.cs - enriched shared AppHost diagnostics, preserved strict OIDC redirect validation, and added managed AppHost lifetime support for the shared auth harness.
* test/Shared/Authentication/RealAppHostProcessFactory.cs - added Aspire-managed AppHost startup, shared environment overrides, and test-scoped Keycloak session-lifetime control for the Web authentication suites.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs - aligned the functional auth wrapper with the shared managed AppHost startup path.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs - aligned the functional fixture with the asynchronous managed shared auth harness.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs - aligned the E2E auth wrapper with the shared managed AppHost startup path.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs - aligned the E2E fixture with the asynchronous managed shared auth harness.
* src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json - widened localhost callback and origin allowlists so runtime Web listener ports remain valid for Keycloak-backed sign-in and sign-out.
* src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs - added a configuration-controlled Keycloak lifetime switch so auth test runs can force a fresh realm import with session-scoped state.
* test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs - added coverage for the session-scoped Keycloak lifetime override used by the auth test harness.
* docs/wiki/testing-and-quality.md - updated the distributed auth harness guidance to describe the Aspire-managed shared AppHost startup path and deterministic Keycloak state handling.
* docs/wiki/local-development.md - updated local auth test guidance and troubleshooting notes to reflect the managed shared harness and Keycloak state reset behavior.

### Removed

* None.

## Additional or Deviating Changes

* Early standalone AppHost captures still exposed only the dashboard listener output.
	* Later focused managed-host reruns surfaced `AppHost.Resources.web` and `AppHost.Resources.keycloak` resource logs directly in the test output, which provided the resource-level evidence Phase 1 needed without a separate dashboard-log capture pass.

* Existing hang dumps were not analyzed in this environment.
	* No supported dump-analysis tool was installed locally, but the managed-host reruns and live runtime listener probes isolated the coupled discovery and Keycloak-state defects before dump analysis became necessary.

* Focused validation was briefly blocked by stale AppHost processes locking `TNC.Trading.Platform.AppHost.exe`.
	* The blocker was cleared by terminating the orphaned AppHost processes and rerunning the same focused validation commands successfully.

## Release Summary

Completed all four remediation phases and resolved the shared Web authentication startup failure once in the shared harness and AppHost layer. The final implementation moved the Web auth suites onto Aspire-managed AppHost startup, kept the probe strict enough to require the real Keycloak OIDC redirect path, widened the local Keycloak realm callback/origin allowlists for runtime Web listener ports, and forced session-scoped Keycloak state during auth test runs so realm import changes apply deterministically.

The implementation modified 11 product, test, and wiki files and refreshed the plan, planning log, and changes log. Validation passed for the focused functional auth repro, the focused Web E2E project, the full Web functional project, and the full Web E2E project. Passing runs still emitted intermittent `sql_check` unhealthy log entries, but those warnings did not fail either Web suite.
