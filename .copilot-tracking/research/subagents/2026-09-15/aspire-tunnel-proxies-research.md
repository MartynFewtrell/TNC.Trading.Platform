<!-- markdownlint-disable-file -->

# Aspire Tunnel Proxy Research

## Scope

Investigate why Docker runs `aspire-container-network-tunnelproxy-*` containers using `dcptun_developer_ms:0.25.13` alongside Mailpit, Keycloak, and SQL Server in `TNC.Trading.Platform`. Establish the exact repository behavior that starts or requires the containers, separate normal local application execution from test execution, assess supported avoidance options, and record evidence with workspace-relative file paths and 1-based line ranges.

## Status

Complete. Evidence reviewed from AppHost and test source, project restore assets, repository runtime/test guidance, installed Aspire orchestration artifacts, and current Aspire testing documentation.

## Evidence Log

### AppHost topology and normal execution

* `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14` creates the distributed-application builder, registers infrastructure plus API and Web projects, and uses `builder.Build().Run()` for ordinary AppHost execution. It contains no test-host call and no tunnel/proxy resource registration.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:21-50` is the complete container registration surface: SQL Server (`sql`), Mailpit (`mailpit`, `axllent/mailpit:v1.27`), and Keycloak (`keycloak`). No code references `dcptun`, `tunnelproxy`, or the image `dcptun_developer_ms:0.25.13`.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:12-44` registers project resources `api` and `web` with external HTTP endpoints. It references container resources and wait dependencies but does not create Docker containers directly.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-24` uses `Aspire.AppHost.Sdk/13.5.3`, Keycloak hosting `13.5.3-preview.1.26425.3`, and SQL Server hosting `13.5.3`. The project has no `Aspire.Hosting.Testing` reference.

### Test-host activation

* `test/Shared/Authentication/ManagedAppHostFixture.cs:30-53` calls `DistributedApplicationTestingBuilder.CreateAsync<Projects.TNC_Trading_Platform_AppHost>`, then `BuildAsync`, `StartAsync`, health waits for `keycloak`, `api`, and `web`, and resolves their endpoints. This is the direct active test path that can cause Aspire/DCP to orchestrate the AppHost resources.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:66-79` stops and disposes the `DistributedApplication` and testing builder. The lifetime is fixture-scoped.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:9-29`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:9-31`, and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:9-29` each directly reference `Aspire.Hosting.Testing/13.5.3`, the AppHost project, and the shared fixture source.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:17-32`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:16-30`, `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:7-25`, and `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:12-24` all initialize `ManagedAppHostFixture`.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:61-124` contains a second testing-builder path in `StartManagedAppHostProcessAsync`, including `DcpPublisher:RandomizePorts=false`. Its current call-site status needs verification before treating it as active.

### Configuration and version markers

* `global.json:1-6` pins the SDK to `10.0.302` with `latestPatch` roll-forward.
* `aspire.config.json:1-5` points the Aspire CLI to the AppHost project.
* No workspace search hit exists for the literal proxy names or `dcptun` outside this research record. The image therefore originates in the Aspire/DCP implementation, rather than this repository's declared resource graph.

### Runtime, endpoint, and fixture evidence

* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:106-125` obtains Mailpit's named SMTP endpoint properties and injects the host and port into the API project. `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:170-196` composes the API and Web Keycloak authority from Keycloak's named HTTP endpoint. These host-process-to-container endpoint references require the orchestrator to provide reachable endpoint addresses.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:46-53` obtains the randomized `web`, `api`, and `keycloak` endpoints from the running `DistributedApplication`. `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:30-48` uses the fixture-provided `web` endpoint in a Playwright browser journey. `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:26-35` uses fixture-provided API and Keycloak endpoints to call the application through real authentication.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/AuthenticationIntegrationTestCollection.cs:3-6`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs:5-8`, and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs:5-8` scope their respective AppHost fixture to a non-parallel xUnit collection. This bounds, but does not eliminate, independently orchestrated AppHost runs across separate test projects or concurrently launched `dotnet test` commands.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:11-50` can start `dotnet run` directly, while lines `55-124` contain an unused alternative testing-builder implementation. Workspace call-site search found only these two definitions and no invocation. It is not the source of the active proxy containers.
* `src/TNC.Trading.Platform.AppHost/obj/project.assets.json:1-70` and `:1996-2050` show the AppHost restores `Aspire.Hosting/13.5.3` and `Aspire.Hosting.AppHost/13.5.3`. `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/obj/project.assets.json:241-310` shows `Aspire.Hosting.Testing/13.5.3` depends on `Aspire.Hosting/13.5.3` and `Aspire.Hosting.AppHost/13.5.3`. Thus test execution does not introduce an unrelated container engine; it invokes the same Aspire hosting pipeline with a test-controlled lifecycle.
* Installed package inspection located `dcp.exe` and `dcptun_c` under `%USERPROFILE%/.nuget/packages/aspire.hosting.orchestration.win-x64/13.5.3/tools/`, and `dcp.exe` plus `dcptun_c` under `%USERPROFILE%/.nuget/packages/aspire.cli.win-x64/13.5.3/.../dcp/`. This is direct local evidence that the resolved Aspire 13.5.3 orchestration/CLI runtime bundles the DCP tunnel companion. It supports attribution of the Docker image to DCP rather than any resource declared in this repository.

