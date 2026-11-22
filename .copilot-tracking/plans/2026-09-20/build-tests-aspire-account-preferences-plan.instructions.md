<!-- markdownlint-disable-file -->
---
applyTo: '.copilot-tracking/changes/2026-09-20/build-tests-aspire-account-preferences-changes.md'
---
# Implementation Plan: Build, Tests, Aspire Upgrade, and Account Preferences

## Overview

Resolve confirmed package-vulnerability warnings and four failing tests, align direct Aspire packages to 13.5.4, and adjust the account-preferences control layout while retaining deterministic test coverage.

## Objectives

### User Requirements

* Create an implementation plan from the supplied research. Source: user request and `.copilot-tracking/research/2026-09-20/build-tests-aspire-account-preferences-research.md`
* Resolve reported build warnings, failing tests, the Aspire 13.5.4 upgrade, and the account preferences layout. Source: research Task Implementation Requests

### Derived Objectives

* Establish the actual warning baseline before remediation, so unobserved diagnostics are not guessed. Derived from: research Build Warning Count
* Preserve API response safety, hosted-service semantics, fixture ownership, DOM order, and keyboard behavior. Derived from: research Failing Test Root Causes and Account Preferences Layout
* Keep coordinated Aspire hosting packages on one verified patch train and validate Keycloak using Docker-backed AppHost orchestration. Derived from: research Aspire Package Alignment and repository Aspire standards

## Context Summary

### Project Files

* `global.json` - pins SDK 10.0.302.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - projects account preference status responses.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs` - controls hosted authentication startup behavior.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` - owns AppHost and integration package references.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - owns the requested layout.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - owns the browser geometry contract.

### References

* `.copilot-tracking/research/2026-09-20/build-tests-aspire-account-preferences-research.md` - primary research and command evidence.
* `.copilot-tracking/research/subagents/2026-09-20/build-tests-aspire-account-preferences-implementation-research.md` - independent source-level verification.

### Standards References

* `.github/copilot-instructions.md` - repository-wide architecture, local Docker, and concise validation requirements.
* `.github/instructions/aspire.instructions.md` - Aspire package and AppHost validation guidance.
* `.github/instructions/tests.instructions.md` - deterministic fixture ownership and test-boundary rules.
* `.github/instructions/playwright.instructions.md` - web-first, accessibility-first E2E assertions.

## Implementation Checklist

### [x] Implementation Phase 1: Baseline and Package Availability

<!-- parallelizable: false -->

* [x] Step 1.1: Capture a clean serial restore/build binary log and record each distinct warning ID, project, dependency path, and location.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 13-34)
* [x] Step 1.2: Query configured NuGet sources for the exact Testcontainers, AngleSharp, and Aspire 13.5.4 target versions before changing package references.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 36-51)
* [x] Step 1.3: Stop and extend planning if the baseline has warnings beyond the two confirmed vulnerability paths or target packages are unavailable.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 53-62)

### [x] Implementation Phase 2: Correct Product and Test Failures

<!-- parallelizable: true -->

* [x] Step 2.1: Project `VerificationFailed` and its safe summary into the immediate mismatch response without persisting a failure state.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 66-86)
* [x] Step 2.2: Ensure the hosted authentication supervisor waits before its first loop execution after its readiness tick, retaining direct loop semantics.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 88-106)
* [x] Step 2.3: Set Web JSON serializer defaults in the mapping unit test and disable the E2E fixture's account-preferences reconciler.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 108-128)
* [x] Step 2.4: Run the named functional and API unit tests after the respective changes.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 130-140)

### [ ] Implementation Phase 3: Adjust Account Preferences Layout

<!-- parallelizable: true -->

* [x] Step 3.1: Change only the outer preferences control row to a start-aligned flex column; retain markup and nested horizontal radio group.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 144-160)
* [x] Step 3.2: Update the desktop Playwright geometry assertions for adjacent radio choices, Save beneath them, left-edge alignment, and 1280px overflow safety.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 162-181)
* [ ] Step 3.3: Run the account-preferences E2E class and Web unit tests, preserving 360px overflow and ArrowRight behavior coverage.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 178-181)

### [x] Implementation Phase 4: Upgrade Coordinated Packages and Remediate Warnings

<!-- parallelizable: false -->

* [x] Step 4.1: Upgrade all direct Aspire references together to the verified 13.5.4 train, including the matching Keycloak preview package.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 182-203)
* [x] Step 4.2: Upgrade `Testcontainers.MsSql` and add the validated direct AngleSharp override, without a direct SSH.NET override.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 204-220)
* [x] Step 4.3: Restore and inspect transitive package graphs plus relevant test suites before full validation.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 221-241)

### [ ] Implementation Phase 5: Full Validation and Operational Check

<!-- parallelizable: false -->

* [ ] Step 5.1: Run serial solution build and all solution tests with no unresolved vulnerabilities or baseline failures.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 242-254)
* [ ] Step 5.2: Start the Docker-backed AppHost and verify SQL Server, Keycloak realm import, OIDC sign-in, API authentication, named endpoint readiness, and cleanup.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 255-268)
* [ ] Step 5.3: Record blockers requiring new research; fix only isolated validation defects.
  * Details: `.copilot-tracking/details/2026-09-20/build-tests-aspire-account-preferences-details.md` (Lines 269-280)

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-20/build-tests-aspire-account-preferences-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK 10.0.302 from `global.json`
* Configured NuGet feeds with target package versions available
* Docker running for Keycloak and SQL Server integration validation
* Existing Aspire AppHost resource configuration and test credentials

## Success Criteria

* The clean warning baseline is documented and every confirmed vulnerability path is removed. Traces to: Research Build Warning Count
* The four named failing tests pass with behavior-focused fixes and no weakened contracts. Traces to: Research Failing Test Root Causes
* Direct Aspire packages use a verified matching 13.5.4 package train and the AppHost remains healthy with Docker-backed Keycloak. Traces to: Research Aspire Package Alignment
* Enabled and Disabled remain adjacent in keyboard/DOM order, while Save renders below the group without overflow. Traces to: Research Account Preferences Layout
