# Research: IG Market Category Instruments

This research evaluates adding persisted IG instruments to the operator
application. It is a recommendation only: no application source, database
schema, API, UI, or plan is changed by this document.

## Requested outcome

Selecting a saved Market Category such as `INDICES` should navigate to a page
that displays the instruments returned by IG's
`GET /categories/{categoryId}/instruments` resource. Before the UI can
display those instruments, the platform must retrieve them through its server,
validate them, and save them to SQL Server. The UI must read them through the
platform's existing Minimal API, not from IG. Acquisition must be initiated
by the application when it starts **if the configured Trading schedule is
active**, or when the next trading window opens, and subsequently only within
that trading window; never by navigation or a user pressing Refresh. The user
chooses which **categories** are interesting; the application later chooses
which **instruments** within those categories merit deeper tracking.

The supplied `INDICES` example establishes these important facts:

- `categoryId` is the saved category `code`, not a locally assigned ID.
- A category may contain hundreds of instruments (`384` in the example).
- The provider returns a paged response containing `instruments` and
  `metadata`.
- The response combines catalogue attributes (for example `epic`,
  `instrumentName`, `instrumentType`, and `lotSize`) with volatile price
  attributes (for example `bid`, `offer`, `high`, `low`, and
  `marketStatus`).
- `expiryTimestamp` is optional, and an expiry can instead be the literal
  `"-"`.

`INDICIES` in the request is assumed to mean IG's `INDICES` category code.
The platform should always use the exact code persisted from IG and should not
invent, correct, or enumerate category codes.

## Analysis-readiness requirement

The purpose of this data is not merely to browse instruments. The application
will later select likely instruments for deeper tracking and possible trading
by applying statistical analysis; an operator will not make that selection.
The selection algorithm, recommendation workflow, watchlist, and any trading
capability remain out of scope for this research.

A current-only catalogue is insufficient for that future analysis. It cannot
show whether an instrument is consistently tradeable, how its spread behaves,
whether it is routinely delayed, how its prices/changes vary, or whether its
category membership changes over time. The initial data capability must
therefore retain **complete, time-stamped, validated observations** as well as
the current snapshot used by the UI.

Each observation needs enough provenance to make a future result explainable
and reproducible:

- broker environment and endpoint profile;
- category code and EPIC, preserving category membership at collection time;
- the platform UTC collection timestamp and a successful collection-run ID;
- all supplied snapshot fields, including prices, market status, delay,
  popularity, scaling factor, and provider update-time text;
- the provider page/total metadata used to establish that the collection was
  complete; and
- a data-quality/completeness status derived by the platform, never silently
  inferred by a later analysis process.

The proposed category-interest checkbox controls the scope of data acquisition
only. It is not a user choice of instrument, a trading authorization, or an
analysis recommendation. Do not create a user-editable "selected instruments"
list as part of this work. A later analysis slice should own its versioned
selection criteria, input time window, resulting recommendation, and reasons.
The discovery slice must only provide trustworthy, correctly partitioned source
observations.

## Current repository evidence

The immediately preceding Market Categories delivery already provides the
correct structural precedent:

| Existing evidence | Relevance to instruments |
| --- | --- |
| `IMarketCategorySnapshotStore` owns a current saved snapshot, while `EfMarketCategorySnapshotStore` scopes reads and replacement to the applied broker environment. | Instrument discovery should be a separate environment-scoped current snapshot, never an unpartitioned shared cache. |
| `MarketCategoryCatalogStates` and `MarketCategories` use `(BrokerEnvironmentId, Code)` as the category identity. | An instrument catalogue must include that same category membership in its key and foreign key. An EPIC must not be assumed globally unique for the platform's persistence purposes. |
| The category store replaces a validated snapshot inside an EF Core transaction. | A category's complete instrument set must similarly be replaced only after every provider page has been retrieved and validated. |
| The existing `IgAccountDetailsGateway` pattern creates an IG v2 session, issues a v1 resource request, permits one fresh-session replay after `401`, and redacts provider failures. | Reuse these mechanics in a dedicated instrument gateway; do not put IG HTTP or token handling in Application, the API endpoint, or a Razor component. |
| The API separates Viewer SQL reads from Operator provider-refresh commands, and the Web application uses delegated tokens and role-gated navigation. | Use the same read/write authorization split for the category page and its refresh action. |

The repository wiki records that Market Categories currently have these
behaviours:

- they are an applied-environment current reference-data snapshot; the
  currently configured broker environment is IG Demo;
- Viewer reads are SQL-only;
- only an Operator may request the *existing manual* category refresh;
- a failed refresh leaves the previous catalogue and timestamp intact; and
- no page rendering, startup, login, or scheduled process currently refreshes
  categories.

