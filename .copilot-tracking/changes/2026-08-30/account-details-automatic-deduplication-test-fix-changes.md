<!-- markdownlint-disable-file -->
# Release Changes: Account Details Automatic Deduplication Test Fix

**Related Plan**: account-details-automatic-deduplication-test-fix-plan.instructions.md
**Implementation Date**: 2026-08-30

## Summary

Adjusted the Account Details unit-test fixture to use a single fixed clock, aligned the fake store to production automatic-only deduplication semantics, and verified the focused regression plus the full Application unit-test project remain green without changing runtime code.

## Changes

### Added

* None.

### Modified

* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs - Replaced mixed wall-clock setup with a fixed `TimeProvider`, derived fixture `UtcNow` and `TradingDay` from the same instant, added an optional trigger parameter to `Snapshot`, and updated the in-memory `ExistsForTradingDayAsync` predicate to match the production `Automatic` trigger requirement.

### Removed

* None.

## Additional or Deviating Changes

* No production or documentation files changed. This fix remains intentionally scoped to the failing unit-test file to avoid altering runtime behavior or operator-facing docs.
  * Reason: The root cause is confined to the test fixture and the fake store predicate, and the selected research explicitly preserved the existing runtime contract.

## Release Summary

The fix affects one file: [test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs](test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs). It replaces the calendar-dependent `TimeProvider.System` usage in the fixture with a nested fixed `TimeProvider`, keeps `TradingDay` and snapshot retrieval timestamps aligned to that same instant, and makes the in-memory deduplication check require `AccountDetailsTriggerSource.Automatic` to mirror the EF store implementation. Validation evidence shows the specific failing regression and the full Application unit-test project both pass.
