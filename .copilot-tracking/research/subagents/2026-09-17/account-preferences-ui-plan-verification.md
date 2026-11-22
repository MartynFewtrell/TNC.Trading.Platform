---
title: Account Preferences UI Plan Verification
description: Verifies implementation anchors and validation commands for the Account Preferences UI improvement plan.
ms.date: 2026-09-17
ms.topic: reference
---

## Research Scope

* Verify the supplied Account Preferences UI improvement research against the current implementation.
* Identify exact implementation and test anchors with line evidence.
* Determine focused project validation commands for the proposed plan.
* Confirm whether the preferred two-tab approach fits the current architecture.

## Status

Complete.

## Findings

### Confirmed implementation boundary

The improvement is a Web presentation slice. The current component renders current
state, the editable trailing-stops control, and observed history as sequential panels:
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor
lines 18-56. It contains the existing state and provider-failure presentation (lines
22-29), actions (lines 30-31), native `InputRadioGroup` editor and save confirmation
(lines 33-52), and history table plus opaque-cursor action (lines 54-56).

`AccountPreferencesPagePresenter` already separates `CurrentError`, `HistoryError`,
`SaveError`, and `Message` (lines 3-13). It loads history after a successful current
state response (lines 20-47), preserves an opaque `NextCursor` operation (lines
84-94), and requests 25 observations per page (lines 112-115). No API, application,
or persistence contract change is needed for the UI plan.

The page currently omits `SaveError`; it is set on a failed save in
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs
lines 50-82, but is not rendered in the page. The plan should decide whether this
existing omission is in scope as part of placing feedback with its owning workflow.

### Confirmed two-tab architecture fit

The preferred two-tab approach matches the current architecture.

* Radzen is an established Web dependency in
	src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj lines 3-16.
* Shared setup registers Radzen components, and the app shell hosts the theme:
	src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs line 13
	and src/TNC.Trading.Platform.Web/Components/App.razor line 12.
* The nearest precedent is
	src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor
	lines 27-62. It uses page-local `RadzenTabs`, a bound selected index, `Change`, an
	accessible `AriaLabel`, and keyed tab content.
* The view model and presenter remain appropriate owners of state and API calls:
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs
	lines 3-47 and
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs
	lines 3-115.

Use exactly two page-local tabs: `Settings` first and `Observed history` second. Keep
the component's existing request lifetime and handlers. Add only a selected-tab field
and Razor structure necessary for the new view. The current Account Details tab
selection-retention logic is dynamic-account-type-specific and should not be copied
for two fixed tabs.

### Confirmed history contract

History ordering is newest observation first and uses the observation identifier as a
tie-breaker in
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs
lines 27-63. The current UI displays `RecordedAtUtc` in the `Recorded` column:
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor
lines 54-56. The plan must preserve server ordering and cursor traversal.

### Confirmed test anchors

* bUnit coverage is in
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs
	lines 17-357. It already covers initial render (lines 73-92), history failure
	rendering (lines 116-136), radio selection (lines 140-158), save status and focus
	restoration (lines 162-192), and cursor use (lines 357-371).
* The nearest bUnit tab model is
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs
	lines 17-159. It asserts tab labels, tab selection, and retained selection after
	dynamic data replacement.
* Browser coverage is in
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs
	lines 6-40. It currently covers the provider-unavailable operator experience only.
* The bUnit project references the Web project and bUnit in
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
	lines 3-25. The E2E project references the AppHost and Playwright in
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj
	lines 3-29.
* E2E tests use an Aspire-hosted fixture that starts and health-checks Keycloak, API,
	and Web before exposing endpoints:
	test/Shared/Authentication/ManagedAppHostFixture.cs lines 10-55. Docker-backed
	infrastructure must be available for that suite.

### Recommended implementation sequence

1. Restructure `AccountPreferences.razor` into labelled page-local `RadzenTabs`,
	 retaining the current top-level load/access states. Put state, verification warning,
	 editor, save feedback, and commands in `Settings`; put history errors, the table,
	 and the cursor pager in `Observed history`.
2. Update `AccountPreferences.razor.css` lines 1-104 to scope radio-choice styling,
	 constrain its width, add grouped action spacing, retain 44px targets and focus
	 styling, and keep the existing narrow-screen stacking behavior.
3. Extend `AccountPreferencesTests.cs` with rendering assertions for the tablist name,
	 default Settings selection, feedback ownership, history tab/pager accessible name,
	 and unchanged save/radio/cursor behavior. Follow the existing documented test
	 convention.
4. Extend `AccountPreferencesE2ETests.cs` with role-based browser interaction for the
	 two tabs and visible history controls. Keep the current authentication fixture and
	 use Playwright web-first assertions.

### Validation commands

Run the fast component suite after each Razor/CSS/test increment:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesTests"
```

This command was executed during verification and passed: 27 tests succeeded. The
build emitted three pre-existing warnings: NU1902 for AngleSharp and two nullable
warnings in unrelated unit test files.

Run the browser suite with Docker available after the tab interaction is implemented:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesE2ETests"
```

Run the full Web unit project before completion:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
```

## Follow-On Questions

* Should the history column display `ObservedAtUtc` to match newest-observed-first
	ordering, or retain `RecordedAtUtc` with an explicit audit-time label?
* Should the plan render the presenter's existing `SaveError` in Settings? It is
	currently computed but invisible, and rendering it aligns with the stated feedback
	hierarchy goal.
* Should a successful next-history-page load announce status, or is updating the
	table and disabled cursor action sufficient for the operator workflow?
