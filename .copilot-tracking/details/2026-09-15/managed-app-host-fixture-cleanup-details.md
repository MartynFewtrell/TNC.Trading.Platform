<!-- markdownlint-disable-file -->
# Implementation Details: Managed AppHost Fixture Cleanup

## Context Reference

Sources: user request from 2026-09-15; `.copilot-tracking/research/2026-09-15/aspire-dcp-tunnel-proxies-research.md`; `.copilot-tracking/research/subagents/2026-09-15/managed-app-host-fixture-cleanup-research.md`.

## Implementation Phase 1: Make managed AppHost cleanup failure-resilient

<!-- parallelizable: false -->

### Step 1.1: Create and link a dependency-free teardown helper

Create `test/Shared/Authentication/AppHostCleanup.cs` containing one `internal` static helper type whose method accepts `Func<Task> stopApplication`, `Func<ValueTask> disposeApplication`, and `Func<ValueTask> disposeBuilder`. Invoke all delegates in that order even if an earlier delegate fails. Capture the first exception, attach later exceptions in `firstException.Data["CleanupExceptions"]`, and throw the first exception after all attempts. Link only this dependency-free source file into the API integration and API unit-test projects. Do not link `ManagedAppHostFixture.cs` into the unit-test project because it imports Aspire integration dependencies.

Files:
* `test/Shared/Authentication/AppHostCleanup.cs` - New narrow, delegate-based teardown helper.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Link the shared helper for fixture use.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj` - Link the shared helper for direct unit-test coverage.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Link the shared helper because this project compiles `ManagedAppHostFixture`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Link the shared helper because this project compiles `ManagedAppHostFixture`.

Discrepancy references:
* Addresses DR-01.

Success criteria:
* The helper has no Docker, Aspire, AppHost, or mutable fixture dependencies beyond its explicit delegates.
* The helper follows the exception-reporting behavior of `AppHostProcessHandle.DisposeAsync`.

Context references:
* `test/Shared/Authentication/ManagedAppHostFixture.cs` (lines 66-79) - Existing sequential teardown that short-circuits.
* `test/Shared/Authentication/AppHostProcessHandle.cs` (lines 179-238) - Existing aggregation pattern.

Dependencies:
* None.

### Step 1.2: Route fixture disposal through the helper

In `ManagedAppHostFixture.DisposeAsync`, capture the current application and builder before cleanup. Pass delegates that stop and dispose the captured application and dispose the captured builder. Set `application` and `builder` to `null` in the relevant `finally` blocks, including when a disposal operation fails, so repeated xUnit cleanup is idempotent and cannot reuse a partially released owner.

Files:
* `test/Shared/Authentication/ManagedAppHostFixture.cs` - Replace the sequential disposal implementation.

Discrepancy references:
* Addresses DR-01.

Success criteria:
* Builder disposal is attempted after stop failure and after application disposal failure.
* A normal successful teardown preserves stop, application disposal, then builder disposal ordering.
* A teardown failure remains visible to xUnit after all cleanup attempts.

Context references:
* `test/Shared/Authentication/ManagedAppHostFixture.cs` (lines 10-17, 66-79) - Ownership and disposal path.

Dependencies:
* Step 1.1 completion.

### Step 1.3: Make wrapper teardown failure-resilient

Update the Functional and E2E real-authentication wrapper fixtures so the `ControllableIgProvider` release is attempted in a `finally`-protected cleanup path after delegation to `ManagedAppHostFixture`. Preserve the primary failure and attach subsequent wrapper cleanup failures using the established cleanup aggregation convention. Keep the existing public fixture contracts and collection boundaries unchanged.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` - Release the controlled IG provider after managed teardown failure.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Apply the same teardown guarantee.

Discrepancy references:
* Addresses DR-02.

Success criteria:
* A managed AppHost teardown failure cannot skip controlled provider disposal.
* Wrapper cleanup failures do not mask the first encountered teardown failure.

Context references:
* `.copilot-tracking/research/subagents/2026-09-15/managed-app-host-fixture-cleanup-research.md` (lines 52-53) - Identified wrapper short-circuit risk.

Dependencies:
* Step 1.2 completion.

## Implementation Phase 2: Prove cleanup sequencing

<!-- parallelizable: false -->

### Step 2.1: Add deterministic teardown tests

Create `AppHostCleanupTests.cs` in the API unit-test project root, using namespace `TNC.Trading.Platform.Api.UnitTests`. Exercise the linked internal helper with in-memory delegates and an ordered event list. Use test names in `MethodName_StateUnderTest_ExpectedResult` form and include the repository-required comments documenting requirement traceability, verification, expected result, and operational reason.

