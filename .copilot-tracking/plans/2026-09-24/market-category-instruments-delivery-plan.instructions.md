---
applyTo: 'src/**/*.cs, src/**/*.csproj, src/**/*.razor, src/**/*.json, test/**/*.cs, test/**/*.csproj, docs/wiki/**/*.md'
---
<!-- markdownlint-disable-file -->

# Delivery Plan: IG Market Category Instruments

## Purpose and Source

Deliver saved, browsable IG instruments for each Market Category, together
with operator-managed category interest and a schedule-gated, application-owned
collector. Collect complete, validated observations for future analysis as
well as a current SQL snapshot for the UI. This is a **plan only**; no
implementation or tests are performed by creating it.

Primary evidence:
[Market Category Instruments research](../../research/2026-09-24/market-category-instruments-research.md).
Follow the work-item and hard-gate structure of the
[earlier Market Categories plan](../2026-09-23/market-categories-delivery-plan.instructions.md).
The research's remaining product choices are resolved below for this delivery;
do not defer them to the implementing agent or substitute a current-only cache.

## Delivery Boundaries and Resolved Decisions

* In scope: a `Features/MarketCategoryInstruments` Application slice, a
  complete-page IG adapter, environment-scoped SQL interests/configuration/
  cycle state/current snapshots/immutable observations, a nonblocking API
  worker, protected API and Web journeys, tests, and updated wiki guidance.
  The existing manual **category** refresh remains, subject to the schedule
  and serialization rules below.
* Out of scope: on-demand/manual **instrument** refresh, browser/provider
  calls, user-selected individual instruments, trading, recommendations,
  derived analytics, streaming prices, and retroactive backfill.
* A new category starts unchecked. Interest is a shared environment-scoped
  preference; a removed category's selection becomes dormant, is visible in
  status, and becomes eligible again only if the category reappears. Only
  current categories may be newly selected. Selecting never triggers IG.
* Require **every** provider page and a consistent complete total. Failed or
  incomplete collections create a gap/failed slot record, never an analysis-
  eligible run; reject a zero-result refresh unless the current IG contract
  explicitly confirms zero is a valid *complete* response. Keep last-good
  snapshot and timestamp on any failure.
* Display saved bid/offer as **snapshot values**, labelled with the platform's
  UTC retrieval time. Label provider `updateTime` as IG-supplied local text;
  never present either as a real-time streaming price.
* `InstrumentUpdatesPerDay` defaults to **1** per active trading day.
  Persist a positive integer with an initial **maximum of 4**, but allow
  values above 1 only when an environment-specific, operator-provided
  non-trading request allowance and measured selected-category page counts
  prove sufficient headroom. If allowance/capacity is absent, pause provider
  collection with explicit configuration status rather than guessing quota;
  do not silently use a default allowance. Revalidate capacity whenever
  interest, frequency, or page counts change. Saving frequency takes effect
  from the next local Trading-schedule day; saving interest takes effect at
  the next eligible slot. Configuration saves may occur outside hours.
* Start with **indefinite online retention in SQL Server** for complete
  observations, including removed categories; no automatic purge, archive,
  or 90-day inherited retention. The primary SQL store is the retained
  analysis source; archive/export destination is **none** in this delivery
  and deletion verification is **not applicable** because no rows are
  deleted. Monitor row growth/storage and expose it operationally. A later
  versioned archival policy with destination, verification and a sufficiently
  long analysis window must precede any retention deletion. Do not apply
  existing operational-event retention jobs to this history.
* Support each *configured and provider-supported* applied IG environment
  independently, including a future Live endpoint profile if provisioned;
  never enable Live trading. Fail closed on absent/unsupported endpoint,
  credentials or environment. Do not silently use Demo credentials or URL for
  another environment. Resolve the **applied** environment at cycle and
  commit time; do not collect selected-but-not-applied environments.
* Use SQL keyset cursor paging, default page size **50**, allowed range
  **1..100**, by ordinal EPIC within a **versioned** saved snapshot. Encode
  environment/category/snapshot-version/last-EPIC in an opaque protected
  cursor; reject invalid, cross-environment/category, or stale cursors with
  validation/conflict Problem Details rather than mixing snapshots. First
  page has no cursor; expose `nextCursor` (nullable at the end). The API is
  forward-only; the Web page retains previously visited cursors in its local
  navigation stack to provide Back. A never-collected category differs from a
  valid complete zero set.
