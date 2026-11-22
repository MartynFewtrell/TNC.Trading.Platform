---
title: Keycloak Testing Plan Validation Gaps Research
description: Evidence and decisions for unresolved Keycloak testing plan validation questions
ms.date: 2026-07-27
ms.topic: reference
---

## Research questions

* Determine whether API integration, Web functional, and Web E2E tests run on one shared runner and filesystem, in separate jobs, or only through a solution command.
* Select the best existing non-unit test project for real filesystem cross-process lease tests and identify exact project-link implications.
* Confirm how the API real-auth fixture starts processes and which behavior-level readiness gates it already enforces.
* Define practical before-and-after fixed-port ownership evidence that does not require the pre-fix suite to pass.

## Findings

### Test execution topology

The repository contains no workflow definitions under `.github/workflows/` and no
workspace task configuration under `.vscode/`. There is therefore no repository
evidence that the API integration, Web functional, and Web E2E projects run in
separate CI jobs or on separate runners.

All three projects are members of `TNC.Trading.Platform.slnx`. The only
repository-owned quality-gate script invokes one `dotnet test
TNC.Trading.Platform.slnx` command and supplies one results directory. This is a
single command on one runner and filesystem, but `dotnet test` may launch a
separate test host for each project and those hosts may overlap. The three
assemblies therefore cannot rely on xUnit collection serialization to protect
fixed port 8080. External CI configuration could still invoke projects in
separate jobs, but none is committed in this repository.

### Cross-process lease test project

The current plan detail proposes
`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/KeycloakPortLeaseTests.cs`.
That location violates the explicit rule that unit tests must not depend on the
file system. A real exclusive lock-file test necessarily uses the file system
and should be an integration test.

Use
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` as the
existing owner of real cross-process lease tests. It is already an integration
project, already exercises unmanaged child processes and real infrastructure,
and already links the shared authentication harness. It does not require a new
project or a browser dependency merely to test test-harness infrastructure.

Place the implementation at
`test/Shared/Authentication/KeycloakPortLease.cs`. Add this compile link to all
three distributed test project files:

```xml
<Compile Include="..\..\Shared\Authentication\KeycloakPortLease.cs" Link="Shared\Authentication\KeycloakPortLease.cs" />
```

Place `KeycloakPortLeaseTests.cs` in the API integration project, preferably
under `Authentication/`. The API integration project compiles the linked helper
and the tests. The Web functional and Web E2E projects compile the same linked
helper for fixture use but do not duplicate its behavior tests. No new
`ProjectReference`, package reference, or solution entry is required. The API
project's existing `Content` entry for `xunit.runner.json` remains unchanged.

The lease test must create a real exclusive lock, prove bounded contention from
a second process rather than only a second stream in the same process, release
the owner, and prove reacquisition. A small child mode in the integration test
assembly or a spawned `dotnet` helper is appropriate. Keep unique lease keys and
cleanup so tests remain deterministic and independent of execution order.

### API readiness scope

The API real-auth fixture uses unmanaged startup. Its wrapper calls the shared
`StartAppHostProcess`, which constructs `ProcessStartInfo` and calls
`Process.Start` for `dotnet run --project ... --launch-profile https`. It does
not retain a `DistributedApplication`, so Aspire
`WaitForResourceHealthyAsync` is unavailable on this path without changing its
startup architecture.

The API path already has three behavior-level gates:

* `WaitForApiBaseUriAsync` discovers a runtime listener only after
  `/health/ready` returns 200 and anonymous `/api/platform/status` returns 401.
* `PlatformAuthenticationIntegrationTestRuntime.WaitForApiReadinessAsync`
  independently waits for `/health/ready` to return 200.
* `RealKeycloakAccessTokenFactory.WaitForTokenEndpointReadinessAsync` obtains a
  non-empty real access token for seeded user `local-viewer` with
  `platform.viewer`, proving the application realm, test client, user,
  credential, and requested scope are usable.

Preserve these API gates. Scope Aspire `WaitForResourceHealthyAsync("keycloak")`
to `StartManagedAppHostProcessAsync`, which is used by the Web functional and
Web E2E fixtures and owns the `DistributedApplication`. Run it after
`application.StartAsync()` and before `ConfigureDynamicWebRedirectUriAsync` or
any direct Keycloak Admin/token request. Retain the later Web OIDC challenge and
login-page behavior probe. This fixes the managed Admin API race without
weakening or unnecessarily redesigning API readiness.

### Before-and-after fixed-port evidence

Do not define the pre-fix baseline as a passing suite. Define it as an
observable contention experiment:

1. Build once, then launch the three project-level `dotnet test --no-build`
   commands concurrently from one PowerShell harness.
2. Redirect each command's stdout and stderr to a separate timestamped artifact
   and retain its process ID, start time, end time, and exit code even when it
   fails.
3. While any command is active, repeatedly capture
   `Get-NetTCPConnection -State Listen -LocalPort 8080`, then resolve
   `OwningProcess` with `Get-Process`. Also record periods with multiple live
   test hosts attempting startup even if only one process can own the port.
4. Capture relevant AppHost/test logs containing bind failures, startup
   failures, or timeouts. Redact secrets and do not require a successful test
   result.

The pre-fix evidence is valid when it records overlapping project execution and
either a port/bind collision or one owner plus competing startup failure. If a
single run does not reproduce the race, the overlapping processes and repeated
attempts remain baseline topology evidence; repeat a bounded number of times
rather than requiring a green or red outcome.

After the fix, run the same concurrent harness. Require all three exit codes to
be zero and capture lease acquisition-request, acquired, released, elapsed-time,
process-ID, and lease-path diagnostics. Port samples should show at most one
owner at a time, and the lease intervals must not overlap. Store both runs under
one quality-evidence directory with `before/` and `after/` subdirectories so
comparison does not depend on the pre-fix suite passing.

## Recommended decisions

* Treat committed execution topology as one solution-level command on one
  runner/filesystem with potentially concurrent per-project test hosts. Keep the
  cross-process lease because neither a single command nor xUnit collections
  serialize assemblies.
* Move real lease behavior tests from the proposed AppHost unit project to
  `TNC.Trading.Platform.Api.IntegrationTests`. Link
  `test/Shared/Authentication/KeycloakPortLease.cs` into API integration, Web
  functional, and Web E2E; keep one test file only in API integration.
* Apply Aspire resource-health waiting only in the managed Web startup method,
  before direct Keycloak calls. Preserve the API unmanaged process startup and
  its existing listener, API readiness, protected-surface, and real-token gates.
* Use a concurrent process harness plus port-owner and lease-event artifacts for
  before/after evidence. Accept expected pre-fix failures as evidence; require
  successful concurrent completion only after the lease is implemented.

## Evidence

* `.github/instructions/tests.instructions.md:49-50` classifies unit tests as
  in-process/in-memory and prohibits real file-system dependencies.
* `.github/instructions/tests.instructions.md:51-52` permits integration tests
  to validate a service boundary with real dependencies.
* `.github/instructions/tests.instructions.md:58-61` requires deterministic,
  order-independent tests and cleanup or unique external state.
* `TNC.Trading.Platform.slnx:15` includes the API integration project.
* `TNC.Trading.Platform.slnx:28-29` includes the Web functional and Web E2E
	projects.
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1:53-60` invokes one
  solution-level `dotnet test` command with one results directory and TRX
  logger.
