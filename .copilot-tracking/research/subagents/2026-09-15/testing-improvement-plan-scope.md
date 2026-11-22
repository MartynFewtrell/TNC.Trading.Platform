---
title: Testing Improvement Plan Scope Research
description: Verified CI, test artifact, and Keycloak authority migration evidence for staged testing improvements.
ms.date: 2026-09-15
ms.topic: reference
---

## Research Scope

* Identify active CI workflows and test configuration paths.
* Identify existing test-result and diagnostic-artifact publication behavior.
* Trace fixed Keycloak authority wiring in the AppHost and its current test coverage.
* Determine the existing Aspire resource-reference pattern for flowing Keycloak authority to API and Web without a hard-coded localhost port.
* Recommend planned files, validation commands, and an implementation sequence for fast, Aspire, browser, and failure-artifact CI stages.

## Status

Complete. Findings distinguish verified repository behavior from proposed changes.

## Findings

### Current CI and Test Configuration

* No CI workflow is currently committed. `.github/workflows/` exists but is empty. Workspace searches found no GitHub Actions YAML, Azure Pipelines YAML, `.runsettings`, or shared `Directory.Build.*` test configuration.
* `dotnet build` was last run successfully from the repository root on 2026-09-15. The repository's documented general validation command is `dotnet test`; `docs/wiki/local-development.md` lines 208-220 also documents `dotnet test -m:1` only as a local workaround for intermittent MSBuild child-node exits.
* API integration and AppHost unit projects reference `Aspire.Hosting.Testing` `13.5.3`: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` lines 9-13 and `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` lines 9-17.
* Web functional and E2E projects reference both `Aspire.Hosting.Testing` and `Microsoft.Playwright` or `Microsoft.Playwright.Xunit`: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` lines 9-16 and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` lines 9-16.
* API integration disables xUnit collection parallelism globally in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json` lines 1-4. The real-auth, synthetic-auth, and Web E2E collection definitions each also use `DisableParallelization = true`: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/AuthenticationIntegrationTestCollection.cs` line 3, `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestCollection.cs` line 3, and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs` line 5.

### Current Artifact Behavior and Gaps

