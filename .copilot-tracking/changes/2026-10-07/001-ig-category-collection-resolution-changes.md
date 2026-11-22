<!-- markdownlint-disable-file -->
# Release Changes: IG Category Collection Resolution

**Related Plan**: `.copilot-tracking/research/2026-10-07/plans/001-ig-category-collection-resolution-plan.md`
**Implementation Date**: 2026-10-07

## Summary

Milestone A (P1, P2, P3, P4, P7A) is implemented and focused validation passes. Existing uncommitted worktree changes were preserved; no changes were reverted, stashed, or committed. No IG calls or live broker probes were made.

## Changes

### Added

- Exact production-gateway regression for the reported 25→14 then size-14→7 response sequence with unchanged advertised total; it remains incomplete, stops before later pages, and cannot publish.
- Bounded fixed-25 diagnostic traversal with per-page count/shape evidence, allow-listed discrepancy codes, 100-page/2,500-row ceilings, and a bounded HTTP request ceiling. Diagnostic results contain no instrument identities or provider payloads and do not produce a production collection.
- Operator-only diagnostic API route at `POST /api/platform/market-categories/{categoryCode}/collection-diagnostic`. The application flow checks supported applied environment, active schedule, saved category, exclusive shared full-run admission, and sufficient shared request allowance before gateway traversal.
- Application tests proving admission conflict and insufficient shared allowance prevent gateway calls, and a diagnostic response does not invoke snapshot publication.
- Diagnostic admission claims no scheduled slot coverage, so it cannot suppress a regular collection slot.
- Web tests for refreshed-versus-stale coverage, missing coverage, failed status refresh, and overlapping status refreshes; API tests assert the diagnostic endpoint's Operator policy.

### Modified

- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs` and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketCategoryInstrumentsGatewayTests.cs`: preserve complete-only production validation and add bounded diagnostic evidence, exact incident regression, candidate-shape and redaction assertions.
- `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/` and `src/TNC.Trading.Platform.Application/Features/MarketDataRuns/MarketDataFullRunTrigger.cs`: add nonpublishing diagnostic contracts and authorized use-case admission/budget checks.
- `src/TNC.Trading.Platform.Api/Features/Platform/`: add bounded Operator route, validation, sanitized response mapping, and API policy coverage.
- `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategoriesPagePresenter.cs` and `MarketCategories.razor`: serialize status refreshes, ignore stale refresh generations, and use current status coverage or show it unavailable instead of reusing old catalogue coverage.
- `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinatorTests.cs`, `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/MarketCategoryInstrumentEndpointTests.cs`, and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MarketCategoriesTests.cs`: focused admission, authorization, nonpublication, and coherent-coverage coverage.
- `docs/wiki/api-reference.md`, `docs/wiki/operator-guide.md`, `docs/wiki/runtime-behavior.md`, and `docs/wiki/testing-and-quality.md`: document explicit invocation, guards, safe evidence, nonpublication, coverage behavior, and focused tests.

### Removed

- None.

## Additional or Deviating Changes

* Companion implementation-details and conventional plan artifacts were not supplied; the user confirmed the attached plan and its linked research are the complete implementation specification.
* The starting worktree already contains uncommitted changes across the plan's gateway, coordinator, API, persistence, UI, tests, and wiki scope. Those changes are treated as the user-approved baseline and will be preserved.
* P5 remains blocked pending separately authorized, sanitized provider evidence and an authoritative IG completion rule. P6 production acceptance changes and P7B contract-backed operational verification are deferred; no provider semantics are inferred.

## Release Summary

### Verified steps

- P0: baseline and applicable instructions confirmed.
- P1: exact incident and complete-only regressions pass.
- P2: bounded sanitized diagnostic traversal, ceilings, count-only response shape, and nonpublication coverage pass.
- P3: Operator route policy, shared admission conflict, and insufficient-budget refusals pass without gateway calls.
- P4: refreshed coverage, missing/failed coverage, and serialized overlapping status refresh tests pass.
- P7A: affected wiki pages and focused validation are complete.

### Focused validation

All commands used the repository-pinned .NET SDK 10.0.302 with VSTest/xUnit v2 and `--no-restore`:

```powershell
dotnet test "test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\TNC.Trading.Platform.Infrastructure.UnitTests.csproj" --no-restore --verbosity quiet
# Passed: 324, Failed: 0

dotnet test "test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\TNC.Trading.Platform.Application.UnitTests.csproj" --no-restore --verbosity quiet
# Passed: 268, Failed: 0

dotnet test "test\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.UnitTests\TNC.Trading.Platform.Api.UnitTests.csproj" --no-restore --verbosity quiet
# Passed: 96, Failed: 0

dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" --no-restore --verbosity quiet
# Passed: 197, Failed: 0
```

### Remaining gates

- P5: blocked; no explicit operational authorization or authoritative provider completion semantics were available.
- P6 and P7B: deferred. Production collection acceptance rules remain unchanged and no live broker verification was attempted.
