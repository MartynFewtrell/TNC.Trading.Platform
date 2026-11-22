# Research: IG Market Details for interested categories

Research date: 2026-09-25. This document recommends an integration; it does
not implement it or define a delivery plan.

## Scope and interpretation

The requested universe is **every distinct EPIC in every currently interested
category that is still present in the refreshed IG category catalogue**, not
only the instruments currently visible on a browser page. Market Details are
the `instrument`, `dealingRules`, and `snapshot` returned by the supplied
`GET /markets/CS.D.ADAUSD.CFD.IP` example. Collection is system-initiated,
schedule-gated, persisted to SQL Server, and read by the platform API/UI from
SQL only. It is not a streaming feed, an order permission, or a trigger for a
provider request when a user opens an instrument.
The market-detail run must attempt **all eligible selected EPICs**: the
application needs complete coverage to decide which instruments warrant
closer monitoring. IG-confirmed unavailable/expired markets and EPICs no
longer in the current selected listings are excluded from subsequent active
collection, with the exclusion reason and history retained. An ordinary
request failure is not evidence that a market has become ineligible.

The example is a **single live response supplied with the request**, not a
guarantee that all fields are present or have identical values for other
instruments or environments. In particular, its `type: "CURRENCIES"` and
cryptocurrency-specific `specialInfo` are provider values, not assumptions
about category membership or permissible trades.

## Existing behavior and constraints

The [application overview](../../../docs/wiki/application-overview.md) and
[runtime behavior](../../../docs/wiki/runtime-behavior.md) describe the
existing read-only market-data workflow. An API background worker runs a due
trading-day slot after bootstrap, refreshes the entire category catalogue,
intersects current categories with shared operator interest, then retrieves
complete paginated instrument lists. Schedule is local-time-zone-aware with
active days/holidays, start-inclusive/end-exclusive trading windows, one to
four slots per active day, next-day-effective frequency changes, missed-slot
gaps rather than catch-up, and a 30-second maximum recheck delay. A new
interest selection affects a later slot, not an immediate IG request.
Unsupported or unconfigured applied broker environments fail closed.

The [IG day-trading wiki](../../../docs/wiki/ig-day-trading-with-ig-apis.md),
[architecture](../../../docs/wiki/architecture.md), and
[operator guide](../../../docs/wiki/operator-guide.md) describe environment-
scoped SQL snapshots, complete retained observations, request controls,
protected Viewer reads, Operator-managed interest and allowances, and the
distinction between saved prices and live quotes. In code,
`MarketCategoryInstrumentCycleCoordinator` refreshes categories before
`CollectCategoryAsync`, with a 2-minute renewable SQL lease/fence, per-category
attempts, bounded retries, and publication only while the schedule, catalogue
revision, applied environment, and lease remain valid
([coordinator](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs)).
`IgMarketCategoryInstrumentsGateway` establishes a v2 session and retrieves
zero-based v1 category-instrument pages, reserves a durable request before
each HTTP call, and checks the applied endpoint before and after transport
([gateway](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs)).
The HTTP profile resolver allows only explicitly capable applied IG Demo and
IG Live profiles ([resolver](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgEndpointProfileResolver.cs)).

`EfMarketCategoryInstrumentSnapshotStore` transactionally publishes one
versioned current category snapshot plus an immutable complete run and its
instrument observations, preserving the last good snapshot on failure
([store](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs)).
These rows contain category-listing fields (including bid/offer and
`updateTime`), **not** the richer market instrument, dealing rules, and
snapshot returned by `/markets`. They are keyed by environment, category,
and EPIC for current listings, while observations are keyed by collection ID
and EPIC ([EF mapping](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs)).
One EPIC may occur in multiple categories, so avoid fetching/storing identical
market details multiple times per environment and slot, but retain the
category-to-EPIC membership and listing-run provenance.

The shared daily allowance covers category, session, and listing-page HTTP
requests and is consumed transactionally by environment/day; a missing or
zero allowance pauses requests
([budget store](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentCycleStore.cs),
[budget adapter](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentRequestBudget.cs)).
The existing singleton IG throttle spaces calls by at least 250 ms **within
one process**, not across replicas
([throttle](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgProviderRequestThrottle.cs)).
The retry policy allows at most two additional attempts for retryable
failures, and explicitly excludes quota, unauthorized, and schedule-closed
failures ([policy](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentRetryPolicy.cs)).
Saved instrument browsing is a protected SQL-only, versioned cursor API
([endpoint](../../../src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentEndpoints.cs)).

## IG contract and sample mapping