* There is no CI artifact publication. No workflow contains `actions/upload-artifact`, `UploadArtifact`, TRX logger configuration, a results directory, or failure-only collection.
* Several unit and integration projects carry `coverlet.collector` `6.0.4`; for example, API integration at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` line 11 and AppHost unit at `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` line 12. Existing local Coverlet output appears below individual project `TestResults/` directories. This is generated local evidence, not a published CI contract.
* The shared synthetic-token fixture records terminal Aspire resource states in memory and appends them to an initialization exception: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` lines 20-23 and 94-108. It does not write resource logs or diagnostics to an artifact directory.
* Existing browser coverage uses `Microsoft.Playwright.Xunit` and `PageTest`: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs` lines 2-8. The current context config accepts development certificates only at lines 15-18. No test code starts tracing, records video, or saves failure screenshots.
* SQL integration failures are not presently collected into a CI diagnostic artifact. The previous research identifies an unowned Docker SQL discovery fixture at `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` lines 18-26 and 48-99. Its connection and cleanup errors currently reach test output only.

### Fixed Keycloak Authority Wiring

* `src/TNC.Trading.Platform.AppHost/AppHostCompositionConstants.cs` line 7 defines the hard-coded authority `https://localhost:8080/realms/tnc-trading-platform`.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` lines 41-53 registers Keycloak with `port: 8080`, disables endpoint proxy support, assigns the configured lifetime, and imports the realm. The Keycloak endpoint exposed by the integration is named `http`.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` lines 35-59 supplies that fixed constant when the API provider is Keycloak; lines 65-81 supplies it for Web unconditionally. `ConfigureAuthenticationProvider` applies these values and only adds `WaitFor(keycloak)` at lines 164-190. It does not add a Keycloak reference to either project.
* API and Web use the supplied `Authentication:Keycloak:Authority`, rather than deriving an authority internally: `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs` lines 77-83 and `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs` lines 85-91.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs` lines 13-24 and 38-47 shows the existing composition style: API references SQL and Web references API plus SQL. This is the nearest established resource-reference pattern.
* Current AppHost tests assert the fixed constant in three places: `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs` lines 84, 135, and 334, plus `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs` line 122. They assert Keycloak wait dependencies but not a resource reference or a runtime-resolved authority.
* The real-auth token helper hard-codes the same endpoint independently at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs` line 12. Its requests at lines 23-57 will remain port-bound unless the helper receives a fixture-resolved Keycloak base URI.
* The managed AppHost test factory explicitly disables randomized ports through `DcpPublisher:RandomizePorts=false` at `test/Shared/Authentication/RealAppHostProcessFactory.cs` lines 88-94. It returns a runtime-resolved Web endpoint at lines 96-109, but the handle then continues socket-based API/Web probing in `test/Shared/Authentication/AppHostProcessHandle.cs` lines 52-170.
* Fixed-port coordination is used by synthetic API, real API, Web functional, and Web E2E fixtures. Examples are `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` lines 19 and 28; `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` lines 10-20 and 55-58; and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` lines 10-18 and 46-49. The lease implementation is `test/Shared/Authentication/KeycloakPortLease.cs` lines 5-44.

### Verified Aspire Migration Direction

* The installed `Aspire.Hosting.Keycloak` package documents `builder.AddKeycloak("keycloak")` followed by `.WithReference(keycloak)`. Its `AddKeycloak` API accepts a nullable fixed-port parameter, so omitting `port: 8080` permits the test host's normal dynamic endpoint behavior.
* The installed `Aspire.Hosting` `13.5.3` documentation exposes `WithEnvironment` overloads for `EndpointReference`, `ReferenceExpression`, and interpolated reference expressions. Therefore the authority must be a reference expression, not a value resolved during AppHost composition.
* Recommended exact AppHost shape, subject to the issuer decision below:

```csharp
var keycloakAuthority = ReferenceExpression.Create(
  $"{infrastructure.Keycloak.GetEndpoint(\"http\").UriExpression}/realms/{AppHostCompositionConstants.KeycloakRealmName}");

configuredApi = configuredApi
  .WithReference(infrastructure.Keycloak)
  .WithEnvironment("Authentication__Keycloak__Authority", keycloakAuthority)
  .WaitFor(infrastructure.Keycloak);

configuredWeb = configuredWeb
  .WithReference(infrastructure.Keycloak)
  .WithEnvironment("Authentication__Keycloak__Authority", keycloakAuthority)
  .WaitFor(infrastructure.Keycloak);
```

This follows the existing `WithReference` plus `WaitFor` model while separating service discovery/configuration from startup ordering. It removes the hard-coded authority constant and `port: 8080`; the endpoint expression resolves to the assigned authority at runtime. Keep the realm name as a stable composition constant.

* Focused AppHost tests should use `GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)`, matching the existing test approach at `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs` lines 214-215, 245-246, and 318-321. Assert that API and Web contain `Authentication__Keycloak__Authority` as an endpoint-reference-derived value and omit `localhost:8080`; assert both resources contain a Keycloak reference and wait annotation. Do not evaluate a run-mode endpoint from this low-cost test because the installed Aspire test surface can block while resolving runtime endpoint value providers.
* Closed-box tests should retain `DistributedApplicationTestingBuilder`, use `app.GetEndpoint("keycloak", "http")` for the fixture's token client, and use `app.CreateHttpClient("api")` or `app.GetEndpoint("web", "https")` for service access. The synthetic fixture already demonstrates named API client creation at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` lines 37-43 and 79-88.

## Planned Files

