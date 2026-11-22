# Current Tests to Aspire Migration

## Status

Complete. Research-only assessment: no product or test code was modified.

## Scope

- Assess the existing test architecture against the recommended .NET Aspire closed-box testing model.
- Inventory current high-level test coverage, fixtures, helpers, AppHost composition, configuration, and recent history.
- Define an incremental migration that retains useful behavioral coverage while replacing lifecycle, endpoint-discovery, and fixed-port mechanisms that conflict with modern Aspire testing.

## Baseline and governing standards

The repository is already aligned with several key structural decisions:

- The test projects are separated by level and live under `test/`, consistent with `.github/instructions/tests.instructions.md:25-35`.
- The API integration, Web functional, and Web E2E projects all reference `Aspire.Hosting.Testing` and the AppHost project: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11,27`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11,25`, and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11,24`.
- The local AppHost is a thin composition root: `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-13`. It composes stable named resources `sql`, `platformdb`, `mailpit`, `keycloak`, `api`, and `web`: `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:21-51` and `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:12-53`.
- The test rules already require `DistributedApplicationTestingBuilder`, external-boundary-only assertions, disposal, and random ports by default: `.github/instructions/aspire-tests.instructions.md:26-48`.

The current runtime implementation does not fully meet those already-adopted rules. The shared test factory contains both an obsolete child-process launcher and an Aspire-managed launcher, while the latter disables port randomization and is still surfaced through the obsolete process-handle abstraction.

### Recommended modern Aspire model

Current Aspire guidance supports this target model:

1. Use `DistributedApplicationTestingBuilder.CreateAsync<Projects.TNC_Trading_Platform_AppHost>()` to launch the AppHost and share the resulting `DistributedApplication` in an xUnit collection fixture for a coherent high-cost suite.
2. Start the app and await named resources through `ResourceNotifications` with a finite cancellation token. Await health, not merely `Running`, before behavior assertions.
3. Access external surfaces with `app.CreateHttpClient("api")`, `app.CreateHttpClient("web")`, or `app.GetEndpoint("web", "https")`; access a named database only through its published connection-string contract when direct data verification is necessary.
4. Retain Aspire's default randomized proxy ports and disabled dashboard. Do not infer resource URLs by scanning host sockets or depend on AppHost launch-profile/dashboard ports.
5. Dispose the application and builder at fixture completion. Tests remain closed-box: configure the AppHost through arguments/configuration and use only HTTP/browser/resource boundaries afterward.

Evidence: [Aspire testing overview](https://aspire.dev/testing/overview/), [Manage the AppHost in tests](https://aspire.dev/testing/manage-app-host/), and [Access resources in tests](https://aspire.dev/testing/accessing-resources/). The Aspire overview explicitly states that random ports and dashboard suppression are defaults, disposal cleans up resources, and tests cannot substitute service DI across process boundaries. The resource guidance recommends `CreateHttpClient`, `GetConnectionString`, `WaitForResourceHealthyAsync`, and bounded cancellation.

## Current architecture inventory

### Tests that already match the intended strategy

| Retain | Evidence | Assessment and migration action |
| --- | --- | --- |
| Application, API, Infrastructure, and Web unit projects | Project separation is documented in `docs/wiki/testing-and-quality.md:67-76`; unit tests use focused fakes/in-memory boundaries. | Retain. These are intentionally not Aspire tests and preserve the test pyramid. Do not convert policy, validation, component, or adapter translation tests to AppHost startup. |
| AppHost composition unit tests | `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs:21-111` builds and inspects resource names, waits, URLs, and provider parity without launching containers. | Retain. This is valuable low-cost composition characterization before distributed suites. Use it to guard resource names required by managed fixtures. |
| Synthetic API token fixture | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:17-42,84-91` uses `DistributedApplicationTestingBuilder`, starts the AppHost, and calls `CreateHttpClient("api")`. | Retain and rework. It is the closest local template. Remove the Keycloak fixed-port lease and process-global environment mutation; pass test configuration as `CreateAsync` arguments or use a factory lifecycle hook. Keep the deliberately synthetic issuer/audience/expiry tests as a separate closed-box configuration variant. |
| Real API authorization contracts | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:24-270` asserts public health, anonymous rejection, role/scopes, protected writes, and persisted audit outcomes through HTTP. | Retain behavior. Move its fixture to the same direct managed-host pattern as the synthetic fixture. This is a valid real Keycloak-plus-API integration slice. |
| Web functional security and redirect contracts | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs:21-166` verifies anonymous redirects, return-url normalization, GET sign-out rejection, CSRF rejection, and post-sign-out fail-closed navigation externally. | Retain behavior. Use a named `web` client/endpoint from a managed fixture. Keep browser/session setup separate from endpoint discovery. |
| Browser E2E sign-in smoke | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:21-54` uses Playwright and a real seeded Keycloak login to reach protected content. | Retain as a narrow browser proof. It is correctly external and high-value; replace regex localhost-port assumptions with the fixture-provided `web` endpoint. |
| Account-preferences high-level scenarios | Functional behavior is external in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:18-83`; browser rendering is external in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:20-43`. | Retain behavior only after the controllable provider is safely hosted and injected through AppHost configuration. These tests validate API/Web/SQL/provider interactions worth keeping at high level. |

### Fixtures and helpers

| Component | Evidence | Decision | Migration inventory |
| --- | --- | --- | --- |
| `SyntheticTokenIntegrationTestFixture` | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:14-91` | Rework | Extract a reusable managed AppHost fixture base/factory only if it removes duplication across project-level fixtures. Preserve named-resource readiness and `CreateHttpClient("api")`; replace the lease and process-wide `TestEnvironmentVariableScope`. |
| `RealAuthenticationIntegrationTestFixture` | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:7-70` | Rework | Replace `StartAppHostProcess`, local listener discovery, and independently created HTTP client with a managed AppHost, `WaitForResourceHealthyAsync("keycloak")`, API readiness probe through `CreateHttpClient("api")`, and `CreateHttpClient("api")` for each test. |
| `RealAuthenticationFunctionalTestFixture` and `RealAuthenticationE2ETestFixture` | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:7-58`; `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:7-58` | Consolidate/rework | These are near-duplicates. Give each suite a thin fixture over one shared managed-AppHost factory that exposes a named Web URI/client. Keep fixture-local Playwright and controllable-provider concern only where needed. |
| `RealAppHostProcessFactory.StartManagedAppHostProcessAsync` | `test/Shared/Authentication/RealAppHostProcessFactory.cs:55-142` | Retire/replace | It correctly creates a testing builder but disables random ports at lines 90-94, creates process-wide environment scopes at lines 79-84, and returns a process-oriented handle. Replace it with a clearly named shared managed fixture/factory that exposes `DistributedApplication` operations directly or with a small resource-oriented interface. |
| `RealAppHostProcessFactory.StartAppHostProcess` | `test/Shared/Authentication/RealAppHostProcessFactory.cs:10-53` | Retire | Starts `dotnet run` as a child process and discovers endpoints indirectly. The fixture at `RealAuthenticationIntegrationTestFixture.cs:24-30` still selects it, causing the current API real-auth failures recorded in `failing-tests-investigation.md:80-99`. |
| `AppHostProcessHandle` | `test/Shared/Authentication/AppHostProcessHandle.cs:7-295` | Retire after callers migrate | It combines child-process killing/output pumps, socket enumeration, HTTP discovery, and managed-AppHost disposal. `WaitForApiBaseUriAsync` scans new local ports at lines 61-125 and is not an Aspire endpoint contract. Resource clients/endpoints eliminate this mixed responsibility. |
| Per-project `AppHostProcessFactory` and `RealAppHostProcessFactory` forwarding wrappers | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAppHostProcessFactory.cs:5-15`; Web functional wrapper `.../RealAppHostProcessFactory.cs:5-18`; E2E wrapper `.../AppHostProcessFactory.cs:5-18` | Retire | Their only purpose is to preserve the process-handle API. Remove after direct fixture migration. |
| `KeycloakPortLease` | `test/Shared/Authentication/KeycloakPortLease.cs:5-105` | Retire after Keycloak endpoint migration | It serializes a hard-coded host port 8080 using a machine-global temp lock. It is acquired by all AppHost fixtures: synthetic API `...SyntheticTokenIntegrationTestFixture.cs:28`, real API `...RealAuthenticationIntegrationTestFixture.cs:20`, functional `...RealAuthenticationFunctionalTestFixture.cs:19`, and E2E `...RealAuthenticationE2ETestFixture.cs:19`. This contradicts default random ports and blocks cross-project concurrency. |
| `KeycloakReadinessPolicy` | `test/Shared/Authentication/KeycloakReadinessPolicy.cs:10-230` | Retain selectively | Preserve its bounded, issuer-validating HTTP readiness behavior when it verifies a real OIDC protocol contract after Aspire resource health. Do not use it to replace `WaitForResourceHealthyAsync`, and parameterize the authority from the managed Keycloak endpoint rather than `http://localhost:8080`. |
| `ControllableIgProvider` | `test/Shared/AccountPreferences/ControllableIgProvider.cs:7-124` | Rework | Keep its deterministic request ledger and failure/response controls. Replace the `TcpListener` reserve-then-`HttpListener` bind at lines 35-45, which the current failure report identifies as a TOCTOU port race (`failing-tests-investigation.md:104-113`). Use a test server that owns its assigned endpoint or a retryable bind factory that returns the actually bound URI. Feed its URI to the AppHost through builder configuration. |
| `SqlServerDatabaseFixture` | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:8-136` | Rework, choose ownership explicitly | It finds any Docker SQL image, scrapes its password/port, then creates a unique database. The failure report records 58 resulting failures from no lifecycle/readiness ownership (`failing-tests-investigation.md:115-127`). Prefer a dedicated test-owned SQL resource/container fixture for service-local SQL adapter integration. Do not make this project start the whole AppHost unless its assertions need API/Web/AppHost behavior. |

## Architecture gaps and risks

1. The API real-auth fixture bypasses `Aspire.Hosting.Testing` even though the package is present. It calls the child-process path and infers an API URI from local sockets: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:24-31`; `test/Shared/Authentication/AppHostProcessHandle.cs:61-125`. The recent investigation confirms 15 failures terminate at this discovery stage rather than a product assertion: `.copilot-tracking/research/subagents/2026-09-15/failing-tests-investigation.md:80-99`.
2. The nominally managed Web route violates the default-port rule by passing `DcpPublisher:RandomizePorts=false`: `test/Shared/Authentication/RealAppHostProcessFactory.cs:90-94`. It then needs a fixed Keycloak port and cross-project lease. This conflicts with `.github/instructions/aspire-tests.instructions.md:39-44` and Aspire's concurrency model.
3. The current shared handle uses host process and TCP listener inspection to discover resources. The dashboard is an AppHost endpoint, not the `api` or `web` resource endpoint; the prior failure investigation documents this mismatch (`failing-tests-investigation.md:92-99`).
4. Process-wide environment variable scopes mutate global state in `test/Shared/Authentication/RealAppHostProcessFactory.cs:79-84`. Project-wide parallel test execution can observe the wrong authentication/provider or test endpoint configuration. Builder arguments/configuration are isolated to an AppHost instance.
5. Fixed Keycloak host origin is coupled to realm callback/origin policy and test documentation. The realm and browser callback setup must be confirmed to allow randomized localhost origins before deleting the port lease. Current documentation calls port 8080 intentional: `docs/wiki/testing-and-quality.md:262-287` and `docs/wiki/local-development.md:204-211`.
6. Infrastructure SQL integration is valid provider-specific coverage, but it is neither AppHost-backed nor test-owned. It should remain a service-local integration project with dedicated owned SQL lifecycle, rather than competing for an arbitrary AppHost container.
7. The current test configuration only disables xUnit collection parallelism in the API integration project: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:1-4`. That does not serialize the API, Web functional, and Web E2E assemblies. No repository-wide runsettings, central package management file, or package lock file was found; this raises reproducibility risk but should be addressed separately from the Aspire migration.

## Sensible staged migration

### Stage 0: Preserve characterization and establish focused baseline

Dependencies: Docker Desktop, current `global.json` SDK, AppHost build, and the existing AppHost composition unit tests.

- Do not change existing behavior tests yet. Run the AppHost unit project and the existing synthetic API test collection to pin expected resource names, waits, API health, and synthetic-token behavior.
- Capture the current `api`, `web`, `keycloak`, and `platformdb` resource contracts from the AppHost tests. These are the only names new managed fixtures should use.
- Focused validation:

```powershell
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformAuthenticationSyntheticTokenIntegrationTests"
```

### Stage 1: Introduce one resource-oriented managed AppHost fixture

Dependencies: Stage 0 and a verified technique for applying test-local configuration through builder arguments or a `DistributedApplicationFactory` lifecycle hook.

- Replace the shared factory's externally visible process-handle model with a managed fixture that owns `IDistributedApplicationTestingBuilder` and `DistributedApplication`.
- Pass `AppHost:UsePersistentKeycloakState=false` and `AppHost:UsePersistentSqlState=false` as AppHost arguments, preserving test session isolation without setting process-global environment variables.
- Expose operations appropriate to closed-box tests: `CreateApiClient()`, `CreateWebClient()`, `WebBaseUri`, `GetConnectionString("platformdb")` only when direct data verification is needed, and a bounded `WaitForResourceHealthyAsync(resourceName)` helper.
- Keep per-suite xUnit collection fixtures. Do not introduce a global solution fixture: test configuration variants and isolated state require separate AppHost instances.
- Focused validation: one anonymous API health assertion and one Web anonymous redirect assertion using named endpoints, with random ports still enabled.

### Stage 2: Migrate API real-token integration coverage

Dependencies: Stage 1 plus a managed Keycloak endpoint/authority exposed to `RealKeycloakAccessTokenFactory`.

- Rewrite only `RealAuthenticationIntegrationTestFixture` to use the new managed fixture. Preserve the real Keycloak token factory and all HTTP assertions in `PlatformAuthenticationIntegrationTests`.
- Await `keycloak` health, validate the realm discovery issuer against the dynamically supplied authority, then prove token acquisition. Await API readiness through `CreateHttpClient("api")`.
- Delete the API project's local `RealAppHostProcessFactory` wrapper once no callers remain.
- Focused validation:

```powershell
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformAuthenticationIntegrationTests"
```

### Stage 3: Migrate Web functional and browser fixtures

Dependencies: Stage 1, Stage 2's dynamic Keycloak authority approach, and verified realm wildcard callback/origin support for the random `web` URL.

- Replace Web fixture startup with the shared managed fixture and set Playwright navigation to `WebBaseUri`; remove `WaitForWebSignInUriAsync` and localhost-port regex assumptions.
- Keep HTTP functional tests and Playwright E2E tests in separate projects and fixtures because browser lifecycle is distinct, but remove duplicated startup code.
- Rework `ControllableIgProvider` before moving account-preferences scenarios. Route its actual bound URI via AppHost configuration before the AppHost is built.
- Focused validation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~Authentication"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~Authentication"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences"
```

