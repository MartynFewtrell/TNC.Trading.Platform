---
title: Failing Tests Diagnosis
description: Evidence-led diagnosis of the repository's current automated test failures.
author: GitHub Copilot
ms.date: 2026-09-15
ms.topic: troubleshooting
---

## Research Scope

* Identify the repository's current failing tests from retained results and focused discovery
* Diagnose reproducibility, fixtures, infrastructure dependencies, and likely defect category
* Recommend the narrowest discriminating validations and remediation options

## Status

Complete. No product or test source was modified. Current evidence identifies 10
failing tests in two Web test projects. The prior 87-failure state recorded in
.copilot-tracking/research/subagents/2026-09-15/failing-tests-investigation.md
is not current: Web unit, API integration, and SQL integration failures are now
resolved by source changes already present in the workspace.

## Current Failure Inventory

| Test project | Result | Failed tests | Classification |
| --- | --- | --- | --- |
| `TNC.Trading.Platform.Web.FunctionalTests` | 8 failed, 0 passed | Six authentication and two account-preferences cases | Deterministic shared test-fixture defect |
| `TNC.Trading.Platform.Web.E2ETests` | 2 failed, 0 passed | One sign-in smoke and one account-preferences browser journey | Persistent Keycloak test-state mismatch |

The functional failures are:

* `PlatformAuthenticationFunctionalTests.SignOutEndpoint_ShouldReturnBadRequest_WhenAntiforgeryTokenIsMissing`
* `PlatformAuthenticationFunctionalTests.SignInEndpoint_ShouldRedirectToLandingPage_WhenExternalReturnUrlIsSupplied`
* `PlatformAuthenticationFunctionalTests.SignOutEndpoint_ShouldReturnNotFound_WhenBrowserUsesLegacyGetRequest`
* `PlatformAuthenticationFunctionalTests.RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry`
* `PlatformAuthenticationFunctionalTests.ConfigurationRoute_ShouldRedirectToSignIn_WhenOperatorRequestsProtectedRouteAfterSignOut`
* `PlatformProtectedRouteFunctionalTests.ConfigurationRoute_ShouldRedirectToAccessDenied_WhenViewerRequestsOperatorRoute`
* `AccountPreferencesFunctionalTests.AccountPreferences_ShouldPersistAndConverge_WhenProviderRecovers`
* `AccountPreferencesFunctionalTests.AccountPreferences_ShouldReturnUnconfiguredWithoutProviderRead_WhenFreshInstallProviderIsUnavailable`

The E2E failures are:

* `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderOperatorHome_WhenSeededViewerSignsInFromAspireDashboard`
* `AccountPreferencesE2ETests.AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable`

## Command Evidence

| Command | Outcome |
| --- | --- |
| `dotnet build` | Succeeded before this investigation, exit code 0 |
| Web unit `dotnet test` | Passed 129/129 in 1.2 seconds |
| API integration `dotnet test` | Passed 26/26 in 65.0 seconds |
| Infrastructure integration `dotnet test` | Passed 29/29 in 21.3 seconds; Testcontainers connected to Docker Desktop and its SQL Server reached readiness |
| Web functional `dotnet test` | Failed 8/8 in 0.7 seconds; every case has `System.ObjectDisposedException` from `System.Net.HttpListener.get_Prefixes()` |
| Web E2E `dotnet test` | Failed 2/2 in 136.9 seconds; each waits 60 seconds for `#username` then receives `Microsoft.Playwright.PlaywrightException` |
| One E2E smoke with process-scoped `AppHost__UsePersistentKeycloakState=false` | Passed 1/1 in 40.3 seconds; the variable was removed after the command |
| `dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"` | Exit code 1 after 23.0 seconds because the host cancelled four higher-level project tasks. It recorded 507 passed and 0 failed before cancellation, so it is not a valid aggregate result. |

The independent project results imply 562 passing and 10 failing executions out of
an expected 572. A completed sequential solution run remains necessary to prove
that aggregate result.

## Root-Cause Findings

### Functional suite fixture defect

Confidence: high.

All eight functional cases fail during fixture initialization, before a request
or an assertion specific to the test body. The shared provider first chooses a
random port and calls `HttpListener.Start()` at
test/Shared/AccountPreferences/ControllableIgProvider.cs:36-47. If the port is
already occupied, the catch block calls `listener.Prefixes.Clear()` at
test/Shared/AccountPreferences/ControllableIgProvider.cs:49-52. On this
Windows runtime, the failed `Start()` has disposed the listener, making that
retry statement throw `ObjectDisposedException` instead of retrying.

The failure is a test-fixture code defect. It is not evidence of a product
behavior defect because the application is never started. The fixture meets the
intent of randomized port allocation but not repeatability: it reuses a listener
after a failed bind.

