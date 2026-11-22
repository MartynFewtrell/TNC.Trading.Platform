<!-- markdownlint-disable-file -->
# Implementation Details: Failing Test Resolution

## Context Reference

Sources: user request; .copilot-tracking/research/2026-09-20/failing-tests-resolution-research.md; .copilot-tracking/research/subagents/2026-09-20/failing-tests-resolution-verification.md.

## Implementation Phase 1: Protect And Observe The Check-Status Contract

<!-- parallelizable: false -->

### Step 1.1: Add Check-Status Warning Unit Coverage

Extend AccountPreferencesTests with a MethodName_StateUnderTest_ExpectedResult test. Arrange CheckAccountPreferencesStatusAsync to return a successful VerificationFailed state with a non-empty unavailable FailureSummary. Execute Check status and assert that the warning locator renders the supplied summary. Reuse the existing bUnit fixture and mock setup.

Files:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs - Add focused Check status warning coverage beside existing initial-load warning coverage.

Discrepancy references:

* Addresses DR-01 by covering the currently untested successful command response path.

Success criteria:

* The test fails if the presenter, view model, or component stops retaining FailureSummary after Check status.
* The test passes without changing the API HTTP 200 failure-state contract.

Context references:

* .copilot-tracking/research/subagents/2026-09-20/failing-tests-resolution-verification.md (Lines 37-56) - Verified propagation path.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs (Lines 155-180) - Existing warning test pattern.

Dependencies:

* Existing Account Preferences bUnit fixture and API-client mock setup.

### Step 1.2: Observe The Browser Check-Status Operation

Update the unavailable-provider E2E test so the Check status click is coupled to explicit request or response observation for POST /api/platform/account-preferences/check-status. Use existing Playwright conventions and resilient semantic locators. Capture enough result information to distinguish no request, delayed request, non-success response, and a successful response carrying failure state. Do not extend the assertion timeout as a substitute for observing the operation.

Files:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs - Add request/result observation around the existing Check status click.

Discrepancy references:

* Addresses DR-02 by resolving the missing check-status request before assigning a product-code owner.

Success criteria:

* The test produces deterministic evidence that the endpoint was reached or that a specific browser-side operation failed.
* The test preserves fixture isolation and existing provider reset behavior.

Context references:

* .copilot-tracking/research/subagents/2026-09-20/failing-tests-resolution-verification.md (Lines 58-68) - Browser-log discrepancy.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs (Lines 13-25) - Fixture ownership.

Dependencies:

* Step 1.1 completion provides a fast guard for expected warning rendering.

### Step 1.3: Select The Evidence-Driven Correction

If the observed request returns HTTP 200 with a non-empty FailureSummary, update the E2E assertion to require the alert and text containing unavailable; do not alter product code. If the operation does not reach the endpoint or returns an unexpected result, trace one hop to the code directly controlling that behavior, add a focused red test, and repair only that path. Do not change PlatformEndpoints, PlatformApiClient, AccountPreferencesPagePresenter, AccountPreferencesPageViewModel, or AccountPreferences.razor unless this evidence disproves the verified current behavior.

Files:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs - Replace the stale exact-text assertion after request completion is established.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs - Modify only if observed request evidence identifies the presenter as the cause.
* src/TNC.Trading.Platform.Web/PlatformApiClient.cs - Modify only if evidence identifies client response handling as the cause.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor - Modify only if evidence identifies command invocation or warning rendering as the cause.

Discrepancy references:

* Resolves DD-01 by replacing the original research document's speculative presenter repair with observed browser evidence.

Success criteria:

* The E2E assertion accepts both supported unavailable summaries by requiring unavailable rather than an exact gateway message.
* Any product modification has a focused failing test and a directly observed operation failure.
* The existing API functional contract remains unchanged.

Context references:

* .copilot-tracking/research/subagents/2026-09-20/failing-tests-resolution-verification.md (Lines 49-68) - Exact-text risk and decision gate.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs (Lines 70-100) - Server contract.

