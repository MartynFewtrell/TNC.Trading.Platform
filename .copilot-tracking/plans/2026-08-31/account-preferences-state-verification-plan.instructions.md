---
description: 'Implements durable account preference intent and account-bound IG verification'
applyTo: '.copilot-tracking/changes/2026-08-31/account-preferences-state-verification-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Preferences State Verification

## Overview

Replace provider-first account-preference reads with a durable SQL desired/current-state projection, account-bound verification, explicit remediation, and status-aware operator UI.

## Objectives

### User Requirements

* Implement the recommendation in the account-preferences state-verification research — Source: user request and `.copilot-tracking/research/2026-08-31/account-preferences-state-verification-research.md`.
* Display the persisted database preference independently of IG availability — Source: research Task Implementation Requests and Executive Recommendation.
* Verify the configured preference against IG when a usable trading session becomes available — Source: research Task Implementation Requests.
* Show failed or mismatched verification beside the database-derived value — Source: research Task Implementation Requests.
* Correct the generic `Unable to load account preferences` behavior — Source: research Failure Diagnosis.

### Derived Objectives

* Preserve SQL as durable operator intent and IG as observed external fact — Derived from: selected research architecture.
* Bind observations and remediation to the configured IG account, desired revision, and one provider session attempt — Derived from: IG token semantics and stale-completion risk.
* Keep API startup independent of IG preference verification and process durable due work in a hosted supervisor — Derived from: availability and replica-safety requirements.
* Preserve API compatibility while moving verification out of the request path — Derived from: current synchronous `200 OK` consumer contract.
* Establish a future fail-closed trading-gate contract without implementing order submission — Derived from: no current order-producing workflow.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/` - Current provider-first query/update handlers and new application operations.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/` - Observation persistence, new current-state store, lease, and migration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Account-bound single-session provider work.
* `src/TNC.Trading.Platform.Api/Features/Platform/` - Existing routes, mappings, and new retry/remediation endpoints.
* `src/TNC.Trading.Platform.Api/Hosting/` - Durable due-work reconciliation supervisor.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/` - Desired/observed status UI and independent error handling.
* `test/` - Unit, SQL integration, Aspire closed-box, and Playwright coverage.
* `docs/wiki/` - Implemented API, architecture, runtime, operator, local-development, and testing behavior.

### References

* `.copilot-tracking/research/2026-08-31/account-preferences-state-verification-research.md` - Primary recommendation, diagnosis, scenarios, and defaults addendum.
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-implementation-planning-research.md` - Verified file inventory, patterns, tests, and commands.

### Standards References

* `.github/copilot-instructions.md` - Repository architecture, testing, documentation, and startup rules.
* `.github/instructions/architecture.instructions.md` - Operation contracts and dependency direction.
* `.github/instructions/configuration.instructions.md` - SQL-owned operator settings and audit requirements.
* `.github/instructions/auth.instructions.md` - Account-bound authentication constraints.
* `.github/instructions/tests.instructions.md` - Test naming and traceability.
* `.github/instructions/aspire-tests.instructions.md` - Closed-box distributed-app testing.
* `.github/instructions/playwright.instructions.md` - Stable accessible UI automation.

## Implementation Checklist

### [x] Implementation Phase 1: Diagnostic Baseline

<!-- parallelizable: true -->

* [x] Step 1.1: Separate current-state and history failures in the presenter and retain successful state when history fails.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 16-37)
* [x] Step 1.2: Preserve useful API Problem Details and forward the event store on provider failures.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 38-57)
* [x] Step 1.3: Run focused Application and Web diagnostic tests.

### [ ] Implementation Phase 2: Durable State Foundation

<!-- parallelizable: true -->

* [x] Step 2.1: Define current-state, status, store, revision, due-work, and lease application contracts.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 68-93)
* [x] Step 2.2: Add EF persistence, account-bound observations, audit, migration, compare-and-set, and SQL lease.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 94-125)
* [ ] Step 2.3: Validate migration, restart durability, atomicity, revision monotonicity, stale completion, and lease contention.

