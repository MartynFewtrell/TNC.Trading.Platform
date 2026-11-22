<!-- markdownlint-disable-file -->

# Keycloak remediation alternatives research

## Research status

Complete. The two supplied research reports were treated as authoritative. Local
inspection was limited to current line verification, repository test rules, and
the absence of an existing finite provisioning resource. This revision also
inspected every real-auth fixture and test, the realm seed, cleanup behavior,
and commits `b5ce789` and `83d84f8`. No product or test source file was edited.

## Research questions

* How should scenarios A through E rank for reliability, implementation complexity, startup performance, stale-state risk, browser issuer constraints, and repository alignment?
* Which staged approach separates immediate readiness remediation from optional local-development persistence and independent concurrency hardening?
* What concrete code, configuration, test changes, and focused validation experiments should follow without editing product files during this research task?

## Working hypothesis

The immediate root cause is the managed Web test fixture calling Keycloak Admin APIs after `DistributedApplication.StartAsync()` without first awaiting Keycloak health and realm-specific OIDC usability. A bounded Aspire resource-health wait followed by a bounded realm discovery probe should remove that startup race without changing product persistence.

Cheap discriminating check: on a fresh session-lifetime Keycloak container, timestamp `StartAsync()`, Keycloak Healthy, realm discovery success, and the first Admin API request. The hypothesis is supported if the current Admin request can fail before the first two barriers and succeeds after both.

The evidence supports this hypothesis. `StartAsync()` and the Admin API call are
adjacent in the current fixture, while official Aspire test guidance treats
application start and resource-health waiting as separate operations. A runtime
trace remains necessary to measure the race window and select timeout values.

## Focused lifetime conclusion

Session lifetime is justified today, but not because Keycloak cannot handle the
number or concurrency of authentication requests. The suites generate a small
number of ordinary token and browser-login requests. Keycloak is designed to
serve many such requests. The relevant test risk is ownership of mutable identity
state: startup realm import, a shared Web client, redirect URI and web-origin
configuration, users, login sessions, issued tokens, and potentially failed-login
counters. Those records outlive an individual request when the container is
reused.

The original value of session lifetime was deterministic configuration, not
capacity. Commit `b5ce789` used persistent Keycloak even for the first real-auth
fixture. Commit `83d84f8` introduced `AppHost:UsePersistentKeycloakState=false`
in the shared harness and the AppHost lifetime switch. Its focused unit test says
the purpose is a fresh realm import so runtime-listener callback changes apply
deterministically. The current Admin API callback mutation is uncommitted work,
so it did not cause the original decision. It does, however, make isolation more
valuable now because the mutation is not restored.

Session lifetime buys a known realm baseline and automatic deletion when each
fixture disposes its AppHost. It costs one Keycloak process bootstrap and realm
import per AppHost fixture. In the current layout this means up to three cold
Keycloaks in a full real-auth run: one API integration AppHost, one Web functional
AppHost, and one Web E2E AppHost. It does not permit those hosts to run concurrently
because all still claim fixed host port 8080.

The lifetime choice is therefore conservative and reliable, but stronger than
strictly necessary once the realm is immutable, its version is verified, and
test-owned mutable records are unique or restored. It should remain until those
conditions exist. The missing readiness barrier is an independent defect: a warm
or persistent Keycloak must still be awaited and protocol-probed.

## Exact fixture and state inventory

| Suite | Fixture and scope | AppHost ownership | Keycloak reads | Keycloak mutations | Cleanup |
|------|-------------------|-------------------|----------------|--------------------|---------|
| API integration | `RealAuthenticationIntegrationTestFixture` is an `IClassFixture`; one current consuming class | One external `dotnet run` AppHost for `PlatformAuthenticationIntegrationTests` | Realm discovery is consumed lazily by JWT validation; direct-grant client, four seeded users, passwords, roles, scopes, signing keys, and token claims are exercised | Every readiness or authenticated request posts a password grant, issuing an access token and creating Keycloak login/session state. Expected credentials are valid, so failed-login counters are not intentionally changed. No Admin API or client/user configuration is changed | Fixture kills the AppHost process tree. Session lifetime removes its Keycloak container. Tokens are only held in disposed HTTP requests |
| Web functional | `RealAuthenticationFunctionalTestFixture` is an `ICollectionFixture`; shared by two classes | One managed AppHost for the whole authentication collection | Startup Admin GET reads the seeded Web client. Sign-in challenges read realm discovery, Web client settings, scopes, users, passwords, roles, and login UI | Startup obtains a master admin token, appends the runtime `/signin-oidc` URI, replaces `webOrigins`, and PUTs the whole Web client. Three browser logins create user and client sessions. One test performs OIDC sign-out and ends its session; two other authenticated sessions are not explicitly logged out. No user creation/update occurs. Valid credentials mean failed-login counters are not intentionally changed | Fixture disposes `DistributedApplication` and builder. Session container deletion discards the un-restored client update and remaining sessions |
| Web E2E | `RealAuthenticationE2ETestFixture` is an `ICollectionFixture`; one current consuming class | One separate managed AppHost for the E2E project | Same startup Admin GET and Web OIDC reads as functional; one seeded viewer is read | Same un-restored Web client PUT. One browser login creates a user/client session and tokens; no explicit logout. No user mutation or intentional failed login | Fixture disposal removes its session container and all mutation residue |