* Use a bounded provider page size compatible with the current IG reference
  (start with **150**), sequential pages with hard caps of **100 pages** and
  **15,000 results** per category/collection to cap memory and requests.
  If the provider exceeds a cap, fail without replacing the snapshot and
  report a safe operational status. The IG Labs endpoint is documented as
  `GET /categories/{categoryId}/instruments` v1 with `pageNumber` and
  `pageSize`; validate the first-page numbering convention and maximum against
  the official IG reference and controlled gateway fixtures before coding
  the paging loop. Do not infer zero/one-based numbering from the supplied
  sample; fail closed if the documented contract differs. This is a bounded
  contract verification, not an open-ended design/research task.
* Use one in-window category-refresh prerequisite per environment/day/slot;
  a failed prerequisite may retry **at most twice** with backoff while active.
  Each category/slot may retry **at most twice** after a failed attempt.
  Count all category, session and instrument HTTP requests against a shared
  durable per-environment trading-day allowance, reserving headroom for
  retries and other app traffic; never treat frequency as an HTTP-call quota.
  On `429`/insufficient budget pause safely and surface status. No unlimited
  `401` replay, page fan-out, or out-of-window retry.

## Non-negotiable Behaviour

1. All Viewer GETs, page navigation and interest/configuration writes use
   saved SQL only; only the worker or the schedule-gated existing Operator
   category refresh invokes IG. Never call IG merely to render or read.
2. Use `TradingScheduleGate.Evaluate` and `GetTradingDay` with the applied
   environment's active schedule: configured time zone, active days, weekends,
   holiday exclusions and start-inclusive/end-exclusive hours. Split **only
   the active window** into N predictable approximately even slots. Use a
   time abstraction; track UTC event times separately from local day/slot.
3. At startup after migrations/bootstrap, an independent worker checks the
   current due slot without blocking API readiness; while running it wakes
   for later slots/opening. Outside the window, disabled schedule, invalid
   setting or unsupported applied context: **zero provider calls**. No
   after-hours catch-up or burst after restart. Missed slots are recorded as
   gaps; completed ones are not replayed.
4. In each due cycle, acquire a durable SQL environment/day/slot lease,
   recheck the schedule and allowance, refresh and fully validate categories
   **first**, persist success for that cycle, then load the current interest
   intersection. With no selection, refresh categories only. For each
   eligible category reserve its own slot and retrieve **all** pages
   sequentially; independent categories may continue after one fails.
5. Check schedule/lease/environment before every provider call, after every
   response and immediately before **and within** the final short SQL
   transaction; enforce an in-window deadline and cancel on close, schedule
   change, shutdown or environment change. Roll back if commit cannot finish
   inside the window. Discard partial results without overwriting last-good
   data. No network I/O while an EF transaction is open.
6. Use unique successful `(BrokerEnvironmentId, CategoryCode, TradingDay,
   ScheduledSlot)` run keys and fenced SQL leases so crashes, replicas and
   restarts cannot publish two successful observations. A lost lease cannot
   commit. Persist redacted attempts/outcomes and gap reasons separately
   from immutable successful runs.
7. Category replacement must diff by code in one transaction: update retained
   category flags, insert new codes, remove current instrument rows/states
   only for codes no longer current, then delete those categories; leave
   separate interest rows dormant and historical runs/observations intact.
   Serialize manual category refresh with cycle refresh/collection, or fence
   instrument publication against a catalogue revision and current
   membership. Manual POST returns `409` outside the active schedule and
   performs no provider I/O or write; recheck before commit.
8. Keep credentials, provider wire DTOs, session headers, raw bodies and IG
   diagnostics inside Infrastructure. Application holds framework-neutral
   records, policies and typed safe outcomes; endpoints stay thin. Provider
   failure/telemetry must not include secrets or raw quotes. No global
   Demo-only branch or silent Demo fallback in the new path.

## Reusable Precedents and Change Locations

