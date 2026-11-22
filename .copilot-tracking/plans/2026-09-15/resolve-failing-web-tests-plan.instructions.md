---
applyTo: 'test/Shared/AccountPreferences/ControllableIgProvider.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/ControllableIgProviderTests.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Resolve Failing Web Tests

## Overview

Repair the fixture defects behind eight Web functional and two browser E2E failures without changing the real-infrastructure testing model.

## Objectives

### User Requirements

* Create a plan to resolve the failing tests in `.copilot-tracking/research/2026-09-15/current-testing-strategy-and-failing-tests-research.md` - Source: user request on 2026-09-15

### Derived Objectives

* Preserve parameterless provider startup while making failed-listener retries deterministic and testable - Derived from: `Random.Shared` prevents an occupied-first-port regression
* Preserve persistent Keycloak for interactive AppHost use and disable it only in the E2E fixture - Derived from: current AppHost default and functional-fixture precedent
* Validate fixture startup before classifying residual failures as product regressions - Derived from: all recorded failures precede feature assertions

## Context Summary

* `test/Shared/AccountPreferences/ControllableIgProvider.cs:36-59` reuses a listener after a failed bind and blocks the functional fixture.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-24` lacks the non-persistent Keycloak setting already present in its functional counterpart.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:8-16,41-47` intentionally defaults Keycloak persistence to enabled.
* `.github/workflows/ci.yml:70-93,117-131` establishes Release-mode staged validation for the affected projects.
* Research: `.copilot-tracking/research/2026-09-15/current-testing-strategy-and-failing-tests-research.md:62-176` and `.copilot-tracking/research/subagents/2026-09-15/failing-web-tests-repair-surfaces-research.md:28-145`.

## Implementation Checklist

### [x] Implementation Phase 1: Repair Listener Retry Ownership

<!-- parallelizable: false -->

* [x] Step 1.1: Construct, configure, and start a distinct `HttpListener` in each bounded candidate-port attempt; dispose failed attempts.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 7-36)
* [x] Step 1.2: Add the narrowest non-public candidate-port selection seam while preserving existing parameterless `Start()` callers.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 38-61)
* [x] Step 1.3: Add `Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied` to the existing Web functional project.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 63-88)
* [x] Step 1.4: Run the filtered Release-mode regression before running the owning suite.

### [ ] Implementation Phase 2: Isolate E2E Keycloak State

<!-- parallelizable: false -->

* [x] Step 2.1: Add `AppHost:UsePersistentKeycloakState = bool.FalseString` beside the E2E fixture's existing scoped settings.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 90-112)
* [ ] Step 2.2: Run both formerly failing browser cases individually, retaining Playwright artifacts on any residual failure.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 114-129)

### [ ] Implementation Phase 3: Validate Repaired Web Suites

<!-- parallelizable: false -->

* [ ] Step 3.1: Run the complete functional project in Release after the provider regression passes.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 131-140)
* [ ] Step 3.2: Run the complete E2E project twice in Release after both focused browser checks pass.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 142-153)
* [ ] Step 3.3: Confirm no fixed-port, global-state, synthetic-authentication, or AppHost-default workaround was introduced.
  * Details: .copilot-tracking/details/2026-09-15/resolve-failing-web-tests-details.md (Lines 155-168)

### [ ] Implementation Phase 4: Final Validation

<!-- parallelizable: false -->

* [ ] Step 4.1: Run the fast, Docker-backed integration, and browser/Keycloak project stages represented by `.github/workflows/ci.yml`, and capture Release-mode results and totals.
* [ ] Step 4.2: Correct only minor isolated fixture or regression-test findings, rerunning the focused command each time.
* [ ] Step 4.3: Record product-assertion failures or solution-host cancellation as new research rather than expanding this repair.

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-15/resolve-failing-web-tests-log.md` for discrepancies, selected path, alternatives, and follow-on work.

## Dependencies

* .NET 10 SDK selected by `global.json`
* Docker Desktop for Aspire and Keycloak resources
* Playwright Chromium and existing real-authentication test prerequisites

## Success Criteria

* A deterministic regression proves replacement-listener recovery from a deliberately occupied candidate port.
* All eight functional tests pass without port leasing, global serialization, or probabilistic retries.
* Both E2E journeys pass with fixture-scoped non-persistent Keycloak state, while AppHost's interactive default remains persistent.
* The staged CI matrix completes without a regression in its fast, integration, or browser/Keycloak test responsibilities.