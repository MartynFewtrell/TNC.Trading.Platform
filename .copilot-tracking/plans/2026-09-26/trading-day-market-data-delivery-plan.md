# Delivery Plan: Trading-Day Market Data

## Purpose and Source

Deliver one broker-calendar-aligned Trading Day and one durable full-update
process for saved IG Market Categories, interested-category Instrument lists,
and details for every Instrument in those lists. This is a **plan only**:
creating it does not implement, build, or test the application.

Primary evidence: [application gap research](../../research/2026-09-26/trading-day-data-application-research.md),
supported by [user requirements](../../research/2026-09-26/trading-day-data-requirements.md)
and [current scheduled-process investigation](../../research/2026-09-26/scheduled-market-data-processes.md).
Follow the work-item, hard-gate, acceptance, and planning-record structure of
the [Market Category Instruments delivery plan](../2026-09-24/market-category-instruments-delivery-plan.instructions.md).
Where the older plan requires next-day frequency changes, a four-update cap,
no catch-up, or stopping an admitted run at closing, the newer requirements
explicitly supersede those rules for this delivery.

## Delivery Boundaries and Resolved Decisions

* In scope: the applied broker's trading calendar, one shared named-zone
  same-day market-data window, a composed application trading-state decision,
  immediate schedule changes, durable full-run admission and slot coverage,
  category/list/detail orchestration, item-level recovery, persisted last-good
  data and status, protected API/Web journeys, automated tests, and affected
  `docs/wiki/` pages.
* Out of scope: order execution changes beyond consuming the authoritative
  state where applicable, individual Instrument-interest flags, historic
  update browsing, new price streaming, analytics, and replay of missed
  previous-day slots. Retain existing immutable observations; only the latest
  available data needs browsing.
* The applied broker's configured trading calendar determines eligible days,
  including weekend and holiday exclusions. One effective shared start/end
  governs all market-data processes: initially 08:00–17:00 Europe/London,
  start-inclusive/end-exclusive, configurable within one local day. Reconcile
  existing platform and broker schedule settings explicitly, without silently
  replacing operator configuration or creating a competing market-data window.
* The shared decision distinguishes `TradingWindowOpen`, `CanTrade`, and
  `CanStartMarketDataUpdate` with safe reason codes and configuration identity;
  an admitted full run has separately fenced continuation authorization after
  closing. Market-data access is not order-placement permission. Zero timed
  updates does not disable manual or interest-triggered updates.
* Count defaults to one; nonnegative counts have no arbitrary maximum. Warn
  above four, never reject solely for exceeding four. Configurable lead-in
  defaults to 15 minutes; the failed-item-only follow-up is one attempt
  15 minutes after exhausting three per-cycle attempts.
* Existing approved request allowance defaults to unset and blocks provider
  collection. Keep that safety constraint explicit and give operators a
  visible way to configure sufficient per-environment/day allowance for the
  promised first automatic update; do not invent a default or bypass quota,
  IG throttling, endpoint validation, or protected credentials.

## Implementation Contract and Resolved Handoffs

The following are delivery decisions, not questions to hand to another
research phase. Inspect the named source files when changing them, but do
not defer these behavioral choices to implementation time.

### Effective Trading Day and configuration

* Use the **applied** `BrokerEnvironmentScheduleProfileEntity` as the
  authoritative calendar and named-zone, same-day hours for market data and
  trading-state decisions. Its weekdays, weekend behavior and excluded
  dates determine eligibility; selected-but-not-applied brokers have no
  effect until applied. Preserve the existing platform schedule's
  non-market-data purpose only where a consumer demonstrably needs it;
  remove it as an independent market-data or trading-capability gate. Route
  the Configuration editor's shared Trading Day changes to the applied
  broker profile, not two independently writable windows. Do not change
  an operator's existing profile automatically on migration: if the
  previously applied platform market-data schedule differs, surface a
  one-time legacy reconciliation requirement and block **new** market-data
  admission until the operator explicitly confirms/edits the applied
  broker's schedule. Record that reconciliation. Thereafter the legacy
  schedule is not compared as a second authority; editing the broker
  profile must not recreate a perpetual conflict.
  Initialize **new** profiles to 08:00–17:00 Europe/London on broker-calendar
  eligible days; retained existing profiles remain intact until reconciled.
* Version the effective schedule (calendar, zone, hours, count, lead-in and
  applied broker/profile identity) on every successful edit. Save a window
  or count change for immediate effect, not a pending next-day frequency.
  Persist count `>= 0` and lead-in `>= 0` minutes; validate start < end,
  valid named time zone, an eligible-day calendar and a same-day window.
  A zero lead-in disables near-future coverage, not timed/manual collection.
  Above four: return a nonblocking warning on the edit and in its preview
  about IG quota/rate/capacity; do not impose the old 1..4 bound elsewhere.
  An unset/zero allowance blocks IG requests and is shown explicitly.
* One fresh typed evaluation (using the injected clock, applied broker ID,
  applied endpoint/profile, schedule version, allowance/readiness and
  operational constraints) returns window open, can trade, can start an
  update, reason codes, next opening/closing and next timed start. The
  `TradingScheduleGate` remains the time/calendar evaluator, not a second
  policy source. Unsafe or missing applied broker/profile, invalid zone,
  conflicting settings or insufficient approved allowance fails closed with
  a stable reason, not UTC fallback or a success-shaped default. `CanTrade`
  additionally requires trade readiness; market-data permission does not.
  Read models may display this freshly evaluated state but may not substitute
  a persisted "open" flag for admission. Use the same result in category,
  list, detail, manual, retry and trading-capability paths.

