<!-- markdownlint-disable-file -->

# Keycloak and .NET Aspire authoritative guidance research

## Research scope

Research authoritative guidance current to 2026-07-27 for:

* Delayed Keycloak health under .NET Aspire
* Persistent Docker container lifetime and persistent Keycloak state
* Keycloak health and readiness endpoints
* Realm import timing
* Reliable .NET Aspire closed-box authentication tests
* Precise semantics and limitations of `WithLifetime(ContainerLifetime.Persistent)`
* Volume mounts and Keycloak data directories
* `AddKeycloak` health checks
* `WaitFor` and `WaitForCompletion`
* Resource health compared with OIDC endpoint usability
* Test startup through `DistributedApplicationTestingBuilder`
* Remediation alternatives, tradeoffs, and version constraints

## Source policy

Use Aspire documentation at <https://aspire.dev/> as the primary Aspire source. Supplement it with official Microsoft Learn, official Keycloak documentation, and official .NET Aspire integration source or repository documentation where implementation details are necessary.

## Findings

### Version and repository context

The findings were checked on 2026-07-27. Current Aspire documentation examples identify Aspire 13.1.0, while this repository targets Aspire AppHost SDK 13.2.3 and `Aspire.Hosting.Keycloak` `13.2.3-preview.1.26217.6`. The installed Keycloak package identifies Microsoft repository commit `0838645cc4f7fc27e46200ac14cbe5c751637593`, depends on `Aspire.Hosting` 13.2.3, and defaults to `quay.io/keycloak/keycloak:26.5`.

Repository evidence:

* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs`
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs`
* `test/Shared/Authentication/RealAppHostProcessFactory.cs`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs`

The AppHost currently gives Keycloak persistent container lifetime by default, does not add a Keycloak data volume, imports `./Realms`, and makes API and Web resources call `WaitFor(keycloak)`. Closed-box real-authentication startup disables persistent Keycloak lifetime, calls `BuildAsync()` and `StartAsync()`, and then immediately requests the Keycloak master-realm token endpoint and Admin API without first awaiting Keycloak resource health or the target realm's OIDC discovery endpoint.

### Persistent container lifetime and persistent state are different controls

Aspire documentation states that ordinary resource containers are destroyed when the AppHost stops. Volumes and bind mounts retain selected files outside the container and therefore outside that destruction lifecycle. Volumes are runtime-managed, more portable, and preferred unless host access is required. A generic volume persists only the container path mounted as its target.

The official Keycloak integration adds `WithDataVolume()` and `WithDataBindMount(source)`. Both mount the Keycloak data directory at `/opt/keycloak/data`. The integration documentation says this data persists outside the container lifecycle and warns that admin credentials are stored in that volume. Changing credentials against existing volume state can fail until the volume is deleted.

`WithLifetime(ContainerLifetime.Persistent)` is not equivalent to `WithDataVolume()`. Aspire source maps persistent lifetime to the orchestrator's persistent container mode and has functional tests that verify reuse of a container identity. It preserves the whole container for reuse rather than selecting a durable data directory. This has several limitations:

* Persistent lifetime is a local orchestration lifecycle choice, not a portable data-backup design.
* Retaining a container can also retain stale runtime configuration, imported realm state, and old bootstrap credentials.
* A data volume survives replacement or recreation of the container, while container persistence depends on the retained container remaining compatible and available.
* Neither mechanism causes a startup realm import to overwrite an existing realm.
* Closed-box tests should normally use session lifetime and isolated state to avoid cross-run coupling.

Pinned Aspire source confirms the public implementation boundary. `WithLifetime()` replaces a `ContainerLifetimeAnnotation`; DCP maps `Persistent` to its persistent mode. Persistent resources receive a stable name derived from the AppHost path unless a container name is supplied, enabling an existing compatible container to be reused. The same source warns that changing injected container-file content recreates a persistent container, so reuse is conditional on the generated resource specification remaining compatible. This is an orchestration behavior, not a guarantee that container writable-layer data will survive arbitrary model changes, manual deletion, runtime cleanup, or migration to another host.

### Realm import timing and persistence interaction

Official Keycloak documentation defines startup import precisely:

* `--import-realm` reads regular `.json` files from `data/import`; for the container image this is `/opt/keycloak/data/import`.
* Import file naming conventions matter for directory imports, including `<realm-name>-realm.json`.
* If a realm already exists, startup import skips it to avoid recreating it and losing state.
* The server does not fully start until startup imports complete.
* Recreating or overriding a realm requires an explicit offline `import` command before server startup or an intentional online provisioning mechanism such as the Admin API.

The Aspire integration's `WithRealmImport()` copies or injects realm files at `/opt/keycloak/data/import` and retains the `AddKeycloak` startup arguments, which include realm import. Current Aspire documentation explicitly limits `WithRealmImport()` to local development because its development-time file injection is unsupported by `aspire publish` and `aspire deploy`.

Therefore, delayed first startup can legitimately be import work, but later startup against retained state follows different semantics: the existing realm is skipped rather than refreshed. Persistent lifetime or `/opt/keycloak/data` persistence can make tests depend on historical state even when the checked-in realm JSON changes.

### Keycloak health and readiness semantics

Official Keycloak documentation exposes four health endpoints when health support is enabled:

* `/health/started` reports startup completion before liveness takes over.
* `/health/live` reports whether the process should continue running.
* `/health/ready` reports whether the instance is ready to process requests and receive traffic.
* `/health` aggregates health procedures.

Success returns HTTP 200 and failure returns HTTP 503. Health endpoints use management port 9000 by default. Health support is disabled by default in the base Keycloak image and is a build-time option. Keycloak also states that enabling health endpoints enables asynchronous server bootstrap by default, so HTTP ports can open while bootstrap remains in progress. Port acceptance is therefore not a readiness signal.

The official Aspire Keycloak integration automatically configures an HTTP health check against the management endpoint's `/health/ready` path. This is stronger than waiting for an open application port and includes Keycloak initialization in the readiness result. Database readiness is represented by the database connection-pool check when metrics are enabled; without metrics, not every optional health component is present.

The repository's pinned integration source confirms the exact setup: it creates a management HTTP endpoint targeting container port 9000, excludes that endpoint from ordinary reference injection, sets `KC_HEALTH_ENABLED=true`, and registers `WithHttpHealthCheck(endpointName: "management", path: "/health/ready")`. The integration does not add Keycloak-specific startup-delay, retry-count, or grace-period settings. It relies on Aspire's ordinary periodic URL health-check machinery and the caller's cancellation or timeout policy.

### Resource health does not prove realm-specific OIDC usability

Keycloak `/health/ready` answers whether the server instance can process requests. It does not prove all application-specific authentication assumptions. A test that needs realm `tnc-trading-platform` should additionally verify the realm discovery document at `/realms/tnc-trading-platform/.well-known/openid-configuration`, then perform the protocol operation under test. This distinction catches:

* A skipped, missing, or malformed realm import
* A missing client, role, redirect URI, or seeded user
* Authority, issuer, hostname, proxy, or randomized-port mismatch
* A healthy master realm while the target realm is unusable

The master-realm token endpoint is also not a substitute for target-realm OIDC readiness. It proves the admin login flow used by the harness, not the application realm's discovery metadata or client configuration.

### Closed-box Aspire test startup

Aspire describes `Aspire.Hosting.Testing` as closed-box integration testing: the AppHost and resources run as separate processes. `DistributedApplicationTestingBuilder.CreateAsync<TAppHost>()` creates the test host builder; `BuildAsync()` builds the `DistributedApplication`; `StartAsync()` starts orchestration. The official test pattern then explicitly calls `app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, cancellationToken)` with a bounded timeout before sending HTTP requests.

The testing builder randomizes proxied ports by default so tests can run concurrently, disables the dashboard by default, and cleans up application resources when the application or builder is disposed. Fixed ports can be requested with `DcpPublisher:RandomizePorts=false`, but doing so reduces isolation and creates collision risk. A stable Keycloak authority port remains useful for interactive browser cookies, while randomized ports are generally preferable for automated closed-box tests when authority and redirect URIs are resolved dynamically.

`StartAsync()` is not documented as waiting until every resource is healthy. The official testing example treats startup and health readiness as separate awaits. Code that immediately calls Keycloak after `StartAsync()` has a race even if dependent project resources use `WaitFor(keycloak)`.

Pinned Aspire source makes the wait contracts more precise:

* `WaitFor(dependency)` records `WaitType.WaitUntilHealthy`. At run time it first waits for every resolved dependency instance to reach `Running`, then waits for `HealthStatus.Healthy` when that dependency has health-check annotations, and finally awaits the dependency's resource-ready event. Because `AddKeycloak` adds a health-check annotation, `WaitFor(keycloak)` is a health barrier rather than only a process-running barrier.
* `WaitForStart(dependency)` deliberately ignores health checks and waits only for `Running` plus its normal startup event processing.
* `ResourceNotifications.WaitForResourceHealthyAsync(name, token)` considers a resource without health-check annotations healthy at `Running`; for Keycloak it waits for the registered health result and then awaits the resource-ready event. Its no-behavior overload uses the configured `ResourceNotificationServiceOptions.DefaultWaitBehavior`, which defaults to stopping on unavailable states in the pinned implementation. The caller still controls elapsed time through the cancellation token; no independent timeout is supplied by this API.
* `WaitForCompletion(dependency, exitCode: 0)` waits for a finite dependency to reach a terminal state, fails if it cannot start, and rejects a different reported exit code. It has no deployment-time effect and is intended for local one-shot initialization work.

These contracts strengthen orchestration ordering but do not perform realm-specific OIDC discovery, token issuance, seeded-client validation, or Keycloak Admin REST calls. The test harness therefore needs both the Aspire health barrier and a protocol-level barrier.

### Warm-start clarification

`ContainerLifetime.Persistent` preserves and reuses the container rather than treating it as an AppHost-session resource. With a proxyless fixed endpoint, the pinned Aspire API explicitly supports keeping that resource reachable whether the Aspire application is running or not. Normal AppHost shutdown therefore does not imply a Keycloak process restart on the next AppHost launch. This repository combines persistent lifetime with `WithEndpointProxySupport(proxyEnabled: false)` and fixed host port 8080, but it does not mount `/opt/keycloak/data` as a volume.

The observed state determines what happens next:

1. A retained but stopped container keeps its writable layer, including Keycloak's embedded development database, but its Keycloak process is not running. Aspire or the container runtime must start the container, which launches a new Keycloak process with the configured startup command and `--import-realm`. Keycloak scans `data/import`, skips the application realm when that realm already exists, and still performs ordinary JVM, Quarkus, database, cache, bootstrap, and health initialization before `/health/ready` returns 200. Availability is therefore faster than a fresh import in many cases, but not instant.
2. A genuinely still-running and healthy persistent container does not restart Keycloak and does not rerun `--import-realm` merely because the AppHost starts again. The existing realm is already loaded in the live process. With this repository's proxyless port, Keycloak may already be usable before AppHost orchestration reconnects to it. Aspire still has to discover or reconcile the resource and observe its health, so callers must use a health or protocol-level barrier rather than assume zero elapsed time. If `/health/ready` is already 200 and the realm discovery endpoint works, realm import contributes no warm-start delay.
3. A recreated container starts a new Keycloak process. Without a Keycloak data volume, as in the current repository, the old writable-layer database is gone, so the realm does not exist and startup import must create it again. With `/opt/keycloak/data` retained in a data volume or bind mount, the new process sees the existing database; `--import-realm` still runs as a startup option but skips the existing realm. In both variants, full process bootstrap and readiness checks still take time. Changing injected realm-file content can itself cause Aspire to recreate a persistent container, according to the pinned container-file API contract.

The concise answer is therefore no: an existing realm does not need to be imported again, and startup import does not overwrite or refresh it. Keycloak still evaluates the startup import path whenever a new Keycloak process starts, but it skips an existing realm. A healthy process that truly remained running should be available immediately at the Keycloak level, subject to Aspire reconciliation and endpoint checks. A stopped or recreated container must start Keycloak again and cannot be assumed immediately ready.

Realm import is a plausible material contributor mainly for fresh state: first launch, session-lifetime tests, or recreation without a retained `/opt/keycloak/data` volume. It is not a credible explanation for every observed delay. Delays after a retained-but-stopped container or a recreated container with retained data are more likely dominated by Keycloak process bootstrap, non-optimized image build-option checks, JVM and Quarkus initialization, database and cache initialization, and health-check observation. Delays while the same healthy process remains running point away from realm import and toward Aspire reconciliation, port or endpoint reachability, health polling, or realm-specific OIDC usability.

This clarification does not change the primary research recommendation. Closed-box tests should continue to use isolated session state, await Aspire's Keycloak health notification, and then verify the target realm's OIDC discovery endpoint. Persistent lifetime remains appropriate for local developer continuity, where a documented reset path is available and stale realm state is acceptable.

## Remediation alternatives

### Recommended test remediation

Use layered, bounded readiness in the real-authentication fixture:

1. Keep Keycloak at `ContainerLifetime.Session` for closed-box authentication tests.
2. Build and start the application with a cancellation token and explicit overall timeout.
3. Await `ResourceNotifications.WaitForResourceHealthyAsync("keycloak", token)`.
4. Poll or retry the target realm's OIDC discovery endpoint with a separate bounded timeout and useful diagnostics.
5. Only then request the master admin token and mutate the dynamic redirect URI.
6. Await API and Web health before beginning browser or protocol assertions.
7. Dispose both `DistributedApplication` and its testing builder in all paths.

This aligns with Aspire's official testing pattern and separates infrastructure readiness from application-realm readiness. It also makes a timeout identify which contract failed.

### State-isolation alternatives

Use ephemeral Keycloak state for tests. This is the preferred deterministic option. Startup import creates a fresh realm each run, and disposal removes the session container. The tradeoff is slower setup.

Use a named data volume only for developer continuity, not shared closed-box test state. This is more durable than persistent container lifetime and survives container recreation, but existing-realm import is still skipped and credentials must remain stable.

Use persistent container lifetime only for local developer speed where stale state is acceptable and an explicit reset workflow exists. It can reduce restart cost, but it is the most coupled option and does not provide publishable persistence semantics.

Use an external database for Keycloak when durable, production-like state is required. This separates identity data from container filesystem lifecycle. It adds infrastructure cost and still requires an idempotent provisioning/versioning strategy for realms.

Use a dedicated initialization job or service with the Keycloak Admin API for controlled, idempotent realm configuration. In Aspire, dependent resources should use `WaitForCompletion(initializer)` only when the initializer is a finite process that exits successfully after provisioning. `WaitForCompletion` is inappropriate for Keycloak itself because Keycloak is a long-running service.

Bake realm files into a custom Keycloak image for deployment. Current Aspire guidance recommends retaining `AddKeycloak(...).WithDockerfile(...)` so integration defaults and startup arguments remain. Baking files makes them deployable but does not change Keycloak's startup rule that existing realms are skipped.

### Startup-performance alternatives

Build an optimized Keycloak image for repeated startup. Official Keycloak container guidance says the standard image detects and applies build options during start, which significantly increases startup time. A custom image that runs `kc.sh build` during image construction shifts that work out of every test or development startup. This improves latency but does not replace health waits or target-realm checks.

Retain Docker-backed infrastructure between manual AppHost runs only where the repository's local-development policy favors speed. Tests should opt out so reliability does not depend on the developer's pre-existing containers or volumes.

## Conclusions, residual gaps, and clarifying questions

The original questions are answered sufficiently for a remediation decision. The immediate defect pattern is a test-side race: `DistributedApplication.StartAsync()` starts orchestration but is not a Keycloak health or OIDC-usability barrier. The strongest low-complexity remediation is an isolated session container, a bounded `WaitForResourceHealthyAsync("keycloak", token)` call, and then a bounded target-realm OIDC check before admin-token and redirect-URI operations.

The remaining gaps do not block that decision:

* Persistent-container reuse and recreation are DCP and container-runtime implementation details beyond the public lifetime contract. Exact behavior can vary with generated-spec changes, Aspire version, Docker or Podman version, manual cleanup, and runtime state.
* No repository-specific trace was captured to prove whether each observed delay used a still-running, stopped, or recreated Keycloak container. Container identity, process start time, Keycloak logs, and `/health/ready` timestamps are needed to classify individual runs.
* No authoritative contract promises that `/health/ready` proves application-specific realm contents. That stronger assertion belongs in the authentication test harness.
* No runtime experiment was performed in this research task, so repository-specific startup-duration distributions and the most useful timeout values remain empirical questions.

No user clarification is required to answer the original research scope.

## References

* [Aspire Keycloak integration](https://aspire.dev/integrations/security/keycloak/)
* [Aspire testing overview](https://aspire.dev/testing/overview/)
* [Aspire: Write your first test](https://aspire.dev/testing/write-your-first-test/)
* [Aspire: Persist data with volumes and binds](https://aspire.dev/fundamentals/persist-data-volumes/)
* [Aspire AppHost eventing APIs](https://aspire.dev/app-host/eventing/)
* [Keycloak: Tracking instance status with health checks](https://www.keycloak.org/observability/health)
* [Keycloak: Importing and exporting realms](https://www.keycloak.org/server/importExport)
* [Keycloak: Running Keycloak in a container](https://www.keycloak.org/server/containers)
* [Microsoft Aspire source repository](https://github.com/microsoft/aspire)
* [Pinned Aspire integration commit](https://github.com/microsoft/aspire/commit/0838645cc4f7fc27e46200ac14cbe5c751637593)
* [Pinned Keycloak integration source](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting.Keycloak/KeycloakResourceBuilderExtensions.cs)
* [Pinned Aspire wait APIs](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting/ResourceBuilderExtensions.cs)
* [Pinned Aspire resource-notification implementation](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting/ApplicationModel/ResourceNotificationService.cs)
* [Pinned Aspire container lifetime implementation](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting/ContainerResourceBuilderExtensions.cs)
* [Pinned Aspire DCP executor](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting/Dcp/DcpExecutor.cs)