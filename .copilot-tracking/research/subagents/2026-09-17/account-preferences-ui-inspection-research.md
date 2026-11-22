---
title: Account Preferences UI Inspection Research
description: Evidence-backed inspection of the Web navigation, Account Preferences UI, Account Details reference layout, action controls, styling, and focused tests
author: GitHub Copilot
ms.date: 2026-09-17
ms.topic: reference
keywords:
	- blazor
	- radzen
	- account preferences
	- account details
	- ui tests
estimated_reading_time: 8
---

## Research Scope

Inspect the Blazor Web UI responsible for:

* Main navigation ordering, including Account details and Account preferences
* Account Preferences page shell, tabs, and cards
* Account Details visual structure as the reference pattern
* Retry verification, Apply desired setting, and Save preference actions
* Relevant CSS, Radzen usage, and focused automated tests

No product code was changed. The only workspace modification is this research record.

## Commands And Search Terms Used

* `file_search` for `src/TNC.Trading.Platform.Web/**/*` and
	`test/TNC.Trading.Platform.Web/**/*`
* `grep_search` for `Retry verification|Apply desired setting|Save preference`
* `grep_search` for `Account details|Account preferences`
* `grep_search` for
	`RetryAsync|RemediateAsync|RetryAccountPreferencesVerificationAsync|RemediateAccountPreferencesAsync`
	within Web tests
* `grep_search` for
	`platform-primary-action|platform-button|account-preferences-actions|account-details-tabs|platform-page-header|panel`
	within Web Razor and CSS files
* Attempted `rg -n` against the relevant Web source and test directories. It failed because
	`rg` is not installed in the active PowerShell environment.
* Used PowerShell `Select-String` as the fallback to collect the cited 1-based line numbers.

## Main Navigation

The shared shell renders navigation only when the operator has at least one platform role.
Within that protected navigation, Home is first, Viewer links appear next, Operator links
follow, then the remaining Viewer history link and the Administrator link.

* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:20` renders Home before
	role-specific links.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:21` begins the Viewer block.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:23` renders Account details
	before Status at line 24.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:27` begins the Operator block.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:29` renders Configuration
	before Account preferences at line 30.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:34` renders IG login history
	after the Operator block.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformNavItem.razor:1` through line 6 use a
	`NavLink` with a `RadzenIcon`; ordering is therefore determined entirely by `MainLayout` markup.

Current behavior: Account details is Viewer-gated and appears in the first role-specific group.
Account preferences is Operator-gated and appears after Configuration. A user with both role
capabilities sees the two destinations in separate role blocks, with Status and Configuration
between them.

Focused coverage: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs:34`
through line 57 asserts that a signed-in viewer sees navigation, Status, and Account details.
It does not assert Account preferences visibility or the relative order of either requested link.

## Account Preferences UI

### Shell And State

The page route is Operator-authorized and begins with a lightweight redirect/loading state.
After a successful initial load, it uses the shared page-header panel followed by a Radzen tab
control.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:2`
	declares `/account-preferences`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:4`
	applies the Operator policy.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:11`
	through line 15 select redirect, loading, or the header.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:15`
	uses `panel platform-page-header` for the heading and hint.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:17`
	instantiates `RadzenTabs` with the accessible name `Account preference views`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:19`
	starts Settings, and line 63 starts Observed history.