### Deterministic slots, admission and recovery

* For broker-local eligible date `D`, let `start` and `end` be wall-clock
  ticks and `N > 0`. Slot `i` (0-based, `0 <= i < N`) starts at
  `start + floor((end - start) * i / N)` local ticks. Resolve each local
  start to UTC: advance nonexistent clock values to the next valid instant,
  choose the **first** instant for ambiguous values. The identity is
  `(applied broker ID, D, schedule version, i)`; multiple starts resolving
  to one instant are covered by one admitted run, and a repeated clock hour
  does not start the same slot twice. Slots never start at `end`.
* Store coverage by slot identity **and** run ID. A manual, category-change
  or catch-up run started in `[nextStart - leadIn, nextStart)` covers that
  next slot; any run active at the slot instant covers it. A run started
  earlier and completed before lead-in is an extra run, not advance slot
  coverage. At an active-day late startup, coalesce all uncovered past
  starts into one catch-up; also cover the next slot if within lead-in.
  Do not enqueue another run for a covered slot. At closing, drop pending
  current-day starts and retries without creating a next-day catch-up.
  For `N = 0`, expose `nextScheduledStartUtc = null` even if a next opening
  exists for manual/interest admission.
* All four full-update triggers (`scheduled`, `catch-up`, `interest`,
  `manual`; changed schedule uses a coalesced `configuration` intent)
  enter one serializable SQL admission transaction and use one renewable,
  fenced lease per applied environment; an active lease wins over another
  replica or trigger. Persist immutable run ID, trigger, local trading
  date, admitted UTC instant, applied broker/endpoint, effective schedule
  version, interest revision and frozen selected codes. Record stage,
  item attempt, last-success, partial/final result, safe reason, covered
  slots and one coalesced reassessment intent. Count IG reservations
  against the applied broker/day **of admission** even after close. Keep
  transactions short; IG I/O occurs only after commit and never with locks
  held. On lease takeover, resume pending work under a new fence without
  redoing successful items or publishing from an old owner.
* On a schedule edit, immediately compare new due starts/coverage under
  the new version; while open admit one needed full run, subject to the
  active lease. While a run is active, capture only the latest config and
  interest revision as one reassessment intent. After it finishes, if the
  new effective window is still open **and** newly required slot or
  interested data was not covered, admit at most one further full run with
  the latest settings/selection. Otherwise clear/defer the intent until
  next eligible opening as appropriate; no one-run-per-edit replay.
  Interest removal alone does not initiate a new data refresh. On a crash,
  durable intent and covered slots survive. A manual click during a run
  reports its run ID and does **not** create a second intent.
* A run that was admitted while open carries a fenced continuation
  permission; closing time alone must not cancel its IG requests or commit.
  Re-evaluate applied broker, supported profile, protected credentials,
  lease fence, allowance and explicit safety stops at each request and
  publication. Shutdown/lost lease stops work for safe resumption; changed
  schedule or interest does **not** invalidate an already admitted run's
  frozen sources. A *new* full run or failed-item follow-up must pass
  fresh admission before closing, even if an old run is still finishing.

### Stage, fallback and status contract

* In the run, try a validated Category refresh and finish its attempts
  before lists. A failed Category refresh leaves the last-good catalogue
  visible and attempts selected known Categories against validated saved
  lists; it does not claim a fresh catalogue. Complete each interested
  Category's listing attempts independently before starting details.
  Each successful listing freezes its complete validated EPIC membership;
  a failed listing falls back only to its last complete validated listing
  in that applied broker/endpoint, clearly marked stale. Deduplicate EPICs
  across interested Categories for provider requests but retain all their
  category memberships and distinct list/detail status. Never substitute
  a partially fetched page or a different applied environment.
* Existing `EfMarketDetailListingSourceReader` currently insists on
  same-slot, current-revision complete sources for **every** selected
  Category; replace that all-or-nothing gate with the admitted run's frozen
  per-Category fresh-or-fallback sources. Do not mark a stale fallback as
  same-slot fresh, or let a new interest/catalogue revision supersede the
  admitted run. A missing last-good source produces a failed/missing item,
  but other valid Categories still progress. No interested Categories
  means zero listing and detail IG requests, with both stages explicitly
  empty. If the full run fails any required Category/list/detail item,
  its outcome is `Partial`, even if fallback details succeed. A hard
  admission failure with no run is `Blocked`, not a completed empty update.
* Per item, retain at most **three attempts in the full run**, with short
  increasing waits (use the existing category 2-second then 5-second
  precedent); a hard safety block does not become an unlimited retry loop.
  When an item's third attempt fails, persist **one** failed-item-only
  attempt due 15 minutes after that failure; do not count it as a fourth
  attempt of the full run. Admit follow-up only before close, recover just
  failed items and never repeat already successful Categories, lists or
  details. A follow-up success repairs that item's current freshness and
  status, but does not retroactively turn the recorded full-run `Partial`
  outcome into `Success`. A newly discovered EPIC after a failed-list
  follow-up waits for the next full update; do not silently expand the
  frozen detail universe. After a failed follow-up or window closure,
  leave the item stale until a future full run.
* Maintain independent last-good Catalogue, Category listing and EPIC
  detail pointers/timestamps. A failed refresh never destroys those
  pointers or historical observations. A deselected Category retains its
  last-good listing/details and is labelled `Historical`/stale; a
  provider-removed Category must be distinguishable from deselection
  without manufacturing current membership. Address
  `EfMarketCategorySnapshotStore` deletion of current rows and
  `EfMarketDetailReadStore` suppression of observations when membership
  is not current. Do not delete previously saved data merely to simplify
  the status projection. API read status reports both last-successful
  values and latest attempt/progress, with safe failure codes only.