| Planned file | Planned responsibility |
| --- | --- |
| `.github/workflows/ci.yml` | New four-job GitHub Actions workflow: fast tests, Docker-backed Aspire tests, browser real-Keycloak tests, and failure-only artifact upload. |
| `src/TNC.Trading.Platform.AppHost/AppHostCompositionConstants.cs` | Remove `KeycloakAuthority`; retain `KeycloakRealmName`. |
| `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` | Remove fixed `port: 8080`; retain session lifetime and realm import for test overrides. Reassess `WithEndpointProxySupport(proxyEnabled: false)` against the selected issuer model. |
| `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` | Build the realm authority from the Keycloak `http` endpoint `UriExpression`; apply `WithReference(keycloak)`, dynamic authority, and `WaitFor(keycloak)` to API and Web. Keep the synthetic API branch free of Keycloak authority/reference if that boundary remains intentional. |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs` | Replace fixed-authority assertions with publish-mode endpoint-expression and Keycloak-reference assertions. |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs` | Assert the default Keycloak API/Web reference and wait topology, while preserving the synthetic API exemption. |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs` | Assert no fixed Keycloak port annotation is present and the named `http` endpoint remains available. |
| `test/Shared/Authentication/RealAppHostProcessFactory.cs` | Remove `DcpPublisher:RandomizePorts=false`, fixed-port environment assumptions, and managed-process wrapper behavior no longer needed after fixtures use direct distributed-application clients. |
| `test/Shared/Authentication/AppHostProcessHandle.cs` | Remove socket enumeration and listener probing after all consumers use named endpoints. Retain only cleanup functionality if not moved into fixtures. |
| `test/Shared/Authentication/KeycloakPortLease.cs` | Delete after all fixture consumers are migrated; remove linked compilation entries and its tests in the same change. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` | Use `DistributedApplicationTestingBuilder` directly, receive the Keycloak `http` endpoint at runtime, and pass it to the token helper. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs` | Replace static `TokenEndpoint` with an authority or token-endpoint argument supplied by the fixture. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` | Remove the lease; keep named-resource readiness and add failure artifact export. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` | Replace lease/process-handle discovery with direct test-host Web/Keycloak endpoints. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` | Replace lease/process-handle discovery with direct test-host Web/Keycloak endpoints. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs` | Replace localhost-specific URL expectation and add failure trace, screenshot, and video capture. |
| `test/Shared/Diagnostics/AspireTestFailureArtifactCollector.cs` | New shared helper, linked by the AppHost-backed test projects, to write resource-state transitions, endpoint metadata with secrets redacted, container identifiers, and SQL diagnostics to `TEST_ARTIFACTS_DIRECTORY` only on fixture failure. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` | Link the shared diagnostic helper and remove obsolete authentication helper links. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` | Link the shared diagnostic helper and remove obsolete authentication helper links. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` | Link the shared diagnostic helper and remove obsolete authentication helper links. |
| `docs/wiki/local-development.md` | Replace fixed-port and lease guidance at lines 227-246 with dynamic endpoint and failure-artifact guidance after implementation is green. |
| `docs/wiki/testing-and-quality.md` | Add the CI stage ownership and retained browser/real-Keycloak artifact policy after implementation is green. |

## Proposed CI Stages and Commands

| Stage | Test scope | Required command pattern | Failure artifacts |
| --- | --- | --- | --- |
| Fast tests | All `*.UnitTests` projects and `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests` | `dotnet test <project> --no-restore --logger "trx;LogFileName=<project>.trx" --results-directory artifacts/test-results/fast/<project>` | TRX and Coverlet XML where the project has the collector |
| Docker-backed Aspire tests | API integration, Web functional, and infrastructure SQL integration | `dotnet test <project> --no-restore --logger "trx;LogFileName=<project>.trx" --results-directory artifacts/test-results/aspire/<project>` | TRX, Coverlet XML, helper-written resource logs, sanitized endpoint map, Docker inspect/log output, and SQL diagnostics |
| Browser and real Keycloak | Web E2E project, after installing the Playwright browser on the Linux runner | `pwsh test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium`; then `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore --logger "trx;LogFileName=web-e2e.trx" --results-directory artifacts/test-results/browser` | TRX, resource logs, SQL diagnostics, Playwright trace ZIP, screenshots, and video |
| Final summary | Dependency ordering and optional branch-gate summary | `dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"` after the stage-specific jobs are individually green | Job summary links to the stage artifacts; do not rely on this final command as the only source of results |

