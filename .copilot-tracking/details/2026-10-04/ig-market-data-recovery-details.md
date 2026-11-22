<!-- markdownlint-disable-file -->
# Implementation Details: IG market-data recovery

## Context and execution boundary

Source R1: `.copilot-tracking\research\2026-10-04\ig-category-page-count-session-denial-research.md`.
Source R2: `.copilot-tracking\research\subagents\2026-10-04\ig-market-data-recovery-planning-research.md`.
Plan: `.copilot-tracking\plans\2026-10-04\ig-market-data-recovery-plan.instructions.md`.
Log: `.copilot-tracking\plans\logs\2026-10-04\ig-market-data-recovery-log.md`.

These are future implementation instructions. No runtime changes or database reset have been performed by creating this plan. Preserve unrelated worktree changes. Read applicable scoped instructions before editing product code, tests, SQL or documentation. Reconcile source-line anchors against current code instead of assuming a deployed fingerprint.

## Phase 1: Safe diagnosis and exact regressions

<!-- parallelizable: false -->

### Steps 1.1-1.2: Parse and propagate bounded evidence

Existing files:
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs` (lines 332-424): parse failed session/page responses; retain operation and add initial/reauthentication phase.
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketDetailsGateway.cs` (lines 550-591, 674-706): existing bounded safe-code prior art; extract common behavior without changing detail semantics.
* `src\TNC.Trading.Platform.Application\Features\MarketDetails\MarketDetailProviderErrorCodes.cs` (lines 5-25): allow-list reference, not a listing-to-details dependency.
* `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentFailure.cs` (lines 4-14): add typed optional safe code/parse state/phase.
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs` (lines 955-969) and `EfMarketCategoryInstrumentStatusReader.cs` (lines 77-90), same directory: stop discarding provider code.
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\Entities\InstrumentCollectionCategoryAttemptEntity.cs` (lines 12-20): integrate diagnostics and any required new columns/migration.
* `src\TNC.Trading.Platform.Web\Components\Pages\MarketCategories\MarketCategories.razor` (lines 105-181): present saved success, latest attempt and separate detail coverage honestly.

New shared parser candidate: `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgProviderErrorCodeParser.cs`. Keep application diagnostic types one per matching file. Follow current failure/status contract references to their API DTOs/mappers and update every consumer; resolve exact consumer files before edits rather than inventing endpoints.

Use a bounded byte limit and known-code allow-list. Represent unknown, missing, malformed, non-JSON, oversized and unsafe input as explicit parse outcomes without retaining arbitrary strings. Never expose tokens, credentials, headers, account identifiers, raw bodies or instrument payloads. Keep original/restarted request evidence separate, including ordinal, requested/returned page and size, totals, raw count and completeness-policy expectation.

### Step 1.3: Exact tests

Extend `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs` (R1 lines 487-491), existing details tests and `test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\MarketCategoriesTests.cs`.

Assert original size150/count70 then restarted size70/count41/total290 yields explicit page-zero expected70/actual41 under legacy policy, with no later page/publication. Assert initial session403 sends no page GET; page401 then session403 records reauthentication and bounded replay. Cover all parse outcomes and last-good success plus latest denied attempt. Label fabricated fixtures as fixtures, not live provider observations.

Dependencies: no new provider calls. Validation: focused adapter, mapping/persistence and UI tests. Success: all safe evidence surfaces agree and no behavior silently converts to success. Discrepancy references: DR-01, DR-02.

## Phase 2: Wired conservative profile and lifecycle safety

<!-- parallelizable: false -->

### Step 2.1: Persist and wire the collection profile

