<!-- markdownlint-disable-file -->

# Keycloak Repository Configuration Research

## Research status

Complete for repository configuration and static code-path analysis. A live
authentication test run was intentionally not started because an AppHost process
and a listener on host port 8080 were already active. Starting another fixture
would not be an isolated check and could disrupt the user's running environment.

## Research questions

* How does the AppHost register Keycloak, including image/version, endpoints, container lifetime, storage, realm import, and health checks?
* Which API and Web resources reference or wait for Keycloak, and what readiness state does each dependency guarantee?
* How do authentication and Aspire integration-test fixtures start the distributed application and gate access to Keycloak, API, and Web resources?
* Which runtime configurations and package versions influence startup, issuer discovery, realm import, and test reliability?
* Which race mechanisms are supported by repository evidence, and which cheap runtime checks can prove or disprove each one?
* Which focused remediation candidates preserve the repository rules against fixed sleeps and shared mutable test state?

## Verified findings

### AppHost Keycloak registration

* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-42`
	registers Keycloak as the stable resource name `keycloak` on host port 8080,
	disables endpoint proxying, imports `./Realms`, and defaults to
	`ContainerLifetime.Persistent`. `AppHost:UsePersistentKeycloakState=false`
	changes only the container lifetime to `Session`.
* The Keycloak resource has no repository-declared `WithDataVolume()` or
	`WithDataBindMount()`. This differs from SQL Server at
	`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:16-19`,
	which explicitly has both a data volume and persistent container lifetime.
* Installed package documentation at
	`C:/Users/martynfewtrell/.nuget/packages/aspire.hosting.keycloak/13.2.3-preview.1.26217.6/lib/net8.0/Aspire.Hosting.Keycloak.xml:100-143`
	says Keycloak data mounts target `/opt/keycloak/data`, while realm import files
	are copied to `/opt/keycloak/data/import`. The repository uses the import API
	but neither data-mount API.
* Container persistence and data persistence are therefore not equivalent here.
	Persistent lifetime preserves and reuses the container and its writable layer
	across AppHost sessions. It does not declare an independently durable named
	volume. Removing/recreating that container removes its realm database. Session
	lifetime creates a fresh container and fresh import state for a test run.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs:54-72`
	explicitly documents why tests select session lifetime: fresh realm imports
	make runtime-listener callback changes deterministic. No test asserts a
	Keycloak data mount or the Keycloak health-check annotation/path.

### Realm import and credentials

* `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:1-34`
	creates the enabled realm, three realm roles, and three delegated scopes.
* `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:35-131`
	defines the bearer-only API client and public PKCE Web client. The Web client
	accepts wildcard localhost callback ports in addition to fixed development
	ports.
* `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:132-207`
	defines the public direct-grant test client used by API integration tests.
* `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:208-259`
	seeds four users with roles and the shared local password. The same password
	is also present in Web runtime configuration and test code. This is local test
	data, but it expands the number of duplicated values that can drift.
* A reused persistent Keycloak container can retain an older imported realm.
	Keycloak startup import is not a general reconciliation mechanism for changes
	to an already existing realm. Session-lifetime test containers avoid that
	stale-import case, provided the fresh container starts successfully.

### Dependency and readiness graph

* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:8-27` makes the
	API wait for SQL database and Mailpit resources.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:150-184` adds
	`WaitFor(keycloak)` to the API only for the real Keycloak provider and always
	adds `WaitFor(keycloak)` to the Web resource.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:29-42` also
	makes Web wait for API. The resulting intended order is Keycloak/SQL/Mailpit,
	then API, then Web, although API and Web health semantics weaken what that
	order proves.
* `src/TNC.Trading.Platform.AppHost/appsettings.json:9-11` sets
	`Dcp:DependencyCheckTimeout` to 60 seconds. Delayed Keycloak health beyond this
	budget can prevent dependent projects from starting even though test fixture
	outer timeouts are 90, 120, or 300 seconds.
* The repository adds no explicit Keycloak `WithHttpHealthCheck` or named health
	check. `AddKeycloak` supplies any Keycloak health annotation internally. The
	exact package-internal probe URI was not established from the XML docs, and no
	composition test asserts it. This is an evidence gap, not proof that Keycloak
	has no health check.

### API and Web readiness semantics

* `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:118-128` registers only
	an always-healthy `self` check. `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:133-165`
	maps readiness over all registered checks and liveness over the `live` tag.
	Consequently both API and Web `/health/ready` endpoints prove process and
	endpoint responsiveness, not Keycloak discovery, imported realm availability,
	token issuance, database readiness after startup, or downstream API access.
* `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:32-70`
	configures JWT authority/audience but does not fetch OIDC metadata during
	startup. Metadata/signing-key retrieval occurs when bearer authentication is
	first exercised.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:57-105`
	configures OIDC, but discovery is exercised when the Web app issues a challenge,
	not by Web startup or `/health/ready`.
