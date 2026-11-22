<!-- markdownlint-disable-file -->
# Release Changes: IG market-data recovery

**Related Plan**: ig-market-data-recovery-plan.instructions.md
**Implementation Date**: 2026-10-04

## Summary

Safe diagnostic propagation, conservative runtime wiring, bounded diagnostic traversal, classified denial handling, same-slot detail generations, fixture-tested reset tooling and wiki documentation are implemented. Remaining work is separately approved provider evidence, full offline validation and the guarded local reset. Existing uncommitted work was present at task start and has been preserved.

## Changes

### Added

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgProviderErrorCodeParser.cs` and result/types - Parse bounded provider error bodies into explicit safe outcomes and allow-listed codes.
* `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentAuthenticationPhase.cs`, `MarketCategoryInstrumentPageEvidence.cs`, and `MarketCategoryProviderErrorCodeParseOutcome.cs` - Retain authentication phase and paired initial/restarted page evidence.
* `src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentPageEvidenceResponse.cs` and `src/TNC.Trading.Platform.Web/MarketCategoryPageEvidenceViewModel.cs` - Expose the safe page diagnostics.
* `infra/scripts/reset-ig-demo-market-data-recovery.sql` - Add a dry-run-first, count-pinned, locally scoped failed-slot reset script with locking and transactional preservation checks.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/IgDemoMarketDataRecoveryResetSqlIntegrationTests.cs` - Exercise the saved SQL asset against fixture-owned SQL Server, including admission and rollback.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgProviderErrorCodeParserTests.cs` - Cover bounded safe parsing outcomes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20261004133153_PersistIgCollectionExecutionProfile.cs` - Persist validated page-size and pacing defaults with SQL constraints.

### Modified

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketDetailsGateway.cs`, and market-category failure/persistence/API/UI mappings - Preserve safe provider and pagination diagnostics without weakening complete-only publication.
* Focused API, Application, Infrastructure and Web tests - Verify page-zero mismatch evidence, session/relogin distinctions, durable diagnostics and rendered status.
* Market-category settings and execution path across Application, API, Infrastructure and Web - Wire SQL-backed page size 25, request spacing 5 seconds and category pause 60 seconds; pace collection HTTP sends, keep retries gateway-owned, renew collection leases through waits, and align full-run continuation and detail capacity estimates.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgProviderRequestThrottle.cs` and affected HTTP client registrations - Enforce scoped pacing alongside distributed request reservations without changing global ServiceDefaults.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs` and its unit tests - Add fixed-size diagnostic traversal, 2,500-row effective capacity and strict fail-closed completeness behavior.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgProviderRequestThrottle.cs`, both market-data gateways and focused tests - Add bounded scope-appropriate cooldowns for allow-listed provider allowance codes; keep unknown 403 and configuration denials nonretryable.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20261004144350_AddMarketDetailUpdateGenerations.cs` and related EF entity/store/read/source paths - Add immutable detail update-generation identity so same-slot provenance changes admit new work without rewriting historical coverage.
* `docs/wiki/runtime-behavior.md`, `docs/wiki/operator-guide.md`, `docs/wiki/local-development.md`, and `docs/wiki/testing-and-quality.md` - Document the conservative request profile, safe diagnostics, diagnostic-only traversal bounds, reset retention contract, and fixture coverage.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/MarketDataFullRunSqlIntegrationTests.cs` - Supply the full-run store to the request-budget test fixture so lease renewal is exercised with its production dependency.

### Removed

## Additional or Deviating Changes

* Existing uncommitted IG market-data recovery edits were present before implementation began; their authorship is not attributed to this task.
* Phase 5's reusable script currently supports only failed-slot rearm; dependency-closed cleanup is not implemented and remains unmarked pending the real target's dry-run blockers.
* No provider diagnostic, local database mutation, worker restart, or local backup has been performed.

## Validation So Far

* Phase 1 focused tests: Infrastructure IG adapters/parser/details 120 passed; Application coordinator 14 passed; API endpoint 6 passed; Web 14 passed; SQL persistence/migration 2 passed.
* Phase 2 focused suites: Application 265 passed; Infrastructure 302 passed; API 96 passed; Web 192 passed; SQL request-throttle integration 3 passed.
* Phase 3 adapter tests: 89 passed; exact 290-row fixture traversed 12 fixed-size GET pages; the 2,501-row boundary and incomplete/unstable traversals remain nonpublishing. No provider diagnostic was performed.
* Phase 4 denial handling: Infrastructure build passed; Application test selectors 45 and 8 passed; Infrastructure unit selectors 144 and 20 passed; API 6 passed; Web 23 passed. The complete `MarketDataFullRunSqlIntegrationTests` selector passed 13/13 after the fixture supplied `fullRunStore`, including the stale-parent-lease regression.
* Phase 5 fixture-owned SQL Server tests: 5 passed; `git diff --check` passed.
* Phase 6: `dotnet build TNC.Trading.Platform.slnx --verbosity minimal` passed with 0 warnings and 0 errors. `dotnet test TNC.Trading.Platform.slnx -m:1 --verbosity minimal` passed all 1,094 tests across API integration/unit, AppHost unit, Application unit, Architecture integration, Infrastructure integration/unit, Web E2E/functional/unit projects; 0 failed and 0 skipped.
* Focused generation/lease verification: `MarketDataFullRunSqlIntegrationTests` passed 13/13 after wiring the request-budget fixture to its full-run store.
* Local preflight was read-only: the intended IG Demo environment is still the active applied profile. The target cycle is `Completed` with `Succeeded` category prerequisite and 13 recorded requests; two target-day full runs are `Completed`/`Partial`, with no published category run for the candidate slot. No live SQL request remains connected.
* The saved reset script was not run against the local database. A fresh connection check after the user's Docker-update report found SQL reachable, the profile migration applied, and the detail-generation migration still absent. The SQL server's registered name still differs from its machine name, which the script's local-target guard rejects. Read-only checks found no running full/detail runs or other executing DB requests at that instant. No backup was created and no migration or data mutation was attempted.

## Release Summary

Phases 1, 2, 4, and 6 are complete; phase 3 retains its separate live-provider approval gate; phase 5 retains the target-preflight decision; phase 7 is blocked by local SQL identity/schema gates. Four wiki pages were updated, the detail-generation migration and read/write path are covered by SQL integration tests, and the full-run SQL integration class passes 13/13. The full solution build and all 1,094 serialized tests pass. No local database mutation, backup, migration, or live provider request has been performed. Resume Phase 7 only after the SQL identity can be verified against the script guard, the remaining migration is safely applied, an access-controlled backup is verified, and writers/schedule/allowance are checked.
