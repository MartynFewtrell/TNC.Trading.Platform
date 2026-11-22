---
title: Account Details Page Ownership Research
description: Research record for the Blazor Account details page ownership and visual structure.
ms.date: 2026-08-08
ms.topic: reference
keywords:
  - Blazor
  - account details
  - page ownership
---

## Research Scope

Identify the owning Blazor Account details page and associated code-behind, models, styles, and tests. Capture 1-based line evidence for trigger metadata, tabs, account identity/status fields, account detail fields, and the styling responsible for the displayed spacing, color, and borders. Product files must remain unchanged.

## Ownership Map

* Visual page owner: `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor`
  * Route and authorization: lines 1-3
  * This file contains the page's inline `@code` block at lines 68-115; no `AccountDetails.razor.cs` code-behind exists.
  * No `AccountDetails.razor.css` file exists. Its local layout is therefore controlled by shared classes in `wwwroot/app.css`.
* Presentation state owner: `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs`
  * Initial retrieval is delegated to `PlatformApiClient.GetAccountDetailsAsync` at line 16.
  * Manual refresh is delegated to `PlatformApiClient.RefreshAccountDetailsAsync` at line 45.
* Page response and account view models: `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsViewModels.cs`
  * `AccountDetailsResponseViewModel`: lines 3-6
  * `AccountDetailsRetrievalViewModel`, including `TriggerSource`: lines 8-13
  * `AccountDetailsAccountViewModel`, including identity, status, type, balance, currency, and transfer properties: lines 15-28
* HTTP boundary used by the presenter: `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
  * Viewer-scoped GET for the saved retrieval: lines 118-128
  * Operator-scoped POST for refresh: lines 130-137
* Discovery and component behavior tests: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs`
  * Initial field and type-tab rendering coverage: lines 20-45
  * Active-tab filtering and metadata/history ordering coverage: lines 48-60
  * Selection-retention coverage after refresh/history retrieval changes: lines 63-98

## Findings

### Rendered Content

* Trigger metadata is rendered in the outer account-details panel at `AccountDetails.razor` line 28. The `Retrieved at`, `Trading day`, and `Trigger` labels bind respectively to `RetrievedAtUtc`, `TradingDay`, and `TriggerSource`.
* Account-type tabs originate at lines 29-60. `accountTypeGroups` groups the retrieved accounts case-insensitively by trimmed `AccountType` at lines 73-76; each group renders a `RadzenTabsItem` at line 33. The selected tab is maintained by lines 90-115.
* Account ID, Alias, and Status display resides in the account article at lines 36-43:
  * Account heading: line 37
  * ID and alias: lines 39-41
  * Status: line 42, using `RadzenBadge` with `BadgeStyle.Info`
* Account type and associated flags are the left `RadzenColumn` at lines 45-48. Currency and monetary fields are the right `RadzenColumn` at lines 50-53.

### Spacing, Color, And Borders

* The containing panel and each account article both use the shared `panel` class (`AccountDetails.razor` lines 27 and 36). `src/TNC.Trading.Platform.Web/wwwroot/app.css` lines 241-248 gives that class:
  * `var(--platform-surface-strong-background)` background
  * `1px solid var(--platform-border-color)` border
  * `1.25rem` border radius and padding
  * `var(--platform-shadow)` shadow
  * `1rem` bottom margin
* The dark palette variables are defined in `app.css` lines 1-16. They produce the dark blue-slate page/surface colors, subdued border, pale text, and cyan accent. `:root[data-platform-theme="light"]` at lines 18-30 substitutes the light palette.
* The trigger and account detail `<dl>` elements use `platform-key-value-grid` (`AccountDetails.razor` lines 28, 46, and 51). `app.css` lines 295-308 makes this a two-column grid with `0.75rem 1rem` gaps, muted labels, and zero-margin values.
* Account ID/Alias/Status use `account-details-account-header` (`AccountDetails.razor` line 38). `app.css` lines 310-321 makes this a three-column grid with the same `0.75rem 1rem` gaps. At viewport widths up to 960px, `app.css` lines 502-504 changes it to one column; the shared key-value grid also becomes one column at lines 515-517.
* Tabs and the Info status badge are not visually overridden in `app.css`. `src/TNC.Trading.Platform.Web/Components/App.razor` line 11 loads `RadzenTheme`, and `src/TNC.Trading.Platform.Web/Components/Layout/PlatformThemeState.cs` lines 73-76 selects Radzen `software` in light mode and `software-dark` in dark mode. That Radzen theme therefore supplies the tab and badge colors, borders, padding, and active-state treatment.

### Recommended Minimal Changes

* For spacing, border, or account-header layout changes limited to this page, add a narrowly named Account details selector to `src/TNC.Trading.Platform.Web/wwwroot/app.css` and apply it to the intended markup in `AccountDetails.razor`. Do not alter `panel` or `platform-key-value-grid` unless the visual change is desired across every page using those shared primitives.
* For tab or status-badge color/border changes, prefer supported Radzen parameters or theme variables. The repository guidance prohibits fragile CSS selectors targeting generated Radzen markup.
* Keep the existing page view models and presenter unchanged for purely visual work. They already own the displayed data and do not determine the described card geometry or theme styling.

### Validation

* Focused behavior test command:

  ```powershell
  dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~AccountDetailsTests
  ```

* The project uses bUnit 1.40.0, xUnit 2.9.3, and `Microsoft.NET.Test.Sdk` 17.14.1 in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` lines 15-20.
* Current tests validate rendered content and tab-selection behavior, but not computed styles, colors, borders, or responsive layout. A visual styling change would need a browser-level visual or DOM-style test if regression coverage for those properties is required.

### Applicable Instructions Reviewed

* `.github/instructions/radzen-blazor.instructions.md`: use free Radzen components, preserve accessible labels, centralize shared theme decisions, prefer parameters/CSS variables over generated-markup overrides, and validate user-visible changes.
* `.github/instructions/csharp.instructions.md`: applied to the inspected presenter and view model C# files.
* `.github/instructions/architecture.instructions.md`: confirms the presenter is an inbound UI adapter and should not acquire business policy for visual work.
* `.github/instructions/tests.instructions.md`: the located test is a deterministic in-process bUnit/xUnit unit test and is the narrowest existing validation target.
* Markdown authoring instructions were applied to this research record.

## Follow-On Questions

* The supplied description did not state the target visual outcome. Is the intended adjustment scoped to the shared panel/grid treatment, only the Account ID/Alias/Status header, or Radzen tab/badge theming?

## Unresolved Questions

* Which theme mode and viewport produced the described appearance? Dark and light modes intentionally use different platform variables and Radzen theme variants.
* Is the expected change page-specific or a product-wide design-system change? `panel` and `platform-key-value-grid` are shared primitives, so changing them has a broader visual impact.
