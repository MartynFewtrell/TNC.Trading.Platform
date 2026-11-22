<!-- markdownlint-disable-file -->
# Task Research: AppHost Tunnel-Proxy Container and New Failures

This investigation explains the `aspire-container-network-tunnelproxy` container visible while the TNC Trading Platform AppHost is running, determines whether it is application or test infrastructure, and correlates recent application failures with repository changes.

## Task Implementation Requests

* Explain why the temporary `aspire-container-network-tunnelproxy` Docker container appears and disappears with the application.
* Determine whether the container is required by the AppHost, test-only infrastructure, or an Aspire/DCP implementation detail.
* Identify recent changes that may explain newly observed application failures.
* Provide evidence-based follow-up checks that separate the container's normal lifecycle from actual application faults.

## Scope and Success Criteria

* Scope: AppHost resource registration, Docker-backed Aspire lifecycle, recent authentication/test-host changes, HTTPS configuration, and available run artifacts.
* Assumptions: The screenshot was captured while the AppHost was running locally through Aspire with Docker-backed SQL Server, Keycloak, and Mailpit.
* Exclusions: No product source changes are made by this research; individual failure diagnosis requires the concrete exception and failing test or endpoint.
* Success Criteria:
  * Establish whether the tunnel-proxy is registered by this repository.
  * Explain its startup and shutdown lifecycle using repository evidence.
  * Identify a defensible timeline and likely sources for regressions.
  * Provide a focused next-check sequence.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14`
  * Builds and runs the distributed application; it does not register a tunnel-proxy resource.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:22-52`
  * Registers SQL Server, Mailpit, and Keycloak. No tunnel-proxy is present.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:12-48`
  * Registers only the API and Web projects.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs:44-50`
  * The composition test expects exactly `sql`, `platformdb`, `mailpit`, `keycloak`, `api`, and `web`.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:21-45,71-117`
  * Uses `DistributedApplicationTestingBuilder`, starts the managed application, and explicitly stops and disposes it and its builder.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:11-24,55-71`
  * Supports both child-process and managed Aspire test-host paths. Both paths force infrastructure containers and disable persistent SQL/Keycloak state.
* `src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:5-16`
  * Exposes HTTPS launch settings, relevant when comparing endpoint scheme and authority with dynamically managed test endpoints.
* `global.json:2-5`
  * Pins SDK `10.0.302`; captured AppHost output reports Aspire `13.2.3`.
* `artifacts/apphost-stdout.log:1-10`
  * Shows Aspire startup, dashboard startup, and a clean distributed-application-started message.
* `artifacts/apphost-stderr.log`
  * Empty in the captured run.

### Code Search Results

* `tunnelproxy`
  * No repository AppHost registration was found. The visible Docker name is therefore not a product resource declared in this repository.
* `AddKeycloak`, `AddMailPit`, SQL registration
  * These are the application-owned infrastructure resources visible in AppHost registration; none creates a tunnel-proxy.
* `DistributedApplicationTestingBuilder`
  * The managed fixture starts Aspire in-process for tests and disposes the application and builder during cleanup.

### External Research

* Aspire documentation landing page: https://aspire.dev/
  * The repository uses Aspire 13.2.3 in the captured run. The observed naming and lifecycle are consistent with a DCP-managed Docker networking helper rather than a resource declared in AppHost code. The direct repository evidence is stronger for ownership: the helper is absent from all application resource registration and disappears when Aspire shuts down.

## Key Discoveries

### The tunnel-proxy is not an AppHost resource

The AppHost declares six expected resources: SQL, the SQL database, Mailpit, Keycloak, API, and Web. The composition unit test asserts that set. The screenshot's `aspire-container-network-tunnelproxy-*` name does not correspond to any resource key or explicit container registration in the AppHost.

The most likely explanation is an Aspire/DCP-created Docker networking helper. It exists only while the Aspire-managed distributed application is active, so its disappearance at shutdown is expected cleanup. It is not evidence that the repository recently added a seventh product service, and it is not inherently test-only.

### Why it may now be more visible

The recent managed-test-host migration makes DCP-owned Docker resources more visible during tests. `DistributedApplicationTestingBuilder` starts Aspire in-process, and the cleanup path explicitly stops and disposes the application and builder. The test factory also forces infrastructure containers on and disables persistent SQL/Keycloak state. Fresh, disposable infrastructure makes helper-container creation and removal obvious.

This does not mean the AppHost itself requires a tunnel-proxy as a business or application dependency. It means the current Aspire/Docker execution mode may use one to provide its container-networking behavior.

### Recent change timeline