* Both apps use the fixed authority
	`http://localhost:8080/realms/tnc-trading-platform` through
	`src/TNC.Trading.Platform.AppHost/AppHostCompositionConstants.cs:6-9` and
	`src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:48-72` rather
	than an Aspire resource reference. This is required for browser-visible issuer
	consistency in the current design, but makes port 8080 global shared state.

### Authentication test harnesses

* API real-auth tests use an external `dotnet run` AppHost process through
	`test/Shared/Authentication/RealAppHostProcessFactory.cs:13-47`. They disable
	persistent Keycloak state and set the admin password. Their fixture waits up
	to 120 seconds to discover API, then up to five minutes for API readiness, then
	up to 90 seconds for a real seeded-user token:
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:13-27`.
* The API discovery algorithm in
	`test/Shared/Authentication/AppHostProcessHandle.cs:75-123` scans every newly
	listening local port and accepts a candidate when `/health/ready` returns 200
	and `/api/platform/status` returns 401. It does not use Aspire's resource
	endpoint lookup for this external-process path. Concurrent hosts or unrelated
	new listeners can be misidentified.
* The real token gate in
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs:19-67`
	is behaviorally meaningful: it proves the imported realm, direct-grant client,
	seeded user, password, and requested scope. It retries connection, cancellation,
	invalid-payload, and non-success HTTP failures because
	`EnsureSuccessStatusCode()` throws `HttpRequestException`, which is retried.
* Web functional and E2E tests use
	`DistributedApplicationTestingBuilder` through
	`test/Shared/Authentication/RealAppHostProcessFactory.cs:49-94`. After
	`application.StartAsync()`, the current workspace immediately calls
	`ConfigureDynamicWebRedirectUriAsync`.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:97-145` posts directly
	to the master-realm admin token endpoint on port 8080, queries the imported Web
	client, and updates redirect URIs. There is no
	`WaitForResourceAsync("keycloak", KnownResourceStates.Healthy)`, discovery
	probe, retry, or timeout around this sequence. A delayed container, delayed
	realm import, or brief connection reset fails fixture initialization before
	the later Web sign-in readiness loop can run. This is the highest-confidence
	repository race.
* Git blame shows `test/Shared/Authentication/RealAppHostProcessFactory.cs:69`
	and `:73-145` are current uncommitted workspace changes. The committed June 30
	harness disabled port randomization and did not perform this admin mutation.
	These findings describe current behavior without assigning committed intent.
* Once initialization succeeds, Web readiness is stronger than `/health/ready`.
	`test/Shared/Authentication/AppHostProcessHandle.cs:125-177` and `:294-337`
	repeatedly challenge the Web sign-in endpoint, follow the redirect to Keycloak,
	fetch the login page, and require expected login markup. This proves OIDC
	discovery, imported Web client availability, and login-page rendering.
* Functional browser authentication then waits for the username field and a
	platform session cookie at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationSessionFactory.cs:14-60`
	and `:87-128`.
* Each real-auth collection disables parallelization inside its own assembly:
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestCollection.cs:1-6`,
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs:1-8`,
	and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs:1-8`.
	This does not serialize separate test projects in a solution-wide test run.
	API integration, Web functional, and Web E2E hosts can therefore compete for
	the same fixed Keycloak host port 8080.

### Package and runtime versions

* `global.json:1-6` selects .NET SDK 10.0.200 with latest-feature roll-forward.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-6`
	uses Aspire AppHost SDK 13.2.3, SQL Server integration 13.2.3, and Keycloak
	integration `13.2.3-preview.1.26217.6`.
* Installed Keycloak package metadata records Microsoft Aspire commit
	`0838645cc4f7fc27e46200ac14cbe5c751637593`, depends on Aspire.Hosting 13.2.3,
	and defaults to `quay.io/keycloak/keycloak:26.5`:
	`C:/Users/martynfewtrell/.nuget/packages/aspire.hosting.keycloak/13.2.3-preview.1.26217.6/aspire.hosting.keycloak.nuspec:2-42`
	and `.../Aspire.Hosting.Keycloak.xml:6-17`.