### API, UI and verification shape

* Add a Viewer GET `/api/platform/market-data-updates/status` (under the
  existing platform route group) returning at least: applied broker ID/
  environment, effective calendar/zone/start/end and revision; evaluated
  `tradingWindowOpen`, `canTrade`, `canStartMarketDataUpdate` and safe
  reason; `nextScheduledStartUtc` nullable; current run ID/trigger/stage
  and completed/expected/failed counts; last full-run ID/outcome/time;
  last successful Category/list/detail UTC times; per-Category
  stale/failed/historical state, counts, safe reason and eligible
  follow-up time where relevant. Put per-EPIC status on the existing
  **paged** saved-list and detail reads, never return an unbounded
  15,000-item status payload.
  Reads use SQL plus the shared in-process decision and make **zero IG
  calls**. Extend existing saved-list/detail responses rather than
  replacing their protected cursor/paging contract.
* Add an Operator POST `/api/platform/market-data-updates` with no provider
  work inside the HTTP request: on admission return **202 Accepted** with
  `runId` and status URL; when already running return **200 OK** with
  `runId`, `state = Running`, no queued request; outside eligible Trading
  Day return **409 Conflict** with reason `TradingWindowClosed` or
  `CalendarDayIneligible`; invalid editor input returns **400** validation
  details; unsupported applied environment or unsafe configuration returns
  **503**, and exhausted/missing approved allowance returns **429**, all
  with safe reason codes and no IG data. Preserve existing Viewer/Operator
  authorization responses and OpenAPI metadata. Keep or deprecate the
  legacy `/api/platform/market-categories/refresh` route explicitly: for
  compatibility, map it to this full-run admission, document its changed
  202/200 behavior, and update its callers/tests. It must never
  independently mutate the catalogue while a full run is active.
* Use the established `PlatformApiClient`, Configuration editor and
  Market Categories pages. Show the evaluated state and a true next timed
  start (or "Timed updates off"), not `NextWakeUpUtc`. Keep already saved
  values visible with UTC last-success and IG-local quote-time provenance.
  Gate manual action by role and admission; render a readable block reason
  and an in-progress acknowledgement, not just a disabled button.
* For each work item, put requirement-traceable test names/comments into
  the corresponding existing test projects. Assert concrete **status
  shape, call count, persisted slot/run keys, frozen membership and exact
  UTC instants**, not only boolean due/not-due results. Use fake time and
  controlled IG responses; SQL concurrency needs the real fixture-owned
  SQL integration suite. All gates include the full solution build and
  complete solution test run, irrespective of focused-test success.

## Non-negotiable Behaviour

1. One full update attempts, in order, Categories, Instrument lists for the
   captured interested Categories, then details for all their Instruments.
   Complete each stage's attempts before the next; no selection means
   Categories only and explicitly empty subsequent stages. All required
   items succeeding means success; otherwise record partial/failed with
   item-level reasons and preserve last-good values.
2. Scheduled starts partition the local window into equal slots, beginning
   at opening and never closing. Three slots at 08:00–17:00 begin at 08:00,
   11:00, and 14:00. A nonexistent local time moves to the next valid local
   time; a repeated time executes once, at its first occurrence. Slot
   identity and durable coverage prevent DST/restart/replica duplication.
3. Late startup during an eligible day admits one uncovered catch-up; a
   start within lead-in covers the next slot and a running update covers any
   slot that arrives during it. Manual/interest updates obey the same
   coverage rule but otherwise remain extra. Never replay previous days;
   zero-count startup outside the window waits for an eligible manual or
   interest trigger. Recompute immediately on window/calendar/count edits.
4. All triggers use one SQL-backed admission boundary and cannot overlap.
   Interest/settings edits during a run leave its captured selection/settings
   intact; coalesce repeated edits into at most one necessary further full
   run, admitted only within the current Trading Day. An already-running
   manual request reports in progress without queuing a duplicate.
5. An admitted run can continue IG requests and publication after close,
   provided its fenced lease, applied environment, safety policy, allowance,
   and shutdown checks remain valid. No *new* full update or failed-item
   follow-up starts outside the Trading Day. No provider I/O inside long SQL
   transactions, and no Viewer GET causes provider I/O.
6. Retry each failing Category/list/Instrument independently up to three
   total attempts per full run with short increasing waits. Continue other
   items; if a category/list refresh fails, use its last validated list for
   detail collection if available and label it stale, without claiming newly
   unseen Instruments were covered. Persist one failed-item-only follow-up
   attempt due 15 minutes later, admit it only before closing, cancel pending
   work at close, and leave persistent failures stale until the next full run.
7. Keep latest saved data and per-section/item last-successful timestamps
   visible during work, failure, restarts, and deselection; unselected
   Categories' saved Instruments remain browseable as historical/stale and
   are not refreshed. Distinguish no data, partial, stale, and historical;
   present a true next scheduled start or no timed update, not a polling tick.

## Reusable Precedents and Change Locations

