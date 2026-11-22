<!-- markdownlint-disable-file -->
# Implementation Details: Build, Tests, Aspire Upgrade, and Account Preferences

## Context Reference

Sources: `.copilot-tracking/research/2026-09-20/build-tests-aspire-account-preferences-research.md`, `.copilot-tracking/research/subagents/2026-09-20/build-tests-aspire-account-preferences-implementation-research.md`, user request, and repository instructions cited in the implementation plan.

## Implementation Phase 1: Baseline and Package Availability

<!-- parallelizable: false -->

### Step 1.1: Establish the diagnostic baseline

Run a clean, serial restore/build before modifying package references. Preserve `artifacts/build-baseline.binlog` for diagnosis and list unique diagnostics by code, package, direct owner, transitive path, and project. Confirm whether the baseline has only `NU1903` through SSH.NET and `NU1902` through AngleSharp; do not infer remediation for any other warning.

Files:
* `artifacts/build-baseline.binlog` - generated diagnostic evidence, not a source edit

Validation commands:
* `dotnet clean .\TNC.Trading.Platform.slnx`
* `dotnet restore .\TNC.Trading.Platform.slnx`
* `dotnet build .\TNC.Trading.Platform.slnx --no-restore -m:1 -bl:artifacts\build-baseline.binlog`

Discrepancy references:
* DR-01

Success criteria:
* Every baseline warning is categorized before package work begins.
* The confirmed warning count is explicitly compared with the reported count of four.

Dependencies:
* .NET SDK 10.0.302
* Available configured NuGet feeds

### Step 1.2: Verify package targets against configured feeds

Before edits, query the configured sources for `Testcontainers.MsSql 4.15.0`, `AngleSharp 1.5.0`, `Aspire.AppHost.Sdk 13.5.4`, `Aspire.Hosting.SqlServer 13.5.4`, `Aspire.Hosting.Testing 13.5.4`, and `Aspire.Hosting.Keycloak 13.5.4-preview.1.26464.4`. Confirm their resolved dependency graphs and retain the existing `Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` versions because they are not Aspire package-train dependencies.

Files:
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` - later Aspire package target
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj` - later Testcontainers target
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - later AngleSharp override

Discrepancy references:
* DR-02

Success criteria:
* Each exact target restores from an approved configured source.
* The chosen targets remove the reported vulnerable transitive versions without an unsupported leaf override.

Dependencies:
* Step 1.1 completion

### Step 1.3: Handle baseline or feed discrepancies

If the serial build emits more than the two confirmed warnings, document their direct owner and dependency path in the planning log before expanding the implementation. If an exact package target is unavailable or incompatible, select the nearest fixed version only after confirming its advisory status and matching Aspire package alignment; update the research and plan log before implementation.

Success criteria:
* No package edit proceeds from unverified warning or feed assumptions.

Dependencies:
* Steps 1.1 and 1.2 completion

## Implementation Phase 2: Correct Product and Test Failures

<!-- parallelizable: true -->

### Step 2.1: Correct mismatch-response projection

Modify `PlatformEndpoints` so the immediate response produced for an authenticated-account mismatch overlays both `VerificationStatus = VerificationFailed` and the handler's safe failure summary before applying `ToResponse`. Do not persist this failure to account preferences state, because the retained state still describes the last verified configuration and durable retry semantics must remain unchanged. Keep the functional test focused on the public HTTP behavior and no preferences I/O.

Files:
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - response overlay at current lines 151-154
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - existing mismatch response contract at current lines 161-172

Discrepancy references:
* None

Success criteria:
* `CheckStatus_ShouldReturnSafeMismatchWithoutPreferencesIo_WhenAuthenticatedAccountChanges` returns `VerificationFailed` and its safe summary.
* The mismatch remains non-persistent.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs` (current line 20) - source failure category
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` (current lines 9-12) - state-to-response mapping

Dependencies:
* None; may proceed in parallel with Steps 2.2 and 2.3

### Step 2.2: Prevent the duplicate hosted startup tick

Modify only the hosted `ExecuteAsync` path of `PlatformAuthenticationSupervisor` to wait for its configured interval before invoking the existing run loop. Retain the readiness tick in `StartAsync` and preserve immediate behavior in direct `RunUntilStoppedAsync` use, so existing direct-loop coverage remains meaningful.

Files:
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs` - initial readiness tick and hosted loop at current lines 30-49
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs` - startup readiness contract at current lines 59-71