Extend `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\Entities\InstrumentCollectionSettingsEntity.cs` (lines 3-12) and its existing settings read/update/contracts/API/UI path. Add SQL-backed validated page-size, request-spacing and category-spacing values, with the recovery profile 25/5 seconds/60 seconds. Generate EF migration in `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\Migrations\` and update `PlatformDbContext.cs` model/snapshot as generated. Reuse existing configuration-version/concurrency validation. Keep unrelated trading settings unchanged.

Application policy candidate: `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCollectionProfile.cs`. Resolve current settings consumers before extending them; validate page size 1-1000, positive bounded pacing and pause, interaction with limits and approved budget. Do not add inert appsettings or rely on the optional constructor default.

Modify `src\TNC.Trading.Platform.Infrastructure\Infrastructure\DependencyInjection\PlatformInfrastructureServiceCollectionExtensions.cs` (lines 142-167) to bind effective settings to collection execution. Persist profile provenance where required to distinguish generations.

### Steps 2.2-2.3: One retry owner and actual-send pacing

Selected path: remove standard resilience handlers for affected IG background discovery/listing/details clients using the existing account-preferences precedent at DI lines 163-167. Do not change the global defaults in `src\TNC.Trading.Platform.ServiceDefaults\Extensions.cs` (lines 42-48), trading/order clients or authentication middleware. Retain explicit request/total deadlines, bounded outer retries and bounded401 recovery. Test the installed DI pipeline to verify there are no hidden replays.

Update listing gateway (lines 341-365, 394-415, 437-456), other affected non-trading adapters, and `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgProviderRequestThrottle.cs` (lines 66-232). Optional new helper: `IgCollectionRequestPacer.cs` in the same IG directory. Every real session/page/detail/retry send must pass scoped pacing, SQL reservation and daily budget exactly once. Use monotonic/fake-time-friendly timing; establish shared-scope five-second admission across affected instances without replacing account30/application60 per-minute caps. Revalidate send time after waiting so delayed reservations cannot produce bunched sends.

Preserve unrelated callers' shared quota enforcement; do not globally sleep trading requests. A category pause is only an additional cushion. It does not replenish daily, historical or provider-side allowance.

### Steps 2.4-2.5: Lease-safe waits and deterministic validation

Modify:
* `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs` (lines 30-31, 435-478, 765-793)
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentRequestBudget.cs` (lines 21-35, 84-108)
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDataFullRunLeaseGuard.cs` (lines 61-75)
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDataFullRunStore.cs` (lines 347-379)
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs` (lines 524-552)
* `src\TNC.Trading.Platform.Application\Features\MarketDetails\MarketDetailCollectionCoordinator.cs` (lines 235-281), `MarketDetailCapacityPolicy.cs` (lines 34-59), and detail request-budget/store counterparts

Use cancellation-aware renewal/checkpoints comfortably below the shortest lease duration throughout long network calls, quota waits, retry delays and category pauses; validate fence and frozen provenance before sends/publication. Stop on lease loss; stale owners cannot publish.

Selected schedule policy: admit new work only inside the eligible window; already-admitted full-run work may continue past schedule close only while parent/child ownership, frozen provenance and daily budget remain valid. Standalone work still expires at its window. Align actual gateway throttle/response checks with coordinator semantics; this is an intentional documented behavior alignment, not unlimited continuation.

Tests must measure minimum five-second actual starts including login/retry, a 60-second gap after category completion before the next category, no trailing pause after the last category, multi-instance shared caps, parent/cycle/detail renewals past five minutes, cancellation, stolen fence, close-of-window and exhausted budget. Use production DI handlers as well as fake-time unit tests. Update detail capacity estimates for login/relogin and paced sends.

Dependencies: phase1 contracts; final schema agreement. Success: no hidden retry/budget bypass or timeout caused by intentional waits. Discrepancy references: DD-01, DD-02.

## Phase 3: Bounded diagnostic traversal and provider-contract gate

<!-- parallelizable: false -->

### Steps 3.1-3.3: Establish the contract without publication

Change `IgMarketCategoryInstrumentsGateway.cs` (lines 11-23, 125-155, 164-272, 491-496, 646-660). Diagnostic mode must hold size25 fixed, disable the smaller-page restart, visit all bounded advertised pages even if a page is short, and be structurally unable to call production snapshot publication. Preserve validation of returned page identity/size, valid fields, duplicate identities, stable metadata and finite bounds. A diagnostic summary is not a `Complete` production result.

Keep the existing100-page/15,000-result ceilings. At size25, reject a conventional2,501-row collection requiring101 pages before exceeding the bound. Do not silently increase to600 pages or spend an unapproved budget. Tests cover2,500/2,501 explicitly.

After phase2 tests pass, one separately approved Demo diagnostic may collect sanitized metadata and count-only summaries. Preserve prior snapshots; record approved remaining budget before starting. Ask IG through approved channels about visible versus broad totals, filtering order, sparse pages, snapshot stability and authoritative exhaustion. Never transmit repository code, credentials, raw instruments or account identifiers.

### Steps 3.4-3.5: Conditional production policy

Select exactly the branch established by evidence:
* Exact full-page contract: keep strict validation and classify violating responses as provider inconsistency.
* Valid sparse pages with exact visible total and complete advertised sequence: relax only unsupported per-page/derived-total-pages assumptions, retaining exact final unique count.
* Broad totals with a confirmed authoritative visible exhaustion rule: implement that rule and document what completeness means.
* No authoritative rule: keep incomplete/unknown; do not publish partial data or continue beyond bounds until a guessed target is reached.

Tests cover total290 as eleven25-row pages plus one15-row page, twelve page GETs at unchanged size25, short intermediate pages, short final union, repeated/wrong pages, drift, invalid rows, duplicates/cross-page overlap and nonpublication on failure. Keep login/retry counts separate from row counts.

Dependencies: phases1-2; provider evidence only for relaxing production completeness. Success: diagnostic mode is useful even when evidence is unavailable, but never masquerades as a successful snapshot. Discrepancy reference: DR-01.

## Phase 4: Classified denial recovery and fresh detail generations

<!-- parallelizable: false -->

### Step 4.1: Code-specific recovery

Update `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentRetryPolicy.cs` (lines 3-39), coordinator failure mapping (lines 870-883), safe provider classification and shared cooldown enforcement. Known allowance codes get bounded scope-appropriate cooldown shared across relevant callers; historical/daily exhaustion is not a one-minute reset. Known key/product/account denials are nonretryable configuration actions. Unknown403 remains nonretryable with parse state. Do not replay order/trading requests.

Add tests for initial/relogin denied phases, no blind2-second retry, allowed later recovery, preserved saved success and surfaced reason. Actual denial of the incident remains unproven until a new safe response is captured.

### Steps 4.2-4.3: Same-slot generation, if reproduced

Reproduce with existing SQL tests before changing:
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailRunStore.cs` (lines 119-178)
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\PlatformDbContext.cs` (lines 510-525)
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailReadStore.cs` (lines 346-405)
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailListingSourceReader.cs` (lines 44-130)
* `src\TNC.Trading.Platform.Application\Features\MarketDetails\MarketDetailCollectionCoordinator.cs` (lines 69-73, 120-146)

If reproduced, introduce explicit update-generation identity in application contracts/entity/migration/unique index/acquisition and all dependent budget/lease/source/read lookups. Historical generation is immutable. Unchanged provenance resumes the correct generation; a genuine new update creates a distinct generation; provenance drift still fences/supersedes old work. Test concurrency, migration/backfill uniqueness, historical coverage readers and new complete-source eligibility. Do not relabel an old `Superseded` row as fresh or remove provenance checks.

Dependencies: phases1-3 policy; reproducible SQL evidence for generation changes. Success: recovery does not conflate saved listing success, latest attempt and detail coverage. Discrepancy reference: DR-03.

## Phase 5: Guarded reset tooling and isolated SQL tests

<!-- parallelizable: true -->

### Steps 5.1-5.2: New scoped reset asset

Create and retain reusable `infra\scripts\reset-ig-demo-market-data-recovery.sql`. Include parameter descriptions and dry-run/apply invocation examples in its header and operator documentation so future resets require no file edits or copied ad hoc SQL. Preserve the distinct failed-only contract of `infra\scripts\reset-instrument-collection-slot.sql` (lines 19-45, 79-115, 174-239). If reusing its logic, correct the tightly coupled diagnostic-clearing omission without broadening that script's success-refusal semantics.

New script inputs: expected local server/database, exact applied IG Demo environment GUID/profile, intended day/slot/recovery scope, reset mode, expected row counts and explicit apply confirmation. Default is dry-run with sanitized scope/preservation counts and reasons for refusal. Never hardcode the old script's sample date. No database drop, unfiltered truncate or automatic worker restart.

Default mode is minimal failed-slot rearm: retain last-good snapshots, history, interests and budgets; reset selected attempt counters/all diagnostics, cycle outcome/prerequisite, expired ownership and blocking slot coverage. Clear only compatible stale intents/follow-up markers required for a new Categories-first full run. If this cannot enable new work, refuse with blocker rather than claim success.

Explicit broader mode uses an audited FK closure for conflicting run/day/slot artifacts. Retain snapshots unless their removal is necessary and explicitly selected. EPIC/current records may be shared across categories. Inspect all dependents even outside the nominal slot; never delete them merely because an EPIC/category matches.

Use `SET XACT_ABORT ON`, TRY/CATCH rollback, protected reads and full-run/cycle/detail/budget application locks in repository order. Recheck schema/FKs, applied scope, expired leases, unchanged preflight counts and preserved allowance inside the transaction. Abort on ambiguity, concurrent change, unsupported schema or active owner.

R2 (lines 98-117) and `PlatformDbContext.cs` establish child-before-parent closure:
1. Remove selected `MarketDetailCurrent` pointers only for observations selected for removal; remove dependent `MarketDetailObservations`/`MarketDetailRunMemberships`.
2. Remove selected `MarketDetailRunTargets`/`MarketDetailRunSources`, then `MarketDetailCollectionRuns`.
3. Remove selected `MarketCategoryInstrumentObservations` before `MarketCategoryInstrumentCollectionRuns`; only if explicitly clearing current listing data, `MarketCategoryInstruments` before `MarketCategoryInstrumentCatalogStates`.
4. Remove selected `MarketDataFullRunSlotCoverages`/`MarketDataFullRunCategories`/`MarketDataFullRunStages`/`MarketDataFullRunItems` before selected `MarketDataFullRuns`; audit independent intents/follow-up triggers.
5. Retain cycle consumption ledgers, fences and earlier cycles; rearm only the intended current eligible cycle and clear all selected attempt diagnostics.

Preserve broker environments/selections/configuration, protected credentials/Data Protection keys, auth/account/audit data, `InstrumentCollectionSettings`, `MarketCategoryInterestStates`/`MarketCategoryInterests` and revisions, category catalogue by default, unrelated environments/history, `UsedRequestBudget`, `IgProviderRateReservations` and provider-confirmed `MarketDetailEligibility` exclusions. A reset does not grant IG quota or repair provider denial.

### Step 5.3: Test the real SQL asset

Create `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\IgDemoMarketDataRecoveryResetSqlIntegrationTests.cs`. Use fixture-owned SQL and actual script execution, not EF in-memory. Extend existing full-run/category/detail integration tests where needed for admission verification.

Seed successful/failed/superseded generations, restrictive FKs, shared EPICs, used daily allowance, provider reservations/exclusions, credentials/config/interests/revisions and unrelated Live/second-Demo rows. Assert dry-run is read-only, ambiguous/nonlocal/Live targets refuse, active lease/concurrent count change refuses, failure rolls back completely, rerun is idempotent, all diagnostic fields clear, preservation hashes/counts match and no fictitious budget appears. Test admission on fixtures yields a NEW Categories-first full run, not `Resumed`, `AlreadyRunning`, `AlreadyCovered` or details-only follow-up.

Dependencies: can author independently alongside phases1-4; final schema-dependent tests run sequentially after migrations stabilize. Success: the script proves requested recovery without expanding scope silently. Discrepancy reference: DD-03.

## Phase 6: Documentation and full pre-reset validation

<!-- parallelizable: false -->

### Step 6.1: Update implemented documentation

Update existing `docs\wiki\runtime-behavior.md` (lines 182-197, 249-265), `docs\wiki\operator-guide.md` (lines 126-133), `docs\wiki\local-development.md` and `docs\wiki\testing-and-quality.md`. Describe effective configuration, actual-attempt retry ownership, schedule/lease behavior, count semantics, safe denial parsing, reset dry-run/apply/preservation/backup and a separate controlled next request. Do not assert experimental settings cured the incident.

### Steps 6.2-6.4: Validation gate before final mutation

Load the run-tests skill when executing .NET validation and confirm the project's runner syntax. Use existing repository tasks/scripts where they cover the work. R2 (lines 135-149) verifies these Windows commands/projects:

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgMarketCategoryInstrumentsGatewayTests|FullyQualifiedName~IgMarketDetailsGatewayTests"
dotnet test test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~MarketCategoryInstrumentCycleCoordinatorTests|FullyQualifiedName~MarketDetailCollectionCoordinatorTests|FullyQualifiedName~MarketDetailCapacityPolicyTests"
dotnet test test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~MarketCategoryInstrumentSqlIntegrationTests|FullyQualifiedName~MarketDetailSqlIntegrationTests|FullyQualifiedName~MarketDataFullRunSqlIntegrationTests|FullyQualifiedName~IgProviderRequestThrottleSqlIntegrationTests|FullyQualifiedName~IgDemoMarketDataRecoveryResetSqlIntegrationTests"
dotnet test TNC.Trading.Platform.slnx -m:1
```