| Boundary | Concrete starting point and intended change |
| --- | --- |
| Application | `src/TNC.Trading.Platform.Application/Features/MarketCategories/`, `Services/TradingScheduleGate.cs`; add `Features/MarketCategoryInstruments/`, extend category refresh schedule policy and re-use schedule evaluator |
| Applied environment | `Infrastructure/Configuration/SqlServer/SqlAppliedBrokerEnvironmentContextResolver.cs` currently regards Demo as executable; support explicit configured IG endpoint profiles and ensure active profile/credentials are resolved consistently |
| IG integration | `Infrastructure/Integrations/Ig/IgMarketCategoriesGateway.cs` currently has a Demo-only guard and Demo fallback; `IgAccountDetailsGateway.cs` provides session/401 precedent; create dedicated instrument gateway, fix category gateway for supported applied environments |
| Persistence | `Infrastructure/Persistence/EntityFramework/EfMarketCategorySnapshotStore.cs` currently deletes/reinserts all rows; extend `PlatformDbContext.cs`, new entities/adapters, additive migration and model snapshot |
| Configuration | `Application/Configuration/PlatformConfigurationSnapshot.cs`, `Features/UpdatePlatformConfiguration/`, `Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs`, and broker schedule profile; persist independent applied-environment frequency/effective-day and allowance, preserve existing update semantics |
| Worker/composition | `Api/Program.cs`, `Api/Hosting/`, `Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs`, `Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs`; start independent worker after `InitializePlatformAsync`, not in blocking auth reconciliation |
| API | `Api/Features/Platform/PlatformEndpoints.cs`, `MarketCategoriesEndpointMapping.cs`, configuration mappings; existing Viewer/Operator policies and Problem Details |
| Web | `Web/Components/Pages/MarketCategories/`, `Web/Components/Pages/Configuration/`, `Web/PlatformApiClient.cs`, layout navigation and presenter conventions; add instruments page |
| Tests | Application, Infrastructure unit/SQL integration, API unit/integration, Web unit/bUnit, AppHost and Web functional/E2E projects under `test/` |

## Work Items

### [x] Work Item 1 — Define the Inward Contracts and Schedule Policy

**Objective:** Make the lifecycle, time semantics and failure modes testable
without SQL, IG HTTP or ASP.NET dependencies.

#### [x] Tasks and steps

1. [x] Add inward instrument, complete collection, current snapshot, run
   provenance and typed safe failure records; keep one top-level C# type per
   file and file-scoped namespaces.
2. [x] Define `IMarketCategoryInstrumentsGateway`,
   `IMarketCategoryInstrumentSnapshotStore`, interest/frequency reader-writer
   and cycle coordinator/clock ports. Separate SQL-only paged read from
   scheduled command; no GET handler dependency on provider gateway.
3. [x] Implement due-slot calculation with `TradingScheduleGate`, applied
   environment, injected `TimeProvider`, local trading day and N evenly spaced
   active-window slots; model next-day effective frequency and missed slots.
   Inactive, holiday, weekend, DST transition and changed-schedule decisions
   must be explicit; never derive a UTC-midnight budget key.
4. [x] Implement cycle orchestration: category-first prerequisite, selected
   current-category intersection, no-selection idle, bounded retry/allowance,
   per-category independence, safe failures and cancellation. Make read-only
   query, configuration/interest mutation and scheduled collection distinct.
5. [x] Unit-test every transition, failed prerequisite, no-selection, dormant
   category, failure preservation, slot boundaries/DST, mid-cycle closure and
   repeat/startup decisions. Add required XML requirement-traceability
   comments and MethodName_StateUnderTest_ExpectedResult test names.

#### [x] Work Item 1 quality gate — mandatory before Work Item 2

1. [x] Run focused Application tests, then `dotnet build TNC.Trading.Platform.slnx`
   with **zero warnings and errors**, then `dotnet test TNC.Trading.Platform.slnx`
   with **every** test passing.
2. [x] Investigate and resolve all build warnings and failed tests, rerun both
   commands, and record SDK, exact commands, warning count, test totals and
   any diagnosed baseline issue. No waivers or later-item work while failing.

**Gate evidence:** SDK `10.0.302`; focused Application feature tests passed
(`dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~MarketCategoryInstrumentPolicyTests"`);
`dotnet build TNC.Trading.Platform.slnx` passed with 0 warnings and 0 errors;
`dotnet test TNC.Trading.Platform.slnx` passed with 717 tests across all
projects (0 failed, 0 skipped). The initial solution build identified 11
pre-existing analyzer/nullability warnings; these were resolved and both full
solution gates were rerun successfully.

### [x] Work Item 2 — Persist Interests, Frequency and Complete Observations

**Objective:** Apply one additive EF Core migration and make all SQL
operations environment-scoped, transactional, versioned and restart-safe.

#### [x] Tasks and steps

1. [x] Extend `PlatformDbContext` and source-controlled migration/model snapshot
   with `MarketCategoryInterestStates` (environment PK/FK, revision),
   `MarketCategoryInterests` (environment/code PK, `SelectedAtUtc`, no mutable
   category FK), `InstrumentCollectionSettings` (environment PK/FK,
   current frequency, pending frequency, pending effective local day, approved
   non-trading daily request allowance), `InstrumentCollectionCycleStates`
   (environment/local trading day/slot PK, schedule revision, category
   prerequisite, outcome, lease owner/fence/expiry, used request budget),
   `InstrumentCollectionCategoryAttempts` (environment/day/slot/category PK,
   attempts, state, lease fence, safe error and UTC time), per-category
   current catalogue state (including monotonically increasing snapshot
   version) and current
   instruments keyed `(environment, category, epic)`, immutable collection runs
   and observations keyed `(collectionId, epic)`.