Success criteria:
* `StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy` observes one startup tick before readiness.
* Cancellation, repeated scheduled ticks, and direct loop behavior remain covered by existing tests.

Dependencies:
* None; may proceed in parallel with Steps 2.1 and 2.3

### Step 2.3: Stabilize serializer and E2E fixture contracts

In `AccountPreferencesEndpointMappingTests`, serialize using `new JsonSerializerOptions(JsonSerializerDefaults.Web)` so its expected camel-case JSON reflects ASP.NET Core output. In the real-auth E2E fixture, pass `AccountPreferences:Reconciliation:Enabled=false` through the existing AppHost configuration route, isolating the explicit remediation scenario from the one-second background reconciler. Do not use sleeps, global serialization, or cross-process pauses.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` - serialization assertion at current lines 112-122
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - fixture environment configuration at current lines 18-23

Success criteria:
* `ToResponse_ShouldSerializeDesiredTrailingStopsValue_WhenStateIsProjected` validates the actual HTTP JSON naming contract.
* `AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation` has fixture-owned reconciliation behavior and no race.

Dependencies:
* None; may proceed in parallel with Steps 2.1 and 2.2

### Step 2.4: Validate each corrected failure

Run the named functional test after Step 2.1, then the API unit-test filter after Steps 2.2 and 2.3. Run the focused warning E2E test as part of the account-preferences E2E class in Phase 3 because that class shares browser/AppHost setup.

Validation commands:
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj" --filter "FullyQualifiedName~CheckStatus_ShouldReturnSafeMismatchWithoutPreferencesIo_WhenAuthenticatedAccountChanges"`
* `dotnet test "test\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.UnitTests\TNC.Trading.Platform.Api.UnitTests.csproj" --filter "FullyQualifiedName~ToResponse_ShouldSerializeDesiredTrailingStopsValue_WhenStateIsProjected|FullyQualifiedName~StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy"`

Success criteria:
* All three focused tests pass before package changes begin.

Dependencies:
* Steps 2.1 through 2.3 completion

## Implementation Phase 3: Adjust Account Preferences Layout

<!-- parallelizable: true -->

### Step 3.1: Make the outer editor row vertical

Change `.account-preferences-control-row` in the scoped stylesheet from the current wrapping horizontal flex direction to `flex-direction: column`, with `align-items: flex-start` and no inter-block gap. Retain the existing markup and nested `.account-preferences-radio-group` horizontal wrapping behavior. Do not use CSS ordering or change DOM order.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - outer row at current lines 76-82, nested radio group at current lines 89-93
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - verified DOM order at current lines 43-52; inspect only, unless unexpected styling coupling requires a minimal change

Success criteria:
* Enabled and Disabled remain horizontally adjacent through the nested radio group.
* Save preferences renders beneath the group with aligned left edges.
* DOM order and keyboard order remain fieldset, Enabled, Disabled, Save.

Dependencies:
* None; may proceed in parallel with Phase 2

### Step 3.2: Revise desktop geometry assertions

Update the existing desktop Playwright assertions in the account-preferences E2E class to assert: Disabled lies to the right of Enabled, Save lies below the radio group, the choice group and Save share a left edge, and the configuration panel does not horizontally overflow at 1280px. Keep user-facing locators and web-first assertions.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - desktop geometry contract at current lines 87-116

Success criteria:
* The geometry assertion describes the requested visual layout rather than the previous side-by-side editor/action layout.
* Existing narrow 360px overflow and ArrowRight radio navigation tests remain unchanged unless a genuine regression exposes a necessary correction.

Dependencies:
* Step 3.1 completion

### Step 3.3: Validate the page behavior

Run the focused E2E class, including the warning scenario, plus Web unit tests after the CSS and geometry contract are updated. Confirm the scenario does not introduce explicit waits or browser-only selector fragility.

Validation commands:
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj"`

Success criteria:
* Desktop, narrow viewport, radio keyboard, and warning browser scenarios pass.

Dependencies:
* Steps 3.1 and 3.2 completion

## Implementation Phase 4: Upgrade Coordinated Packages and Remediate Warnings

<!-- parallelizable: false -->

### Step 4.1: Align the Aspire package train