Implement four focused cases:
* Stop failure still runs application and builder disposal.
* Application disposal failure still runs builder disposal.
* Multiple failures retain the first exception and attach later failures under `CleanupExceptions`.
* A successful sequence invokes each action once in ownership order.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AppHostCleanupTests.cs` - New deterministic cleanup unit-test coverage.

Discrepancy references:
* Addresses DR-01 and DR-02.

Success criteria:
* Tests have no dependency on Docker, Keycloak, SQL Server, or a running AppHost.
* Tests prove all ownership release delegates are attempted in every failure scenario.

Context references:
* `.copilot-tracking/research/subagents/2026-09-15/managed-app-host-fixture-cleanup-research.md` (lines 76-89) - Testability recommendation.
* `.github/copilot-instructions.md` - Automated-test naming and documentation requirements.

Dependencies:
* Step 1.1 completion.

### Step 2.2: Run focused deterministic validation

Run the new test class in isolation before the Docker-backed lifecycle tests. Investigate failures by correcting helper behavior rather than widening test serialization or adding cleanup commands.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AppHostCleanupTests"` - Runs deterministic teardown unit tests only.

Success criteria:
* The focused suite passes without creating DCP or Docker resources.

Dependencies:
* Step 2.1 completion.

## Implementation Phase 3: Validate normal AppHost lifecycle

<!-- parallelizable: false -->

### Step 3.1: Compare Docker state around a focused closed-box test

With no local AppHost or AppHost-backed test session active, record the current tunnel-proxy container list. Run the focused existing API real-authentication test to normal completion, then inspect the list again. Do not delete containers as part of automated tests or normal validation.

Validation commands:
* `docker ps -a --filter "name=aspire-container-network-tunnelproxy" --format "table {{.ID}}\t{{.Names}}\t{{.Status}}\t{{.CreatedAt}}"` - Captures the pre- and post-test proxy state.
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PlatformAuthenticationIntegrationTests"` - Runs one real AppHost-backed lifecycle.

Success criteria:
* After normal completion, no new tunnel proxy remains whose labels identify an exited creator process.

Dependencies:
* Step 2.2 completion, Docker Desktop, and the supported local Aspire resources.

### Step 3.2: Diagnose only residual normal-completion proxies

For any residual tunnel proxy after the focused test, collect labels and inspect the labelled creator PID. Do not remove a running proxy until its owning AppHost/test session has been confirmed stopped. Record residual normal-completion evidence as an Aspire/DCP follow-up rather than treating it as a fixture test failure that can be hidden by global serialization.

Validation commands:
* `docker inspect <container-id> --format "{{json .Config.Labels}}"` - Retrieves DCP/Aspire owner labels.
* `Get-Process -Id <creator-process-id>` - Checks whether the labelled owner still exists.

Success criteria:
* Any residual is attributable as either active infrastructure or an orphan requiring a documented runtime investigation.

Dependencies:
* Step 3.1 completion.

## Implementation Phase 4: Final validation

<!-- parallelizable: false -->

### Step 4.1: Build and run all affected test projects

Run the solution build and each AppHost-backed test project serially for this diagnostic validation. Serial execution here provides attributable Docker observations; it is not a proposed permanent global serialization policy.

Validation commands:
* `dotnet build TNC.Trading.Platform.slnx --no-restore`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore`

Success criteria:
* Build and all affected test projects pass.
* Post-run Docker inspection meets the normal-lifecycle success criterion.

Dependencies:
* Implementation Phases 1-3 completion.

### Step 4.2: Resolve minor findings and report runtime blockers

Correct isolated implementation or test failures discovered above. When normal teardown succeeds but DCP still leaves an orphaned proxy, preserve labels, creator PID state, timestamps, and test output as a version-specific Aspire/DCP `13.5.3` investigation. Separately perform a deliberate testhost termination experiment to document the expected limitation of abrupt process termination, without asserting xUnit fixture disposal can run in that case.

Success criteria:
* Known teardown errors are either fixed or captured with actionable runtime evidence.
* The plan does not extend scope into DCP implementation replacement, Docker container deletion automation, port randomization changes, or global parallelization changes.

Dependencies:
* Step 4.1 completion.

## Dependencies

* .NET SDK `10.0.302`
* Docker Desktop and documented Aspire resources
* Aspire.Hosting.Testing `13.5.3`

## Success Criteria

* Cleanup runs all AppHost owner-release actions after individual teardown faults.
* Deterministic tests prove sequencing and aggregation without Docker.
* Closed-box validation confirms normal completion does not leave an orphaned DCP tunnel proxy.
