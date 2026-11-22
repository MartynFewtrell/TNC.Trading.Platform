<!-- markdownlint-disable-file -->
---
description: 'Implementation plan for the Account Details tabbed redesign.'
applyTo: 'src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor, src/TNC.Trading.Platform.Web/wwwroot/app.css, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs'
---
# Implementation Plan: Account Details Tabbed Redesign

## Overview

Replace the Account Details retrieval-wide account list with accessible Account type tabs while retaining the existing metadata, retrieval workflows, and Web visual language.

## Objectives

### User Requirements

* Group accounts on separate in-page Account type tabs. Source: user request and supplied research, Task Implementation Requests.
* Preserve retrieval metadata, visual language, the requested header fields, and requested two-column field placement. Source: user request and supplied research, Task Implementation Requests.
* Use researched free Radzen components. Source: user request and supplied research, Component Selection.

### Derived Objectives

* Keep selected Account type as component-local state. Derived from: verification research, lines 17-21; no backend contract change is justified.
* Reconcile selection by normalized type key after every retrieval replacement. Derived from: verification research, lines 20-21.
* Avoid nested `panel` surfaces. Derived from: verification research, Plan-Critical Deviations.
* Prove grouping and tab interaction in focused bUnit tests. Derived from: supplied research, Implementation Validation.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - route, states, retrieval display, refresh, and history navigation (lines 1-50).
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsViewModels.cs` - all requested fields already exist (lines 3-29).
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs` - retrieval transitions only; no tab state (lines 5-51).
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` - shared panel and key-value-grid rules (lines 241-248, 295-308, and 500-502).
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - focused bUnit suite to extend (lines 9-61).

### References

* `.copilot-tracking/research/2026-08-08/account-details-tabbed-redesign-research.md` - supplied component research and selected design.
* `.copilot-tracking/research/subagents/2026-08-08/account-details-tabbed-redesign-verification-research.md` - verified implementation, test baseline, and plan-critical deviations.

### Standards References

* `.github/instructions/radzen-blazor.instructions.md` - use free configured components, accessible interaction, `@key`, and focused testing.
* `.github/instructions/architecture.instructions.md` - keep UI selection outside application and persistence contracts.
* `.github/instructions/tests.instructions.md` - add deterministic documented xUnit/bUnit coverage.

## Implementation Checklist

### [x] Implementation Phase 1: Build Local Tabbed Presentation

<!-- parallelizable: false -->

* [x] Step 1.1: Add normalized Account type grouping and local key-based selection reconciliation in `AccountDetails.razor`.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 10-33)
* [x] Step 1.2: Replace flat account markup with keyed `RadzenTabs`, responsive account panels, visible status badge text, and requested field groups.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 35-54)
* [x] Step 1.3: Keep metadata before tabs and history navigation after tabs; consolidate the duplicate refresh error to one location.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 56-69)
* [x] Step 1.4: Run the focused Account Details test class.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 71-74)

### [x] Implementation Phase 2: Add Responsive Header Styling

<!-- parallelizable: false -->

* [x] Step 2.1: Add only page-specific three-item identity-header grid styling and its narrow-screen stack.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 78-89)
* [x] Step 2.2: Manually verify tab focus, keyboard interaction, and wide/narrow layout.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 91-97)

### [x] Implementation Phase 3: Extend Focused Account Details Tests

<!-- parallelizable: false -->

* [x] Step 3.1: Add documented bUnit scenarios for grouping, active-tab-only rendering, required fields, selection retention, fallback, and navigation location.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 101-116)
* [x] Step 3.2: Add an exact-count failed-refresh error assertion.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 118-130)
* [x] Step 3.3: Run focused tests after each local repair.
  * Details: `.copilot-tracking/details/2026-08-08/account-details-tabbed-redesign-details.md` (Lines 132-135)

### [x] Implementation Phase 4: Validation

<!-- parallelizable: false -->

* [x] Step 4.1: Run the full Web unit-test project and build the Web project.
* [x] Step 4.2: Correct isolated failures, rerun the same full validation commands, and repeat until they pass or a blocker is identified.
* [x] Step 4.3: Report blockers and business-ordering, status-semantics, or deep-linking needs as follow-on work without widening backend scope.

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-08/account-details-tabbed-redesign-log.md` for discrepancies, decisions, alternatives, and follow-on work.

## Dependencies

* Installed `Radzen.Blazor` 10.3.2 and existing Interactive Server rendering.
* Existing bUnit `PlatformComponentTestContext` with Radzen services.

## Success Criteria

* Each normalized Account type has one accessible Radzen tab, whose active panel contains only that type's accounts.
* Metadata remains above tabs; retrieval-history navigation remains below all tab panels; existing query and refresh contracts remain unchanged.
* Each account shows the requested header and responsive profile/financial field groups.
* Focused and full Web unit tests pass, and the Web project builds.