The proposed feature **changes** the last two operational behaviours:
an application-owned startup/scheduled process will refresh categories and
collect instruments without an Operator request. Viewer reads remain SQL-only;
operator-only configuration writes and credential protection remain intact.
This is an intentional change from the current wiki description, not an
assertion that the collector already exists. It is market
discovery, not trading, and must operate against every configured, supported
IG broker environment. It must not hard-code, assume, or branch on `Demo`;
each broker environment requires its own credentials, endpoint profile, and
persisted snapshot. The absence of a configured Live environment today is not
a reason to design the feature as Demo-only, nor does this research enable
Live trading. Provider credentials and session tokens must remain protected.

## IG contract and implications

IG's [How to use the trading API](https://www.ig.com/uk/trading-platforms/trading-apis/how-to-use-ig-api)
describes market instruments as derivative products identified by EPICs. That
places this capability in the platform's market-discovery path. It does not
make the resulting data a trading authority, a price stream, or an order-entry
capability.

The IG Labs [category instruments reference](https://labs.ig.com/reference/categories-category-id-instruments.html)
identifies `GET /categories/{categoryId}/instruments` as the relevant
resource. The IG Labs [REST guide](https://labs.ig.com/rest-trading-api-guide.html)
and the repository's IG wiki guidance establish the existing session/header
model. At implementation time, the exact currently supported resource version,
query-parameter limits, and error codes must be rechecked against that
reference rather than copied from an old sample.

### Provider access

The gateway should follow the established IG session flow:

1. Resolve the applied broker environment and reject non-IG, unavailable, or
   provider-unsupported contexts before credential access or network I/O.
2. In Infrastructure, obtain protected credentials and create an IG session.
3. Request the category-instruments resource with the API key, CST,
   `X-SECURITY-TOKEN`, required `Accept` value, and the provider's required
   resource version.
4. If the resource request returns `401`, create one fresh session and replay
   the failed request once only.
5. Classify timeout, transport, allowance/rate-limit, provider rejection, and
   malformed-payload outcomes without putting request headers, tokens, raw
   payloads, or provider diagnostic text into logs, responses, or UI state.

The gateway must propagate caller cancellation. It must not perform blind
retries, an unbounded re-login loop, or concurrent page fan-out; each can
unnecessarily consume IG's non-trading allowance.

### Pagination

The supplied payload reports `pageNumber`, `pageSize`, `totalPages`, and
`totalResults`, so saving only the page displayed in the UI would produce an
incomplete catalogue. A refresh must obtain all pages sequentially and should
not publish data until the aggregate has passed validation.

The IG reference has historically documented `pageNumber` and `pageSize`,
while the supplied response shows page number `1`. Because page numbering can
be easy to misread and provider documentation can change, the implementation
should use an explicit, tested first-page convention from the live reference
and validate the returned metadata rather than infer page indexing from a
single example. It should:

- select a bounded provider page size supported by the current reference;
- capture the first response's total-page and total-result values;
- request each expected page once, sequentially;
- require a consistent category response and coherent metadata across pages;
- reject missing metadata, impossible page counts, duplicate EPICs in the
  category, and a final aggregate count that conflicts with `totalResults`;
- retain the prior SQL snapshot if any page or validation step fails.

The platform's own read API should use SQL paging (preferably an opaque
keyset cursor) rather than pass the caller's paging choices through to IG.
This gives consistent UI results from a single saved snapshot and keeps Viewer
reads provider-free.

### Data classification and validation

The provider DTO should use nullable members. Validation must occur in
Infrastructure before mapping to Application records or persistence entities.

| Data class | Examples | Handling |
| --- | --- | --- |
| Identity and presentation | `epic`, `instrumentName`, `instrumentType`, `underlyingName`, `expiry` | Require nonblank EPIC and name; trim and length-check all stored strings; preserve provider values rather than inventing labels. |
| Trading properties | `lotSize`, `otcTradeable`, `scalingFactor`, `expiryTimestamp` | Store provider values with explicit nullable semantics where the contract permits absence. Convert an expiry epoch to `DateTimeOffset` only after range validation. |
| Volatile observation | `marketStatus`, `delayTime`, `bid`, `offer`, `high`, `low`, `netChange`, `percentageChange`, `updateTime`, `popularity` | Retain in an immutable observation labelled with the platform UTC collection time; do not represent it as live market data. |
| Provider paging | `pageNumber`, `pageSize`, `totalPages`, `totalResults` | Use only to validate and drive the refresh. Persisting provider totals is optional diagnostic metadata, not the UI source of truth. |

Use .NET `decimal` and an explicitly configured SQL decimal precision for
prices, changes, and lot size; do not introduce binary floating-point
rounding into market values. `popularity` should be represented by a signed
64-bit integer after range validation. An absent quote, status, or time is not
necessarily malformed provider data, particularly outside market hours, so
those fields should be nullable if IG permits them. Missing/blank EPIC,
missing/blank instrument name, duplicate EPIC within one category snapshot,
invalid numeric ranges, malformed expiry timestamp, or inconsistent pagination
must fail the whole refresh.

`updateTime` is a provider-local time-of-day value in the supplied response,
not a globally unambiguous refresh timestamp. The UI should show the saved
snapshot's UTC refresh time as the authoritative “data retrieved” time and
label any displayed provider update time as supplied by IG.

For future analysis, calculate values such as bid-offer spread, spread
percentage, availability rate, and price-change/volatility measures from the
stored observations, not by overwriting provider fields with early derived
values. Persisting the original numeric values, collection time, and
completeness evidence allows selection logic to be versioned and recalculated
without another IG retrieval. Derived metrics may later be stored as a
separate, versioned analysis projection once their formulas and time windows
are approved.

## Recommended design

Extend market discovery as a dedicated `MarketCategoryInstruments` vertical
slice. Do not create a new physical API service: the existing platform API is
already a Minimal API and is the correct authenticated boundary for the Web
application.

### Application and Infrastructure boundaries

Application should own:

- framework-neutral instrument and snapshot records;
- a query handler that reads saved instruments only;
- a collection command that first refreshes the applied environment's
  categories, then loads its saved category interests, and collects each
  eligible category's complete instrument set;
- policy for due slots, the per-day update setting, bounded retry eligibility,
  and category selection, independent of the hosting implementation;
- narrow inward-owned ports such as
  `IMarketCategoryInstrumentsGateway` and
  `IMarketCategoryInstrumentSnapshotStore`, plus a selection/configuration
  reader and a cycle coordinator; and
- stable, secret-safe typed failure results.

Infrastructure should own:

- the typed IG HTTP client, session headers, provider request/query building,
  private wire DTOs, pagination loop, response validation, and error
  translation;
- EF Core entities, mappings, migration, and environment resolution;
- the transactions that change the saved catalogue, interest selection, and
  observation history, with a SQL-backed run/slot lease; and
- the existing Data Protection-backed credential access.

The API should own route and Problem Details mapping plus a separate hosted
collector that starts **after** migrations/bootstrap and runs the same
Application collection command on startup and at subsequent due slots.
The Web application should own its API transport client, presenter/view model,
protected Razor page, navigation link, loading/empty/error states, accessible
table/grid, category-interest controls, and setting editor.
Neither host nor Application should reference IG wire types, `HttpClient`,
EF Core entities, CST, or `X-SECURITY-TOKEN`.

### Category-interest selection and update setting

Add an **Interested** checkbox column to the existing Market Categories
page. All new categories default to unchecked: collecting all by default
would negate the request limit. An Operator can select one, several, or every
currently listed category; a Viewer can see the selected state but cannot
change it. Provide a clear save/confirmation state and accessible labels that
include the category code. Do not confuse `nonTradeable` with eligibility:
show it as today, but do not infer that a checked category authorizes trading.
An empty selection is valid; refresh categories automatically but make **no**
instrument requests and show why collection is idle.

Persist interest as a separate, environment-scoped preference, not as an IG
field or a flag on the replaceable `MarketCategories` rows:

```text
MarketCategoryInterestStates
  BrokerEnvironmentId uniqueidentifier PK/FK -> BrokerEnvironments
  Revision ... NOT NULL

MarketCategoryInterests
  BrokerEnvironmentId uniqueidentifier NOT NULL
  CategoryCode nvarchar(128) NOT NULL
  SelectedAtUtc datetimeoffset NOT NULL
  PRIMARY KEY (BrokerEnvironmentId, CategoryCode)
  FOREIGN KEY (BrokerEnvironmentId)
    -> BrokerEnvironments(BrokerEnvironmentId)
```

An interest row means "selected"; no row means "not selected". The separate
table survives current-category replacement and temporary category removal.
Only codes present in the applied environment's **current** validated
catalogue may be selected or scheduled. A removed category's interest can
remain as dormant preference for audit/reappearance, visibly flagged as
inactive, but it must not cause a provider call until IG lists it again.
New categories never inherit an old global/default selection. This explicitly
preserves user intent when categories are re-fetched. Use an environment-level
interest-set revision and a transactional compare-and-update so simultaneous
operators cannot silently undo each other's choices (including an unchecked
category with no row). This is shared operator-managed configuration, not a
per-user browser preference. Changes take effect at the next eligible
collection slot; selecting a category does not itself fetch instruments.

