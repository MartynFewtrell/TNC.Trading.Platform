---
title: Failing Tests Resolution Verification
description: Evidence-based verification of Account Preferences browser test failures
ms.date: 2026-09-20
ms.topic: troubleshooting
---

## Research Scope

* Verify the current code paths and focused tests for unavailable-provider warning propagation
* Verify the current responsive desktop choice-layout assertion
* Identify focused validation commands and risks for the proposed corrections

## Research Status

Complete. The two named E2E tests reproduce in isolation. Current source and
the focused test result identify one stale layout assertion. They do not prove
a `FailureSummary` propagation defect in the product code.

## Reproduction Evidence

The following VSTest command ran on 2026-09-20 with .NET SDK 10.0.302 and
failed both selected tests in 46.5 seconds:

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~TNC.Trading.Platform.Web.E2ETests.AccountPreferences.AccountPreferencesE2ETests.AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation|FullyQualifiedName~TNC.Trading.Platform.Web.E2ETests.AccountPreferences.AccountPreferencesE2ETests.AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide" --logger "trx;LogFileName=failing-tests-resolution-verification.trx" --results-directory "artifacts\test-results\failing-tests-resolution-verification"
```

The retained result is:

* artifacts/test-results/failing-tests-resolution-verification/failing-tests-resolution-verification.trx
* `AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation` failed at AccountPreferencesE2ETests.cs:77 because no `account-preferences-warning` element appeared within Playwright's five-second assertion window
* `AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide` failed at AccountPreferencesE2ETests.cs:112 because `disabledBox.X > enabledBox.X` was false

## Verified Failure One: Unavailable Provider Warning

### Current Browser Test

test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:65-78 saves an enabled setting, changes the controlled provider to unavailable, clicks `Check status`, then expects the warning locator to contain the exact sentence `IG account preference observation was not available.`

The E2E fixture in test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:13-25 owns a controllable provider, an Aspire AppHost, and disables background account-preferences reconciliation. The provider returns HTTP 503 for every route, including session creation, when unavailable: test/Shared/AccountPreferences/ControllableIgProvider.cs:112-134.

### Current Product Path

1. The Razor page invokes `Presenter.CheckStatusAsync` from the `Check status` button at src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:31-32 and 105-106.
2. The presenter invokes `PlatformApiClient.CheckAccountPreferencesStatusAsync` and immediately applies its successful response to state at src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:103-110.
3. The typed API client sends `POST /api/platform/account-preferences/check-status`, requires an HTTP success result, then deserializes `AccountPreferencesViewModel` at src/TNC.Trading.Platform.Web/PlatformApiClient.cs:163-170. The model exposes `FailureSummary` at lines 225-245.
4. `AccountPreferencesPageViewModel.Apply` copies `preferences.FailureSummary` into the rendered state at src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:20-37.
5. The Razor page creates the alert whenever that state is non-empty at src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-20.
6. The API endpoint maps a handler result with state to HTTP 200, retaining the safe reason as `FailureSummary`, at src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:146-163.
7. The handler persists and returns a verification-failed state with the gateway safe reason at src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:12-42.

This is a complete current propagation path. No omitted assignment or conditional was found between the API response and the Razor warning.

### Current API Contract and Exact-Text Risk

test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:70-100 verifies the direct API contract: when unavailable, check status returns HTTP 200, `applicationStatus` is `VerificationFailed`, and `failureSummary` contains `unavailable`.

The exact E2E expectation depends on how the outage happens:

* A non-success preference observation response yields `IG account preference observation was not available.` at src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:24-28 and 132-140.
* The E2E provider makes session creation itself return 503. `CreateSessionAsync` throws, and `ObserveAsync` catches that `HttpRequestException` as `IG account preferences service is unavailable.` at the same file:15-32 and 142-154.

The E2E test's exact text therefore does not describe its own provider setup. It should assert the operator-visible unavailable condition, not an observation-route-specific message.

### Browser Discrepancy and Classification

The focused TRX includes Web-client request logs for the page load and save, but no logged `POST /api/platform/account-preferences/check-status`. The warning absence is therefore not currently attributable to `FailureSummary` propagation. The functional test proves the server response, and the Web source proves the successful response is rendered.

The unresolved browser issue is whether the Blazor click does not invoke the operation, invokes it after the assertion's five-second window, or receives a non-success response that the presenter places in `CurrentError`. `CheckStatusAsync` has no focused Web unit or component test for a successful `VerificationFailed` response with a non-empty summary. Existing warning coverage only covers a summary returned during the initial page load: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:155-180.

Do not change the endpoint, client, presenter, view model, or Razor warning yet. The concrete evidence supports a stale exact-text assertion plus incomplete browser-operation observability, not a demonstrated product propagation defect.

## Verified Failure Two: Desktop Choice Layout

### Current Browser Test

test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-120 sets a 1280 by 900 viewport and requires the Disabled radio to be to the right of Enabled, vertically aligned with it, with Save below the fieldset, and no horizontal overflow. The reproduced failure is precisely the `Disabled.X > Enabled.X` assertion at line 112.

### Current CSS Contract

The controls are intentionally a vertical flex column at src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:76-83. The radio group is a wrapping flex container at lines 89-93, with no desktop override. The only responsive breakpoint, at lines 148-161, affects state rows and status content, not the choice arrangement.

At 1280 pixels, the source permits the choices to wrap. A horizontal placement assertion is not a product contract and is contradicted by the current CSS. This is a stale E2E expectation.

The neighboring narrow viewport check at AccountPreferencesE2ETests.cs:122-136 already verifies visibility and no horizontal overflow. The desktop test should retain its 1280-pixel viewport and verify visible, usable controls, vertical Save placement, and no overflow, while removing the horizontal and same-row geometry requirements unless a documented design decision explicitly requires them.

## Focused Projects and Commands

The Web unit project is test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj. It uses bUnit and is the appropriate fast boundary for a new check-status summary rendering test.

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" --filter "FullyQualifiedName~AccountPreferencesTests"
```

