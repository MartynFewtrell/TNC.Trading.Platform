<!-- markdownlint-disable-file -->
# Task Research: Keycloak Aspire Readiness and Persistence

Research the delayed Keycloak health transition in the Aspire application, its Docker persistence behavior, and the reliability impact on authentication tests.

The delayed transition is consistent with normal Keycloak bootstrap and realm import. The immediate test defect is a readiness race: the managed Web authentication fixture calls the Keycloak token and Admin APIs immediately after `DistributedApplication.StartAsync()`, before it waits for the Keycloak resource to become Healthy or verifies the imported application realm. Local container persistence and automated-test reliability are separate concerns.

## Task Implementation Requests

* Determine why Keycloak starts unhealthy before becoming healthy while SQL Server and Mailpit are immediately available.
* Verify whether Keycloak is configured to remain running and retain state between Aspire startup events.
* Determine whether authentication tests race Keycloak readiness.
* Select an evidence-based resolution that aligns with repository conventions and current Aspire and Keycloak guidance.

## Scope and Success Criteria

* Scope: AppHost resource configuration, Keycloak container lifecycle and storage, health/readiness semantics, authentication test startup coordination, and authoritative external guidance.
* Assumptions: Local development uses Docker and Keycloak is orchestrated by .NET Aspire; no runtime files outside this research directory will be changed.
* Success Criteria:
  * Identify the controlling configuration and startup sequence with file-and-line evidence.
  * Distinguish persistent container lifetime from persistent Keycloak data.
  * Explain whether delayed health is expected or indicates a configuration defect.
  * Identify the mechanism behind authentication test unreliability.
  * Compare viable resolutions and select one implementation approach.

## Outline

1. Verified repository configuration and startup dependencies.
2. Traced authentication fixtures and readiness gates.
3. Compared behavior with Aspire and Keycloak documentation.
4. Evaluated readiness, persistence, provisioning, and isolation alternatives.
5. Selected a staged resolution with focused validation.

## Potential Next Research

* Capture clean, warm, and cold startup traces.
  * Reasoning: Timing evidence is needed to choose fixture deadlines and determine whether the 60-second DCP dependency timeout is too short.
  * Reference: `src/TNC.Trading.Platform.AppHost/appsettings.json:8-10`
* Confirm whether CI starts real-authentication test projects concurrently.
  * Reasoning: Fixed host port 8080 creates a cross-process collision only when independent test assemblies overlap.
  * Reference: `test/Shared/Authentication/RealAppHostProcessFactory.cs:13-94`
* Prototype a portable cross-process lease for the real-Keycloak suites.
  * Reasoning: xUnit collection serialization does not coordinate separate test processes.
  * Reference: `.github/instructions/aspire-tests.instructions.md:18-58`
* Measure the generated Keycloak volume name before adding a reset command.
  * Reasoning: A reset workflow must delete only this AppHost's identity data and must remain an explicit destructive action.
  * Reference: `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-42`

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-42`
  * Registers Keycloak on fixed host port 8080, disables endpoint proxying, imports `./Realms`, and uses `ContainerLifetime.Persistent` by default.
  * `AppHost:UsePersistentKeycloakState=false` changes the lifetime to `Session` for authentication tests.
  * Keycloak has no repository-declared `WithDataVolume()` or `WithDataBindMount()`. SQL Server has both a data volume and persistent lifetime at lines 16-19.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:48-72,150-184`
  * Injects the fixed public authority `http://localhost:8080/realms/tnc-trading-platform` into API and Web configuration.
  * API and Web use `WaitFor(keycloak)`, but this does not gate direct calls made by a test fixture after `StartAsync()`.
* `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:118-165`
  * API and Web readiness endpoints contain only an always-healthy self check. They do not prove OIDC discovery, realm import, token issuance, or downstream identity availability.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:49-145`
  * The managed Web fixture calls `StartAsync()` and immediately obtains a master-realm admin token, queries the imported client, and updates redirect URIs.
  * It has no Keycloak resource-health wait, application-realm discovery probe, bounded overall timeout, or transient retry policy before those calls.
* `test/Shared/Authentication/AppHostProcessHandle.cs:125-177,294-337`
  * The later Web sign-in readiness loop is stronger: it follows an OIDC challenge and verifies the Keycloak login page. The earlier Admin API race prevents this loop from running when Keycloak starts slowly.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs:19-67`
  * API tests already use a behavior-level token retry that proves the realm, direct-grant client, seeded user, password, and scope.
