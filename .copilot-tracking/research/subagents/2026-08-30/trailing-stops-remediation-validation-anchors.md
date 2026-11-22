---
title: Trailing-Stops Remediation Validation Anchors
description: Verified repository anchors and watchdog commands resolving Plan Validator discrepancy DR-07
ms.date: 2026-08-30
ms.topic: reference
---

## Research questions

1. Which exact SQL fixture and helper files and line ranges own Docker startup, SQL connection, Respawn/reset, migration command timeout, and disposal?
2. What exact canonical synthetic-token API integration smoke fact should validate the API fixture?
3. What exact canonical AppHost-backed Web functional or E2E smoke fact should validate the Web fixture?
4. Which existing files and line ranges implement API readiness probing and immediate process-exit detection?
5. Which PowerShell watchdog invocation and artifact commands fit this repository while retaining TRX, stdout, and stderr and killing the complete process tree before 60 seconds?

## Status

Complete. All DR-07 validation-anchor questions are answered from the current
workspace. No product, test, configuration, or wiki file was modified.

## Findings

### SQL fixture and helper ownership

The account-preferences SQL facts use the `SQL Server` xUnit collection and
`SqlServerDatabaseFixture`. No Testcontainers or Respawn package is referenced
by the Infrastructure IntegrationTests project. The fixture discovers an
already-running Aspire SQL Server container and creates one disposable database
inside it.

Exact anchors:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:13-26` discovers the running SQL container through Docker, inspects its password, and resolves its mapped port
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:28-49` builds master/database connection strings, opens the master connection, and creates the disposable database
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:52-60` creates every `PlatformDbContext`; this is the shared insertion point for `EnableRetryOnFailure` or a finite EF command timeout
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:64-85` owns reset through an explicit `DROP TABLE IF EXISTS` SQL batch; there is no Respawn checkpoint or reset helper
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:87-99` owns database disposal through master connection open, single-user rollback, and `DROP DATABASE`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:101-121` owns Docker process startup, redirected output, exit waiting, and Docker failure diagnostics
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:18-23`, `:33-36`, and `:47-50` are the three current account-preferences migration call sites
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:9-18` confirms that the project has SQL Client and EF SQL Server packages but no Respawn or Testcontainers dependency

Current timeout gaps are concrete: `OpenAsync`, `ExecuteNonQueryAsync`, Docker
stdout/stderr reads, `WaitForExitAsync`, and all three `MigrateAsync` calls use no
caller cancellation token. Commands also retain provider defaults because
neither `SqlCommand.CommandTimeout` nor EF `SetCommandTimeout` is set. The plan
should not say this fixture starts a Docker container or uses Respawn.

### Canonical synthetic-token API smoke fact

Project:

`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`

Fully-qualified fact:

`TNC.Trading.Platform.Api.IntegrationTests.Authentication.PlatformAuthenticationSyntheticTokenIntegrationTests.IgLoginHistoryEndpoint_ShouldReturnOkWithSecretSafeResponse_WhenViewerTokenIsProvided`

