---
title: E2E Tests Failure Research
description: Failure analysis for test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests
author: GitHub Copilot
ms.date: 2026-06-30
ms.topic: troubleshooting
keywords:
  - e2e tests
  - playwright
  - aspnet
  - failure analysis
estimated_reading_time: 5
---

## Scope

Investigate failing tests in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` only. This document records concrete failure evidence, the minimal code and configuration inspected, and the most likely controlling cause.

## Research Questions

1. What exact command reproduces the failure for this test project?
2. What concrete error messages, stack traces, or logs explain the failure?
3. Which local code or configuration most directly controls the failing behavior?
4. What root cause is best supported by the evidence?
5. Which alternative explanations can be ruled out with current evidence?

## Evidence Log

### Commands run

1. `dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj --nologo --verbosity normal`
   * Outcome: build and test execution completed, then the single E2E test failed after 120.8 seconds during fixture initialization.
   * Summary: total 1, failed 1, succeeded 0, skipped 0.

2. `docker ps --format "{{.ID}}"`
   * Outcome: succeeded.
   * Summary: Docker was available and returned three running containers.

3. `dotnet run --project .\src\TNC.Trading.Platform.AppHost\TNC.Trading.Platform.AppHost.csproj --launch-profile https`
   * Outcome: AppHost started and exposed only the Aspire dashboard listener within the 45 second capture window.
   * Summary: output showed `Now listening on: https://localhost:17257` for the dashboard and did not show any `Now listening on:` entries for the Web resource during the captured startup period.

### Failure excerpts

From the narrow `dotnet test` run:

```text
[xUnit.net 00:02:00.17]     TNC.Trading.Platform.Web.E2ETests.Authentication.PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderOperatorHome_WhenSeededViewerSignsInFromAspireDashboard [FAIL]
[xUnit.net 00:02:00.17]       System.TimeoutException : The AppHost-started Web sign-in URL could not be discovered from runtime listeners before the timeout expired. Discovered listener URIs: https://localhost:17257. Newly observed local ports: 62292, 62296, 62301, 62302, 62303, 62304, 62305, 62306, 62307, 62318, 62319, 62320, 62321.
```

```text
Stack Trace:
   at TNC.Trading.Platform.TestShared.Authentication.AppHostProcessHandle.WaitForWebSignInUriAsync(TimeSpan timeout) in D:\Repos\TNC.Trading\TNC.Trading.Platform\test\Shared\Authentication\AppHostProcessHandle.cs:line 129
   at TNC.Trading.Platform.Web.E2ETests.Authentication.AppHostProcessFactory.GetWebBaseUriAsync(AppHostProcessHandle appHostProcess) in D:\Repos\TNC.Trading\TNC.Trading.Platform\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\Authentication\AppHostProcessFactory.cs:line 14
   at TNC.Trading.Platform.Web.E2ETests.Authentication.RealAuthenticationE2ETestFixture.InitializeAsync() in D:\Repos\TNC.Trading\TNC.Trading.Platform\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\Authentication\RealAuthenticationE2ETestFixture.cs:line 14
```

From the direct AppHost startup capture:

```text
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: https://localhost:17257
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at https://localhost:17257/login?t=294967f08ff9c117d56b456888acef28
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application started. Press Ctrl+C to shut down.
```

### Controlling code and configuration inspected

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:11-14
  * The fixture fails before Playwright navigation starts because initialization blocks on Web base URI discovery.

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:7-14
  * The E2E project starts the real AppHost process and waits up to 120 seconds for `WaitForWebSignInUriAsync`.
  * It explicitly passes `enableInteractiveSignIn: false` to the shared AppHost process factory.

* test/Shared/Authentication/RealAppHostProcessFactory.cs:16-17,24,26
  * The harness runs `dotnet run` on the AppHost, enables infrastructure containers, and sets `Authentication__Test__EnableInteractiveSignIn` from the `enableInteractiveSignIn` argument.

* test/Shared/Authentication/AppHostProcessHandle.cs:87-129,205-226,255,327
  * The harness succeeds only if it can probe a candidate `/authentication/sign-in` URL that eventually redirects to a path containing `protocol/openid-connect/auth`.
  * Listener discovery is driven by `Now listening on:` process output plus newly opened local TCP ports.
  * The thrown timeout message reports which listeners were actually discovered.

