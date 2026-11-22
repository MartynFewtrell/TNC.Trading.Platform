<!-- markdownlint-disable-file -->
# Release Changes: Resolve Out-of-Schedule Authentication Transition Failure

**Related Plan**: [out-of-schedule-authentication-transition-plan.instructions.md](../../plans/2026-08-31/out-of-schedule-authentication-transition-plan.instructions.md)
**Implementation Date**: 2026-08-31

## Summary

Phases 1 through 3 are complete. Continued out-of-schedule reconciliation now refreshes persisted metadata without requesting a self-transition, with focused regression coverage and updated wiki guidance.

## Changes

### Added

* None.

### Modified

* [src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs](../../../src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs) - Reuses the captured reconciliation timestamp and refreshes continued `OutOfSchedule` metadata without transition side effects.
* [test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs](../../../test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs) - Adds changed-reason and unchanged-reason persisted reconciliation regressions.
* [test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs](../../../test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs) - Locks strict self-transition rejection and non-mutation.
* [docs/wiki/runtime-behavior.md](../../../docs/wiki/runtime-behavior.md) - Documents continued-inactivity timestamps and entry-only side effects.
* [docs/wiki/architecture.md](../../../docs/wiki/architecture.md) - Documents reconciler and transition-engine ownership.
* [docs/wiki/operator-guide.md](../../../docs/wiki/operator-guide.md) - Documents normal reason rollover and fail-fast boundaries.
* [docs/wiki/testing-and-quality.md](../../../docs/wiki/testing-and-quality.md) - Records the focused regression coverage.

### Removed

* None.

## Additional or Deviating Changes

* Phase 1 focused validation passed: the existing `Active -> OutOfSchedule` workflow test succeeded.
* Phase 2 focused validation passed: 25 Infrastructure authentication retry tests and 20 Application transition-engine tests succeeded.
* Phase 3 documentation validation passed: diagnostics, heading/link inspection, and `git diff --check` succeeded for all four wiki pages.
  * Existing repository warnings were reported by the phase agent; no task-introduced warning was identified.

## Release Summary

All four implementation phases are complete. The focused suites, complete affected unit-test projects, and Clean Architecture quality gate pass.

Validation evidence:

* Focused suites: 45 tests passed.
* Complete Application unit tests: 129 passed.
* Complete Infrastructure unit tests: 114 passed.
* Clean Architecture quality gate: 519 passed, 0 failed, 0 skipped.
* Quality-gate artifacts: [artifacts/quality-gates/out-of-schedule-reconciliation/20260831-103434](../../../artifacts/quality-gates/out-of-schedule-reconciliation/20260831-103434)

The quality gate required a process-scoped PowerShell execution-policy bypass because the unsigned repository script could not run under the machine's default policy. The gate itself completed successfully. Existing preview-SDK and package-vulnerability warnings remain outside this task's scope.
