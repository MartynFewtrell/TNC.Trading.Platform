<!-- markdownlint-disable-file -->
# Keycloak Testing Plan Surface Research

## Status

Complete. Repository research only; no product or test implementation code was changed.

## Research Questions

* What exact methods and line ranges control real AppHost startup, readiness, cleanup, and API token acquisition?
* Which fixtures and call sites must acquire a portable bounded cross-process lease for fixed Keycloak port 8080?
* What package versions and project references constrain the implementation plan?
* Where should readiness retry classification, failure cleanup, and AppHost model behavior be tested?
* What focused and full validation commands apply?
* Which wiki pages require updates?
* Does the sequence selected by the primary research remain valid, and what dependencies or conflicts affect it?

## Findings

### Primary sequence assessment

The sequence selected in `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md` remains valid:

1. Repair managed readiness and failure cleanup.
2. Measure clean, warm, and cold startup timing.
3. Add cross-process serialization for fixed Keycloak port 8080.
4. Treat explicit local Keycloak data persistence as optional and separate.
5. Align `Aspire.Hosting.Testing` package versions.

Two implementation dependencies refine that sequence:

* Extract the HTTP readiness/retry policy into a testable shared helper before wiring it into the managed factory. Use the same transient/permanent status classification in the API token helper so HTTP 400 and 401 fail immediately rather than being retried for 90 seconds.
* Harden partial-start cleanup in the same first phase. Every additional readiness await increases the interval in which a built or started `DistributedApplication` can leak if initialization fails.

No repository conflict invalidates the sequence. The stable Keycloak issuer and fixed host port are intentional constraints, so dynamic ports remain a later authority/issuer redesign rather than part of this testing plan.

### Exact shared factory surface

`test/Shared/Authentication/RealAppHostProcessFactory.cs` contains these controlling methods:

* Lines 11-12 define the AppHost project path used by the unmanaged process path.
* Lines 14-47, `StartAppHostProcess`, capture existing processes and listeners, set test overrides, run `dotnet run`, and return a shared handle. The process starts at lines 42-43. There is no cross-process lease in this method.
* Lines 49-92, `StartManagedAppHostProcessAsync`, create process-wide environment scopes, create and build the Aspire testing application, start it, mutate the Keycloak Web client, and return the handle.
* Lines 67-73 are the proven readiness race: `CreateAsync`, `BuildAsync`, and `StartAsync` are followed immediately by `ConfigureDynamicWebRedirectUriAsync`; there is no `WaitForResourceHealthyAsync`, realm discovery probe, shared deadline, or cancellation token.
* Lines 84-90 dispose only environment scopes in the catch path. If creation succeeds and build, start, readiness, or client configuration fails, local `applicationBuilder` and `application` values are not disposed.
* Lines 94-145, `ConfigureDynamicWebRedirectUriAsync`, call the master token endpoint, application-realm client query, and client update. Lines 101-115 acquire the admin token, lines 122-130 query the seeded client, and lines 132-143 mutate callback/origin state.
* Lines 147-161 locate the repository root.
* Lines 163-183 implement process-wide environment-variable restoration.

The managed sequence should use one bounded cancellation deadline across `StartAsync`, `application.ResourceNotifications.WaitForResourceHealthyAsync("keycloak", token)`, application-realm discovery and issuer validation, and dynamic redirect configuration. Per-request timeouts may be shorter, but retries must not reset the overall deadline.

### Related handle and readiness surface

`test/Shared/Authentication/AppHostProcessHandle.cs` owns both unmanaged and managed cleanup:

* Lines 8-34 declare process, Aspire application/builder, environment scope, output, and listener state.
* Lines 36-74 construct the handle and start stdout/stderr pumps for unmanaged processes.
* Lines 78-117, `WaitForApiBaseUriAsync`, retry API readiness and require the protected status surface to return 401.
* Lines 119-169, `WaitForWebSignInUriAsync`, retry Web sign-in discovery and require the redirect chain to reach recognizable Keycloak login markup.
* Lines 171-211, `DisposeAsync`, kill the unmanaged process, await output pumps, dispose the Aspire application, dispose the builder, and restore environment scopes. Cleanup order is correct for a completed handle, but lines 188-203 are not independently protected. One disposal failure can prevent later builder and environment cleanup.
* Lines 294-337, `TryResolveKeycloakLoginUriAsync`, provide the final behavior-level Web readiness proof after the earlier factory setup succeeds.
* Lines 364-379 build useful Web timeout diagnostics from process status, launch command, environment overrides, listeners, probe events, and process output.

The handle should continue to own AppHost resources. Lease ownership is clearer in each fixture because the lease must be acquired before either factory path starts and released only after handle disposal completes.

### API token helper

`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs` contains:

