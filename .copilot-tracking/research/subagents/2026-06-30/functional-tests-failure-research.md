---
title: Functional Tests Failure Research
description: Failure analysis for TNC.Trading.Platform.Web.FunctionalTests on 2026-06-30
ms.date: 2026-06-30
ms.topic: troubleshooting
---

## Scope

* Investigate failures in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` only.
* Collect concrete failure evidence from actual test execution.
* Identify the controlling cause without changing product or test code.

## Research Questions

* What exact command reproduces the failure for this test project?
* What failure messages, stack traces, or logs are produced?
* What code or configuration directly controls the failing behavior?
* What is the most likely root cause, and what alternatives can be ruled out?

## Findings In Progress

## Commands Run

* `dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj --logger "console;verbosity=detailed"`
	* Outcome: test host started discovery, then the run was canceled almost immediately with `MSB5021` and no test-level assertion or exception was emitted. This captured only partial evidence.
* `dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry" --logger "console;verbosity=detailed" --diag ".\artifacts\functional-tests-diag.log" --blame-crash --blame-hang --blame-hang-timeout 90s`
	* Outcome: the targeted functional test run hung during startup and was aborted by the blame-hang collector after about 90 seconds.
* `dotnet run --project .\src\TNC.Trading.Platform.AppHost\TNC.Trading.Platform.AppHost.csproj --launch-profile https`
	* Outcome: the AppHost process reported only the Aspire dashboard listener at `https://localhost:17257` during the captured startup window.

## Failure Evidence

### First functional test project run

Important console output:

```text
[xUnit.net 00:00:00.10]   Starting:    TNC.Trading.Platform.Web.FunctionalTests
Attempting to cancel the build...
	TNC.Trading.Platform.Web.FunctionalTests test net10.0 failed with 1 warning(s) (2.4s)
		C:\Program Files\dotnet\sdk\10.0.301\Microsoft.TestPlatform.targets(48,5): warning MSB5021: Terminating the task executable "dotnet" and its child processes because the build was canceled.
```

This run did not provide a direct test exception, only evidence that execution stopped before a normal test result was emitted.

### Targeted single-test diagnostic run

Important console output:

```text
[xUnit.net 00:00:00.09]   Starting:    TNC.Trading.Platform.Web.FunctionalTests
	TNC.Trading.Platform.Web.FunctionalTests net10.0              Testing (57.9s)
...
		D:\Repos\TNC.Trading\TNC.Trading.Platform\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\bin\Debug\net10.0\TNC.Trading.Platform.Web.FunctionalTests.dll : error TESTRUNABORT: Test Run Aborted.

Build failed with 1 error(s) and 46 warning(s) in 93.6s

Attachments:
	D:\Repos\TNC.Trading\TNC.Trading.Platform\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TestResults\cd976083-d692-4d19-b0c7-66ba333f286a\testhost_17504_20260630T153415_hangdump.dmp
	D:\Repos\TNC.Trading\TNC.Trading.Platform\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TestResults\cd976083-d692-4d19-b0c7-66ba333f286a\TNC.Trading.Platform.AppHost_7540_20260630T153415_hangdump.dmp
	D:\Repos\TNC.Trading\TNC.Trading.Platform\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TestResults\cd976083-d692-4d19-b0c7-66ba333f286a\dotnet_13560_20260630T153415_hangdump.dmp
```

Important host diagnostic behavior:

```text
[xUnit.net 00:00:00.09]   Starting:    TNC.Trading.Platform.Web.FunctionalTests
...
TpTrace Verbose ... TcpClientExtensions.MessageLoopAsync: Polling ...
```

The testhost log in `artifacts/functional-tests-diag.host.26-06-30_15-32-45_93635_5.log` shows the testhost remained alive and idle after xUnit started the functional test, which is consistent with a startup hang inside fixture initialization rather than a testhost crash.

### AppHost runtime evidence

Important console output from direct AppHost startup:

```text
info: Aspire.Hosting.DistributedApplication[0]
			Now listening on: https://localhost:17257
info: Aspire.Hosting.DistributedApplication[0]
			Login to the dashboard at https://localhost:17257/login?t=294967f08ff9c117d56b456888acef28
info: Aspire.Hosting.DistributedApplication[0]
			Distributed application started. Press Ctrl+C to shut down.
```