* Web functional, Web E2E, and AppHost unit tests use Aspire.Hosting.Testing
	13.2.3. API integration tests alone use 13.1.2 at
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:9-16`.
	The project references a 13.2.3 AppHost, so this version drift is avoidable
	compatibility risk, although static evidence does not prove it causes the
	delayed-health failure.
* API JWT bearer and Web OIDC packages are 10.0.5. Playwright packages are
	1.58.0. These versions do not expose a direct static cause for Keycloak delay.

## Startup flow

1. `src/TNC.Trading.Platform.AppHost/AppHost.cs:1-14` creates settings,
	infrastructure, API, and Web resources; applies environment wiring; then
	builds and runs the distributed application.
2. Keycloak starts on host port 8080. For normal local runs it reuses a persistent
	container; test harnesses set session lifetime so a fresh container imports
	the realm JSON.
3. API waits for database, Mailpit, and Keycloak resource readiness. Web waits
	for API and Keycloak resource readiness.
4. API startup creates/recreates its SQL schema and performs application startup
	work before opening endpoints at `src/TNC.Trading.Platform.Api/Program.cs:18-58`.
	No startup step verifies OIDC discovery or token issuance.
5. Web opens after its Aspire dependencies permit startup. No startup step
	verifies OIDC discovery. A challenge performs the first meaningful OIDC check.
6. The external API fixture discovers an HTTP endpoint, waits on process-only API
	readiness, then polls Keycloak token issuance. This eventually gates tests on
	real auth behavior.
7. The managed Web fixture currently starts the application and immediately
	requests an admin token to mutate redirect URIs. Only after that succeeds does
	it poll the sign-in challenge and Keycloak login page.

## Hypotheses and discriminating runtime checks

### H1: Ungated admin mutation races Keycloak startup

Confidence: high.

The managed Web harness calls the master token endpoint immediately after
`StartAsync()`. `StartAsync()` starts orchestration; it does not establish that
every resource is healthy or that realm import is complete.

Cheap check: instrument timestamps around `application.StartAsync()`,
`WaitForResourceAsync("keycloak", KnownResourceStates.Healthy)`, the master token
POST, and the imported-client GET. Run one functional fixture against a clean
session container. If the current POST fails before the resource reaches Healthy
but succeeds immediately after the resource wait, H1 is proven.

### H2: Fixed port 8080 causes cross-host contention or wrong-host access

Confidence: high for solution-wide concurrent test runs.

All real hosts bind Keycloak to 8080 and all direct auth calls use localhost:8080.
Collection controls do not serialize different test assemblies. During research,
an AppHost process and a listener on 8080 were already active, demonstrating the
environmental precondition without identifying that listener's container owner.

Cheap check: before each fixture, record `Get-NetTCPConnection -State Listen
-LocalPort 8080` and the owning process/container; run each real-auth project
alone, then run the three projects concurrently. A bind failure, changed owner,
or requests reaching another fixture's realm proves H2.

### H3: `WaitFor` and app readiness report healthy before auth is usable

Confidence: high for API/Web readiness; package-internal Keycloak probe details
remain unverified.

API/Web readiness only runs the self check. JWT metadata and Web OIDC discovery
are lazy. Therefore an API/Web Healthy state is not an auth-ready state.

Cheap check: on a clean startup, poll and timestamp API `/health/ready`, Web
`/health/ready`, realm discovery
`/realms/tnc-trading-platform/.well-known/openid-configuration`, a seeded-user
token grant, and the Web login page. Any interval where app readiness is 200 but
one auth probe fails proves the readiness gap.

### H4: The 60-second DCP dependency timeout is shorter than cold Keycloak startup

Confidence: medium.

Test-level waits cannot help if DCP stops waiting for Keycloak after 60 seconds
and never starts API/Web. Cold image pulls, Windows container-engine load, and
fresh realm import can exceed that threshold.

Cheap check: capture Aspire resource-state transitions and Keycloak logs from
container start through Healthy. If Healthy occurs after 60 seconds and dependent
resources report dependency timeout, H4 is proven. If Keycloak is healthy well
inside 60 seconds, reject H4 for that environment.

### H5: Persistent local container carries stale realm state

Confidence: medium for local development, low for test harnesses that successfully
apply `UsePersistentKeycloakState=false`.

Normal AppHost runs preserve the same container writable layer, while changed
realm imports may not overwrite an existing realm. This explains configuration
drift, missing clients, or stale redirect URIs, but not a clean session container's
slow health transition.

Cheap check: query the realm/client through the admin API, stop/restart the
persistent AppHost, then remove only the Keycloak container and start again.
Compare client IDs, redirect URIs, users, and import logs. A difference only after
container recreation proves stale container state. A named volume should not be
expected because none is configured.

### H6: API endpoint discovery selects another local listener

Confidence: medium when multiple hosts start concurrently.

The external-process API harness scans all new local ports. Its 200-readiness and
401-protected-route signature is specific but not tied to the launched AppHost
resource instance.

Cheap check: log each candidate URI and response signature together with the
launched AppHost's resource endpoint. If the accepted URI differs from Aspire's
API endpoint, H6 is proven. Running the API fixture alone with no other new
listeners should disprove it for isolated runs.

### H7: Aspire.Hosting.Testing 13.1.2 causes API harness incompatibility

Confidence: low.

The version differs from the 13.2.3 AppHost and all other Aspire test projects.
No static call failure or binary error was found.

Cheap check: compare restore graph and one isolated API real-auth run before and
after aligning only Aspire.Hosting.Testing to 13.2.3. Treat unchanged timing and
behavior as evidence against H7.

## Focused remediation candidates

1. Gate the managed redirect mutation on Aspire resource state. After
	`StartAsync()`, await Keycloak Healthy with a bounded cancellation token, then
	perform a behavior probe that proves the imported realm/client exists. Keep
	retries condition-based and diagnostic; do not add a fixed sleep.
2. Make the admin mutation retry only transient connection, 404-during-import,
	and service-unavailable outcomes until one overall deadline. Include the last
	status/body and resource state in failure output. Avoid retrying authentication
	failures indefinitely because those indicate configuration drift.
3. Decide test isolation for port 8080 explicitly. The smallest reliable options
	are a cross-process lock/serialized CI stage for all real-Keycloak suites, or
	one shared real-auth AppHost fixture/suite. Randomizing Keycloak's issuer port
	is a larger change because browser-visible authority, API issuer validation,
	and callback configuration must all receive the same dynamic endpoint.
4. Replace external API listener scanning with Aspire.Hosting.Testing endpoint
	lookup if practical. This directly binds the test client to resource `api` and
	improves disposal and diagnostics. If the external process must remain, read
	the resource service endpoint rather than scanning every new local port.
5. Add an explicit auth-readiness contract for tests. A realm discovery probe
	plus one seeded-user token grant is sufficient for API real-auth tests; a Web
	challenge that renders the Keycloak login page is sufficient for Web tests.
	Do not redefine general application readiness to require an external IdP
	unless production orchestration semantics also require that coupling.
6. Reconcile `Dcp:DependencyCheckTimeout` with observed cold-start data. Increase
	it only if measured Keycloak Healthy transitions exceed 60 seconds; preserve
	a finite bound and report resource diagnostics on timeout.
7. Align Aspire.Hosting.Testing to 13.2.3 across test projects. This removes a
	compatibility variable but should not substitute for the missing readiness
	gate.
8. Add AppHost model tests for Keycloak lifetime, endpoint, realm-import mount,
	and health annotation. Keep persistence semantics explicit: add
	`WithDataVolume()` only if realm data must survive container replacement.
	Otherwise retain persistent container lifetime for local convenience and
	document that deleting the container resets Keycloak state.
9. Consolidate duplicated auth constants and remove obsolete duplicate
	`AppHostProcessHandle` files under individual test projects if they are no
	longer compiled. This reduces drift, but it is secondary to the startup race.

## Evidence and references

* Repository instructions prohibit time-based sleeps as the primary auth/test
	readiness strategy and require Aspire closed-box tests:
	`.github/instructions/auth.instructions.md:38-66`,
	`.github/instructions/aspire-tests.instructions.md:22-58`, and
	`.github/instructions/tests.instructions.md:38-76`.
* Commit `b5ce789` (`Refactored flakey tests`, 2026-05-25) introduced the focused
	real-auth fixture/token-readiness design and documented session-lifetime intent.
* Commit `83d84f8` (2026-06-30) introduced the shared managed AppHost harness. Git
	blame distinguishes its committed lines from the current uncommitted dynamic
	redirect mutation.
* Installed package README recommends stable port 8080 for local development to
	avoid browser cookie/token issuer drift:
	`C:/Users/martynfewtrell/.nuget/packages/aspire.hosting.keycloak/13.2.3-preview.1.26217.6/README.md:12-29`.
	That local-development rationale does not itself solve concurrent test-host
	ownership of the same fixed port.
* A binary string inspection attempt for the package-internal health path could
	not run because `rg` is unavailable in the current PowerShell environment.
	XML documentation and repository model tests do not expose the exact probe.

## Gaps and clarifying questions

* The exact health endpoint and success condition added internally by
	Aspire.Hosting.Keycloak `13.2.3-preview.1.26217.6` remain unverified. Inspect
	Microsoft Aspire source at package commit
	`0838645cc4f7fc27e46200ac14cbe5c751637593`, or inspect the built resource model
	health annotation in a focused unit test.
* No clean, isolated runtime trace was captured because port 8080 and an AppHost
	were already active. Resource transition timestamps and Keycloak import logs
	are needed to determine whether the observed delay exceeds the 60-second DCP
	dependency timeout.
* The owner and purpose of the active port-8080 listener were not changed or
	terminated. Confirm whether CI and local test orchestration routinely run the
	three real-auth test projects concurrently.
* Current uncommitted changes in the shared harness may be active work. Any
	remediation should preserve the intended dynamic callback-port behavior rather
	than reverting those changes without owner confirmation.