---
applyTo: '.copilot-tracking/changes/2026-08-31/account-preferences-ui-clarity-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Preferences UI Clarity

## Overview

Deliver an explicit accessible Boolean selector, clear pending and provider-confirmed state, and dismissible save feedback through focused test-first Web presentation slices without changing backend contracts.

## Objectives

### User Requirements

* Replace account-specific introductory text with `Live trailing stops control for the configured account.` - Source: `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md` (Lines 6-11)
* Replace the checkbox with a control that displays Enabled and Disabled while retaining the Boolean backend contract - Source: same research task requests
* Render `Trailing stops enabled` or `Trailing stops disabled` from the current selection - Source: same research task requests
* Replace persistent save text with a dismissible popup or boxed notification - Source: same research task requests
* Convert the selected research approach into an implementation-ready delivery sequence without modifying production or tests during planning - Source: user request and `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md` (Lines 6-18)

### Derived Objectives

* Distinguish unconfirmed, pending, and provider-confirmed values - Derived from: preventing the default `false` value from becoming actionable after load failure
* Separate transient save feedback from durable load, history, and application status - Derived from: selection clearing and partial-success requirements plus the presenter's shared error state
* Preserve explicit unchanged saves - Derived from: confirmed writes currently create valid observations and no product requirement changes that behavior
* Keep implementation within the Web presentation adapter, focused bUnit tests, and two affected wiki sections - Derived from: verified repository ownership and unchanged API/application contracts
* Treat reflow, browser focus, keyboard behavior, and screen-reader announcements as manual completion gates - Derived from: bUnit and current fixture limitations

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` - Add pending and confirmed presentation state
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Separate feedback ownership and correct save/history transitions
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Replace copy, checkbox, and feedback markup; add focus restoration
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - New scoped segmented selector and responsive status styling
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Extend deterministic state, transition, payload, and semantic coverage
* `docs/wiki/operator-guide.md` - Align operator workflow documentation
* `docs/wiki/testing-and-quality.md` - Align deterministic test coverage documentation

### References

* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md` - Selected control, state, and status patterns
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md` - Canonical delivery research and validation strategy
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-plan-verification-research.md` - Current anchors, 10-test baseline, and sequence corrections

### Standards References

* `.github/copilot-instructions.md` - Repository-wide architecture, test traceability, wiki, and validation rules
* `.github/instructions/architecture.instructions.md` - Web adapter responsibility and dependency direction
* `.github/instructions/radzen-blazor.instructions.md` - Blazor and semantic-control selection conventions
* `.github/instructions/tests.instructions.md` - Test naming, traceability comments, and validation conventions
* `.github/instructions/playwright.instructions.md` - Browser-test fixture and reliability constraints
* `.github/instructions/docs.instructions.md` - Wiki documentation requirements
* `.github/instructions/refactoring.instructions.md` - Small behavior-preserving delivery slices

## Implementation Checklist

### [x] Implementation Phase 0: Establish Focused Baseline

<!-- parallelizable: false -->

* [x] Step 0.1: Run and record the existing account-preferences bUnit baseline
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 12-30)

### [x] Implementation Phase 1: Define Pending and Confirmed State

<!-- parallelizable: false -->

* [x] Step 1.1: Add failing tests for unconfirmed, pending, confirmed, and dirty state
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 36-55)
* [x] Step 1.2: Implement the explicit page-state contract
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 57-74)
* [x] Step 1.3: Rerun the focused account-preferences test class
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 76-81)

### [x] Implementation Phase 2: Correct Presenter Transitions and Feedback Ownership

<!-- parallelizable: false -->

* [x] Step 2.1: Add failing transition tests for selection, HTTP 409, cancellation, dismissal, and partial success
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 87-109)
* [x] Step 2.2: Separate feedback categories and implement corrected presenter transitions
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 111-129)
* [x] Step 2.3: Rerun the focused account-preferences test class
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 131-136)

### [x] Implementation Phase 3: Deliver Semantic Component Behavior

<!-- parallelizable: false -->

* [x] Step 3.1: Replace checkbox assertions with failing semantic and payload tests
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 142-162)
* [x] Step 3.2: Implement exact copy, state heading, explicit radios, and safe conditional rendering
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 164-182)
* [x] Step 3.3: Add persistent dismissible confirmation and Save focus restoration
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 184-200)
* [x] Step 3.4: Rerun the focused account-preferences test class
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 202-207)

### [x] Implementation Phase 4: Add Scoped Responsive Styling

<!-- parallelizable: false -->

* [x] Step 4.1: Add page-scoped segmented selector, focus, status, and narrow-layout styles
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 213-230)
* [x] Step 4.2: Run focused tests, all Web unit tests, Web build, and format checks
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 232-241)

### [x] Implementation Phase 5: Align Wiki Documentation

