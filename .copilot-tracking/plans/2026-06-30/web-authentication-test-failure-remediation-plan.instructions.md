---
description: "Implementation plan for resolving the shared AppHost-backed startup and endpoint-discovery failure affecting the Web functional and E2E authentication suites"
applyTo: 'test/Shared/Authentication/*.cs, test/TNC.Trading.Platform.Web/**/*.cs, src/TNC.Trading.Platform.AppHost/**/*.cs, src/TNC.Trading.Platform.Web/**/*.cs'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Web Authentication Test Failure Remediation

## Overview

Resolve the shared startup failure blocking the Web functional and E2E authentication suites by first converting the remaining ambiguity into actionable runtime evidence, then applying one fix in the controlling shared harness or AppHost layer, and finally validating both suites against the real Keycloak-backed sign-in flow.

## Objectives

### User Requirements

* Create a plan to resolve the failing tests in test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests and test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests. Source: user request on 2026-06-30.

### Derived Objectives

* Treat the two failing projects as one shared remediation area because both suites fail in the same AppHost-driven Web sign-in discovery path before their test bodies execute. Derived from: .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 78-121).
* Use a short diagnostic phase before the main code change because current research still leaves one high-impact ambiguity between resource startup failure and brittle endpoint discovery, and it explicitly calls for resource-level startup evidence to break that tie. Derived from: .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 22-31, 102-121, 176-177).
* Preserve real Keycloak-backed authentication for both suites rather than introducing a synthetic fallback or looser reachability check. Derived from: .copilot-tracking/research/subagents/2026-06-30/web-test-classification-research.md and test/Shared/Authentication/AppHostProcessHandle.cs.
* Keep the fix in the shared harness or the owning AppHost layer rather than duplicating per-project changes. Derived from: .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 44-63, 78-121).

## Context Summary

### Project Files

* test/Shared/Authentication/AppHostProcessHandle.cs - current shared sign-in endpoint discovery and timeout logic
* test/Shared/Authentication/RealAppHostProcessFactory.cs - shared AppHost process launch path for the failing suites
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs - functional fixture blocked in shared startup
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs - E2E fixture blocked in shared startup
* src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs - AppHost Web resource registration and external endpoint exposure
* src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs - Keycloak-backed Web authentication wiring and resource waits

### References

* .copilot-tracking/research/2026-06-30/web-test-failures-research.md - consolidated failure analysis and selected remediation direction
* .copilot-tracking/research/subagents/2026-06-30/functional-tests-failure-research.md - functional suite hang-abort evidence and hang-dump references
* .copilot-tracking/research/subagents/2026-06-30/e2e-tests-failure-research.md - E2E suite timeout evidence and shared stack trace
* .copilot-tracking/research/subagents/2026-06-30/web-test-classification-research.md - suite classification and real Keycloak-backed auth model
* .github/instructions/aspire-tests.instructions.md - Aspire test expectations and preference for managed closed-box testing
* .github/instructions/playwright.instructions.md - Playwright test expectations and anti-flake constraints

### Standards References

* d:\Repos\TNC.Trading\TNC.Trading.Platform\.github\instructions\aspire-tests.instructions.md - AppHost-backed test guidance relevant to the shared harness and validation strategy
* d:\Repos\TNC.Trading\TNC.Trading.Platform\.github\instructions\playwright.instructions.md - Playwright-specific expectations for the E2E flow and anti-timeout discipline
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\markdown.instructions.md - markdown authoring requirements for planning artifacts
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\writing-style.instructions.md - markdown writing-style requirements for planning artifacts
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\prompt-builder.instructions.md - .instructions.md authoring requirements for the plan file

## Implementation Checklist

### [x] Implementation Phase 1: Capture discriminating startup evidence in the shared harness

<!-- parallelizable: false -->

* [x] Step 1.1: Add targeted diagnostics to the shared AppHost-backed startup path
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 25-46)
* [x] Step 1.2: Reproduce the failure with narrow validation commands and collect the new evidence
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 48-66)
* [x] Step 1.3: Inspect Aspire resource-level logs for `web` and `keycloak`
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 68-95)
* [x] Step 1.4: Inspect an existing hang dump if resource-level evidence remains inconclusive
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 97-122)
* [x] Step 1.5: Validate Phase 1 changes
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 124-130)

### [x] Implementation Phase 2: Replace brittle endpoint discovery with stable shared discovery when the Web resource is healthy

<!-- parallelizable: false -->

* [x] Step 2.1: Move shared endpoint discovery away from parent-process output scraping
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 136-160)
* [x] Step 2.2: Keep the functional and E2E auth flows consistent after the shared discovery change
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 162-178)

### [x] Implementation Phase 3: Repair AppHost Web or Keycloak readiness if startup, not discovery, is the blocker

<!-- parallelizable: false -->

* [x] Step 3.1: Fix the controlling AppHost startup or readiness gate exposed by Phase 1
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 184-207)
* [x] Step 3.2: Keep the shared harness expectations strict after the AppHost fix
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 209-225)

### [x] Implementation Phase 4: Final validation and closure

<!-- parallelizable: false -->

* [x] Step 4.1: Run the full validation set for the affected area
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 231-239)
* [x] Step 4.2: Fix minor validation regressions and report any remaining blocker
  * Details: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 241-243)

## Planning Log

See .copilot-tracking/plans/logs/2026-06-30/web-authentication-test-failure-remediation-log.md for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* Existing failure research under .copilot-tracking/research/2026-06-30/
* Docker and a local environment capable of starting AppHost, Keycloak, and the Web resource
* Ability to run the two target dotnet test projects locally with their current infrastructure prerequisites
* Write access to shared authentication test utilities and the owning AppHost startup files
* Access to the AppHost-emitted Aspire dashboard URL or persisted AppHost console output in artifacts/local/ for resource-log inspection
* Visual Studio dump debugging or dotnet-dump analyze available if the hang-dump fallback becomes necessary

## Success Criteria

* The implementation resolves the shared startup timeout so both target test suites reach their real authentication assertions. Traces to: .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 78-121).
* The final fix is applied once in the shared harness or AppHost layer rather than duplicated separately in the two Web test projects. Traces to: .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 44-63, 78-121).
* Both suites continue to authenticate against the real Keycloak-backed sign-in flow instead of a synthetic or relaxed fallback. Traces to: .copilot-tracking/research/subagents/2026-06-30/web-test-classification-research.md.
* Validation proves the functional and E2E test projects pass as full projects after the fix, not only as narrow repro commands. Traces to: .copilot-tracking/details/2026-06-30/web-authentication-test-failure-remediation-details.md (Lines 231-243).