Settings consists of two sibling panels inside the Settings tab. The first is the five-row state
definition list plus Verification actions; the second is the confirmed-value heading, radio
fieldset, save action, and status feedback.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:20`
	starts the state panel.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:23`
	through line 29 render Desired setting, account identifiers, observed value, and verification
	status.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:38`
	starts the separate preference editor panel when a confirmed value exists.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:42`
	through line 47 render native `InputRadioGroup<bool>` controls.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:64`
	through line 73 render the history panel, semantic table, and older-observations command.

Current behavior: the Radzen tab control is not wrapped in an Account Preferences-specific tab
container and has no local `::deep .rz-tabview` overrides. It will retain the active Radzen theme's
default tab container treatment, unlike Account Details.

### Requested Commands

All three commands are in Settings and use native `button` elements.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:31`
	starts the named Verification actions group.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:32`
	conditionally shows Retry verification when `AccountId` is non-null. It calls `RetryAsync` and
	is disabled while `Presenter.IsOperating` or `Presenter.IsSaving`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:33`
	conditionally shows Apply desired setting when a desired revision exists and observed and desired
	trailing-stop values differ. It calls `RemediateAsync` and uses the same disabled condition.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:49`
	starts the named Preference actions group.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:50`
	renders Save preference with `platform-primary-action`, a stable test id, `SaveAsync`, and a
	disabled condition of `IsSaving || IsLoading`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:94`
	through line 95 wire the page callbacks to presenter methods.

The page delegates the command behavior to the presenter.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:54`
	through line 84 implement Save. Success applies the returned state, announces confirmation, and
	refreshes history; failure produces `SaveError`; `IsSaving` is reset in `finally`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:97`
	through line 104 implement Retry. It exits for a missing account, calls the retry API, applies
	returned state, and resets `IsOperating`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:106`
	through line 113 implement Apply desired setting. It additionally requires a desired revision,
	calls remediation with the selected Boolean value, and resets `IsOperating`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:24`
	through line 40 map the API response into desired, observed, revision, failure, and verification
	state used by the visibility conditions.

## Account Details Reference Structure

Account Details is the closest in-repository visual reference. It uses the same shared header
panel, a containing panel with retrieval metadata, then an Account Details-specific wrapper around
the Radzen tabs. Each selected account type renders one or more nested account panels.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:10`
	through line 15 show the shared header and optional Refresh command.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:27`
	starts the outer retrieval panel.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:28`
	renders retrieval metadata before tabs.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:29`
	supplies `account-details-tabs`, the CSS scope for the Radzen customization.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:30`
	creates named Radzen tabs; lines 34 through 60 create one tab per account type and nested account
	panels.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:65`
	through line 68 keep history navigation after the retrieval panel.

The tab CSS removes the Radzen card treatment so the tab strip visually belongs to the outer
retrieval panel.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:19`
	through line 30 set transparent tab backgrounds, no tab-view border or shadow, and top padding.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:32`
	through line 45 define the tab-strip divider and selected accent underline.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:48`
	through line 53 remove panel border, radius, and shadow.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:5`
	through line 16 set a three-column account summary that collapses at line 55 for narrow layouts.

## CSS And Radzen Patterns

Shared app CSS owns the global visual vocabulary:

* `src/TNC.Trading.Platform.Web/wwwroot/app.css:241` through line 248 define `.panel` as a strong
	surface with border, `1.25rem` radius, padding, shadow, and bottom margin.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:250` through line 254 define the flexible,
	space-between `.platform-page-header` arrangement.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:277` through line 287 define the gradient
	`.platform-primary-action` used only by Save preference in this feature.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:294` through line 306 define the reusable
	key-value grid used by Account Details metadata.

Account Preferences local CSS supports its current two-panel settings design:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:1`
	through line 18 format the state list as label/value grid rows.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:21`
	through line 30 provide wrapping, 44px-minimum action controls.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:47`
	through line 92 create two adjacent selectable radio labels and visible selected/focus states.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:94`
	through line 118 style success feedback and keyboard focus.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:121`
	through line 149 stack state rows, radio choices, action buttons, and confirmation controls at
	600px or below.

Radzen is already a direct dependency at
`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:9`, version `10.3.2`. Existing usage
uses `RadzenTabs`, `RadzenTabsItem`, `RadzenRow`, `RadzenColumn`, `RadzenBadge`, and `RadzenIcon`.
The Account Details scoped `::deep` override is the established local pattern for modifying
Radzen-generated tab markup.

## Focused Test Coverage

Existing bUnit coverage is the correct first validation layer for structural and state changes:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:110`
	through line 133 assert the selected Settings tab, tab names, state definition list, radio
	semantics, and absence of the history table until selected.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:137`
	through line 158 assert alert placement, named Verification/Preference action groups, and save
	failure feedback.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:162`
	through line 181 assert the history tab, table caption, named history group, and enabled cursor
	action.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:253`
	through line 276 assert save confirmation, preserved application status, and focus return after
	dismissal.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:319`
	through line 333 assert that saving disables the fieldset and save action.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:19`
	through line 47 assert source-ordered account-type tabs and selected-tab content.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:50`
	through line 66 assert the retrieval and tab CSS hook classes.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:36`
	through line 76 cover pointer and keyboard activation of Observed history.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:81`
	through line 112 cover the accessible history table and disabled cursor command.