2. [x] Constrain required strings from IG contract and boundary tests; use
   explicit decimal precision, nullable optional market values, checked
   epoch conversion and `bigint` popularity. Keep a composite restrictive
   FK from current category states to `MarketCategories` and from current
   instruments to their category state. Historical runs reference broker
   environment, **not** mutable current category rows. Unique successful
   category/day/slot; index category/EPIC reads and observation analysis by
   environment, category, EPIC and collection time. Use an ordinal-compatible
   binary SQL collation for stored EPIC sort/predicate, rather than relying
   on a case-insensitive default collation to implement an ordinal cursor.
3. [x] Persist each complete run's environment, endpoint profile, category,
   local trading day/slot, UTC time, page count, result count, completeness
   evidence and quality status. Reject partial or duplicate success. In one
   short transaction append immutable run/observations, replace that
   category's current projection and timestamp; on failure update only
   attempt/cycle status, never last-success data.
4. [x] Implement compare-and-update of the **entire** interest set under an
   environment-level revision; stale revision -> conflict even for an
   unchecked/missing row. Selecting absent categories fails validation;
   removal leaves dormant preferences. Initialize default frequency once per
   environment; do not silently default corrupt/missing runtime settings.
   Store pending next-day frequency separately so the current day retains
   its prior slots/budget.
5. [x] Replace delete/reinsert in `EfMarketCategorySnapshotStore` with the
   transactional category diff described above. Serialize/fence category
   replacement against in-flight instrument publication and verify membership
   and revision under the same transaction before committing it.
6. [x] Add SQL-backed cycle lease with expiry, fencing token and recovery,
   atomic category-slot reservation, durable HTTP allowance accounting and
   idempotent success. No race can double-spend allowance or publish duplicate
   runs; record retries, gaps and safe errors. Do not reuse retention cleanup
   for observations.
7. [x] SQL integration tests on fixture-owned SQL Server: migration constraints,
   readback in a fresh context, rollback on failed page/commit, snapshot
   replacement and history preservation, environment separation, revision
   conflicts, removed/reappeared categories, duplicate-run race, expired
   lease/fencing and retained observation history.

#### [x] Work Item 2 quality gate — mandatory before Work Item 3

1. [x] Run focused Infrastructure unit and SQL integration tests, including
   migration and concurrency cases.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` (**zero warnings/errors**)
   and `dotnet test TNC.Trading.Platform.slnx` (**all pass**). Repair and rerun
   every warning/failure; record commands, SDK, counts and totals. Block Work
   Item 3 until successful.

**Gate evidence:** SDK `10.0.302`. Focused checks:
`dotnet test test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --verbosity minimal`
(161 passed) and
`dotnet test test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --verbosity minimal`
(52 passed); `dotnet ef migrations has-pending-model-changes --project src\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.csproj --startup-project src\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.csproj`
reported no pending model changes. `dotnet build TNC.Trading.Platform.slnx`
passed with 0 warnings and 0 errors; `dotnet test TNC.Trading.Platform.slnx`
passed with 730 tests (0 failed, 0 skipped). A persistence review found and
prompted fixes for concurrent page-version consistency and durable category-
prerequisite error storage; focused SQL tests and all full-solution gates were
rerun successfully after those fixes. The EF CLI 10.0.5 emitted an older-than-
runtime (10.0.12) notice; migration/model validation and all gates still passed.

### [x] Work Item 3 — Complete the IG Provider Boundary

**Objective:** Resolve only the applied, supported IG endpoint and ingest
fully validated paginated results with a finite allowance.

#### [x] Tasks and steps

1. [x] Update applied-environment capability/endpoint resolution and the
   existing category gateway: eliminate Demo credential/URL fallback on the
   production path; support explicitly configured IG Demo/Live endpoint
   profiles without changing order/trading authorization. Fail closed on
   unknown/unsafe profile and missing protected credentials. Reuse the
   existing Data Protection and session patterns.
2. [x] Build a typed `IgMarketCategoryInstrumentsGateway`. Use IG session v2,
   the documented resource version (v1 if still supported), API key, CST,
   `X-SECURITY-TOKEN`, `Accept`, URI-escaped exact saved category code and
   documented `pageNumber`/`pageSize`. Check schedule/budget per page; request
   sequentially. On a resource `401`, obtain one new session and replay the
   failed page **once**; no other blind retry or page fan-out.
3. [x] Parse nullable private wire DTOs; verify page number/size, consistent
   total pages/results/category across pages, one visit per expected page,
   maximum pages/results and final count. Require nonblank, ordinal-unique
   EPIC and name, length-checked strings, valid decimal/range/expiry data;
   preserve optional quotes and provider update-time text. Fully validate
   before returning an inward collection; malformed/empty-by-default data
   cannot publish partial rows. Do not log raw payloads/headers/tokens.
4. [x] Count category, session and page calls in the shared durable allowance,
   enforce bounded page/result caps and throttling, and report a safe quota
   failure before exhaustion. Explicitly test the sample 384-result case at
   page size 10 (39 pages) as well as boundary metadata, 401 replay on a
   middle page and restart/closure during paging.
5. [x] Add controlled-handler tests for URL/method/version/headers/profile,
   missing unsupported profile, Demo/Live isolation, every malformed case,
   cancellation, timeout, 429, rejection and redaction; no test contacts IG.

#### [x] Work Item 3 quality gate — mandatory before Work Item 4

1. [x] Run focused gateway and category-store regression tests.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` (**zero warnings/errors**)
   and `dotnet test TNC.Trading.Platform.slnx` (**all pass**); diagnose,
   repair, rerun and record SDK, commands, warning count and totals. Block
   Work Item 4 until successful.

