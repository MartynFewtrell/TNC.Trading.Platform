---
applyTo: 'test/TNC.Trading.Platform.Web/**/*, src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/**/*'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Failing Test Resolution

## Overview

Resolve the two reproduced Account Preferences E2E failures by protecting the existing failure-summary rendering contract, observing the browser operation before changing product code, and replacing assertions contradicted by the current UI contract.

## Objectives

### User Requirements

* Create a plan to resolve the failing tests. Source: user request on 2026-09-20.
* Base the plan on the supplied failing-test investigation. Source: .copilot-tracking/research/2026-09-20/failing-tests-resolution-research.md.

### Derived Objectives

* Preserve the HTTP 200 failure-state API contract and avoid speculative Web implementation changes. Derived from: verified source-path and functional-test evidence.
* Assert the operator-visible unavailable outcome rather than a gateway-path-specific sentence. Derived from: distinct supported safe summaries.
* Assert responsive usability rather than a same-row desktop implementation detail. Derived from: the current wrapping CSS contract.
* Defer the unverified third report until its test name and retained failure output are available. Derived from: no current evidence identifies it.

## Context Summary

### Project Files

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:65-136 - Both failing browser assertions and shared viewport setup.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:155-180 - Existing initial-load warning coverage; extension point for Check status coverage.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:103-110 - Applies check-status response to page state.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:20-37 - Copies FailureSummary into rendered state.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-20, 105-106 - Renders warning and invokes Check status.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:76-93 - Vertical and wrapping choice-control contract.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:70-100 - Unavailable-provider response contract.

### References

* .copilot-tracking/research/2026-09-20/failing-tests-resolution-research.md - Original failure investigation.
* .copilot-tracking/research/subagents/2026-09-20/failing-tests-resolution-verification.md - Verified source path, reproduction, and risks.

### Standards References

* .github/copilot-instructions.md - Evidence-based changes and test naming requirements.
* .github/instructions/tests.instructions.md - Automated-test requirements.
* .github/instructions/playwright.instructions.md - Playwright locator and flake-avoidance requirements.

## Implementation Checklist

### [ ] Implementation Phase 1: Protect And Observe The Check-Status Contract

<!-- parallelizable: false -->

* [x] Step 1.1: Add a focused Web unit test for a successful VerificationFailed check-status response with a non-empty FailureSummary.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 12-36)
* [x] Step 1.2: Make the unavailable-provider E2E test wait for and inspect the POST check-status request/result before asserting the warning.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 38-63)
* [ ] Step 1.3: Apply a product correction only if completed-request evidence identifies the controlling failure path; otherwise update the E2E expectation semantically.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 65-96)
* [ ] Step 1.4: Run focused unit, functional, and warning E2E validation.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 94-102)

### [x] Implementation Phase 2: Align Responsive Layout Coverage

<!-- parallelizable: false -->

* [x] Step 2.1: Obtain an Account Preferences product/design-owner decision, recorded in an approved work item or design artifact, confirming the accepted 1280px choice-layout behavior; then replace unsupported same-row geometry checks with semantic visibility, usable non-overlap, Save placement, and overflow assertions.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 108-133)
* [x] Step 2.2: Run the isolated desktop layout E2E test.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 135-139)

### [x] Implementation Phase 3: Final Validation And Evidence Retention

<!-- parallelizable: false -->

* [x] Step 3.1: Run full Web unit, functional, and E2E projects and retain E2E TRX output.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 145-161)
* [x] Step 3.2: Triage the third report only after obtaining its test name, stack trace, and current output.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 163-173)
* [x] Step 3.3: Report non-minor failures as a separate investigation rather than broadening the Account Preferences change.
  * Details: .copilot-tracking/details/2026-09-20/failing-tests-resolution-details.md (Lines 175-177)

## Planning Log

See .copilot-tracking/plans/logs/2026-09-20/failing-tests-resolution-log.md for discrepancy tracking, implementation paths, and follow-on work.

## Dependencies

* The SDK selected by global.json.
* Docker, Aspire-backed test infrastructure, and installed Playwright browsers.

## Success Criteria

* A focused unit test proves that a successful VerificationFailed result renders a non-empty FailureSummary warning.
* The unavailable-provider E2E test observes Check status and asserts an alert containing unavailable without requiring a gateway-specific sentence.
* The desktop E2E test validates responsive usability without requiring Disabled to be right of Enabled.
* Affected Web tests complete with retained TRX output.
* No code change is made for the third report until current evidence identifies it.