After Step 1.2 verifies feed availability, update all direct Aspire references as one patch set: `Aspire.AppHost.Sdk`, `Aspire.Hosting.SqlServer`, the matching preview `Aspire.Hosting.Keycloak`, and four `Aspire.Hosting.Testing` test-project references. Do not alter `Microsoft.Extensions.Http.Resilience` or `Microsoft.Extensions.ServiceDiscovery`; their 10.1.0 versions are separate .NET Extensions dependencies.

Files:
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` - AppHost SDK, SQL Server, and Keycloak references
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Aspire.Hosting.Testing reference
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` - Aspire.Hosting.Testing reference
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Aspire.Hosting.Testing reference
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Aspire.Hosting.Testing reference

Discrepancy references:
* DR-02

Success criteria:
* Seven direct Aspire references resolve to one verified 13.5.4 patch train, including the matching Keycloak preview version.

Dependencies:
* Phase 1 and focused Phase 2/3 validation completion

### Step 4.2: Remediate vulnerable test dependencies

Upgrade `Testcontainers.MsSql` from 4.8.1 to the verified fixed target and add the verified direct `AngleSharp` override to the Web unit-test project. Do not introduce a direct SSH.NET reference, because the tested owning-package upgrade is the lower-risk correction.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj` - Testcontainers.MsSql reference
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - bunit.web and direct AngleSharp override

Discrepancy references:
* DR-01
* DR-02

Success criteria:
* Restore no longer resolves SSH.NET 2024.2.0 or AngleSharp 1.2.0 through these project graphs.
* No direct SSH.NET override is added.

Dependencies:
* Step 1.2 completion

### Step 4.3: Inspect package and focused regression results

Restore the solution, inspect the two affected project graphs with transitive packages included, and run the infrastructure integration tests and Web unit tests. Treat a compatibility failure in bUnit-related AngleSharp packages or preview Keycloak integration as a package-resolution blocker requiring renewed research.

Validation commands:
* `dotnet restore .\TNC.Trading.Platform.slnx`
* `dotnet list "test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj" package --include-transitive`
* `dotnet list "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" package --include-transitive`
* `dotnet test "test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj"`
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj"`

Success criteria:
* Resolved package graphs contain fixed versions and focused suites pass.

Dependencies:
* Steps 4.1 and 4.2 completion

## Implementation Phase 5: Full Validation and Operational Check

<!-- parallelizable: false -->

### Step 5.1: Run full managed validation

Run a serial no-restore solution build followed by the complete no-restore solution test suite. Compare warning output against the Phase 1 baseline and verify the four named failures no longer occur.

Validation commands:
* `dotnet build .\TNC.Trading.Platform.slnx --no-restore -m:1`
* `dotnet test .\TNC.Trading.Platform.slnx --no-restore`

Success criteria:
* Build completes without the confirmed `NU1903` and `NU1902` vulnerability warnings.
* Solution tests pass, except for documented unrelated pre-existing failures that were demonstrably present in the Phase 1 baseline.

Dependencies:
* Phases 2 through 4 completion

### Step 5.2: Validate Docker-backed distributed behavior

Start the existing AppHost entry point with Docker available. Verify resource and endpoint health in the Aspire dashboard, Keycloak realm import, local OIDC sign-in, authenticated API access, SQL Server connectivity, named endpoint readiness, and graceful asynchronous cleanup. Reuse existing persisted local infrastructure where supported; do not manually start Keycloak or SQL Server outside Aspire.

Success criteria:
* AppHost composition succeeds with healthy Keycloak, SQL Server, API, and Web resources.
* Authentication and data-access flows work on the upgraded Keycloak preview integration.

Dependencies:
* Step 5.1 completion
* Docker daemon running

### Step 5.3: Classify validation failures

Repair straightforward, localized compilation, test, or assertion defects and rerun the owning focused command. For new package compatibility, AppHost resource wiring, or cross-service failures, record the evidence in the planning log and create follow-on research rather than expanding scope.

Success criteria:
* Final outcomes clearly distinguish fixed defects from blockers needing a follow-on plan.

Dependencies:
* Steps 5.1 and 5.2 completion

## Dependencies

* .NET SDK 10.0.302
* Docker Desktop or equivalent Docker daemon
* Approved NuGet sources and package advisory metadata
* Existing Aspire AppHost and test credentials

## Success Criteria

* Implementers can execute each change and validation command without inferring ownership or expected behavior.
* The resulting change preserves local Keycloak-in-Aspire composition, Clean Architecture boundaries, deterministic test fixtures, and semantic page order.
