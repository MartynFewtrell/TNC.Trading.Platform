<!-- markdownlint-disable-file -->
# Release Changes: Account Preferences State Verification

**Related Plan**: [account-preferences-state-verification-plan.instructions.md](../../plans/2026-08-31/account-preferences-state-verification-plan.instructions.md)
**Implementation Date**: 2026-08-31

## Summary

Implementation is substantially complete. This log records completed phases, remaining gaps, and validation results.

## Changes

### Added

* Account-preference durable state contracts, EF entities, migration, SQL lease, reconciliation/remediation operations, API routes, provider double, and closed-box test scaffolding.

### Modified

* Account-preference Application, Infrastructure, API, Web, AppHost, test fixtures, and wiki documentation to separate durable desired intent from account-bound IG observations.
* [ControllableIgProvider.cs](../../../../test/Shared/AccountPreferences/ControllableIgProvider.cs) to use the supported WireMock response API.
* [20260831130930_AddAccountPreferencesCurrentState.cs](../../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs) and generated SQL were inspected; no observation backfill is present.

### Removed

## Additional or Deviating Changes

* Phase 2 still lacks the requested audit-insert command-interceptor rollback test.
* Phase 4 lacks dedicated reconciliation/retry/remediation policy unit tests beyond the passing focused Application suite.
* Phase 6 closed-box execution and aggregate Playwright validation remain incomplete because the new distributed tests were not discovered by the available focused runner.
* Known dependency vulnerability warnings and preview SDK warnings remain outside this task's scope.

## Release Summary

Phases 1, 3, 4, 5, and 7 completed; Phase 2 completed functionally with one missing failure-injection test; Phase 6 has core background/configuration and test scaffolding with incomplete executable closed-box coverage; Phase 8 completed isolated repair and migration safety inspection but cannot claim a clean full-suite result. Focused validation included Application 12/12, API 17/17, Web 26/26, Infrastructure provider 10/10, and SQL integration 14/14 passes at the reported checkpoints.