### Stage 4: Remove process/port-era infrastructure and prove concurrency

Dependencies: Stages 2 and 3 passing with randomized ports.

- Delete `AppHostProcessHandle`, the child-process `StartAppHostProcess` branch, all forwarding factory wrappers, and `KeycloakPortLease` with its tests after their callers are gone.
- Simplify `KeycloakReadinessPolicy` to dynamic endpoint inputs or retain only the protocol assertion logic.
- Run API integration, Web functional, and Web E2E projects concurrently to demonstrate no shared host-port or process-global-config dependence. Do not hide failures by globally disabling solution parallelism.
- Focused validation: the three project commands above started concurrently, followed by the full solution suite.

### Stage 5: Give service-local SQL integration tests owned infrastructure

Dependencies: an explicit decision that Infrastructure integration remains provider-adapter coverage rather than an AppHost E2E suite.

- Replace Docker image discovery in `SqlServerDatabaseFixture` with a dedicated, test-owned SQL lifecycle. The preferred design is a dedicated SQL test container/resource that exposes a connection string and readiness signal to this project; use an Aspire-backed SQL fixture only if it provides a clean test-owned resource without starting unrelated API/Web projects.
- Preserve unique database creation and explicit cleanup. Move raw SQL migration, locking, and transaction tests unchanged onto that owned connection.
- Focused validation:

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj
dotnet test TNC.Trading.Platform.slnx --no-build
```

## Exact suggested updates to instructions and Copilot artifacts

These are proposed future edits, not changes made by this research task.

| Path | Proposed rule text | Reason |
| --- | --- | --- |
| `.github/instructions/aspire-tests.instructions.md` | `MUST obtain HTTP resource clients with DistributedApplication.CreateHttpClient(resourceName[, endpointName]) and resource URIs with GetEndpoint(resourceName, endpointName). MUST NOT discover AppHost resource endpoints by parsing AppHost output, scanning local TCP listeners, probing launch-profile ports, or inspecting the Aspire dashboard endpoint.` | Makes the existing closed-box rule executable and prevents the current API endpoint-discovery failure. |
| `.github/instructions/aspire-tests.instructions.md` | `MUST keep Aspire test configuration scoped to the testing builder or DistributedApplicationFactory lifecycle. MUST NOT set process-wide environment variables, mutate static application configuration, or use machine-wide locks to configure an AppHost test instance.` | Replaces process-global environment scopes and port locking with per-test-host configuration. |
| `.github/instructions/aspire-tests.instructions.md` | `When an AppHost resource has health checks, wait for WaitForResourceHealthyAsync with a caller-owned bounded cancellation token before exercising its external contract. Use protocol-specific probes only as an additional behavior check, not as a substitute for resource readiness.` | Encodes the recommended health-before-probe sequence. |
| `.github/instructions/aspire-tests.instructions.md` | `MUST retain default randomized proxied ports. A fixed endpoint is permitted only when the test proves an external protocol constraint cannot support dynamic endpoints; document the constraint and add a focused concurrent-run validation. Remove that exception when the constraint is eliminated.` | Tightens the existing port exception and creates an exit condition for the Keycloak 8080 lease. |
| `.github/instructions/tests.instructions.md` | `Integration tests that validate one service adapter against a database or provider MUST own or explicitly provision that dependency and await readiness. MUST NOT discover an arbitrary existing Docker container by image, inspect its credentials, or depend on an unrelated AppHost session.` | Prevents the SQL fixture's ownership/readiness failure while preserving service-local integration coverage. |
| `.github/instructions/tests.instructions.md` | `Use an xUnit collection fixture to share one managed AppHost only within a suite that has the same configuration and state-isolation requirements. Do not serialize unrelated test assemblies globally to compensate for fixed ports, mutable process state, or unowned resources.` | Keeps expensive startup amortized without masking isolation defects. |
| `.github/AGENTS.md` | Add under `Stable-learning upkeep`: `For Aspire test architecture changes, use a resource-oriented fixture contract (named clients/endpoints, bounded readiness, and disposal) rather than a process-oriented launcher contract.` | Gives specialized test agents a concise routing rule. |
| `.github/lessons.md` | Add: `For AppHost-backed tests, obtain resource endpoints through Aspire resource names and keep test configuration scoped to the testing builder; socket scanning, dashboard-port assumptions, fixed-port leases, and process-global environment overrides are migration smells.` | Records the durable lesson discovered by the failure investigation. |
| `docs/wiki/testing-and-quality.md` | Replace the statements that shared helpers discover runtime listeners from output/local listeners and intentionally coordinate fixed port 8080 (`:103-115`, `:247-287`) with: `AppHost-backed suites use named Aspire resource clients/endpoints and randomized proxied ports. Fixtures use bounded resource health waits and dispose their managed AppHost instances. Real OIDC readiness additionally verifies the provider discovery contract using the fixture-provided authority.` | The current wiki documents the retired target behavior as current practice. |
| `docs/wiki/local-development.md` | Replace fixed-Keycloak test-process/lease guidance in `:241-269` with a distinction between stable manual local development endpoints and randomized automated AppHost test endpoints. | Prevents operators and contributors from treating fixed-port coordination as required test architecture. |

## Relevant history and configuration evidence

- `52bfba4` (2026-09-14, `feat: Update SDK versions and Keycloak URLs to use HTTPS`) is the most recent change touching the shared auth harness and fixture infrastructure. It also introduced the bUnit/AngleSharp test dependency regression identified separately in `.copilot-tracking/research/subagents/2026-09-15/failing-tests-investigation.md:55-79`; that package correction is independent of the Aspire migration.
- `b5ce789` (`Refactored flakey tests`) and `a6084f8` (`Harden auth tests and add mitigation plan artifacts`) explain why the custom lifecycle, listener discovery, and fixed-port controls were added. They were reasonable stabilization attempts before direct managed resource endpoints became consistently used, but now duplicate Aspire's supported test-host behavior.
- The completed AppHost refactor plan states the intended real Aspire-managed topology but documents process/output/listener discovery and fixed Keycloak locking as the delivered state: `docs/005-refactor-app-host/plans/001-delivery-plan.md:142-231` and `docs/wiki/testing-and-quality.md:103-115,247-287`.
- The API project has the only explicit xUnit runner configuration and disables collection parallelism: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:1-4`. There is no repository-wide `*.runsettings`, `Directory.Packages.props`, or checked-in `packages.lock.json` in the current workspace.
- The AppHost's test-isolation switches are supported by real composition: nonpersistent Keycloak and SQL have session lifetime at `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:12-30,42-49`. Those switches should be passed as builder-scoped configuration, not global environment variables.