No `Now listening on:` line for the Web front end was captured during this startup sample.

## Controlling Code And Configuration

The functional tests block in fixture initialization before any test body can execute:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:13`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:14`

The fixture starts the AppHost and then waits for a Web sign-in URL:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:14`
* `test/Shared/Authentication/AppHostProcessHandle.cs:87`

The discovery logic only succeeds if it can derive a candidate URL from `Now listening on:` output or newly observed local ports, and then follow redirects until it sees a Keycloak OIDC authorization path:

* `test/Shared/Authentication/AppHostProcessHandle.cs:205`
* `test/Shared/Authentication/AppHostProcessHandle.cs:214`
* `test/Shared/Authentication/AppHostProcessHandle.cs:219`
* `test/Shared/Authentication/AppHostProcessHandle.cs:226`
* `test/Shared/Authentication/AppHostProcessHandle.cs:255`
* `test/Shared/Authentication/AppHostProcessHandle.cs:327`
* `test/Shared/Authentication/AppHostProcessHandle.cs:129`

The AppHost still registers the Web project with external HTTP endpoints:

* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:36`
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:39`
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:40`

The Web application still exposes the sign-in route and anonymous redirect target expected by the tests:

* `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:16`
* `src/TNC.Trading.Platform.Web/Program.cs:49`
* `src/TNC.Trading.Platform.Web/Program.cs:55`
* `src/TNC.Trading.Platform.Web/Program.cs:63`

The AppHost wiring waits for API and Keycloak before the Web project becomes useful:

* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:37`
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:38`
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:180`

## Most Likely Root Cause

The most likely root cause is that the functional test harness never observes a reachable Web front-end listener that can complete the expected redirect chain to Keycloak, so fixture initialization hangs in `WaitForWebSignInUriAsync` and the run aborts before any assertion executes.

The evidence supports that conclusion:

* The functional fixture blocks entirely on AppHost startup and Web base URI discovery before test bodies run.
* The diagnostic run produced hang dumps for `testhost`, `dotnet`, and `TNC.Trading.Platform.AppHost`, which indicates a long-running startup wait rather than a normal assertion failure.
* A direct `dotnet run` of the AppHost reported only the Aspire dashboard listener (`https://localhost:17257`) in the captured output, which matches the discovery code's inability to identify the Web listener from `Now listening on:` lines.
* The discovery helper requires a candidate endpoint to eventually redirect to a URL containing `protocol/openid-connect/auth`; if the Web project never becomes externally reachable, or its endpoint is not surfaced in a way this helper can discover, the helper times out by design.

## Alternative Explanations Ruled Out

* The sign-in route itself is not missing: the Web app still maps `GET /authentication/sign-in` at `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:16`.
* The expected anonymous redirect is not absent from the Web app: `src/TNC.Trading.Platform.Web/Program.cs:49`, `:55`, and `:63` still redirect to `/authentication/sign-in?returnUrl=%2F&prompt=login`.
* The AppHost does not appear to have dropped the Web project registration entirely: `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:36-40` still adds the `web` project and marks it with external HTTP endpoints.
* The targeted diagnostic run does not point to a Playwright browser bootstrap problem. The selected test (`RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry`) uses `HttpClient`, not Playwright, and the hang occurs before any test body output.
* The targeted diagnostic run does not point to an xUnit or testhost crash. The host diagnostic log shows the testhost remained running and polling after test start until the blame-hang timeout produced dumps.

## Recommended Resolution Direction

Focus on why the AppHost-started Web application is not becoming discoverable to the harness within the startup timeout.

Most direct checks:

* Inspect the AppHost runtime and Aspire resource state to confirm whether the `web` resource is actually starting and binding an external endpoint when launched under the functional test environment.
* Verify whether the Web app is blocked waiting on API or Keycloak readiness rather than exposing its listener.
* If the Web resource is healthy but not discoverable, adjust the startup detection strategy in the shared AppHost process handle to read Aspire resource endpoints more reliably than scraping `Now listening on:` lines and local ports.

## Remaining Gaps

* I did not inspect the hang dumps themselves, so I cannot prove which internal wait the AppHost process was in at the moment of abort.
* I captured direct AppHost console output showing only the Aspire dashboard listener, but I did not query Aspire's runtime resource model to prove whether the `web` resource failed to start versus started without emitting a discoverable listener line.
