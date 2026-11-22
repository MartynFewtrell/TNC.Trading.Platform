<!-- markdownlint-disable-file -->
# Release Changes: Failed Build Remediation

**Related Plan**: failed-build-remediation-plan.instructions.md
**Implementation Date**: 2026-09-14

## Summary

Remediated the stale Infrastructure SQL integration-test contract call. Package, SDK, and full compatibility validation remain in progress.

## Changes

### Added

* None.

### Modified

* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs - supplied the authentication snapshot ID and past authenticated and due timestamps required by the seven-argument state-store contract.

### Removed

* None.

## Additional or Deviating Changes

* None.

## Release Summary

Phase 1 is complete. The owning integration-test project builds successfully, with the documented preview SDK and package advisory warnings still pending later phases.

Phase 2 is complete. Verified package families and four required direct dependency alignments were applied. Restore succeeds and the solution vulnerability graph reports zero findings; 96 Humanizer `NU1608` warnings remain for the clean-build gate.

Phase 3 is complete. `global.json` now pins stable SDK `10.0.302` with `latestPatch` roll-forward. `dotnet --version` reports `10.0.302`, and the preview SDK diagnostic is eliminated.

Phase 4 is blocked. The ordinary solution build succeeds, but `dotnet build ... -warnaserror` fails on 96 Humanizer `NU1608` dependency-constraint warnings in the Web functional and E2E projects. Focused and full test suites were not run because the strict build gate is incomplete.