## Unresolved questions

1. Can the current Keycloak realm's wildcard redirect URIs and web origins complete OIDC sign-in on fully randomized HTTPS web endpoints after the recent HTTPS migration? This must be proven in Stage 3 before retiring the fixed-port lease.
2. Does the Keycloak Aspire integration publish a directly usable dynamic endpoint for the authority/issuer in this AppHost version, or is a small AppHost test-only configuration seam required? The implementation must preserve external closed-box behavior.
3. For Infrastructure integration, is a dedicated SQL test container already an approved dependency/tooling choice, or should that fixture use a minimal test-specific Aspire host? The answer determines implementation cost, not the required ownership/readiness outcome.
4. Is persistent local SQL/Keycloak state intentionally shared with manual development while tests run? The migration assumes tests must keep using session-scoped state so their cleanup cannot affect a developer's persistent AppHost resources.
5. Should the repository add central package management and/or lock files as a separate dependency reproducibility work item? The current AngleSharp/bUnit failure makes the missing package-governance policy visible, but it is not necessary to complete the Aspire fixture migration.

## References

- .github/instructions/aspire-tests.instructions.md
- .github/instructions/aspire.instructions.md
- .github/instructions/tests.instructions.md
- .github/AGENTS.md
- .github/lessons.md
- .copilot-tracking/research/subagents/2026-09-15/failing-tests-investigation.md
- docs/wiki/testing-and-quality.md
- docs/wiki/local-development.md
- docs/005-refactor-app-host/plans/001-delivery-plan.md
- https://aspire.dev/testing/overview/
- https://aspire.dev/testing/manage-app-host/
- https://aspire.dev/testing/accessing-resources/