Add changed settings/API/Web test selectors to the relevant existing runner invocation. Validate real production DI retry behavior, schema migrations/backfill, cross-generation queries, redaction and both reset modes. Use isolated output if running resources lock binaries; follow repository lifecycle guidance rather than terminating unrelated processes. Keep testing offline/provider-stubbed except the separately approved diagnostic.

Apply minor fixes, rerun affected checks, and record blockers requiring additional planning. No developer database reset until implementation, migrations, documentation and validation pass. An unresolved provider contract leaves fail-closed behavior in place; it does not justify failing offline tests.

Dependencies: phases1-5 complete. Success: build/test evidence and wiki changes are persistent before final reset.

## Phase 7: Reset local Demo database data and prove fresh-request eligibility

<!-- parallelizable: false -->

### Steps 7.1-7.3: Final guarded execution

This is the LAST stage and contains the requested database mutation. The user's 13:57 clarification requires running the saved reusable script once on the intended local database, not only authoring/testing it. Perform no new feature work or automatic live collection after it.

1. Identify local SQL server/database, applied environment GUID/profile, migrated schema, intended day/slot and exact recovery mode. Reject Live/nonlocal/ambiguous targets. Run dry-run and record nonsecret counts, dependent closure and preserved usage.
2. Stop all API/collector/other writers sharing the Demo/provider scope with Aspire lifecycle guidance from https://aspire.dev/. Keep SQL/Keycloak infrastructure running where supported. Verify no in-flight provider calls. Wait for leases to expire or use validated fenced cancellation; never null a live owner's lease.
3. Take and verify a recoverable backup/export before mutation. Keep it access-controlled outside source control; do not expose protected secret contents. Preserve sanitized baseline IDs/attempt timestamps and reset-scope diagnostics.
4. Execute the exact saved `infra\scripts\reset-ig-demo-market-data-recovery.sql` once in apply mode using the verified parameters; record its SHA-256 before execution. The script rechecks preconditions inside the tested transaction and applies minimal rearm. Use confirmed broader FK closure only if required; preserve unrelated data/history and consumed budgets. Do not replace execution with pasted SQL or repeat mutation solely to prove idempotence (that belongs in fixture tests).
5. Commit only when preservation/integrity/admission assertions pass. Otherwise roll back and leave writers stopped with a specific failure; do not manufacture a success-shaped receipt.

