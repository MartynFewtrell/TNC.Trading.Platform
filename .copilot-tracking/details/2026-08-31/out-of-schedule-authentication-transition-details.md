<!-- markdownlint-disable-file -->
# Implementation Details: Resolve Out-of-Schedule Authentication Transition Failure

## Context Reference

Sources:

* `.copilot-tracking/research/2026-08-31/out-of-schedule-authentication-transition-research.md` - Verified root cause, selected semantics, test intent, and documentation impact.
* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` - Current line anchors, fixture constraints, deterministic test scenario, and executable validation commands.
* User request on 2026-08-31 - Create an implementation plan to resolve the researched startup failure.

## Implementation Phase 1: Correct Continued-Inactivity Reconciliation

<!-- parallelizable: false -->

### Step 1.1: Reuse the reconciliation timestamp for out-of-schedule handling

Modify the schedule-blocked branch in `ReconcileCoreAsync` to pass its already-captured `now` value into `TransitionToOutOfScheduleAsync`. Update the helper signature and use the supplied timestamp for both metadata refreshes and genuine entry transitions. This preserves one authoritative instant for schedule evaluation and persisted validation.

Files:

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - Pass the captured timestamp through the owning reconciliation path.

Discrepancy references:

* `DD-01` - Refines the original research sample to reuse the outer timestamp instead of reading the clock again.

Success criteria:

* Schedule evaluation and out-of-schedule persistence use the same `DateTimeOffset` value.
* Existing cancellation, exception propagation, and save behavior remain unchanged.
* No public contract, database schema, configuration shape, or startup orchestration changes.

Context references:

* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 23-31) - Owning reconciliation path and captured timestamp.
* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 84-93) - Smallest production edit.

Dependencies:

* .NET SDK baseline `10.0.200` requested by `global.json` with `latestFeature` roll-forward.
* Existing `PlatformAuthenticationReconciler` state-store and side-effect dependencies.

### Step 1.2: Refresh metadata without submitting a self-transition

At the beginning of `TransitionToOutOfScheduleAsync`, branch on `currentState.SessionStatus == PlatformSessionStatus.OutOfSchedule`. For every continued-inactive result, assign the current schedule reason to `BlockedReason`, assign the supplied timestamp to `LastValidatedAtUtc`, and return. Preserve `LastTransitionAtUtc`, session fields, degradation fields, retry fields, `CurrentRetryCycleId`, retry-cycle persistence, operational events, and notification state. Leave the existing transition-engine invocation and entry side effects unchanged for actual status changes.

Files:

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - Own same-state reconciliation policy at the use-case boundary.

Discrepancy references:

* `DR-01` - Excludes production-row inspection because the deterministic contract mismatch is sufficient to implement the fix.
* `DR-02` - Leaves duplicate startup invocation analysis outside this defect repair.

Success criteria:

* A changed inactive reason while already `OutOfSchedule` no longer calls `PlatformStateTransitionEngine.Apply`.
* An unchanged inactive reason still advances `LastValidatedAtUtc`.
* `BlockedReason` always reflects the current schedule result after successful reconciliation.
* `LastTransitionAtUtc` changes only on a real status transition.
* Continued inactivity produces no retry-cycle mutation, operational event, or notification.
* Genuine invalid transitions continue to propagate and can fail startup.

Context references:

* `.copilot-tracking/research/2026-08-31/out-of-schedule-authentication-transition-research.md` (Lines 137-157) - Preferred metadata-refresh semantics.
* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 32-54) - Current side effects and strict transition graph.
* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 335-351) - Verified ownership and behavioral invariants.

Dependencies:

* Step 1.1 completion.

### Step 1.3: Validate the production edit with the existing focused workflow test

Run the pre-existing out-of-schedule workflow test before adding regression cases. This is the cheapest executable check that the actual `Active -> OutOfSchedule` entry behavior, event emission, and persistence path remain intact.

Validation command:

* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~GetStatusAsync_ShouldShowOutOfScheduleProjectionWithoutFailureSummary_WhenTradingScheduleBecomesInactive"` - Existing entry-transition workflow.

Success criteria:

* The focused test passes without changing its expected entry-transition behavior.

## Implementation Phase 2: Add Focused Regression Coverage

<!-- parallelizable: true -->

Phase 2 starts after Phase 1. Steps 2.1 and 2.2 touch different test projects and can be implemented and validated in parallel.

### Step 2.1: Cover persisted continued-inactivity reconciliation

Add two documented xUnit tests immediately after the existing out-of-schedule workflow case in `AuthRetryCycleTests`. Follow the file's XML summary convention with `Trace`, `Verifies`, `Expected`, and `Why` statements. Do not add Arrange/Act/Assert comments.