Repository evidence for the API token behavior is
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs:8-67`.
The real API class calls it for `local-viewer`, `local-operator`, `local-admin`,
and `local-norole` at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:64-387`.
The API fixture and class scope are at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:5-48`
and
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:5-18`.

The Web Admin token, client GET, redirect/origin mutation, and client PUT are at
`test/Shared/Authentication/RealAppHostProcessFactory.cs:97-145`. The method adds
the runtime sign-in callback, but it replaces `webOrigins` with only the current
Web origin and never records the old client for restoration. The checked-in realm
already accepts wildcard localhost sign-in and sign-out callbacks and wildcard
localhost origins at
`src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:44-68`.
Consequently dynamic Web callback ports do not, on current evidence, require this
mutation. A focused run without it is still needed to prove that Keycloak 26.5
accepts the wildcard form for the complete sign-in and sign-out flows.

Functional fixture sharing is defined at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs:5-8`;
its two consumers are
`PlatformAuthenticationFunctionalTests.cs:5-13` and
`PlatformProtectedRouteFunctionalTests.cs:5-13`. Browser credential submission
and session creation are at
`RealAuthenticationSessionFactory.cs:8-60`; the three calls are at
`PlatformAuthenticationFunctionalTests.cs:48-54`,
`PlatformAuthenticationFunctionalTests.cs:130-136`, and
`PlatformProtectedRouteFunctionalTests.cs:27-33`. The one real sign-out is at
`PlatformAuthenticationFunctionalTests.cs:57-76`.

E2E fixture sharing is defined at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs:5-8`.
Its one login is at
`PlatformDashboardAuthenticationE2ETests.cs:29-36`. Functional and E2E wrappers
both invoke the same shared managed factory, but they are separate test projects
and therefore create separate AppHosts and separate fixture containers.

No real-auth test deliberately submits a bad Keycloak password, calls a user
Admin API, creates/deletes users or clients, revokes tokens, clears sessions, or
resets failed-login counters. The realm JSON does not explicitly enable brute
force protection. Failed-login counters are therefore only a residual risk from
unexpected retries or future tests, not a current intentional mutation.

## Concurrency and isolation

Request concurrency and state isolation are different concerns:

* Concurrent valid logins and token grants are normal Keycloak workload. Nothing
	in this suite's volume supports a capacity-based need for fresh containers.
* Concurrent fixtures cannot safely start independent Keycloaks because
	`AppHostInfrastructureRegistration.cs:29-36` binds proxyless host port 8080 and
	`AppHostCompositionConstants.cs:5-9` fixes the issuer to that origin.
* `DisableParallelization=true` on each xUnit collection serializes only inside
	that test assembly. It does not coordinate the API, functional, and E2E
	processes. A solution-level parallel run can collide on port 8080 whether the
	lifetime is Session or Persistent.
* A single shared Keycloak can serve all three projects only if one owner starts
	it and the projects are serialized or given unique clients/users. Multiple
	AppHosts must not race to own the same fixed resource.
* Session containers solve stale-state and cleanup ownership after a fixture;
	they do not solve cross-process fixed-port ownership during fixture startup.

Aspire cleanup is complete on the normal managed-fixture path:
`test/Shared/Authentication/AppHostProcessHandle.cs:174-205` disposes the
application and builder. The external API path kills its AppHost process tree at
the same location. Session lifetime delegates container removal to Aspire. There
is no logical rollback of the Web client PUT; cleanup works only because the
whole session container is removed. The factory's initialization failure path at
`test/Shared/Authentication/RealAppHostProcessFactory.cs:66-92` currently disposes
environment scopes but can leak an application/builder created before the Admin
call fails. That is a cleanup defect independent of the selected lifetime.