### Steps 7.4-7.5: Read-only postconditions and handoff

Record each check in a sanitized reset receipt, including saved script path/SHA-256, execution time, nonsecret target/parameters, exactly one successful apply invocation and committed transaction result. Dry-run/fixture results alone are not proof of the requested local reset:
* Correct applied IG Demo profile and market-data capability; unchanged selected interests and configuration/revision values.
* No live prior full-run/cycle/detail lease, resumable blocker, covered target slot or incompatible stale intent/follow-up.
* Intended cycle/prerequisite pending, selected attempts zero, every stale failure/code/status/operation/page/count/phase/parse diagnostic cleared; daily consumption unchanged.
* A future admission can start a NEW full run at Categories; new listings must precede detail sources. Existing generations are not relabeled fresh.
* Remaining approved allowance is sufficient for listing/session/relogin/retries and intended paced detail batches with bounded detail passes. Preserve genuine reservations and exclusions; wait naturally for minute capacity.
* Schedule/day/timezone/window/frequency are valid. On a closed day/window, name the next eligible slot; do not alter schedules or zero usage to claim immediate eligibility.
* FK integrity and preserved-table counts/nonsecret hashes match preflight; all changed rows lie inside the approved closure.
* Before/after changed-row counts, preservation counts/nonsecret hashes, reset timestamp, baseline run/collection IDs and next eligible slot are recorded with worker-stopped status; the tested script remains saved in `infra\scripts` for reuse.

The local reset only removes local blockers. Provider access and completeness remain evidence-gated. If allowance/schedule/access prevents a fresh request, report the real blocker and do not mark final execution ready until it is resolved or a genuine eligible future slot is identified.

Leave workers stopped. A later separately authorized request must demonstrate new IDs/timestamps, measured25/5s/60s behavior, complete-only publication and fresh detail provenance. That request is a handoff, NOT an additional plan stage or a claim made by the reset.

Dependencies: phase6 passing, exact safe target, verified backup and quiescent writers. Success: exact saved reusable script executed once in apply mode on the intended local database, committed reset and before/after evidence recorded, preservation proven, and fresh-request prerequisites verified. Do not claim execution if blocked or rollback occurs. Traces to UR-02 and 13:57 clarification; references R2 lines 70-133.