| Boundary | Starting points and intended work |
| --- | --- |
| Application | `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs`, `Configuration/AppliedBrokerEnvironmentContext.cs`, `Features/MarketCategoryInstruments/MarketCategoryInstrumentSchedulePolicy.cs`, existing category/list/detail coordinators: compose shared state, slot policy, full-run orchestration and reasoned outcomes. |
| Settings and broker calendar | Platform configuration handlers and SQL store, broker environment schedule profiles: align effective calendar/window, immediate versioned count/lead-in, validation and capability reporting. |
| SQL and provider | Infrastructure EF cycle, snapshot, listing-source, detail-run and budget stores plus IG gateways: fenced durable run/coverage/recovery, last-good provenance, safe continuation and existing throttle/allowance. |
| Host and API | `src/TNC.Trading.Platform.Api/Hosting/MarketCategoryInstrumentCollector.cs` and `Features/Platform/MarketCategoryInstrumentEndpoints.cs`: one worker lifecycle, thin protected manual/status endpoints; address the legacy category-only POST without letting it disrupt an active full run. |
| Web | `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/`, `Components/Pages/Configuration/`, `PlatformApiClient.cs`: status panel, manual action, interest feedback, saved-data freshness and shared-window editor. |
| Tests and docs | Existing Application, Infrastructure SQL/unit, API, Web/bUnit and Aspire/E2E projects under `test/`; affected architecture, runtime, operator, API and testing pages under `docs/wiki/`. |

### Concrete change checklist

* **Effective calendar and editor:** update
  `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs`,
  `Configuration/TradingScheduleConfiguration.cs`, the broker environment
  profile read/write contracts and
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlBrokerEnvironmentCatalogService.cs`.
  Add the authorized broker-profile edit operation needed by Configuration;
  preserve applied-versus-selected semantics. Update
  `SqlPlatformConfigurationStore.cs`,
  `PlatformConfigurationBootstrapParser.cs` and existing seeded
  broker defaults together so new records have the new hours/zone without
  rewriting old ones. Change the existing platform configuration GET/PUT
  DTOs, validation, handler and Web form mapper in the same work item so
  clients cannot continue writing an independent market-data window.
* **Count/lead-in migration:** remove the pending-next-day path in
  `Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentFrequency.cs`
  and `Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs`;
  preserve and migrate an existing pending value explicitly, making the
  currently effective value authoritative until the operator saves a new
  value. Update the API and Application validators, Web numeric input/mapper,
  `EfMarketCategoryInstrumentFrequencyStore.cs`,
  `Entities/InstrumentCollectionSettingsEntity.cs`,
  `MarketCategoryInstrumentSchedulePolicy.cs` and the SQL model/migration.
  In `PlatformDbContext.cs`, remove 1..4 frequency and 0..3 slot check
  constraints for cycle/category attempt/collection run **and detail-run
  tables**, replacing them with nonnegative slot and frequency rules
  suitable for count zero (which has no timed slot). Revise keyed stage
  stores and status DTOs that assume a slot always exists; do not encode
  manual runs as a fabricated slot 0. Verify the migration against both a
  fresh database and one with existing cycles/history.
* **One run:** integrate `MarketCategoryInstrumentCycleCoordinator.cs`,
  `MarketDetailCollectionCoordinator.cs`,
  `Api/Hosting/MarketCategoryInstrumentCollector.cs` and the EF cycle,
  detail-run and listing-source stores under the new full-run key.
  Existing separate same-slot leases and the detail prerequisite "all
  listings current from this slot" must no longer determine whether a
  valid fallback category can proceed. Retain existing complete IG
  category/list/detail gateway validation and throttle, rather than
  implementing another provider client.
* **After-close fencing:** audit and change *all* current window-deadline
  enforcement points together:
  `MarketCategoryInstrumentScheduleGuard.cs`,
  `MarketCategoryInstrumentCycleCoordinator.cs`,
  `MarketDetailCollectionCoordinator.cs`,
  `EfMarketCategoryInstrumentRequestBudget.cs`,
  `EfMarketDetailRequestBudget.cs`,
  `EfMarketCategorySnapshotStore.cs` and
  `EfMarketCategoryInstrumentSnapshotStore.cs`. Ensure provider gateway
  budget/context checks accept only a live, fenced admitted-run token
  after close; a manual/failed-only request cannot reuse it. Retain
  in-window checks on **admission**, and test both before- and after-close
  publication, allowance exhaustion, shutdown and lease loss.
* **Saved data and status:** update
  `EfMarketCategorySnapshotStore.cs` (currently removes current rows for
  vanished Categories), `EfMarketDetailListingSourceReader.cs` (currently
  requires same-slot sources from every selected Category) and
  `EfMarketDetailReadStore.cs` (currently hides detail observations without
  current membership). Keep historical observations immutable. Adapt
  `GetMarketCategoryInstrumentStatusHandler.cs` and
  `Api/Features/Platform/MarketCategoryInstrumentStatusResponse.cs`;
  keep current instrument cursor/data-protection semantics when exposing
  item freshness on existing pages.
* **Endpoint and client:** add mappings in
  `src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentEndpoints.cs`
  under the existing `/api/platform` group in `PlatformEndpoints.cs`;
  update `RefreshMarketCategoriesManuallyHandler.cs` and
  `UpdateMarketCategoryInterestHandler.cs` to use the common admission/
  durable intent. Wire the new ports through existing API/Infrastructure DI.
  Update `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` and the
  `MarketCategories/` and `Configuration/` components. Include viewer
  versus operator, accepted/running/conflict/blocked responses, no-IG GETs
  and accessible stale/historical presentation in tests.
* **Test destinations:** add focused fake-clock tests beside
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/MarketCategoryInstruments/MarketCategoryInstrumentPolicyTests.cs`
  and `MarketCategoryInstrumentCycleCoordinatorTests.cs`, plus existing
  `Features/MarketDetails/MarketDetailCollectionCoordinatorTests.cs`.
  Exercise SQL leases/migrations in
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/MarketCategoryInstrumentSqlIntegrationTests.cs`
  and `MarketDetailSqlIntegrationTests.cs`; API role/status/manual paths in
  `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/MarketCategoryInstrumentEndpointTests.cs`
  and the API integration project; Web state in
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MarketCategoryInstrumentsTests.cs`
  and `MarketDetailsTests.cs`. Follow existing naming and test traceability
  comments. Use `docs/wiki/architecture.md`, `runtime-behavior.md`,
  `api-reference.md`, `operator-guide.md`,
  `ig-day-trading-with-ig-apis.md` and `testing-and-quality.md`.

