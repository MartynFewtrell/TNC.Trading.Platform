<!-- markdownlint-disable-file -->
# Trailing Stops Build and Test Failure Research

## Status

Complete. Research-only investigation completed on 2026-08-30. No production
or test files were modified.

## Research Scope

* Identify the exact tests that time out or appear to run indefinitely after the
	trailing-stops implementation.
* Inventory explicit waits, timeout budgets, collection fixtures, and shared
	AppHost behavior in the affected test paths.
* Determine whether the verified API startup dependency-injection failure causes
	API or Web service-discovery waits and distinguish that root failure from test
	cascades.
* Classify the complete available failure inventory using existing reports,
	logs, artifacts, and static test-path evidence before considering any fresh
	command.
* Record workspace-relative paths with 1-based line ranges, prioritized remedies,
	and externally bounded validation commands without changing product or test
	code.

## Constraints

* Do not modify production or test files.
* Prefer retained evidence and static analysis.
* Bound every fresh executable command externally to at most 60 seconds.
* Clean up application processes and session-created containers after any fresh
	runtime check.

## Repository State and Implementation Footprint

The two prerequisite reports establish the relevant implementation state:

* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-static-integration-audit-research.md:21-47`
	records 18 tracked modifications and 40 untracked files in the uncommitted
	trailing-stops implementation.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:89-121`
	proves that the API reaches endpoint construction and exits because four new
	route services are absent from the explicit application registration list.
* The startup report also records that AppHost and DCP remain active, Web has no
	usable listener because it waits for API, and the API process has exited at
	`.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:60-77`.

## Commands and Results

No fresh test command was run. Existing runtime and focused test evidence was
sufficient, and every implicated fixture has an internal budget longer than the
permitted 60-second external limit.

Read-only commands in this resumed investigation were externally capped at 60
seconds:

* `rg --files -uu` could not run because `rg` is not installed in the active
	PowerShell environment. This is a tooling limitation, not a repository
	failure.
* A PowerShell recursive inventory found historical TRX files and June Web
	functional-test hang dumps, but no August 30 TRX or log for the failing API,
	Web, or trailing-stops runs.
* A date- and project-scoped artifact search returned no August 30 test result.
* The current Copilot debug log contains only this resumed session and does not
	contain the earlier test output.
* Process inspection found no `testhost`, AppHost, API, Web, or DCP process. The
	surviving `vstest.console` process is a child of VS Code's
	`Microsoft.VisualStudio.Code.ServiceHost` and has no `testhost` child. It is
	the editor's idle test service, not a test still executing.

Existing executable evidence remains authoritative:

* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-static-integration-audit-research.md:263-273`
	records a successful API build and 40 passing focused new tests.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:79-105`
	records direct API exit code 1 with ASP.NET Core's `Body was inferred`
	exception.
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-failure-evidence.md:22-51`
	records three isolated, identical failures of the account-details automatic
	capture test, including one fully qualified CLI-filtered run that failed in
	8 ms.

## Failure Inventory and Classification

### Verified root runtime failure

The immediate trailing-stops failure is API startup, not compilation. The three
new route delegates require four unregistered application services at
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-60` and
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:130-146`.
The explicit registration list omits all four at
`src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:18-46`.
`Program.cs` builds the host, initializes SQL, maps the route graph, and fails
during endpoint construction at
`src/TNC.Trading.Platform.Api/Program.cs:13-21` and
`src/TNC.Trading.Platform.Api/Program.cs:64-84`.

Classification: deterministic production composition defect. Confidence: very
high, based on direct reproduction and matching source.

### Verified 15-failure API cascade

The earlier run reported exactly 15 failures, including an API base-URL
discovery timeout. No durable TRX retained the names. Static reconstruction is
nevertheless strong because the only real-auth class using that timeout has
exactly 15 facts. All are constructed with the same
`RealAuthenticationIntegrationTestFixture` at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:6-17`.

The reconstructed members are:

```text
PlatformAuthenticationIntegrationTests.cs:29    HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously
PlatformAuthenticationIntegrationTests.cs:46    StatusEndpoint_ShouldReturnUnauthorized_WhenAnonymousCallerRequestsProtectedSurface
PlatformAuthenticationIntegrationTests.cs:61    StatusEndpoint_ShouldReturnOk_WhenViewerBearerTokenIsProvided
PlatformAuthenticationIntegrationTests.cs:82    ConfigurationEndpoint_ShouldReturnForbidden_WhenViewerTokenLacksOperatorRole
PlatformAuthenticationIntegrationTests.cs:102   AuthAdministrationEndpoint_ShouldReturnOk_WhenAdministratorTokenIsProvided
PlatformAuthenticationIntegrationTests.cs:122   AuthAuditEndpoint_ShouldPersistSignOutEvent_WhenAuthenticatedCallerPostsAuditRecord
PlatformAuthenticationIntegrationTests.cs:161   StatusEndpoint_ShouldReturnUnauthorized_WhenTokenSignatureIsInvalid
PlatformAuthenticationIntegrationTests.cs:183   StatusEndpoint_ShouldReturnForbidden_WhenAuthenticatedCallerHasNoPlatformRole
PlatformAuthenticationIntegrationTests.cs:203   ConfigurationEndpoint_ShouldReturnOk_WhenOperatorUpdatesConfiguration
PlatformAuthenticationIntegrationTests.cs:225   ManualRetryEndpoint_ShouldReturnConflict_WhenOperatorTokenIsProvidedAndManualRetryIsUnavailable
PlatformAuthenticationIntegrationTests.cs:245   EventsEndpoint_ShouldReturnOk_WhenViewerTokenIsProvided
PlatformAuthenticationIntegrationTests.cs:265   AuthAdministrationEndpoint_ShouldReturnForbidden_WhenOperatorTokenLacksAdministratorRole
PlatformAuthenticationIntegrationTests.cs:285   AuthAuditEndpoint_ShouldPersistSignInEvent_WhenAuthenticatedCallerPostsAuditRecord
PlatformAuthenticationIntegrationTests.cs:325   AuthAuditEndpoint_ShouldPersistAccessDeniedEvent_WhenAuthenticatedCallerPostsAuditRecord
PlatformAuthenticationIntegrationTests.cs:365   AuthAuditEndpoint_ShouldPersistTokenAcquisitionFailedEvent_WhenAuthenticatedCallerPostsAuditRecord
```

The fixture starts AppHost once for the class and waits up to 120 seconds for an
API listener before any test body runs at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:13-31`.
The shared process helper repeatedly probes `/health/ready` and the protected
status route, using five-second request budgets inside the 120-second outer
budget, then throws the reported URL-discovery exception at
`test/Shared/Authentication/AppHostProcessHandle.cs:76-120`.

Classification: 15 xUnit failures caused by one fixture initialization failure;
the individual test assertions do not run. Confidence that this is the observed
15-member group: high, but not absolute because the original TRX is missing.

### Verified independent unit-test failure

`CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure` fails
independently at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:96-120`.
The fixture hard-codes trading day 2026-08-07 but injects
`TimeProvider.System` at the same file's lines 145-163. On 2026-08-30 the
handler calculates 2026-08-30, reaches the gateway, and returns `Saved` instead
of expected `Deferred`.

Classification: deterministic test date-drift defect, pre-existing and unrelated
to trailing stops. Confidence: very high, based on three isolated runs.

### Secondary and latent failures that are not roots

* The retention service's `TaskCanceledException` follows host failure and
	shutdown. It is cancellation noise, not the startup cause, as recorded at
	`.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:79-105`.
* Web startup is downstream. AppHost declares Web `.WaitFor(api)` at
	`src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:28-41`.
	When API exits, Web never exposes a usable sign-in listener.
* The duplicate and undiscoverable trailing-stops migration is a verified schema
	defect, but it did not initiate the reproduced startup failure because the
	API passed `MigrateAsync`. The classification and duplicate-object proof are
	at
	`.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:123-170`.