<!-- parallelizable: true -->

* [x] Step 5.1: Update operator guidance for the delivered workflow
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 247-262)
* [x] Step 5.2: Update testing guidance for deterministic coverage and manual boundaries
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 264-279)
* [x] Step 5.3: Join documentation edits and run existing Markdown checks
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 281-288)

### [ ] Implementation Phase 6: Complete Repository and Accessibility Validation

<!-- parallelizable: false -->

* [x] Step 6.1: Run the existing repository quality gate and retain evidence
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 294-312)
* [ ] Step 6.2: Validate browser keyboard behavior and reflow at desktop, 320 CSS pixels, and 400 percent zoom
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 314-331)
* [ ] Step 6.3: Verify announcements, naming, checked state, and focus with NVDA and Narrator
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 333-347)
* [x] Step 6.4: Fix isolated issues and report wider blockers as follow-on work
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 349-355)

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-31/account-preferences-ui-clarity-log.md` for discrepancy tracking, implementation paths considered, the unchanged-save decision, and suggested follow-on work.

## Implementation Risks

* R-01: Feedback separation regresses load/history warnings - Medium likelihood, high impact; Phase 2 owner mitigates with transition-first tests and focused reruns
* R-02: Pending selection is presented as provider-confirmed - Medium likelihood, high impact; Phases 1 through 3 owner mitigates with separate state and HTTP 409/partial-success tests
* R-03: Segmented styling hides focus or breaks reflow - Medium likelihood, medium impact; Phases 3, 4, and 6 owner mitigates with native semantics and manual keyboard/zoom gates
* R-04: Distributed prerequisites block final evidence - Medium likelihood, medium impact; Phase 6 owner retains failed evidence and uses only the documented child-node fallback
* R-05: Manual validation captures sensitive data - Low likelihood, high impact; Phase 6 owner sanitizes evidence and stops before recording credentials or account identifiers

Full triggers and mitigations: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 357-388).

## Rollback Boundaries

* Phase 0 is read-only
* Phase 1 reverts view-model state and direct tests only
* Phase 2 reverts presenter transitions and their tests while retaining Phase 1
* Phase 3 reverts Razor behavior and component assertions while retaining state and presenter contracts
* Phase 4 removes scoped styling while preserving native radio semantics
* Phase 5 reverts the two wiki sections when runtime behavior is backed out
* Phase 6 retains validated code for environmental failures and reverts only isolated fixes that introduce regressions

Detailed boundaries: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 390-398).

## Implementation Handoff

* Start: Run Phase 0 from the repository root, then execute Phases 1 through 6 in order
* Parallel work: Run only Phase 5 Steps 5.1 and 5.2 concurrently, then join for Step 5.3
* Evidence: Record focused and Web gates, quality-gate artifacts, and sanitized manual browser/accessibility results
* Stop conditions: Stop after three failed repairs, before widening contract boundaries, or before exposing sensitive data; record blockers and request new planning when scope must expand
* Resolved decision: Preserve unchanged saves because confirmed writes create valid observations
* Detailed handoff: `.copilot-tracking/details/2026-08-31/account-preferences-ui-clarity-details.md` (Lines 400-417)

## Dependencies

* .NET SDK 10.0.200 from `global.json`
* Existing Blazor, Radzen Blazor 10.3.2, bUnit, and xUnit dependencies
* Existing sequenced HTTP test handler for outbound Boolean assertions
* Docker and distributed-test prerequisites for the final repository quality gate
* Configured local Test account for manual provider scenarios, with no credentials captured in artifacts
* NVDA, Narrator, Firefox or Chrome, and Edge for manual accessibility validation

## Success Criteria

* Both Enabled and Disabled are always visible after confirmed load, and exactly one maps to the existing Boolean contract - Traces to: user explicit-control requirement
* The heading reflects pending selection while last-confirmed text changes only after authoritative success - Traces to: user state-heading requirement and pending/confirmed derived objective
* Initial preference-load failure exposes no editable default Boolean or Save action - Traces to: safe-load research finding
* Update failure and HTTP 409 preserve confirmed state; confirmed save plus history failure produces qualified success only - Traces to: DD-01 and DD-02
* Confirmation is persistent, atomic, dismissible, separate from application status, and returns browser focus to Save - Traces to: user notification requirement and DR-01 manual gate
* Focused tests, Web gates, repository quality gate, responsive checks, keyboard checks, and assistive-technology checks produce completion evidence - Traces to: delivery-plan validation request
* `docs/wiki/operator-guide.md` and `docs/wiki/testing-and-quality.md` describe delivered behavior before completion - Traces to: repository wiki source-of-truth requirement
* No API, application, infrastructure, persistence, configuration, schema, database, package, or project contract changes are introduced - Traces to: verified implementation boundary