**Gate evidence:** SDK `10.0.302`. Rechecked the IG Labs category-instruments
reference: resource v1; `pageNumber` defaults to and starts at 0; `pageSize`
defaults to 150 and permits 1–1000. The reference page did not expose
`totalPages`/`totalResults`, so those response fields are validated against
the supplied research sample. Focused provider/category regression command:
`dotnet test test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --filter "FullyQualifiedName~IgMarketCategoryInstrumentsGatewayTests|FullyQualifiedName~IgMarketCategoriesGatewayTests|FullyQualifiedName~EfMarketCategorySnapshotStoreTests" --verbosity minimal`
(79 passed). `dotnet build TNC.Trading.Platform.slnx --verbosity minimal`
passed with 0 warnings and 0 errors. `dotnet test TNC.Trading.Platform.slnx
--verbosity minimal` passed with 800 tests (0 failed, 0 skipped): Architecture
13; Application Unit 176; API Unit 79; Web Unit 154; Infrastructure Unit 230;
Infrastructure Integration 52; API Integration 26; AppHost Unit 44; Web E2E
13; Web Functional 13. Tests used controlled HTTP handlers only; no IG
requests or real credentials were used.

### [x] Work Item 4 — Run the Schedule-Gated Collector Safely

**Objective:** Start a nonblocking worker after SQL bootstrap and make
cycle/slot outcomes deterministic across shutdown, restart and replicas.

#### [x] Tasks and steps

1. [x] Register ports/adapters/handlers in the active composition roots.
   Add an API `BackgroundService` independent of the blocking authentication
   supervisor. It must run after `InitializePlatformAsync`, create fresh
   scopes, check the currently applied environment/schedule, sleep until the
   next opening/slot using injectable time and react to configuration changes
   without startup/readiness dependence on IG.
2. [x] Implement cycle/category leases, category-before-instruments success
   marker, bounded in-window retries/backoff, shared request budget, selected
   current-category intersection and independent category outcomes. At an
   empty selection record idle after successful category refresh; a new
   selection waits until the next due slot.
3. [x] Re-evaluate gate/lease/applied environment before/after calls and
   commit, cancel at window close and on shutdown; roll back failed/late SQL
   commits. Changes to schedule invalidate future due slots without
   out-of-hours replay; missing/corrupt frequency or quota yields an
   actionable safe paused status rather than success-shaped defaults.
4. [x] Expose SQL-backed progress, last category success, per-category last
   success, due/next slot, retries/failures/skips and gaps. Emit redacted
   structured counters/timings/page counts/collection IDs without raw
   provider content, tokens or diagnostic quotes.
5. [x] Test startup inside/outside hours, next opening without restart,
   multiple slots/time zones/DST, same-slot restart and competing workers,
   lost lease, category failure, category independence, quota exhaustion,
   near-close cancellation and no post-close provider-derived writes against
   controlled clock/gateway/SQL. Do not depend on real IG.

#### [x] Work Item 4 quality gate — mandatory before Work Item 5