### Official Aspire behavior

* The current [Aspire testing overview](https://aspire.dev/testing/overview/) states that `Aspire.Hosting.Testing` supplies `DistributedApplicationTestingBuilder`; it launches the AppHost in the background, manages its lifecycle, and disposes resources after a test completes.
* The same document states that the testing builder disables the dashboard and randomizes ports of proxied resources by default to permit concurrent application instances. Its documented opt-out for port randomization is `DcpPublisher:RandomizePorts=false`; it does not document this setting as disabling proxies or DCP's container-network tunnel.
* The documented model is closed-box: tests start the AppHost, which orchestrates applications and resources, then access runtime endpoints. That matches this repository's `ManagedAppHostFixture` behavior.

### Normal local use versus test execution

* Normal local API/UI use is not standalone API or Web execution. `docs/wiki/local-development.md:6-27` declares AppHost plus Docker-managed SQL Server, Mailpit, and Keycloak as the supported local runtime. `docs/wiki/local-development.md:95-106` directs users to start it with `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` or `aspire run`.
* The ordinary AppHost entry path and the testing-builder path both use the AppHost's Aspire hosting model and DCP. The image is therefore not required by the API or Blazor UI binaries in isolation, but it can be required by Aspire/DCP when the application graph needs a host-to-container network bridge. This AppHost has exactly that topology: API and Web are host projects and SQL Server, Mailpit, and Keycloak are Docker containers with referenced endpoints.
* The extra containers are not direct resources and should not be interpreted as application services such as SQL Server, Mailpit, or Keycloak. `aspire-container-network-tunnelproxy-*` identifies a DCP implementation resource for a particular Aspire application/session network. The `dcptun_developer_ms:0.25.13` image tag is DCP runtime behavior and is not versioned or configured in this repository.
* Tests commonly produce multiple proxies because they can create separate AppHost instances: API real-token and synthetic-token collections each instantiate a fixture, Web functional and Web E2E each instantiate a fixture, and test projects can be run separately. Normal `dotnet run` can also have its own proxy for the same host-plus-containers graph. Session-lifetime test containers should be removed when `ManagedAppHostFixture.DisposeAsync` succeeds; persistent local containers are intentionally retained by the AppHost configuration.

## Findings

1. The proxy containers do not originate in this repository's AppHost declarations. The sole declared Docker resources are SQL Server, Mailpit, and Keycloak. There is no direct code or configuration reference to `dcptun`, `tunnelproxy`, `aspire-container-network-tunnelproxy`, or `dcptun_developer_ms`.
2. The direct active trigger in the test path is `ManagedAppHostFixture.InitializeAsync`, which creates, builds, and starts the AppHost through `DistributedApplicationTestingBuilder`. Four test projects declare the testing package, but active distributed fixtures are present in API integration, Web functional, and Web E2E suites. The AppHost unit project restores the package for model-level tests but its shown composition tests only call `DistributedApplication.CreateBuilder` and do not start an application.
3. Aspire testing is an explanation for proxies seen during AppHost-backed tests, but it is not sufficient to classify the proxy as test-only. The ordinary local run starts the same container-plus-host-process graph through DCP. The necessary distinction is: proxies are an Aspire/DCP orchestration implementation detail, required only while a corresponding orchestration session needs the bridge, not an independently required product service.
4. `WithEndpointProxySupport(proxyEnabled: false)` at `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:45` applies to Keycloak endpoint proxy support. It does not disable the DCP container-network tunnel proxy, as demonstrated by the remaining host-to-container references and by the distinct DCP-owned proxy image/name. Treating it as a switch for these containers would be incorrect.
5. The exact reason multiple proxies remain visible cannot be proven from static source alone. Expected cleanup is present in the fixture, so multiple stopped containers can be normal remnants from completed test sessions; multiple running containers indicate multiple active AppHost/DCP sessions or incomplete shutdown. Docker labels, state, creation timestamps, and owning-process information are required to distinguish those cases.

## Recommendations

### Recommended approach

Keep the DCP tunnel proxy containers for supported local AppHost runs and AppHost-backed distributed tests. Do not remove or attempt to suppress them through `WithEndpointProxySupport(false)`, image deletion, or arbitrary Docker cleanup while a corresponding AppHost is running. They bridge the supported host-process plus Docker-container topology and are managed by the resolved Aspire runtime.

For test-related accumulation, use the existing fixture lifecycle first: run distributed projects serially when investigating, allow the test process to exit normally, and ensure failed/cancelled test runs are stopped cleanly. The fixture's `StopAsync` and `DisposeAsync` calls are the designed cleanup mechanism. Investigate stale stopped containers using Docker's creation time, state, labels, and associated DCP/AppHost process before deleting only orphaned stopped instances.

Treat package upgrades as the appropriate route for changed DCP proxy behavior. This repository resolves the image behavior through Aspire `13.5.3`; no source setting pins the image. Any Aspire upgrade should be validated with an AppHost normal run and the three AppHost-backed test projects, because the proxy is runtime implementation behavior.

### Alternatives and constraints

| Alternative | Effect | Constraints and risks |
| --- | --- | --- |
| Run unit and architecture suites only | Avoids starting AppHost/DCP, thus avoids proxy creation for those commands | Does not validate Docker, SQL Server, Mailpit, Keycloak, API, Web, real authentication, or browser integration. It cannot replace retained distributed coverage. |
| Do not run AppHost-backed API integration, Web functional, and Web E2E projects | Avoids test-created proxy sessions | Leaves the application graph untested and conflicts with the documented closed-box test strategy. Normal AppHost use can still create proxies. |
| Start API/Web outside AppHost and supply external SQL/Keycloak/Mailpit endpoints | May avoid DCP and its proxy for that manually assembled topology | Not the supported local runtime in `docs/wiki/local-development.md:6-27`; bypasses the required Aspire-managed Keycloak local-auth approach and loses AppHost service discovery/orchestration validation. |
| Change `DcpPublisher:RandomizePorts=false` | Uses non-randomized test resource ports | It is documented only to disable port randomization, not tunnel proxies. Fixed ports introduce conflicts and contradict the repository's stated randomized-endpoint fixture model. The only source usage is dormant `RealAppHostProcessFactory` code at `test/Shared/Authentication/RealAppHostProcessFactory.cs:90-94`. |
| Set Keycloak `.WithEndpointProxySupport(proxyEnabled: false)` | Already used for Keycloak endpoint proxy behavior | Does not control the DCP container-network tunnel proxy. Changing it can alter Keycloak endpoint access behavior while failing to remove the reported containers. |
| Delete `aspire-container-network-tunnelproxy-*` containers | Reclaims stale stopped containers after their owners are gone | Deleting a running proxy can break endpoint connectivity for an active AppHost/test session. Do not use it as an architectural solution. |
| Upgrade/downgrade Aspire packages or CLI | May change DCP implementation/image behavior | Requires coordinated version compatibility across AppHost SDK, hosting packages, testing package, installed CLI bundle, and .NET 10 SDK. It has broad runtime/test impact; upgrade rather than downgrade is preferable, with release-note review and focused validation. |

## Open Questions

* Inspect current Docker state to associate every `aspire-container-network-tunnelproxy-*` container with an AppHost/DCP session using labels, status, creation time, network, and command. Static repository evidence cannot determine whether the observed instances are active, exited normally, or orphaned.
* Capture one normal `dotnet run` AppHost session and one focused `dotnet test` AppHost-backed session with DCP debug logs, then compare container labels/timestamps. This would empirically prove the per-session creation count for the locally installed `13.5.3` runtime.
* Review Aspire 13.5.3 DCP release notes or source, if available for this exact runtime, to confirm the image tag's `0.25.13` mapping and any supported setting that changes container-network tunneling. No documented supported proxy-disable setting was found in the consulted Aspire testing documentation.