* `.github/workflows/` is empty, and `.vscode/` does not exist. No committed job,
  runner matrix, or workspace task overrides the solution-level execution
  evidence.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:27-32`
  references AppHost and links both shared authentication process helpers.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:23-28`
  has the same AppHost reference and shared helper links.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:23-28`
  has the same AppHost reference and shared helper links.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:1-4`
  disables collection parallelization only within the API test assembly.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestCollection.cs:3-6`
  also disables parallelization only for its in-assembly collection.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs:5-8`
  and
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs:5-8`
  define separate assembly-local collections.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:28-36`
  fixes Keycloak to host port 8080 and disables endpoint proxying.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:14-45` captures
  baseline processes/ports, builds `ProcessStartInfo`, and starts unmanaged
  `dotnet run` with `Process.Start`.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAppHostProcessFactory.cs:7-10`
  routes the API real-auth fixture to that unmanaged startup method.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:18-24`
  starts the unmanaged host and applies URI, API, and token readiness gates.
* `test/Shared/Authentication/AppHostProcessHandle.cs:76-105` requires both API
  readiness and unauthorized protected-surface behavior before accepting a
  discovered API URI.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTestRuntime.cs:7-35`
  waits up to five minutes for `/health/ready` status 200.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs:22-47`
  waits for a non-empty real access token under a bounded timeout.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:49-73` creates,
  builds, and starts a managed `DistributedApplication`, then immediately enters
  dynamic Keycloak client configuration.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:7-10`
  and
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:7-10`
  route both Web fixtures through the managed startup method.
* `test/Shared/Authentication/AppHostProcessHandle.cs:112-145` retains the later
  Web behavior gate that follows the sign-in redirect and resolves a Keycloak
  login URI.

## Follow-on questions

* Inspect organization-level or external CI configuration if it exists outside
  this repository. It may place project commands on separate machines, in which
  case a machine-local file lease cannot coordinate those machines, although
  separate machines also do not share host port 8080.

## Clarifying questions

None. The repository evidence is sufficient to resolve the plan decisions; only
external CI topology remains unknowable from this workspace.