Add an operator-managed, SQL-persisted setting
`InstrumentUpdatesPerDay` (working name), scoped to the applied broker
environment; default **1**, positive integer, with a validated upper bound
driven by IG quota and measured selected-category page counts. Display and
edit it through the existing Configuration experience. Define one "update"
as **one complete instrument collection per selected category** within a
scheduled cycle. Thus with three selected categories and a setting of 2,
up to two complete observations *per category* are expected each day; it does
not mean two HTTP requests, as each category may require many pages.
Changing the setting applies from the **next trading day in the configured
Trading schedule time zone** so slot identities and the day's budget cannot
be rewritten in mid-cycle; it must not erase completion history or trigger
an immediate unbudgeted collection. If the setting is
invalid or unavailable, report a configuration error and pause collection;
do not silently replace it with a fallback.

`InstrumentUpdatesPerDay` means updates on an **active trading day**, not on
each UTC calendar day. No category or instrument data is fetched on excluded
days, holidays, or outside the configured start/end hours. Operator changes to
checkboxes or frequency may be saved outside trading hours because these
change local configuration, not IG Market Category or Instrument data.

### Persistence model

Use two complementary models per broker environment and category:

1. a current complete snapshot for low-latency UI reads; and
2. immutable, complete collection observations for later statistical analysis.

