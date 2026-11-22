---
applyTo: '.copilot-tracking/changes/2026-08-31/out-of-schedule-authentication-transition-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Resolve Out-of-Schedule Authentication Transition Failure

## Overview

Make continued out-of-schedule authentication reconciliation refresh persisted metadata without requesting an invalid self-transition, while preserving strict transition rules, entry-only side effects, startup fail-fast behavior, and operator-visible documentation.

## Objectives

### User Requirements

* Create an actionable plan to resolve the out-of-schedule authentication transition failure. Source: User request on 2026-08-31.
* Resolve the verified repeated authentication-state transition at its owning policy boundary. Source: `.copilot-tracking/research/2026-08-31/out-of-schedule-authentication-transition-research.md` (Lines 122-157).
* Include focused regression tests and affected documentation. Source: `.copilot-tracking/research/2026-08-31/out-of-schedule-authentication-transition-research.md` (Lines 158-176).

### Derived Objectives

* Reuse the timestamp captured for schedule evaluation when refreshing or transitioning state. Derived from: one-instant reconciliation semantics verified in `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 84-93).
* Preserve `LastTransitionAtUtc`, retry state, events, and notifications during continued inactivity. Derived from: the distinction between state transitions and successful same-state validation.
* Keep `OutOfSchedule -> OutOfSchedule` invalid in `PlatformStateTransitionEngine`. Derived from: the explicit graph and rejection-before-mutation contract.
* Use deterministic in-memory tests for both changed and unchanged inactive reasons. Derived from: the missing composed regression cases and repository test standards.
* Update the wiki source of truth before treating implementation as complete. Derived from: `.github/copilot-instructions.md` and `.github/instructions/docs.instructions.md`.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - Owns schedule routing, same-state policy, real transition application, side effects, and durable save orchestration.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs` - Owns the strict status graph and rejects self-transitions before mutation.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs` - Exercises the real reconciler with in-memory EF, a mutable clock, event storage, retry storage, and recorded notifications.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs` - Directly verifies graph acceptance, rejection, timestamps, and non-mutation.
* `docs/wiki/runtime-behavior.md` - Runtime state, schedule, event, notification, and timestamp contract.
* `docs/wiki/architecture.md` - Reconciliation, startup, Application policy, and transition-engine ownership.
* `docs/wiki/operator-guide.md` - Operator interpretation and startup troubleshooting.
* `docs/wiki/testing-and-quality.md` - Test coverage and quality-gate workflow.

### References

* `.copilot-tracking/research/2026-08-31/out-of-schedule-authentication-transition-research.md` - Primary verified defect research and selected repair.
* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` - Current implementation anchors, corrected deterministic scenario, and validation commands.
* `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` - Step-level implementation instructions and success criteria.

### Standards References

* `.github/copilot-instructions.md` - Application policy placement, test documentation, wiki completion, and repository validation requirements.
* `.github/instructions/architecture.instructions.md` - Keep the business decision in the Application use case and avoid speculative abstractions.
* `.github/instructions/csharp.instructions.md` - Preserve async/error semantics and add behavior-focused tests.
* `.github/instructions/tests.instructions.md` - xUnit naming, XML traceability comments, deterministic execution, and unit-test boundaries.
* `.github/instructions/refactoring.instructions.md` - Keep the fix narrow and preserve public contracts.
* `.github/instructions/auth.instructions.md` - Preserve authentication safety and identity-provider behavior.
* `.github/instructions/configuration.instructions.md` - Preserve SQL-backed configuration and secret boundaries.
* `.github/instructions/docs.instructions.md` - Update wiki behavior and validate navigation and links.

## Implementation Checklist

### [x] Implementation Phase 1: Correct Continued-Inactivity Reconciliation

<!-- parallelizable: false -->

* [x] Step 1.1: Pass the `now` captured by `ReconcileCoreAsync` into `TransitionToOutOfScheduleAsync` and use it for refresh and entry-transition timestamps.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 16-42).
* [x] Step 1.2: Treat every existing `OutOfSchedule` result as an in-state refresh of `BlockedReason` and `LastValidatedAtUtc`, returning before transition and entry side effects.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 44-74).
* [x] Step 1.3: Run the existing focused `Active -> OutOfSchedule` workflow test immediately after the production edit.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 76-86).

### [x] Implementation Phase 2: Add Focused Regression Coverage

<!-- parallelizable: true -->

Phase 2 starts after Phase 1. The Infrastructure workflow tests and Application transition-engine test touch separate files and projects, so their implementation and scoped validation can run in parallel.

* [x] Step 2.1: Add changed-reason and unchanged-reason continued-inactivity tests to `AuthRetryCycleTests` using the mutable clock and a deterministic time-window-to-bank-holiday rollover.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 94-130).
* [x] Step 2.2: Add an `OutOfSchedule` self-transition rejection and complete non-mutation test to `PlatformStateTransitionEngineTests`.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 132-161).
* [x] Step 2.3: Run each affected test class in its owning project.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 127-130 and 159-161).

### [x] Implementation Phase 3: Update Runtime and Operator Documentation

<!-- parallelizable: true -->

Phase 3 starts after Phase 1 and can run alongside Phase 2. The four wiki files can be assigned independently if terminology remains aligned with the selected behavior.

* [x] Step 3.1: Update runtime and architecture pages with reason rollover, timestamp, side-effect, strict-graph, and ownership semantics.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 169-195).
* [x] Step 3.2: Update operator and testing pages with expected continued-inactivity behavior, troubleshooting boundaries, and regression coverage.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 197-223).
* [x] Step 3.3: Check editor diagnostics, whitespace, relative links, and heading fragments for all changed wiki pages.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 225-238).

### [x] Implementation Phase 4: Final Validation

<!-- parallelizable: false -->

* [x] Step 4.1: Run both focused suites and the complete Application and Infrastructure unit-test projects.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 244-261).
* [x] Step 4.2: Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId out-of-schedule-reconciliation` and retain generated evidence.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 263-282).
* [x] Step 4.3: Fix and revalidate only minor task-introduced issues; report failures requiring broader research or architecture changes.
  * Details: `.copilot-tracking/details/2026-08-31/out-of-schedule-authentication-transition-details.md` (Lines 284-291).

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-31/out-of-schedule-authentication-transition-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK baseline `10.0.200` requested by `global.json` with `latestFeature` roll-forward.
* Existing Application and Infrastructure xUnit projects.
* Existing in-memory EF reconciliation fixture and deterministic `TestTimeProvider`.
* Existing `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1` quality-gate script.
* Editor diagnostics and manual link inspection for wiki validation because no repository Markdown validator is available.

## Success Criteria

* Startup reconciliation succeeds when durable status is already `OutOfSchedule`, whether the inactive reason changes or remains the same. Traces to: user requirement and primary research technical scenario.
* Continued inactivity updates `BlockedReason` and `LastValidatedAtUtc` while preserving `LastTransitionAtUtc`, retry/session state, event count, and notification count. Traces to: selected metadata-refresh semantics.
* `PlatformStateTransitionEngine` still rejects `OutOfSchedule -> OutOfSchedule` without mutation. Traces to: explicit graph requirement.
* Two persisted workflow tests and one direct policy test lock the repaired contract. Traces to: focused regression-test requirement.
* Runtime, architecture, operator, and testing wiki pages describe the implemented behavior. Traces to: repository wiki source-of-truth requirement.
* Focused suites, affected projects, solution build, and solution tests pass through the repository quality gate. Traces to: repository validation standard.