Evidence:

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationSyntheticTokenIntegrationTests.cs:352-384` sends a valid synthetic viewer token, requires HTTP 200, validates the response shape, and rejects credential fields
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/AuthenticationIntegrationTestCollection.cs:3-5` binds the class to `SyntheticTokenIntegrationTestFixture`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:19-33` starts the Aspire application and waits for API readiness once for the collection

This is preferable to a negative 401 fact because it proves that the API is
ready, synthetic token validation succeeds, authorization succeeds, routing is
active, and a protected response remains secret-safe. It does not call real IG.

### Canonical AppHost-backed Web smoke fact

Project:

`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`

Fully-qualified fact:

`TNC.Trading.Platform.Web.FunctionalTests.Authentication.PlatformAuthenticationFunctionalTests.RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry`

Evidence:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs:14-34` requests the real runtime root and verifies the expected sign-in redirect
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:14-25` starts the managed AppHost and resolves the Web base URI for the collection
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:7-17` delegates startup to the shared managed-AppHost factory and waits for a real Keycloak sign-in URI
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:21-29` references AppHost and links the shared process/factory helpers

This is the smallest existing AppHost-plus-Keycloak-backed Web fact. It avoids
interactive browser automation, seeded sign-in, and real IG access.

### API readiness and process-exit patterns

Existing readiness patterns:

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:24-32` creates the API client after AppHost startup and delegates readiness
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTestRuntime.cs:7-35` polls `/health/ready`, accepts only HTTP 200, suppresses transient transport failures, and delays 500 ms between attempts
* `test/Shared/Authentication/AppHostProcessHandle.cs:75-120` is the stronger shared direct-process API probe: it uses a five-second per-request timeout, requires `/health/ready` HTTP 200, and confirms `/api/platform/status` returns HTTP 401

Existing process-exit and diagnostic patterns:

* `test/Shared/Authentication/AppHostProcessHandle.cs:61-70` starts bounded stdout and stderr pumps for a directly launched process
* `test/Shared/Authentication/AppHostProcessHandle.cs:178-199` checks `HasExited`, kills the complete process tree, waits for exit, and drains both output pumps during disposal
* `test/Shared/Authentication/AppHostProcessHandle.cs:387-409` converts `HasExited` and `ExitCode` into diagnostic process status
* `test/Shared/Authentication/AppHostProcessHandle.cs:421-447` retains bounded timestamped process/probe output queues

There is no existing immediate startup-exit race. Repository search found no
`Task.WhenAny` combining a readiness operation with `WaitForExitAsync`, and no
probe-loop `HasExited` check. The current direct-process status is reported only
after timeout. Managed startup from
`test/Shared/Authentication/RealAppHostProcessFactory.cs:45-84` returns a handle
with `process: null`, so managed API failure must be detected from Aspire
resource notifications/state rather than an OS `Process`. DR-07 should require
implementation to compose these existing patterns, not claim immediate exit
detection already exists.

### Repository-compatible watchdog and retained artifacts

The repository pins .NET SDK 10.0.200 in `global.json:1-6` and already writes
quality-gate evidence under `artifacts/quality-gates/<gate>/<timestamp>/` in
`.github/scripts/Invoke-CleanArchitectureQualityGate.ps1:10-18`. The following
PowerShell uses a 50-second wait, leaving ten seconds for `taskkill.exe` and
PowerShell cleanup before the required 60-second ceiling. It retains full
stdout, stderr, TRX, blame-hang attachments, and a timeout marker.

```powershell
$gateRoot = Join-Path $PWD 'artifacts/quality-gates/trailing-stops-remediation'

