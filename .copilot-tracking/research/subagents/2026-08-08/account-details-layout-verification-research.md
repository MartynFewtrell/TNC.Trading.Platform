---
title: Account Details Layout Verification Research
description: Repository verification for the proposed Account details visual layout update.
ms.date: 2026-08-08
ms.topic: reference
---

## Research Scope

* Verify the Account details layout research claims against the current repository.
* Identify the owning Razor markup and styles, focused tests and command, and applicable Web UI and test constraints.

## Status

Complete.

## Verified Ownership And Anchors

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:25-29`
  owns the retrieval metadata, including Trigger, followed immediately by `RadzenTabs`. This is the direct markup boundary for the requested vertical gap.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:36-53`
  owns each account card. The account identity `<dl>` contains Account ID, Alias, and Status at
  lines 38-42, and the Account type/Currency `RadzenRow` follows at lines 44-53.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:29-60`
  is the only `RadzenTabs` usage in the Web project. Preserve its `@bind-SelectedIndex`,
  `Change`, `AriaLabel`, dynamic group loop, and `@key` values.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:295-321` owns the existing shared
  `platform-key-value-grid` and `account-details-account-header` layout rules. The current
  account header is a three-column grid with labels above values.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:502-517` owns responsive collapse for the
  account header and shared key/value grid below the established mobile breakpoint.
* `src/TNC.Trading.Platform.Web/Components/App.razor:9-16` loads `app.css`, the Radzen theme,
  and the Radzen script. `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:8-14`
  confirms the Web SDK and `Radzen.Blazor` 10.3.2.
* No `AccountDetails.razor.css` currently exists. Therefore, the supplied research statement
  that layout is owned by the Razor page and CSS is correct, with one qualification: existing
  CSS ownership is global `wwwroot/app.css`, not a page-local stylesheet.

## Plan Implications

* Limit the visual update to `AccountDetails.razor` and a new colocated
  `AccountDetails.razor.css`. Add narrow layout hooks for the retrieval metadata and the tab
  container, while retaining the existing state and data flow.
* Use the existing `account-details-account-header` class for desktop label/value pairs and
  retain its one-column small-screen behavior. Add a local lower gap before the existing
  `RadzenRow` rather than changing the shared `platform-key-value-grid`.
* Scope any Radzen rendered-markup selectors below the new page-local tabs container via
  `::deep`. This confines the transparent tab navigation, baseline, selected underline, and
  unframed tab panel treatment to Account details. Do not change global Radzen selectors or
  the shared `panel` utility.
* Use existing platform tokens from `app.css:1-35`, including `--platform-border-color`,
  `--platform-text-color`, `--platform-muted-text-color`, and `--platform-accent-color`, so
  both configured themes retain consistent contrast.

## Relevant Constraints

* `.github/instructions/radzen-blazor.instructions.md` applies to the planned Razor and CSS.
  It requires preserving semantic structure, keyboard navigation, focus visibility, and existing
  Radzen interaction behavior. It prefers supported component parameters and CSS variables;
  scoped generated-markup selectors are appropriate only where required for the desired tab
  baseline and underline.
* `.github/instructions/tests.instructions.md` and
  `.github/instructions/csharp.instructions.md` apply to any updated or added C# test. Tests
  must remain xUnit/bUnit unit tests, use the prescribed naming convention, and include a
  requirement-focused comment above each test method.
* `.github/instructions/playwright.instructions.md` applies only if visual regression coverage
  is introduced. Such coverage must use accessible locators and web-first assertions, with no
  hard-coded waits. It is not required for this presentation-only change when bUnit markup
  coverage and manual responsive/theme review are sufficient.

## Focused Validation

The focused coverage is
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:7-116`.
It verifies Account type grouping, tab accessibility and selection, refresh/history selection
retention, content order, and error behavior. It does not verify computed CSS geometry,
responsive layout, or color treatment.

Run the following after implementation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~AccountDetailsTests
```

Baseline result on 2026-08-08: passed, 7 of 7 tests. The run emitted existing package security
and nullable-reference warnings outside the Account details layout scope.

Add one focused bUnit assertion for the new layout-hook classes and retain the existing tab
behavior assertions. Then manually review desktop and small-screen layouts in light and dark
themes, confirming both requested gaps, horizontal summary pairs, keyboard tab navigation,
visible focus, selected-tab persistence, and the absence of an extra tab-content frame.

## Clarifying Question

The supplied research proposes `1.5rem` structural gaps and a `0.5rem` label/value gap. The
repository defines no Account details-specific spacing token or acceptance image with measurable
dimensions. Confirm whether these starting values are acceptable, or provide a target screenshot
or spacing specification for final visual tuning.
