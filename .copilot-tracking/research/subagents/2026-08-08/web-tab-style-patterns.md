---
title: Web Tab Style Patterns Research
description: Research evidence for subtle line-based tabs in the TNC.Trading.Platform web application
ms.date: 2026-08-08
ms.topic: reference
---

## Research Scope

Investigate existing Blazor and Radzen tab implementations in TNC.Trading.Platform that could support a subtle, line-based tab treatment for the Account details page. Record exact source and style references, established APIs and selectors, accessibility considerations, viable implementation patterns, rejected alternatives, validation options, and unresolved questions.

## Status

Complete.

## Findings

### Existing Account details implementation

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:29-60` is the only tab implementation in the web source. It uses `RadzenTabs` with `@bind-SelectedIndex`, `Change="OnTabChanged"`, and `AriaLabel="Account types"`, with dynamically generated `RadzenTabsItem` children.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:68-120` retains the account-type selection across refresh and history navigation, falling back to the first available group when necessary. A visual change must preserve these bindings and the `@key` on each tab item.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:310-321` styles Account details metadata only. A workspace search found no existing `.rz-tabview`, `.rz-tabview-nav`, `.rz-tabview-selected`, or other Radzen tab CSS override.
* The local visual comparison point is `src/TNC.Trading.Platform.Web/wwwroot/app.css:190-207`: sidebar navigation uses muted text by default and stronger text with a surface background for `.platform-nav-item.active`. It is a useful color hierarchy, but its rounded active container does not match the requested simple-line tabs.

### Established Radzen setup and APIs

* The app references `Radzen.Blazor` 10.3.2 in `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:10`, imports `Radzen.Blazor` in `src/TNC.Trading.Platform.Web/Components/_Imports.razor:9`, registers services through `AddRadzenComponents()` in `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:13`, and loads the theme and script in `src/TNC.Trading.Platform.Web/Components/App.razor:11-16`.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformThemeState.cs:19,73-76` selects Radzen `software` in light mode and `software-dark` in dark mode.
* The installed package documentation at `C:/Users/martynfewtrell/.nuget/packages/radzen.blazor/10.3.2/lib/net10.0/Radzen.Blazor.xml` identifies `SelectedIndex`, `SelectedIndexChanged`, `Change`, `TabPosition`, and `RenderMode` on `RadzenTabs`; `Text`, `Template`, `Selected`, and `Disabled` on `RadzenTabsItem`; and keyboard support for Arrow keys, Home, End, Space, and Enter.
* The installed Software theme files, `C:/Users/martynfewtrell/.nuget/packages/radzen.blazor/10.3.2/staticwebassets/css/software.css:1` and `software-dark.css:1`, expose the relevant selectors and variables. The root wrapper is `.rz-tabview`; headers are `.rz-tabview-nav`; the selected item is `.rz-tabview-selected`; keyboard focus uses `.rz-state-focused` and `:focus-visible`; content uses `.rz-tabview-panels` and `.rz-tabview-panel`. Variables include `--rz-tabs-border`, `--rz-tabs-background-color`, `--rz-tabs-tab-background-color`, `--rz-tabs-tab-selected-background-color`, `--rz-tabs-tab-selected-top-border-color`, and the focus-outline variables.
* The default top-tab rules intentionally form connected, rounded tabs: `.rz-tabview-top > .rz-tabview-nav li` gives each item a border and rounded top corners, and `.rz-tabview-top > .rz-tabview-nav .rz-tabview-selected` joins the selected tab to the bordered panel. This differs from a simple baseline with a selected underline.

### Accessibility and behavior evidence

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:19-65` verifies the `role="tab"` elements, the accessible tab-list name, one selected account type at a time, and correct source ordering.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:66-116` verifies selection survives refresh and pagination and falls back predictably when a type disappears.
* Retain the existing `AriaLabel`, Radzen-managed tab semantics, focus outline, keyboard behavior, and contrast between unselected and selected labels. The visual indicator should not be the only selected-state signal: keep an accent underline plus a color and/or weight change.

## Recommended Pattern

Keep `RadzenTabs` and its current state-management APIs. Add a page-specific class to the Account details tab component, then add a narrow `app.css` ruleset scoped to that class.

The scoped rules should replace the default connected-tab frame with a single shared bottom border, transparent tab backgrounds, square corners, a muted unselected label, a two-pixel accent bottom border for `.rz-tabview-selected`, and a visible focus outline. Use the existing `--platform-border-color`, `--platform-muted-text-color`, `--platform-text-color`, and `--platform-accent-color` tokens so both platform themes remain coherent. Remove or neutralize the default panel border, shadow, and padding only if the account article panels retain enough visual grouping.

This is the smallest change because it preserves the existing `RadzenTabs` accessibility contract, selection persistence, dynamic grouping, and tests. The wrapper class makes the override local to Account details rather than changing all Radzen tabs.

## Alternatives Rejected

* Global changes to Radzen `--rz-tabs-*` variables: rejected because the current page is the only tab consumer and the change would silently establish a platform-wide visual contract.
* Replacing `RadzenTabs` with hand-built buttons and panels: rejected because it duplicates keyboard navigation, ARIA linkage, selected-state management, and the tested selection behavior.
* Using only a selected underline with no label-color or weight difference: rejected because a single visual channel is less resilient for low-vision users and can be unclear on the platform backgrounds.
* Reusing `.platform-nav-item`: rejected because it produces a rounded surface treatment designed for sidebar navigation, not the requested simple line.

## Validation Options

* Run the focused Account details bUnit test file to confirm tab semantics, switching, refresh retention, history retention, and fallback selection remain intact.
* Run the Web unit-test project after any CSS or markup change to catch integration regressions.
* Manually test keyboard navigation in both `software` and `software-dark`: Tab into the list, use Arrow keys, Home, End, Space, and Enter, and confirm the focus outline and selected indicator are visible.
* Use the existing interactive Web application with at least two account types, then verify normal, hover, focused, selected, narrow-viewport, light-theme, and dark-theme states.

## Unresolved Questions

* The requested phrase "simple line designating tabs" does not specify whether the line should run across the entire tab strip or only appear beneath the selected label. The recommended pattern assumes a full-width baseline plus selected underline.
* It is not yet known whether the account content panel should retain its Radzen border and padding after the headers become line-based. Retaining the account article panels is recommended for grouping, but the outer panel treatment needs visual confirmation.
