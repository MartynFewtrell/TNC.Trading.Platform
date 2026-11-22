<!-- markdownlint-disable-file -->
# Implementation Details: Resolve Failing Web Tests

## Context Reference

Sources: `.copilot-tracking/research/2026-09-15/current-testing-strategy-and-failing-tests-research.md:62-176`; `.copilot-tracking/research/subagents/2026-09-15/failing-web-tests-repair-surfaces-research.md:28-145`.

## Implementation Phase 1: Repair Listener Retry Ownership

<!-- parallelizable: false -->

### Step 1.1: Create and own a listener per bind attempt

Modify `test/Shared/AccountPreferences/ControllableIgProvider.cs:36-59`. Construct and configure `HttpListener` inside each existing bounded candidate-port attempt. Return and retain only the listener whose `Start()` succeeds. On `HttpListenerException`, close or dispose that attempt before continuing. Preserve the existing attempt limit, loopback base URI, request routes, stateful behavior, and asynchronous provider cleanup.

Files:
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Listener lifecycle repair

Discrepancy references:
* Addresses DR-01 in the planning log

Success criteria:
* No listener that fails binding is reconfigured or restarted.
* The successfully started listener remains owned by existing `DisposeAsync` behavior.

Dependencies:
* None

### Step 1.2: Add a deterministic test seam

Add an internal or test-only candidate-port sequence or selector adjacent to `Start()`. Leave current callers on parameterless `Start()` and retain its random high-port strategy. Do not add a public production API, fixed-port lease, process-wide setting, or new test project.

Files:
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Candidate-port selection seam

Discrepancy references:
* Addresses DR-02 in the planning log

Success criteria:
* Existing fixture callers require no change.
* The regression can force an occupied candidate, followed by a usable candidate, with no sleep or random collision.

Dependencies:
* Step 1.1 approach selected

### Step 1.3: Add focused replacement-listener coverage

Create `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/ControllableIgProviderTests.cs`. Bind an occupying loopback listener to the first injected port, supply a free second port, call the provider, then request an existing provider endpoint. Assert its expected response and dispose both objects through reliable cleanup. Do not start an AppHost.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/ControllableIgProviderTests.cs` - New retry regression

Success criteria:
* Test is named `Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied`.
* It fails against failed-listener reuse and passes only when the replacement listener serves a route.
* No `.csproj` change is needed because the functional project already directly compiles the shared provider.

Validation command:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release --filter "FullyQualifiedName~ControllableIgProviderTests.Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied"`

Dependencies:
* Steps 1.1 and 1.2

## Implementation Phase 2: Isolate E2E Keycloak State

<!-- parallelizable: false -->

### Step 2.1: Scope non-persistent Keycloak to browser fixture

Modify `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-24` to add `AppHost:UsePersistentKeycloakState` with `bool.FalseString`. Retain the current IG base URL and interactive-sign-in entries. Do not modify `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs`.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Fixture configuration entry

Discrepancy references:
* Resolves the E2E Keycloak state-isolation failure; it does not address solution-level test-host cancellation

Success criteria:
* Browser fixtures receive a fresh Keycloak lifecycle.
* Functional and interactive local AppHost persistence behavior is unchanged.

Dependencies:
* None

### Step 2.2: Validate individual browser journeys

Run each previously failing E2E test in Release. Keep Playwright trace, screenshot, final URL, and relevant Aspire resource logs for any residual failure before determining whether it reaches a feature assertion.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --filter "FullyQualifiedName~PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderOperatorHome_WhenSeededViewerSignsInFromAspireDashboard"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --filter "FullyQualifiedName~AccountPreferencesE2ETests.AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable"`

Dependencies:
* Step 2.1
* Docker Desktop and Playwright Chromium

## Implementation Phase 3: Validate Repaired Web Suites

<!-- parallelizable: false -->

### Step 3.1: Run the functional project

Run all functional tests after the focused provider test passes. Repeated fixture-startup failures return to Phase 1; only failures that reach a functional assertion become product-behavior investigation.

Validation command:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release`

### Step 3.2: Run E2E twice to demonstrate isolation

Run the whole E2E project after both individual tests pass, then run it once more. The repeat proves a clean distributed lifecycle does not leak Keycloak state.

Validation command:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release`

Dependencies:
* Focused validation from Phases 1 and 2

### Step 3.3: Preserve fixture boundaries

Confirm no fixed-port leasing, global serialization, Docker metadata discovery, environment overrides, synthetic authentication, or AppHost default change was introduced. Record solution-level `dotnet test` host cancellation separately rather than accepting its partial count as a quality gate.

## Implementation Phase 4: Final Validation

<!-- parallelizable: false -->

### Step 4.1: Capture CI-equivalent results

Run the final functional project once and E2E project twice in Release after focused tests pass. Then run the independent fast, Docker-backed integration, and browser/Keycloak project stages represented by `.github/workflows/ci.yml:70-161`. Capture each stage's exit code and totals. Do not substitute the cancelled solution command's partial result; a successful existing CI workflow is the authoritative aggregate confirmation when local tooling cannot complete each stage.

### Step 4.2: Correct minor local findings

For an isolated failure in a changed fixture or test, fix that slice and rerun its focused command before its project-level suite.

### Step 4.3: Escalate broader blockers

Record product changes, test-architecture rewrites, or solution-level host cancellation as new research items.

## Dependencies

* .NET 10 SDK, Docker Desktop, Aspire Keycloak resources, and Playwright Chromium

## Success Criteria

* Deterministic fresh-listener retry coverage passes.
* Both Web test projects pass in CI-aligned Release configuration.
* Repeated E2E execution demonstrates Keycloak state isolation.