For `TickAsync_ShouldRefreshOutOfScheduleMetadataWithoutTransition_WhenInactiveReasonChanges`, start the mutable clock on Friday 2026-04-03 after the configured active window and add Saturday 2026-04-04 to `Bootstrap:TradingSchedule:BankHolidayExclusions:0`. Tick once to enter `OutOfSchedule` with the time-window reason, capture persisted state and side-effect counts, advance into Saturday, and tick again. Assert the bank-holiday reason and advanced validation timestamp, while transition timestamp, retry fields, current retry-cycle identifier, retry-cycle count, inactive-event count, and notification count remain unchanged.

For `TickAsync_ShouldRefreshValidationWithoutSideEffects_WhenOutOfScheduleReasonIsUnchanged`, tick after hours, capture persisted state and side-effect counts, advance one minute without leaving the same inactive condition, and tick again. Assert the same reason, advanced validation timestamp, unchanged transition timestamp and retry state, exactly one entry event, and no additional retry-cycle or notification activity.

Files:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs` - Add composed in-memory EF regression tests through the real reconciler.

Discrepancy references:

* `DD-02` - Uses a time-window-to-bank-holiday rollover instead of Friday-to-Saturday inactive-day rollover because the fixture explicitly enables all seven days.

Success criteria:

* Both tests fail against the old reason-sensitive transition guard and pass with the metadata-refresh implementation.
* Tests use `TestTimeProvider.Advance` and contain no sleeps or dependence on wall-clock time.
* Assertions prove persistence, timestamp, retry, event, and notification semantics.
* Test method names follow `MethodName_StateUnderTest_ExpectedResult`.
* Each test has the repository-required requirement and regression-risk documentation.

Context references:

* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 99-164) - Fixture APIs, insertion point, deterministic rollover, and required assertions.
* `.github/instructions/tests.instructions.md` (Lines 25-70) - xUnit naming, documentation, and deterministic unit-test rules.

Dependencies:

* Implementation Phase 1 completion.
* Existing in-memory EF fixture, `TestTimeProvider`, recorded notifications, and event store.

Validation command:

* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AuthRetryCycleTests"` - Composed reconciliation workflow suite.

### Step 2.2: Lock the strict transition graph

Add `Apply_ShouldRejectSelfTransition_WhenCurrentStateIsOutOfSchedule` after the existing rejection tests. Initialize a fully populated `OutOfSchedule` state, submit another `OutOfSchedule` transition, and assert rejection plus complete non-mutation of status, reason, session interval, degraded flag, validation timestamp, and transition timestamp. Document the strict-graph requirement and why metadata refresh belongs in reconciliation policy.

Files:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs` - Add a direct policy regression test.

Discrepancy references:

* None. This directly preserves the selected implementation path.

Success criteria:

* The self-transition remains rejected with the expected transition error.
* No authentication-state field changes on rejection.
* The test follows repository naming and XML documentation requirements.

Context references:

* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 166-178) - Existing graph tests and insertion point.
* `.copilot-tracking/research/2026-08-31/out-of-schedule-authentication-transition-research.md` (Lines 159-162) - Required strict-graph test.

Dependencies:

* Implementation Phase 1 completion for final combined validation; test authoring itself is file-independent from Step 2.1.

Validation command:

* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~PlatformStateTransitionEngineTests"` - Transition-policy suite.

## Implementation Phase 3: Update Runtime and Operator Documentation

<!-- parallelizable: true -->

Phase 3 can run in parallel with Phase 2 after Phase 1 fixes the behavioral contract. The four pages are independent files, but their terminology must remain consistent.

### Step 3.1: Document reconciliation and ownership semantics

Update runtime behavior to describe reason rollover, same-state validation refresh, unchanged transition timestamps, the absent self-edge, and entry-only `TradingScheduleInactive` events. Update architecture guidance to retain fail-fast initial reconciliation while assigning continued-inactivity metadata refresh to the Application reconciler and transition validation to the engine.

Files:

* `docs/wiki/runtime-behavior.md` - Define observable state, timestamp, event, retry, and notification behavior.
* `docs/wiki/architecture.md` - Clarify Application ownership without changing startup or hosting composition.

Discrepancy references:

* `DR-02` - Explicitly preserve both current startup reconciliation invocations as separate follow-on scope.

Success criteria:

* Documentation states that `BlockedReason` can change while status remains `OutOfSchedule`.
* Documentation distinguishes validation freshness from transition time.
* The state graph still omits `OutOfSchedule -> OutOfSchedule`.
* Events and retry cleanup are described as entry-transition side effects only.

Context references:

* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 188-209) - Runtime and architecture sections to update.

Dependencies:

* Implementation Phase 1 completion.

### Step 3.2: Document operator and test expectations

Update the operator guide so `OutOfSchedule` is described as showing the current governing reason, normal reason rollover is non-fatal, and genuine startup failures remain fail-fast. Update testing guidance to record the in-memory EF reason-rollover coverage, strict transition graph test, persisted timestamp checks, and side-effect assertions.

Files:

* `docs/wiki/operator-guide.md` - Clarify expected behavior and troubleshooting boundaries.
* `docs/wiki/testing-and-quality.md` - Record the focused regression suites and validation evidence.

Discrepancy references:

* None. These updates implement the research documentation recommendation.

Success criteria:

* Operators can distinguish normal inactive-reason rollover from genuine startup policy or persistence failures.
* Testing documentation identifies both the composed reconciliation tests and direct transition-engine test.
* Existing headings, relative links, and wiki navigation remain valid.

Context references:

* `.copilot-tracking/research/subagents/2026-08-31/out-of-schedule-plan-verification-research.md` (Lines 210-228) - Operator and testing sections to update.
* `.github/instructions/docs.instructions.md` (Lines 20-70) - Wiki maintenance and link requirements.

Dependencies:

* Implementation Phase 1 completion.

### Step 3.3: Validate changed documentation

Use editor diagnostics for each changed wiki page, inspect changed lines for trailing whitespace, and manually verify affected relative links and heading anchors. Do not claim unavailable frontmatter, Markdown lint, or link-check commands.

Validation checks:

* VS Code diagnostics for `docs/wiki/runtime-behavior.md`, `docs/wiki/architecture.md`, `docs/wiki/operator-guide.md`, and `docs/wiki/testing-and-quality.md`.
* `git diff --check -- docs/wiki/runtime-behavior.md docs/wiki/architecture.md docs/wiki/operator-guide.md docs/wiki/testing-and-quality.md` - Whitespace validation only.
* Manual inspection of affected relative links and generated heading fragments.

Success criteria:

* No new diagnostics or whitespace errors.
* Affected wiki links and heading fragments resolve.

## Implementation Phase 4: Final Validation

<!-- parallelizable: false -->

### Step 4.1: Run focused and project-level tests

Run both focused suites first. Then run the complete Application and Infrastructure unit-test projects to catch neighboring regressions.

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AuthRetryCycleTests"`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~PlatformStateTransitionEngineTests"`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj`

Success criteria:

* All focused and project-level tests pass.

Dependencies:

* Implementation Phases 1 through 3 complete.

### Step 4.2: Run the repository quality gate

Run the existing Clean Architecture quality-gate script with a task-specific identifier. It builds and tests `TNC.Trading.Platform.slnx` and stores logs and TRX files under `artifacts/quality-gates/out-of-schedule-reconciliation/<timestamp>/`.

Validation command:

* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId out-of-schedule-reconciliation`

Fallback command:

* `dotnet test TNC.Trading.Platform.slnx -m:1` - Use only if concurrent test-host execution causes the repository's documented contention failure.

Success criteria:

* Solution build and test stages pass.
* Quality-gate evidence is retained in the generated artifact directory.

Dependencies:

* Step 4.1 completion.

### Step 4.3: Fix minor issues and report blockers

Apply only isolated corrections for compile errors, test assertion mistakes, documentation diagnostics, or whitespace issues introduced by this work, then rerun the failed scope. Do not broaden into startup deduplication, schema changes, transition-engine redesign, or unrelated failing tests. Record any issue requiring broader changes as follow-on planning work.

Success criteria:

* All task-introduced issues are corrected and revalidated.
* Unrelated or architecture-expanding failures are reported with the failing command and affected file rather than fixed opportunistically.

## Dependencies

* .NET SDK baseline `10.0.200` requested by `global.json` with `latestFeature` roll-forward.
* Existing xUnit Application and Infrastructure unit-test projects.
* Existing EF Core in-memory test fixture and deterministic `TestTimeProvider`.
* Existing `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1` script.

## Success Criteria

* Startup reconciliation no longer throws when durable state is already `OutOfSchedule` and the schedule-derived reason changes.
* Continued inactivity refreshes `BlockedReason` and `LastValidatedAtUtc` without changing transition time or executing entry side effects.
* The transition engine continues rejecting `OutOfSchedule -> OutOfSchedule` and leaves state untouched on rejection.
* Focused workflow and policy tests cover changed-reason and unchanged-reason reconciliation.
* Wiki documentation reflects implemented runtime, ownership, operator, and test behavior.
* Focused tests, both affected test projects, and the repository quality gate pass.