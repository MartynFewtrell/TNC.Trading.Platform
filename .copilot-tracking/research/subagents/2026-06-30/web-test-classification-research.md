---
title: Web Test Classification Research
description: Classification of the Web FunctionalTests and E2ETests projects, including their AppHost and Keycloak authentication model.
author: GitHub Copilot
ms.date: 2026-06-30
ms.topic: reference
keywords:
  - web tests
  - functional tests
  - e2e tests
  - apphost
  - keycloak
estimated_reading_time: 3
---

## Scope

Question investigated: whether these two projects are Aspire tests, Playwright tests using the AppHost to locate Keycloak and authenticate against it, or some combination.

Projects inspected:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests

## Conclusions

### FunctionalTests

Classification: AppHost-backed functional/integration test project with a Playwright-assisted authentication helper.

It is not an Aspire.Hosting.Testing-driven test harness in the inspected source. The project references Aspire.Hosting.Testing, but the runtime is started by shelling out to `dotnet run` on the AppHost project, then the tests use `HttpClient` against the discovered Web endpoint.

It does use AppHost to locate the Web runtime and confirm the Keycloak-backed sign-in path. It also uses Playwright headlessly to complete the real Keycloak sign-in flow once, then copies the authenticated browser cookies into a `CookieContainer` so the main assertions can run over HTTP.

Evidence:

* The project references both Aspire.Hosting.Testing and Microsoft.Playwright, and references the AppHost project: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11-12,23
* The fixture starts the AppHost process and asks for the Web base URI from it: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:13-14
* The local factory delegates to the shared AppHost launcher and waits for a discovered Web sign-in URI: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:9,14
* The shared launcher starts the AppHost with `dotnet run --project "src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj" --launch-profile https` and enables infrastructure containers: test/Shared/Authentication/RealAppHostProcessFactory.cs:16-26
* The shared handle discovers listener URIs from `Now listening on`, probes sign-in URLs, and only accepts a flow that redirects to `protocol/openid-connect/auth` and returns the branded login page: test/Shared/Authentication/AppHostProcessHandle.cs:87,255,268,327
* The functional auth helper creates a Playwright browser, fills `#username` and `#password`, clicks `Sign In`, and then copies the resulting platform cookies into the HTTP test session: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationSessionFactory.cs:16,33-40,130-131
* The functional tests call that helper before asserting protected-route behavior, and they assert OIDC redirects containing `protocol/openid-connect/auth`: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs:48-52,98,130-134

### E2ETests

Classification: Playwright-based browser end-to-end test project that is also AppHost-backed.

It is a real browser test project. The main test class derives from `PageTest` and performs UI-level sign-in against the rendered login page. As with FunctionalTests, the inspected source does not use Aspire.Hosting.Testing APIs to host the app in-process. Instead, it starts the AppHost externally and discovers the Web endpoint from that running AppHost.

It does use AppHost to locate the Web runtime and authenticate against the Keycloak-backed login flow.

Evidence:

* The project references Aspire.Hosting.Testing, Microsoft.Playwright.Xunit, and the AppHost project: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11-12,23
* The fixture starts AppHost and gets the Web base URI from the discovered sign-in endpoint: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:13-14
* The E2E AppHost factory delegates to the shared AppHost launcher with `enableInteractiveSignIn: false` and waits for `WaitForWebSignInUriAsync`: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:9,14
* The shared discovery logic is the same AppHost-plus-Keycloak probe described above: test/Shared/Authentication/RealAppHostProcessFactory.cs:16-26 and test/Shared/Authentication/AppHostProcessHandle.cs:87,255,268,327
* The browser test class derives from `PageTest`, navigates to `/authentication/sign-in?returnUrl=%2Fstatus`, fills `#username` and `#password`, clicks `#kc-login`, and expects to land on the operator UI: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:8,29,31-35,37-45

## Direct Answer

These projects are a combination, but not in the sense of using Aspire.Hosting.Testing as the primary harness.

* FunctionalTests: AppHost-backed functional/integration tests with a Playwright-assisted login helper. They use AppHost to discover the Web endpoint and confirm the Keycloak OIDC sign-in flow, then authenticate through that flow and continue mainly with `HttpClient`.
* E2ETests: Playwright-based browser E2E tests that are also AppHost-backed. They use AppHost to discover the Web endpoint and then sign in through the real Keycloak login page in the browser.

For both projects, the inspected source shows AppHost is used to locate the runtime and the Keycloak-backed authentication path. The actual launch mechanism is `dotnet run` against the AppHost project, not an in-process Aspire testing host API.