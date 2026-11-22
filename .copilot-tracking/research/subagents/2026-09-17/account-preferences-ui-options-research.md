---
title: Account Preferences UI Options Research
description: Evidence-based implementation alternatives for consolidating Account Preferences navigation and action styling.
author: GitHub Copilot
ms.date: 2026-09-17
ms.topic: concept
keywords:
  - account preferences
  - blazor
  - authorization
  - accessibility
estimated_reading_time: 4
---

## Research Scope

Evaluate the reported Account Preferences UI changes without modifying product code:

* Place Account preferences directly under Account details while preserving Viewer and Operator authorization boundaries
* Reuse `.platform-primary-action` for Retry verification and Apply desired setting, including whether existing semantic secondary or danger styling is needed
* Select between a tab wrapper with CSS reset only and a surrounding panel matching Account Details

## Status

Complete. The selected approach keeps policy enforcement intact, aligns action styling with
existing semantics, and removes nested Radzen tab chrome without adding an unnecessary outer
panel.

## Evidence

* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:19-27` has separate
  conditional groups. Account details is under `operatorContext.IsViewer`; Account preferences
  is under `operatorContext.IsOperator` after Configuration.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:3`
  applies the Viewer policy. Its extra Refresh action is independently scope-checked at
  lines 63-65, demonstrating localized authorization without changing the page policy.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:4`
  applies the Operator policy; lines 100-102 also require the Operator scope before loading.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:195-199`
  gives `local-operator` only the Operator scope and `local-viewer` only the Viewer scope.
  Therefore, nesting the preferences link inside the Viewer block would hide it from a normal
  operator test principal.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:30-31`
  renders Retry verification and Apply desired setting. Lines 113-114 delegate to presenter
  operations.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:93-112`
  shows Retry requests verification again, while Remediate applies the retained desired setting
  and reports completion. Both are recovery or forward-progress actions, not destructive actions.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:277-287` defines the sole shared action class,
  `.platform-primary-action`. The stylesheet has no `.platform-secondary-action` or destructive
  button class. `--platform-danger-color` at lines 12 and 31 is consumed by `.error` at
  lines 447-449 and severity badges, not commands.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:27-59`
  has one outer `account-details-panel` around retrieval metadata and account-type tabs.
  Its `account-details-tabs` wrapper at line 29 supplies scoped Radzen visual resets in
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:19-54`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:19-81`
  already contains one panel per tab workflow. Adding a panel around the whole `RadzenTabs`
  would frame those panels inside another panel. The current page has no wrapper that removes
  Radzen's default tabview background, border, padding, or shadow.

## Alternatives And Decision

### Navigation Order

Rejected: move Account preferences into the existing `if (operatorContext.IsViewer)` block.
This creates an unintended Viewer-and-Operator requirement and hides the link for the distinct
Operator-only principal used by the component test context.

Rejected: retain the current grouping. It preserves authorization but leaves Account preferences
visually separated from Account details by Status and Configuration.

Selected: move only the existing preferences navigation item to an independent Operator block
immediately after the Viewer Account details block, then render Status in its own Viewer block.
The exact minimal ordering is:

```razor
@if (operatorContext.IsViewer)
{
    <PlatformNavItem Href="/account-details" Icon="account_balance" Text="Account details" />
}

@if (operatorContext.IsOperator)
{
    <PlatformNavItem Href="/account-preferences" Icon="tune" Text="Account preferences" />
    <PlatformNavItem Href="/configuration" Icon="settings" Text="Configuration" />
}

@if (operatorContext.IsViewer)
{
    <PlatformNavItem Href="/status" Icon="monitor_heart" Text="Status" />
}
```

This is the smallest markup relocation that makes preferences directly follow details whenever
both roles exist, keeps its Operator gate independent, and does not alter the page-level
`[Authorize]` policy. For the established mutually exclusive local identities, Viewer sees
Account details then Status; Operator sees Account preferences then Configuration.

### Action Styling

Rejected: leave Retry verification and Apply desired setting unclassified. They currently use
browser-default button styling while Save preference at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:50`
uses the shared primary action.

Rejected: add a secondary or danger action class in this change. The shared stylesheet offers no
such command semantic, and the operations are not cancel, delete, or other destructive actions.
Creating a semantic system for one page would expand scope without an identified conflicting
action.

Selected: add `class="platform-primary-action"` to both commands at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:30-31`.
They are the principal available resolution actions when displayed. Retain the existing disabled
state, `role="group"`, and accessible group name. A future secondary action should be introduced
globally only when a non-primary, non-destructive command has a concrete product use case; a
danger style should be reserved for destructive or irreversible operations.

### Tabs And Panels

Rejected: surround the preferences tabs with a `.panel` modeled literally on Account Details.
Unlike Account Details, which combines retrieval metadata and tabs in one composite surface,
each Account Preferences tab already owns a `.panel`. The result would be nested panels and
duplicated border, shadow, padding, and bottom margin from
`src/TNC.Trading.Platform.Web/wwwroot/app.css:241-248`.

Selected: wrap the existing `RadzenTabs` in `<div class="account-preferences-tabs">` and add
the scoped CSS reset equivalent to Account Details:

* Transparent tabview and tab backgrounds
* No tabview border, shadow, or panel border
* Top padding of `1.25rem 0 0`
* Bottom rule on the tab list and accent bottom border for the selected tab
* Existing muted, selected, and hover tab text colors

This preserves the page's two single-level workflow panels while making the tabs visually
consistent with Account Details. No presenter, route, authorization, or API change is needed.

## Accessibility And Testing

* Preserve the existing tab `AriaLabel="Account preference views"` at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:20`,
  native radio group, named command groups, live status, and error alerts. A wrapper `div` has
  no semantic effect.
* Preserve keyboard focus styling. The page-specific focus selector at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:115-117`
  continues to cover action buttons after class assignment.
* Add a focused `MainLayoutTests` assertion for both role states: Viewer has Account details and
  no Account preferences; Operator has Account preferences and no Account details. Add ordering
  assertions only for a test principal explicitly granted both scopes if the test harness gains
  one, because its default identities are mutually exclusive.
* Extend `AccountPreferencesTests.Render_ShouldShowSettingsInformationArchitecture_WhenInitialLoadSucceeds`
  at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:115-133`
  to assert the `.account-preferences-tabs` wrapper and both action buttons have
  `platform-primary-action`.
* Existing browser coverage already validates tab semantics and keyboard navigation at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:36-78`.
  No E2E test is required for CSS-only changes, but run the focused suite when the markup moves.
* Existing page authorization coverage remains relevant: the viewer denial test is at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:549-555`.

## Validation Commands

Run the fast component tests after implementation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~MainLayoutTests|FullyQualifiedName~AccountPreferencesTests"
```

Run the focused browser suite when the Aspire-backed test environment is available:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"
```

Build the Web project to validate Razor and scoped CSS compilation:

```powershell
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj
```
