---
applyTo: 'test/Shared/Authentication/AppHostCleanup.cs, test/Shared/Authentication/ManagedAppHostFixture.cs, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AppHostCleanupTests.cs, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Managed AppHost Fixture Cleanup

## Overview

Harden AppHost-backed test teardown so Aspire/DCP receives builder disposal even when application shutdown fails, preventing orphaned tunnel proxy containers after normal test completion.

## Objectives

### User Requirements

* Create a plan to harden the test suite against recurrence of orphaned DCP tunnel proxy containers. Source: user request, 2026-09-15.
* Preserve the completed manual removal of the five known orphaned containers as an operational action, not a test-suite feature. Source: user request, 2026-09-15.

### Derived Objectives

* Attempt every owned AppHost cleanup operation after any preceding teardown failure. Derived from: `.copilot-tracking/research/2026-09-15/aspire-dcp-tunnel-proxies-research.md` (lines 273-279).
* Preserve the original cleanup failure and retain later failures as diagnostics. Derived from: `test/Shared/Authentication/AppHostProcessHandle.cs` (lines 179-238).
* Verify cleanup ordering deterministically without relying on Docker, then verify actual DCP behavior through an existing closed-box test. Derived from: `.copilot-tracking/research/subagents/2026-09-15/managed-app-host-fixture-cleanup-research.md` (lines 76-89).

## Context Summary

### Project Files

* `test/Shared/Authentication/ManagedAppHostFixture.cs` - Owns the distributed application testing builder and has the short-circuiting disposal path.
* `test/Shared/Authentication/AppHostProcessHandle.cs` - Provides the existing local failure-aggregation precedent.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` - Releases the controlled IG provider after delegated AppHost disposal.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - Has the same wrapper-fixture release ordering.
* `test/Shared/Authentication/AppHostCleanup.cs` - New dependency-free teardown sequencing helper linked into fixture and unit-test projects.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AppHostCleanupTests.cs` - New deterministic cleanup-sequencing unit test file.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Links the shared helper for the fixture compiled by Functional tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Links the shared helper for the fixture compiled by E2E tests.

### References

* `.copilot-tracking/research/2026-09-15/aspire-dcp-tunnel-proxies-research.md` - Identifies DCP tunnel proxies as required Aspire infrastructure and records the observed orphaning evidence.
* `.copilot-tracking/research/subagents/2026-09-15/managed-app-host-fixture-cleanup-research.md` - Maps the cleanup failure path, consumers, and focused validation approach.

### Standards References

* `.github/copilot-instructions.md` - Requires focused, documented automated tests and preservation of the local Aspire/Keycloak topology.
* `.github/instructions/aspire-tests.instructions.md` - Requires fixture-owned, awaited distributed application and builder cleanup.
* `.github/instructions/tests.instructions.md` - Requires focused tests and rejects global serialization as a way to hide lifecycle defects.

## Implementation Checklist

### [x] Implementation Phase 1: Make managed AppHost cleanup failure-resilient

<!-- parallelizable: false -->

* [x] Step 1.1: Create a dependency-free shared teardown helper and link it into the fixture and unit-test projects.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 11-36)
* [x] Step 1.2: Route `ManagedAppHostFixture.DisposeAsync` through the helper and null owned fields in `finally` blocks.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 38-57)
* [x] Step 1.3: Harden Functional and E2E wrappers so their controlled IG provider is released after managed teardown failures.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 59-79)

### [x] Implementation Phase 2: Prove cleanup sequencing

<!-- parallelizable: false -->

* [x] Step 2.1: Add API unit tests for failed stop, failed application disposal, aggregated diagnostics, and successful ordering.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 81-107)
* [x] Step 2.2: Run the focused cleanup test before starting Docker-backed validation.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 109-119)

### [x] Implementation Phase 3: Validate normal AppHost lifecycle

<!-- parallelizable: false -->

* [x] Step 3.1: Record the proxy-container baseline and run one focused API AppHost-backed authentication test to normal completion.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 121-140)
* [x] Step 3.2: Inspect any residual proxy's labels and creator PID; treat a residual after normal completion as evidence requiring follow-up, not automatic deletion.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 142-157)

### [~] Implementation Phase 4: Final validation

<!-- parallelizable: false -->

* [~] Step 4.1: Build the solution and run the API integration, Web functional, and Web E2E test projects.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 159-177)
* [x] Step 4.2: Fix only isolated test or teardown defects found by validation; record any normal-completion DCP leak for Aspire/DCP investigation.
  * Details: `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md` (Lines 179-187)

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-15/managed-app-host-fixture-cleanup-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK `10.0.302` and restored solution packages
* Docker Desktop running SQL Server, Mailpit, and Keycloak for closed-box validation
* Aspire hosting and testing packages `13.5.3`

## Success Criteria

* A failure from `StopAsync` does not prevent application and builder disposal. Traces to: User Requirement 1 and research conclusion at `.copilot-tracking/research/2026-09-15/aspire-dcp-tunnel-proxies-research.md` (lines 273-279).
* A failure from application disposal does not prevent builder disposal, and teardown exposes all captured failures. Traces to: Derived Objective 2.
* Functional and E2E wrapper-owned IG providers are released even when managed teardown fails. Traces to: research subagent findings (lines 52-53).
* A focused normal-completion AppHost-backed test leaves no DCP proxy whose labelled creator process has exited. Traces to: User Requirement 1.
* The solution builds and all three AppHost-backed test projects pass. Traces to: final validation phase.
