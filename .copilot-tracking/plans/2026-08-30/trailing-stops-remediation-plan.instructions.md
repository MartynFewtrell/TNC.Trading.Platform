---
applyTo: '.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Trailing-Stops Remediation

## Overview

Restore deterministic API startup and bounded test signal first, then correct provider, application, persistence, environment, retention, and UI behavior without rewriting potentially deployed migration history or calling real IG from automated tests.

## Objectives

### User Requirements

* Resolve the observed post-implementation failures using the supplied research — Source: user request and `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md`
* Produce an actionable implementation plan rather than modify product or test code — Source: current Task Planner request
* Resolve the five currently reproduced failures across four causal clusters — Source: user-provided `.copilot-tracking/research/2026-08-30/failing-tests-root-cause-research.md`

### Derived Objectives

* Restore API route composition before broad distributed validation — Derived from: reproduced missing-service startup exception
* Bound focused diagnostic, fixture, and startup checks below 60 seconds with process-tree cleanup, while using finite hang detection for the healthy full repository gate — Derived from: fixture failure cascades, unbounded SQL risk, and the measured 134.9-second repository baseline
* Correct the IG wire contract, confirmation semantics, and operational telemetry before treating focused green tests as release evidence — Derived from: static integration audit
* Preserve discoverable migration history until all non-disposable environments are inventoried — Derived from: duplicate table proof and unknown deployment scope
* Partition observation history, reject invalid cursors, enforce Test/Demo compatibility, unify retention, and cancel disposed UI work — Derived from: correctness defects hidden by existing tests
* Update wiki and durable delivery records only after executable gates pass — Derived from: repository completion rules and conflicting historical validation claims
* Correct test contracts without changing production behavior where the executing contract is already correct — Derived from: verified Application, Web, and SQL GUID root causes
* Reconcile EF migration lineage only after non-disposable database and released-assembly evidence is complete — Derived from: conflicting candidate migrations and unknown deployment history

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs` - Explicit API application-service composition root missing four registrations
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Account-preferences route graph that currently fails construction
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Incorrect provider property and acknowledgement classification
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` - Confirmation mismatch and telemetry policy
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework` - Duplicate model/migration artifacts and global history query
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Uncancelled component HTTP lifetime
* `test/TNC.Trading.Platform.Api` - Route composition and bounded distributed fixture coverage
* `test/TNC.Trading.Platform.Infrastructure` - Provider, persistence, migration, cursor, and retention coverage
* `docs/wiki` - Implementation documentation source of truth to update after validation

### References

* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md` - Primary failure analysis and selected remediation sequence
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-plan-verification.md` - Current-workspace anchors, test homes, commands, and unresolved gates
* `.copilot-tracking/research/2026-08-30/failing-tests-root-cause-research.md` - Reproduced five-test baseline and four-cluster causal analysis
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-plan-verification-research.md` - Residual implementation surfaces, phase dependencies, and validation commands
* `https://labs.ig.com/reference/accounts-preferences.html` - Authoritative provider property contract

### Standards References

* `.github/copilot-instructions.md` - Repository-wide composition, tests, wiki, and delivery rules
* `.github/instructions/architecture.instructions.md` - Boundary ownership and vertical-slice rules
* `.github/instructions/aspire-tests.instructions.md` - Distributed fixture lifecycle and cleanup
* `.github/instructions/configuration.instructions.md` - Operator configuration and sensitive-data handling
* `.github/instructions/tests.instructions.md` - Test structure, naming, documentation, and determinism
* `.github/instructions/radzen-blazor.instructions.md` - Web UI conventions

## Implementation Checklist

### [x] Implementation Phase 0: Resolve Migration and Contract Gates

<!-- parallelizable: true -->

* [x] Step 0.1: Inventory `20260830112227_SnapshotRepair` in every non-disposable database without changing schema
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 14-35)
* [x] Step 0.2: Confirm or record defaults for HTTP 409 mismatch behavior and shared operational retention
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 36-57)
  * Migration inventory blocks Phase 4 only; it does not block Phases 1 through 3

### [x] Implementation Phase 1: Restore API Composition

<!-- parallelizable: true -->

* [x] Step 1.1: Add the four scoped account-preferences handler and validator registrations
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 62-82)
* [x] Step 1.2: Add in-process route-construction and service-resolution regression coverage
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 83-101)
* [x] Step 1.3: Build API and run only the composition test under the watchdog
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 102-109)

### [x] Implementation Phase 2: Make Distributed Failures Bounded and Diagnostic

<!-- parallelizable: false -->

* [x] Step 2.1: Propagate one sub-60-second cancellation chain through Aspire lifecycle and discovery
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 114-143)
* [x] Step 2.2: Add finite cancellation and command timeouts to SQL fixture operations
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 144-164)
* [x] Step 2.3: Replace the independent account-details test's system clock with a fixed clock
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 165-184)
* [x] Step 2.4: Run one exact-filter fact per distributed fixture and retain diagnostics
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 185-206)

### [x] Implementation Phase 3: Correct Provider and Command Semantics

<!-- parallelizable: false -->

* [x] Step 3.1: Use typed `trailingStopsEnabled` DTOs and classify malformed 2xx PUT acknowledgements as indeterminate
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 211-230)
* [x] Step 3.2: Require exact readback equality and return typed not-applied behavior on mismatch
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 231-256)
* [x] Step 3.3: Emit one sanitized event for each failed or indeterminate provider operation
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 257-273)
* [x] Step 3.4: Map not-applied behavior to HTTP 409 Problem Details
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 274-291)
* [x] Step 3.5: Run gateway, Application, and API slices separately under the watchdog
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 292-300)

