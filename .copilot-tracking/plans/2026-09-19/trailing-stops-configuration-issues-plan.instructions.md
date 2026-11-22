<!-- markdownlint-disable-file -->
---
applyTo: 'src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css,test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs'
---

# Implementation Plan: Trailing Stops Configuration Issues

## Overview

Align the trailing-stops configuration controls through a focused CSS and browser-test change while preserving the current fail-closed IG account-binding policy and resolving the reported incident through the supported authentication recovery procedure.

## Objectives

### User Requirements

* Create a delivery plan for the recommended changes in `.copilot-tracking/research/2026-09-19/trailing-stops-configuration-issues-research.md` - Source: user request, 2026-09-19

### Derived Objectives

* Keep Enabled, Disabled, and Save visually adjacent and vertically aligned on desktop without changing native radio semantics or narrow-screen wrapping - Derived from: primary research, Configuration Layout
* Retain the broker-account mismatch barrier so no preference I/O can target an account other than the durable binding - Derived from: primary and subagent research, Account Binding and Command Recovery
* Provide an evidence-gathering route for an incident that persists after reauthentication, rather than changing identity-comparison semantics without IG documentation - Derived from: subagent research, Unresolved Gaps and Decisions

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - Owns the flex sizing and cross-axis alignment that misplaces Save
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Owns desktop geometry, narrow viewport, and keyboard interaction coverage
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs` - Rejects mismatched bindings before persistence or provider remediation
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs` - Stops status observation and remediation before provider I/O on a mismatch
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Rechecks the fresh IG session account identity at the provider boundary

### References

* `.copilot-tracking/research/2026-09-19/trailing-stops-configuration-issues-research.md` - Primary issue analysis and selected remedies
* `.copilot-tracking/research/subagents/2026-09-19/trailing-stops-delivery-scope-research.md` - Current implementation surface, tests, and validation commands
* `docs/wiki/local-development.md` - Docker prerequisite and repository build/test commands

### Standards References

* `.github/copilot-instructions.md` - Requires safe configuration ownership, Keycloak-in-Aspire local auth, documented tests, and wiki updates when implementation changes documentation needs
* `.github/instructions/auth.instructions.md` - Applies to any future authentication or account-binding change
* `.github/instructions/radzen-blazor.instructions.md` - Applies to the Web UI if implementation expands beyond the CSS slice
* `.github/instructions/playwright.instructions.md` - Applies to the Account Preferences E2E test
* `.github/instructions/tests.instructions.md` - Applies to automated test updates

## Implementation Checklist

### [x] Implementation Phase 1: Correct the Configuration Control Layout

<!-- parallelizable: false -->

* [x] Step 1.1: Remove the fieldset expansion and center the control row cross-axis alignment without changing page markup or existing gap and wrapping rules
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 11-36)
* [x] Step 1.2: Replace the permissive desktop geometry expectation with bounding-box checks for adjacent Save placement and vertical-center alignment, preserving narrow-view and keyboard-radio coverage
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 37-66)
* [x] Step 1.3: Validate the layout slice
  * Run the focused Account Preferences E2E class at desktop and its existing narrow viewport path
  * Run `dotnet build` after the focused test
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 67-81)

### [ ] Implementation Phase 2: Recover the Current Account-Binding Incident

<!-- parallelizable: false -->

* [x] Step 2.1: Reauthenticate the intended IG Demo account and invoke Check status, retaining the existing fail-closed guard and no-rebind policy
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 84-109)
* [x] Step 2.2: Capture browser network and console evidence and escalate when the mismatch remains after successful reauthentication
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 110-128)
* [x] Step 2.3: Record intentional rebinding and account-ID canonicalization as separately authorized follow-on work, not as a Save behavior change
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 129-143)

### [ ] Implementation Phase 3: Final Validation and Handoff

<!-- parallelizable: false -->

* [x] Step 3.1: Run full project validation after the layout patch and operational recovery check
  * Execute `dotnet build` and `dotnet test`, using `-m:1` only for the documented intermittent child-node fallback
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 146-163)
* [x] Step 3.2: Fix isolated CSS, test, or build failures; document blockers that require IG identity-contract research or a new rebind slice
  * Details: `.copilot-tracking/details/2026-09-19/trailing-stops-configuration-issues-details.md` (Lines 164-175)

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-19/trailing-stops-configuration-issues-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK 10.0.302 from `global.json`
* Docker and the Aspire local environment for the Playwright E2E suite and IG authentication recovery
* An authenticated session for the intended IG Demo account
* Browser DevTools access if the mismatch persists after reauthentication

## Success Criteria

* The desktop control row keeps the Save command adjacent to the natural-width radio group and vertically centered - Traces to: User Requirement and primary research, Configuration Layout
* Existing narrow-screen no-overflow and keyboard radio behavior remain covered by the focused E2E test - Traces to: subagent research, CSS Alignment and Browser Coverage
* No application, API, or infrastructure change permits cross-account IG preference I/O or silent account rebinding - Traces to: primary research, Account Binding and Command Recovery
* The current incident is either recovered through matching-account reauthentication and Check status or backed by network/console evidence for targeted diagnosis - Traces to: subagent research, Account-Mismatch Guard and Recovery