The new workflow should use `actions/upload-artifact@v4` with `if: ${{ failure() }}` for high-volume diagnostics and an unconditional upload for TRX results. Give each job a unique artifact name and output folder. The Docker-backed and browser jobs require a Linux hosted runner with Docker available. The browser job should install the Playwright browser after `dotnet build --configuration Release --no-restore`, because the generated `playwright.ps1` is copied to the output directory.

## Implementation Sequence

1. Add focused AppHost tests first. Capture the intended resource-reference and generated authority contract before changing composition.
2. Remove fixed `port: 8080` and `KeycloakAuthority`; wire API and Web to the Keycloak `http` endpoint through a reference expression and `WithReference(keycloak)`. Run AppHost unit tests immediately.
3. Convert the real API fixture and token helper to direct `DistributedApplicationTestingBuilder` endpoint access. Remove its lease and run the API integration project with default randomized ports.
4. Convert Web functional and E2E fixtures to direct named endpoints. Remove socket probing, `DcpPublisher:RandomizePorts=false`, and the lease only after every linked consumer is migrated. Run functional and E2E projects twice to detect lifecycle leakage.
5. Add `AspireTestFailureArtifactCollector` and the Playwright failure-output mechanism. Validate a deliberate, temporary failure locally to confirm each artifact is written, then remove the temporary failure.
6. Add `.github/workflows/ci.yml` with the four stages, per-project TRX output, and conditional diagnostic upload. Validate workflow YAML and run the exact local stage commands.
7. Update `docs/wiki/local-development.md` and `docs/wiki/testing-and-quality.md` after the implementation tests pass. Do not leave fixed-port instructions after the migration.

## Open Decisions

* Keycloak issuer model: `WithEndpointProxySupport(proxyEnabled: false)` currently contributes to a localhost issuer, while the planned endpoint expression supplies a dynamically assigned host port. Confirm whether tests should validate a dynamically advertised external issuer, or whether Keycloak needs an explicit hostname/proxy configuration so its issuer, discovery endpoint, redirects, and JWT issuer remain identical. This must be resolved before removing the fixed port.
* AppHost test assertion form: verify the exact manifest/reference-expression text emitted by `Aspire.Hosting` `13.5.3` in a focused test before asserting it verbatim. The semantic requirement is an endpoint-derived authority with no `localhost:8080`, not a fragile serialization string.
* SQL diagnostics ownership: decide whether the shared failure collector queries the fixture-owned SQL connection directly or the workflow captures only Docker metadata and resource logs. Direct `SqlConnection` diagnostics provide migration version and database-state evidence but must redact all connection-string credentials.
* Playwright retention policy: decide whether traces/screenshots/videos are recorded only after a test fails, or always written and uploaded only on failure. The former minimizes runtime and artifact volume; the latter gives fuller failure timelines.

## Open Decisions

Pending targeted source and workflow inspection.---
title: Testing Improvement Plan Scope
description: Verified scope and delivery order for resolving current test-harness failures.
author: GitHub Copilot
ms.date: 2026-09-15
ms.topic: troubleshooting
keywords:
  - testing
  - aspire
  - keycloak
  - sql-server
---

## Research Topics

* Verify the test projects, fixtures, shared helpers, dependencies, documentation, and validation commands implicated by `failing-tests-research.md`.
* Define which migrations share infrastructure and which can proceed independently.
* Record bounded implementation order, required documentation and artifact updates, and unresolved decisions.

## Findings

### Verified failure boundaries

