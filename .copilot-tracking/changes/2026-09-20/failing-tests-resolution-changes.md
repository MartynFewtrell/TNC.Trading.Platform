<!-- markdownlint-disable-file -->
# Release Changes: Failing Test Resolution

**Related Plan**: failing-tests-resolution-plan.instructions.md
**Implementation Date**: 2026-09-20

## Summary

Implementation tracking for the Account Preferences failing-test resolution plan.

## Changes

### Added

### Modified

* Phase 3 validation evidence was recorded without additional product-code changes.
* Full Web unit validation passed: 139 passed, 0 failed.
* Full Web functional validation passed: 13 passed, 0 failed.
* Full Web E2E validation retained `artifacts/test-results/failing-tests-resolution/failing-tests-resolution-final.trx` with 13 tests: 10 passed and 3 failed.

### Validation Failures

* `AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation` failed in the E2E request-observation boundary: the helper expected `GET /gateway/deal/accounts/preferences`, but the provider recorded only `POST /gateway/deal/session` requests. The warning assertion was not reached.
* `AccountPreferences_ShouldExposeAccessibleHistoryControls_WhenHistoryTabIsActive` failed because the selected Observed history tab remained `aria-selected="false"` after click.
* `AccountPreferences_ShouldDisableLoadOlderObservations_WhenNoNextCursorExists` failed with the same `aria-selected="false"` result after click.

The Check-status circuit failure is classified separately from the history-tab failures. No product correction was made because the retained evidence identifies a browser/test-operation boundary failure, not a confirmed presenter, client, or component propagation defect.

### Removed

## Additional or Deviating Changes

* The previously unverified third report now has named evidence in the retained TRX: it is the history-controls test `AccountPreferences_ShouldExposeAccessibleHistoryControls_WhenHistoryTabIsActive`. Its failure mechanism is separate from the Check-status circuit failure and requires separate investigation.

## Release Summary

Phase 3 completed with unit and functional suites passing. The E2E artifact is retained for follow-up; three browser failures remain classified without broadening the Account Preferences product change.

