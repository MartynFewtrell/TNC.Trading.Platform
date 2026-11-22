---
title: Failing Tests Investigation
description: Evidence-led investigation of automated test failures while the solution builds and runs.
author: GitHub Copilot
ms.date: 2026-09-15
ms.topic: troubleshooting
keywords:
  - dotnet
  - tests
  - aspire
  - keycloak
  - sql-server
---

## Status

Complete. No product or test code was modified. The present result is 605 total tests, 518 passed, 87 failed, and 0 skipped. The failures divide into one deterministic dependency regression and three test-infrastructure failures; no captured failure reaches a product-behavior assertion.

## Research scope

Investigate why automated tests fail in a workspace where `dotnet build` succeeds and the application runs. Do not modify product or test code.

## Verified Executive Findings

`dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"` completed in 62.9 seconds. A second run using TRX output outside the worktree confirmed the same result.

| Failure | Count | Verified root cause | Classification |
|---|---:|---|---|
| Web unit | 4 | Commit `52bfba4` added direct AngleSharp `1.8.1` at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:15`, overriding bUnit `1.40.0`'s declared AngleSharp `1.2.0` dependency at line 16. The result is `MissingMethodException` for `IHtmlCollection<T>.Item(Int32)`. | Deterministic test dependency regression |
| API real-auth integration | 15 | `test/Shared/Authentication/AppHostProcessHandle.cs:76` discovers API endpoints by scanning new local ports and times out at line 125. The AppHost listener at `src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:8` is the Aspire dashboard, not the API. The synthetic API fixture passes because it uses `DistributedApplicationTestingBuilder` and `CreateHttpClient("api")` at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:33` and line 89. | Test harness/AppHost defect |
| Web functional and E2E | 10 | `test/Shared/AccountPreferences/ControllableIgProvider.cs:37` obtains an ephemeral TCP port, then starts an `HttpListener` at lines 39-45 after releasing the reservation. All failures are `HttpListenerException` address-in-use during fixture setup. | Test harness port race |
| Infrastructure SQL integration | 58 | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:18` locates any running SQL image rather than owning SQL lifecycle, then connects using the resolved port at line 48. All 29 cases and 29 cleanup results fail with SQL TCP connection refused. | Test infrastructure ownership/readiness defect |

The strongest resolution order is: restore a bUnit-compatible AngleSharp graph; replace the API real fixture with the existing Aspire test-host pattern; make each higher-level fixture own and await its resource lifecycle; replace image-based SQL discovery; then replace the TCP-listener/HTTP-listener handoff. Historical quality-gate artifacts also establish a separate fixed Keycloak port-8080 lease timeout mechanism in `test/Shared/Authentication/KeycloakPortLease.cs:25` and line 33.

## Detailed Evidence

  * `dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"` completed in 62.9 seconds with 605 total tests: 518 passed, 87 failed, 0 skipped.
  * A second clean test execution wrote TRX files outside the repository at `%TEMP%/TNC.Trading.Platform-test-investigation-20260915`. Its aggregate result is identical: 605 total, 518 passed, 87 failed.
  * The test environment uses .NET SDK `10.0.302` according to `global.json:3`, but the historical August quality gate used `10.0.400-preview.0.26322.102`. This is a risk factor, but it is not needed to reproduce the current failures.
  * No `*.runsettings`, central `Directory.Packages.props`, or checked-in `packages.lock.json` file exists. Only API integration supplies `xunit.runner.json`, whose `parallelizeTestCollections` setting is `false` at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:3`.
  * `artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/quality-gate.log` records a previous quality gate. Historical TRX aggregation records repeated `Keycloak port 8080 lease timed out` failures against `C:\Users\martynfewtrell\AppData\Local\Temp\TNC.Trading.Platform\leases\keycloak-port-8080.lock`.

  ### Reproduction commands

  ```powershell
  dotnet build
  dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"
  ```

  The investigative rerun used the following command so results remained outside the worktree:

  ```powershell
  $resultDirectory = Join-Path $env:TEMP 'TNC.Trading.Platform-test-investigation-20260915'
  dotnet test TNC.Trading.Platform.slnx --no-build --results-directory $resultDirectory --logger "trx;LogFilePrefix=investigation" --logger "console;verbosity=minimal"
  ```

  ### Current test-result distribution

  | Test project | Result | Classification |
  |---|---:|---|
  | Architecture integration | 13 passed | Healthy |
  | Application unit | 69 passed | Healthy |
  | API unit | 131 passed | Healthy |
  | AppHost unit | 43 passed | Healthy |
  | Infrastructure unit | 123 passed | Healthy |
  | Web unit | 125 passed, 4 failed | Deterministic dependency regression |
  | API integration | 14 passed, 15 failed | Process-launcher endpoint-discovery failure |
  | Web functional | 8 failed | Local HTTP listener port collision |
  | Web E2E | 2 failed | Local HTTP listener port collision |
  | Infrastructure integration | 58 failed | SQL container ownership/readiness failure |

  The source inventory contains ten test projects and 484 declared `[Fact]` or `[Theory]` methods. Theory data expands the execution count to 605.

## Evidence log

  ### 1. Web unit tests have a deterministic package compatibility regression

  Four Web unit tests fail before their behavior assertions can run:

  * `AccountPreferencesTests.Select_ShouldChangePendingValue_WhenOperatorChoosesEnabledRadio`
  * `AccountPreferencesTests.Save_ShouldSendBooleanJsonValue_WhenSelectionIsChanged`, both data cases
  * `ConfigurationTests.Render_ShouldOpenEnvironmentAccordionByDefault_WhenConfigurationLoads`

  Three failures throw `System.MissingMethodException` for `AngleSharp.Dom.IHtmlCollection<T>.get_Item(Int32)`. The fourth is a bUnit `WaitForFailedException` whose inner exception is the same missing member. The failures originate in `Bunit.RefreshableElementCollection.get_Item` before reaching the corresponding test assertion, including `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:162`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:230`, and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:60`.

  Commit `52bfba4` (2026-09-14, "Update SDK versions and Keycloak URLs to use HTTPS") added a direct AngleSharp dependency at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:15`. The same project references `bunit.web` `1.40.0` at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:16`.

  The resolved graph contains direct `AngleSharp` `1.8.1` and `bunit.web` `1.40.0`. The installed bUnit package manifest, `C:\Users\martynfewtrell\.nuget\packages\bunit.web\1.40.0\bunit.web.nuspec`, specifies `AngleSharp` `1.2.0` for supported target frameworks. The explicit `1.8.1` override is therefore incompatible with the bUnit binary being executed. This is a test dependency regression, not an application regression.

  ### 2. API real-authentication integration tests use an invalid AppHost endpoint-discovery contract
* `dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"` on 2026-09-15 completed in 62.9 seconds with 605 total tests: 518 passed, 87 failed, 0 skipped.
  Fifteen of 29 API integration tests fail from the real-authentication collection. The 14 synthetic-token tests pass because they use `DistributedApplicationTestingBuilder.CreateAsync` and `appHost.CreateHttpClient("api")` at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:33`, `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:39`, and `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:89`.

  The failing real fixture instead acquires the port lease and launches `dotnet run` at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:20` and `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:26`. `test/Shared/Authentication/RealAppHostProcessFactory.cs:11` creates that process. It forces fresh infrastructure containers at lines 19-21, but does not obtain resource endpoints through the Aspire testing API.

  `test/Shared/Authentication/AppHostProcessHandle.cs:76` polls for an API URI, then throws the observed timeout at line 125. Its candidate list is inferred from newly opened host ports at lines 278 and 373. That inference is unsafe because the AppHost's listener is the Aspire dashboard, not an API resource endpoint.