## Lifetime model evaluation

| Model | Reliability | Startup | State risk | Fit for this repository |
|-------|-------------|---------|------------|-------------------------|
| Fresh Keycloak per fixture/project | Highest baseline determinism once readiness is fixed; normal disposal owns all state | Slowest, up to three bootstraps/imports | Lowest stale-state risk; current un-restored client mutation is harmless after container deletion | Current model and selected default for CI until reuse prerequisites exist |
| One shared Keycloak per test process | Reliable only if all real-auth classes in that process use one fixture and reset or uniquely namespace mutations | One bootstrap per process | Lower than cross-process sharing, but currently little or no gain because each of the three projects already has one real-auth fixture | Useful only after consolidating suites or adding more fixture classes; not the smallest present optimization |
| One shared persistent developer/CI Keycloak across projects | Fastest after warm-up | Avoids repeated bootstrap/import | Highest coupling: stale realm import, shared seeded users/sessions, un-restored client update, ambiguous owner, fixed-port contention, and failures dependent on prior runs | Acceptable for manual local development with reset guidance; reject as an unmanaged CI prerequisite |
| Immutable pre-provisioned shared Keycloak plus per-test clients/users | High when provisioning is versioned, clients/users are unique, and teardown or expiry is explicit | One warm bootstrap plus small provisioning cost | Controlled; global realm remains immutable and test state is namespaced | Best target if measured startup cost warrants engineering work, but larger than the immediate readiness repair |

The third model must not be confused with Aspire persistent lifetime inside each
independent AppHost. Three AppHosts concurrently requesting the same fixed port
do not become a shared service automatically. A shared service needs an explicit
owner and consumer contract.

## Selected recommendation

Keep session lifetime for the immediate repair. Add the Keycloak Healthy and
realm-protocol barriers described below, fix partial-start disposal, and serialize
the three real-auth projects with a cross-process lease while port 8080 remains
fixed. This is justified by deterministic imported configuration and cleanup,
not authentication request throughput.

The smallest design change that can safely test warm reuse is a serialized,
run-scoped persistent Keycloak experiment using the existing persistent-lifetime
branch, not a permanently shared developer or CI dependency:

1. Remove or bypass `ConfigureDynamicWebRedirectUriAsync` after proving the
	 checked-in wildcard callbacks and origins support both sign-in and sign-out.
	 This removes the only current configuration mutation.
2. Acquire one cross-process lease before any real-auth AppHost starts and hold
	 it through fixture disposal. This resolves fixed-port ownership.
3. Reuse the persistent Keycloak container across the serialized API,
	 functional, and E2E projects, but validate a realm version/fingerprint and the
	 required clients/users before each project. On mismatch, fail with a reset
	 instruction or recreate the test-owned container; do not silently accept an
	 existing realm because startup import skips it.
4. Keep browser contexts, application cookies, and tokens fixture-local. If
	 later tests mutate clients/users or intentionally trigger lockout, provision
	 unique per-run records and delete them, or revert to session lifetime.
5. Measure the three-project run before and after. Adopt warm reuse only if the
	 saved startup time outweighs the lease, validation, and reset complexity.

This experiment is smaller than introducing an external Keycloak deployment or
an idempotent provisioning service. It is not safe to change only the lifetime
flag today: the un-restored client PUT and skipped existing-realm imports would
make results depend on prior runs. If wildcard callback validation fails, retain
session lifetime or add snapshot-and-restore of the Web client under the same
cross-process lease.

The longer-term high-isolation option is one explicitly owned warm Keycloak with
an immutable versioned realm and unique clients/users per test run. That design
supports concurrent auth traffic but still cannot support concurrent browser
issuers on a single fixed `localhost:8080` unless client records are unique and
the AppHosts consume, rather than each try to own, the shared resource.

## Remaining lifetime uncertainty

* No clean runtime experiment proves that the wildcard callback and web-origin
	entries alone satisfy Keycloak 26.5 for every functional and E2E browser flow.
* No timing data quantifies how much of the suite is Keycloak bootstrap/import
	versus API, Web, SQL, image, or Playwright startup. The value of warm reuse is
	therefore architectural but not yet measured.