1. [x] Run focused scheduler, API-hosting and SQL integration tests.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` (**zero warnings/errors**)
   and `dotnet test TNC.Trading.Platform.slnx` (**all pass**). Resolve every
   warning/failure, rerun and record the commands and totals; block Work Item
   5 until successful.

**Gate evidence:** SDK `10.0.302`. The API worker and cycle coordinator have
controlled-clock/gateway coverage; API-hosting and SQL integration tests ran
in the full suite. The focused Application gate
`dotnet test .\test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --verbosity minimal`
passed all 179 tests. The full build
`dotnet build .\TNC.Trading.Platform.slnx --no-restore --verbosity minimal`
passed with 0 warnings and 0 errors. The full test gate
`dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
passed all 806 tests (0 failed, 0 skipped): Architecture 13; Application
Unit 179; API Unit 80; Web Unit 154; Infrastructure Unit 230;
Infrastructure Integration 54; API Integration 26; AppHost Unit 44;
Web E2E 13; Web Functional 13. EF reported no pending model changes; the
instrument-observation feature is included in the single additive migration.
`git diff --check` passed. Late window-close cancellation records a skipped
cycle and does not publish provider-derived data.

### [x] Work Item 5 — Add Protected API Operations and Manual Gate

**Objective:** Keep transport thin and reads provider-free.

#### [x] Tasks and steps

1. [x] Extend Viewer `GET /api/platform/market-categories` with interest
   and per-category saved status plus interest-set revision. Add Viewer
   `GET /api/platform/market-categories/{categoryCode}/instruments` with
   validated keyset cursor/pageSize and Viewer
   `GET /api/platform/instrument-collection/status`; distinguish nonexistent
   category, never-collected and complete-empty states. No HTTP handler
   queries EF or calls IG directly.
2. [x] Add Operator `PUT /api/platform/market-categories/{categoryCode}/interest`
   with `{ interested, expectedRevision }`; validate exact current membership
   for adding, use transactional set-level CAS, return new revision or `409`
   on conflict. Extend existing Operator configuration GET/PUT mapping and
   validation for environment-scoped frequency, effective day and allowance/
   quota status, without resetting unrelated settings.
3. [x] Gate existing Operator
   `POST /api/platform/market-categories/refresh` before provider call and
   before publication using the same schedule and cycle serialization.
   Return safe `409` when inactive; preserve success and failure contracts
   for in-window operation. Interest/configuration writes are allowed when
   schedule is inactive and do not fetch categories/instruments.
4. [x] Map input validation to validation Problem Details, stale interest/
   cursor to `409`, unknown category to `404`, and provider-side typed
   failures to safe status/status telemetry. Retain existing Viewer/Operator
   policies, delegated auth/OIDC flow and OpenAPI conventions.
5. [x] API tests for paths, auth roles, response/cursor shape, revision
   conflicts, invalid settings/membership, manual inactive `409`, no IG
   calls on GET/PUT and redacted status; controlled SQL/fixture integration
   if supported by existing fixtures.

#### [x] Work Item 5 quality gate — mandatory before Work Item 6