* Package vulnerability warnings are non-fatal. Both prior reports record
	successful builds.

## Root-Cause Analysis

### Why the tests appear to run forever

No implicated AppHost test body contains the initiating hang. Long fixture
budgets and cross-project lease contention hide the fast API crash:

* The real API fixture acquires the global Keycloak lease and can spend 120
	seconds in listener discovery before cleanup at
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:13-31`.
* The synthetic API collection fixture starts AppHost and then uses a five-minute
	readiness loop at
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:18-37` and
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTestRuntime.cs:5-37`.
	Its 11 tests at
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationSyntheticTokenIntegrationTests.cs:8-366`
	cannot begin while collection-fixture initialization is pending.
* Web functional and E2E fixtures start a managed AppHost, wait only for
	Keycloak health, obtain the Web endpoint, and then probe sign-in for up to 120
	seconds at `test/Shared/Authentication/RealAppHostProcessFactory.cs:45-99`,
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:5-17`, and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:5-17`.
	Their six functional facts and one E2E fact are downstream risk, but no
	retained August 30 result proves they were in the observed 15 failures.
* Every real or synthetic AppHost fixture acquires the same file lease for fixed
	Keycloak port 8080. Acquisition polls for up to five minutes at
	`test/Shared/Authentication/KeycloakPortLease.cs:5-54`. Separate test projects
	can run concurrently, so one failing fixture can hold the lease during its
	readiness timeout while other projects wait. Their lease timeouts are
	cascades, not independent Keycloak failures.

The API failure therefore causes service-discovery waits in two forms: direct
API readiness or listener probing, and indirect Web listener probing because
Web waits for API. The tests currently wait for timeout instead of observing
the terminal API resource state and failing immediately with API diagnostics.

### SQL fixture distinction

The SQL integration collection is shared and nonparallel at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:124-127`.
Its initialization, reset, disposal, Docker subprocess reads, SQL connection,
and database drop have no explicit timeout or cancellation at the same file's
lines 13-48 and 62-120. The three new account-preferences SQL tests share this
fixture at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:10-65`.

This is a genuine unbounded-wait risk if Docker or SQL stalls. It is not a
verified August 30 hang: Docker and SQL were responsive during direct startup
research, no isolated SQL result was retained, and the accidental
`SnapshotRepair` migration currently lets the feature migration assertion pass.

## Candidate Remedies and Validation

### Prioritized remedies

1. Register `GetAccountPreferencesHandler`,
	`UpdateAccountPreferencesValidator`, `UpdateAccountPreferencesHandler`, and
	`GetTrailingStopsPreferenceObservationsHandler` in the API application
	composition root. Add one route-graph or service-resolution test that builds
	the real registration set. This removes the startup root.
2. Make AppHost fixtures fail fast when API reaches a terminal failed or exited
	resource state. Include captured API resource logs or exit diagnostics in the
	fixture exception instead of waiting 120 or 300 seconds for discovery.
3. Use one collection fixture per AppHost-backed project consistently and avoid
	the real API class fixture plus separate collection serialization split. Keep
	cleanup in `finally` or fixture disposal and retain random ports except where
	the fixed Keycloak contract is unavoidable.
4. Give Keycloak lease acquisition a cancellation token derived from the test's
	external budget and report the current owner metadata. Do not let multiple
	project fixtures consume consecutive five-minute budgets in broad runs.
5. Add cancellation and command timeouts to every Docker and SQL operation in
	`SqlServerDatabaseFixture`, including connection open, reset, and drop. This
	repairs the separate unbounded-wait risk without conflating it with API DI.
6. Repair the account-details test with a fixed `TimeProvider` and align its fake
	store's automatic-trigger predicate with production. This is independent of
	trailing-stops startup.
7. Reconcile the migration history only after deciding whether
	`20260830112227_SnapshotRepair` exists outside disposable local state. Do not
	make the duplicate named migration discoverable against the current schema.

### Bounded validation commands