* Web unit is an independent dependency correction. `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:15-16` directly references `AngleSharp` 1.8.1 beside `bunit.web` 1.40.0. The primary evidence records four pre-assertion `MissingMethodException` failures. Retain bUnit and remove only the direct AngleSharp override.
* API integration owns two fixture models in one project. The synthetic collection uses `SyntheticTokenIntegrationTestFixture` through `AuthenticationIntegrationTestCollection` at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/AuthenticationIntegrationTestCollection.cs:3-5`; it creates a named `api` client at `Authentication/SyntheticTokenIntegrationTestFixture.cs:33-38,76-84`. The real-token suite uses `RealAuthenticationIntegrationTestFixture` at `Authentication/RealAuthenticationIntegrationTestFixture.cs:6-35`, starts a separate process at line 26, and gets its API URI through listener discovery at line 27.
* The synthetic fixture is a named-client control, not the intended shared lifecycle. It still acquires `KeycloakPortLease` at `Authentication/SyntheticTokenIntegrationTestFixture.cs:28` and sets a process-wide environment override at line 31. The shared migration must remove these patterns from both synthetic and real-auth paths while preserving the synthetic API-only provider behavior.
* API real-auth, Web functional, and Web E2E must migrate together. Their projects all compile the shared AppHost process handle, Keycloak lease, and AppHost factory: API `TNC.Trading.Platform.Api.IntegrationTests.csproj:31-34`; functional `TNC.Trading.Platform.Web.FunctionalTests.csproj:27-31`; E2E `TNC.Trading.Platform.Web.E2ETests.csproj:27-31`.
* `test/Shared/Authentication/AppHostProcessHandle.cs:72-125` finds the API by iterating inferred candidate URIs. Its candidates derive from observed TCP listeners at lines 251-278 and 319-373. `RealAppHostProcessFactory.StartManagedAppHostProcessAsync` disables port randomization with `DcpPublisher:RandomizePorts=false` at `test/Shared/Authentication/RealAppHostProcessFactory.cs:83-104`, scopes configuration through process-wide environment variables at lines 71-75, and provides the Web endpoint only after that startup at lines 105-118. This conflicts with the currently applicable Aspire-test guidance at `.github/instructions/aspire-tests.instructions.md:25-44`.
* `test/Shared/Authentication/KeycloakPortLease.cs:22-54` coordinates a fixed `keycloak-port-8080.lock`; its fixed-port premise is also documented in `docs/wiki/testing-and-quality.md` under "Port coordination and concurrent validation" and `docs/wiki/local-development.md` under "Automated validation". Its dedicated tests are `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/KeycloakPortLeaseTests.cs` and must be removed or replaced when the helper is retired.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs:37-45` selects a port using `TcpListener`, disposes that reservation, then binds an `HttpListener` to the released port. The functional fixture consumes it at `Authentication/RealAuthenticationFunctionalTestFixture.cs:18-23`; the E2E fixture does so at `Authentication/RealAuthenticationE2ETestFixture.cs:18-23`. Account-preferences consumers are `FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:8-16` and `E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:7-15`; migrate them with the shared provider change.
* Infrastructure SQL is a separate service-adapter migration. `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:13-42` searches a running Docker image, reads its password, and creates a database. It resets that database at lines 64-91 and drops it at lines 93-106, but never owns or awaits the SQL Server itself. The only consumers are `AccountPreferencesSqlIntegrationTests` and `PlatformSqlServerIntegrationTests`, found in the same project. Its project has no `Aspire.Hosting.Testing` reference (`TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:11-26`).

### Applicable guidance and documentation

* `.github/instructions/tests.instructions.md:29-70` requires independently runnable project types, deterministic external-state cleanup, and no global serialization for order-dependent tests.
* `.github/instructions/aspire-tests.instructions.md:22-45` requires `DistributedApplicationTestingBuilder`, closed-box assertions, disposal, and concurrent randomized-port instances. `.github/instructions/playwright.instructions.md:20-46` applies to E2E migration and requires xUnit Playwright, web-first assertions, and no explicit waits.
* `docs/wiki/testing-and-quality.md` currently identifies the affected project roles in its "Test projects" table and documents listener discovery, fixed Keycloak port `8080`, and lease serialization in "AppHost-backed distributed validation model", "Keycloak readiness and isolation", and "Port coordination and concurrent validation". Update this page to name resource clients/endpoints, fixture-scoped configuration, readiness, and randomized ports.
* `docs/wiki/local-development.md` separates manual stable Keycloak access from automated validation. Update its "Validate" section only: preserve manual local endpoint guidance, but replace automated listener-discovery and lease statements with the managed test-host model.
* Extend `.github/instructions/aspire-tests.instructions.md` with explicit prohibitions on listener/dashboard discovery and process-wide configuration mutation. Extend `.github/instructions/tests.instructions.md` with fixture ownership/readiness requirements for mutable container dependencies. Record the concise durable routing rule in `.github/AGENTS.md` or `.github/lessons.md`.