### [x] Implementation Phase 4: Reconcile Migration and History Persistence

<!-- parallelizable: false -->

* [x] Step 4.1: Apply the inventory-approved migration strategy, preserving `SnapshotRepair` by default
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 305-331)
* [x] Step 4.2: Partition history by platform and broker environment and reject invalid cursor membership
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 332-355)
* [x] Step 4.3: Add fresh-database, applied-migration, index, paging, and cursor SQL coverage
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 356-371)
* [x] Step 4.4: Validate migration identity, unit behavior, and one SQL fact at a time
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 372-380)

### [x] Implementation Phase 5: Complete Environment, Retention, and UI Behavior

<!-- parallelizable: true -->

* [x] Step 5.1: Require platform Test plus broker Demo and present Test externally
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 385-404)
* [x] Step 5.2: Remove inert retention configuration and use `OperationalRecordsDays` consistently
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 405-427)
* [x] Step 5.3: Cancel account-preferences HTTP operations when the Blazor component is disposed
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 428-447)
* [x] Step 5.4: Validate environment, retention, and Web slices independently
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 448-456)

### [x] Implementation Phase 6: Documentation and Delivery Records

<!-- parallelizable: false -->

* [x] Step 6.1: Update all six affected wiki pages after corresponding executable gates pass
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 461-480)
* [x] Step 6.2: Create the remediation changes record and correct overstated historical release claims
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 481-497)

### [~] Implementation Phase 7: Final Validation

<!-- parallelizable: false -->

* [x] Step 7.1: Run ordered focused gates with per-command process-tree deadlines below 60 seconds
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 502-514)
* [x] Step 7.2: Run serial solution build and test with finite hang detection only after focused gates pass
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 515-530)
* [~] Step 7.3: Capture sanitized, operator-controlled manual IG Test GET/PUT evidence and rollback
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 531-546)
* [x] Step 7.4: Fix only isolated validation issues and report larger blockers for new planning
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 547-554)

### [x] Implementation Phase 8: Resolve Residual Migration Decision Gate

<!-- parallelizable: true -->

* [x] Step 8.1: Inventory both candidate migration IDs across every non-disposable database and released Infrastructure assembly
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 559-581)
* [x] Step 8.2: Select and record exactly one evidence-backed canonical lineage path and broker-only index decision
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 583-604)
  * This phase blocks migration source changes and final migration-ID assertions, but does not block Phase 9

### [x] Implementation Phase 9: Correct Independent Test Contracts

<!-- parallelizable: true -->

* [x] Step 9.1: Make the Application fake return the complete page contract and configure the expected cursor
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 610-631)
* [x] Step 9.2: Make the Web delayed-content fake cancellation-aware and synchronize assertions with cancellation completion
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 633-655)
* [x] Step 9.3: Run the exact Application and Web facts, the owning unit projects, and ten Web disposal repetitions
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 657-668)

### [x] Implementation Phase 10: Reconcile EF Lineage and SQL Test Contracts

<!-- parallelizable: false -->

* [x] Step 10.1: Reconcile the migration main file, designer metadata, model snapshot, and generated SQL using the selected lineage branch
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 674-696)
* [x] Step 10.2: Rebuild historical recovery fixtures through the initial EF migration and replace hard-coded migration counts
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 698-720)
* [x] Step 10.3: Compare keyset pages with SQL Server GUID ordering and assert complete page continuity
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 722-743)
* [x] Step 10.4: Run EF metadata checks and Infrastructure integration tests serially
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 745-759)

### [x] Implementation Phase 11: Residual-Failure Final Validation

<!-- parallelizable: false -->

* [x] Step 11.1: Build the solution and run the complete repository gate serially after all focused checks pass
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 765-781)
* [x] Step 11.2: Record corrected evidence, fix only isolated validation defects, and report larger blockers
  * Details: `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md` (Lines 783-793)

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-30/trailing-stops-remediation-log.md` for DR-01 through DR-13, implementation paths considered, selected defaults, and suggested follow-on work.

## Dependencies

* .NET SDK 10.0.200 pinned by `global.json`
* Docker-backed SQL Server and Keycloak prerequisites
* EF Core CLI compatible with the pinned SDK
* Read-only migration-history access for every non-disposable environment
* External process-tree watchdog with bounded output capture
* API and product ownership for contract confirmation, with research-backed defaults when deferred

## Success Criteria

* API constructs and starts with all four account-preferences dependencies explicitly registered — Traces to: reproduced startup root cause
* Failed distributed startup produces direct diagnostics in under 60 seconds instead of a multiplied fixture cascade — Traces to: reported test failures
* Provider payload, confirmation, telemetry, and HTTP mismatch behavior match the researched contract — Traces to: IG reference and DR-02
* Migration lineage supports fresh and already-applied databases without duplicate table creation — Traces to: DR-01 and DD-01
* History partitioning and cursor validation prevent cross-environment data mixing and silent page resets — Traces to: static integration audit
* Test/Demo presentation, retention, and component cancellation are behaviorally covered — Traces to: verified hidden correctness defects
* Wiki and delivery records match retained executable evidence — Traces to: repository completion requirements
* All focused and solution gates pass without automated calls to real IG — Traces to: user-supplied research constraints
* The Application fake forwards a complete page, the Web test observes cancellation, and SQL paging matches provider order without production changes — Traces to: current five-failure research clusters
* Discovered, applied, and pending migration sets agree for fresh, guarded-baseline, conflict-recovery, and rerun scenarios — Traces to: migration recovery failures and DR-09
