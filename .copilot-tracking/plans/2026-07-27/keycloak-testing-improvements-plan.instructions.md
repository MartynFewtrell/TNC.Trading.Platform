---
applyTo: '.copilot-tracking/changes/2026-07-27/keycloak-testing-improvements-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Keycloak Testing Improvements

## Overview

Improve real-Keycloak test reliability by adding layered Aspire and OIDC readiness, exception-complete cleanup, and cross-process ownership of fixed port 8080 while retaining clean session-scoped identity state.

## Objectives

### User Requirements

* Create an implementation plan that improves Keycloak use specifically for testing. Source: user request and `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md`.
* Base the plan on the supplied research into delayed health, persistence, and authentication-test readiness. Source: user-attached research artifact.

### Derived Objectives

* Gate managed Web Keycloak Admin calls on bounded Aspire resource health and exact application-realm discovery, while preserving the unmanaged API fixture's API health, protected-surface, and real-token behavior gates. Derived from: the proven race after managed `DistributedApplication.StartAsync()` and the distinct API process architecture.
* Classify transient and permanent HTTP failures consistently so configuration defects fail immediately and secret-safe diagnostics retain the controlling evidence. Derived from: the API token helper's current retry of HTTP 400 and 401.
* Dispose partially created AppHost resources and environment overrides after every initialization failure. Derived from: the managed factory's partial-start leak and fixed-port reuse risk.
* Coordinate API integration, Web functional, and Web E2E fixtures across processes because all real-Keycloak suites require host port 8080. Derived from: assembly-local xUnit serialization cannot coordinate independent test processes.
* Preserve volume-free session lifetime for automated tests until mutable client configuration and stale-realm risks are removed. Derived from: startup import skips existing realms and the Web fixture mutates callback state.
* Measure startup transitions before changing DCP timeouts or pursuing performance optimizations. Derived from: the absence of cold, clean-session, and warm timing evidence.

## Context Summary

### Project Files

* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Starts managed and unmanaged AppHost test runtimes, currently calls Keycloak Admin APIs before readiness, and owns dynamic Web client mutation.
* `test/Shared/Authentication/AppHostProcessHandle.cs` - Owns behavior-level API/Web probes and AppHost cleanup; cleanup must become exception-complete.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs` - Provides application-token readiness but currently retries permanent HTTP failures.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` - API real-Keycloak fixture requiring cross-process lease ownership.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` - Web functional fixture requiring the same lease.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Web E2E fixture requiring the same lease.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` - Defines fixed Keycloak port, realm import, persistent local lifetime, and session test lifetime.
* `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json` - Defines imported clients, wildcard localhost redirects, roles, scopes, and seeded users.
* `docs/wiki/testing-and-quality.md` - Owns the documented distributed-auth test architecture and validation workflow.
* `docs/wiki/local-development.md` - Owns Keycloak startup, stale-state, and troubleshooting guidance.

### References

* `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md` (Lines 8-290) - Primary evidence, selected implementation path, alternatives, and acceptance checks.
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md` (Lines 18-280) - Verified implementation surfaces, project versions, fixture call sites, validation commands, and wiki impact.
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-validation-gaps-research.md` (Lines 14-176) - Committed test topology, integration-test lease ownership, API readiness scope, and before/after evidence design.
* `https://aspire.dev/integrations/security/keycloak/` - Aspire Keycloak health and persistence behavior.
* `https://aspire.dev/testing/write-your-first-test/` - Separate `StartAsync` and `WaitForResourceHealthyAsync` testing pattern.
* `https://www.keycloak.org/observability/health` - Keycloak readiness semantics.
* `https://www.keycloak.org/server/importExport` - Existing-realm import behavior and stale-state implications.

### Standards References

* `.github/copilot-instructions.md` - Keycloak-in-Aspire local auth, one top-level C# type per file, test traceability, and wiki completion requirements.
* `.github/instructions/aspire-tests.instructions.md` - Closed-box Aspire tests, condition-based readiness, and deterministic cleanup.
* `.github/instructions/tests.instructions.md` - Unit-first test pyramid, naming, XML traceability, isolation, and no startup sleeps.
* `.github/instructions/auth.instructions.md` - Stable local Keycloak port, realm import, persistence reset, and standards-based auth constraints.
* `.github/instructions/architecture.instructions.md` - Narrow testable responsibilities and avoidance of broad shared abstractions.
* `.github/instructions/playwright.instructions.md` - Applies to retained browser sign-in and sign-out validation.

## Implementation Checklist

### [x] Implementation Phase 1: Readiness Policy and Package Alignment

<!-- parallelizable: false -->