The observation store is not an audit copy of raw JSON. It is a structured,
queryable time-series source with enough provider provenance to exclude failed
or partial collections. A suitable relational shape is:

```text
MarketCategoryInstrumentCatalogStates
  BrokerEnvironmentId uniqueidentifier NOT NULL
  CategoryCode nvarchar(128) NOT NULL
  LastRefreshedAtUtc datetimeoffset NOT NULL
  PRIMARY KEY (BrokerEnvironmentId, CategoryCode)
  FOREIGN KEY (BrokerEnvironmentId, CategoryCode)
    -> MarketCategories(BrokerEnvironmentId, Code)

MarketCategoryInstruments
  BrokerEnvironmentId uniqueidentifier NOT NULL
  CategoryCode nvarchar(128) NOT NULL
  Epic nvarchar(...) NOT NULL
  InstrumentName nvarchar(...) NOT NULL
  InstrumentType nvarchar(...) NULL
  Expiry nvarchar(...) NULL
  ExpiryAtUtc datetimeoffset NULL
  UnderlyingName nvarchar(...) NULL
  LotSize decimal(...) NULL
  OtcTradeable bit NULL
  MarketStatus nvarchar(...) NULL
  DelayTime int NULL
  Bid decimal(...) NULL
  Offer decimal(...) NULL
  High decimal(...) NULL
  Low decimal(...) NULL
  NetChange decimal(...) NULL
  PercentageChange decimal(...) NULL
  ProviderUpdateTime nvarchar(...) NULL
  ScalingFactor int NULL
  Popularity bigint NULL
  PRIMARY KEY (BrokerEnvironmentId, CategoryCode, Epic)
  FOREIGN KEY (BrokerEnvironmentId, CategoryCode)
    -> MarketCategoryInstrumentCatalogStates(...)

InstrumentCollectionRuns
  CollectionId uniqueidentifier PK
  BrokerEnvironmentId uniqueidentifier NOT NULL
  CategoryCode nvarchar(128) NOT NULL
  TradingDay date NOT NULL
  ScheduledSlot int NOT NULL
  CollectedAtUtc datetimeoffset NOT NULL
  EndpointProfile nvarchar(...) NOT NULL
  ProviderPageCount int NOT NULL
  ProviderResultCount int NOT NULL
  UNIQUE (BrokerEnvironmentId, CategoryCode, TradingDay, ScheduledSlot)
  FOREIGN KEY (BrokerEnvironmentId)
    -> BrokerEnvironments(BrokerEnvironmentId)

InstrumentMarketObservations
  CollectionId uniqueidentifier NOT NULL
  BrokerEnvironmentId uniqueidentifier NOT NULL
  CategoryCode nvarchar(128) NOT NULL
  Epic nvarchar(...) NOT NULL
  InstrumentName nvarchar(...) NOT NULL
  InstrumentType nvarchar(...) NULL
  Expiry nvarchar(...) NULL
  ExpiryAtUtc datetimeoffset NULL
  UnderlyingName nvarchar(...) NULL
  LotSize decimal(...) NULL
  OtcTradeable bit NULL
  MarketStatus nvarchar(...) NULL
  DelayTime int NULL
  Bid decimal(...) NULL
  Offer decimal(...) NULL
  High decimal(...) NULL
  Low decimal(...) NULL
  NetChange decimal(...) NULL
  PercentageChange decimal(...) NULL
  ProviderUpdateTime nvarchar(...) NULL
  ScalingFactor int NULL
  Popularity bigint NULL
  PRIMARY KEY (CollectionId, Epic)
  FOREIGN KEY (CollectionId) -> InstrumentCollectionRuns(CollectionId)

InstrumentCollectionCycleState (durable coordination; shape illustrative)
  BrokerEnvironmentId uniqueidentifier NOT NULL
  TradingDay date NOT NULL
  ScheduledSlot int NOT NULL
  CategoryRefreshCompletedAtUtc datetimeoffset NULL
  Outcome/lease/attempt fields ...
  PRIMARY KEY (BrokerEnvironmentId, TradingDay, ScheduledSlot)
```

The exact string lengths and decimal precision should be selected from the
current IG schema plus deliberately malformed-boundary tests; they should not
be unrestricted or silently truncate values. The current snapshot is a
projection of the same validated collection that is displayed by the UI; its
volatile values may be replaced, while observations are never updated. Keep
the provider's fields normalised only as far as the UI and future analysis
need. Do not persist raw JSON because it adds sensitive diagnostic exposure
without improving the structured analysis source.
The unique run key makes completed category-slot collections idempotent across
restarts; a durable cycle state/lease records category-refresh success,
in-progress work, bounded failures, and due work that still needs retry.
Derive `TradingDay` from the configured Trading schedule time zone, using
the same schedule policy used for broker operations; retain
`CollectedAtUtc` for unambiguous provenance. Derive slot boundaries inside
the schedule's active start/end window, not at UTC midnight. Use the same
trading-day identity, time zone, and slot boundaries in setting, collection,
UI, and tests. A separate category-refresh state per cycle is needed because
a successful instrument run for one category does not prove that the
catalogue was refreshed for other categories in that cycle.

