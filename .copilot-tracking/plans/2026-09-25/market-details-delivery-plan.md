# Delivery Plan: IG Market Details for Interested Categories

## Purpose and Source

Deliver schedule-gated, saved Market Details (`instrument`, `dealingRules`,
`snapshot`) for **every distinct eligible EPIC** in every currently interested
and current category. Preserve individually valid observations even when a
run is incomplete; require complete run coverage for any later all-instrument
selection or analysis. This document is a **plan only**. Creating it changes
no application code, tests, schema, runtime configuration or wiki behavior.

Primary evidence: [Market Details research](../../research/2026-09-25/market-details-research.md).
The layout, unchecked work items and mandatory quality gates follow the
[Market Category Instruments delivery plan](../2026-09-24/market-category-instruments-delivery-plan.instructions.md).
Use the research's repository evidence and provider links as the source
inventory; the decisions and implementation sequence below are sufficient
to execute without another open-ended research phase.

## Delivery Boundaries and Resolved Decisions

* In scope: a new Application MarketDetails slice, version-specific IG
  transport/validation, environment-scoped SQL run/target/membership/history
  and latest-good projections, a resumable schedule-gated worker extension,
  shared durable request controls, protected saved-data reads, a dedicated
  Web detail page with compact status on existing pages, tests and wiki updates.
* Out of scope: streaming, trades/order validation, recommendations or the
  subsequent instrument-selection algorithm, ad hoc/manual market-detail
  refresh, browser-to-IG requests, unbounded history exports, new frequency
  settings, and automatic deletion or archival of detail observations.
* A slot freezes **complete immutable listing runs** for the intersection of
  current category catalogue and shared interest. Include complete empty
  listings. Deduplicate EPICs per environment/slot for transport and storage,
  but retain one membership per source category. Never construct the universe
  from one of the existing 50-row Viewer pages, a stale listing, or a dormant
  category. A failed category/listing prerequisite blocks full coverage; do
  not silently count its last-good listing as current.
* A run's `ExpectedCount` means eligible distinct targets requiring a valid
  observation for this run; `CompletedCount` counts those with exactly one
  validated observation. `ExcludedCount` counts frozen targets subsequently
  confirmed unavailable/expired by IG; `FrozenCount =
  ExpectedCount + ExcludedCount`. `OutstandingCount = ExpectedCount -
  CompletedCount`, and failed targets are a subset of outstanding targets,
  not a second denominator. Derive and check counts from target rows during
  finalization. A run is `Complete` only with validated catalogue/listing
  sources, unchanged applicable revisions, confirmed exclusions and
  `CompletedCount == ExpectedCount`; a zero-target run completes only with
  validated empty prerequisites. An individually complete EPIC can be read
  during an aggregate-incomplete run but cannot make that run analysis-ready.
* EPICs disappearing from a newly validated current interested listing are
  absent from the **next** frozen universe, not provider exclusions counted
  against it; retain their history and last-good data with appropriate
  historical/out-of-date labeling. Only documented, EPIC-specific provider
  evidence may mark an existing target excluded. A temporary `CLOSED` quote,
  non-tradeable category flag, generic 404 on a malformed batch, timeout,
  401, or other transient error cannot do so. Later positive provider evidence
  should reinstate eligibility; a reappearing listing alone triggers an
  attempt/reconciliation rather than permanent blacklisting.
* Prefer `GET /markets?epics=...&filter=ALL` v2, up to **50 EPICs** per
  request, with the existing v2 session's CST/security token. **Do not ship**
  bulk parsing as complete until controlled Demo contract fixtures show that
  `instrument`, `dealingRules`, and `snapshot` contain every required field.
  For confirmed bulk gaps, use a separately budgeted version-specific
  `GET /markets/{epic}` fallback only for affected EPICs/fields. Do not mix
  single-market v3 (`updateTime`, bid/offer) and v4
  (`updateTimestampUTC`, price ladder) shapes or auth headers. If a safe,
  complete path is unavailable, keep the run visibly blocked/incomplete,
  retain last-good observations, and do not pretend incomplete data is valid.
* Use the **same** applied-environment/local-trading-day durable daily
  allowance for category, listing, login, bulk detail, fallback and replay
  requests. The existing 250 ms throttle is per process, not a cross-replica
  quota. Serialize all relevant IG consumers per account/API key or add a
  durable distributed rate limiter that actually enforces the documented
  default **30/account/min** and **60/app/min** non-trading limits, including
  other consumers; never confuse them with the operator-approved daily
  allowance. On insufficient measured budget/window capacity, pause with an
  explicit reason before an unbounded fetch, not a silently complete subset.
* Detail runs have their **own** durable fenced lease and resumable target
  state. The listing cycle's `Completed` result and lease must retain their
  existing semantics; neither permits an expired listing lease to authorize
  detail calls. Resume an interrupted detail run only within its original
  active slot/window; no after-hours or next-day catch-up. Interest/catalogue/
  schedule/applied-profile drift supersedes the frozen run for current
  coverage, without deleting its validated individual history.
* Persist complete validated observations indefinitely in SQL Server as the
  analysis source, including successes from incomplete/superseded runs and
  later-retired environments/categories; **do not** apply operational-event
  retention. Monitor row growth; add no purge/archive until a later versioned
  policy specifies a verified destination and sufficient analysis window.
  Current/latest-good projections may be cleared on environment retirement
  while history and its parent run/source/target records remain.