* `src/TNC.Trading.Platform.AppHost/appsettings.json:8-10`
  * `Dcp:DependencyCheckTimeout` is 60 seconds. This may be shorter than a cold image pull and first realm import, but runtime timing has not yet established that it is insufficient.
* `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:1-259`
  * Defines the application realm, clients, roles, scopes, and seeded test users. Startup import does not reconcile this file into an already existing realm.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:9-16`
  * Uses `Aspire.Hosting.Testing` 13.1.2 while the AppHost and other Aspire test projects use 13.2.3. This is avoidable compatibility drift, not a demonstrated cause of delayed health.

### Code Search Results

* `WithLifetime` and `UsePersistentKeycloakState`
  * Local development selects persistent container lifetime; both real-authentication harness paths disable it to obtain a clean realm import.
* `WaitForResourceHealthyAsync`
  * No call gates the managed fixture's direct Keycloak Admin API sequence.
* `WaitFor(keycloak)`
  * API and Web resource registration waits for Keycloak, but test-fixture code runs outside that dependency graph.
* `WithDataVolume` and `WithDataBindMount`
  * SQL Server uses explicit durable storage. Keycloak does not.
* Fixed port `8080`
  * API integration, Web functional, and Web E2E authentication fixtures all require the same Keycloak host port. Per-assembly xUnit collections cannot serialize separate test processes.

### External Research

* Aspire Keycloak integration: `https://aspire.dev/integrations/security/keycloak/`
  * `AddKeycloak` enables health support and registers `/health/ready` on management port 9000.
  * `WithDataVolume()` persists `/opt/keycloak/data` outside the container lifecycle.
* Aspire testing: `https://aspire.dev/testing/write-your-first-test/`
  * The official pattern treats `StartAsync()` and `WaitForResourceHealthyAsync()` as separate operations.
* Aspire data persistence: `https://aspire.dev/fundamentals/persist-data-volumes/`
  * Container lifetime and durable mounted data are separate controls. Runtime-managed volumes are preferred when host file access is unnecessary.
* Keycloak health checks: `https://www.keycloak.org/observability/health`
  * `/health/ready` returns 200 when the instance is ready and 503 otherwise. Opening the HTTP port is not a readiness signal because bootstrap can continue asynchronously.
* Keycloak realm import: `https://www.keycloak.org/server/importExport`
  * Startup imports complete before full server startup, but Keycloak skips a realm that already exists. Persistence can therefore preserve stale realm state.
* Keycloak container optimization: `https://www.keycloak.org/server/containers`
  * A prebuilt optimized image can reduce repeated startup work, but it does not replace health and protocol-level readiness barriers.