* Commit `52bfba4`, 2026-09-14: SDK and Keycloak URLs changed to HTTPS. This can cause certificate, redirect, authority, or scheme mismatches.
* Commit `3ef847e`, 2026-09-15: authentication tests migrated to `ManagedAppHostFixture`, changing how endpoints are allocated and how Docker infrastructure is started and stopped.
* Commit `86514ba`, 2026-09-15: added `AppHostCleanup`; managed applications now stop and dispose the application and then the builder.

### Most plausible sources of the new failures

1. Test-host behavior changed. The managed `Aspire.Hosting.Testing` path and the `dotnet run` child-process path do not have identical endpoint allocation, readiness, or cleanup behavior.
2. HTTPS configuration changed. Tests or callers may still assume HTTP, a fixed port, an HTTP Keycloak authority, or a certificate trust model that no longer matches the current launch settings.
3. Readiness and cleanup behavior changed. Fresh infrastructure containers and explicit disposal can expose Keycloak/SQL readiness races or cleanup-related exceptions.
4. SDK/package alignment should be checked. The repo pins SDK `10.0.302`, while the captured run reports Aspire `13.2.3`; this is not proof of failure, but restore/build output should be correlated with the failing run.

The tunnel-proxy itself is not currently supported as the cause of product failures. Its presence is expected unless inspection shows an error state, failed health status, or network membership inconsistent with the DCP-managed resources.

## Technical Scenarios

### Normal AppHost Run

**Requirements:** Run SQL, Mailpit, Keycloak, API, and Web under Aspire; allow Aspire/DCP to manage Docker networking; remove framework-owned helpers when the distributed application stops.

**Preferred Approach:** Treat the tunnel-proxy as an internal Aspire lifecycle resource. Do not add or remove it from AppHost code. Verify the AppHost-owned resource set through `AppHostCompositionTests` and inspect Docker labels only when diagnosing a concrete runtime failure.

**Implementation impact:** No product-code change is indicated by the screenshot alone.

### Failure Isolation

**Requirements:** Determine whether failures come from application behavior, endpoint scheme/authority, infrastructure readiness, or test cleanup.

**Preferred Approach:** Run checks in this order:

1. Run the AppHost composition unit tests.
2. Run the smallest failing functional test using the managed fixture.
3. Run the equivalent path using the child-process `dotnet run` fixture, if available.
4. Compare the endpoint returned by `application.GetEndpoint(...)` with the scheme and authority used by the test; do not infer managed-test ports from `launchSettings.json`.
5. While the AppHost is running, inspect `docker inspect aspire-container-network-tunnelproxy-*` and record labels, health status, and network membership.
6. Correlate the first exception with AppHost stdout/stderr and Keycloak/SQL readiness logs.

A healthy helper with normal DCP labels and clean shutdown should be treated as incidental. A failed endpoint, certificate, readiness, or authentication assertion should be fixed at its owning boundary.

#### Considered Alternatives

* Remove the tunnel-proxy container manually or suppress it in AppHost.
  * Rejected: it is not registered by the repository and is likely required by the current Aspire/DCP Docker networking mode. Suppression would be an unsupported framework workaround and could break service reachability.
* Assume the tunnel-proxy is test-only and unrelated to every run.
  * Rejected: its lifecycle is tied to Aspire/DCP-managed execution, while the AppHost itself also uses Docker-backed resources. The test migration only makes the behavior more visible; it does not establish exclusive test ownership.
* Roll back the recent changes immediately.
  * Rejected: the available evidence identifies several plausible regression boundaries, especially HTTPS and managed-test endpoints. A first-failure classification is cheaper and avoids reverting unrelated improvements.

## Recommended Conclusion

The screenshot shows expected Aspire/DCP behavior: `aspire-container-network-tunnelproxy-*` is a temporary framework-owned Docker networking helper. It is not declared in `AppHost.cs`, is absent from the repository's expected composition, and disappearing when the AppHost closes is normal cleanup.

The new application failures should be investigated separately. The highest-value suspects are the 2026-09-14 HTTPS changes and the 2026-09-15 managed Aspire test-host migration, including endpoint allocation, readiness, and disposal changes. The next diagnostic artifact needed is the first concrete failure message or test name, together with a Docker inspect result for the helper while the AppHost is running.

## Potential Next Research

* Inspect the exact failing application/test output and classify the first failure.
  * Reasoning: The current evidence cannot distinguish endpoint/certificate problems from readiness or product behavior without an exception.
* Inspect Docker labels and networks for the tunnel-proxy during a live AppHost run.
  * Reasoning: This directly verifies DCP ownership and health.
* Compare package versions and restore output around commits `52bfba4`, `3ef847e`, and `86514ba`.
  * Reasoning: Confirms whether a tooling update or test-host API change coincided with the regression.