### Migration groups and implementation order

1. Remove the Web unit direct AngleSharp reference. This is standalone and restores the bUnit suite without changing test topology.
2. Establish a shared managed AppHost fixture used by both API auth modes. Reuse the synthetic fixture's `CreateHttpClient("api")` contract, but replace its lease and environment mutation. Migrate `RealAuthenticationIntegrationTestFixture` and its wrapper `Authentication/RealAppHostProcessFactory.cs` in the API project.
3. Migrate the Web functional and E2E fixture pair and their thin factories onto that shared resource-oriented model. Use the named `web` endpoint/client, retain their functional and Playwright assertions, and make `ControllableIgProvider` endpoint-owning before it is injected as `Ig__AccountPreferencesBaseUrl`.
4. After every shared consumer has moved, delete `AppHostProcessHandle`, `RealAppHostProcessFactory`, `KeycloakPortLease`, project-local forwarding wrappers, and lease tests. Keep no socket scanning, dashboard interpretation, fixed port, or global environment scope.
5. Independently replace `SqlServerDatabaseFixture` Docker discovery with a test-owned SQL Server lifecycle, explicit readiness, unique database/reset boundary, and disposal. A dedicated Testcontainers-style fixture best preserves this project's service-local scope; a minimal Aspire fixture is viable only if the team explicitly wants that project to depend on AppHost semantics.
6. Update the instructions and wiki pages listed above, then run the focused projects twice and the full solution under ordinary concurrency.

### Focused and full validation commands

Run from the repository root. The solution file includes all ten current test projects at `TNC.Trading.Platform.slnx:1-35`.

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformAuthenticationIntegrationTests"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformAuthenticationSyntheticTokenIntegrationTests"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~Authentication|FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~Authentication|FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"
```

The two Web higher-level commands should be run twice after the provider migration. The API integration, Web functional, and Web E2E project commands should also be launched concurrently once after the AppHost fixture migration to prove that resources do not rely on the retired lease.

### Required implementation artifacts

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs`
* `test/Shared/Authentication/AppHostProcessHandle.cs`, `test/Shared/Authentication/KeycloakPortLease.cs`, and `test/Shared/Authentication/RealAppHostProcessFactory.cs`, followed by their linked compile entries and project-local forwarding wrappers
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` and both Web real-auth fixtures
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` and its project file if a lifecycle library is selected
* `.github/instructions/aspire-tests.instructions.md`, `.github/instructions/tests.instructions.md`, `.github/AGENTS.md` or `.github/lessons.md`, `docs/wiki/testing-and-quality.md`, and `docs/wiki/local-development.md`

### Unresolved decisions

* Choose whether the shared AppHost fixture lives as linked source under `test/Shared` or in a dedicated shared test project. Linked source matches the current helper model; a project gives a stronger compile-time dependency boundary.
* Confirm that the imported Keycloak realm accepts the randomized Web callback/origin for the real authorization-code browser journey before deleting fixed `8080` behavior. The primary evidence describes wildcard localhost support, but the existing active guidance still asserts an exact fixed issuer.
* Decide whether infrastructure provider integration uses a dedicated SQL test container or a minimal Aspire-managed SQL fixture. Do not use the full application AppHost for service-local SQL adapter behavior.

## References

* Primary evidence: `.copilot-tracking/research/2026-09-15/failing-tests-research.md`