1. [x] Run focused API and affected Application/Infrastructure tests.
   Evidence: API UnitTests 85 passed; Application UnitTests 191 passed;
   Infrastructure UnitTests 230 passed.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` (**zero warnings/errors**)
   and `dotnet test TNC.Trading.Platform.slnx` (**all pass**). Resolve,
   rerun and record failures/warnings and full totals; block Work Item 6
   until successful.
   Evidence: SDK 10.0.302; solution build 0 warnings/0 errors; full
   solution test run passed 822 tests across 10 projects (0 failed,
   0 skipped). Full test command used `--no-build` after the successful
   solution build. The added manual-refresh API mapping test also passed
   in the focused API rerun (85 API tests total).

### [x] Work Item 6 — Deliver Category Interest and Saved Instruments UX

**Objective:** Enable category click-through without tying browsing to
provider requests, and make acquisition state visible and accessible.

#### [x] Tasks and steps

1. [x] Extend `PlatformApiClient` and presenter/view models using existing
   delegated Viewer/Operator scopes, cancellation and safe error handling.
   Preserve visible saved data during errors or worker refresh. Keep role-
   gated edit controls behind the existing Operator context.
   Evidence: client-contract tests cover cursor encoding, interest/status
   requests and refresh/reload; presenters preserve already-loaded data on
   typed API failures.
2. [x] In `/market-categories`, link exact saved codes to
   `/market-categories/{categoryCode}/instruments`, show read-only Interested
   state to Viewers and accessible labelled checkboxes/save/confirmation to
   Operators. Handle one/many/all/none, saving/pending disablement, conflicts,
   dormant interest and per-category last collection/next slot. Retain
   `Non-tradeable` as a provider fact, not trading permission.
   Evidence: market-category bUnit tests cover code navigation, Viewer versus
   Operator presentation, interest state and safe failure handling.
3. [x] Add Viewer-protected instruments page with SQL-only paged loading,
   category code, last successful UTC retrieval, selected status/next slot,
   explicit never-collected/complete-empty/loading/failure/stale states,
   snapshot-price disclaimer and semantic accessible table (name, EPIC,
   type, expiry, bid, offer, market status, IG update time). Put lower-priority
   attributes in an accessible details view; support next/previous navigation
   consistently with the published cursor contract.
   Evidence: instrument-page tests cover SQL-backed paging, loading,
   never-collected/empty, stale cursor and retained saved results on metadata
   errors; API-client tests verify protected cursor requests.
4. [x] Add `InstrumentUpdatesPerDay` and the approved per-environment daily
   non-trading request allowance to the existing Operator Configuration UI;
   show default/effective-next-trading-day explanation and quota/capacity
   feedback. Do not request provider data on save, and use the full existing
   save/validation conventions. Without a configured allowance show a paused
   collector, not an implicit allowance.
   Show paused/failed collection status without leaking provider diagnostics.
   Evidence: configuration bUnit tests verify quota/pause presentation and
   frequency/allowance submission; Application regression tests retain valid
   frequency settings when status reads fail.
5. [x] Add Web/bUnit tests with XML traceability comments: Viewer vs Operator,
   links/code escaping, categories checked/unchecked/dormant, 409 edits,
   frequency editor, server paging, never-collected/empty, loading, stale
   retained saved results and safe errors. Add browser E2E only if a
   browser-only behaviour cannot be verified by bUnit.
   Evidence: focused Web/API-client and affected Application tests passed;
   the final Web UnitTests project contains 171 passing tests.

#### [x] Work Item 6 quality gate — mandatory before Work Item 7

1. [x] Run focused Web unit tests and API client-contract tests.
   Evidence: focused Web filter passed 39 tests; focused Application status
   regression filter passed 3 tests.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` (**zero warnings/errors**)
   and `dotnet test TNC.Trading.Platform.slnx` (**all pass**). Investigate,
   fix and rerun every failure or warning and record totals; block Work
   Item 7 until successful.
   Evidence: SDK 10.0.302; final solution build had 0 warnings and 0 errors.
   Final full test run passed 841 tests across 10 projects (0 failed,
   0 skipped); exact per-project totals are recorded under Work Item 7.

### [x] Work Item 7 — Update Documentation and Prove Acceptance

**Objective:** Bring durable operator guidance into line with automatic,
schedule-gated acquisition and verify end-to-end integrity.

#### [x] Tasks and steps

1. [x] Update `docs/wiki/ig-day-trading-with-ig-apis.md`,
   `runtime-behavior.md`, `architecture.md`, `api-reference.md`,
   `operator-guide.md`, and `testing-and-quality.md` for endpoint/version,
   quotas, startup/in-window lifecycle, environment support, schema,
   analytics provenance/indefinite history, category diff, role/paging/error
   contracts, time zones, frequency/interest, paused/gap status and
   operational monitoring. Remove now-outdated manual-only claims. Refer
   to top-level `docs/` developer run guidance instead of duplicating it.
   Evidence: all six required wiki pages plus `README.md` and
   `application-overview.md` were updated.
2. [x] Exercise acceptance using the available controlled test layers for
   the AppHost/API, IG collection and SQL persistence during and outside a
   window, including category-first, restart deduplication and protected
   SQL-only reads. If a fixture cannot run, repair the environment/test
   issue; do not declare its failure harmless. No real credentials or IG
   network access in automated tests.
   Evidence and limitation: fake-clock/fake-gateway coordinator and policy
   tests cover schedule eligibility, category planning, window-close safety
   and preservation of last-good data; SQL Server integration tests cover
   environment partitioning, transactional retention, durable leases,
   retries and competing writers; API endpoint tests cover protected route
   policies and saved-data contracts; AppHost/API integration suites passed.
   The repository does not contain one fixture that wires controlled IG,
   SQL Server and AppHost/API together in a single end-to-end run. Coverage
   is layered rather than one combined fixture; automated tests use no live
   IG credentials or network.
3. [x] Verify requirements: no provider GET/UI side effect, no partial
   provider sample, no lost prior snapshot, independent environment data,
   no duplicate complete runs, no off-hours/unsupported-environment fetch
   including manual category POST, redacted status, and observation retention.
   Evidence: coordinator/policy, API endpoint, IG gateway and SQL Server
   integration tests cover these controls; the final full suite passed.