* The solution/CI runner was not found to define cross-project serialization.
	Confirm the actual orchestration command before assigning collision frequency.
* The exact DCP behavior when sequential AppHosts reconnect to the same persistent
	proxyless container should be validated with container ID and process-start
	timestamps on the supported Docker environment.
* The realm has no explicit version marker. A safe reuse experiment must define
	what is hashed or queried and who resets incompatible state.

## Authoritative inputs

* `.copilot-tracking/research/subagents/2026-07-27/keycloak-repository-configuration-research.md`
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-aspire-authoritative-guidance-research.md`

## Scenario analysis

Scores use 5 as best. For complexity, 5 means least implementation complexity.
For stale-state risk, 5 means least risk. Overall rank emphasizes deterministic
test reliability and repository alignment over startup speed.

| Rank | Scenario | Reliability | Complexity | Startup performance | Stale-state risk | Browser issuer fit | Repository alignment |
|------|----------|-------------|------------|---------------------|------------------|--------------------|----------------------|
| 1 | A. Readiness-only fixture repair | 5 | 5 | 3 | 5 | 5 | 5 |
| 2 | E. Cross-process serialization first; dynamic test ports later | 5 | 3 | 2 | 5 | 4 | 4 |
| 3 | B. Readiness plus explicit local data volume/reset workflow | 5 for tests; 4 locally | 3 | 4 locally; 3 in tests | 3 locally; 5 in session tests | 5 | 4 |
| 4 | C. Idempotent provisioning job with `WaitForCompletion` | 5 when fully engineered | 1 | 2 | 4 | 5 | 3 |
| 5 | D. Shared persistent Keycloak across tests | 2 | 2 | 5 after warm-up | 1 | 5 | 1 |

### Scenario A: readiness-only fixture repair

This is the immediate root-cause remediation. Keep session-lifetime Keycloak for
real-authentication tests, await the Aspire Keycloak resource's Healthy state,
then await target-realm OIDC discovery before requesting an admin token or
mutating the Web client. Use one bounded deadline and report the last resource
state, URI, status code, and response body on failure.

The sequence addresses two distinct contracts:

1. `WaitForResourceHealthyAsync("keycloak", token)` waits for the integration's
	 `/health/ready` probe and resource-ready event.
2. Polling
	 `/realms/tnc-trading-platform/.well-known/openid-configuration` proves that
	 the imported application realm is usable and that its issuer matches
	 `http://localhost:8080/realms/tnc-trading-platform`.

Only after both barriers should the fixture perform the master-realm token call,
client lookup, and redirect update. Transient connection failures, HTTP 404
during import, HTTP 429, and HTTP 503 may be retried within the shared deadline.
HTTP 400 or 401 from the admin token request should fail immediately because it
indicates credential or configuration drift rather than startup delay.

This scenario does not improve cold-start duration, but it converts variable
duration into bounded, observable waiting. It preserves the fixed issuer needed
by the browser and avoids shared state.

### Scenario E: test-isolation hardening

This is separate concurrency hardening, not the readiness fix. The API
integration, Web functional, and Web E2E assemblies can start independent
Keycloak containers that all bind host port 8080. Their xUnit collection settings
serialize tests only inside each process. Solution-wide or CI-level parallelism
can therefore cause a bind failure or direct requests to another fixture.

Use a repository-wide cross-process lease around every real-Keycloak fixture as
the first implementation. A named operating-system mutex is easy on Windows but
not portable. A small file-lock helper under `test/Shared/Authentication`, with a
bounded acquisition timeout and owner diagnostics, is portable enough for local
and CI use. CI job/stage serialization is an acceptable additional boundary but
does not protect developers who run multiple projects concurrently.

Dynamic Keycloak ports are deferred. They are architecturally cleaner for test
parallelism, but the public browser URL, JWT authority, OIDC issuer, direct token
calls, and redirect configuration must all resolve the same dynamic endpoint.
Changing only the container port would produce issuer or browser reachability
failures. Dynamic ports should be a dedicated authority-plumbing change, not part
of the race repair.

### Scenario B: explicit local persistence and reset

This scenario combines A for tests with a local-development-only named Keycloak
data volume and a documented reset workflow. It is a useful operator experience
improvement, but it does not remediate the fixture race and must not be enabled
for closed-box authentication tests.