Use prebuilt outputs so the test budget measures runtime rather than restore or
compilation. PowerShell's process timeout is the external authority; the
`--blame-hang-timeout` is an inner diagnostic bound.

```powershell
$project = 'test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj'
$filter = 'FullyQualifiedName=TNC.Trading.Platform.Application.UnitTests.Features.AccountDetails.AccountDetailsHandlerTests.CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure'
$process = Start-Process dotnet -ArgumentList @('test', $project, '--no-build', '--no-restore', '--filter', $filter, '--blame-hang', '--blame-hang-timeout', '45s') -PassThru -NoNewWindow
if (-not $process.WaitForExit(55000)) { taskkill /PID $process.Id /T /F | Out-Null; throw 'Bounded test exceeded 55 seconds.' }
if ($process.ExitCode -ne 0) { throw "Bounded test failed with exit code $($process.ExitCode)." }
```

After the API composition fix, run one smoke fact, not the whole integration
project. Record platform PIDs and container IDs before the run; remove only
resources created by that run in `finally`.

```powershell
$beforePids = @(Get-CimInstance Win32_Process | Where-Object CommandLine -Match 'TNC.Trading.Platform.(Api|Web|AppHost)|aspire-dcp' | ForEach-Object ProcessId)
$beforeContainers = @(docker ps -aq)
$project = 'test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj'
$filter = 'FullyQualifiedName=TNC.Trading.Platform.Api.IntegrationTests.Authentication.PlatformAuthenticationIntegrationTests.HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously'
try {
	 $process = Start-Process dotnet -ArgumentList @('test', $project, '--no-build', '--no-restore', '--filter', $filter, '--blame-hang', '--blame-hang-timeout', '45s') -PassThru -NoNewWindow
	 if (-not $process.WaitForExit(55000)) { taskkill /PID $process.Id /T /F | Out-Null; throw 'Bounded smoke test exceeded 55 seconds.' }
	 if ($process.ExitCode -ne 0) { throw "Bounded smoke test failed with exit code $($process.ExitCode)." }
}
finally {
	 Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -notin $beforePids -and $_.CommandLine -match 'TNC.Trading.Platform.(Api|Web|AppHost)|aspire-dcp' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
	 $newContainers = @(docker ps -aq | Where-Object { $_ -notin $beforeContainers })
	 if ($newContainers.Count -gt 0) { docker rm -f $newContainers | Out-Null }
}
```

Then apply the same wrapper to one synthetic-token fact and one Web functional
fact. Do not run all AppHost-backed projects together until each fixture fails
or succeeds within the 55-second smoke budget.

## Remaining Gaps and Clarifying Questions

* The original 15-failure TRX and console output were not retained. The exact
	member list is a high-confidence reconstruction from the unique 15-fact class
	behind the reported timeout, not a parsed result file.
* No fresh AppHost test was started because its current internal timeout exceeds
	the requested external limit and direct API evidence already proves the root.
* The 11 synthetic API, six functional Web, and one E2E Web tests are proven to
	depend on the failed startup graph, but this session did not claim they were
	members of the observed 15 failures.
* SQL fixture operations are structurally unbounded, but no retained evidence
	identifies a specific SQL test that hung on August 30.
* It remains unknown whether `SnapshotRepair` reached a non-disposable database.
	That answer controls migration remediation, not the API fixture diagnosis.

## Recommended Next Research Not Completed

* [ ] Capture a TRX from the single bounded real-auth smoke test after API DI is
	repaired
* [ ] Capture Aspire resource-state transitions and API child logs in the
	fixture's fail-fast path
* [ ] Run one externally bounded SQL migration fact after SQL fixture operations
	receive internal cancellation
* [ ] Confirm whether `SnapshotRepair` exists in any shared or deployed database

## Clarifying Questions

* Has `20260830112227_SnapshotRepair` been applied outside disposable local
	`platformdb` state?
* Should CI continue running AppHost-backed test projects concurrently while a
	fixed Keycloak 8080 lease serializes them, or should orchestration assign each
	project an isolated Keycloak endpoint?