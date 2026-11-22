---
applyTo: 'src/**/*.cs,src/**/*.razor,src/**/*.css,test/**/*.cs'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Preferences Page Issues

## Overview

Deliver a safe account-mismatch contract, actionable Save feedback, correct confirmation-time rendering, and a compact accessible preferences control row without changing account binding or reconciliation semantics.

## Objectives

### User Requirements

* Deliver the research-recommended Account Preferences changes. Source: user request and `.copilot-tracking/research/2026-09-19/account-preferences-page-issues-research.md`.
* Correct `Last confirmed`, account-mismatch warnings, trailing-stops layout, and Save failure handling. Source: primary research Task Implementation Requests.
* Move the existing trailing-stops choice controls into the horizontal preferences pane with Save preferences on desktop. Source: user clarification.

### Derived Objectives

* Preserve durable-target and fresh-session guards because they prevent preferences I/O against a switched IG account. Derived from: verification research lines 31-38.
* Preserve typed API failure metadata through the Web client so the UI does not infer identity from arbitrary 409 text. Derived from: verification research lines 46-51.
* Retain native radio semantics, focus behavior, and responsive usability. Derived from: primary research Technical Scenarios.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs` - produces generic durable-target mismatch outcomes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - protects fresh-session account-switch races.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - maps categorized outcomes to Problem Details.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - drops Problem Details type and category.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - has the incorrect timestamp binding and separate controls.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - creates tiles and narrow-screen stacking.

### References

* `.copilot-tracking/research/2026-09-19/account-preferences-page-issues-research.md` - primary research and selected approach.
* `.copilot-tracking/research/subagents/2026-09-19/account-preferences-plan-verification-research.md` - verified owners, test gaps, and commands.

## Implementation Checklist

### [x] Implementation Phase 1: Normalize Account-Mismatch Server Contract

<!-- parallelizable: false -->

* [x] Step 1.1: Add a feature-local safe mismatch reason and apply it to Check, Save, and both gateway guards.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 12-45)
* [x] Step 1.2: Classify only durable-target Save mismatch as `AccountMismatch` and route it through typed Problem Details; preserve unrelated generic conflicts.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 46-70)
* [x] Step 1.3: Add Application, Infrastructure, API, and functional tests for outcome parity, unchanged durable state, and no IG preferences I/O.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 71-90)
* [x] Step 1.4: Run scoped handler, gateway, and API tests.

### [x] Implementation Phase 2: Preserve and Present Typed Save Failures

<!-- parallelizable: false -->

* [x] Step 2.1: Extend the Web error contract to retain HTTP status, Problem Details type, failure category, title, and detail.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 103-125)
* [x] Step 2.2: Display mismatch-specific guidance only when both stable type and category match; retain generic handling for every other error.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 126-150)
* [x] Step 2.3: Add Web client and bUnit parsing, safe-presentation, generic-fallback, and non-disclosure coverage.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 151-168)
* [x] Step 2.4: Run focused Web unit tests.

### [x] Implementation Phase 3: Correct Timestamp and Responsive Control Row

<!-- parallelizable: false -->

* [x] Step 3.1: Render `LastVerifiedAtUtc` as localized semantic time and omit the complete row when it is null.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 179-205)
* [x] Step 3.2: Move the trailing-stops choice controls and current Save action into the horizontal desktop preferences pane; use flex wrapping only when the narrow viewport cannot fit the controls.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 206-228)
* [x] Step 3.3: Update bUnit timestamp expectations and add desktop/narrow browser geometry, overflow, and keyboard coverage.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 229-246)
* [x] Step 3.4: Run focused Web unit and E2E tests.

### [ ] Implementation Phase 4: Prove Account-Switch Safety End to End

<!-- parallelizable: false -->

* [x] Step 4.1: Add a fixture-controlled account-A-to-B case covering both Check and Save.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 258-275)
* [x] Step 4.2: Assert identical safe responses, zero preferences GET or PUT, unchanged target state, IG-first Save repair, and observation-only reconciliation.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 276-298)

### [x] Implementation Phase 5: Final Validation

<!-- parallelizable: false -->

* [x] Step 5.1: Run the focused regression matrix and solution build.
  * Details: `.copilot-tracking/details/2026-09-19/account-preferences-page-issues-details.md` (Lines 299-312)
* [x] Step 5.2: Correct isolated compilation, assertion, and formatting issues, then rerun their focused validation.
* [x] Step 5.3: Report any account-rebinding, public-support-wording, or timestamp-policy blocker for follow-on planning.

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-19/account-preferences-page-issues-log.md` for discrepancy tracking, alternatives, and follow-on work.

## Dependencies

* .NET 10 SDK selected by `global.json`
* Docker for Aspire-backed Web E2E tests and Keycloak
* Existing Application, Infrastructure, API, Web unit, functional, and E2E test projects

## Success Criteria

* Check and Save return the same typed non-identifying mismatch 409 with no preferences I/O after either guard fails. Traces to: requirements and verification research lines 31-51.
* Web Save guidance appears only for the recognized mismatch and generic behavior remains for other errors. Traces to: Save-failure requirement.
* `Last confirmed` renders a time only when available; trailing-stops controls and Save preferences share a horizontal desktop pane and remain accessible at narrow widths. Traces to: timestamp and layout requirements plus user clarification.
* Focused handler, gateway, API, functional, Web, and browser tests pass. Traces to: primary research validation plan.