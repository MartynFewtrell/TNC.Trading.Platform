<!-- markdownlint-disable-file -->
# Release Changes: Account Preferences Page Issues

**Related Plan**: account-preferences-page-issues-plan.instructions.md
**Implementation Date**: 2026-09-19

## Summary

Implementation tracking for the Account Preferences account-mismatch contract, typed Save feedback, timestamp rendering, and responsive control row.

## Changes

### Added

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesAccountMismatch.cs` - Shared safe account-mismatch contract.
* `src/TNC.Trading.Platform.Web/PlatformApiException.cs` - Typed Web API failure metadata.

### Modified

* Account Preferences Application, Infrastructure, API, and focused tests - Typed account-mismatch handling, safe Problem Details mapping, and no-I/O/state-preservation coverage.
* Web API client, Account Preferences presenter, and Web unit tests - Preserved Problem Details metadata and exact mismatch-only Save guidance with generic fallbacks.
* Account Preferences Razor/CSS, bUnit tests, and Playwright tests - Semantic confirmation time and compact wrapping native control row.
* API reconciliation supervisor and AppHost configuration - Added a default-enabled reconciliation gate and disabled it only in the account-switch functional fixture for deterministic safety coverage.

### Removed

## Additional or Deviating Changes

* Phase 1 focused validation found one unrelated existing API serialization assertion failure: `ToResponse_ShouldSerializeDesiredTrailingStopsValue_WhenStateIsProjected` expects a property not present in current output.
* Phase 3 E2E validation had two existing shared authenticated provider/history tests fail; the four new Phase 3 browser tests passed.
* Phase 4 account-switch Check and Save coverage now pass after adding a fixture-scoped reconciliation gate; production behavior remains enabled by default.

## Release Summary

All five implementation phases are complete. The solution builds and the focused account-preferences functional suite passes.

Validation summary: Application 24 passed, Infrastructure 16 passed, Web unit 46 passed, account-preferences functional tests passed, and the solution build succeeded. One existing API serialization assertion and two existing E2E instability failures remain outside this change.