The API functional project is test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj. Its existing unavailable-provider contract test is `SaveAndCheckStatus_ShouldFailSafelyAndRecover_WhenProviderIsUnavailable`.

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj" --filter "FullyQualifiedName~SaveAndCheckStatus_ShouldFailSafelyAndRecover_WhenProviderIsUnavailable"
```

The Aspire-hosted Playwright project is test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj. Use separate filters while diagnosing the browser issue so each test receives an isolated fixture run.

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation"
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide"
```

## Risks

* Replacing the warning assertion with a broad text match without proving the check-status request ran can hide a broken browser command path.
* Asserting the exact gateway sentence is brittle because session failures and preference-read failures intentionally produce different safe summaries.
* Changing `CheckStatusAsync` or API result mapping without a red test risks duplicating already-correct state propagation and changing the established HTTP 200 failure-state contract.
* Restoring a horizontal desktop layout only to satisfy the current test would silently change the intended wrapping CSS behavior.
* Geometry assertions based on individual radio input bounding boxes are sensitive to browser and Radzen rendering. Prefer semantic locators and overflow checks, retaining only geometry that is an explicit product requirement.
* The E2E fixture owns mutable provider state at collection scope. Any future test that changes `Available` must restore it or rely on the existing per-test `InitializeAsync` reset.

## Follow-On Questions

* Is a same-row Enabled/Disabled desktop layout at 1280 pixels an approved product requirement? Current CSS says no, but no design acceptance criterion was located.
* Why does the focused browser TRX omit a check-status POST after the Playwright click? Capture the browser request/circuit result before changing product code. The current test artifacts contain no screenshot, trace, or browser console record.

## Recommended Plan Phases

1. Add a fast bUnit or presenter test that supplies a successful check-status response with `VerificationFailed` and a non-empty `FailureSummary`, then asserts the warning is rendered. This protects the already-verified API-to-view-state contract.
2. Instrument or otherwise observe the existing E2E browser check-status operation. Assert the click reaches the API and distinguish a returned failure-state response from a `CurrentError` path before deciding whether any product correction is needed.
3. Update the unavailable-provider E2E assertion to require the alert and an `unavailable` semantic message after the request completion is observed. Do not retain the observation-specific exact sentence.
4. Replace the desktop horizontal and same-row assertions with visibility, non-overlap where required, Save placement, and no horizontal-overflow assertions. Keep the test at 1280 by 900.
5. Run the focused unit, functional, and each E2E filter. Then run the full Web E2E project and retain a TRX result.