* `artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/quality-gate.log` records the quality gate invocation. Historical TRX aggregation includes repeated `Keycloak port 8080 lease timed out` failures, demonstrating an earlier independent contention problem in the same integration-test infrastructure.
  The current Aspire CLI log confirms the process starts normally: it creates `api`, `web`, `sql`, and `keycloak`, and reports `https://localhost:17257` as the dashboard. The same dashboard endpoint is configured in `src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:8`; the HTTP dashboard endpoint is at line 21. The AppHost composition itself is conventional at `src/TNC.Trading.Platform.AppHost/AppHost.cs:3` through `src/TNC.Trading.Platform.AppHost/AppHost.cs:13`.
## Findings
  The resource model is likewise valid: SQL and Keycloak are registered at `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:21` and `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:42`. The failing tests do not prove an API authorization, endpoint, Keycloak, or product behavior regression. Their fixture terminates before an endpoint-specific request is made.
### Confirmed shared AppHost discovery failure
  ### 3. Web functional and E2E tests fail before AppHost startup because their controllable provider has a port race
The real authentication integration, functional, and E2E test fixtures use the process-based AppHost launcher in `test/Shared/Authentication/RealAppHostProcessFactory.cs`. It invokes `dotnet run` for the AppHost. `test/Shared/Authentication/AppHostProcessHandle.cs` then enumerates newly observed local TCP listeners and probes each as an API or web endpoint.
  All eight Web functional tests and both Web E2E tests fail with `System.Net.HttpListenerException: The process cannot access the file because it is being used by another process.` The failure occurs while their shared fixture calls `ControllableIgProvider.Start()` at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:21` and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:21`.

  `test/Shared/AccountPreferences/ControllableIgProvider.cs:37` picks a temporary ephemeral port with `TcpListener`, releases that listener at the end of the method scope, and starts `HttpListener` on the selected URI at lines 39-45. This creates a time-of-check/time-of-use port allocation race. The exact competing process was not retained in the TRX, so this is classified as a harness/environment failure, not a demonstrated product regression.

  ### 4. SQL integration tests do not own or await their SQL Server

  All 29 SQL test cases fail, and xUnit reports a matching 29 collection-cleanup failures, producing 58 failed results. The representative exception is `Microsoft.Data.SqlClient.SqlException`, TCP provider error 0: the remote computer refused the network connection. Failures occur in `ResetDatabaseAsync` at `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:70` and in disposal at line 99.

  The fixture neither creates nor starts SQL Server. At `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:18`, it searches Docker for any running `mcr.microsoft.com/mssql/server:2022-latest` container. Lines 22 and 26 inspect that arbitrary container and resolve a host port; line 48 opens the connection. The test execution concurrently runs AppHost-owning test fixtures, which may create, stop, or replace matching session-lifetime containers. The fixture has no ownership, readiness, or lifecycle guarantee for the container it finds. This explains the refused connection and the cleanup failures.

  ### 5. Fixed Keycloak port leasing remains a separate historical contention mechanism

  `test/Shared/Authentication/KeycloakPortLease.cs:25` creates a process-global, fixed lock file at line 33 for Keycloak host port 8080. The real API fixture acquires it at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:20`; the synthetic API and Web fixtures also acquire the same lease.

  Historical TRX failures show this lease timing out at 45 seconds (and earlier at five minutes). The current rerun did not leave a lock file, so it was not one of the current five failure signatures. It remains a reproducible scheduling hazard because test projects run concurrently while only API integration disables collection parallelization.

  ### Failure classification

  | Failure group | Failed results | Product assertion evaluated | Classification |
  |---|---:|---|---|
  | AngleSharp and bUnit incompatibility | 4 | No | Deterministic test dependency regression introduced by `52bfba4` |
  | API real-auth endpoint discovery | 15 | No | Test harness/AppHost integration defect |
  | Web functional/E2E listener collision | 10 | No | Test harness port-allocation race, conflicting process unconfirmed |
  | SQL integration connection and cleanup | 58 | No | Test infrastructure ownership/readiness defect |
  | Historical Keycloak lease timeout | Historical only | No | Test infrastructure contention defect |

  No captured failure is a behavioral assertion against product code. The 518 passing tests include all Architecture, Application unit, API unit, AppHost unit, and Infrastructure unit tests, plus 14 synthetic-token API integration tests.

  ### Relevant history

  * `52bfba4` (2026-09-14): adds explicit AngleSharp `1.8.1`, updates SDK and Keycloak URLs to HTTPS, and changes related integration-test dependencies.
  * `f4b3b3b` (2026-08-30): adds account-preferences unit/integration tests and last changed `WaitForApiBaseUriAsync`.
  * `8df68d8` (2026-07-28): moves the solution toward its Clean Architecture arrangement.
  * Historical quality gates under `artifacts/quality-gates/keycloak-testing-hardening*/` and `artifacts/quality-gates/account-preferences-ui-clarity/` retain the earlier port-lease and AppHost startup failures.

  ## Resolution approach

  1. Restore a compatible bUnit/AngleSharp graph first. Remove the direct AngleSharp `1.8.1` override so bUnit resolves its declared `1.2.0`, or upgrade bUnit to a release that explicitly supports AngleSharp `1.8.1`. Run only the Web unit project before changing any infrastructure tests.
  2. Replace the real API fixture's process-and-port scanning with the established `Aspire.Hosting.Testing` pattern already used in `SyntheticTokenIntegrationTestFixture`: use `DistributedApplicationTestingBuilder`, wait for named resources, and obtain the API client through `CreateHttpClient("api")`. This eliminates dashboard/resource confusion and removes the need to identify endpoints through local listener enumeration.
  3. Make every higher-level test fixture own its infrastructure. Prefer Aspire test-host fixtures for AppHost-backed suites, with randomized ports, resource readiness checks, and disposal. Remove the cross-project fixed port-8080 lease after the fixed Keycloak port is removed. This follows the repository's Aspire testing standard and avoids serializing unrelated projects.
  4. Give SQL integration a dedicated, test-owned SQL Server lifecycle. Either start and await an Aspire-managed SQL resource from the fixture or use a dedicated SQL test-container fixture. Do not select an arbitrary Docker container by image, scrape its password, or rely on a concurrently running AppHost.
  5. Replace the `TcpListener` then `HttpListener` handoff used by `ControllableIgProvider` with a test server that owns its dynamically assigned endpoint, or retry a complete listener allocation/bind when the race occurs. The chosen implementation must expose the final bound URI to the fixture.
  6. Validate in layers: Web unit project, API integration project, Web functional/E2E projects, Infrastructure integration project, then `dotnet test TNC.Trading.Platform.slnx --no-build`. Keep the outer solution execution parallel after resource ownership is fixed; do not conceal the races by globally serializing the suite.

  ## References

  * [Aspire testing overview](https://aspire.dev/testing/overview/)
  * [Aspire guide: write your first test](https://aspire.dev/testing/write-your-first-test/)
  * [bUnit 1.40.0 NuGet package](https://www.nuget.org/packages/bunit.web/1.40.0)
  * [.NET testing documentation](https://learn.microsoft.com/dotnet/core/testing/)
The current console evidence shows the child process is healthy enough to start the Aspire CLI dashboard at `https://localhost:17257`, but no API URI is exposed to the probe. `src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json` assigns `17257` and `15046` to the AppHost/dashboard process rather than the API. The harness is therefore trying to infer resource endpoints from host-local ports, an invalid contract for the current Aspire launcher.

