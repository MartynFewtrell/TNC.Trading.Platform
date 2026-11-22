<!-- markdownlint-disable-file -->
# Task Research: Aspire DCP Tunnel Proxy Containers

Docker Desktop shows several running containers named
`aspire-container-network-tunnelproxy-*`, using the image
`dcptun_developer_ms:0.25.13`, in addition to the expected SQL Server, Mailpit,
and Keycloak containers. This research determines their purpose, whether the
application requires them, and how to avoid unnecessary instances.

## Task Implementation Requests

* Identify the purpose and owner of the additional Docker containers.
* Determine whether the API and Web application require the containers.
* Recommend how to remove them when they are not required.
* Explain why the containers appeared after recent test-support work and assess
  why completed test runs left them running.

## Scope and Success Criteria

* Scope: Aspire AppHost local execution and the AppHost-backed API integration,
  Web functional, and Web E2E test fixtures.
* Exclusions: changing product source, deleting active Docker resources, and
  replacing the documented local development topology.
* Assumptions: The screenshot was taken while one or more local AppHost or
  AppHost-backed test sessions were active or recently active.
* Success criteria:
  * Identify the source and lifecycle of `aspire-container-network-tunnelproxy-*`.
  * Separate direct product dependencies from Aspire/DCP implementation resources.
  * Select an evidence-based approach to controlling accumulation.

## Outline

1. Inspect the declared AppHost resource graph and test startup path.
2. Attribute the containers to the resolved Aspire/DCP runtime.
3. Compare local AppHost and test lifecycles.
4. Evaluate supported ways to avoid or clean up instances.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14`
  * The normal local entry point builds and runs the distributed application.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:21-50`
  * The only declared container resources are SQL Server, Mailpit, and Keycloak.
  * There is no declaration for a DCP tunnel proxy or `dcptun` image.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:12-44`
  * API and Web are host-run projects with references to the container resources.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:106-125`
  * Mailpit SMTP endpoint host and port are injected into API configuration.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:170-196`
  * API and Web Keycloak authority values are composed from Keycloak's endpoint.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:30-53`
  * The fixture starts the actual AppHost using
    `DistributedApplicationTestingBuilder.CreateAsync`, waits for Keycloak, API,
    and Web, then obtains their runtime endpoints.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:66-79`
  * Fixture disposal stops and disposes the distributed application and testing
    builder.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:55-124`
  * An alternative testing-builder method exists, including
    `DcpPublisher:RandomizePorts=false`, but it has no discovered call site.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:9-29`
  * The API integration tests reference `Aspire.Hosting.Testing` and the AppHost.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:9-31`
  * The Web functional tests reference `Aspire.Hosting.Testing` and the AppHost.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:9-29`
  * The Web E2E tests reference `Aspire.Hosting.Testing` and the AppHost.
* `global.json:1-6`
  * The repository uses .NET SDK `10.0.302`.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-24`
  * The AppHost resolves Aspire AppHost SDK and hosting packages at `13.5.3`.
* `docs/wiki/local-development.md:6-27`
  * The supported local topology is AppHost plus Docker-managed SQL Server,
    Mailpit, and Keycloak.
* `docs/wiki/local-development.md:95-106`
  * Supported local commands run the AppHost using `dotnet run` or `aspire run`.

### Code Search Results

* `dcptun|tunnelproxy|aspire-container-network-tunnelproxy`
  * No production or test source declaration was found. The visible image and
    container naming originate in the resolved Aspire/DCP implementation.
* `ManagedAppHostFixture`
  * Active use occurs in the API integration, Web functional, and Web E2E test
    suites through their real-authentication fixtures.

### External Research