* src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:36-40
  * The Web project is registered as an external endpoint resource and should expose an HTTPS endpoint once it starts.

* src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:63-76,180-182
  * The Web resource is always configured with `Authentication__Provider = Keycloak`.
  * Interactive test sign-in is only enabled when AppHost settings resolve `Authentication:Test:EnableInteractiveSignIn` to true.
  * The Web resource explicitly waits for Keycloak before startup.

* src/TNC.Trading.Platform.Web/Authentication/PlatformTestAuthenticationSignInHandler.cs:14-16
  * The synthetic interactive sign-in page is enabled only when `Provider == Test` and `EnableInteractiveSignIn == true`.

* src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:16,77
  * `/authentication/sign-in` always exists, but in this configuration it drives a real OpenID Connect challenge, not the synthetic local sign-in page.

* src/TNC.Trading.Platform.Web/Program.cs:49,55,63
  * The root path redirects unauthenticated users back into `/authentication/sign-in`, so the real Web resource must come up cleanly for the E2E test to proceed.

## Findings

### Most likely root cause

The failing E2E test is blocked before browser automation begins because the harness never discovers a reachable Web sign-in endpoint within the 120 second fixture timeout. The strongest evidence is that `WaitForWebSignInUriAsync` reports only the Aspire dashboard listener, `https://localhost:17257`, while the AppHost startup capture also shows only the dashboard listener during the observed window. That means the harness did not observe the Web resource exposing its external endpoint at all.

The controlling startup dependency is the AppHost Web resource wiring in `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:180-182`, where the Web project waits for Keycloak and is configured for real Keycloak authentication. Because the E2E factory uses `enableInteractiveSignIn: false` in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:9`, the test cannot fall back to the synthetic sign-in surface. The fixture therefore depends entirely on the real Web plus Keycloak path becoming live, and the observed evidence shows that this path never exposed a discoverable Web listener before timeout.

### Alternative explanations ruled out

* Docker not running
  * Ruled out by `docker ps --format "{{.ID}}"`, which returned running containers.

* Playwright/browser interaction failure
  * Ruled out because the failure occurs in fixture initialization before `Page.GotoAsync` in the test body.

* Missing `/authentication/sign-in` route in the Web app
  * Ruled out by `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:16`, which maps that route.

* Synthetic interactive sign-in path expected by the E2E test
  * Ruled out by configuration. The E2E factory passes `enableInteractiveSignIn: false`, and the Web app only enables the synthetic page when `Provider == Test` and `EnableInteractiveSignIn == true`. The AppHost Web wiring sets the provider to Keycloak.

* The harness probing the wrong sign-in semantics
  * Not supported by current evidence. The harness explicitly accepts only a real OIDC redirect path containing `protocol/openid-connect/auth`, which matches the intended real Keycloak scenario for this E2E project.

### Recommended resolution direction

Investigate why the AppHost Web resource never surfaces its external HTTPS listener during the E2E startup window, starting from the real Keycloak-backed startup path rather than the Playwright test itself. The next most targeted checks are:

1. Capture AppHost resource startup logs for the `web` and `keycloak` resources, not only the dashboard process output, to determine whether Keycloak readiness, Web startup, or endpoint publication is stalled.
2. Verify whether the Web resource reaches a running state under the same environment values used by `test/Shared/Authentication/RealAppHostProcessFactory.cs`.
3. If the Web resource is healthy but not discoverable, inspect whether the current Aspire hosting version still emits or proxies child resource listeners in the way `test/Shared/Authentication/AppHostProcessHandle.cs:327` expects.

## Remaining Gaps

* The collected evidence proves that the Web endpoint was not discovered by the harness, but it does not yet show the internal reason the `web` resource failed to publish its listener.
* The direct AppHost capture covered the dashboard process output only. It did not include per-resource logs from the Aspire dashboard or child resource diagnostics.
* No additional runtime traces were collected from Keycloak or the Web project, so the exact blocking subcomponent remains unresolved.