`WithLifetime(ContainerLifetime.Persistent)` reuses a compatible container. It
does not declare durable `/opt/keycloak/data` storage. `WithDataVolume()` makes
the persistence boundary explicit and survives container replacement. Both
retain an existing realm, and startup realm import skips an existing realm rather
than reconciling it. The reset workflow must therefore be part of the feature:

* Keep `AppHost:UsePersistentKeycloakState=false` for tests so each run imports a
	fresh realm into session state
* Add the data volume only when persistent local state is selected
* Document that realm JSON, bootstrap credential, or client changes may require
	stopping AppHost and deleting the Keycloak data volume before restart
* Add a repository maintenance script only after confirming the generated volume
	name across supported container runtimes; require explicit confirmation before
	deletion
* Add AppHost model tests that distinguish session tests, persistent lifetime,
	data-volume annotation, realm import, and the Keycloak health annotation

The volume stores admin credentials and identity data, so stale-state risk is
higher than scenario A. Its benefit is faster, more continuous local developer
state rather than better automated-test reliability.

### Scenario C: idempotent provisioning job

A finite initializer can wait for healthy Keycloak, reconcile realm resources via
the Admin API, exit with code 0, and allow API/Web resources to use
`WaitForCompletion(initializer)`. This is coherent when realm configuration must
be versioned and reconciled against durable state. `WaitForCompletion` belongs on
the finite initializer, not on long-running Keycloak.

The repository has no current provisioning project or `WaitForCompletion` usage.
Implementing this scenario requires a new project boundary, admin-secret delivery,
idempotency and update semantics for clients/roles/users, error classification,
versioning, and tests. It also risks putting local bootstrap policy into a path
that would need a separate deployment design because `WithRealmImport()` itself
is local-development oriented. It is disproportionate for the current fixture
race. Reconsider it only if explicit durable realm reconciliation becomes a
product requirement.

### Scenario D: shared persistent Keycloak across tests

One warm Keycloak instance can reduce suite startup time, but it creates the
largest reliability and stale-state risks. The current Web fixture mutates a
client's redirect URI and web origins, so concurrent or failed suites would leave
state for later suites. Realm imports would be skipped after first creation,
credentials and checked-in realm JSON could drift, and cleanup ownership would
be ambiguous.

This conflicts with repository rules that tests avoid shared mutable state and
that Aspire tests clean up their resources. It also makes test success dependent
on execution order and the health of an external shared fixture. Reject this
scenario for automated tests.

## Staged recommendation

Select A as the immediate repair, with B and E kept as independent stages:

1. Apply scenario A in the shared managed fixture. Preserve session lifetime,
	add a bounded Keycloak Healthy wait, add target-realm OIDC discovery and issuer
	validation, then run the Admin API mutation with bounded transient retries.
	Ensure partial startup disposes both the application and builder.
2. Measure clean and warm startup behavior. Change
	`Dcp:DependencyCheckTimeout` only if traces show Keycloak reaches Healthy after
	the current 60-second dependency deadline. Do not raise it speculatively.
3. Apply the cross-process serialization portion of scenario E before enabling
	concurrent solution-wide real-auth test projects. Keep dynamic Keycloak ports
	deferred until authority and issuer plumbing can be redesigned end to end.
4. Consider scenario B as an optional local-development improvement. Add a named
	data volume and reset workflow only if developers need state to survive
	Keycloak container replacement. Never use that volume in closed-box tests.
5. Defer scenario C until durable, idempotent realm reconciliation is an explicit
	requirement. Reject scenario D for automated tests.

This staging prevents persistence work from obscuring the immediate race and
prevents concurrency work from being mistaken for application readiness.

## Concrete changes

### Immediate test-fixture changes

* Update `test/Shared/Authentication/RealAppHostProcessFactory.cs:49-94` to create
	a bounded cancellation token for managed startup, pass it through build/start
	where supported, and await
	`application.ResourceNotifications.WaitForResourceHealthyAsync("keycloak",
	token)` before the redirect mutation.
* Split `test/Shared/Authentication/RealAppHostProcessFactory.cs:97-145` into a
	target-realm discovery/issuer barrier and the Admin API mutation. Pass the
	cancellation token to every HTTP and JSON operation. Preserve the fixed public
	authority rather than substituting the management endpoint.
* Add bounded retry classification around transient Admin API failures. Include
	the last HTTP result and Aspire resource state in the thrown exception. Do not
	use a fixed startup sleep.