There is one important coupling with the existing category-store algorithm.
It currently removes and reinserts every category row during a replacement.
Once instruments have a proper foreign key to a category, that algorithm would
either violate the foreign key or force deletion of all instrument snapshots
whenever categories refresh. Even without the foreign key, resetting rows
would lose selections if interest were stored on the provider-owned category
row. The delivery must address this deliberately:

- prefer a transactional diff-based category replacement (insert new codes,
  update mutable flags, delete only removed codes after their dependent
  **current** instrument snapshots are removed); or
- otherwise explicitly delete all dependent current-snapshot data inside the same
  category-refresh transaction and communicate that a category refresh
  invalidates every cached instrument page.

The first option is recommended because it preserves still-valid instrument
snapshots for unchanged categories. A foreign-key-free table is not
recommended: it would permit orphaned and misleading discovery data.
Serialize manual category refresh with the cycle prerequisite, or otherwise
recheck current membership and category-catalogue revision before publishing
an instrument collection whose provider request spans multiple pages. The
existing manual category-refresh operation must also be schedule-gated before
its provider call and before its SQL replacement; otherwise an Operator could
update Market Category data outside trading hours despite the automatic
collector's gate.
This rule applies only to the current snapshot tables. Collection runs retain
the category code as historical provenance but deliberately do not use a
foreign key to the mutable `MarketCategories` projection; otherwise a removed
current category would erase or block retention of the observations needed for
analysis.

For an instrument collection, first retrieve and validate every provider page
in memory. Then, in one short SQL transaction, insert the completed collection
run and all immutable observations, replace the selected category's current
instrument rows, upsert its state timestamp, and commit. No provider I/O
belongs inside that transaction. A failure must insert neither a *successful*
collection run nor observations and must leave the old current snapshot and
its last-successful-refresh timestamp readable; record the failed attempt
separately in durable cycle state for diagnostics and controlled retry.

Observation retention must be an explicit policy rather than the existing
current-snapshot behaviour. Retain enough complete samples to cover the
longest intended analysis window, plus a defined safety margin; partition or
archive older data before it becomes an operational problem. A short,
undefined 90-day retention should not be inherited automatically: it may be
too little for seasonal analysis and too costly for high-frequency collection.
The policy must define collection cadence, retention duration, archive/export
destination, deletion verification, and the handling of an instrument or
category removed from the current catalogue. Historical observations should
remain available within that policy even after a current category membership
is removed.

### API contract

The following resource-oriented surface is recommended:

| Method and route | Role | Behaviour |
| --- | --- | --- |
| `GET /api/platform/market-categories/{categoryCode}/instruments?cursor={opaque}&pageSize={size}` | Viewer | Reads the current SQL snapshot for the applied environment and category. Returns a deterministic page, nullable `lastRefreshedAtUtc`, and cursor metadata. Never contacts IG or writes SQL. |
| Existing `GET /api/platform/market-categories` (extended response) | Viewer | Reads the current catalogue and a separate SQL interest projection; each category includes `interested` and no provider request occurs. |
| Suggested `PUT /api/platform/market-categories/{categoryCode}/interest` | Operator | Sets/clears interest with expected revision; validates category membership for a new selection; updates SQL only. No IG request. |
| Existing `GET`/`PUT /api/platform/configuration` (extended contract) | Existing configuration roles | Reads/updates the environment-scoped `InstrumentUpdatesPerDay` setting with validation; saving it does not run collection. |
| Suggested `GET /api/platform/instrument-collection/status` | Viewer | Reads last successful category refresh, each selected category's collection age, due/running/failed/skipped slots, and safe failure summaries from SQL. |

Do not introduce a manual instruments-refresh route as the primary ingestion
path. The existing manual **category** refresh can remain for diagnostics,
but it must preserve interest choices, must not itself fetch instruments, and
must return a clear conflict when the Trading schedule is inactive. Any
future manual instruments retry must obey the same schedule gate and durable
per-trading-day/slot allowance and idempotency rules, not bypass them.

The GET response should identify the requested category code, its last
successful refresh time, the page of instrument summary fields needed by the
UI, and next/previous cursor information where applicable. It must clearly
distinguish “category exists but has never been successfully retrieved” from
“a successful snapshot contains zero instruments.” The latter should be
accepted only if the current IG contract explicitly defines it as valid; it
should not erase a useful prior snapshot by default.