### [x] Implementation Phase 3: Account-Bound Provider Boundary

<!-- parallelizable: true -->

* [x] Step 3.1: Replace independent gateway methods with account-bound observe and remediation operations.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 138-157)
* [x] Step 3.2: Keep `GET`, optional single `PUT`, and confirming `GET` in one token context with strict account checks.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 158-173)
* [x] Step 3.3: Run focused IG gateway tests.

### [x] Implementation Phase 4: Application Reconciliation and Remediation

<!-- parallelizable: false -->

* [x] Step 4.1: Convert current `GET` and desired-state `PUT` handlers to SQL-first operations.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 183-204)
* [x] Step 4.2: Implement observe-only reconciliation, retry policy, revision guards, and account-partitioned lease use.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 205-222)
* [x] Step 4.3: Implement explicit revision-bound remediation and manual retry commands.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 223-242)
* [ ] Step 4.4: Run focused application policy tests.

### [x] Implementation Phase 5: API and Web Contracts

<!-- parallelizable: false -->

* [x] Step 5.1: Expand the API projection and add authorized retry and remediation routes with Problem Details.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 252-275)
* [x] Step 5.2: Update the Web client, presenter, view model, and page to show desired and observed values independently.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 276-296)
* [x] Step 5.3: Run focused API and bUnit tests for all statuses, accessibility, and stale revisions.

### [ ] Implementation Phase 6: Background and Closed-Box Integration

<!-- parallelizable: false -->

* [x] Step 6.1: Register a non-blocking scoped supervisor and nudge it from automatic and manual successful-authentication paths.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 307-335)
* [ ] Step 6.2: Add the configurable account-preferences provider URL, WireMock double, and Aspire closed-box convergence scenario.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 336-361)
* [ ] Step 6.3: Add the stable Playwright operator flow after final UI wording is fixed.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 362-374)

### [x] Implementation Phase 7: Documentation Alignment

<!-- parallelizable: true -->

* [x] Step 7.1: Update all seven affected wiki pages to match implemented contracts and operator behavior.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 379-398)
* [x] Step 7.2: Document the future order-boundary fail-closed requirement without claiming current trade readiness.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 399-412)

### [ ] Implementation Phase 8: Final Validation

<!-- parallelizable: false -->

* [ ] Step 8.1: Run migration inspection, full build, focused suites, closed-box tests, and full solution tests.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 417-432)
* [x] Step 8.2: Fix isolated validation defects and rerun the failing scope.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 433-436)
* [x] Step 8.3: Report blockers requiring new research rather than widening implementation scope.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-state-verification-details.md` (Lines 437-440)

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-31/account-preferences-state-verification-log.md` for discrepancy tracking, selected defaults, implementation paths, and follow-on work.

## Dependencies

* .NET SDK selected by `global.json`
* Docker-backed Aspire SQL Server and Keycloak environment
* EF Core CLI compatible with the repository SDK
* Controllable IG provider double for closed-box verification
* Operator authorization for the explicit target IG account

## Success Criteria

* The current preference `GET` reads SQL only and remains available during IG failure — Traces to: user database-display requirement.
* New and migrated installations remain `Unconfigured` until explicit operator intent is committed — Traces to: primary research bootstrap rule.
* Every observation is bound to the configured account, desired revision, attempt identifier, and observation time — Traces to: account/session research.
* Reconciliation records `InSync`, `Drifted`, `VerificationFailed`, or `Unsupported` without writing to IG — Traces to: observe-only recommendation.
* Explicit remediation rejects stale revisions and performs initial `GET`, at most one `PUT`, and confirming `GET` in one session context — Traces to: remediation recommendation.
* API startup succeeds when IG verification is unavailable, while due work persists and retries — Traces to: startup availability requirement.
* The UI always labels `Desired setting` and `Last observed at IG` separately and preserves current state when history fails — Traces to: user display and failure requirements.
* Focused and full validation pass, and affected wiki pages describe the implemented behavior — Traces to: repository completion rules.