---
applyTo: 'src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor, src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor, src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Preferences UI Alignment

## Overview

Align Account Preferences navigation, tab presentation, and verification actions with
the existing Account Details and shared Web UI patterns without changing behavior or
authorization.

## Objectives

### User Requirements

* Place Account preferences directly beneath Account details in main navigation.
  Source: user request and account-preferences-ui-alignment-research.md.
* Align the Account preferences shell and tabs with Account details. Source: user
  request and account-preferences-ui-alignment-research.md.
* Align verification-action buttons with Save preference. Source: user request and
  account-preferences-ui-alignment-research.md.

### Derived Objectives

* Preserve independent Viewer and Operator navigation visibility because each route
  has a distinct authorization boundary. Derived from: research verification,
  Navigation findings.
* Scope Radzen tab styling to Account Preferences and preserve existing keyboard tab
  behavior. Derived from: research verification, Account Details reference and
  existing E2E coverage.
* Add structural unit assertions for each requested visual contract before validating
  the Web project. Derived from: research verification, Existing tests.

## Context Summary

### Project Files

* src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor - owns role-gated
  primary navigation.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor - owns preferences tabs and action controls.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css - owns isolated page styling.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css - local tab-presentation reference.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs - navigation visibility test surface.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs - preferences structural test surface.

### References

* .copilot-tracking/research/2026-09-17/account-preferences-ui-alignment-research.md - supplied task research and implementation recommendation.
* .copilot-tracking/research/subagents/2026-09-17/account-preferences-ui-alignment-verification-research.md - current-workspace verification, test gaps, and commands.

### Standards References

* .github/copilot-instructions.md - repository-wide Web and test conventions.
* .github/instructions/radzen-blazor.instructions.md - Radzen accessibility and scoped styling guidance.
* .github/instructions/tests.instructions.md - automated test conventions.

## Implementation Checklist

### [x] Implementation Phase 1: Preserve Navigation Authorization and Order

<!-- parallelizable: false -->

* [x] Step 1.1: Move the existing Operator-gated Account preferences navigation item
  to its own block immediately after the Viewer-gated Account details block.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 11-34)
* [x] Step 1.2: Extend navigation unit coverage for Viewer-only, Operator-only, and
  combined-role visibility and link order.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 36-61)
* [x] Step 1.3: Validate the focused navigation tests.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 63-72)

### [x] Implementation Phase 2: Align Account Preferences Presentation

<!-- parallelizable: false -->

* [x] Step 2.1: Wrap the existing Radzen tabs in the isolated Account Preferences
  styling hook without changing tab state or content.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 78-101)
* [x] Step 2.2: Port only the Account Details tab-reset selectors into the Account
  Preferences stylesheet using the new wrapper scope.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 103-130)
* [x] Step 2.3: Apply the existing primary-action class to both verification buttons
  and assert the wrapper and all three action classes in unit tests.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 132-159)
* [x] Step 2.4: Validate the focused Account Preferences and Account Details test
  slice.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 161-171)

### [x] Implementation Phase 3: Final Validation

<!-- parallelizable: false -->

* [x] Step 3.1: Run the full focused Web unit-test slice and build the owning Web project.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 177-192)
* [x] Step 3.2: Run the Account Preferences E2E suite when Docker and Aspire-backed
  test infrastructure are available.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 194-207)
* [x] Step 3.3: Fix minor validation failures, or record larger issues for additional
  research and planning.
  * Details: .copilot-tracking/details/2026-09-17/account-preferences-ui-alignment-details.md (Lines 209-218)

## Planning Log

See .copilot-tracking/plans/logs/2026-09-17/account-preferences-ui-alignment-log.md
for discrepancy tracking, implementation paths considered, and suggested follow-on
work.

## Dependencies

* .NET SDK specified by global.json and restored Web projects
* Existing Radzen Blazor components and isolated CSS support
* Docker and Aspire-backed infrastructure for optional focused E2E execution

## Success Criteria

* A combined Viewer and Operator principal sees Account preferences immediately after
  Account details, while Operator-only navigation continues to show Account
  preferences. Traces to: user navigation requirement and research Navigation findings.
* Account Preferences renders its existing two tabs within an isolated wrapper using
  the Account Details tab-reset pattern without nested panels. Traces to: user shell
  and tab alignment requirement.
* Retry verification, Apply desired setting, and Save preference all use the shared
  platform-primary-action class. Traces to: user verification-action requirement.
* Focused unit tests and the Web build pass; E2E coverage passes when its infrastructure is available. Traces to: research Focused validation.