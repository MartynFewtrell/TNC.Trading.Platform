---
applyTo: '.copilot-tracking/changes/2026-08-30/account-details-automatic-deduplication-test-fix-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Details Automatic Deduplication Test Fix

## Overview

Repair the calendar-dependent Account Details automatic de-duplication unit test with a fixed fixture clock and a production-faithful automatic-trigger predicate, without changing runtime behavior.

## Objectives

### User Requirements

* Implement the changes recommended by the Account Details automatic de-duplication failure research. Source: user-confirmed attachment `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md`
* Preserve the research constraint that the fix remains focused and evidence-based. Source: research task scope and selected recommendation

### Derived Objectives

* Eliminate wall-clock dependence by deriving the test instant, trading day, and handler time from one fixed `TimeProvider`. Derived from: the verified mismatch between the fixture's fixed day and `TimeProvider.System`
* Make the in-memory store model production de-duplication by counting only successful automatic snapshots. Derived from: the EF store predicate and research fidelity finding
* Retain existing neighboring test behavior through optional fixture and snapshot-helper parameters. Derived from: live call-site analysis
* Keep production code and wiki documentation unchanged. Derived from: the test-only root cause and absence of runtime, API, schema, architecture, or operator-facing changes

## Context Summary

### Project Files

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs` - Sole implementation target containing the failing test, fixture, fixed-date setup, and in-memory store
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs` - Confirms the handler correctly derives the trading day from its injected clock
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs` - Defines the production automatic-trigger predicate the fake store must preserve
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj` - Focused validation scope

### References

* `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md` - Primary failure analysis, alternatives, and selected recommendation
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-failure-evidence.md` - Isolated failure evidence
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-fix-alternatives.md` - Fix alternatives analysis
* `.copilot-tracking/research/subagents/2026-08-30/account-details-plan-context.md` - Live line anchors, exact edits, validation commands, and scope decisions

### Standards References

* `.github/copilot-instructions.md` - Repository-wide test documentation, wiki, and scope conventions
* `.github/instructions/architecture.instructions.md` - Direct in-memory handler test and boundary-preservation requirements
* `.github/instructions/csharp.instructions.md` - C# style and test-change requirements
* `.github/instructions/tests.instructions.md` - Deterministic xUnit, naming, documentation, and isolation requirements

## Implementation Checklist

### [x] Implementation Phase 1: Repair the Deterministic Fixture

<!-- parallelizable: false -->

* [x] Step 1.1: Align the test clock and seeded snapshot
  * Details: `.copilot-tracking/details/2026-08-30/account-details-automatic-deduplication-test-fix-details.md` (Lines 12-42)
* [x] Step 1.2: Match the production automatic-trigger predicate
  * Details: `.copilot-tracking/details/2026-08-30/account-details-automatic-deduplication-test-fix-details.md` (Lines 44-72)
* [x] Step 1.3: Run the focused regression test
  * Details: `.copilot-tracking/details/2026-08-30/account-details-automatic-deduplication-test-fix-details.md` (Lines 74-92)

### [x] Implementation Phase 2: Validate and Close Scope

<!-- parallelizable: false -->

* [x] Step 2.1: Run the affected Application unit-test project
  * Details: `.copilot-tracking/details/2026-08-30/account-details-automatic-deduplication-test-fix-details.md` (Lines 98-117)
* [x] Step 2.2: Verify the one-file scope and report blocking issues
  * Details: `.copilot-tracking/details/2026-08-30/account-details-automatic-deduplication-test-fix-details.md` (Lines 119-138)

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-30/account-details-automatic-deduplication-test-fix-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK `10.0.200` selected by `global.json`
* Restored `TNC.Trading.Platform.Application.UnitTests.csproj` dependencies
* Existing xUnit and `TimeProvider` APIs; no new package or infrastructure dependency

## Success Criteria

* `CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure` passes independently of the execution date and local time zone. Traces to: user-confirmed research recommendation
* The fixture uses one fixed instant for retrieval time, trading day, and the tested handler clock. Traces to: research fixed-clock implementation requirements
* The fake existence check requires environment, trading day, and `Automatic` trigger source. Traces to: production EF predicate fidelity finding
* The complete Application unit-test project passes. Traces to: repository validation requirements
* The implementation changes only `AccountDetailsHandlerTests.cs`; production code, wiki pages, and the 2026-08-07 Account Details plan remain unchanged. Traces to: researched scope boundaries