function Invoke-TrailingStopsWatchdog {
	param(
		[Parameter(Mandatory)] [string] $Name,
		[Parameter(Mandatory)] [string[]] $DotNetArguments
	)

	$artifactRoot = Join-Path $gateRoot $Name
	$trxRoot = Join-Path $artifactRoot 'trx'
	New-Item -ItemType Directory -Path $trxRoot -Force | Out-Null
	$stdoutPath = Join-Path $artifactRoot 'stdout.log'
	$stderrPath = Join-Path $artifactRoot 'stderr.log'
	$timeoutPath = Join-Path $artifactRoot 'timeout.txt'

	$process = Start-Process -FilePath 'dotnet' -ArgumentList $DotNetArguments `
		-PassThru -NoNewWindow -RedirectStandardOutput $stdoutPath `
		-RedirectStandardError $stderrPath

	if (-not $process.WaitForExit(50000)) {
		"Timed out at $(Get-Date -Format o); PID=$($process.Id)" |
			Set-Content -Path $timeoutPath
		& taskkill.exe /PID $process.Id /T /F | Add-Content -Path $stderrPath
		throw "Gate '$Name' exceeded 50 seconds. Evidence: $artifactRoot"
	}

	$process.Refresh()
	if ($process.ExitCode -ne 0) {
		throw "Gate '$Name' failed with exit code $($process.ExitCode). Evidence: $artifactRoot"
	}
}
```

Run the canonical API smoke after its project has built successfully:

```powershell
$apiProject = 'test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj'
$apiFact = 'FullyQualifiedName=TNC.Trading.Platform.Api.IntegrationTests.Authentication.PlatformAuthenticationSyntheticTokenIntegrationTests.IgLoginHistoryEndpoint_ShouldReturnOkWithSecretSafeResponse_WhenViewerTokenIsProvided'
$apiArtifacts = Join-Path $gateRoot 'api-synthetic-smoke/trx'
Invoke-TrailingStopsWatchdog 'api-synthetic-smoke' @(
	'test', $apiProject, '--no-restore', '--no-build', '--filter', $apiFact,
	'--blame-hang', '--blame-hang-timeout', '45s',
	'--results-directory', $apiArtifacts,
	'--logger', 'trx;LogFileName=api-synthetic-smoke.trx'
)
```

Run the canonical Web smoke after its project has built successfully:

```powershell
$webProject = 'test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj'
$webFact = 'FullyQualifiedName=TNC.Trading.Platform.Web.FunctionalTests.Authentication.PlatformAuthenticationFunctionalTests.RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry'
$webArtifacts = Join-Path $gateRoot 'web-apphost-smoke/trx'
Invoke-TrailingStopsWatchdog 'web-apphost-smoke' @(
	'test', $webProject, '--no-restore', '--no-build', '--filter', $webFact,
	'--blame-hang', '--blame-hang-timeout', '45s',
	'--results-directory', $webArtifacts,
	'--logger', 'trx;LogFileName=web-apphost-smoke.trx'
)
```

Retained paths:

* `artifacts/quality-gates/trailing-stops-remediation/api-synthetic-smoke/stdout.log`
* `artifacts/quality-gates/trailing-stops-remediation/api-synthetic-smoke/stderr.log`
* `artifacts/quality-gates/trailing-stops-remediation/api-synthetic-smoke/timeout.txt` when timed out
* `artifacts/quality-gates/trailing-stops-remediation/api-synthetic-smoke/trx/api-synthetic-smoke.trx`
* `artifacts/quality-gates/trailing-stops-remediation/web-apphost-smoke/stdout.log`
* `artifacts/quality-gates/trailing-stops-remediation/web-apphost-smoke/stderr.log`
* `artifacts/quality-gates/trailing-stops-remediation/web-apphost-smoke/timeout.txt` when timed out
* `artifacts/quality-gates/trailing-stops-remediation/web-apphost-smoke/trx/web-apphost-smoke.trx`

VSTest places blame-hang attachments beneath the corresponding `trx` results
directory. On timeout, `taskkill.exe /T /F` terminates the captured `dotnet`
process and all descendants, including testhost and AppHost children. The logs
are intentionally not removed in success, failure, or timeout paths.

## Recommended plan and details edits

Apply these textual corrections to the remediation plan/details before
implementation:

1. Replace Phase 2 Step 2.2's directory-only file entry with
	 `SqlServerDatabaseFixture.cs:13-121` and
	 `AccountPreferencesSqlIntegrationTests.cs:18-50`. State that the fixture
	 discovers an already-running Aspire SQL container, creates/drops a disposable
	 database, and resets via explicit SQL. Remove references that imply
	 Testcontainers startup or Respawn ownership.
2. Require one linked sub-60-second token through Docker process wait/output,
	 SQL open/execute, reset, migration, and disposal. Require finite
	 `SqlCommand.CommandTimeout` and EF migration command timeout at the shared
	 `CreateDbContext` boundary, with each current `MigrateAsync` call receiving
	 the fixture token.
3. Name the API smoke project and fully-qualified fact exactly as recorded in
	 the canonical synthetic-token section. Replace the older real-authentication
	 health fact filter from the prior research because it does not validate the
	 synthetic fixture requested by Phase 2 Step 2.4.
4. Name the Web FunctionalTests project and fully-qualified root-route fact
	 exactly as recorded in the canonical Web section. State that its collection
	 fixture uses `StartManagedAppHostProcessAsync` and real Keycloak readiness.
5. In Phase 2 Step 2.1, cite
	 `PlatformAuthenticationIntegrationTestRuntime.cs:7-35` for the current API
	 readiness loop and `test/Shared/Authentication/AppHostProcessHandle.cs:75-120`
	 for the stronger readiness-plus-protected-surface probe. Record the present
	 gap: no loop fails immediately on process/resource exit.
6. Split exit handling by launch mode. Direct-process paths should race or check
	 `Process.WaitForExitAsync`/`HasExited` and include buffered stdout/stderr.
	 Aspire-managed paths should observe terminal API resource state through
	 `ResourceNotifications`; they cannot use `AppHostProcessHandle.Process`
	 because it is null.
7. Replace generic watchdog prose in validation steps with the 50-second
	 invocation and retained artifact layout above. Keep `--blame-hang-timeout
	 45s`, unique TRX filenames, and stop-on-first-failure ordering.

These edits resolve DR-07 without changing product, test, configuration, or
wiki files. They also prevent implementation from adding a Respawn dependency
to solve behavior currently owned by a short explicit reset batch.

## Follow-on questions

* During implementation, confirm the exact Aspire 13.2.3 terminal resource
	state names/API before adding managed API exit detection. The repository has
	no existing terminal-state observer to copy.

## Clarifying questions

None.