Use existing authorization policies: Viewers inspect stored results, Operators
change interest and update frequency, and the host's internal collector (not
an end-user HTTP call) owns scheduled provider I/O. Validate `categoryCode`,
page size, setting range, membership, and concurrency revision before
committing user changes. Map invalid inputs to validation Problem Details and
stale revisions to `409`. For the background collector classify malformed IG
data, rate limits, outages, and timeouts into safe durable outcomes and
observable status; never return provider headers or raw payloads.
The existing manual Market Categories POST additionally rejects an inactive
Trading schedule (for example `409`) without contacting IG or changing its
catalogue; the API GETs and local interest/configuration edits remain available.

### Web experience

Make each Market Category code an accessible link to a route such as:

```text
/market-categories/{categoryCode}/instruments
```

The page should:

- load solely from the platform GET route;
- show the exact category code, saved refresh time, and the fact that prices
  are snapshot data;
- show a semantic, responsive table or the established accessible Radzen grid
  with instrument name, EPIC, type, expiry, bid, offer, market status, and
  update-time columns;
- allow expansion or a detail route for lower-priority fields rather than
  making the first table impractically wide;
- support deterministic server-driven pagination;
- show an explicit empty/not-yet-retrieved state;
- show whether this category is selected for automatic acquisition, when it
  was last successfully collected, and the next expected slot (if scheduled);
- provide safe failure feedback without coupling navigation to ingestion; and
- retain the previous visible snapshot if a later refresh fails, marking it as
  stale rather than blanking the page.

This fulfils the requested click-through behaviour without allowing a page
render or a Viewer read to silently create an external request. On the
**Market Categories** page, add an `Interested` checkbox column accessible
to Operators and read-only state to Viewers; show last category refresh and
per-category collection status. In the Configuration page, explain that
`InstrumentUpdatesPerDay` controls per-category *complete* samples, not the
number of network calls. Newly selected categories will populate at the next
scheduled slot; a category not yet collected shows a clear empty state rather
than initiating an on-demand fetch.

### Concurrency, freshness, and observability

The collector is part of the **initial acquisition capability**, not an
optional later prefetch. Default `InstrumentUpdatesPerDay = 1` means one
scheduled collection opportunity per selected category per **active Trading
schedule day**, within that day's configured trading hours. `N > 1` divides
only the active trading window into `N` predictable, approximately even slots
(subject to a validated maximum and enough time/quota to complete). The
configured time zone, trading days, weekend rule, bank-holiday exclusions,
and start-inclusive/end-exclusive hours are authoritative: use the existing
Application `TradingScheduleGate.Evaluate` and `GetTradingDay`, not a second
UTC-day or hard-coded market-hours policy. Its configured-time-zone trading
day is the uniqueness/budget key; persist actual event times in UTC.

At API startup after database initialization, evaluate the schedule **before
any IG call**. If it is inactive, do not refresh Market Categories or
Instruments; wait for the next active window while the process remains
running. If it is active, run only the currently due uncompleted slot. Check
later slots during the trading window without a restart. A restart does not
repeat a completed slot. Missed prior slots/days are recorded as gaps rather
than replayed after hours or crowded into the next window. A newly selected
category waits for the next due in-window slot. "Once per trading day" is a
target cadence, not a guarantee when IG is unavailable or the application
starts too near close.

For **each in-window due cycle**, acquire an environment/trading-day/slot
lease and:

1. Re-evaluate the Trading schedule. Only while active, refresh the complete
   IG Market Categories catalogue **before any instrument request**, even
   when no categories are yet selected (to populate the selection page).
   Use the existing category gateway and validate before changing SQL.
   Persist a successful prerequisite marker for that cycle.
2. On category failure, keep the last saved catalogue and all instrument
   snapshots but make **no** instrument request in that cycle; record the
   error and retry only while the schedule remains active and within a
   bounded backoff/attempt budget. Do not treat the prior cycle's catalogue
   as a successful prerequisite.
3. After successful category refresh, load the shared selected interests and
   intersect them with the newly validated current catalogue; log dormant
   selections without fetching them. For each eligible category, reserve that
   category/slot, request **all** its IG pages in sequence **only while the
   Trading schedule remains active**, and commit its complete observation
   and current snapshot atomically before the window closes. A failed
   category must not prevent independently eligible categories from
   collecting while the window is still active.
4. Persist success/failure and timestamps per category and slot. Subsequent
   retries skip already completed category/slot keys, and do not exceed the
   configured daily slots, the trading window, or an independent, bounded
   retry/HTTP allowance.

Check the schedule again before each provider page, after a response, and
immediately before committing provider-derived rows; enforce a deadline at
the trading-window close and cancel outstanding requests on shutdown or
schedule invalidation. If a window closes or the schedule is disabled/changed
mid-cycle, stop both category and instrument retrieval, discard incomplete
instrument data, do not commit out-of-window provider-derived updates, and
record a skipped/incomplete slot. A previously committed complete category
or instrument snapshot remains valid; never publish a partial category or
instrument set to "finish" a cycle after close. Changes to the configured
schedule should invalidate/recalculate future due windows without retroactive
off-hours catch-up. Give the short SQL commit a bounded in-window deadline
and roll it back if it cannot finish before close. End-of-window time checks
must use the same schedule policy and an injected clock in tests, including
time-zone/DST transitions.