* Read paths use the applied environment and SQL only. A known current EPIC
  with no details returns a typed `200 NotCollected`, not a fabricated
  observation; an unknown current category/EPIC returns `404`, and optional
  mismatched listing version returns `409`. Historical access, if exposed,
  uses a separate bounded environment-scoped read; never make a retired
  category appear current. No raw provider response, credential, auth header
  or untrusted `specialInfo` markup reaches UI or logs.

## Non-negotiable Behaviour and Data Contract

1. Reuse `TradingScheduleGate`/existing slot policy and applied endpoint
   resolver. Check active day, holiday, configured time zone and
   start-inclusive/end-exclusive window before each provider request, after
   each response and at SQL publication; use UTC instants for retrieval and
   local schedule day for quota/slot identity. Disabled schedule, invalid
   allowance/profile or unsupported applied environment means zero IG calls.
2. Acquire/renew/fence the detail run; freeze source listing collection IDs,
   versions, catalogue/interest/schedule revisions and profile before staging
   targets. Per-EPIC successes are atomic with target success and latest-good
   pointer; final aggregate completion is a separate transaction verifying
   every source/target and current revisions. No SQL transaction spans HTTP.
   Late responses, stale leases or changed applied environments cannot publish.
3. Persist source endpoint/version, run/slot, per-market `RetrievedAtUtc`,
   provider unzoned `updateTime` text (or version-specific actual UTC value),
   market status and delay, full validated nested terms and nullable numeric
   values. Retain individual rule value **and unit**, ordered currency/band/
   notice fields, and `max: null`. Use decimal precision, not floating point.
   Distinguish absent/null/zero where the provider contract makes it material.
   Provider status/capability flags and saved prices are facts, not trading
   permission or live quotes.
4. Bound batch size, URL length, field/array/payload sizes, HTTP timeouts,
   per-EPIC attempts (at most initial attempt plus two retryable attempts)
   and per-slot work. Validate exactly one matching EPIC per requested
   response, no extras/duplicates, complete required sections and usable
   numeric values. Persist safe per-target failure instead of a fake
   observation. Retry only within the same slot/lease/window and quota.
   Reauthenticate at most once after 401; classify documented 403 allowance
   exhaustion separately from 403 authorization and 429. Never log secrets.
5. Freshness has two axes: **individual observation** and **aggregate
   current-universe run**. Saved last-good details remain readable, clearly
   `Out of date` for outstanding/blocked/superseded later runs; full-coverage
   analysis reads a complete run's member observations, not mixed latest-good
   values across slots. Never equate the listing retrieval time with the
   detail retrieval time or parse an unzoned IG `updateTime` as UTC/local.
6. Viewer GETs, page/tab navigation and saved-data Reload perform SQL reads
   only; protect with existing Viewer auth and applied-environment scope.
   No operator UI action or API read starts an IG market request.

## Reusable Precedents and Change Locations

| Boundary | Starting point and intended extension |
| --- | --- |
| Schedule/collector | `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs`, `MarketCategoryInstrumentSchedulePolicy.cs`; create `Features/MarketDetails/` contracts, policy/coordinator and detail lease without changing listing completion |
| IG and request controls | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs`, `IgEndpointProfileResolver.cs`, `IgProviderRequestThrottle.cs`; add `IgMarketDetailsGateway`, versioned wire DTOs and shared distributed quota discipline |
| SQL | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`, `EfMarketCategoryInstrumentCycleStore.cs`, `EfMarketCategoryInstrumentRequestBudget.cs`, `EfMarketCategoryInstrumentSnapshotStore.cs`; add detail entities, migration, run store, observation writer/reader and shared lease-aware budget |
| API/worker | `src/TNC.Trading.Platform.Api/Hosting/`, `Features/Platform/MarketCategoryInstrumentEndpoints.cs` and existing composition roots; resume details within the same due slot, map protected dedicated saved-data endpoints and compact projections |
| Web | `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor`, `MarketCategoryInstruments.razor`, `MarketCategoryInstrumentsPagePresenter.cs`, `PlatformApiClient.cs`; compact coverage/link and one lazy-loaded `MarketDetails.razor`/`.razor.css` page |
| Tests/docs | Existing Application, Infrastructure SQL/unit, API integration, Web bUnit/client and AppHost/functional/E2E tests under `test/`; update `docs/wiki/` only with implemented behavior |

## Work Items

### [x] Work Item 1 — Verify the Provider Contract and Define Inward Policies

**Objective:** Resolve the finite v2-bulk-vs-single version/coverage fork
before schema or collector assumptions become irreversible.

#### [x] Tasks and steps