* [x] Step 1.1: Align the API integration project's `Aspire.Hosting.Testing` package to 13.2.3.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 12-32)
* [x] Step 1.2: Add a pure shared bounded readiness policy and fast unit coverage for transient, permanent, issuer, cancellation, and deadline behavior.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 33-62)
* [x] Step 1.3: Validate the helper tests and API integration project build.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 63-68)

### [x] Implementation Phase 2: Managed Startup and Cleanup Hardening

<!-- parallelizable: false -->

* [x] Step 2.1: Gate managed Web Admin calls on Keycloak Healthy state and realm discovery, add Admin-sequence fault injection, and preserve unmanaged API behavior gates.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 73-103)
* [x] Step 2.2: Make managed initialization and handle disposal exception-complete across application, builder, and environment cleanup.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 104-125)
* [x] Step 2.3: Run AppHost helper tests plus focused Web functional and API integration authentication checks.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 126-132)

### [x] Implementation Phase 3: Baseline Topology and Timing Evidence

<!-- parallelizable: false -->

* [x] Step 3.1: Verify the committed one-runner solution-test topology and record the limits of machine-local coordination.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 137-158)
* [x] Step 3.2: Capture pre-lease concurrent-process, port-owner, and startup-timing evidence and select a finite lease deadline.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 159-186)
* [x] Step 3.3: Validate that baseline artifacts and diagnostics are complete and secret-safe.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 187-193)

### [x] Implementation Phase 4: Cross-Process Ownership and State Simplification

<!-- parallelizable: false -->

* [x] Step 4.1: Add the portable bounded file lease and real child-process behavior tests in API integration.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 198-225)
* [x] Step 4.2: Acquire the lease in all three real-Keycloak fixtures and release it after complete cleanup.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 226-245)
* [x] Step 4.3: Repeat the baseline concurrent harness and require successful projects, non-overlapping lease intervals, and one port owner.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 246-251)
* [x] Step 4.4: Prove whether imported wildcard callbacks support real browser sign-in and sign-out without Admin API mutation.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 252-275)
* [x] Step 4.5: Repeat the managed fixture at least 20 times and run focused browser auth tests after any state simplification.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 276-281)

### [x] Implementation Phase 5: Documentation and Final Validation

<!-- parallelizable: false -->

* [x] Step 5.1: Update testing and local-development wiki guidance; update architecture documentation only if runtime composition changes.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 286-309)
* [x] Step 5.2: Build the solution, run focused projects individually and concurrently, and execute the full Clean Architecture quality gate.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 310-318)
* [x] Step 5.3: Fix isolated validation defects without masking readiness failures through sleeps or unmeasured timeout increases.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 319-322)
* [x] Step 5.4: Report authority, provisioning, persistent-state, or CI-topology blockers as follow-on research rather than widening this implementation.
  * Details: `.copilot-tracking/details/2026-07-27/keycloak-testing-improvements-details.md` (Lines 323-326)

## Planning Log

See `.copilot-tracking/plans/logs/2026-07-27/keycloak-testing-improvements-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK 10.0.200 pinned by `global.json`
* Docker Desktop for Aspire, Keycloak, and distributed authentication tests
* Playwright prerequisites for Web E2E tests
* Existing fixed public Keycloak issuer at `http://localhost:8080/realms/tnc-trading-platform`
* Secret-safe local test credentials and realm import already supplied by the repository

## Success Criteria

* Managed Web Keycloak Admin calls occur only after resource health and exact application-realm discovery succeed; unmanaged API startup retains API health, protected-surface, and real-token gates. Traces to: user requirement and primary research readiness finding.
* HTTP 400, HTTP 401, issuer mismatch, and malformed successful responses fail immediately; only documented transient conditions retry under one finite deadline. Traces to: derived retry objective.
* Admin token, client-query, and client-update fault injection proves transient recovery, immediate permanent failure, and deadline exhaustion. Traces to: primary research acceptance checks.
* Failure after build, start, readiness, or protocol setup leaves no AppHost process, container, environment override, lease, or port owner. Traces to: subagent critical cleanup finding.
* Before/after evidence records process overlap and port ownership; after implementation, API integration, Web functional, and Web E2E suites coordinate fixed port 8080 with non-overlapping leases and pass individual plus concurrent validation. Traces to: fixed-port research finding.
* Automated tests continue using fresh volume-free session state and never rely on stale realm imports. Traces to: primary research isolation recommendation.
* At least 20 repeated managed Web fixture runs pass with the image present and a fresh session container, and one cold-image timing run informs any timeout change. Traces to: primary research acceptance checks.
* `docs/wiki/testing-and-quality.md` and `docs/wiki/local-development.md` match the delivered readiness, lease, cleanup, and troubleshooting behavior. Traces to: repository wiki completion requirement.
* The full Clean Architecture quality gate passes, or environment and product blockers are recorded with command evidence and follow-on actions. Traces to: repository validation convention.