* Aspire testing overview: [Test your applications with Aspire](https://aspire.dev/testing/overview/)
  * `Aspire.Hosting.Testing` starts AppHost in the background, manages its
    lifecycle, and disposes resources when testing completes.
  * The testing builder randomizes proxied resource ports by default to enable
    concurrent application instances. `DcpPublisher:RandomizePorts=false`
    opts out of randomization; it is not documented as disabling DCP networking
    helpers.

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md` and
  `.github/instructions/folders.instructions.md`.
* The documented local authentication standard requires Keycloak in a
  Docker-backed Aspire environment.

## Key Discoveries

### Project Structure

The AppHost owns a resource graph containing three product-supporting Docker
resources: SQL Server, Mailpit, and Keycloak. API and Web run as projects on the
host and consume endpoints exposed by those Docker resources. No project declares
the proxy containers seen in Docker Desktop.

`Aspire.Hosting.Testing` does not introduce a separate architecture. It starts
the same AppHost graph under a test-owned lifecycle. The installed Aspire `13.5.3`
orchestration packages include DCP and its tunnel companion, `dcptun_c`, which
attributes `dcptun_developer_ms:0.25.13` to Aspire/DCP rather than repository code.

### Implementation Patterns

The observed tunnel proxies are DCP networking infrastructure. They bridge a
single Aspire application/session network when host-run projects need to reach
Docker-run resources. They are not standalone product services and are not
dependencies of the API or UI binaries when those applications are run outside
Aspire.

They can appear during either supported local AppHost execution or AppHost-backed
tests. Tests produce several instances when separate test projects or fixture
collections create their own AppHost sessions. The screenshot showing five proxies
is consistent with multiple live or incompletely cleaned-up sessions, but source
inspection alone cannot establish which case applies to each individual container.

### Complete Example

The active test behavior is equivalent to the following fixture pattern:

```csharp
_testingBuilder = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();
_application = await _testingBuilder.BuildAsync();
await _application.StartAsync();
```

Its cleanup is equivalent to:

```csharp
await _application.StopAsync();
await _application.DisposeAsync();
await _testingBuilder.DisposeAsync();
```

### API and Schema Documentation

No application API endpoint or configuration schema exposes these proxy
containers. They are managed internally by the Aspire/DCP runtime selected by
the AppHost and testing packages.

## Technical Scenarios

### Supported Local Application and Distributed Tests

The supported local topology combines Docker resources with host-run API and Web
projects. DCP creates its tunnel proxy as implementation infrastructure for this
topology. Test fixtures create the same topology with a temporary, test-owned
lifecycle.

**Requirements:**

* Retain SQL Server, Mailpit, and Keycloak for the documented local application.
* Preserve AppHost-backed tests that exercise real resource wiring,
  authentication, and browser behavior.
* Avoid retaining orphaned infrastructure after interrupted or failed runs.

**Preferred Approach:**

Retain tunnel proxies for active supported AppHost sessions. Address unexpected
accumulation operationally: diagnose session ownership, rely on fixture disposal,
and remove only stopped orphaned resources. Upgrade Aspire as a coordinated change
if a future runtime offers different supported DCP behavior.

```text
Host process: API/Web
        |
        | DCP session networking
        v
aspire-container-network-tunnelproxy-*
        |
        v
Docker resources: SQL Server, Mailpit, Keycloak
```

**Implementation Details:**

1. Do not change `WithEndpointProxySupport(proxyEnabled: false)` to try to remove
   these containers. It controls Keycloak endpoint proxy support, not the DCP
   container-network tunnel.
2. Do not delete a running tunnel proxy. It can break connectivity for its owning
   AppHost or test session.
3. Let the existing `ManagedAppHostFixture.DisposeAsync` path finish after tests.
   Do not cancel test runs if cleanup can be allowed to complete.
4. When Docker Desktop shows unexpected instances, inspect their state, creation
   timestamp, labels, and network. Stopped containers without an owning AppHost or
   DCP process may be deleted safely as stale runtime output.
5. Run distributed test projects serially while diagnosing. This makes each proxy
   session attributable and prevents simultaneous fixtures from multiplying the
   count.
6. For day-to-day tests that do not require the application graph, run unit and
   architecture suites separately. They do not start AppHost/DCP.

```powershell
docker ps -a --filter "name=aspire-container-network-tunnelproxy" --format `
  "table {{.ID}}\t{{.Names}}\t{{.Status}}\t{{.CreatedAt}}\t{{.Networks}}"

docker inspect <container-id> --format `
  "{{json .Config.Labels}}"
```

The first command distinguishes running from exited instances; the second exposes
the DCP/Aspire labels needed to associate an instance with its owner. After the
related AppHost/test process is confirmed stopped, stale *exited* instances can be
removed:

```powershell
docker rm <container-id>
```

#### Considered Alternatives

| Alternative | Result | Reason not selected |
|-------------|--------|---------------------|
| Remove proxy containers from AppHost code | No code exists to remove | The image is DCP runtime behavior, not an AppHost resource declaration |
| Disable Keycloak endpoint proxy support | Does not remove DCP tunnel proxies | It alters a different Keycloak endpoint behavior and is already disabled |
| Set `DcpPublisher:RandomizePorts=false` | Uses non-randomized resource ports | The setting does not disable tunnel proxies and raises port-conflict risk |
| Stop all AppHost-backed tests | Avoids test-created proxies | Removes closed-box coverage for real authentication, resource wiring, and browser flows |
| Run API/Web outside AppHost | May avoid DCP for a manually assembled environment | Bypasses the documented Aspire and Keycloak local-development topology |
| Delete all visible proxies | Removes active networking infrastructure too | Can break active sessions; use only targeted cleanup of stopped, orphaned instances |
| Upgrade or downgrade Aspire | May change runtime implementation | A broad compatibility change; use a coordinated upgrade with focused validation, not a workaround |

## Potential Next Research

* Capture Docker labels, state, networks, timestamps, and command for the five
  currently visible proxy containers.
  * Reasoning: This distinguishes concurrent live sessions from failed cleanup.
  * Reference: Docker Desktop screenshot and the fixture lifecycle above.
* Start one normal AppHost session and one focused distributed test session, then
  compare the created containers.
  * Reasoning: This directly measures the local Aspire `13.5.3` lifecycle.
  * Reference: `ManagedAppHostFixture` and the local development guide.

## Selected Conclusion

### Timing and Cleanup Follow-Up

Read-only Docker inspection verified that the five proxies in the screenshot are
orphaned DCP resources. They remain `running`, but each has a different
Docker-labelled creator process ID (`22092`, `2940`, `26408`, `10044`, or `15044`),
and none of those creator processes existed at inspection time. Each container was
created two to three seconds after its creator began, executes
`/usr/local/bin/dcptun_c client`, has no mounts, and has no Docker restart policy.
They are not currently active product infrastructure.

The timing is explained by two commits made on 2026-09-14:

* `52bfba4c336f9d5dd70cdf41ec3de078ec9cd01a` upgraded Aspire from `13.2.3` to
  `13.5.3`, selected the matching DCP bundle, and enabled `AspireUseCliBundle`.
* `3ef847e` introduced `ManagedAppHostFixture` and changed the affected real-
  authentication tests from manually spawned AppHost processes to
  `DistributedApplicationTestingBuilder`.

The second change directly started a DCP orchestration session for each relevant
test fixture. It is the high-confidence explanation for why these containers became
visible only after the test-support work.

The existing normal cleanup path is awaited, but it is not resilient to failure:
`test/Shared/Authentication/ManagedAppHostFixture.cs:66-79` performs cleanup in
sequence. If `application.StopAsync()` throws or is cancelled, neither
`application.DisposeAsync()` nor `testingBuilder.DisposeAsync()` is called. Since
the testing builder owns DCP orchestration, this is a credible orphaning route. A
forced testhost termination is another route because xUnit cannot guarantee fixture
disposal in that case. The available evidence cannot prove which occurred for these
five sessions, or exclude an Aspire/DCP `13.5.3` cleanup defect after otherwise
successful disposal.

The recommended implementation follow-up is to harden
`ManagedAppHostFixture.DisposeAsync` with `finally`-protected disposal: attempt
application and testing-builder disposal even when stopping fails, then report any
captured cleanup failure. Add a focused test that makes stopping fail and verifies
builder disposal still executes. This follows the more defensive former cleanup
pattern in `test/Shared/Authentication/AppHostProcessHandle.cs:179-238`.

For the current Docker state, after confirming no AppHost or AppHost-backed test
session is active, the five containers can be force-removed because their creators
have exited:

```powershell
docker ps -a --filter "name=aspire-container-network-tunnelproxy" --format `
  "table {{.ID}}\t{{.Names}}\t{{.Status}}\t{{.CreatedAt}}\t{{.Networks}}"

docker rm -f <container-id>
```

Run one focused AppHost-backed test to normal completion and inspect Docker before
and after it. Repeat after deliberately cancelling a test run. Those two checks
will distinguish an ordinary Aspire `13.5.3` cleanup leak from interrupted tests.

The additional containers are expected Aspire/DCP tunnel proxies. They are not
additional product services and cannot be removed from repository resource
registration because the repository never creates them directly. They are needed
for active AppHost sessions that bridge host-run API/Web projects to Docker-run
SQL Server, Mailpit, and Keycloak. Therefore, they should not be removed as an
architecture change.

The suitable reduction is to avoid creating unnecessary AppHost sessions, let
fixture cleanup complete, and delete only exited orphaned proxies once their owning
session has ended. Multiple *running* proxies warrant an operational inspection of
their labels and owners, not code deletion. A future Aspire upgrade is the only
supported route to a different underlying DCP implementation.