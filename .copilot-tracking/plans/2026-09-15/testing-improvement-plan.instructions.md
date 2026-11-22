<!-- markdownlint-disable-file -->
---
applyTo: 'test/**/*.cs, test/**/*.csproj, .github/**/*.md, docs/wiki/**/*.md'
---

# Implementation Plan: Testing Improvement

## Overview

Restore deterministic test execution by replacing incompatible test-only dependencies and unowned infrastructure with fixture-owned resource contracts, then codify the resulting test strategy in repository guidance and wiki documentation.

## Objectives

### User Requirements

* Create a plan to improve testing from the supplied failure research. Source: user request, 2026-09-15.
* Include updates to artifacts and documentation that enforce the future testing direction. Source: user request, 2026-09-15.

### Derived Objectives

* Account for each current failure category without masking defects through global serialization. Derived from: `.copilot-tracking/research/2026-09-15/testing-improvement-plan-research.md`.
* Retain bUnit for component coverage while removing its incompatible direct AngleSharp override. Derived from: retained coverage decision in the failure research.
* Keep SQL provider integration independent of the full AppHost while owning its database lifecycle. Derived from: service-local test boundary and fixture ownership evidence.

## Context Summary

### Project Files

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - contains the incompatible direct AngleSharp override.
* `test/Shared/Authentication/` - contains the process, socket-discovery, and fixed-port helpers to replace after migration.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - contains the released-port HTTP listener race.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` - discovers instead of owning SQL Server.
* `.github/instructions/aspire-tests.instructions.md` and `.github/instructions/tests.instructions.md` - enforce future fixture practices.
* `docs/wiki/testing-and-quality.md` and `docs/wiki/local-development.md` - currently describe fixed-port and listener-discovery behavior.

### References

* `.copilot-tracking/research/2026-09-15/testing-improvement-plan-research.md` - consolidated, verified planning evidence.
* `.copilot-tracking/research/2026-09-15/failing-tests-research.md` - original failure analysis and official-source strategy.
* `.copilot-tracking/research/subagents/2026-09-15/testing-improvement-plan-scope.md` - verified migration grouping, consumers, and commands.

### Standards References

* `.github/instructions/aspire-tests.instructions.md` - Aspire test-host lifecycle and randomized port requirements.
* `.github/instructions/tests.instructions.md` - testing pyramid, isolation, and determinism requirements.
* `.github/instructions/playwright.instructions.md` - browser test locator, assertion, and wait rules.
* `.github/copilot-instructions.md` - repository-wide test, documentation, and durable-learning requirements.

## Implementation Checklist

### [x] Implementation Phase 1: Restore Web Unit Dependency Compatibility

<!-- parallelizable: true -->

* [x] Step 1.1: Remove the direct AngleSharp 1.8.1 package reference and retain `bunit.web` 1.40.0.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 12-31)
* [x] Step 1.2: Restore and execute the Web unit project, confirming the four pre-assertion `MissingMethodException` failures no longer occur.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 32-38)

### [~] Implementation Phase 2: Migrate AppHost-Backed Fixtures

<!-- parallelizable: false -->

* [x] Step 2.1: Add a shared linked-source managed Aspire fixture with named clients/endpoints, bounded health readiness, builder-scoped configuration, and async disposal.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 43-68)
* [x] Step 2.2: Move synthetic and real API authentication collections onto the shared fixture while preserving their distinct token scenarios.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 69-88)
* [x] Step 2.3: Replace the fixed Keycloak authority with an endpoint-derived AppHost reference and update composition tests before proving dynamic authentication.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 89-108)
* [~] Step 2.4: Prove the real authorization-code flow with a randomized Web callback/origin, then migrate Web functional and E2E fixture wrappers to the named `web` resource.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 109-129)
* [x] Step 2.5: Replace the controllable IG endpoint implementation and retire process scanning, fixed-port leasing, and forwarding helpers after all consumers migrate.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 130-150)
* [ ] Step 2.6: Run API integration, Web functional, and Web E2E focused suites twice and once concurrently under ordinary parallelism.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 151-160)

### [x] Implementation Phase 3: Own SQL Integration Lifecycle

<!-- parallelizable: true -->

* [x] Step 3.1: Provision an Infrastructure-test-owned SQL Server fixture with readiness, isolated database naming/reset, diagnostics, and async disposal.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 165-188)
* [x] Step 3.2: Update SQL fixture consumers and package/project references without introducing AppHost dependency into service-local tests.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 189-202)
* [x] Step 3.3: Run the Infrastructure integration project twice and confirm cleanup leaves no dependency on arbitrary running Docker containers.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 203-209)

### [x] Implementation Phase 4: Enforce and Document the Strategy

<!-- parallelizable: false -->

* [x] Step 4.1: Update Aspire and general testing instructions with enforceable resource discovery, lifecycle, isolation, and UI test-boundary rules.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 214-229)
* [x] Step 4.2: Record the durable routing rule in repository agent guidance and lessons.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 230-243)
* [x] Step 4.3: Update wiki testing and local-development guidance to distinguish randomized automated endpoints from stable manual development endpoints.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 244-259)
* [x] Step 4.4: Add CI stages and failure-artifact publication for fast, Aspire-backed, and browser/real-Keycloak testing.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 260-279)
* [x] Step 4.5: Validate workflow, Markdown, frontmatter, and retired-guidance searches.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 280-288)

### [~] Implementation Phase 5: Final Validation

<!-- parallelizable: false -->

* [~] Step 5.1: Run focused tests, solution build, and full solution test validation using normal concurrency.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 293-304)
* [~] Step 5.2: Preserve diagnostics and repair only isolated validation defects; create a follow-up plan for failures that require broader architectural work.
  * Details: `.copilot-tracking/details/2026-09-15/testing-improvement-details.md` (Lines 305-311)

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-15/testing-improvement-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* Docker Desktop running with access to required SQL Server and Keycloak images.
* .NET SDK 10.0.302 pinned by `global.json`.
* `Aspire.Hosting.Testing`, xUnit, Playwright browser dependencies, and the selected SQL test-container library.
* A test-safe Keycloak realm/client configuration that permits dynamically assigned localhost callback/origin URIs.
* A GitHub Actions Linux runner with Docker and Playwright Chromium prerequisites.

## Success Criteria

* The Web unit suite passes using bUnit's compatible transitive AngleSharp version. Traces to: user requirement and failure research.
* API integration, Web functional, and Web E2E no longer scan ports, infer dashboard endpoints, use fixed Keycloak port leases, or mutate process-wide configuration. Traces to: fixture migration research.
* API and Web derive their Keycloak authority from the AppHost Keycloak endpoint without a fixed `localhost:8080` value. Traces to: validator research.
* SQL integration owns, awaits, isolates, resets, and disposes its SQL dependency. Traces to: fixture ownership research.
* Guidance and wiki pages direct future work to named Aspire resources, randomized ports, fixture-scoped configuration, bounded readiness, bUnit component coverage, and browser-only Playwright coverage. Traces to: user requirement.
* CI partitions fast, Aspire-backed, and browser/real-Keycloak tests and publishes actionable diagnostics for failures. Traces to: failure research observability strategy.
* The full solution passes under normal concurrency without adding global serialization. Traces to: failure research validation plan.