* Pinned Aspire source commit: `https://github.com/microsoft/aspire/commit/0838645cc4f7fc27e46200ac14cbe5c751637593`
  * Confirms that the repository's Keycloak integration uses management port 9000, `KC_HEALTH_ENABLED=true`, and `/health/ready`.

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`, `.github/instructions/aspire.instructions.md`, `.github/instructions/aspire-tests.instructions.md`, `.github/instructions/auth.instructions.md`, `.github/instructions/tests.instructions.md`, and `.github/instructions/folders.instructions.md`.
* Local authentication remains Keycloak orchestrated by Aspire.
* Closed-box tests should use isolated resources, bounded condition-based waits, deterministic cleanup, and no fixed startup sleeps.
* A stable browser-visible Keycloak port is an intentional local constraint because tokens, cookies, authorities, and redirect URIs share the issuer origin.

## Key Discoveries

### Project Structure

The AppHost owns Keycloak resource lifecycle and dependency ordering. Shared authentication test infrastructure under `test/Shared/Authentication` owns direct readiness and Admin API calls. API and Web authentication packages defer meaningful OIDC work until the first bearer request or challenge, so process readiness is intentionally weaker than authentication readiness.

### Implementation Patterns

The repository already separates local and test lifecycle through `AppHost:UsePersistentKeycloakState`. Tests select session lifetime to force a fresh realm import. This is the correct isolation model and should remain.

Aspire's `WaitFor(keycloak)` is meaningful because `AddKeycloak` supplies a health-check annotation. It waits for Running, Healthy, and the resource-ready event. That protects API and Web startup dependencies, but it cannot protect fixture code that directly calls Keycloak after `StartAsync()`.

The test fixture needs layered readiness:

1. Aspire resource health proves Keycloak `/health/ready`.
2. Application-realm discovery proves the imported realm exists and advertises the expected issuer.
3. The protocol operation under test proves the required client, user, scope, and redirect configuration.

Persistent lifetime reuses a compatible container and its writable layer. An explicit data volume makes `/opt/keycloak/data` survive container replacement. Neither mechanism updates an existing realm from checked-in import JSON.

### Complete Examples

The following shape illustrates the selected readiness sequence. Exact helper names should follow the existing shared fixture when implemented.

```csharp
using var readinessTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

await application.StartAsync(readinessTimeout.Token);
await application.ResourceNotifications.WaitForResourceHealthyAsync(
  "keycloak",
  readinessTimeout.Token);

await WaitForRealmDiscoveryAsync(
  new Uri("http://localhost:8080/realms/tnc-trading-platform/.well-known/openid-configuration"),
  expectedIssuer: "http://localhost:8080/realms/tnc-trading-platform",
  readinessTimeout.Token);

await ConfigureDynamicWebRedirectUriAsync(readinessTimeout.Token);
```

Retries should share one bounded deadline. Connection failures, temporary 404 responses during import, HTTP 429, and HTTP 503 are transient candidates. HTTP 400 and 401 from the admin token endpoint indicate configuration or credential drift and should fail immediately with response diagnostics.

### API and Schema Documentation

* Keycloak readiness: `GET http://<management-endpoint>/health/ready`
  * Used internally by the Aspire Keycloak integration on management port 9000.
* Application-realm discovery: `GET http://localhost:8080/realms/tnc-trading-platform/.well-known/openid-configuration`
  * Must return 200 and an `issuer` equal to the configured public authority.
* Admin token: `POST http://localhost:8080/realms/master/protocol/openid-connect/token`
  * Proves bootstrap administrator access, not application-realm readiness.
* Application token and Web challenge
  * Provide the final behavior-level readiness proof for API and browser tests respectively.

### Configuration Examples

If explicit local data durability is selected later, apply the volume only to persistent local mode. Session test mode must remain volume-free.

```csharp
var keycloak = builder.AddKeycloak("keycloak", hostPort: 8080)
  .WithEndpointProxySupport(false)
  .WithRealmImport("./Realms");

if (usePersistentKeycloakState)
{
  keycloak
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();
}
else
{
  keycloak.WithLifetime(ContainerLifetime.Session);
}
```

This is an optional local-development change. It does not fix the test race and requires a documented, guarded reset workflow because deleting the volume removes realms, users, credentials, and sessions.

## Technical Scenarios

### Keycloak Container Lifecycle and Readiness

Delayed Keycloak health is not itself evidence of a broken health check. Keycloak must initialize services and import a fresh realm before `/health/ready` succeeds. The Aspire dashboard correctly reports Unhealthy during that interval and Healthy afterward.

The inconsistent comparison with SQL Server and Mailpit has two likely environmental explanations: their containers are already warm and their startup work differs. Keycloak test runs intentionally use session lifetime, so they pay fresh-container and realm-import cost. Normal local Keycloak runs use persistent lifetime and should reuse a compatible container, but they still undergo process bootstrap on restart and may retain stale realm state.

Container existence does not by itself mean instant Keycloak availability. The expected behavior depends on the retained runtime state:

| State when AppHost starts | Realm behavior | Expected readiness |
|---------------------------|----------------|--------------------|
| Persistent container is still running and Healthy | No new Keycloak process starts, so `--import-realm` does not run again | Usually already usable; Aspire must still reconcile the resource and observe its health check |
| Persistent container exists but is stopped | Keycloak starts again; startup import sees the existing realm in the writable layer and skips it | Not instant because JVM, Quarkus, database, cache, and health-check startup still run |
| Container is recreated without a data volume | The old embedded database is lost and the realm is imported into fresh state | Slowest normal path because full bootstrap and realm import both run |
| Container is recreated with `/opt/keycloak/data` retained | The realm remains in the mounted data store, so startup import skips it | Full process bootstrap still runs even though realm import is skipped |

Aspire persistent lifetime normally leaves the container running when AppHost stops. If that same healthy container is reused, realm import is not the cause of a subsequent delay. Realm import is most relevant to the first launch, session-lifetime tests, or recreation without a retained data directory. A delay while reattaching to an unchanged running container instead points toward Aspire reconciliation, health-check polling, endpoint reachability, or a realm-specific OIDC check.

Keycloak startup import never refreshes an existing realm. It deliberately skips it. This makes warm local startup faster, but changes to checked-in realm JSON do not take effect until state is reset or an explicit provisioning/import process updates the realm.

### Why Authentication Tests Use Session Lifetime

Session lifetime is not required because Keycloak cannot process concurrent authentication requests. Keycloak is designed to support many clients, users, sessions, and requests. The test setting instead provides isolation from mutable identity-provider state and ensures the checked-in realm definition is imported into known-empty state.

The value differs by test suite:

* API real-authentication tests do not change realm or client configuration. They issue password grants, tokens, and login/session state. These tests could potentially share a correctly provisioned Keycloak instance.
* Web functional and E2E fixture startup currently uses the Admin API to append a callback URI and replace the shared Web client's `webOrigins` in `test/Shared/Authentication/RealAppHostProcessFactory.cs:97-145`.
* The Web client mutation is not restored. Disposing the session-lifetime container is currently the only complete cleanup for that configuration change and any remaining authentication sessions.
* Separate API, Web functional, and Web E2E projects can each create an AppHost. Session lifetime can therefore cause up to three Keycloak bootstraps and realm imports in a full run.
* Session lifetime does not solve concurrent fixed-port ownership. Independent containers still compete for host port 8080, so cross-process serialization remains necessary.

The realm JSON already permits wildcard localhost callbacks and origins at `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:44-68`. If a runtime sign-in test proves those entries support the randomized Web callback, the Admin API mutation may be unnecessary. Removing it would eliminate the strongest mutable-state reason for a fresh Keycloak container.

A safe warm-reuse experiment should proceed in this order:

1. Prove complete Web sign-in and sign-out using the imported wildcard callback configuration and remove the dynamic client mutation if it is redundant.
2. Serialize ownership of fixed host port 8080 across real-authentication test processes.
3. Reuse one run-scoped Keycloak container rather than an indefinite developer-owned instance.
4. Before each project, verify a realm version or fingerprint and the required clients, users, roles, redirect URIs, and issuer.
5. Keep browser cookies, tokens, and HTTP clients fixture-local and clean up sessions where tests depend on session state.
6. Measure total suite duration and reliability against the session-container baseline.

Changing only `AppHost:UsePersistentKeycloakState` is unsafe. Startup import skips an existing realm, so a persistent test instance can silently use stale configuration. With the current un-restored client mutation, it can also carry changes from one fixture into another. Session lifetime should remain the deterministic baseline until those two conditions are removed.

**Requirements:**

* Preserve local Keycloak-in-Aspire architecture.
* Make local startup behavior predictable.
* Prevent authentication tests from starting before OIDC is usable.

**Preferred Approach:**

Use a staged resolution:

1. Repair managed test readiness in `test/Shared/Authentication/RealAppHostProcessFactory.cs:49-145`.
  Await bounded Keycloak Healthy state, then bounded target-realm discovery and issuer validation before requesting an admin token or changing redirect URIs. Add transient retry classification and dispose partially started application/builder instances on failure. Keep session-lifetime Keycloak for tests.
