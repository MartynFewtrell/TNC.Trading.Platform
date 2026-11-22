---
applyTo: 'src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/**,test/TNC.Trading.Platform.Web/**/AccountPreferences*'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Preferences UI Improvement

## Overview

Reorganize Account Preferences into accessible Settings and Observed history tabs while preserving the existing page presenter, API, persistence, radio semantics, and opaque cursor behavior.

## Objectives

### User Requirements

* Make account preference behavior and hierarchy clearer. Source: User-provided research document.
* Correct label emphasis, elevate mismatch warnings, align actions, reduce trailing-stop control dominance, and make history easier to access. Source: User-provided research document.

### Derived Objectives

* Preserve one-way newest-first cursor paging and existing presenter ownership because the API exposes only `NextCursor`. Derived from: `.copilot-tracking/research/2026-09-17/account-preferences-ui-improvement-research.md`.
* Cover rendering and browser interaction separately because repository tests assign bUnit and Playwright distinct responsibilities. Derived from: `.github/instructions/tests.instructions.md` and `.github/instructions/playwright.instructions.md`.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Renders the currently stacked settings, editor, and history workflow.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - Owns page-local choice and action styling.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - Establishes the local accessible `RadzenTabs` pattern.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Focused rendering and lifecycle test suite.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Browser test slice.

### References

* `.copilot-tracking/research/2026-09-17/account-preferences-ui-improvement-research.md` - Supplied requirements, verified UI concerns, and selected implementation path.
* `.copilot-tracking/research/subagents/2026-09-17/account-preferences-ui-plan-verification.md` - Verified implementation anchors and focused validation commands.

### Standards References

* `.github/instructions/radzen-blazor.instructions.md` - Accessible Radzen component and interaction standards.
* `.github/instructions/tests.instructions.md` - bUnit and Playwright test-scope guidance.
* `.github/instructions/playwright.instructions.md` - Locator and browser assertion guidance.

## Implementation Checklist

### [x] Implementation Phase 1: Reframe Account Preferences Workflows

<!-- parallelizable: false -->

* [x] Step 1.1: Add two page-local Radzen tabs, `Settings` and `Observed history`, following the Account Details accessibility and selected-index pattern.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 11-30)
* [x] Step 1.2: Move workflow-specific feedback, state, editor commands, history table, and opaque cursor pager into their owning tab without changing presenter calls or state bindings.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 32-53)
* [x] Step 1.3: Preserve native `InputRadioGroup` semantics while replacing unequal label emphasis with definition-list state markup and bounded choice layout.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 55-72)

### [ ] Implementation Phase 2: Cover the New Page Contract

<!-- parallelizable: true -->

* [x] Step 2.1: Extend bUnit coverage for the selected Settings tab, tab labels, contextual feedback, action groups, and history pager label.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 76-94)
* [ ] Step 2.2: Extend Playwright coverage for tab activation through accessible locators and keyboard navigation, then assert the history table and older-observations control.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 96-113)

### [ ] Implementation Phase 3: Validation

<!-- parallelizable: false -->

* [x] Step 3.1: Run focused Account Preferences bUnit tests.
  * Include a solution-level build before the focused test slice.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 113-121)
* [ ] Step 3.2: Run focused Docker-backed Aspire Playwright tests and inspect desktop and mobile rendering for wrapping and horizontal scrolling.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 123-134)
* [x] Step 3.3: Fix isolated validation failures; log any change that requires API, presenter, or persistence scope expansion.
  * Details: `.copilot-tracking/details/2026-09-17/account-preferences-ui-improvement-details.md` (Lines 136-141)

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-17/account-preferences-ui-improvement-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* Radzen Blazor tabs already referenced by the Web project
* Docker-backed Aspire environment for end-to-end test execution
* Existing Account Preferences test fixtures and authenticated E2E setup

## Success Criteria

* Settings is the initially selected, keyboard-accessible tab; observed history is separately reachable with correctly scoped feedback. Traces to: User requirements and research selected approach.
* Existing desired-setting save, remediation, radio binding, and cursor traversal behavior remains intact. Traces to: Research scope exclusions.
* Focused bUnit and Playwright Account Preferences suites pass, with mobile and desktop layout inspection recorded. Traces to: Research validation impact.