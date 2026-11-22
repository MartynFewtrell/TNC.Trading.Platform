---
title: Account Preferences UI Alignment Verification Research
description: Workspace verification of supplied Account Preferences UI alignment research.
author: GitHub Copilot
ms.date: 2026-09-17
ms.topic: reference
keywords:
  - account preferences
  - account details
  - blazor
  - radzen
  - ui tests
---

## Research Scope

* Verify the supplied Account Preferences UI Alignment research against the current workspace.
* Inspect cited navigation, Account Preferences and Account Details Razor/CSS, and relevant unit and E2E tests.
* Record minimal implementation steps and focused validation commands without modifying product code.

## Status

Complete.

## Findings

### Supplied research comparison

The supplied research is partially current. Its navigation, Account Details CSS, and
verification-button findings remain accurate. Its Account Preferences markup findings
are superseded by a later UI-improvement implementation: the page now has `Settings`
and `Observed history` tabs, a selected-tab field, `@key` for observation rows, and
visible save-failure feedback. It still lacks the proposed Account Preferences tab
wrapper and matching scoped tab reset.

### Navigation

`src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:19-24` renders the
Viewer-gated `Account details` then `Status` links. Its independent Operator block at
lines 26-30 renders `Configuration` then `Account preferences`. Therefore, a
principal with both roles sees Status and Configuration between the two account
destinations; an Operator-only principal still sees Account Preferences.

The proposed relocation remains minimal and correct only if `Account preferences`
stays in its own Operator-gated block immediately after the Viewer block. Moving it
into the Viewer block would change authorization-driven navigation visibility.

### Account Preferences markup and behavior

`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:2-7`
defines the Operator-authorized route and injects its presenter and access coordinator.
Lines 11-15 retain redirect/loading states and the shared page header. Lines 17-76
already render `RadzenTabs` with a bound `selectedTabIndex`, change callback, and
`AriaLabel="Account preference views"`.

Settings is the initial tab at lines 19-58. It keeps state, verification commands,
the native Boolean editor, Save preference, save error, and confirmation together.
Observed history is isolated in the second tab at lines 60-76; it renders a semantic
table, preserves observation identity with `@key`, and retains the opaque-cursor
command. Lines 80-106 retain existing request lifetime ownership and presenter calls.

The verification buttons at lines 32-33 have no CSS class. Save preference at line 50
uses `platform-primary-action`. The supplied recommendation to apply that existing
class to Retry verification and Apply desired setting remains valid without changing
their click handlers or disabled conditions.

No `account-preferences-tabs` wrapper is present around `RadzenTabs`. The scoped CSS
has no `.rz-tabview` selector, so Account Preferences retains the active Radzen theme
container chrome rather than the Account Details tab presentation.

### Account Details reference

`src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:27-64`
uses a parent `panel`, then an `account-details-tabs` wrapper around named Radzen tabs.
`src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:19-53`
scopes custom properties and generated-markup selectors through that wrapper. The
rules remove tab-view card chrome, add the tab-strip divider and selected underline,
and remove the generated panel treatment.

Account Preferences currently contains sibling `.panel` sections inside its Settings
and history tabs. The supplied recommendation not to add a new outer `.panel` remains
correct: shared `.panel` styling at
`src/TNC.Trading.Platform.Web/wwwroot/app.css:241-248` would create nested card
chrome. Copy only the Account Details tab-specific selectors, replacing the wrapper
name, into Account Preferences scoped CSS.

### Existing tests

`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs:34-57`
checks signed-in Viewer navigation and Account Details but neither Account Preferences
visibility nor relative link order. It is the appropriate focused unit-test file for
the navigation move.

`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:73-150`
already protects live rendering, tab labels, the selected Settings tab, state markup,
and deferred history rendering. Lines 154-177 cover contextual save failure and named
action groups; lines 181-201 cover history activation and the older-observations
command. These tests do not assert the proposed tab wrapper or that all three commands
use the primary action class.

`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:53-70`
asserts its reference wrapper and generated Radzen tab view. It provides a local
structural test pattern for an Account Preferences wrapper assertion.

`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:21-103`
already verifies the current two-tab experience with accessible tab locators,
Space/ArrowRight keyboard activation, a named table, and disabled cursor behavior.
It contains no visual-style or primary-action assertions. Its project at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:3-30`
uses Aspire hosting and Playwright, so Docker-backed infrastructure is required.

### Recommended minimal implementation

1. In `MainLayout.razor`, move the existing Operator-gated Account Preferences item to
  a standalone Operator condition directly after the Viewer block. Leave
  Configuration in the following Operator block and preserve all `Href`, icon, and
  authorization checks.
2. In `AccountPreferences.razor`, surround the existing `RadzenTabs` with an
  `account-preferences-tabs` `div`. Do not add a panel or alter tab state, content,
  action callbacks, disabled conditions, or history paging.
3. In `AccountPreferences.razor.css`, port only Account Details' scoped tab-view
  variables, nav border, selected underline, and tab-panel reset, changing the scope
  to `.account-preferences-tabs`.
4. Add `platform-primary-action` to the existing Retry verification and Apply desired
  setting buttons.
5. Extend `MainLayoutTests` for Viewer-only, Operator-only, and combined-role
  visibility/order. Extend `AccountPreferencesTests` for the wrapper and primary
  class on all three commands. Preserve the existing accessible-tab E2E coverage;
  add a browser assertion only if the final visual requirement needs a real-browser
  regression check.

### Focused validation

Run the component/navigation test slice after the changes:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MainLayoutTests|FullyQualifiedName~AccountPreferencesTests|FullyQualifiedName~AccountDetailsTests"
```

Build the owning Web project:

```powershell
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore
```

When Docker and the Aspire-backed E2E environment are available, run the focused
browser suite:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesE2ETests"
```

### Verification result

The focused bUnit files were executed on 2026-09-17 with the workspace test runner:
`MainLayoutTests.cs`, `AccountPreferencesTests.cs`, and `AccountDetailsTests.cs`.
All 41 tests passed. The run verifies current behavior but cannot prove the pending
navigation order, wrapper, or action-class requirements because their assertions do
not yet exist.

## Follow-On Questions

* The supplied alignment research refers to visual consistency but provides no
  acceptance screenshot or measurable visual specification. Is matching the existing
  Account Details tab reset sufficient, or should a browser visual regression test
  also compare a specific desktop and mobile target?
