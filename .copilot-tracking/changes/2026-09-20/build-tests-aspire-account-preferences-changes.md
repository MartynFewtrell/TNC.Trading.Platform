<!-- markdownlint-disable-file -->
# Release Changes: Build, Tests, Aspire Upgrade, and Account Preferences

**Related Plan**: build-tests-aspire-account-preferences-plan.instructions.md
**Implementation Date**: 2026-09-20

## Summary

Implementation tracking for the build, test, Aspire package, and account-preferences layout plan.

## Changes

### Added

* `artifacts/build-baseline.binlog` - clean serial baseline evidence.

### Modified

* API mismatch projection, hosted authentication timing, account-preferences layout, coordinated Aspire packages, Testcontainers, AngleSharp, bUnit compatibility, and focused test contracts were updated per the plan.

### Removed

## Additional or Deviating Changes

* The clean build has no vulnerability warnings after package remediation. Web unit tests pass 138/138 and infrastructure integration tests pass 30/30.
* The focused AccountPreferences E2E class remains at 9 passed and 3 failed after reaching the page. The mismatch functional test passes. Remaining failures are tracked as WI-03 in the planning log.

## Release Summary

Phases 1, 2, and 4 are complete, and the implementation portions of Phase 3 are complete. The serial solution build passes with zero warnings and zero errors. The Docker-backed AppHost reaches healthy Keycloak, SQL Server, API, and Web resources with clean shutdown. Full solution test success and three focused AccountPreferences browser assertions remain outstanding.