The cheapest discriminating validation is a focused functional test after the
retry creates a new `HttpListener` for each attempt. It should move past fixture
initialization; then run all eight functional tests to expose any real behavior
failures previously masked by setup.

Remediation options:

* Create, configure, and dispose a fresh `HttpListener` on each failed bind,
  returning only the successfully started instance
* Replace the ad hoc listener with a test server that owns its dynamic endpoint
  and exposes its final bound URI

### E2E persistent Keycloak state mismatch

Confidence: high.

Both browser tests navigate to the Web sign-in route and wait for Keycloak's
`#username` control at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:29-35
and
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:25-32.
Neither control appears in the normal E2E execution, causing the two Playwright
timeouts. No retained trace, screenshot, or page URL explains the unexpected
browser page.

The E2E fixture does not supply `AppHost:UsePersistentKeycloakState=false` at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:15-24.
The AppHost defaults that setting to persistent state unless its configuration
is exactly `false`, at
src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:8-16,
and configures Keycloak as persistent in that case at
src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:41-47.
This allows stale realm import, client, and user data to survive across test
runs. In contrast, the functional fixture explicitly opts out of persistent
Keycloak state at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:20-25.

The focused E2E rerun with the process-scoped
`AppHost__UsePersistentKeycloakState=false` override passed. That changes only
the suspected lifecycle input and directly disproves a product login-page
regression as the current primary cause. The failure is a test strategy and
fixture-configuration defect: closed-box tests require fixture-owned,
repeatable state rather than a long-lived Keycloak container.

The cheapest permanent validation is the same one-test filter after the E2E
fixture passes the explicit nonpersistent setting, followed by its two-test
project run. Capture a Playwright trace or screenshot on retry only if either
test still fails.

Remediation options:

* Pass `AppHost:UsePersistentKeycloakState=false` from the E2E fixture, matching
  the functional fixture and the repository's fixture-ownership policy
* Ensure the fixture also sets `AppHost:UsePersistentSqlState=false` if E2E data
  must be isolated across runs
* Configure Playwright diagnostics on failure to retain the final URL, page
  screenshot, and trace, rather than relying on a missing locator alone

### Previously failing groups now pass

Confidence: high for the current workspace state.

The Web unit project now has no direct AngleSharp package reference at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:11-17
and passes 129/129, resolving the prior bUnit dependency conflict.

The shared AppHost fixture uses `DistributedApplicationTestingBuilder`, waits
for named resources, and resolves named endpoints at
test/Shared/Authentication/ManagedAppHostFixture.cs:28-55. API integration
passes 26/26, so the prior socket-scanning AppHost-discovery failure is fixed.

The SQL fixture owns `MsSqlContainer`, starts it, creates a unique database, and
disposes it at test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:8-42
and test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:85-107.
Its 29 tests pass with Docker Desktop available, resolving the former
arbitrary-container discovery and connection-refused failure group.

## Testing Strategy Assessment

The repository rules require mutable integration dependencies to be
fixture-owned, readiness-checked, and cleaned up. They prohibit fixed-port
coordination and inferred AppHost endpoints in
.github/instructions/tests.instructions.md:46-58 and
.github/instructions/aspire-tests.instructions.md:24-54. The managed AppHost
and SQL fixture implementations now follow this approach. The remaining local
deviations are an unsafe HTTP listener retry lifecycle and the E2E fixture's
persistent Keycloak state.

The full solution command launches higher-level projects concurrently. Its host
cancellation means it cannot presently serve as the authoritative quality gate
in this environment. A sequential orchestration command or CI task should
produce the full aggregate result after the two fixture repairs; global
serialization should not conceal the defects.

## Recommended Remediation Order

1. Repair `ControllableIgProvider.Start()` to use a fresh listener for each bind
   attempt, then run the functional project.
2. Make the E2E fixture explicitly request nonpersistent Keycloak state, then
   run its two tests and retain diagnostics on failure.
3. Run each high-level project independently, then run the solution through a
   sequential or otherwise non-cancelling test orchestration path.
4. Re-enable the normal parallel solution test command only after confirming
   that all AppHost fixtures remain isolated under concurrent startup.

## Open Questions and Research Gaps

* Why the normal full solution invocation is cancelled after approximately 23
  seconds. The output records host cancellation, not a test failure, and no
  repository task or runsettings file was identified in this investigation.
* Whether the E2E fixture should also disable persistent SQL state. Keycloak is
  proven causal for the login-page failure, but SQL state may still leak between
  future E2E runs.
* What the browser page URL and markup were before the missing-`#username`
  assertion. The environment override confirms the cause sufficiently for
  remediation, but trace capture would make recurrence diagnosis faster.