### Acceptance fixture matrix

| Fixture | Expected persisted state, calls and displayed result |
| --- | --- |
| London eligible day 08:00–17:00, count 3 | Starts exactly 08:00, 11:00 and 14:00 local, never 17:00; each uncovered slot has one coverage record. |
| Weekend/holiday exclusion or missing applied profile | No new admission or IG calls; stable blocked reason and next eligible start, if one exists. |
| Count 0 with manual request/interest add at 10:00 | No timed slot/next timed start; one full run admitted per eligible trigger, same three-stage order. |
| 10:46 manual request, next slot 11:00, lead-in 15 minutes | One run and slot 11:00 covered; 10:44 request finishing before 10:45 remains extra and 11:00 stays due. |
| Late startup at 13:00 with 08:00 and 11:00 uncovered | One catch-up starts promptly and covers both, not two requests; no previous-day replay on startup after close. |
| One run still active at 11:00 or second replica/manual click | The slot is covered by the active run; other replica and manual click get the same running run ID, no second provider collection. |
| Interest/count/window edited repeatedly mid-run | Frozen run finishes; exactly zero or one additional needed in-window run using the latest revisions; none begins outside the new window. |
| Spring missing hour / autumn repeated hour | Missing slot starts at first valid instant; repeated slot starts at first occurrence only and retains one durable coverage record. |
| Category/list failure and one EPIC failure | Other lists/details run; old validated list used only with stale provenance; three in-run attempts for failed items, partial run and one due failed-only attempt per exhausted item. |
| Third failed attempt near closing | Follow-up admitted only if its due instant is before close; pending attempt at/after close is cancelled; admitted full run alone may continue past close. |
| All interests removed / never collected / previously interested | Categories-only full run, no list/detail IG calls; explicit no-data vs historical label and persisted last-success for saved data. |
| Restart during an incomplete run, then lease expiry | New fence resumes unfinished items once; old owner cannot commit, no repeated successful item or budget reservation, last-good data remains visible. |

## Work Items

### [x] Work Item 1 — Compose the Shared Trading State and Schedule

**Objective:** Give every consumer one reasoned, testable view of broker-day
eligibility, trade readiness, market-data admission and future slot starts.

#### [x] Tasks and steps

1. [x] Define typed state/clock/configuration contracts and one application
   decision service over the applied broker calendar, shared named-zone
   same-day hours, operational trading readiness, market-data access and
   explicit block reasons. Wire existing gates, collectors, guards, and
   relevant trading-capability consumers to the same evaluated policy without
   conflating `CanTrade` with market-data admission.
2. [x] Reconcile broker/platform schedule storage, defaults, migrations,
   bootstrap and configuration APIs. Make count zero or greater immediately
   effective, lead-in configurable (15-minute default), and >4 a warning;
   validate time zone, same-day bounds and allowance independently.
3. [x] Compute equal starts and next eligible broker-calendar opening with
   UTC conversion and durable-friendly day/slot identities; explicitly test
   08:00/11:00/14:00, zero, holidays/weekends, both DST transitions, and
   immediately changed calendar/window/count. Do not cache an expired
   time-sensitive permission.

#### [x] Work Item 1 quality gate — mandatory before Work Item 2

1. [x] Run focused schedule, configuration and trading-state tests, including
   divergent `CanTrade`/`CanStartMarketDataUpdate` cases.
2. [x] Run a **full** `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal`
   with zero warnings/errors and a **complete**
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
   with every project passing. Investigate every warning and test failure,
   remediate the underlying cause while preserving application design, then
   rerun both full commands; do not advance on a failing or skipped gate.
3. [x] Record SDK, commands, warning/error counts, per-project test results
   and any blocker beneath this gate. **Only after all exit checks pass, mark
   the completed tasks, this gate and Work Item 1 `[x]`.**

**Gate evidence:** SDK `10.0.302`. Focused trading-state tests passed (6/6),
including divergent trade/market-data readiness, zero timed updates, applied
broker/profile identity, and next-window/holiday boundaries. Full build:
`dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` — 0 warnings,
0 errors. Full tests:
`dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal -m:1`
— 983 passed, 0 failed, 0 skipped across API Integration (29), API Unit (94),
AppHost Unit (44), Application Unit (246), Architecture Integration (13),
Infrastructure Integration (79), Infrastructure Unit (262), Web E2E (13),
Web Functional (14), and Web Unit (189). `git diff --check` passed.

### [x] Work Item 2 — Persist Full-Run Admission, Coverage and Last-Good State

**Objective:** Make trigger admission, progress, slot coverage and resume
safe across restarts and multiple API instances.

#### [x] Tasks and steps

1. [x] Add additive SQL schema/migrations for a single environment-scoped
   full-run identity, trigger, configuration/interest versions, frozen
   selection, stage/item attempts, safe failures, last-success timestamps,
   covered slots and a fenced renewable lease; preserve existing immutable
   observations and last-good pointers.
2. [x] Atomically admit scheduled, catch-up, manual and interest-change
   triggers; transactionally record interest-change intent with the interest
   revision. Mark slots covered by lead-in starts or active runs without a
   duplicate queue. Coalesce repeated settings/interest edits and distinguish
   resume of an interrupted run from new collection.