1. [x] Using the [IG bulk reference](https://labs.ig.com/reference/markets.html),
   [single-market reference](https://labs.ig.com/reference/markets-epic.html),
   [REST guide](https://labs.ig.com/rest-trading-api-guide.html) and **safe
   controlled Demo fixtures** (no recorded credentials or live-account
   dependency), establish Version headers, `filter=ALL`, v2 `marketDetails[]`
   cardinality, mixed-success/whole-request failure, encoded URL length,
   supported market types, and fields actually returned. Compare field by
   field with the research's instrument/rules/snapshot inventory, including
   nullable/nested/zero and single-market v3/v4 differences. Record fixture
   evidence and choose bulk plus targeted single fallback, or block delivery
   of "complete" collection if no safe path exists. This is a **bounded
   contract verification**, not a request for further product research.
2. [x] Define inward request/result/port types in
   `Application/Features/MarketDetails/`: collect/query operations,
   version-aware validated observation, safe failures, individual and
   aggregate state, frozen source/target/membership, eligibility policy,
   gateway, run store, observation writer and SQL-only reader. Separate
   provider DTOs (Infrastructure), API DTOs (API) and Web view models (Web).
   Use one top-level C# type per file, explicit nullability and typed units.
3. [x] Specify state transitions in tested policy: `NeverCollected`,
   `Running`, `Incomplete`, `Complete`, `Blocked`, `Superseded`, plus
   individual `NotCollected`, `Complete`, `OutOfDate`, `Excluded`; retries,
   reinstatement, complete empty universe, count invariant, revision drift
   and safe failure classifications. An excluded target remains in frozen
   target/membership history; missing from a later validated listing does
   not count as a provider exclusion.
4. [x] Unit-test state/count transitions, shared-EPIC membership,
   missing listing source, provider-confirmed vs transient exclusion,
   supersession, unit/null/zero handling and fake-clock schedule boundaries.
   Document tests with requirement-traceability comments and repository
   MethodName_StateUnderTest_ExpectedResult names.

#### [x] Work Item 1 quality gate — mandatory before Work Item 2

1. [x] Run focused Application/contract tests, then a **full**
   `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` with
   zero warnings/errors, then the **full** test suite
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
   with every test passing.
2. [x] Examine and resolve **all** build warnings/errors and any failing
   tests, including apparent baseline failures, preserving application
   behavior and this research. Rerun both full commands after fixes; record
   SDK, commands, warning/error count, per-project test totals, fixture
   findings, chosen endpoint/version and any blocked provider contract.
   Do not check the gate or proceed on an unverified contract.

### [x] Work Item 2 — Persist Frozen Detail Runs and Immutable Observations

**Objective:** Add restart-safe, environment-scoped SQL state and historical
provenance without altering replaceable listing snapshots.

#### [x] Tasks and steps

1. [x] Add an additive EF Core migration/model snapshot and entities for
   `MarketDetailCollectionRuns` (unique environment/trading-day/slot, fenced
   lease, revisions/profile/status/counts/window), `MarketDetailRunSources`
   (run/category, retained listing collection FK and version),
   `MarketDetailRunTargets` (run/EPIC, status/attempts/safe error/exclusion),
   `MarketDetailRunMemberships` (run/EPIC/category),
   `MarketDetailObservations` (unique run/EPIC, per-market retrieval UTC,
   endpoint/version, typed selection attributes and full validated terms),
   `MarketDetailCurrent` (environment/EPIC latest-good pointer) and
   `MarketDetailEligibility` (provider-confirmed evidence and reinstatement).
   Scope FKs to retained environment/run/listing records, never to mutable
   current listing rows; match the listing's case-sensitive EPIC collation.
2. [x] Choose typed indexed searchable fields plus bounded, schema-versioned
   nested JSON **only** for variable fields not needing relational queries,
   or normalized ordered child tables where queries demand them. Preserve
   all currencies, margin bands, notices and optional fields; bound size and
   use explicit decimal SQL precision/scale. Validate representative SQL
   row/index/payload growth before settling sizes; index history by
   `(environment, EPIC, RetrievedAtUtc)` and targets by `(run, status)`.
3. [x] Stage the source/target/membership universe transactionally from
   validated complete immutable listing runs, via bounded SQL batches rather
   than a long-lived EF tracker. Persist failures/exclusions without creating
   observations. Commit validated observation + target success + latest-good
   pointer in one short fenced transaction; use a second transaction to
   recalculate counts/source revisions and finalize. Handle unique insert
   and rowversion/update conflicts explicitly; never mark them successful
   by swallowing exceptions. History survives restart, later category
   replacement and environment retirement; only the active projection/lease
   is cleared on retirement.
4. [x] SQL integration-test two categories sharing one EPIC (one target and
   observation, two memberships), validated empty universe, failed listing
   prerequisite, partial success, no duplicate replay, stale lease,
   revision change, retirement without history cascade, and per-market
   retrieval time/source/version/nested nullable value round trips.

#### [x] Work Item 2 quality gate — mandatory before Work Item 3

1. [x] Run focused SQL/model tests and migration/model-sync validation, then
   the **full zero-warning/error solution build** and **full passing solution
   test suite** using the exact two commands in Work Item 1's gate.
2. [x] Diagnose and resolve all warnings/errors and failing tests, including
   Docker-backed SQL fixture failures; rerun both full commands and record
   SDK, warning/error count, per-project test totals, migration evidence and
   SQL storage/row-size measurements before proceeding.

**Work Item 2 evidence:** SDK 10.0.302; additive migration
`20260925185108_PersistMarketDetailRuns`; EF model-sync validation reported no
pending changes. Focused SQL tests: 6 passed. SQL probes verified a
representative combined nested-JSON payload below 40,000 bytes, maximum
physical index record below 8,060 bytes, and one-observation index allocation
below 1,000,000 bytes. Full build passed with 0 warnings and 0 errors. Full
solution tests passed: 877 total (Application unit 212, Infrastructure unit
233, Infrastructure integration 62, API unit 85, API integration 26, Web
unit 176, Web functional 13, Web E2E 13, AppHost unit 44, Architecture
integration 13). EF CLI 10.0.5 reported that it is older than runtime
10.0.12; migration generation and model-sync validation both completed
successfully.

### [x] Work Item 3 — Integrate IG Details with Shared Budget and Rate Safety

**Objective:** Acquire complete version-correct detail records without
unbudgeted provider requests or unintended Demo/Live fallback.

#### [x] Tasks and steps

1. [x] Implement `IgMarketDetailsGateway` with separate versioned wire DTOs:
   preferred v2 bulk `filter=ALL` batches of at most 50, smaller when URL
   length/provider behavior demands; only contract-verified single-EPIC
   fallback for missing required fields. Encode EPICs, use the applied Demo
   or Live capable profile and existing protected v2 login, set explicit
   resource `Version`, accept JSON and verify exact requested EPIC identity.
   Validate every returned market independently, including
   `instrument`/`dealingRules`/`snapshot`, rules' own units, decimals,
   nullable maxima, ordered arrays and provider text. No null-filled
   complete records, no unredacted raw bodies in logs.
2. [x] Extend the existing **environment/day** SQL allowance authorization
   to accept a valid fenced *detail* lease (not a stale listing lease).
   Reserve **before each** session, bulk, single fallback and 401 replay
   request, including failed HTTP requests. Coordinate account/API-key
   non-trading per-minute limits across replicas and competing IG callers;
   retain the daily operator-approved cap. Make 403 allowance exhaustion,
   other 403 auth failure, 429, 401 one-time replay, 400 oversize and
   EPIC-specific 404 explicit safe outcomes. Unknown/expired exclusion
   requires verified evidence for the specific EPIC, not a failed mixed batch.
3. [x] Recheck schedule, applied profile, lease/fence and remaining quota
   around each call; limit retries to initial plus two retryable attempts
   within the original slot and deadline. Abort safely on window closure,
   cancelled lease, unsupported profile or capacity shortfall. Never delay
   past the window in order to satisfy rate pacing.
4. [x] HTTP fixture and SQL-budget tests cover Demo/Live isolation, mixed
   responses, duplicate/mismatched/missing/unexpected EPIC, partial fields,
   v3/v4 mismatch, fallback cost, URL/batch bounds, 401 replay, 403 quota
   vs auth, 429, cross-replica pacing, timeout, no secrets in logs, and
   **exactly one allowance unit per attempted HTTP request**.

#### [x] Work Item 3 quality gate — mandatory before Work Item 4

1. [x] Run focused IG fixture/request-budget tests, then the **full
   zero-warning/error solution build** and **full passing solution test
   suite** using the Work Item 1 gate commands.
2. [x] Investigate/resolve every warning/error or failing test, rerun both
   commands, record SDK, warnings/errors, per-project totals, version/
   fallback fixture evidence and quota/rate accounting. Do not proceed if
   field coverage or shared budget discipline is unproved.

**Work Item 3 evidence:** SDK 10.0.302. The supplied ADAUSD and two-EPIC
fixtures verify the v2 `filter=ALL` response sections; no unverified
single-market fallback was added. Focused IG gateway and Application request
contract tests passed (29 and 4); focused SQL rate/budget tests passed (10).
The SQL-backed limiter coordinates 30/account/minute and 60/application/
minute using hashed scopes, while all IG requests remain covered by the
environment/day allowance. Gateway validation now rejects malformed rows,
overlength database fields and JSON, including after typed model
serialization; successful batched observations use the canonical
`/gateway/deal/markets?filter=ALL` source endpoint. `dotnet ef migrations
has-pending-model-changes` reported no pending model changes (EF CLI 10.0.5
against runtime 10.0.12).

The first full-suite run exposed account-preferences functional/E2E failures
because existing controlled fixtures intentionally have empty credentials;
the shared limiter rejected those values before transport. The limiter now
hashes empty fixture scopes conservatively instead of raising an argument
error. After this correction, the full build passed with 0 warnings and 0
errors. `dotnet test .\\TNC.Trading.Platform.slnx --no-build --verbosity
minimal` passed all 910 tests: Application unit 212, Infrastructure unit
262, Infrastructure integration 66, API unit 85, API integration 26, Web
unit 176, Web functional 13, Web E2E 13, AppHost unit 44, Architecture
integration 13. No failure remains; the initial failure was diagnosed and
covered by the final passing run.

### [x] Work Item 4 — Orchestrate and Resume Schedule-Gated Detail Collection

**Objective:** Attach details to the existing due slot without regressing
the category-first listing collector or creating after-hours IG calls.

#### [x] Tasks and steps

1. [x] Add Application coordinator and API host wiring after listing
   publication, and on later worker ticks **only within that same active
   slot**, to acquire/renew an independent detail lease. Preserve listing
   cycle `Completed` and worker readiness semantics and the existing
   30-second maximum recheck delay. Stage fresh selected category sources
   and deterministic distinct targets once; resume only outstanding
   EPICs after restart/lease recovery, not previously successful targets.
   A failed source marks coverage blocked and never substitutes last-good
   listing membership.
2. [x] Estimate no-retry provider cost using category/listing/session calls
   plus `ceil(U / effectiveBatchSize)` and separately budget all planned
   fallbacks, reauth/retries, cross-replica rate and remaining window time.
   E.g. 384 distinct EPICs need at least 8 batches at size 50; a
   384-call single-EPIC fallback cannot be assumed to fit. Missing allowance
   or demonstrably insufficient capacity yields visible `Blocked` or
   `Incomplete`, not a fabricated complete run or an unbounded fetch.
3. [x] Recheck current catalogue/interest/schedule revision, source runs,
   applied profile, lease fence, window and quota before each HTTP request
   and within each bounded publication transaction. If revised, supersede
   current-universe coverage but retain successes as historical observations.
   Publish individual successes independently; finalize only after invariant
   validation. Ended-window attempts remain incomplete/out of date with no
   next-day replay; next due slot freezes a new universe. Use safe
   non-sensitive failure codes and observable aggregate/progress status.
4. [x] Test fake-clock + SQL/worker fixture scenarios: category-first,
   selected vs dormant, validated empty, duplicate EPIC across categories,
   restart/stale lease/repeated worker ticks, mid-response window close,
   DST/holiday/next-day slot, interest/catalogue/profile changes, 429/quota,
   capacity blocked, one failed target amid readable successful peers,
   counted exclusion/reinstatement, latest-good preservation and no
   out-of-hours provider calls.

#### [x] Work Item 4 quality gate — mandatory before Work Item 5

1. [x] Run focused coordinator/worker and SQL tests, then the **full
   zero-warning/error solution build** and **full passing solution test
   suite** using the Work Item 1 gate commands.
2. [x] Resolve all warnings/errors and failed tests (including Aspire/
   Docker dependencies), rerun both commands and record SDK, warning/error
   count, per-project totals and fake-clock/worker evidence before continuing.

**Work Item 4 evidence:** SDK 10.0.302. The worker test confirms the listing
coordinator runs before the detail coordinator on each tick, with separate
scoped lifetimes and clean cancellation. Focused coordinator/capacity tests
passed (6), worker test passed (1), and SQL detail source, capacity, lease,
revision-fence, retry, partial-history, and persistence tests passed (12).
The 384-target estimate is 8 bounded provider batches per pass, 16
no-retry session/request calls, and 72 worst-case calls including two
retryable attempts and one 401 replay per market request. Fake-clock tests
prove that a response at the exclusive window end is not written and that a
closed schedule does not acquire a lease or call IG. SQL tests prove selected
current-source freezing, dormant-category exclusion, blocked stale sources,
per-request revision preflight, and transaction-time supersession without a
late observation. Existing policy/store/IG tests cover empty universes,
duplicate EPIC membership, restart and stale-lease recovery, DST/holiday slot
boundaries, quota/rate failures, partial successes, exclusions/reinstatement,
and retained last-good history. EF model-sync reported no pending changes
(CLI 10.0.5 against runtime 10.0.12). The full solution build passed with
0 warnings and 0 errors. The full solution test suite passed: 920 total
(Application unit 218, Infrastructure unit 262, Infrastructure integration
70, API unit 85, API integration 26, Web unit 176, Web functional 13, Web
E2E 13, AppHost unit 44, Architecture integration 13).

### [x] Work Item 5 — Expose Protected SQL-Only Read and Status Contracts

**Objective:** Support detail, compact coverage and bounded history
without an IG dependency on any Viewer read.

#### [x] Tasks and steps

1. [x] Add inward SQL reader/handler and API-layer
   `MarketDetailAvailabilityResponse` (compact row state),
   `MarketDetailResponse` (individual observation/state and independent
   aggregate/source coverage), and a bounded
   `MarketDetailHistoryPageResponse` if historical browsing ships.
   Return separate listing and detail UTC retrieval times, source
   endpoint/version, safe failures, expected/completed/excluded/outstanding
   counts and next scheduled check when known. Do not load nested details
   on every listing row.
2. [x] Map Viewer-authorized
   `GET /api/platform/market-categories/{categoryCode}/instruments/{epic}/market-details`
   with escaped route params and current category/EPIC membership scoped to
   the applied environment. Return `200 NotCollected` for a known current
   EPIC without observation, last-good plus `OutOfDate` or `Excluded` as
   appropriate, `404` for genuine current membership absence, `409` on an
   optional stale listing version, and explicit safe unavailable state for
   unsupported applied environment. Do not rely on an opaque listing cursor
   for direct links. Preserve existing 401/403 and Problem Details patterns.
3. [x] Extend existing category/listing/status SQL projections to show
   category coverage and per-EPIC availability in **bounded batched
   queries**, retaining separate listing outcome/request quota. An
   unselected category is `Not followed`; blocked/incomplete runs never
   appear complete because earlier latest-good observations exist. Add a
   separately scoped keyset/history cursor only if the UI exposes history;
   bound page size and guard environment/EPIC/version on continuation.
4. [x] API integration tests check Viewer/Operator auth, applied Demo/Live
   isolation, 404/409/NotCollected/stale/partial/excluded semantics, exact
   timestamp/source shape, SQL-only calls, bounded category and listing
   queries, status failure independence, cursor tampering and no
   serialization of provider headers/credentials/raw errors.

#### [x] Work Item 5 quality gate — mandatory before Work Item 6

1. [x] Run focused API/read-adapter tests, then the **full zero-warning/error
   solution build** and **full passing solution test suite** using the Work
   Item 1 gate commands.
2. [x] Resolve all warnings/errors and failed tests, rerun both commands;
   record SDK, warning/error count, per-project totals and protected
   SQL-only/response-shape evidence before continuing.

**Work Item 5 gate evidence:** SDK `10.0.302`; `dotnet build
.\TNC.Trading.Platform.slnx --verbosity minimal` succeeded with 0 warnings and
0 errors. Focused Application (6), API mapping (5), Viewer/Operator auth (3),
and market-detail SQL integration (13) tests passed. The first full test run
exposed a one-second AppHost readiness-test deadline that could expire under
parallel suite load; the test deadline was raised to five seconds, the test
passed in isolation, and the required final build and
`dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
passed all 945 tests (Application 221, Infrastructure unit 262,
Infrastructure integration 71, API unit 90, API integration 29, Web unit 176,
Web functional 13, Web E2E 13, AppHost unit 44, Architecture integration 13).
SQL integration and API mapping evidence verifies environment-scoped current
membership, stale-version and missing-membership outcomes, `NotCollected`,
last-good/provenance/timestamp shape, compact batched availability, and
Viewer-protected reads without provider calls.

### [x] Work Item 6 — Deliver Saved Market Details UX

**Objective:** Make full saved details discoverable without overloading the
50-tab listing, implying live prices or triggering a provider call.

#### [x] Tasks and steps

1. [x] Extend category page with compact **Market details coverage** and
   last-complete time (distinct from listing outcome and quota); add category
   detail coverage in the instrument page header, and only a per-tab
   `Complete / Out of date / Not collected / Excluded` line, detail
   retrieval time and `View saved market details` link. Preserve existing
   listing fields, snapshot disclaimer, pagination, tab keyboard behavior
   and presenter-selected EPIC. Do not render 50 nested details or add N+1
   Web/API/EF reads; a link works even for not-yet-collected EPICs.
2. [x] Add `MarketDetails.razor`/scoped CSS and dedicated presenter/view
   model in `Components/Pages/MarketCategories/`, with stable deep link
   `/market-categories/{categoryCode}/instruments/{epic}/market-details`.
   Use `PlatformApiClient` delegated Viewer-scope GET, one lazy detail GET
   per selected EPIC, saved-data Reload, back breadcrumb with in-memory
   selected tab/page preservation when available and direct-URL fallback.
   Cancel/ignore stale loads on route/parameter change and disposal.
3. [x] Show provenance/coverage first; historical quote snapshot (status,
   bid/offer, high/low, changes, delay, provider time and optional factors)
   clearly **not live**; instrument identity/terms/capability flags; each
   dealing rule's value and own unit; margin factor/unit and nullable
   premium/slippage; every ordered currency, margin band and provider
   notice; optional opening/expiry/rollover and collapsed technical source.
   Reuse native panel/Radzen responsive layout and
   `PlatformAccordionSection`, semantic `<dl>`, `<time>`, and responsive
   `<table>` with `<th scope="col">`. `max: null` is `No upper bound`;
   absent optional values are `Not supplied by IG`, genuine zero is `0`.
   Escape `specialInfo`, avoid derived FX/pip math, do not conflate
   listing/detail quote times, and retain original provider text.
4. [x] Label actual UTC retrieval or configured trading-schedule zone
   **with offset/abbreviation** consistently with existing instrument
   timestamps; never use Blazor Server `ToLocalTime()` as a claimed viewer
   zone or convert unzoned provider `updateTime`. Show text as well as
   color for status, legible light/dark contrast, mobile overflow/stacked
   tables, accessible heading/focus/keyboard flow and no trade controls.
5. [x] Handle `NotCollected`, partial-but-individually-complete, active,
   out-of-date with last good, budget/schedule failure, confirmed exclusion,
   not-followed category, changed listing version (`409` with reload),
   `404`, auth and unsupported environment distinctly. A detail error uses
   `role="alert"`; independent status metadata failure uses
   `role="status"` while keeping an already displayed detail. No timer
   polling or "Refresh from IG". If history is implemented, collapse it,
   page on SQL server and label each historical selection explicitly.
6. [x] Extend `MarketCategoryInstrumentsTests.cs` and
   `PlatformApiClientTests.cs` and add focused detail bUnit tests for
   null/zero/unit, multi-currency and unbounded bands, escaped notices,
   version conflict, rapid route changes, back navigation, accessibility/
   mobile layout, status failure, snapshot disclaimer and **one selected
   details GET rather than 50**. Add controlled functional/E2E coverage
   of protected deep links and no read-triggered IG call.

#### [x] Work Item 6 quality gate — mandatory before Work Item 7

1. [x] Run focused Web/bUnit/client and affected functional/E2E tests, then
   the **full zero-warning/error solution build** and **full passing solution
   test suite** using the Work Item 1 gate commands.
2. [x] Resolve all warnings/errors and failing tests (including UI and
   infrastructure fixtures), rerun both full commands and record SDK,
   warning/error count, per-project totals, one-GET and no-provider-read
   evidence before continuing.

**Work Item 6 gate evidence:** SDK `10.0.302`; the full solution build
`dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` succeeded with
0 warnings and 0 errors. Focused Web/client coverage passed (47 tests), and
the authenticated Viewer deep-link functional test passed (1 test). The
one-selected-EPIC bUnit assertion verifies one detail GET, and the real-runtime
functional test verifies the controlled IG provider received no request.
After the build, the full solution test command
`dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
passed all 941 tests: Application unit 221, Infrastructure unit 262,
Infrastructure integration 71, API unit 90, API integration 29, Web unit 184,
Web functional 14, Web E2E 13, AppHost unit 44, Architecture integration 13.

### [ ] Work Item 7 — Update Wiki, Capacity Evidence and Acceptance

**Objective:** Align durable guidance with implemented behavior and prove
complete coverage and safe failure modes before marking delivery complete.

#### [ ] Tasks and steps

1. [x] Update `docs/wiki/application-overview.md`,
   `architecture.md`, `runtime-behavior.md`,
   `ig-day-trading-with-ig-apis.md`, `api-reference.md`,
   `operator-guide.md`, and `testing-and-quality.md` as affected:
   endpoint/version/fallback, completeness vs individual state, source
   provenance, per-slot resumption, cross-replica limits and daily quota,
   applied environment, Viewer API, page states, historical retention,
   retirement, audit-safe exclusion/reinstatement, storage monitoring and
   fixture-based validation. Reference existing top-level `docs/`
   developer-run guidance; do not duplicate it in a work package. Keep
   `docs/wiki/` as the implemented-source-of-truth before completion.
2. [ ] Exercise representative large selected-category fixtures in SQL/
   fake IG to measure `U`, category page counts, actual session/fallback/
   retry cost, per-account/app rate, slot time remaining, migration/index
   write cost, history query latency and storage growth. Require the
   configured approved daily allowance and active window to cover the
   measured **full** eligible universe at the configured cadence, or
   explicitly expose a capacity-blocked/incomplete operational state;
   never claim guaranteed coverage from the 15,000 listing cap or the
   theoretical 50-per-batch maximum.
3. [ ] Verify end-to-end controlled category-first, duplicate membership,
   full run vs individual success, empty validated run, failure/resume/
   window-close, stale lease and revision changes, retirement/history,
   Viewer SQL-only reads, stored terms and UI provenance. No real IG
   credentials in automated tests. Confirm applicable market-data/API
   agreement permits intended long-lived internal retention and any
   planned redistribution **before** production use; absent confirmation,
   record a release/compliance blocker, not assumed permission.

**Work Item 7 evidence and blockers:** The seven affected wiki pages now
describe the delivered v2 `filter=ALL` contract, applied-environment
Viewer-only SQL reads, individual versus aggregate completeness, provenance,
slot resumption, shared daily and rolling limits, UI states, retirement,
history retention, monitoring, and fixture coverage. A new 15,000-target
capacity-policy test passes and records 300 first-attempt market batches,
600 no-retry requests including sessions, and a 2,700-request bounded
three-attempt/401-replay worst case. At 30 account requests/minute, the
theoretical lower bound is 20 minutes without retries and 90 minutes in the
worst case, excluding listing calls, actual spacing, SQL work, and slot
closure. These are policy calculations, not end-to-end SQL/provider-double
capacity measurements. Full-universe SQL write/history latency, migration
write cost, storage growth, and live configured allowance/window sufficiency
remain unmeasured. Also, no owner confirmation of the applicable IG
market-data/API terms for indefinite internal retention or redistribution is
available in this task. These are explicit release blockers; do not infer
capacity or contractual permission from passing fixtures.

#### [ ] Work Item 7 final quality gate — release blocker

1. [x] Validate links/Markdown if repository tooling exists and run affected
   focused controlled tests after the last edits.
2. [x] Run `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal`
   with **zero warnings and errors**, resolving all warnings (including
   baseline warnings) rather than waiving them.
3. [x] Run `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
   with **every project passing**. Investigate and fix every failure so
   application functionality remains consistent with this plan/research;
   rerun both full commands after changes. Docker-backed SQL/Aspire failures
   are not passes or acceptable skips.
4. [ ] Record SDK, exact commands, warning/error counts, per-project totals,
   capacity/retention-compliance evidence and results of each previous gate;
   do not mark complete with an unverified fixture, unexamined failure or
   unresolved contractual blocker.

**Work Item 7 technical gate evidence:** SDK `10.0.302`. The final solution
build succeeded with 0 warnings and 0 errors. The final full solution test
run passed all 942 tests: Application unit 222, Infrastructure unit 262,
Infrastructure integration 71, API unit 90, API integration 29, Web unit 184,
Web functional 14, Web E2E 13, AppHost unit 44, Architecture integration 13.
The focused 15,000-target capacity policy tests passed (3 total tests in the
class). Relative Markdown links in the seven updated wiki pages all resolve;
no separate Markdown lint tool was present. Docker-backed SQL and Aspire
tests passed. Item 4 remains unchecked: full-universe SQL capacity measurements
and confirmation of the applicable IG retention/redistribution terms are
release blockers, so this work item and its final quality gate are not
certified complete.

## Wiki-Impact Assessment by Work Item

This is a planning document; **no wiki page changes now**. During delivery,
decide and document the following before each gate; do not publish
future-tense guidance as implemented behavior.

| Item | Wiki decision at implementation | Relevant pages |
| --- | --- | --- |
| 1 | No behavior change if only inward policy/fixtures; record verified provider shape when wired | `ig-day-trading-with-ig-apis.md`, `testing-and-quality.md` |
| 2 | Update architecture, history/retention, retirement and audit/exclusion model when SQL ships | `architecture.md`, `runtime-behavior.md`, `ig-day-trading-with-ig-apis.md` |
| 3 | Document auth/version, rate/allowance/failure behavior when provider adapter ships | `ig-day-trading-with-ig-apis.md`, `runtime-behavior.md`, `testing-and-quality.md` |
| 4 | Document slot/resume, stale leases, revision drift, audit-safe status and retention of partial successes | `runtime-behavior.md`, `architecture.md`, `operator-guide.md` |
| 5 | Document Viewer contract, response states, pagination and applied-environment behavior | `api-reference.md`, `architecture.md`, `operator-guide.md` |
| 6 | Document saved page, warnings, time labels, back navigation and accessibility | `operator-guide.md`, `application-overview.md`, `testing-and-quality.md` |
| 7 | Reconcile all prior decisions with shipped behavior, retention/audit and monitoring; verify navigation | All listed pages |

## Acceptance Criteria

* A due, supported applied-environment slot with validated category/listing
  prerequisites stages all distinct eligible selected EPICs and their
  one-to-many source memberships; a complete run has an observation for
  every eligible target **in that run**, and an excluded target has
  provider-confirmed evidence. A missing target or failed prerequisite
  cannot produce `Complete`.
* Valid individual details remain inspectable even during incomplete,
  superseded and later failed runs; old latest-good values are marked out
  of date where required. A subsequent complete-run analysis cannot mix
  different slots' latest-good projections. History outlives removal and
  retirement.
* Every IG HTTP attempt is lease/schedule/profile-gated and counted once
  against shared daily allowance and cross-replica/account/app rate limits.
  Restart, window close, quota, endpoint change, cancellation and retries
  cannot cause unbudgeted, duplicate-success or out-of-hours observations.
* Protected Viewer pages/API read saved SQL only: genuine absent current
  membership is `404`, stale optional listing version is `409`, known
  uncollected EPIC is typed `200`; details load once for the chosen EPIC.
  Details and listings carry distinct UTC source times, nullable vs zero
  and each dealing-rule unit, with no live/trading implication or escaped
  provider text rendered as markup.
* Additive migration, controlled IG/SQL/worker/API/Web tests, capacity
  evidence and implemented wiki guidance ship together. **Each item**
  ends with a zero-warning full solution build and a passing full solution
  test run, with failures investigated and fixed before advancing.

## Implementation Sequencing and Dependencies

Execute **1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7**. Every unchecked quality gate
is a hard prerequisite for the next item. If the contract check shows v2
`ALL` is incomplete, implement only the documented, tested single-market
fallback and prove capacity before enabling full collection; if that is
also infeasible, stop with explicit blocked status and do not certify a
complete run. If tooling/dependency failures prevent a full build or suite,
diagnose and repair them before proceeding; report blockers rather than
checking the gate. Use the repository's `global.json` SDK, existing scripts
or tasks where available, and Docker-backed test infrastructure; do not
introduce a new framework target or unrelated behavior. This plan does
not request an additional `docs/00x-work/` package.

## Planning Record

* Created: 2026-09-25.
* Scope of this session: this Markdown plan only; no application code,
  tests, schema, wiki or provider calls changed or run.
* Basis: [research](../../research/2026-09-25/market-details-research.md);
  [previous plan](../2026-09-24/market-category-instruments-delivery-plan.instructions.md);
  [IG bulk endpoint](https://labs.ig.com/reference/markets.html);
  [IG single endpoint](https://labs.ig.com/reference/markets-epic.html);
  [IG FAQ quota](https://labs.ig.com/faq.html).

## Execution Status — 2026-09-25

The requester supplied a `filter=ALL` response for
`CS.D.ADAUSD.CFD.IP`. It confirms the response's `marketDetails` array and
that this observed market includes `instrument`, `dealingRules`, and
`snapshot`, including nullable values, zero values, typed rule units, and
ordered nested terms. Together with the official references above, the
implementation will use the documented bulk v2 endpoint and reject
incomplete/mismatched target responses. The fixture covers one
`CURRENCIES` market only.

The requester then confirmed the Demo endpoint URL for that response and
provided a second Demo request for two EPICs. Its HTTP 200 response contains
exactly two complete `marketDetails` entries matching the requested EPICs,
with the same required sections. The one-EPIC URL is 80 characters; the
two-EPIC encoded-comma URL is 101 characters. Based on IG's documented EPIC
character/length pattern and 50-EPIC maximum, the maximum encoded URL for
the documented query shape is 1,709 characters. The actual mixed-validity
multi-EPIC response has not been observed. The implementation therefore
validates every returned EPIC independently, fails any requested-but-missing,
duplicate, or unexpected EPIC, and never interprets a batch-level error as
an EPIC-specific exclusion. This preserves valid peers if IG returns partial
success and fails the whole batch safely if IG returns a request-level error.
No single-market fallback will be treated as verified. For markets not
represented by the supplied CURRENCIES fixtures, provider type values remain
opaque bounded strings; any missing required section/value fails that target
and prevents false aggregate completion.

The first Application contract/policy slice and its tests were added, along
with sanitized resource fixtures containing both supplied responses. Focused
tests passed (20 Application tests; 2 response-fixture tests). With SDK
10.0.302, `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal`
passed with 0 warnings and 0 errors; the full
`dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
suite passed all 871 tests (Application 212, Infrastructure unit 233,
Infrastructure integration 56, API unit 85, API integration 26, Web unit
176, Web functional 13, Web E2E 13, AppHost unit 44, Architecture 13).
Work Item 1 and its quality gate passed. Although provider mixed-validity
batch behavior was not observed, the chosen contract does not infer
EPIC-specific outcomes from batch failures, and exact per-EPIC response
validation prevents incomplete runs from being marked complete. Proceed to
Work Item 2.