* Lines 7-11: fixed client credentials metadata and the fixed application-realm token endpoint.
* Lines 13-21: authenticated request creation.
* Lines 23-52, `WaitForTokenEndpointReadinessAsync`: a 90-second behavior-level retry loop.
* Lines 36-45: all `HttpRequestException`, `TaskCanceledException`, and `InvalidOperationException` values are retried. Because `EnsureSuccessStatusCode` produces `HttpRequestException` for every non-success response, permanent 400/401 configuration failures currently consume the retry deadline.
* Lines 54-67, `RequestTokenAsync`: request and response parsing.
* Lines 69-84: seeded direct-grant request form.

This helper proves more than realm discovery: client presence, direct grants, seeded user credentials, and requested scope. Retain it as the API behavior barrier, but align its retry classification and diagnostics with the new shared policy.

### Fixture and lease call sites

The portable bounded lease should be a new one-type file under `test/Shared/Authentication`, compiled by link into each real-auth test assembly. A lock file opened with exclusive sharing in a stable machine-local temporary path is portable across Windows and Unix. The held stream is the lease; acquisition retries against one bounded deadline and reports lock path, elapsed time, and process identity on timeout. A named Windows mutex alone would not meet the portability requirement.

Every real-Keycloak fixture must acquire the same lease name before starting AppHost and retain it until AppHost cleanup finishes:

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:5-46`
	* Add a lease field near lines 7-9.
	* Acquire it at the start of `InitializeAsync` before environment scopes and `StartAppHostProcess` at lines 16-19.
	* On initialization failure, dispose any process/scopes and then release the lease.
	* In `DisposeAsync` at lines 29-45, dispose the AppHost and environment scopes before releasing the lease.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:5-27`
	* Acquire before `StartAppHostProcessAsync` at line 13.
	* Release after handle disposal at lines 18-26, including the initialization-failure path.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:5-27`
	* Acquire before `StartAppHostProcessAsync` at line 13.
	* Release after handle disposal at lines 18-26, including the initialization-failure path.

The wrappers at API `Authentication/RealAppHostProcessFactory.cs:5-17`, Web functional `Authentication/RealAppHostProcessFactory.cs:5-17`, and Web E2E `Authentication/AppHostProcessFactory.cs:5-17` contain no independent lifecycle and do not need separate lease acquisition.

Assembly-local controls do not solve the collision:

* API real auth collection: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestCollection.cs:3-6`
* Web functional collection: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs:5-8`
* Web E2E collection: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs:5-8`
* API runner-wide collection serialization: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:1-4`

These settings serialize only within one test process. They cannot coordinate three independently launched test assemblies.

### Package versions and project references

All relevant projects target `net10.0`; `global.json:1-6` pins SDK 10.0.200 with `latestFeature` roll-forward.

* API integration project, `TNC.Trading.Platform.Api.IntegrationTests.csproj:9-32`
	* `Aspire.Hosting.Testing` 13.1.2
	* `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3, runner 2.8.2, Coverlet 6.0.4
	* Direct AppHost project reference at line 27
	* Shared handle and factory compile links at lines 31-32
* Web functional project, `TNC.Trading.Platform.Web.FunctionalTests.csproj:9-28`
	* `Aspire.Hosting.Testing` 13.2.3, `Microsoft.Playwright` 1.58.0
	* `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3, runner 2.8.2
	* Direct AppHost project reference at line 23
	* Shared handle and factory compile links at lines 27-28
* Web E2E project, `TNC.Trading.Platform.Web.E2ETests.csproj:9-28`
	* `Aspire.Hosting.Testing` 13.2.3, `Microsoft.Playwright.Xunit` 1.58.0
	* `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3, runner 2.8.2
	* Direct AppHost project reference at line 23
	* Shared handle and factory compile links at lines 27-28