3. [x] Fence publications and allowance reservations by lease, applied
   environment, day of run admission and source revision; keep provider I/O
   outside SQL locks. Resume incomplete stages without replaying successful
   items or double-spending allowance. Make legacy category-only refresh
   serialize with the full run or clearly replace it by the full-update path.
4. [x] Exercise SQL migration/readback, two-replica races, lease expiry and
   takeover, restart, source changes, budget contention and covered-slot
   deduplication in fixture-owned SQL integration tests.

#### [x] Work Item 2 quality gate — mandatory before Work Item 3

1. [x] Run focused SQL migration, admission, lease, interest-revision and
   concurrency tests; check EF model/migration synchronization.
2. [x] Run the **full** zero-warning/error
   `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` and
   **complete** passing
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`.
   Examine and remediate all warnings, build errors and test failures without
   compromising the design; rerun both full commands after fixes.
3. [x] Record SDK, commands, warning/error counts and per-project totals.
   **Only after the exit gate passes, mark completed tasks, gate and Work
   Item 2 `[x]`; otherwise block Work Item 3.**

**Gate evidence:** SDK `10.0.302`. Focused parent-run/detail SQL suite:
`dotnet test .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~MarketDataFullRunSqlIntegrationTests|FullyQualifiedName~MarketDetailSqlIntegrationTests" --verbosity minimal -m:1`
— 24 passed, 0 failed, 0 skipped. This fixture-owned SQL coverage includes
migration/readback, concurrent admission, frozen-source/profile changes,
interest/configuration intent coalescing, lease takeover and progress,
slot coverage, budget contention, category/list/detail fencing, and
category-only publication exclusion. EF synchronization:
`dotnet ef migrations has-pending-model-changes --project .\src\TNC.Trading.Platform.Infrastructure --startup-project .\src\TNC.Trading.Platform.Api`
— no pending model changes (EF tools 10.0.5; runtime 10.0.12).

Full build:
`dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` — 0 warnings,
0 errors. Full tests:
`dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal -m:1`
— 994 passed, 0 failed, 0 skipped across API Integration (29), API Unit
(94), AppHost Unit (44), Application Unit (246), Architecture Integration
(13), Infrastructure Integration (90), Infrastructure Unit (262), Web E2E
(13), Web Functional (14), and Web Unit (189). `git diff --check` passed.

### [x] Work Item 3 — Orchestrate Scheduled and Triggered Full Updates

**Objective:** Run Categories, interested-category listings and all their
Instrument details as one non-overlapping, auditable lifecycle.

#### [x] Tasks and steps

1. [x] Adapt the existing single hosted loop to re-evaluate shared state and
   due coverage at startup, opening, slots and immediately on configuration
   edits; admit one current-day catch-up only when needed. Keep the API
   responsive and use durable state rather than host-local flags.
2. [x] At admission freeze interest/settings. Refresh Categories first,
   intersect frozen interests with refreshed current Categories, then
   complete selected listings before detail attempts; freeze validated EPIC
   membership. Make zero selected Categories an explicit Categories-only
   successful path. Distinguish all-success from partial item failures.
3. [x] Carry run-scoped fenced continuation after closing through guards,
   IG calls, request budget and snapshot publication. Retain cancellation
   on shutdown, lost lease, unsafe applied-environment/profile changes and
   explicit safety stops. Replace time-before-close capacity estimates that
   would incorrectly terminate an already admitted run.
4. [x] Verify slots covered while running, manual/interest lead-in coverage,
   no-overlap, changed settings/interest coalescence, count-zero triggers,
   start-after-close rejection and finish-after-close permission with fake
   clock plus host/SQL integration tests.

#### [x] Work Item 3 quality gate — mandatory before Work Item 4

1. [x] Run focused worker/coordinator and SQL lifecycle tests across every
   trigger and boundary.
2. [x] Run the **full** zero-warning/error
   `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` and
   **complete** passing
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`.
   Investigate and remediate warnings/errors/failures consistent with the
   intended design; rerun both full commands after changes.
3. [x] Record SDK, commands, counts and per-project results; **mark tasks,
   gate and Work Item 3 `[x]` only once the exit gate passes**. Block the
   next item otherwise.

**Gate evidence:** SDK `10.0.302`. Focused coordinator/detail tests:
`dotnet test .\test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~MarketCategoryInstrumentCycleCoordinatorTests|FullyQualifiedName~MarketDetailCollectionCoordinatorTests" --verbosity minimal -m:1`
— 12 passed, 0 failed, 0 skipped. Hosted-worker handoff:
`dotnet test .\test\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.UnitTests\TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~MarketCategoryInstrumentCollectorTests" --verbosity minimal -m:1`
— 1 passed, 0 failed, 0 skipped. Full-run SQL lifecycle:
`dotnet test .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~MarketDataFullRunSqlIntegrationTests" --verbosity minimal -m:1`
— 11 passed, 0 failed, 0 skipped, including duplicate scheduled-slot
suppression and endpoint-profile replacement coverage.

EF synchronization:
`dotnet ef migrations has-pending-model-changes --project .\src\TNC.Trading.Platform.Infrastructure --startup-project .\src\TNC.Trading.Platform.Api`
— no pending model changes (EF tools 10.0.5; runtime 10.0.12).
`git diff --check` passed.

Full build:
`dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal -m:1`
— 0 warnings, 0 errors. Full tests:
`dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal -m:1`
— 998 passed, 0 failed, 0 skipped across API Integration (29), API Unit
(94), AppHost Unit (44), Application Unit (250), Architecture Integration
(13), Infrastructure Integration (90), Infrastructure Unit (262), Web E2E
(13), Web Functional (14), and Web Unit (189). Runtime behavior wiki updated
for shared parent-lease continuation and profile-scoped slot coverage.