The category prerequisite may run again when retrying a failed cycle; retain
the last success time and avoid unnecessarily re-fetching it within the same
successfully refreshed cycle. Use a shared provider-call budget and
throttling across category and instrument requests: at page size 10 the
supplied `INDICES` example needs 39 instrument GETs for **one** observation.
The update setting is not itself a request-rate limiter. Bound page counts,
space calls to respect the actual IG account/app non-trading allowance, and
report when the selected categories cannot fit the configured budget instead
of silently collecting a partial catalogue. Defaults and caps must be
reconciled against IG quotas before enabling higher frequencies.

The existing authentication supervisor currently performs a blocking first
reconciliation before normal readiness. Do **not** add many paginated IG
requests to that blocking startup path. Start an independent API-hosted
worker after migrations and bootstrap are ready; schedule an immediate
best-effort due cycle **only if the Trading schedule is active**, otherwise
wait for the next active window. Expose collection progress/failures via its
own SQL status without making the API/Web unavailable merely because IG is
slow.
Only the **applied** broker environment is collected by that running process;
once a different supported environment becomes applied, its own selections,
settings, and slot history govern acquisition. Never collect all configured
environments with unselected or inactive credentials.

On a fresh installation, the first in-window cycle populates Market
Categories but downloads **zero** instruments until an Operator has selected
at least one category. On subsequent in-window starts, the persisted
selection supplies the scope of the currently due cycle; an out-of-window
startup never populates or updates provider data.

Use a durable SQL lease/application lock for the environment/slot and
category/slot reservation across replicas, plus unique successful-run keys.
A process restart or lost lease may cause a bounded IG request replay but
must not publish two successful observations for the same category and slot.
Cancellation and timeout release leases and leave last-good data intact.
Record redacted telemetry for category refresh, selected-category count,
slot due/completed/skipped/retried, attempts, page counts, request budget,
durations, collection IDs, and sampling gaps. Never record credentials,
headers, raw payloads, or quote values solely for diagnostics.

## Alternatives considered

| Option | Benefits | Drawbacks | Assessment |
| --- | --- | --- | --- |
| Recommended: current SQL snapshot plus immutable complete observations | Meets the storage-before-display boundary; preserves history required for analysis; works across restarts/replicas; controls quota; supports pagination. | Requires retention, collection-cadence, migration, and pagination decisions. | Best fit for discovery now and application-owned statistical selection later. |
| Fetch IG directly when a user opens the page | Initially simple and always attempts fresh data. | Violates the saved-data boundary, exposes provider latency/rate limits to page navigation, makes Viewer reads side-effecting, and complicates credentials/security. | Reject. |
| API fetches on GET, then stores and returns | Keeps credentials server-side. | Still makes normal reads write/provider operations, creates duplicate refreshes, and hides quota consumption behind page navigation. | Reject for the initial feature. |
| Persist only the first provider page | Smallest schema and request volume. | The supplied category has 39 pages; the UI would incorrectly present an incomplete market universe. | Reject. |
| Persist catalogue fields only; discard all quote fields | Avoids presenting stale prices as live. | Removes the data needed to analyse spread, availability, and price behaviour. | Reject. |
| Persist only the latest full provider summary | Supports immediate visualisation with a modest schema. | Overwrites the time series needed for statistical selection. | Reject. |
| Startup-only download | Initially simple; independent of user visits. | A once-only download misses subsequent days unless restarts are guaranteed; repeated starts may violate daily limits. | Reject; a startup due check plus running scheduler and durable slot history are needed. |
| System-owned, Trading-schedule-gated collection of interested categories | Produces samples during the defined trading window independently of page visits and limits coverage to explicit interests. | Needs category-first refresh, quota budget, distributed lease, retention policy, and monitoring. | Recommended; include in initial acquisition capability with default one update per active trading day. |
| Store interest on provider category rows | One less table. | Existing refresh deletes/reinserts them, losing choices; provider-owned data and operator intent become coupled. | Reject; use a separate environment-scoped preference table. |
| Streaming prices for every listed instrument | Gives near-real-time updates. | The category can contain hundreds of EPICs, while IG documents subscription limits; it is much more complex and inappropriate for passive discovery. | Do not add. Consider streaming only for a bounded future watchlist. |
| Search-first market discovery | Avoids browsing very large categories and can be useful for known EPIC/name discovery. | Does not replace the requested category browse journey or its durable category membership. | Valuable later as a complementary feature. |

## Recommended verification scope for a future delivery

The future implementation should be protected at the existing test boundaries:

| Test boundary | Essential evidence |
| --- | --- |
| Application unit tests | SQL-only query does not call IG; category refresh precedes any instrument request; missing/invalid category response blocks instruments; only checked and currently listed categories collect; none checked produces no instrument calls; a completed collection saves exactly one current snapshot plus immutable observations; failure cannot publish a partial sample. |
| Infrastructure gateway tests | Method/path/version/session headers; query encoding; all-page traversal; one-and-only-one `401` re-session replay; pagination inconsistency and malformed field rejection; safe classification of timeout, allowance, and provider failures. |
| EF Core unit and SQL integration tests | Migration, keys, foreign keys, decimal mappings, atomic observation append/current replacement, interest persistence across category refresh/removal/reappearance, environment isolation, concurrency revisions, unique category-slot success across restarts and replicas, and fresh-context readback. |
| Scheduler tests with a controllable clock | Startup before open/after close or on weekends/holidays makes zero IG calls; startup inside the window checks only its due slot; opening while running triggers collection; repeated/restarted startup does not repeat a completed slot; frequency 1 or N fits within the configured hours and trading-day cap; setting changes start next trading day; failed category refresh retries only before close; missed slots create gaps, not bursts; a newly checked category waits for the next slot; time zone/DST and configured schedule changes are honoured; crossing close cancels an in-flight page with no partial/out-of-window write. |
| API tests | Viewer read and Operator interest/configuration-write authorization; revision conflict, invalid settings/category, safe collection status; GET and checkbox changes do not fetch IG; manual category refresh rejects inactive schedule with no IG call or catalogue write. |
| Web unit tests | Accessible checked/unchecked and read-only category-interest states; selecting one/many/all/none, saved changes and concurrency errors; frequency editor; current snapshot, not-yet-collected, failure, and slot status. |
| Aspire/API integration tests | If an existing fixture supports it, exercise hosted startup during and outside a configured trading window against a controlled IG gateway and SQL, including category-before-instruments and restart deduplication; never call real IG or use real secrets. |

Test names and traceability comments should follow the repository's test
standards. No test should contact IG.

## Decisions requiring product confirmation

These decisions influence behaviour and should be made before delivery:

1. **Frequency guardrail and timing:** Default once per **active Trading
   schedule day**; confirm the maximum allowed `InstrumentUpdatesPerDay` and
   how many complete category downloads fit inside its configured hours.
   Display due slots in the configured time zone and quota estimates clearly.
2. **Retention and archive:** What is the longest statistical window, where
   will older observations be archived, and how will deletion/archival be
   verified?
3. **Completeness rule:** Must every expected provider page succeed before a
   sample is eligible for analysis (recommended), and how should gaps be
   represented?
4. **Snapshot price display:** Should volatile provider values be displayed,
   and what visible disclaimer/time label is acceptable?
5. **Refresh retention:** When a category disappears from IG, should its
   current instruments be deleted while historical complete observations remain
   under the analytics-retention policy (recommended)?
6. **Broker environments:** Each configured, supported IG broker environment
   must keep independent snapshots, credentials, and endpoint configuration.
   Confirm the provider-capability validation and endpoint-profile rules that
   determine which broker environments are supported; do not make this
   conditional on a Live environment already existing.
7. **Pagination UX:** Is server-side paging sufficient for the initial page,
   or is client-side filter/search required for a large category?
8. **Interest lifecycle:** Confirm that new categories are unchecked, a
   removed category's interest is dormant rather than discarded, and a
   newly checked category starts at the next due slot.
9. **Retry envelope:** Define the maximum retried pages/attempts and quota
   headroom per environment. The requested daily frequency bounds *successful
   samples*, not all HTTP requests made while recovering from errors; retries
   never extend past the configured closing time.

## Conclusion

The best-practice implementation is a new, environment-scoped Market Category
Instruments slice that retrieves every IG page through Infrastructure,
validates the complete response, atomically stores a current SQL snapshot and
an immutable complete observation set, and exposes the current snapshot via
the existing protected Minimal API. Category links should lead to a
saved-data page. Operators can check which categories are of interest and set
the number of complete updates per day; both choices persist per broker
environment. After startup, an independent collector refreshes categories
first and then downloads only the currently listed, interested categories
for the due slot **only while the configured Trading schedule is active**,
continuing on in-window slots while the process runs. An out-of-window start
waits until the next active trading window; an in-flight cycle stops at close,
and the existing manual category refresh must obey the same gate. A durable
lease and trading-day slot record prevent restart/replica duplication;
failures leave last-good data intact. The system, not the operator, will
later choose individual instruments using the retained observations. This
changes the current manual-only Market Categories runtime intentionally while
preserving read authorization, credential protection, and saved-data failure
semantics.

## References

- [IG: How to use the trading API](https://www.ig.com/uk/trading-platforms/trading-apis/how-to-use-ig-api)
- [IG Labs: Category instruments endpoint](https://labs.ig.com/reference/categories-category-id-instruments.html)
- [IG Labs: REST trading API guide](https://labs.ig.com/rest-trading-api-guide.html)
- [Repository IG API guidance](../../../docs/wiki/ig-day-trading-with-ig-apis.md)
- [Repository architecture](../../../docs/wiki/architecture.md)
- [Repository runtime behaviour](../../../docs/wiki/runtime-behavior.md)
- [Repository API reference](../../../docs/wiki/api-reference.md)
- [Prior Market Categories research](../2026-09-23/market-categories-research.md)