* Extend the exception path at
	`test/Shared/Authentication/RealAppHostProcessFactory.cs:79-91` to dispose an
	application and builder that were created before a readiness or mutation
	failure. The current catch disposes only environment scopes.
* Set `preferredWebBaseUri` from the already resolved Web endpoint when creating
	`AppHostProcessHandle` at
	`test/Shared/Authentication/RealAppHostProcessFactory.cs:75-85`. The handle
	already prioritizes that endpoint at
	`test/Shared/Authentication/AppHostProcessHandle.cs:230-238` and
	`test/Shared/Authentication/AppHostProcessHandle.cs:294-306`, avoiding listener
	scanning for the managed Web path.
* Align `Aspire.Hosting.Testing` from 13.1.2 to 13.2.3 in
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:9-16`.
	Treat this as compatibility cleanup, not the readiness fix.

### Focused tests for the immediate change

* Add a shared helper unit test for retry classification: connection failure,
	404, 429, and 503 retry; 400 and 401 fail immediately; cancellation stops all
	retries.
* Add an AppHost model assertion that Keycloak has a health-check annotation and
	management endpoint in
	`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs:8-51`.
* Preserve and extend the session-lifetime test at
	`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs:53-72`.
* Add one managed closed-box fixture test that starts from a fresh session
	container and proves, in order, Keycloak Healthy, target-realm discovery,
	redirect update, Web challenge, and Keycloak login page.

### Optional local-persistence changes

* Refactor `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:7-39`
	so persistent local mode adds both persistent lifetime and `WithDataVolume()`,
	while test mode retains session lifetime and no data volume.
* Add configuration documentation beside the local run guide and a guarded reset
	script in the repository's existing scripts area. The reset must target only
	this AppHost's Keycloak volume and explain that it deletes realms, users,
	credentials, and sessions.
* Add model tests proving that the volume exists only in persistent local mode.

These persistence changes touch product orchestration and documentation and are
therefore proposals only in this research task.

### Separate concurrency changes

* Add one shared cross-process lease helper under `test/Shared/Authentication`
	and acquire it before either path in
	`test/Shared/Authentication/RealAppHostProcessFactory.cs:13-94` starts AppHost.
	Hold the lease until `AppHostProcessHandle.DisposeAsync()` completes.
* Emit lease owner, wait duration, process ID, and timeout diagnostics. Ensure a
	crashed process releases the operating-system file handle.
* Keep the assembly collection settings at
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestCollection.cs:1-6`,
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs:1-8`,
	and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs:1-8`.
	They still prevent unnecessary concurrency within each real-auth suite.
* Treat dynamic ports as a later vertical change: parameterize the Keycloak host
	endpoint, inject the resulting public authority into API and Web, update direct
	token/Admin calls, validate discovery issuer consistency, and configure browser
	redirects from the same authority. Do not combine this work with scenario A.

## Focused validation experiments

1. Run one managed Web functional fixture against a clean session container.
	Timestamp `StartAsync()` completion, Keycloak Running, Keycloak Healthy,
	target-realm discovery 200, first successful Admin token, redirect update,
	Web Healthy, and login-page confirmation. Repeat at least 20 times after the
	Keycloak image is present. Zero pre-barrier Admin calls should occur.
2. Repeat the same fixture after removing the session container between runs.
	Capture Keycloak import logs and the distribution of time to Healthy and OIDC
	discovery. Use the upper observed range plus a bounded margin to choose the
	fixture deadline.
3. Perform one cold-image run separately. If Keycloak Healthy arrives after 60
	seconds and API/Web report dependency timeout, increase
	`src/TNC.Trading.Platform.AppHost/appsettings.json:8-10` based on evidence. If
	Healthy arrives before 60 seconds, leave the DCP setting unchanged.
4. Inject or simulate transient discovery/Admin responses to prove retries stop
	at the overall deadline and that HTTP 400/401 fail immediately with response
	diagnostics.
5. Force a failure after `BuildAsync()` and after `StartAsync()`. Verify no
	AppHost process, Keycloak container, builder, or environment override leaks.
6. Run API real-auth, Web functional authentication, and Web E2E authentication
	individually. Then start all three concurrently without the lease to reproduce
	or rule out port-8080 contention. Repeat with the lease and verify serial
	ownership and successful cleanup.
7. For optional scenario B, compare a realm/client inventory before and after
	AppHost restart, after container recreation with the volume retained, and
	after the documented volume reset. Verify realm JSON changes are skipped until
	reset, and verify tests never attach the volume.