### [ ] Work Item 4 — Recover Individual Failures and Preserve Freshness

**Objective:** Keep collecting useful data after isolated failures while
truthfully representing old sources and limiting recovery requests.

#### [ ] Tasks and steps

1. [ ] Retry each failed Category/list/detail item at most three total
   attempts in a full run, with short increasing waits; continue independent
   items and preserve their successful results. Use last validated catalogue
   and listing as fallback after source failure, labeling provenance/staleness
   and collecting details only for known validated EPICs.
2. [ ] Persist per-item attempt/last-success/stale/failed state and exactly
   one failed-item-only follow-up due 15 minutes after the third failed
   attempt. Admit that follow-up only before close; do not restart Categories
   or successful items, cancel pending follow-ups at closing, and do not
   convert a recovery into a full update. Retain old data after repeated
   failure or category deselection.
3. [ ] Ensure safe quota/rate-limit accounting across all stages, retries
   and post-close continuation, without weakening existing IG validation,
   explicit unsupported-environment/allowance errors, or protected logging.
   Verify restart persistence and distinctions between provider removal,
   deselection, empty selection and never collected.

#### [ ] Work Item 4 quality gate — mandatory before Work Item 5

1. [ ] Run focused stage failure, stale fallback, allowance and follow-up
   timing tests, including a closing boundary and restart/replica cases.
2. [ ] Run the **full** zero-warning/error
   `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` and
   **complete** passing
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`.
   Examine and remediate all warnings/errors and every failing test without
   violating the design, then rerun both commands.
3. [ ] Record SDK, commands, counts and per-project results; **only after
   the exit gate passes, mark tasks, gate and Work Item 4 `[x]`**.

**Gate evidence (implementation scope still open):** SDK `10.0.302`.
`dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal -m:1` passed
with 0 warnings and 0 errors. `dotnet test .\TNC.Trading.Platform.slnx
--no-build --verbosity minimal` passed 1,004 tests across the solution:
Application Unit Tests 254, API Unit Tests 94, Web Unit Tests 189,
Infrastructure Unit Tests 262, AppHost Unit Tests 44, Architecture
Integration Tests 13, API Integration Tests 29, Infrastructure Integration
Tests 92, Web Functional Tests 14, and Web E2E Tests 13.

Focused recovery checks also passed: Application coordinator/capacity
regressions 19/19 and durable SQL follow-up persistence/restart/close
regression 1/1. `dotnet ef migrations has-pending-model-changes` reported no
pending model changes (EF tools 10.0.5, runtime 10.0.12), and
`git diff --check` passed. The full gate is not checked: the durable
failed-item follow-up currently retries exhausted detail EPIC targets only
and skips Categories and Listings. Resolve whether the plan requires failed
listing-category items to be included, and extend the follow-up if required,
before marking Work Item 4 complete.

### [ ] Work Item 5 — Expose Protected Full-Update and Status APIs

**Objective:** Give operators an explicit manual request and Viewers a
stable SQL-only picture of the current/last run and next scheduled start.

#### [ ] Tasks and steps

1. [ ] Add an Operator-only manual full-update operation using shared
   admission: accepted response with run/status reference, already-running
   result without queuing, and explicit out-of-window, invalid settings,
   unavailable environment and budget/authorization outcomes with safe
   reason codes. Recheck authorization and admission at request time.
2. [ ] Add Viewer-authorized saved-data/status reads with nullable *true*
   next scheduled start (null when count zero), calendar/window state,
   current stage and progress, latest full result, per-item freshness and
   last-success timestamps. Distinguish a polling wake-up from an update.
   Keep GETs SQL-only and redact IG secrets, raw diagnostics and quotes.
3. [ ] Align configuration DTOs, authorization, Problem Details and OpenAPI
   metadata; migrate or clearly distinguish the legacy category-only POST
   so it cannot mutate a full run's sources. Test response shapes, role
   boundaries, no-provider-read behavior and running/out-of-window races.

#### [ ] Work Item 5 quality gate — mandatory before Work Item 6

1. [ ] Run focused Application/API unit and integration tests of status,
   manual admission, safety and legacy endpoint behavior.
2. [ ] Run the **full** zero-warning/error
   `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` and
   **complete** passing
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`.
   Diagnose and remediate warnings/errors/failing tests while preserving
   authorization and runtime design, then rerun both full commands.
3. [ ] Record SDK, commands, counts and per-project results; **mark tasks,
   gate and Work Item 5 `[x]` only after the exit gate passes**.

**Gate evidence:** Pending implementation; no checks run for this plan.

### [ ] Work Item 6 — Deliver Trading-Day Configuration and Saved-Data UX

**Objective:** Make current state, manual action, saved data and recovery
understandable without a browser-to-IG request.

#### [ ] Tasks and steps

1. [ ] Extend Configuration to show the applied broker calendar, one shared
   08:00–17:00 Europe/London initial window (editable), zero-or-more timed
   updates, lead-in, slot preview/next eligible start, explicit allowance
   requirement and a nonblocking >4 warning; show field-level validation.
2. [ ] Add a Market data update panel showing shared open/closed state and
   reason, separate trading capability, running stage/progress, last full
   result, last-successful times, next eligible start or "Timed updates off".
   Show "No data yet" before first success; enable Operator manual full
   update only when eligible and show "Update in progress" instead of a
   duplicate action. Preserve last-good data during refresh/failure.