Dependencies:

* Step 1.2 operation evidence.

### Step 1.4: Validate Phase 1

Run the scoped checks after each correction. Save the E2E TRX result under artifacts/test-results/failing-tests-resolution.

Validation commands:

* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" --filter "FullyQualifiedName~AccountPreferencesTests"` - Account Preferences component and presenter behavior.
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj" --filter "FullyQualifiedName~SaveAndCheckStatus_ShouldFailSafelyAndRecover_WhenProviderIsUnavailable"` - API unavailable-provider contract.
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation" --logger "trx;LogFileName=failing-tests-resolution-warning.trx" --results-directory "artifacts\test-results\failing-tests-resolution"` - Browser warning workflow.

## Implementation Phase 2: Align Responsive Layout Coverage

<!-- parallelizable: false -->

### Step 2.1: Replace Unsupported Desktop Geometry Assertions

Before editing the test, obtain an Account Preferences product/design-owner decision, recorded in an approved work item or design artifact, confirming whether wrapping is accepted at the 1280 by 900 viewport. If it is accepted, keep the viewport and accessible locators, remove assertions requiring Disabled to have a greater X coordinate than Enabled or to share its row, and assert visibility, required non-overlap, Save placement, and no horizontal overflow. If it is not accepted, preserve the geometry requirement and create a separate CSS-change plan rather than changing the test to accept wrapping.

Files:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs - Replace same-row assumptions with responsive usability assertions.

Discrepancy references:

* Addresses DR-03 by aligning the E2E test with the wrapping CSS contract.

Success criteria:

* The test fails for clipped, overlapping, inaccessible, or horizontally overflowing controls.
* The test passes when responsive wrapping moves Disabled below Enabled.
* No CSS production change is made solely to satisfy a stale test.

Context references:

* .copilot-tracking/research/subagents/2026-09-20/failing-tests-resolution-verification.md (Lines 70-80) - CSS and test mismatch.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css (Lines 76-93) - Current layout contract.

Dependencies:

* Phase 1 completion because both phases modify AccountPreferencesE2ETests.cs.

### Step 2.2: Validate Desktop Layout Coverage

Validation commands:

* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide" --logger "trx;LogFileName=failing-tests-resolution-layout.trx" --results-directory "artifacts\test-results\failing-tests-resolution"` - Desktop responsive-layout workflow.

## Implementation Phase 3: Final Validation And Evidence Retention

<!-- parallelizable: false -->

### Step 3.1: Run Affected Project Validation

Execute full Web unit, functional, and E2E projects after both phases complete. Preserve browser TRX output so future reports include a test name, trace, and browser evidence.

Validation commands:

* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj"` - Full Web unit suite.
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj"` - Full Web functional suite.
* `dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --logger "trx;LogFileName=failing-tests-resolution-final.trx" --results-directory "artifacts\test-results\failing-tests-resolution"` - Full Aspire-hosted browser suite.

Success criteria:

* Affected projects pass, or remaining failures are retained with complete output and classified separately.

Dependencies:

* Phase 1 and Phase 2 completion.

### Step 3.2: Triage The Unverified Third Failure

Do not modify implementation files for the third report. Obtain its originating test name, stack trace, and current TRX or reproduce the named test in isolation. Start a separate research and planning cycle only if new evidence identifies a distinct failure mechanism.

Success criteria:

* The third report is not misclassified or hidden by unrelated Account Preferences changes.

Dependencies:

* Source-run output from the original reporter or a reproducible test name.

### Step 3.3: Report Blocking Results

Treat failures requiring changes beyond the Check status command path or responsive layout assertions as separate work. Record the exact test, owning code path, retained output path, and recommended research before expanding scope.

## Dependencies

* Docker, Aspire test prerequisites, Playwright browsers, and the controllable provider fixture.

## Success Criteria

* The plan can be implemented without assuming a product defect current evidence does not establish.
* Every reproducible failure has focused validation and a behavior-based resolution.