* AppHost unit project, `TNC.Trading.Platform.AppHost.UnitTests.csproj:9-27`
	* `Aspire.Hosting.Testing` 13.2.3
	* `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3, runner 2.8.2, Coverlet 6.0.4
	* Direct AppHost project reference at line 26
* AppHost product project, `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-20`
	* Aspire AppHost SDK 13.2.3, Keycloak integration 13.2.3-preview.1.26217.6, SQL Server integration 13.2.3
	* Internal visibility for AppHost unit tests at lines 16-18

Align API integration `Aspire.Hosting.Testing` to 13.2.3 before depending on 13.2.3 testing APIs. Add the new shared lease and readiness helper compile links to all three distributed projects. If the helpers are tested from AppHost unit tests, link only those pure helper files into that project; do not link the process factory unless its Aspire lifecycle is first made injectable.

### Test locations and seams

Use `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` for fast helper and model coverage. It already owns AppHost model tests and has the matching Aspire testing version.

* Add a dedicated readiness-policy test file for transient connection failures, transient 404 during import, 429, 503, eventual success, issuer mismatch, immediate 400/401 failure, cancellation, and overall deadline exhaustion.
* Test through a scripted `HttpMessageHandler` or narrow injected send delegate. Do not start Docker for retry classification tests and do not use real delays; inject delay/time behavior or use zero-duration attempts.
* Add a dedicated lease test file for exclusive ownership, bounded timeout, release/reacquisition, and cancellation. A true second-process smoke is stronger than two handles in one process for proving cross-process semantics; keep it focused and bounded if introduced.
* Add managed lifecycle cleanup tests only after introducing a narrow injectable lifecycle seam around builder creation/build/start/readiness/configuration. Verify builder disposal after build failure, application then builder disposal after start/readiness/configuration failure, and reverse-order environment restoration. Without that seam, these failure tests would require Docker and would be slow and nondeterministic.
* Extend `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs:7-91` for AppHost model behavior. Existing lines 9-50 verify resource registration and endpoint annotations; lines 52-73 verify session lifetime when persistence is disabled. Add default persistent-lifetime coverage and Keycloak health-check annotation coverage. If optional `WithDataVolume` work is later selected, add separate assertions that the volume exists only in persistent mode and is absent in session test mode.
* Keep one managed Web distributed smoke as the integration proof that `StartAsync` is followed by Keycloak health, realm discovery, client configuration, and login-page readiness. The existing functional/E2E tests already exercise the final login-page behavior, so do not duplicate a broad matrix.

Every new test method must use `MethodName_StateUnderTest_ExpectedResult` and have an XML comment immediately above it that records applicable requirement identifiers, what is verified, expected outcome, and regression significance. Aspire tests must remain closed-box, condition-based, bounded, and responsible for deterministic cleanup.

## Severity-Ordered Implementation Concerns

1. Critical: managed startup leaks Aspire resources after partial failure. `RealAppHostProcessFactory.cs:67-90` keeps builder/application values inside the try and disposes only environment scopes in catch. A failed start, readiness probe, or Keycloak client update can leave processes or containers running and port 8080 occupied.
2. High: managed Web setup calls Keycloak before readiness. `RealAppHostProcessFactory.cs:69-73` starts the application and immediately enters Admin API configuration. The later Web readiness loop at `AppHostProcessHandle.cs:119-169,294-337` cannot run if the Admin call fails first.
3. High: fixed port 8080 is uncoordinated across test processes. The three fixture initialization methods start independent AppHosts, while collection serialization is assembly-local. Concurrent solution test execution can produce nondeterministic binding failures.
4. High: current retry behavior does not distinguish permanent configuration failures. `RealKeycloakAccessTokenFactory.cs:23-52` retries every `HttpRequestException`, including 400 and 401 from `EnsureSuccessStatusCode`. The planned managed readiness helper must not copy this behavior.
5. Medium: cleanup is not exception-complete. `AppHostProcessHandle.cs:171-211` has one outer `try/finally`, but application disposal failure prevents builder and environment-scope cleanup. Fixture initialization methods also need catch cleanup so lease acquisition does not create a new leak path.
6. Medium: test package drift can change Aspire testing behavior. API integration uses 13.1.2 while every other relevant Aspire project uses 13.2.3.
7. Medium: dynamic Web client mutation remains shared mutable identity state. `RealAppHostProcessFactory.cs:122-143` appends redirects and replaces origins without restoring them. Session-scoped Keycloak currently supplies cleanup; warm shared-test reuse must remain deferred until the mutation is removed or reconciled.
8. Low: a lease coordinates participating tests, not external owners. A developer-run AppHost or unrelated process can still own port 8080. Lease timeout and bind-failure diagnostics must identify this distinction rather than suggesting the lease guarantees port availability.

## Recommended Phases

### Phase 1: Testable readiness and cleanup

* Align API integration `Aspire.Hosting.Testing` to 13.2.3.
* Extract a pure shared readiness/retry policy with injected HTTP behavior and one bounded deadline.
* Add fast classification tests in AppHost unit tests.
* Update managed startup to wait for Keycloak resource health, validate application-realm discovery and exact issuer, then configure the dynamic callback.
* Refactor managed startup locals so application and builder are disposed after every failure point. Make handle disposal exception-complete.
* Align API token readiness with the same transient/permanent classification.

### Phase 2: Portable cross-process lease

* Add the shared exclusive file lease.
* Add lease tests, including bounded timeout and reacquisition after disposal.
* Acquire the lease in all three fixture initialization methods before AppHost startup.
* Release it only after AppHost, environment, and browser/client cleanup.
* Run the three projects concurrently to prove serialization while retaining fixed issuer port 8080.

### Phase 3: AppHost model and timing evidence

* Extend AppHost unit model tests for default persistent lifetime, session test lifetime, endpoint/health metadata, and any later conditional volume behavior.
* Capture cold, clean-session, and warm startup timestamps for Start, Healthy, realm discovery, admin token, redirect update, and login-page readiness.
* Change `Dcp:DependencyCheckTimeout` only if measured Healthy transitions exceed its current 60-second value.

### Phase 4: Optional local persistence

* Decide separately whether local identity data must survive container replacement.
* If selected, add `WithDataVolume` only in persistent local mode, preserve volume-free session test mode, add model coverage, and document an explicit destructive reset workflow.
* Do not use the local persistent volume as a shared automated-test optimization.

## Validation Commands

Run from the repository root. Docker must be available for distributed commands; Playwright prerequisites must be installed for E2E validation.

Fast AppHost and helper tests:

```powershell
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj
```

Focused real-auth projects, individually:

```powershell
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter FullyQualifiedName~PlatformAuthenticationIntegrationTests
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter FullyQualifiedName~Authentication
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter FullyQualifiedName~PlatformDashboardAuthenticationE2ETests
```

Explicit concurrent lease proof after a successful build:

```powershell
dotnet build TNC.Trading.Platform.slnx
$projects = @(
	'test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj',
	'test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj',
	'test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj'
)
$jobs = $projects | ForEach-Object {
	Start-Job -ArgumentList $_ -ScriptBlock {
		param($project)
		dotnet test $project --no-build
		[pscustomobject]@{ Project = $project; ExitCode = $LASTEXITCODE }
	}
}
$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
$results | Format-Table -AutoSize
$failedResults = $results | Where-Object {
	$_.PSObject.Properties.Name -contains 'ExitCode' -and $_.ExitCode -ne 0
}
if ($failedResults) { throw 'At least one concurrent real-auth test project failed.' }
```

Full repository gate:

```powershell
.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId keycloak-testing-hardening
```

Documented serialized fallback for the intermittent MSBuild child-node failure, not as a substitute for concurrent lease proof:

```powershell
dotnet test TNC.Trading.Platform.slnx -m:1
```

For the research document itself, editor diagnostics are the available focused check because `.copilot-tracking` research files opt out of markdownlint with `<!-- markdownlint-disable-file -->`.

## Wiki Updates

Two wiki pages require updates before a numbered implementation plan is complete:

* `docs/wiki/testing-and-quality.md:24-36,274-288`
	* Document layered Keycloak readiness, shared bounded retry classification, exception-complete cleanup, and cross-process serialization across the three real-auth assemblies.
	* Clarify that xUnit collection reuse remains per assembly and the portable lease protects fixed port 8080 across participating test processes.
	* Add focused and concurrent validation commands where the running-tests section currently gives only broad solution commands.
* `docs/wiki/local-development.md:25,159-172,264-270`
	* Explain that real-auth suites wait for Keycloak health and application-realm discovery before protocol calls.
	* Add lease-timeout troubleshooting, including the distinction between another test process and an external developer AppHost holding port 8080.
	* Preserve session-scoped test state and existing stale-realm reset guidance.

`docs/wiki/architecture.md:130-146,269-271` needs an update only if the implementation changes runtime composition, adds conditional data-volume persistence, or promotes the lease/readiness mechanism into an architectural runtime statement. Readiness-only test harness changes do not require an architecture-page edit.

`docs/wiki/runtime-behavior.md` and `docs/wiki/api-reference.md` do not require changes because the plan changes test orchestration rather than product runtime or public API contracts.

## Unresolved Questions

* What acquisition deadline should the cross-process lease use? It must exceed the longest measured cold real-auth project runtime, not only Keycloak startup time.
* Should the lease key be repository-specific plus port-specific, or only port-specific? Repository plus port avoids unrelated repositories blocking each other but cannot coordinate another checkout using the same fixed port; port-only coordinates correctly but has broader machine scope.
* Does the imported wildcard callback configuration already support randomized Web callbacks without Admin API mutation? The primary research recommends proving this before any warm shared-test reuse, but it is not a dependency for readiness and lease hardening.
* Will CI invoke the three projects in separate jobs or containers? A machine-local file lease coordinates processes sharing a filesystem; it does not coordinate separate agents or containers, which also normally do not share the host network namespace.
* Which requirement identifiers should new helper tests trace to? Existing AppHost model tests use FR1, FR2, NF2, and TR1, but the implementation plan should name the governing work package before test comments are authored.

## References

* `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md`
* `.github/copilot-instructions.md`
* `.github/instructions/aspire-tests.instructions.md`
* `.github/instructions/tests.instructions.md`
* `.github/instructions/playwright.instructions.md`
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-42`
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:11-183`
* `test/Shared/Authentication/AppHostProcessHandle.cs:8-430`