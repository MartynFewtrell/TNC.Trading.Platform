<!-- markdownlint-disable-file -->
---
applyTo: '.copilot-tracking/changes/2026-09-18/account-preferences-rationalisation-changes.md'
---
# Implementation Plan: Account Preferences Rationalisation

## Overview

Replace the SQL-first account-preferences workflow with IG-confirmed save and explicit Check status convergence while rationalising the operator UI, durable history, tests, and wiki documentation.

## Objectives

### User Requirements

* Implement the changes recommended by the account-preferences rationalisation research. Source: user request and `.copilot-tracking/research/2026-09-18/account-preferences-rationalisation-research.md`.

### Derived Objectives

* Keep page reads SQL-only and make IG effects explicit commands. Derived from: selected approach and existing projection architecture.
* Derive the broker account and audit actor on the server, not from browser input. Derived from: verified snapshot store and claims resolver.
* Make the IG-to-SQL boundary recoverable and idempotent. Derived from: absence of a distributed transaction and the required IG-first confirmation semantics.
* Restore deterministic browser coverage through production-compatible provider behavior and fixture-owned state. Derived from: existing focused E2E failures.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Owns account-preferences routes and command dispatch.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs` - Current SQL-first durable save behavior to replace.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Current-state/audit mapping and migration anchor for the operation journal.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Operator page requiring the terminology and action consolidation.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Controlled provider that must match the production adapter contract.

### References

* `.copilot-tracking/research/2026-09-18/account-preferences-rationalisation-research.md` - Task research and selected behavior.
* `.copilot-tracking/research/subagents/2026-09-18/account-preferences-implementation-path-research.md` - Verified source anchors, test scope, and commands.

### Standards References

* `.github/instructions/architecture.instructions.md` - Vertical-slice and dependency-direction requirements.
* `.github/instructions/csharp.instructions.md` - C# contribution conventions.
* `.github/instructions/tests.instructions.md` - Test naming and traceability requirements.
* `.github/instructions/playwright.instructions.md` - Browser test conventions.
* `.github/instructions/configuration.instructions.md` - Durable configuration and secret-handling standards.

## Implementation Checklist

### [ ] Implementation Phase 1: Durable command contracts and persistence

<!-- parallelizable: false -->

* [x] Step 1.1: Define journal, save, and Check status application contracts, preserving one top-level type per C# file.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 17-48)
* [x] Step 1.2: Add journal persistence, mapping, migration, and atomic finalisation of desired state, audit, observation, projection, and operation phase.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 50-83)
* [x] Step 1.3: Implement IG-first idempotent Save preferences and body-free Check status compare-and-converge commands using the snapshot account, claims actor, lease, revision guard, and gateway readback.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 85-129)
* [x] Step 1.4: Validate application and persistence behavior with focused unit and SQL integration tests.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 131-148)

### [ ] Implementation Phase 2: API and operator UI migration

<!-- parallelizable: false -->

* [x] Step 2.1: Replace browser-facing retry/remediation endpoints with the minimal idempotent save and body-free Check status API contracts.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 154-181)
* [x] Step 2.2: Update the API client, presenter, view model, and Razor page for the consolidated Trailing stops workflow and projection refresh.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 183-218)
* [x] Step 2.3: Validate endpoint and bUnit contracts after the shared API/UI migration.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 220-235)

### [ ] Implementation Phase 3: Provider, functional, and browser coverage

<!-- parallelizable: false -->

* [x] Step 3.1: Correct the controllable IG provider route, payload, session response, and headers; add a production-adapter contract test.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 241-261)
* [x] Step 3.2: Replace functional scenarios with IG-first save and Check status sequence coverage using server-derived ownership.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 263-281)
* [ ] Step 3.3: Make E2E state fixture-owned, use click activation for ordinary tab coverage, and retain one specified keyboard accessibility regression.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 283-306)
  * Execution: After Step 3.1 is validated, Steps 3.2 and 3.3 may run in parallel because they modify separate test projects.

### [ ] Implementation Phase 4: Wiki alignment

<!-- parallelizable: true -->

* [x] Step 4.1: Update operator, runtime, API, architecture, application, local-development, and testing wiki pages for the shipped behavior.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 312-340)

### [ ] Implementation Phase 5: Final validation

<!-- parallelizable: false -->

* [ ] Step 5.1: Run repository build and the affected unit, integration, functional, and E2E partitions.
  * Details: `.copilot-tracking/details/2026-09-18/account-preferences-rationalisation-details.md` (Lines 346-365)
* [ ] Step 5.2: Fix isolated validation failures and rerun the smallest affected partition.
* [ ] Step 5.3: Record blocking failures requiring follow-up planning without expanding this change.

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-18/account-preferences-rationalisation-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK defined by `global.json`
* Docker, SQL Server, Keycloak, and Aspire resources for closed-box functional and E2E tests
* Existing IG login snapshot and authenticated Operator policy
* Entity Framework Core migration tooling

## Success Criteria

* Save preferences changes IG first, confirms by readback, then atomically persists the complete SQL projection. Traces to: research Save Preferences IG-First Durability.
* Check status safely converges IG to the persisted desired value and emits a durable safe warning on failure. Traces to: research Check Status Compare And Converge.
* The operator page shows the consolidated Trailing stops workflow and refreshes its returned projection without reload. Traces to: research Trailing Stops Page Rationalisation.
* Provider, unit, SQL, API, bUnit, functional, and E2E tests cover command, durability, ownership, and browser behavior. Traces to: research Automated Test And Current Failure Resolution.
* Wiki content documents the shipped semantics and local verification path. Traces to: research Wiki Updates.