IG's [API introduction](https://www.ig.com/uk/trading-platforms/trading-apis/how-to-use-ig-api)
identifies EPICs as market-instrument identifiers; these API markets are
derivatives (CFDs/spread bets), not a general share-price feed. The detailed
[single-market reference](https://labs.ig.com/reference/markets-epic.html)
specifies `GET /markets/{epic}` in versions **1-4**, whereas the
[multi-market reference](https://labs.ig.com/reference/markets.html)
specifies `GET /markets?epics=...` in versions **1-2**. The latter takes
comma-separated EPICs, at most **50 per request**, and v2 returns
`marketDetails[]`. Request `filter=ALL` (instrument + dealing rules +
snapshot), not `SNAPSHOT_ONLY`, which leaves most instrument fields unset.
This is the preferred transport candidate for the requested all-EPIC
universe, **subject to verifying its v2 field coverage against the supplied
single-market example** using safe Demo fixtures. It is not safe to assume
that `/markets` and `/markets/{epic}` share the same schema or Version header.

The [REST guide](https://labs.ig.com/rest-trading-api-guide.html) specifies
Demo/Live `/gateway/deal` bases, JSON content negotiation, `X-IG-API-KEY`
and a `Version` header (otherwise default v1). The existing v2 session's
`CST` and `X-SECURITY-TOKEN` can be used with the REST resource; v3 session
auth instead uses OAuth `Authorization` plus `IG-ACCOUNT-ID`. Do not mix
authentication or market DTO versions inadvertently. The supplied response
resembles a single-market bid/offer snapshot; **v3** documents snapshot
`bid`/`offer`, `delayTime`, `marketStatus` and string `updateTime`, whereas
**v4** documents `updateTimestampUTC` in Unix seconds and `priceLadder`
with string bid/ask, without the same top-level bid/offer fields or
`openingHours`. The sample alone does not prove the request's `Version`
header. Persist the actual endpoint version and deliberately map its shape;
do not silently deserialize v4 into a v3 model.

The supplied response identifies a single market by `instrument.epic` and has
three distinct kinds of information:

| Response area | Example data | Storage and validation implication |
| --- | --- | --- |
| Instrument identity/static-ish terms | `epic`, `expiry`, `name`, `marketId`, `type`, `unit`, `lotSize`, `forceOpenAllowed`, `stopsLimitsAllowed`, `controlledRiskAllowed`, `streamingPricesAvailable` | Keep actual EPIC as join key, preserve source strings/booleans, validate identity and bounded text. Terms may change; do not treat them as immutable. |
| Instrument commercial/risk terms | `currencies[]`, `marginDepositBands[]` (including `max: null`), `marginFactor`, `marginFactorUnit`, `slippageFactor`, `limitedRiskPremium`, `valueOfOnePip`, `onePipMeans`, `contractSize`, `specialInfo[]` | Preserve complete ordered nested collections and string-valued pip/contract fields; do not flatten bands into one margin or infer permission to trade. Use decimals, not binary floats, for numeric values. |
| Optional instrument metadata | `openingHours`, `expiryDetails`, `rolloverDetails`, `newsCode`, `chartCode`, `country`, sprint-expiry fields | Explicitly distinguish absent, null, and populated fields where meaningful. IG can vary by market type and endpoint version. Bound arrays and text, but do not reject a valid nullable optional field. |
| Dealing rules | `minStepDistance`, `minDealSize`, `minControlledRiskStopDistance`, `minNormalStopOrLimitDistance`, `maxStopOrLimitDistance`, `controlledRiskSpacing` (unit/value), `marketOrderPreference`, `trailingStopsPreference` | Units differ (`POINTS`, `PERCENTAGE`); retain each value with its own unit. Do not apply these snapshot rules to live order validation without a fresh trading-specific decision. |
| Quote snapshot | `marketStatus`, `netChange`, `percentageChange`, `updateTime`, `delayTime`, `bid`, `offer`, `high`, `low`, `binaryOdds`, `decimalPlacesFactor`, `scalingFactor`, `controlledRiskExtraSpread` | Save provider-supplied update-time text separately from platform UTC retrieval time, delay, and source/version. Never present as live/streamed price. |

The sample also has `currencies[0].baseExchangeRate` and `exchangeRate`,
nullable `marginDepositBands[].max`, and nullable `snapshot.binaryOdds` and
`controlledRiskExtraSpread`: models and SQL schema must accommodate these
cases. Avoid reusing the category-listing instrument DTO: its shape and
validation differ. Prefer typed values for known fields, with versioned
bounded payload preservation only if necessary to retain provider fields
not yet mapped; never expose unredacted raw HTTP bodies to clients or logs.
The v2 bulk contract should be inspected field by field against this
inventory; if `ALL` omits required information, use a separately budgeted
single-market fallback for only the missing fields. Do not fabricate
null-filled "complete" records or silently narrow the data required for
instrument selection.

The [market endpoint exceptions](https://labs.ig.com/reference/markets.html)
include **400** (too many EPICs), **401** (missing/invalid credentials),
**403** (account/API-key allowance exceeded), and **404**
(`error.public-api.epic-not-found`); the
[REST error guide](https://labs.ig.com/rest-trading-api-guide.html) describes
JSON `errorCode`. Therefore budget exhaustion is not limited to HTTP 429:
classify documented 403 allowance errors safely, distinguish other 403
authorization failures, replay at most once after a 401 reauthentication,
and treat unknown/expired EPICs as explicit collection failures rather than
successful empty records until IG has conclusively identified them as
ineligible for future collection. Record that exclusion explicitly, retain
history, and distinguish permanent market unavailability from transient
provider/request errors. For a mixed bulk response, validate that every
requested EPIC appears exactly once and no unexpected EPIC appears; document
and test whether IG fails the whole request or returns partial successes.

## Recommended integration

Treat a Market Details collection as a **distinct, dependent data product**
within the existing schedule/allowance control plane. After successful
category refresh and complete instrument-list collection, derive a frozen
set of EPICs from *currently interested and current* categories, including
the empty-set case. Deduplicate EPICs across categories for transport while
recording which complete listing run(s) supplied membership. Do not infer
the universe from one paginated Viewer response or from old/dormant category
rows. Account explicitly for IG-confirmed unavailable/expired EPICs as
excluded rather than quietly dropping them from the denominator. A category
that fails its listing refresh should not silently count its last-good rows
as fresh coverage; expose that category's details status as stale/incomplete
and preserve previously published data.

Add an Application-owned market-details collection contract, status/read
ports and policy, an Infrastructure-local IG adapter with dedicated wire
DTOs and validation, SQL persistence and indexes, and API composition/Viewer
read mappings; reuse the existing applied-profile resolver, protected
credentials, schedule guard, lease fencing, throttle, daily allowance, and
failure vocabulary rather than inventing another scheduler or bypassing the
budget. An operator-facing detail view may build on each saved instrument
tab, but must not initiate IG traffic. Avoid adding a write-capable market
endpoint or placing logic in `Program.cs`.

For the preferred v2 bulk `ALL` path, split the distinct EPIC set into
bounded batches of **at most 50**, further reducing batch size if encoded
query length or provider behavior requires it; budget *one request per batch*
and all session/replay calls. The current per-process 250 ms throttle alone
does not meet the documented **30 per-account** or **60 per-app** non-trading
requests/minute limits, especially across replicas. Coordinate the effective
rate across the account/API key (or serialize provider collection with a
proved single active owner), accounting for all other platform IG consumers
sharing those quotas, not just this worker.

Store a **current latest-good details projection** per applied environment
and EPIC, plus **immutable observations** keyed to a details collection/run
with retrieval UTC, provider/source/version, trading day/slot, originating
instrument-list snapshot version(s), expected/returned/failed EPIC counts,
and completeness/quality state. Normalize multi-valued currencies, margin
bands, special information, and opening hours where queries require it, or
store bounded, schema-versioned JSON for variable nested attributes with
validated indexed identity/operational fields. Decide storage from the
actual provider contract; do not overwrite the category listing's price
fields or conflate a listing collection ID with a details observation ID.
Preserve historic observations if a category disappears, as the wiki already
does for listing history. Keep retirement and SQL growth semantics explicit.

Publication should never claim a **complete** market-detail run when an EPIC
in its eligible frozen universe is missing, duplicated, mismatched, or its
response is malformed. A fully validated individual EPIC observation may
publish as individually complete even while the **aggregate run remains
incomplete**. Continue bounded attempts for the outstanding eligible EPICs
while the schedule, lease and allowance permit; never repeatedly refetch
successful EPICs needlessly. After a failure or window closure, retain each
last successful projection and mark any EPIC without an up-to-date observation
**out of date**, not current by virtue of its older value. Report expected,
completed, excluded, failed and outstanding counts, distinguishing
`NeverCollected`, `Complete`, `Incomplete` and `OutOfDate` at the appropriate
run and EPIC/category levels. A previously complete aggregate run must not
be relabeled as complete for a newer incomplete universe. Recheck schedule,
lease/fence, catalogue/interest
membership, applied environment, and quota immediately before HTTP calls and
publication. If selection changes during a run, do not publish the resulting
coverage as current for the changed universe. Use cancellation/deadline
checks and bounded retries; record safe errors rather than logging provider
responses, headers, or credentials.

### Concrete persistence recommendation

Use an additive EF Core migration and environment-scoped tables rather than
adding dozens of nullable detail columns to the existing category listing.
The names below are **proposed entities/tables and important columns**, not
an implemented schema or a delivery sequence:

| Proposed entity / SQL table | Key fields and constraints | Purpose |
| --- | --- | --- |
| `MarketDetailCollectionRunEntity` / `MarketDetailCollectionRuns` | `RunId` PK; unique `(BrokerEnvironmentId, TradingDay, ScheduledSlot)` under the existing one-run-per-slot semantics; `CategoryCatalogRevision`, `InterestRevision`, `ScheduleRevision`, `EndpointProfile`, `StartedAtUtc`, `FinishedAtUtc`, `WindowEndUtc`, `LeaseOwner`, `LeaseFence`, `LeaseExpiresAtUtc`, `Status`, `ExpectedCount`, `CompletedCount`, `ExcludedCount`, `FailedCount`; FK to retained broker-environment identity | A durable, resumable **detail** run and its aggregate coverage, distinct from the existing listing cycle outcome. Check nonnegative counts and permit `Complete` only when all required listing sources are validated and `CompletedCount == ExpectedCount`; excluded EPICs are counted separately. |
| `MarketDetailRunSourceEntity` / `MarketDetailRunSources` | `(RunId, CategoryCode)` PK; `ListingCollectionId` FK to retained `MarketCategoryInstrumentCollectionRuns.CollectionId`, `ListingSnapshotVersion`, `CategoryCatalogRevision`; source row for each interested current category, including complete empty lists | Freezes the contributing **immutable** category listing runs without a FK to the replaceable `MarketCategoryInstruments` rows. A failed listing prerequisite prevents full detail coverage and must be recorded as such, not omitted. |
| `MarketDetailRunTargetEntity` / `MarketDetailRunTargets` | `(RunId, Epic)` PK with binary/case-sensitive EPIC collation consistent with listing rows; `Status`, `AttemptCount`, `LastAttemptAtUtc`, `LastSafeFailure`, `ExclusionReason`; bounded fields and state constraints | Explicit distinct-EPIC denominator and resumable per-EPIC progress. `Succeeded` must reference one validated observation; `Excluded` requires confirmed evidence. Batch failures do not erase successful targets, and retries select only outstanding targets. |
| `MarketDetailRunMembershipEntity` / `MarketDetailRunMemberships` | `(RunId, Epic, CategoryCode)` PK; FKs to run target and run source | Preserves one-to-many membership when an EPIC occurs in multiple categories; enables accurate per-category coverage without issuing duplicate market calls. Never cascade-delete it when a current category disappears. |
| `MarketDetailObservationEntity` / `MarketDetailObservations` | `ObservationId` PK; unique `(RunId, Epic)`; FK to run target, `BrokerEnvironmentId`, `RetrievedAtUtc`, `ProviderUpdateTimeText` or version-specific UTC timestamp, `ApiVersion`, `MarketStatus`, `DelayTime`, validated fields for selection, `DetailsSchemaVersion` and bounded structured nested terms | Immutable **individual** success with explicit retrieval/source provenance. For v2 batch requests, retrieval time belongs to each returned market, not just the final batch. Keep deal-size/risk units with values; persist nullable optional values and ordered currencies, margin bands and `specialInfo`. |
| `MarketDetailCurrentEntity` / `MarketDetailCurrent` | `(BrokerEnvironmentId, Epic)` PK; `ObservationId` FK, `LastSucceededAtUtc`; optional SQL Server `rowversion` for competing projection updates | Latest-good individual detail, updated atomically with its new observation only if the run/lease and applied environment remain valid. Derive **out of date** from the most recent required run/target and selection rather than overwriting good data with nulls or an old mutable freshness flag. |
| `MarketDetailEligibilityEntity` / `MarketDetailEligibility` | `(BrokerEnvironmentId, Epic)` PK; `EligibilityStatus`, `ReasonCode`, `FirstObservedAtUtc`, `LastConfirmedAtUtc`, optional `ReinstatedAtUtc`; reason code is safe, bounded and auditable | Records provider-confirmed unavailable/expired states independently of the last good snapshot. Missing from a *validated current listing* removes an EPIC from active membership, not from history. Do not treat a temporary `CLOSED`/non-`TRADEABLE` quote or a transient HTTP failure as permanent unavailability. Define how later positive IG/listing evidence reinstates eligibility without silently blacklisting an EPIC forever. |

Index observations on `(BrokerEnvironmentId, Epic, RetrievedAtUtc)`
for historical time-series queries and run targets on `(RunId, Status)`
for bounded retries; scope all read keys by applied broker environment. Keep
history FKs restricted to retained run/broker records rather than mutable
category or current-projection rows; explicitly update retirement handling
to disable/clear active leases and purge the latest-current projection while
retaining **all runs with successful observations**, including incomplete
runs, their membership, and the observations themselves for analysis.
Check actual SQL row, index, and nested
payload growth before selecting fixed column sizes and indexes. Map
searchable selection attributes to typed indexed columns; if variable
provider-only nested fields are stored as validated JSON, include a schema
version and bound its size, and do not assume JSON-path indexing is
available on the deployed SQL Server/EF version.

Do not create an observation for a failed response. Persist a target's
failure/exclusion separately, with a safe reason and attempts; do not save
raw provider error bodies or authentication tokens in the historical
payload. Model numerical quantities as `decimal` with explicit SQL
precision/scale, units as bounded strings or a deliberately versioned
vocabulary, and provider optionality as nullable where the IG contract
allows it. Use one transaction for a validated observation, its target
success, and latest-good pointer; use a *separate* finalization transaction
to validate all source revisions, counts, and target states before marking
the aggregate run complete. A database unique constraint on `(RunId, Epic)`
plus a lease fence protects against duplicate insertion on retry; handle
both unique-key conflicts and concurrency-token conflicts explicitly, not
as successful provider results. [EF Core concurrency guidance](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
distinguishes update conflicts from unique-key insert failures; its
[index guidance](https://learn.microsoft.com/en-us/ef/core/modeling/indexes)
supports deliberate unique/composite indexes.
Stage the frozen target/membership universe in SQL and read pending EPICs
in deterministic bounded batches rather than materializing every category
and large response in a long-lived EF change tracker. Commit validated
individual observations and progress in bounded transactions; never hold
a database transaction open across an IG HTTP call. Benchmark migrations,
index write amplification, history query latency and SQL storage with
representative large categories before choosing additional analytical
indexes or a future archive strategy.

### Application wiring and slot correctness

Place inward-facing operation contracts and policies under
`Application/Features/MarketDetails` (for example
`CollectMarketDetailsRequest`, `CollectMarketDetailsResponse`,
`CollectMarketDetailsHandler`, `GetMarketDetailsRequest`,
`GetMarketDetailsResponse`, `GetMarketDetailsHandler`, and a
`MarketDetailEligibilityPolicy`). Add narrow `IMarketDetailsGateway`,
`IMarketDetailRunStore`, `IMarketDetailObservationWriter`, and
`IMarketDetailReader` ports only where their consumers need them.
`IgMarketDetailsGateway` belongs alongside the existing IG gateways;
`EfMarketDetailRunStore` and `EfMarketDetailObservationStore` implement
the durable state and queries. Follow the repository's one-top-level-type-
per-file convention. Register adapters in the existing Infrastructure/API
composition roots; keep the API worker as host orchestration and map a
protected SQL-only `GET /api/platform/market-categories/{categoryCode}/instruments/{epic}/market-details`
in the dedicated Platform endpoint mapping, returning distinct
`NotCollected`, `OutOfDate`, and validated-detail states. An environment-
scoped read contract can support later analysis without forcing the UI to
load an entire category.

The **existing listing cycle is not enough to resume details**:
`MarketCategoryInstrumentCycleCoordinator` records the slot `Completed`
after listing attempts, and the durable request budget currently accepts
a running listing-cycle lease for scheduled requests. Keep listing status
semantics unchanged. A detail coordinator can be invoked by the same
worker after successful listing publication and on later ticks **within
that slot's active trading window** to resume outstanding details, using
its own fenced SQL detail-run lease. Extend the *same environment/day
allowance transaction* to authorize a detail-run lease before each HTTP
request; do not create an uncounted second allowance or pass an expired
listing lease to the existing budget adapter. Serialise owner/lease changes
and check schedule, profile, source runs, interest revision, and exclusion
evidence at publication. An interrupted detail run may resume within its
window; an ended window remains incomplete/out of date, with **no
after-hours or next-day replay**. A new trading-day slot freezes a new
universe and starts a new run. If the category prerequisite failed, mark
detail coverage blocked/incomplete and do not fabricate a smaller universe.

The detail run is aggregate-complete only if all currently selected
category source snapshots were fully validated, every eligible distinct
target has exactly one individually valid observation for **that run**,
and all exclusions have confirmed reasons. A complete zero-target run is
valid only when its category and listing prerequisites completed and
there were genuinely no eligible EPICs. On an interest/catalogue revision
change, mark the frozen run `Superseded` for current-universe selection;
retain its individually valid historical observations, and never relabel
the current universe `Complete` from old rows. An analysis job requiring
full coverage should consume a complete run and its member observations,
not join arbitrary latest-good EPIC rows from different slots. This avoids
mixing incompatible quote times or selection universes.

Instrument exclusions need two distinct meanings: **not in current selected
listings** (derived from fresh catalogue/listing membership, with history
intact) and **IG-confirmed unavailable/expired** (persisted reason/evidence).
Do not interpret a non-tradeable category flag, a transient 404 for an
entire malformed batch, or a temporarily closed market as proof of
permanent ineligibility. A documented IG "epic not found" response for one
specific EPIC warrants provider-contract verification and an explicit
exclusion/reconciliation state, not deletion of its last good details.

## Capacity and freshness decision

Let `U` be distinct selected EPICs, `C` selected categories, `P_i` the listing
page count for category `i`, and `S` session calls. With one market request per
EPIC, a no-retry slot costs at least `1 + S + sum(P_i) + U` HTTP requests
(one category catalogue call), before any re-login or retry. For example,
one selected category with 384 instruments in three pages requires **at
least 389 calls if one session is used for listings and details**:
`1 + 1 + 3 + 384`; existing listing code starts a new session per category,
so the real baseline may be higher. Four slots multiply the daily cost.
With the documented bulk limit of 50, the same 384 EPICs instead require
`ceil(384 / 50) = 8` market calls, so the theoretical no-retry shared-session
minimum is `1 + 1 + 3 + 8 = 13` HTTP requests; current per-category session
creation can raise this. This **does not** demonstrate that the bulk v2
response has every desired field, that URL length is safe, or that any
operator-approved allowance is large enough.
The current 15,000-instruments-per-category ceiling is a *listing validation
cap*, not an approved market-detail request budget. At 250 ms/call 384 detail
calls alone need at least 96 seconds, without HTTP latency, DB work, other
replicas, or provider per-minute limits. At 30 account non-trading calls/min,
384 detail calls alone need at least 12.8 minutes even with perfect pacing.
These **30/min account and 60/min app defaults** are from
[IG's FAQ](https://labs.ig.com/faq.html); they are not the platform's daily
operator-approved allowance and may differ by account/approval.

**Do not enable an unbounded "all selected EPICs per slot" fetch by default.**
First establish supported batch semantics, batch-size/URL limits, per-call
quota accounting and actual selected-universe size, then compute whether full
coverage fits the configured allowance and active window. If batching is not
safe/supported, expose a capacity-blocked or incomplete state and mark
unrefreshed EPICs out of date rather than silently fetching a subset and
calling it complete. The recommendation is to attempt complete coverage
after each due instrument-list collection slot under its existing trading
window and allowance; if measured capacity cannot support that cadence,
make the limitation visible rather than silently switching to a slower
schedule. Quote snapshots remain time-of-fetch only.

## Operator UI research and recommendation

### Existing navigation and presentation

The Viewer-protected
[Market categories page](../../../src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor)
shows category interest, the existing listing outcome and request budget;
category links open
[`/market-categories/{CategoryCode}/instruments`](../../../src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategoryInstruments.razor).
That page loads **50 listing rows per SQL page**, renders one named
`RadzenTabsItem` per row, and shows only the selected instrument's current
listing fields as key/value pairs. It retains selected EPIC on tab changes
and shows an explicit stale-cursor warning if a new listing version appears.
Its [presenter](../../../src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategoryInstrumentsPagePresenter.cs)
owns bounded page/cursor state and loads category and schedule metadata
separately; a failure in metadata does not hide the instrument page. The
[Web API client](../../../src/TNC.Trading.Platform.Web/PlatformApiClient.cs)
uses delegated Viewer scope, URL-escapes category/cursor and reads only the
platform API. The current page formats UTC values with `ToLocalTime()`
inside Blazor Server: this means the **server's** local time zone, not
necessarily the browser operator's. A new detail page should avoid
silently describing that as the viewer's time zone. Styling already provides
responsive [`panel`, `platform-key-value-grid` and scoped tab CSS](../../../src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategoryInstruments.razor.css);
the app hosts the free Radzen theme and uses interactive server rendering.

**Recommendation: keep these category and listing surfaces, but put the
large Market Details payload on a dedicated read-only instrument page.**
Putting currencies, margin bands, six dealing-rule objects, special info
and snapshot data into all 50 tab panels or the listing response would
overwhelm the tab experience and turn one SQL page into 50 expensive detail
reads. The selected tab should keep its existing compact listing values
and add a single **Market details status** line and a clearly named
`View saved market details` link for that EPIC. A stable route such as
`/market-categories/{categoryCode}/instruments/{epic}/market-details` allows
deep links and Back navigation without altering the existing category page
or tab-paging contract. The link is available to Viewers even when no
details exist, so the destination can explain the state. Do not put market
details on the main navigation menu: the user's path remains category >
instrument > details.

This is preferable to an always-expanded section in every tab (large
payload, repeated rendering and no stable deep link), or a modal (poor
fit for multi-section data, long tables, history and direct navigation).
An inline **status/link**, rather than a full inline panel, preserves the
existing instrument tabs and their tested keyboard navigation.

| Surface | Recommended visible addition | Data source / scope |
| --- | --- | --- |
| `/market-categories` | Add a **Market details coverage** column for interested categories (`Complete 34/34`, `Collecting 27/34`, `Out of date`, `Not collected`, or `Blocked`, with exclusions separately counted), and a safe last-complete timestamp. Keep existing instrument outcome and daily request quota distinct. For unselected/dormant categories show `Not followed` rather than misleading coverage. | Extend the existing category/status SQL projection with compact aggregate data; avoid per-category follow-up HTTP calls from Web. |
| Existing instrument page header | Summarize this category's detail coverage and retrieval window separately from **Last successful collection** of instrument listings; link to collector status if capacity/schedule blocks it. | Bounded SQL status query; no direct IG access or timer-based poll. |
| Selected instrument tab | `Saved market details: Complete / Out of date / Not collected / Excluded`, last successful detail retrieval and one details link. Preserve the listing bid/offer and its existing "not a live quote" notice unchanged. | Add only minimal availability fields to the 50-row listing page response, joined/batched by `(environment, epic)`; **no N+1 Web/API or EF queries** and no nested full details. |
| New detail page | Summary/status plus structured sections below; a saved-data Reload control, never an IG refresh. | One protected SQL-only detail read for the selected EPIC, lazily loaded on navigation. |
| Historical detail (if exposed to operators) | A collapsed **Saved observations** section listing bounded timestamps and outcomes with older/newer cursor paging; selecting one shows its source run and exact saved values with an explicit historical label. No unbounded chart/history download. | Separate Viewer-authorized, environment-scoped SQL history query; never default-load all history with the detail page. |

### Detail page information architecture

Reuse the existing `panel` layout, responsive `RadzenRow`/`RadzenColumn`
and accessible key/value lists, plus the repository's native
[`PlatformAccordionSection`](../../../src/TNC.Trading.Platform.Web/Components/Layout/PlatformAccordionSection.razor)
for lower-priority groups. Reserve the existing Radzen instrument tabs
for **navigation among instruments**, not a second level of 50-field tabs.
The free [Radzen Tabs reference](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenTabs)
supports selected index and keyboard navigation; client-mode rendering
is not a substitute for fetching one selected SQL record. Prefer semantic
HTML `<dl>`, `<table>`, `<time>`, headings and links inside the panels to
a dense generic grid for one instrument. A
[Radzen DataGrid](https://blazor.radzen.com/datagrid) is useful only if
later historical comparisons genuinely need server-side sorting/filtering;
it is unnecessary for six dealing-rule rows.

| Page group, in display order | Fields / presentation |
| --- | --- |
| Breadcrumb and title | `Market categories > {CategoryCode} > Saved instruments > {instrument.name}`; EPIC as selectable text, applied IG Demo/Live environment, category interest state. Back link to the category page; where possible preserve the previously selected tab/page in in-memory navigation, but a direct URL must work independently. |
| Coverage and provenance banner | Text **Saved market details**, `Complete`/`Out of date`/`Not collected`/`Excluded`/`Incomplete`, last successful detail retrieval in an **explicitly labeled time zone** with UTC ISO value in `<time datetime>`, current slot/run coverage (`27 of 34 eligible; 2 excluded`), API version and a clear `Listing last retrieved` timestamp if different. Show the most recent safe failure and next scheduled collection check if relevant. These are *separate* facts; never imply that a recent listing refresh also refreshed detail terms. |
| Quote snapshot (prominent but explicitly historical) | Provider `marketStatus`, bid, offer, high, low, net and percentage change, delay, provider `updateTime`, binary odds, decimal/scaling factors, extra controlled-risk spread. Label with this **detail observation's** retrieval time. Do not combine its quote fields with the category listing's older/newer bid/offer as though they were simultaneous. No buy/sell buttons or "live price" styling. |
| Instrument summary | Name, EPIC, `marketId`, expiry, type, unit, lot size, contract size, value-of-one-pip, `onePipMeans`, trading and risk capability flags, news/chart codes, country, opening/expiry/rollover details when present. `streamingPricesAvailable` is a provider fact, not an instruction to subscribe. |
| Dealing rules | Labeled rows for `minStepDistance`, `minDealSize`, minimum normal/controlled-risk stop distances, maximum distance, controlled-risk spacing; display each `value` **beside its own `unit`**, followed by order/trailing-stop preferences. Plain-language field labels with original IG enum values when needed; do not infer risk limits from historic rules. |
| Margins and currencies | Margin factor **and its unit**, limited-risk premium, slippage factor, one currency row per IG currency (`code`, `symbol`, `baseExchangeRate`, `exchangeRate`, `isDefault`), and a margin-band table with Min, Max, Margin, Currency. For `max: null`, show `No upper bound`, not zero. Keep returned band order and currency unit; no invented conversions or derived deposits. |
| Provider notices / technical details | Ordered `specialInfo` as escaped plain text, optional opening-hours/expiry/rollover information, and a collapsible provenance block (`marketId`, source endpoint/version, run ID if safe to display, retrieval UTC, provider time). Unknown provider text is data, not markup, hyperlinks, executable instructions, or a trading recommendation. |

For optional fields show `Not supplied by IG` (or `Not applicable`
only when that meaning is actually known), not a blank or `0`. Render a
genuine numeric zero as `0`; preserve precision of decimal quantities,
associate currency symbols with their currency code rather than assuming
`$` means a specific currency, and retain provider text such as
`valueOfOnePip` without speculative arithmetic. The supplied
`snapshot.updateTime` is unzoned text: **do not convert it to UTC/local**;
use only platform `RetrievedAtUtc` for chronological ordering and
deliberate time-zone conversion. Prefer the configured trading-schedule
time zone with its abbreviation/offset (or explicit UTC) across this page
and the existing instrument timestamp labels; show a browser-local time
only if an explicit browser-zone mechanism is added. Show `delayTime`
and provider status alongside the snapshot.
Quoted market status `TRADEABLE`, `forceOpenAllowed`, category
`NonTradeable`, and `controlledRiskAllowed` must remain provider facts,
not platform trade authorizations. Avoid color alone as a status indicator;
include explicit text and accessible labels. Margin bands and currency
tables need column headers (`<th scope="col">`), horizontal overflow or
stacked labeled rows on narrow screens, and legible light/dark contrast
without fragile Radzen-internal CSS overrides.

### UI states, consistency and API contract

The page needs both **individual observation state** and **aggregate run
state**: a complete EPIC can be viewed during an incomplete run, but any
selection/analysis predicated on *all* instruments must say the run is
incomplete. The recommended copy and behavior are:

| State | What a Viewer sees |
| --- | --- |
| Known EPIC, never collected | `Market details not collected yet`; no fabricated instrument/rule values; last listing and next scheduled check if known. |
| Detail run active / partial | `Collection in progress` with aggregate counts. An individually complete EPIC shows its observation and UTC retrieval time; an outstanding EPIC keeps last good details under an **Out of date** banner, or `Not collected` if it has none. |
| Last run failed, budget paused or schedule closed | Last good details stay readable; banner names the safe reason and **Out of date** state. The button reads `Reload saved data`, never `Refresh from IG`. Do not continuously poll or accidentally trigger after-hours provider requests. |
| IG-confirmed excluded / category no longer followed | `Excluded from new collection` or `Category not followed`, with safe reason and exclusion/last-good times; history remains clearly labeled. A temporary market closure must not receive the permanent exclusion label. |
| Version changed while browsing | Preserve the visible listing tab/details and explain that the saved source changed; offer `Reload latest saved details` or return to the category's first listing page rather than silently attaching detail data to another EPIC. |
| Category or EPIC genuinely absent | `404` from a **current category** route; offer Back to categories. Historical records, if exposed, need a separate scoped read path so retired/disappeared categories don't suddenly become current. |
| API/metadata failure | Separate `role="alert"` for detail failure and `role="status"` warning for collection metadata, preserving already loaded saved details; never hide a valid snapshot just because status failed. `401`/`403` continue through the existing auth/scope flow; an unsupported applied environment gets an explicit safe unavailable message. |

For a current known EPIC without observations, the detail API should
return a typed `200` state such as `NotCollected` rather than confusing
it with a `404` for an unknown EPIC; likewise, return known stale/excluded
states with the last good observation if available. Use `409` when an
optional listing snapshot version included in a navigation link is no
longer current, with a clear reload action. The route parameters need
EPIC/category URL escaping, environment and current-membership validation,
Viewer authorization and no direct IG dependency. Deep links should not
depend on an opaque listing cursor being available; optionally carry a
listing version for drift detection. Do not put a complete nested market
detail DTO on every instrument page row.

Suggested transport/presentation types are
`MarketDetailAvailabilityResponse` (small per-listing-row projection),
`MarketDetailResponse` (state, current observation, source/coverage
metadata), `MarketDetailHistoryPageResponse` (bounded cursor), and Web
`MarketDetailViewModel`, `MarketDetailPagePresenter`, and
`MarketDetailHistoryPageViewModel`. Keep API DTOs in the API layer, Web
view models in Web, and the inward read handler/port in Application.
Extend `PlatformApiClient` with Viewer-scoped SQL-only GET methods, not
IG credentials or provider DTOs. A dedicated
`MarketDetails.razor`/`.razor.css` page under
`Components/Pages/MarketCategories` aligns with existing routes and
feature CSS; preserve the current instrument presenter and tabs rather
than duplicating their pagination logic. Add lightweight status fields
to `MarketCategoryViewModel` and
`MarketCategoryInstrumentViewModel` **only** when backed by bounded
SQL projections. Explicitly distinguish a globally complete run from
this EPIC's validated observation in each response shape.

Blazor performance guidance recommends limiting work rendered in repeated
lists ([Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0)).
The existing page bounds its tabs to 50; **don't** append all nested
detail panels to each tab. Lazy-load the detail page, render only selected
sections, page historical observations on the server, use `@key` for
repeated currency/band/history rows when stable IDs exist, and cancel
outstanding loads on page disposal or parameter changes so rapid
navigation cannot show another EPIC's data. Do not claim a client-only
filter over 50 instruments searches the entire interested category;
if all-category search becomes necessary, it needs a bounded SQL-backed
query rather than a misleading browser filter.

### UI-specific verification

Extend the existing
[bUnit market-instrument tests](../../../test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MarketCategoryInstrumentsTests.cs)
and [API client tests](../../../test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs)
with representative stored responses; no IG credentials or network. Cover
Viewer/Operator scope, category-to-EPIC deep links and back navigation,
selected-tab preservation where available, cancellation on rapid tab/
route changes, and **one selected details GET instead of 50**. Check
not-collected, complete, individually complete while run incomplete,
out-of-date with last good data, confirmed exclusion, missing membership,
409 stale listing version, and independent status failure without
blinking the detail panel. Verify optional/null vs zero, each dealing-rule
unit, `max: null`, multi-currency rows, `specialInfo` HTML escaping,
different listing/detail timestamps, unzoned IG `updateTime`, mobile table
layout, accessible headings/focus/keyboard tab navigation, and snapshot
disclaimer. The existing
[wiki operator guide](../../../docs/wiki/operator-guide.md) should
describe the new screen and status semantics **when implemented**;
this research does not change the application's present behavior.

## Read surface, safety and verification considerations

Expose saved details through a Viewer-authorized SQL-only API keyed by EPIC
and applied environment (with category membership validated where accessed
from a category page), with clear retrieval UTC, source and completeness/
staleness fields. Include safe collector progress and detail-call budget in
the existing status/configuration projections without changing their meaning.
Use explicit `404` for unknown EPIC/category, a distinct not-collected state
for known EPICs without details, and safe unavailability semantics for an
unsupported applied environment. Rendering must label snapshots as historical
and keep provider text escaped; never imply `marketStatus: TRADEABLE` grants
platform authorization or that saved dealing rules are live.

Validation should cover the contract's nullable/nested shape, exact EPIC
identity and duplicate handling, large strings/numbers, no-source-credentials
logging, supported Demo/Live profile isolation, rate limits, timeouts,
unauthorized-session replay, quota counting, interest/category mutation,
lease expiry, DST/schedule boundary, restart/missed slots, empty universe,
multi-category duplicate EPIC, partial coverage and last-good preservation.
Use controlled HTTP fixtures and SQL integration tests like the existing
[quality suite](../../../docs/wiki/testing-and-quality.md), not live IG
credentials. A dedicated load/capacity exercise is necessary before promising
full universe coverage at the configured cadence.
In particular, assert the **exact run invariant** in SQL-backed tests:
two selected categories sharing an EPIC produce one target/observation
and two memberships; an empty *validated* universe completes; one
unresolved eligible EPIC prevents aggregate completion while valid peers
remain individually readable; expiry/unavailability is an explicit counted
exclusion rather than an invisible dropped target. Test worker restart
and stale-lease reentry within the same active slot, duplicate batch replay
without duplicate observations, late responses after window closure,
interest/catalogue revision changes, retirement/category removal without
historical cascade, and quota usage including every session, batch and
fallback/retry request. Verify API timestamps and out-of-date labels against
saved UTC retrieval instants rather than the provider's unzoned time text.

## Confirmed product requirements and outstanding research

- **Complete selected coverage:** Every eligible EPIC in every currently
  interested category must be attempted and validated. Aggregate completeness
  requires every eligible observation, because the application will use the
  details to choose instruments for closer monitoring.
- **Individual versus run state:** Fully downloaded, validated EPIC details
  are individually complete and usable. A run is incomplete until all
  eligible EPIC observations are valid; continue bounded attempts and
  explicitly mark unrefreshed details out of date. Surface coverage and
  exclusions rather than disguising a partial run as complete. Any
  application decision requiring *all* selected instruments should depend
  on aggregate completeness, not on the presence of some valid EPICs.
- **History:** Keep historical details available in SQL for application
  analysis, including observations for later-excluded instruments. Do not
  inherit the unrelated 90-day operational-record purge. Monitor growth;
  any later retention/archive policy must preserve this analysis requirement.
- **Exclusions:** Exclude IG-confirmed unavailable/expired markets and
  instruments no longer in current selected listings from further active
  collection while retaining their history and status. A no-longer-listed
  EPIC is absent from the *next* frozen universe; it does not inflate that
  run's provider-excluded count. An EPIC present in the frozen universe but
  confirmed unavailable/expired is counted explicitly as excluded.
  Reconcile membership if a market later becomes eligible again; a transient
  failure alone must not cause permanent exclusion.
- **Still unknown (provider contract):** Whether bulk v2 `filter=ALL` covers
  every required field across Demo/Live and market types. Verify against
  controlled fixtures; use single-EPIC requests for confirmed gaps rather
  than treating incomplete fields as valid observations.
- **Licensing:** No specific licensing/retention restriction was identified
  in the product feedback. This is *not* evidence of a contractual right
  to retain or redistribute IG market data. The applicable API/account and
  market-data agreements remain an external compliance check, especially
  before external distribution.

## Evidence

Repository behavior: [runtime](../../../docs/wiki/runtime-behavior.md),
[architecture](../../../docs/wiki/architecture.md),
[IG integration guide](../../../docs/wiki/ig-day-trading-with-ig-apis.md),
[operator guide](../../../docs/wiki/operator-guide.md), and linked source
above. The most relevant file-and-line anchors are:

| Claim | Evidence |
| --- | --- |
| Trading-window slot semantics and no catch-up | `docs/wiki/runtime-behavior.md:48-91`; `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentSchedulePolicy.cs:20-101` |
| Category-first selection and per-category attempts | `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs:111-170`; `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCyclePolicy.cs:10-35` |
| v2 session, v1 listing pages and shared per-request budget | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:220-337`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentCycleStore.cs:383-427` |
| Current listing vs retained observation layout and identity | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:424-481`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/MarketCategoryInstrumentEntity.cs:3-26` |
| SQL-only protected Viewer page, cursor and status | `src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentEndpoints.cs:14-53`; `docs/wiki/api-reference.md:84-136` |
| Why detail work needs resumable state and a shared budget extension | `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs:111-213`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentRequestBudget.cs:29-84` |
| Why detail history cannot depend on mutable category rows | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategorySnapshotStore.cs:101-130`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:424-481` |
| Retirement distinguishes current state from retained historical evidence | `docs/wiki/runtime-behavior.md:791-819`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlBrokerEnvironmentCatalogService.cs:161-180` |
| Existing 50-tab listing UI, timestamp display and stale-cursor handling | `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategoryInstruments.razor:65-151`; `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategoryInstruments.razor:156-213`; `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategoryInstrumentsPagePresenter.cs:10-150` |
| Viewer-scoped client reads and reusable UI patterns | `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:203-245`; `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor:15-111`; `src/TNC.Trading.Platform.Web/Components/Layout/PlatformAccordionSection.razor:1-15` |

Official IG references: [API introduction](https://www.ig.com/uk/trading-platforms/trading-apis/how-to-use-ig-api),
[single-market versions](https://labs.ig.com/reference/markets-epic.html),
[bulk-market v2](https://labs.ig.com/reference/markets.html),
[REST auth/errors](https://labs.ig.com/rest-trading-api-guide.html), and
[default request limits](https://labs.ig.com/faq.html). These establish
documented contracts, not a guarantee of live account behavior. IG does not
explicitly guarantee a maximum snapshot age or a universal timezone for
v3 `updateTime` in the cited reference; keep the provider text separately
from the platform UTC fetch instant. No API-specific permission to retain
or redistribute market data was verified; the intended historical SQL
retention should be checked against applicable agreements rather than
assumed permitted or prohibited. The single-market JSON analyzed
above is the response supplied in the request, not a network call made by
this research.

UI references: [Radzen Tabs](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenTabs),
[Radzen DataGrid](https://blazor.radzen.com/datagrid), and
[Microsoft Learn: Blazor rendering performance](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0).
