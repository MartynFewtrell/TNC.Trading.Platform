<!-- markdownlint-disable-file -->
# IG market-data recovery planning supplement

Research date: 2026-10-04. Planning-only, read-only investigation except for this new document.

## Scope and conclusion

This supplements `.copilot-tracking\research\2026-10-04\ig-category-page-count-session-denial-research.md`; it does not replace the incident research or authorize execution. No database queries/mutations, provider requests, credential inspection, application lifecycle operations, tests, or product-code edits were performed.

The actionable plan should implement safe session diagnostics, an explicitly wired conservative collection profile, wire-attempt retry ownership, lease-safe waits, and contract-gated fixed-size traversal before its **last stage** resets local Demo collection state. The reset stage must finish with read-only eligibility evidence, not an automatic restart/provider call.

The primary document's final verification boundary remains binding: source/public-contract evidence is not a fresh provider reproduction or runtime fingerprint (`.copilot-tracking\research\2026-10-04\ig-category-page-count-session-denial-research.md:512-514`). Provider completeness semantics and the actual session denial remain unknown.

## Planning artifact collision and standards

* Existing `.copilot-tracking\plans\2026-10-04\ig-market-collection-failures-plan.instructions.md:1-15,77-102` is a different, partly completed plan. It already contains a reset step and historical claims of backup/reset/test execution. Do not overwrite it, reuse its completion checkmarks, or treat those claims as evidence about today's database. Its final startup/live validation remains outstanding.
* Candidate **new** plan: `.copilot-tracking\plans\2026-10-04\ig-market-data-recovery-plan.instructions.md`; corresponding **new** details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md`. These are proposed tracking artifacts, not a new work package. The named collision above was confirmed by directory inventory.
* Read Markdown rules before writing: `.github\instructions\docs.instructions.md:23-42` requires one H1, ordered ATX headings, fenced language labels, descriptive relative links, and wiki updates after implementation. Its `docs/**` placement constraint is scoped to docs, not this explicitly requested tracking supplement.
* `.github\instructions\work-packages.instructions.md:26-28,36-46,62-65` prohibits unsolicited work packages; numbered plans inside an existing work package require its `plans` folder and wiki-impact assessment. Do not invent a new `docs\00x-work` package for this request.
* `.github\copilot-instructions.md:35-46` requires repeatable scripts, documented tests, wiki source-of-truth updates, and file-line evidence.
* `.github\instructions\architecture.instructions.md:54-80` keeps use-case policy inward, provider/persistence details in adapters, and concrete wiring in composition roots.
* `.github\instructions\configuration.instructions.md:23-39` requires SQL-backed operator settings, UI-mediated updates where exposed, and write-only protected secrets. Adding appsettings alone is not an operator-configuration implementation.
* `.github\instructions\csharp.instructions.md:23-67` requires existing language/format conventions, one type/file, null/cancellation safety and behavior tests. `.github\instructions\refactoring.instructions.md:23-49` requires contract-preserving, incremental changes with targeted validation.
* `.github\instructions\tests.instructions.md:27-69` requires unit-first deterministic tests, requirement comments and repository naming, plus fixture-owned external dependencies. Reset SQL needs real SQL integration coverage, not only an EF in-memory test.
* Wiki-impact candidates, all **existing**: `docs\wiki\runtime-behavior.md`, `docs\wiki\operator-guide.md`, `docs\wiki\local-development.md`, `docs\wiki\testing-and-quality.md`. Cover pacing/retry ownership, continuation/lease behavior, diagnostics, reset retention/audit and validation. Planning-only work must not assert these changes are implemented.

## Exact implementation seams

All paths in this section are **existing** unless marked new.

| Concern | Evidence and planning action |
|---|---|
| Listing page profile | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:11-23,125-155` owns default 150, bounds and adaptive restart. Plan fixed diagnostic size 25 with adaptive restart deliberately bypassed, not merely a new constructor default. Primary research `:301-321,337-350` explains missing production binding and the 100-page/15,000-result capacity conflict. |
| Production wiring | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\DependencyInjection\PlatformInfrastructureServiceCollectionExtensions.cs:142-155` registers one shared throttle and typed category/listing/detail clients; listing page size and pacing are not bound there. `:163-167` already demonstrates removing resilience handlers for the account-preferences client. Use targeted collection-client configuration, not a global trading-client delay. |
| Operator collection settings | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\Entities\InstrumentCollectionSettingsEntity.cs:3-12` contains frequency, pending frequency/date, approved allowance, lead-in and configuration version only. New operator-managed pacing fields imply entity/migration/read-update API/UI validation changes; an internal diagnostic profile may instead be explicit DI-bound options. Decide ownership in the plan, do not silently claim either exists. |
| Shared rate enforcement | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgProviderRequestThrottle.cs:10-40,66-126,130-142,165-180` has process-wide next-start scheduling, 250-ms default spacing, 30 account/60 application per-minute caps, SQL hash-scoped reservations, and one-minute expiry. Preserve caps/shared reservations; implement five-second spacing only for the agreed background non-trading workload. A process-wide changed default also affects other callers. |
| Actual sends | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:341-365,394-415,437-456` waits, reserves daily budget, then calls `SendAsync` for both session and page. Unsuccessful session/page bodies are discarded at `:366-369,421-424`. Introduce bounded allow-listed code parsing and explicit initial/reauthentication phase without raw bodies/tokens. |
| Hidden retry ownership | `src\TNC.Trading.Platform.ServiceDefaults\Extensions.cs:42-48` applies standard resilience to default HTTP clients. A gateway reservation is outside `SendAsync`; production-handler retries can bypass per-send pacing/budget. Decide one retry owner: remove competing resilience retries for affected IG collection clients or place reservations/pacing at each actual attempt. Test DI-built handlers; bare-client tests cannot prove this. |
| Outer listing retries | `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentRetryPolicy.cs:6-38` caps two extra attempts with 2/5-second backoff, excluding allowance/rate/schedule/lease/unauthorized etc. Do not blindly retry unknown session403 or conflate a category attempt with HTTP attempts. |
| Category boundary pause | `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:435-478` collects selected categories sequentially. Add a cancellation-aware 60-second pause only between actual categories, with lease/context renewal throughout waiting; it must not replace within-category shared throttling. |
| Cycle and parent leases | Coordinator `:30-31,765-793` uses two-/five-minute durations and renews both between operations. `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentRequestBudget.cs:21-35,84-108` validates the parent but renews only the cycle per reservation. `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDataFullRunLeaseGuard.cs:61-75` rejects expired parent owners. Long page traversal needs parent/cycle heartbeat/checkpoints, not a longer sleep alone. |
| Window semantics | Listing gateway `:341-346,394-399,467-473` still stops throttle/response validation at the frozen window end. Coordinator `:770-793` and request budget `:57-60,94-108` allow some admitted full-run continuation. Document and test one consistent policy at the actual send/publication boundary before increasing elapsed duration. |
| Details retries/capacity | `src\TNC.Trading.Platform.Application\Features\MarketDetails\MarketDetailCollectionCoordinator.cs:235-281` checks worst-case allowance/rate time and performs up to three passes with 2/5-second delays. `src\TNC.Trading.Platform.Application\Features\MarketDetails\MarketDetailCapacityPolicy.cs:5-9,34-59` counts login, bulk batches and bounded reauthentication. Update timing estimates if the scoped profile covers details too; do not budget only listing GETs. |
| Details budget | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailRequestBudget.cs:17-40,63-85` checks detail/parent lease and delegates to the shared cycle budget. Resetting a details table does not replenish request allowance. |
| Same-slot provenance | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailRunStore.cs:119-138,169-178` looks up environment/day/slot, supersedes a mismatched provenance row and later refuses `Superseded`/`Complete`. `PlatformDbContext.cs:510-525` enforces a unique environment/day/slot. This is a recovery design issue, not a session403 explanation. Plan a tested run-generation identity change or explicitly scoped detail-run removal; never just relabel superseded old work as fresh. |
| Listing-to-details freshness | `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailListingSourceReader.cs:44-78,83-130` requires current selected category codes, validated cycle prerequisite, matching schedule and a complete profile/version-specific listing. `IsFresh` additionally compares day/slot/category revision. Retained last-good sources are not proof of a new listing attempt. |

Candidate **new** type paths, contingent on ownership selected by the plan:

* `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCollectionProfile.cs`: inward policy value for page/pacing/category-spacing/capacity only if coordinated policy needs it.
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgCollectionRequestPacer.cs`: scoped actual-attempt pacing implementation if extracting that responsibility.
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgProviderErrorCodeParser.cs`: bounded shared safe-code parser only if reuse with `IgMarketDetailsGateway.cs:550-591,674-706` warrants extraction.
* New migration under `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\Migrations\` only after deciding whether settings persistence or detail-run generation changes require schema changes. Do not invent a timestamp or claim migration already exists.

## Existing reset tooling is narrower than fresh-state recovery

The only located reset asset is **existing** `infra\scripts\reset-instrument-collection-slot.sql`.

* `:1-15,19-45` hardcodes a sample day/slot, verifies `platformdb`, and identifies exactly one applied IG Demo (`Ig`/`Demo`/`IgDemo`). Do not copy the sample date into a future execution.
* `:47-77` takes full-run and cycle application locks. `:79-99` requires an expired/no lease and rejects a later slot.
* `:101-115` rejects any successful attempt or any listing publication in that slot. `:117-160` restricts completed/pending resets.
* `:174-181` requires remaining approved allowance; `:183-197` releases only completed-partial full-run coverage.
* `:206-214` resets attempts/legacy safe error, but does **not** clear new `FailureKind`, `ProviderErrorCode`, `HttpStatus`, `Operation`, `PageNumber`, `Expected`, `Actual` fields. Those fields exist in `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\Entities\InstrumentCollectionCategoryAttemptEntity.cs:12-20`.
* `:216-239` deletes scoped coverage and makes the cycle/prerequisite pending, preserving `UsedRequestBudget` and cycle fence. It does not clear detail runs/targets/current snapshots/eligibility, listing snapshots or full-run history/intents/follow-ups.
* `docs\wiki\runtime-behavior.md:182-197` expressly describes failed-only replay, preservation of historical run/consumed requests and refusal of successful-category replay. Its “restart then reset” guidance is **not** suitable for a broader destructive final reset: stop writers first.
* No dedicated reset-script test was located by searches for its filename/guard codes under `test`. Add one rather than claiming existing integration tests execute this script.

Recommended **new** asset for a distinct broader recovery contract: `infra\scripts\reset-ig-demo-market-data-recovery.sql`. Keep the existing narrow script intact unless separately fixing its newly coupled diagnostic-clearing defect. The new script should default to dry-run and require an explicit scoped apply confirmation; no database destruction or unfiltered truncate.

## Safe final-stage reset contract

### Scope choice

Prefer the least destructive scope that genuinely unblocks the desired request:

1. **Failed-slot rearm**, when existing guards pass and details are independently eligible: retain last-good listing/detail snapshots, historical full runs and budget consumption; clear only target cycle attempts/diagnostics/prerequisite and coverage. This best preserves the primary research's last-good semantics.
2. **Fresh Demo collection-state reset**, when a same-slot detail identity or published-state blocker makes the failed-only script insufficient: use the new guarded script to remove precisely inventoried collection artifacts for the explicitly confirmed Demo environment and recovery run/day/slot set. Retain unrelated history when a smaller dependency closure is sufficient. An environment-wide collection-artifact clear is an explicit broader mode, not an implicit default. Export removed diagnostics/observations first. Clearing snapshots is not necessary merely to request categories again, so make it an explicit scope decision.

A run-scoped deletion must include every dependent detail run source referring to a listing collection slated for removal, even if its detail run has a different day/slot. Conversely, EPICs can span categories; do not delete a shared current pointer merely because one category is selected. Either restrict closure to all dependent artifacts with an audited scope, or retain the referenced listing/detail history.

### Required ordered execution design

This is a proposal for the future final stage, not an operation performed here:

1. Finish code/schema/documentation changes and offline validation first. Do not reset data merely to investigate a failing implementation.
2. Verify exact local SQL server/database identity, applied environment GUID/provider/kind/profile, effective schema/migrations, intended recovery day/slot and affected row counts. Reject Live/nonlocal/ambiguous targets. Record counts and nonsecret hashes only, not credential material.
3. Stop the API/collector and every other writer that can share this Demo/provider scope. Verify no in-flight provider work or live full-run/cycle/detail leases; stopping a worker does not automatically clear durable leases. Wait for expiry or use a designed fenced cancellation; never simply null an active owner's lease. Keep SQL/Keycloak infrastructure available.
4. Take and verify a recoverable backup/export before mutation; it can contain protected secrets and must be access-controlled, outside source control. Capture the selected deletion closure, diagnostics, budget totals and preserved-table counts.
5. Use `XACT_ABORT`, TRY/CATCH rollback, serializable/protected reads and existing full-run/cycle/detail/budget locks in consistent ordering. Include assertions for expected scope/counts and refusal on concurrent change. Recheck the actual database FK graph after migrations, not just this source model.
6. Execute the smallest selected reset dependency closure below; clear all attempt diagnostic fields when resetting counters. Preserve cycle `UsedRequestBudget` and historical daily totals, including requests made during failed runs.
7. Commit only after integrity, preservation and fresh-eligibility assertions pass. Produce a sanitized receipt distinguishing “reset applied” from “provider collection succeeded.”
8. Finish with the worker still stopped and read-only eligibility checks. Schedule-controlled next startup/live confirmation is a separately gated action, not hidden inside the reset script or planning request.

### Preservation boundary

Preserve `BrokerEnvironments`, `BrokerEnvironmentSelections`, defaults/schedule/retry/notification profiles, `PlatformConfigurations`, `ProtectedCredentials`, `DataProtectionKeys`, auth runtime/audit/proof/account/account-preference data, operational/configuration audit and unrelated environments. Preserve `InstrumentCollectionSettings`, `MarketCategoryInterestStates` and `MarketCategoryInterests` including their revision values. Retain category definitions/catalogue state by default; pending prerequisite refresh already requests categories again.

`PlatformDbContext.cs:9-46,66-68,373-401` establishes these independent state/settings sets. Interest keys reference the broker environment, not a listing snapshot (`:373-380`).

Do **not** clear `IgProviderRateReservations` to manufacture capacity. It is hash-scoped by application/account, not broker environment (`PlatformDbContext.cs:683-694`); its scopes can be shared with other traffic. Let real one-minute reservations expire naturally (`IgProviderRequestThrottle.cs:165-180`). A local reset cannot replenish IG-side quotas, daily/historical allowances, repair a key/account denial, or prove another client is idle.

Preserve provider-confirmed `MarketDetailEligibility` exclusions by default (`PlatformDbContext.cs:667-679`; `EfMarketDetailRunStore.cs:416-432,864-891`). They are genuine provider evidence, not merely exhausted run attempts. New successful observations can restore eligibility (`EfMarketDetailRunStore.cs:592-609`). Do not promise every EPIC becomes eligible after reset.

### FK dependency closure

All described relationships are restrictive, not cascading. Evidence: `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\PlatformDbContext.cs`.

| Artifact group | Child-before-parent order if removal is explicitly selected | Source |
|---|---|---|
| Details | `MarketDetailCurrent` pointers referencing removed observations; `MarketDetailObservations` and `MarketDetailRunMemberships`; then `MarketDetailRunTargets` and `MarketDetailRunSources`; then `MarketDetailCollectionRuns` | `:534-559,565-602,631-640,653-663` |
| Listings | Dependent `MarketDetailRunSources` first; `MarketCategoryInstrumentObservations` before `MarketCategoryInstrumentCollectionRuns`; `MarketCategoryInstruments` before `MarketCategoryInstrumentCatalogStates` | `:441-459,470-480,534-545` |
| Category catalogue | Keep by default. If explicitly clearing it, remove listing catalogue states first because they reference `MarketCategories`; preserve interested codes and interest-state revisions | `:441-448,373-380` |
| Full runs | Scoped `MarketDataFullRunSlotCoverages`, `MarketDataFullRunCategories`, `MarketDataFullRunStages`, `MarketDataFullRunItems` before `MarketDataFullRuns` | `:727-769,778-785` |
| Intents/follow-up | `MarketDataFullRunIntents` is independently environment-keyed; remove only recovery-scope stale/coalesced intent after audit, or retain a compatible one deliberately. Full-run follow-up markers live on the parent and can trigger details-only work rather than a fresh full sequence | `:698-722,793-803`; `EfMarketDataFullRunStore.cs:151-190` |
| Cycle/attempt | Retain daily consumption ledger rows; reset selected current cycle/prerequisite/attempt state to pending. Category attempts reference environment, not the cycle; SQL FK enforcement will not ensure semantic consistency | `:396-434`; `EfMarketCategoryInstrumentCycleStore.cs:520-551` |

Never delete all cycle states: daily usage is computed by summing those rows, so deletion grants fictitious unused capacity. If earlier terminal cycles prevent scheduling through latest-progress selection, reopen only the latest intended current eligible slot without deleting its budget, or wait for a real next slot/day. Retain earlier and unrelated cycles. `EfMarketCategoryInstrumentCycleStore.cs:25-44` orders latest day/slot; `:543-551` sums usage and increments it under budget lock.

## Measurable fresh-request eligibility

Reset success requires all relevant checks below; “empty tables” or “Pending” alone is insufficient:

1. Applied GUID/profile still identifies executable IG Demo with market-data capability; schedule references that GUID, has valid timezone/version/days/window and no reconciliation block. `src\TNC.Trading.Platform.Application\Features\TradingState\TradingStateEvaluator.cs:21-39,70-121` separates market-data readiness from trading-session readiness.
2. The next controlled startup's trading day/current slot is known, effective scheduled frequency is positive, and its window is open. A reset on Sunday/closed configured days does not grant permission to run. `MarketCategoryInstrumentSchedulePolicy.cs:23-39,48-88,90-108` handles disabled/closed/observed slots; do not edit the schedule just to make reset appear successful.
3. No active prior full run/lease is resumable; the chosen scheduled/catch-up slot coverage is absent. Otherwise admission returns `AlreadyRunning`, `Resumed` or `AlreadyCovered`, not a new run. `EfMarketDataFullRunStore.cs:43-122,203-234`.
4. Pending configuration/interest intent and failed-item follow-up are absent or explicitly compatible with the intended fresh-full-run trigger. Confirm a new full run would start `Categories`, not details-only retry (`EfMarketDataFullRunStore.cs:141-155,249-253`).
5. Cycle outcome/prerequisite and category counters are pending, selected attempts are zero, all stale failure fields are null and no live lease remains. Budget consumption is unchanged. There must not be a later incompatible slot (`reset-instrument-collection-slot.sql:79-115,206-239`).
6. Selected category interests/revisions are unchanged and nonempty for a meaningful listing request. Category refresh must precede listings; newly complete listing collection IDs/versions and day/slot/catalogue revision establish fresh detail sources (`EfMarketDetailListingSourceReader.cs:44-78,83-130`).
7. Details have a genuinely new eligible generation/key, or the scoped conflicting old detail run/dependencies were safely removed. Do not recycle a `Complete`/`Superseded` row; targets for newly staged work begin `Pending` (`EfMarketDetailRunStore.cs:119-178,249-270`).
8. Approved allowance minus **preserved** consumed usage is sufficient for the bounded listing/session/reauth/retry and intended detail capacity, not just greater than zero. Shared reservation windows/provider cooldown remain respected (`EfMarketCategoryInstrumentCycleStore.cs:520-551`; `MarketDetailCapacityPolicy.cs:34-59`).
9. Record a reset timestamp and baseline IDs/counters. A later separately authorized live validation must show new full-run/collection IDs, new attempt timestamps, measured request-start spacing, fixed page size, complete-only publication and detail provenance. Retained snapshots/new UI diagnostics must not be mistaken for that proof.

## Validation inventory for the plan

No validation commands were executed for this documentation-only supplement.

Repository documented commands: `docs\wiki\local-development.md:103-119,266-280` (`dotnet build`, isolated output option, `dotnet test`, serialized fallback); `docs\wiki\testing-and-quality.md:530-550` (existing quality-gate script and full serialized suite). Use Windows separators:

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgMarketCategoryInstrumentsGatewayTests|FullyQualifiedName~IgMarketDetailsGatewayTests"
dotnet test test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~MarketCategoryInstrumentCycleCoordinatorTests|FullyQualifiedName~MarketDetailCollectionCoordinatorTests|FullyQualifiedName~MarketDetailCapacityPolicyTests"
dotnet test test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~MarketCategoryInstrumentSqlIntegrationTests|FullyQualifiedName~MarketDetailSqlIntegrationTests|FullyQualifiedName~MarketDataFullRunSqlIntegrationTests|FullyQualifiedName~IgProviderRequestThrottleSqlIntegrationTests"
dotnet test TNC.Trading.Platform.slnx -m:1
```

The listed test classes/projects exist; focused filters are proposed selectors derived from that inventory, not verbatim documented recipes. Existing `MarketDataFullRunSqlIntegrationTests.cs`, `MarketCategoryInstrumentSqlIntegrationTests.cs`, `MarketDetailSqlIntegrationTests.cs` and `IgProviderRequestThrottleSqlIntegrationTests.cs` are relevant SQL boundaries. Candidate **new** reset test: `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\IgDemoMarketDataRecoveryResetSqlIntegrationTests.cs`.

Add deterministic coverage for fixed 25-row traversal/bounds/short unions; safe initial/reauth403 parsing/redaction; actual-attempt accounting with production DI; five-second starts/60-second category spacing/cancellation; parent and child lease renewal during long work; coherent window semantics; same-slot generation recovery. Reset SQL tests must seed restrictive FKs, shared EPICs, preserved usage/reservations/exclusions/credentials/config/interest revisions, Live and second-Demo data; assert dry-run/no-op/refusal/rollback, idempotence and new admission/detail eligibility. Fixture-owned SQL only: do not run reset tests against the developer database.

## Remaining blockers and bounded decisions

* **Provider completeness contract**: fixed traversal can be diagnostic now, but successful complete publication still needs authoritative count/exhaustion semantics. Preserve final union/duplicate/metadata validation; do not mask 70/41.
* **Actual denial code and runtime HTTP pipeline**: historical discarded body is unrecoverable; new diagnostics need a later controlled request. Determine actual installed retry behavior via production DI tests before claiming accounted pacing.
* **Live reset preconditions**: current database/server/schema, active workers/leases, day/slot, consumption, other shared clients and deletion counts were not inspected. The supplement defines preflight gates, not a claim that reset is executable now.
* **Scope/generation decision**: choose minimal rearm versus explicit dependency-closed clean collection state; choose detail generation identity before designing same-slot reset. Preserve provider exclusions and consumed quota in both.
* **No new external research required**: use already verified primary sources and `https://aspire.dev/` for any future lifecycle guidance. This planning request must not start/stop Aspire or initiate IG traffic.
