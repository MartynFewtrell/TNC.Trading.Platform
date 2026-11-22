<!-- markdownlint-disable-file -->
# Release Changes: Testing Improvement

**Related Plan**: testing-improvement-plan.instructions.md
**Implementation Date**: 2026-09-15

## Summary

Implementation in progress. Test dependency compatibility and fixture-owned resource lifecycle changes are being applied phase by phase.

## Changes

### Added

* `Testcontainers.MsSql`-backed SQL lifecycle behavior in the Infrastructure integration fixture.
* `.github/workflows/ci.yml` - staged restore/build, fast test, Aspire integration, browser/Keycloak, and final summary jobs.

### Modified

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - removed the incompatible direct AngleSharp override while retaining bUnit.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj` - added the owned SQL test-container dependency.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` - replaced arbitrary Docker discovery with owned startup, readiness, isolation, diagnostics, and disposal.
* Testing instructions, repository lessons, and wiki guidance - documented named resources, owned lifecycle, randomized automation endpoints, and test-layer boundaries.

### Removed

* Phase 1 validation: Web unit suite passed 129 tests after restore; initial stale-assets failure was resolved by restoring packages.
* Phase 2 validation: affected API/Web/AppHost projects compile, AppHost composition tests pass 13/13, and legacy process/socket/fixed-port helpers are removed. Runtime auth proof remains blocked by Keycloak 405, synthetic 401, and Web sign-in readiness timeout.
* Phase 2 follow-up validation: AppHost composition 17/17, synthetic API auth 11/11, and real API auth 15/15 pass; Web redirect mismatch was corrected, but the focused functional host did not produce a final summary and remains unverified.
* Phase 3 validation: Infrastructure integration suite passed 29 tests on the repeat run; the fixture created and removed its own SQL container.
* Phase 4 validation: solution build and diff checks passed; frontmatter and workflow linters were unavailable, and the optional C# diagnostics collector was not added because no compatible existing abstraction was found.

## Additional or Deviating Changes

* None yet.

## Release Summary

Phases 1, 3, and 4 are complete. Phase 2 and Phase 5 remain partial because Web functional and full solution test processes hang during AppHost lifecycle cleanup. The solution build passes; a focused follow-up is required before the full success criteria can be claimed.