The 87 current failures are environmental/test-harness failures because fixture initialization terminates before the tests make their endpoint-specific requests. No product assertion failure is visible in the captured result.
  * Which process held the endpoint selected by `ControllableIgProvider` during the reproduced Web fixture failures. The root race is demonstrated, but the competing process is not identified in retained evidence.
  * Whether a newer bUnit release supports AngleSharp `1.8.1` and the repository's `net10.0` target. The safer immediate correction is to remove the incompatible direct override.
  * Whether a planned CI environment requires a shared, long-lived SQL Server. If so, integration tests still need a stable explicit connection contract and readiness check rather than Docker image discovery.
`test/Shared/Authentication/KeycloakPortLease.cs` serializes tests using a fixed temporary lock file for host port 8080. Historical TRX records show repeated 45-second (and earlier five-minute) lease timeouts. This is a separate failure mechanism from endpoint discovery, but it affects the same real-AppHost test category and can mask the primary failure depending on test scheduling.

### Infrastructure preconditions
  * Prototype the API real-auth fixture using `DistributedApplicationTestingBuilder` and verify endpoint access with a single authorization scenario.
  * Evaluate the current bUnit release notes and dependency graph before selecting either a bUnit upgrade or an AngleSharp rollback.
  * Capture process and socket ownership when reproducing the `HttpListenerException` to determine whether a fixture leak or unrelated local process wins the port race.
  * Design a dedicated SQL integration fixture and run the SQL project both alone and concurrently with AppHost-backed suites.
## Open questions

* Which of the 87 failures belong to API integration, Web functional, and Web E2E projects, and whether each is exclusively a shared fixture failure.
* Whether the SQL integration suite passed in the current run or is bypassed/masked by its prerequisite.
* Whether the current AppHost process model can be replaced throughout by `Aspire.Hosting.Testing`, which is already referenced by the Web higher-level test projects.

## Recommended next research

* Parse the current console output by project and error type
* Inspect the three real-authentication fixtures and their startup paths
* Trace the port lease and process-discovery code through Git history
