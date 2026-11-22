---
applyTo: 'src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor, src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Details Layout Refinements

## Overview

Update the Account details page with local layout hooks and scoped Radzen tab styling to improve spacing, summary alignment, and visual hierarchy without changing account data or tab behavior.

## Objectives

### User Requirements

* Add space between the Trigger metadata row and the CFD/Spreadbet tabs. Source: user request and `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` lines 8-11.
* Render Account ID, Alias, and Status values beside their labels and separate this summary from Account type/Currency details. Source: user request and `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` lines 8-11.
* Replace the heavy tab surround with a subtle page-integrated treatment informed by the Radzen Tabs reference. Source: user request and `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` lines 8-11.

### Derived Objectives

* Preserve `RadzenTabs` bindings, ARIA label, keyed tab loop, active selection retention, and retrieval/data behavior. Derived from: the behavioral contract at `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` lines 33-37.
* Isolate visual rules in an Account details CSS-isolation file and retain shared `app.css` utilities unchanged. Derived from: localized ownership findings at `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` lines 73-76.
* Verify new markup hooks with focused bUnit coverage, then manually inspect responsive light and dark layouts. Derived from: validation guidance at `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` lines 219-226.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - Owns retrieval metadata, the dynamic tab loop, the identity/status summary, and detail columns.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css` - New CSS-isolation file for page-only layout and Radzen rendered-DOM styling.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` - Defines platform tokens and shared layouts; remains unchanged.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - Focused bUnit coverage for tab behavior and new markup hooks.

### References

* `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` - Primary task research and selected path.
* `.copilot-tracking/research/subagents/2026-08-08/account-details-layout-verification-research.md` - Verified source anchors and passing baseline test result.
* `https://blazor.radzen.com/tabs` - Visual hierarchy reference.

### Standards References

* `.github/copilot-instructions.md` - Focused changes and test conventions.
* `.github/instructions/radzen-blazor.instructions.md` - Radzen behavior and accessibility.
* `.github/instructions/tests.instructions.md` - Test organization and assertions.

## Implementation Checklist

### [x] Implementation Phase 1: Add Page Layout Hooks And Coverage

<!-- parallelizable: false -->

* [x] Step 1.1: Add `account-details-retrieval` to the retrieval `<dl>` and wrap existing `RadzenTabs` in `account-details-tabs`.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 12-33.
* [x] Step 1.2: Retain the Account ID, Alias, and Status markup within `account-details-account-header`, and add focused bUnit assertions for the new hooks.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 35-53.
* [x] Step 1.3: Run focused Account details bUnit tests after the markup change.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 55-61.

### [x] Implementation Phase 2: Apply Account-Scoped Visual Treatment

<!-- parallelizable: false -->

* [x] Step 2.1: Create `AccountDetails.razor.css` with desktop label/value grids, structural spacing, and the <=960px collapse.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 67-91.
* [x] Step 2.2: Scope Radzen tab variables and `::deep` selectors under `account-details-tabs` to remove the frame and render a baseline and selected underline.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 93-116.
* [x] Step 2.3: Re-run focused Account details bUnit tests after stylesheet introduction.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 118-123.

### [ ] Implementation Phase 3: Validation

<!-- parallelizable: false -->

* [x] Step 3.1: Run focused tests and build the Web project.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 129-139.
* [ ] Step 3.2: Manually verify desktop and <=960px rendering in both themes, including focus/keyboard behavior and selection persistence.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 141-155.
* [x] Step 3.3: Record blockers requiring a design decision or Radzen package-DOM reassessment instead of broadening scope.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-layout-refinements-details.md` lines 157-162.

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-08/account-details-layout-refinements-log.md` for discrepancy tracking, implementation paths, and follow-on work.

## Dependencies

* .NET SDK version pinned by `global.json`.
* `Radzen.Blazor` 10.3.2 markup and the existing `software` / `software-dark` themes.
* Docker-backed local environment only when visual testing requires running the composed app.

## Success Criteria

* Trigger metadata and Account type tabs have a visible page-rhythm gap without retrieval changes. Traces to: User Requirement 1.
* Account ID, Alias, and Status display as desktop label/value pairs, collapse coherently at <=960px, and are separated from lower details. Traces to: User Requirement 2.
* Account details tabs have transparent headers/content, a low-contrast baseline, and an accent selected underline in both themes, without changing shared styles. Traces to: User Requirement 3.
* Focused bUnit tests pass and manual checks confirm keyboard navigation, focus visibility, selection persistence, and responsive visual intent. Traces to: Derived Objective 3.