4. [x] Record final SDK (pinned by `global.json`), exact focused/full commands,
   warning count, test project totals, SQL integration/AppHost results and
   any release-blocking discrepancies. All wiki pages affected by this
   delivery must be updated **before** this plan can be marked complete.
   Evidence: `global.json` resolves SDK 10.0.302. Focused commands:
   `dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj --no-build --filter "FullyQualifiedName~PlatformApiClientTests|FullyQualifiedName~MarketCategoryInstrumentsTests|FullyQualifiedName~ConfigurationTests" --verbosity minimal`
   (39 passed), and
   `dotnet test .\test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\TNC.Trading.Platform.Application.UnitTests.csproj --no-build --filter "FullyQualifiedName~UpdatePlatformInstrumentCollectionSettingsTests" --verbosity minimal`
   (3 passed). Final full commands:
   `dotnet build .\TNC.Trading.Platform.slnx --verbosity minimal`
   (0 warnings, 0 errors) and
   `dotnet test .\TNC.Trading.Platform.slnx --no-build --verbosity minimal`
   (841 passed, 0 failed, 0 skipped).

   | Test project | Passed |
   |---|---:|
   | Architecture.IntegrationTests | 13 |
   | AppHost.UnitTests | 44 |
   | Api.UnitTests | 85 |
   | Application.UnitTests | 192 |
   | Web.UnitTests | 171 |
   | Infrastructure.UnitTests | 230 |
   | Infrastructure.IntegrationTests (SQL Server) | 54 |
   | Web.E2ETests | 13 |
   | Web.FunctionalTests | 13 |
   | Api.IntegrationTests | 26 |

   `git diff --check` passed. No repository Markdown lint or link-validation
   command was found. EF model synchronization reported no pending model
   changes; the EF CLI emitted only its 10.0.5-versus-runtime-10.0.12 version
   advisory, not a build warning.

#### [x] Work Item 7 final quality gate — release blocker

1. [x] Run relevant documentation validation, if available, and affected
   focused tests after the final edits.
   Evidence: focused tests passed after the final code edits; `git diff
   --check` passed. No dedicated documentation validator is configured.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` with **zero warnings and
   errors**; resolve all warnings, not just those believed to be new.
   Evidence: final solution build completed with 0 warnings and 0 errors.
3. [x] Run `dotnet test TNC.Trading.Platform.slnx` with **every project
   passing**. Analyze and fix failing tests and application regressions,
   then rerun both commands. Do not mark complete with a failing Docker/
   Aspire fixture, unverified manual gate or an unresolved warning.
   Evidence: final full solution suite passed all 841 tests across 10
   projects; SQL Server integration, Web E2E/functional, API integration
   and AppHost unit projects all passed.
4. [x] Capture and review evidence for every previous work-item gate before
   declaring acceptance.
   Evidence: Work Items 1–5 gates and their totals remain recorded above;
   Work Items 6–7 final evidence is recorded here. The absence of a single
   combined AppHost/API + controlled IG + SQL fixture is disclosed above;
   no fixture failure or real-IG manual test is represented as passing.

## Acceptance Criteria

* An active in-window cycle refreshes categories first and ingests complete
  instruments only for selected, currently listed categories; off-hours,
  unsupported or misconfigured contexts never call IG. A fresh environment
  refreshes categories but has zero instrument calls until interest is saved.
* N daily slots are defined within the applied schedule's local active day;
  changes to interest/frequency, restarts, replicas, DST and window close do
  not create extra, duplicate or out-of-hours complete runs.
* A complete category observation is append-only, environment-scoped and
  time-stamped with metadata and all permitted values. Every read uses a
  saved, consistent SQL snapshot; failed/partial ingestion retains last-good
  data, and historical observations outlive category removal.
* Operators can change interest and frequency, Viewers can only browse;
  neither UI nor API reads/writes of preferences fetch IG. Manual category
  refresh is schedule-gated and category replacement preserves valid
  instrument snapshots and dormant interests.
* Secrets and provider diagnostic payloads stay outside UI, Application
  outcomes and logs. Migration, worker, SQL constraints, pages, tests and
  wiki changes ship together. Each gate has a zero-warning build and all
  relevant focused **and full solution** tests passing.

## Implementation Sequencing and Dependencies

Execute strictly in order: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7. A successful
work-item quality gate is a hard prerequisite for the next item. If a
dependency/environment prevents a full suite from running, investigate and
repair it before moving on; document the evidence rather than checking the
gate as successful. Use `global.json` for the SDK; do not introduce a new
framework target or unrelated build changes.

## Planning Record

* Created: 2026-09-24.
* Scope: this Markdown plan only. No source code, migration, test, runtime
  configuration or wiki file was changed in this planning session.
* Evidence: [research](../../research/2026-09-24/market-category-instruments-research.md);
  [prior plan](../2026-09-23/market-categories-delivery-plan.instructions.md);
  [IG endpoint reference](https://labs.ig.com/reference/categories-category-id-instruments.html).