3. [ ] On Category, Instrument and detail pages show distinct current,
   updating, stale, failed and historical states with per-item/section last
   success; explain interest-change admitted/coalesced/deferred outcome,
   retry time, and retained unselected data. Keep existing non-live quote
   provenance and Viewer/Operator permissions; no historic-run browser.
4. [ ] Add documented requirement-traceable bUnit/client tests for empty
   state, roles, stale fallback, zero count, excluded days, active updates,
   failed-item labels, restart-read persistence and accessible status text.

#### [ ] Work Item 6 quality gate — mandatory before Work Item 7

1. [ ] Run focused Web/bUnit and API-client tests; exercise browser-only
   interactions with the existing functional/E2E harness when necessary.
2. [ ] Run the **full** zero-warning/error
   `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal` and
   **complete** passing
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`.
   Investigate and remediate warnings/errors and all failing tests without
   hiding a design regression; rerun both commands after fixes.
3. [ ] Record SDK, commands, counts and per-project results; **only after
   the exit gate passes, mark tasks, gate and Work Item 6 `[x]`**.

**Gate evidence:** Pending implementation; no checks run for this plan.

### [ ] Work Item 7 — Document and Prove End-to-End Acceptance

**Objective:** Align durable operator/developer guidance and verify the
entire scheduled, manual and recovery journey before release.

#### [ ] Tasks and steps

1. [ ] Update affected `docs/wiki/` architecture, runtime, API, operator,
   IG market-data and testing guidance to explain the aligned broker
   calendar, shared state versus trade permission, defaults/configuration,
   quota, triggering and lead-in, continuation after close, fallback, failed
   item recovery, roles and retained data. Reference the top-level `docs/`
   developer run guidance rather than duplicating commands in work packages.
2. [ ] Run fake-clock/SQL/host integration scenarios for 08:00/11:00/14:00,
   excluded days, DST, zero, late startup, lead-in and overlapping slots,
   repeated edits, interrupted run/second replica, before/after close,
   source and EPIC failures, no interests, partial retry, historical reads
   and no-data UI. Check no extra provider calls, correct durable coverage,
   source/version freshness and safe reason codes, not just test pass counts.
3. [ ] Review migration synchronization, full API/Web role behavior and
   real infrastructure fixture results. Do not claim controlled IG, Docker,
   Aspire or browser verification that was not actually executed; investigate
   and remediate failing fixtures before declaring the release gate.
4. [ ] Record SDK, exact commands, zero-warning/error build, complete
   per-project suite counts, environment/setup, and any remaining blockers.
   All affected wiki pages must reflect implemented behavior **before**
   marking this numbered delivery plan complete.

#### [ ] Work Item 7 final quality gate — release blocker

1. [ ] Run focused acceptance/integration checks and available documentation
   checks after the final edits.
2. [ ] Run a **full** `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal`
   with **zero warnings and errors**, then a **complete**
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
   with **all test projects passing**. Examine and remediate every warning,
   build error and test failure in a design-preserving manner; rerun both
   full commands after any remediation. Do not waive Docker/Aspire or SQL
   test failures as "environmental" without diagnosing and resolving them.
3. [ ] Review recorded evidence for Work Items 1–6, wiki accuracy and
   acceptance scenarios. **At the end of this exit gate, mark each actually
   completed task and gate `[x]`, then Work Item 7 `[x]` and the plan complete;
   leave any unverified item unchecked and block release.**

**Gate evidence:** Pending implementation; no checks run for this plan.

## Acceptance Criteria

* Every collection trigger and status consumer uses the same broker-aligned
  trading-state decision; trade readiness and market-data admission remain
  distinct. No new **run or follow-up** is admitted on an ineligible day or
  after close, but a safely admitted full run can make provider calls and
  finish after close.
* Slot starts, catch-up, lead-in and running-slot coverage produce at most
  one necessary non-overlapping full run; count zero disables only timed
  starts. Calendar/count/time edits and interest changes apply promptly
  without redundant runs across restart, replica or DST boundaries.
* Full runs attempt all three required stages in order; independently
  failing items do not block others. Partial and fallback provenance remain
  honest; a single eligible failed-item-only retry does not repeat successes.
  Budget and rate safety are enforced across stages and continuation.
* SQL persists current/last run, coverage, latest good values and
  last-successful timestamps; deselected Categories remain browseable as
  historical. Viewer reads never call IG, and Operator manual updates never
  queue a duplicate or bypass the Trading Day.
* UI distinguishes no data, zero timed starts, running, partial, stale and
  historical states; shows a true next start, when one exists. Updated wiki,
  migration, focused tests, **zero-warning full build and passing complete
  solution test suite at every work-item exit gate** ship together.

## Implementation Sequencing and Dependencies

Execute in order: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7. Every work-item exit gate
is a hard prerequisite for the next. Record evidence **after** the focused
tests and full build/test suite, remediate and rerun on any warning or
failure, then check off only the items genuinely completed. Use the
repository-pinned SDK from `global.json`; do not change target frameworks
just to deliver this feature. With no implementation in this planning
session, all checkboxes intentionally remain open.

## Planning Record

* Created: 2026-09-26.
* Scope: this Markdown plan only; no application source, migration,
  configuration, tests, runtime or wiki files were changed.
* Evidence: [application research](../../research/2026-09-26/trading-day-data-application-research.md);
  [requirements](../../research/2026-09-26/trading-day-data-requirements.md);
  [scheduled-process investigation](../../research/2026-09-26/scheduled-market-data-processes.md);
  [format precedent](../2026-09-24/market-category-instruments-delivery-plan.instructions.md).
* Execution status: not started. Build, test and exit-gate results belong
  to future implementation, not this plan.