2. Measure clean, warm, and cold startup transitions.
  Record `StartAsync()`, Running, Healthy, realm discovery, admin token, redirect update, API/Web readiness, and login-page readiness. Increase `Dcp:DependencyCheckTimeout` only if measured Healthy transitions exceed 60 seconds.
3. Serialize real-Keycloak test processes.
  Add a bounded, portable cross-process lease around API integration, Web functional, and Web E2E fixtures because all bind port 8080. Preserve per-assembly collections. Defer dynamic ports until authority, issuer, browser URL, token calls, and redirects can be redesigned together.
4. Optionally make local data persistence explicit.
  Add `WithDataVolume()` only for persistent local mode if surviving container replacement is a requirement. Provide an explicit reset workflow and model tests. Never share this volume with automated tests.
5. Align `Aspire.Hosting.Testing` to 13.2.3 across test projects as compatibility cleanup.

Acceptance checks:

* Run the managed Web authentication fixture at least 20 times with the image already present and a fresh session container. No Admin API call may occur before both readiness barriers, and all runs must reach the Keycloak login page.
* Run one cold-image startup and compare Keycloak Healthy timing with the 60-second DCP dependency timeout.
* Inject transient discovery/Admin failures and verify retry deadlines, immediate 400/401 failure, and diagnostic output.
* Force failures after Build and Start and verify no AppHost process, builder, container, or environment override leaks.
* Run the three real-authentication projects individually, then concurrently before and after the cross-process lease, recording port 8080 ownership.
* For optional local persistence, verify state after AppHost restart, container recreation with the volume retained, and explicit volume reset. Confirm tests never attach the volume.

#### Considered Alternatives

| Rank | Scenario | Decision | Rationale |
|------|----------|----------|-----------|
| 1 | Layered fixture readiness | Select now | Directly fixes the proven race with low complexity and no issuer change |
| 2 | Cross-process serialization | Select as hardening | Prevents fixed-port conflicts across test assemblies while retaining the current public issuer |
| 3 | Local data volume plus reset | Optional | Improves developer continuity but increases stale-state risk and does not fix tests |
| 4 | Idempotent provisioning job | Defer | Appropriate only when durable realm reconciliation becomes a requirement; adds a new project and provisioning contract |
| 5 | Shared persistent test Keycloak | Reject | Creates shared mutable state, stale imports, ordering dependencies, and ambiguous cleanup |
| 6 | Dynamic Keycloak test ports | Defer | Requires an end-to-end issuer and authority redesign across browser, API, Web, and direct test calls |

`WaitForCompletion` should not target Keycloak because Keycloak is long-running. It becomes appropriate only if a separate finite, idempotent realm-provisioning resource is introduced.

## Evidence-Based Conclusion

The delayed dashboard transition is expected readiness behavior, not the root defect. Keycloak's Aspire integration already probes `/health/ready`; the managed authentication fixture bypasses that contract by calling the Admin API directly after `StartAsync()`. Add the missing resource and realm readiness barriers first.

Container persistence should be clarified independently. The current persistent lifetime retains a compatible local container but does not declare durable `/opt/keycloak/data` storage. A named data volume is reasonable only if developers need identity state to survive container replacement, and it must come with a reset workflow because startup import skips an existing realm. Automated tests should continue using clean session containers.

Fixed port 8080 is a second reliability risk when separate test projects overlap. Cross-process serialization is the smallest compatible hardening step. Dynamic ports are a later architectural change because the public issuer must remain consistent everywhere.

## Research Artifacts

* Primary: `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md`
* Repository analysis: `.copilot-tracking/research/subagents/2026-07-27/keycloak-repository-configuration-research.md`
* Authoritative guidance: `.copilot-tracking/research/subagents/2026-07-27/keycloak-aspire-authoritative-guidance-research.md`
* Alternatives analysis: `.copilot-tracking/research/subagents/2026-07-27/keycloak-remediation-alternatives-research.md`