Gap: no direct test match exists in
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`
for `Retry verification`, `Apply desired setting`, `RetryAsync`, or `RemediateAsync`. The current
E2E tests do not exercise Retry verification, Apply desired setting, or Save preference either.

## Recommended Minimal Changes

The requested visual alignment can be made without changing routes, authorization, API client
calls, presenter methods, page state, or button conditions.

1. In `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`,
	 wrap the existing `RadzenTabs` in a page-local container such as
	 `<div class="account-preferences-tabs">`. Preserve the existing Settings and Observed history
	 markup, tab binding, accessible name, and all command elements.
2. In `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`,
	 add the Account Details tab-view rules under `.account-preferences-tabs ::deep` with Account
	 Preferences selectors. This removes the Radzen nested surface and provides the established
	 divider/selected-underline treatment.
3. Keep Settings' two existing panels, verification group, and preference group as they are unless
	 a product decision explicitly asks to merge them. They convey distinct live state and edit/save
	 workflows and existing tests assert both group names.
4. Add a bUnit test that fixes the Account Preferences tab wrapper and emitted Radzen tab-view in
	 place, modeled on
	 `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:50`
	 through line 66. Extend `MainLayoutTests` only if navigation order is intentionally changed.
5. Add bUnit behavior cases for the verification controls: Retry is absent without an account and
	 present with one; Apply desired setting appears only for a revision-backed mismatch; both become
	 disabled while their request is in flight. Use sequenced/delayed responses already established
	 in `AccountPreferencesTests`.

## Viable Alternatives

* Copy the Account Details tab CSS exactly into Account Preferences under a new wrapper. This is
	the preferred alternative because it reuses the local Radzen pattern and keeps styles scoped.
* Add global `.rz-tabview` rules in `wwwroot/app.css`. Do not prefer this for the requested change:
	Account Details demonstrates that the visual reset is page-specific, and a global rule could
	affect unrelated Configuration or future tab surfaces.
* Replace `RadzenTabs` with plain HTML tabs. Do not prefer this: it discards established keyboard
	behavior already validated in E2E tests and expands scope beyond visual consistency.
* Merge the two Settings panels into one outer card. This could mimic the Account Details outer
	panel more closely, but it changes the current workflow grouping and needs revised structural
	tests. Use it only when a design requirement explicitly requests one card.
* Move Account preferences adjacent to Account details in `MainLayout`. This is a navigation-policy
	decision, not necessary for page visual alignment. It would also change role-group ordering and
	requires explicit expected ordering for multi-role users.

## Suggested Executable Validation

Run the narrow bUnit slice after markup/CSS and test changes:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~MainLayoutTests|FullyQualifiedName~AccountPreferencesTests|FullyQualifiedName~AccountDetailsTests"
```

Run the full Web unit project when the narrow slice passes:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
```

Run the targeted browser suite only when an actual interactive behavior changes:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"
```

The E2E project requires its existing Keycloak, SQL Server, and Aspire-compatible test
infrastructure. For CSS-only tab alignment, the narrow bUnit test plus a manual browser visual
check is the proportionate initial validation.