8. If dynamic ports are later prototyped, assert that the discovery document's
	`issuer`, API JWT authority, Web OIDC authority, browser authorization URL,
	token endpoint, and configured callback URI all use the same randomized public
	origin.

## Evidence and references

### Repository evidence

* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-39`
	selects persistent lifetime by default, session lifetime when configured false,
	imports `./Realms`, fixes host port 8080, and adds no Keycloak data volume.
* `src/TNC.Trading.Platform.AppHost/AppHostCompositionConstants.cs:3-10` fixes the
	application realm authority at `http://localhost:8080`.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:48-72` injects that
	fixed authority into API and Web authentication configuration.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:150-184` makes API
	and Web wait for Keycloak, but those dependencies do not gate direct fixture
	calls made immediately after the distributed application starts.
* `src/TNC.Trading.Platform.AppHost/appsettings.json:8-10` sets the DCP dependency
	check timeout to 60 seconds.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:13-23` and
	`:49-60` disable persistent Keycloak state for both real-auth harness paths.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:69-73` starts the
	managed distributed application and immediately invokes dynamic redirect
	configuration.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:97-145` calls the
	master token endpoint and target-realm Admin API without a resource health wait,
	realm discovery probe, overall timeout, or transient retry policy.
* `test/Shared/Authentication/AppHostProcessHandle.cs:75-123` discovers API
	candidates by scanning new local listeners. `:125-177` provides a stronger Web
	protocol probe that follows the OIDC challenge and confirms login markup.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:13-27`
	already applies a token-level readiness barrier after API readiness.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs:19-67`
	demonstrates a bounded protocol-level retry, although its diagnostics and
	classification can be improved.
* `.github/instructions/aspire-tests.instructions.md:18-58` requires closed-box
	Aspire testing, cleanup, concurrent-test support, port randomization by default,
	and no hard-coded sleeps as the primary readiness strategy.
* `.github/instructions/auth.instructions.md:32-66` requires Aspire-orchestrated
	local Keycloak, a stable local browser port, explicit volume reset when identity
	configuration changes, realm import, and no time-based auth-readiness fix.
* `.github/instructions/tests.instructions.md:58-76` requires deterministic tests
	that avoid shared mutable state and execution-order dependencies.

### External authoritative references

* [Aspire Keycloak integration](https://aspire.dev/integrations/security/keycloak/)
* [Aspire testing overview](https://aspire.dev/testing/overview/)
* [Aspire: Write your first test](https://aspire.dev/testing/write-your-first-test/)
* [Aspire: Persist data with volumes and bind mounts](https://aspire.dev/fundamentals/persist-data-volumes/)
* [Aspire AppHost eventing APIs](https://aspire.dev/app-host/eventing/)
* [Keycloak health checks](https://www.keycloak.org/observability/health)
* [Keycloak realm import and export](https://www.keycloak.org/server/importExport)
* [Keycloak container guidance](https://www.keycloak.org/server/containers)
* [Pinned Aspire Keycloak integration source](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting.Keycloak/KeycloakResourceBuilderExtensions.cs)
* [Pinned Aspire wait APIs](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting/ResourceBuilderExtensions.cs)
* [Pinned Aspire resource notification service](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting/ApplicationModel/ResourceNotificationService.cs)
* [Pinned Aspire container lifetime implementation](https://github.com/microsoft/aspire/blob/0838645cc4f7fc27e46200ac14cbe5c751637593/src/Aspire.Hosting/ContainerResourceBuilderExtensions.cs)

## Remaining gaps

The gaps do not block the staged decision:

* No isolated runtime trace establishes the clean, warm, and cold distributions
	for Keycloak Healthy, realm discovery, Admin API readiness, or the current
	60-second DCP deadline.
* The normal CI command and project-level parallelization policy were not part of
	the supplied evidence. Confirm whether the three real-auth projects are started
	concurrently before treating cross-process serialization as release-blocking.
* The portable cross-process lease location and stale-lock diagnostics need a
	small implementation spike against every supported CI operating system.
* The generated Keycloak data-volume name is not established. It must be measured
	before documenting or scripting a destructive reset.
* Dynamic-port feasibility requires a separate prototype because Keycloak issuer
	generation, browser reachability, API validation, and direct test calls must be
	changed together.

No clarification is required for the immediate scenario A recommendation.
