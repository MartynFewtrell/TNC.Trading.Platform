<!-- markdownlint-disable-file -->
# Task Research: IG category page-count and session-denial failures

Research date: 2026-10-04.

Updated following the user's 2026-10-04 clarification that market-data collection is not time critical and their proposals for a 60-second category pause and page size 25.

## Task implementation requests

Investigate the new COMMODITIES page-zero expected-70/received-41 failure and CRYPTOCURRENCY session-stage HTTP 403. Explain why the additional UI diagnostics have not resolved collection, distinguish evidence from hypotheses, and recommend a safe resolution.

## Scope and success criteria

* Scope: category-instrument adapter, provider pagination contract, authentication/error handling, retry accounting, retained snapshots, and the screenshot's separate detail-coverage state.
* Freshness is not time critical. Prefer measured, conservative request pacing over minimizing elapsed collection time, while retaining cancellation, budgets and lease safety.
* Research only: no application edits, provider requests, database changes, credential access, or application start/stop operations.
* Findings describe the current workspace, not a verified fingerprint of the running application.
* Success criteria: trace each displayed diagnostic to source, verify relevant public contracts, identify missing evidence, select a resolution strategy, and specify acceptance tests.
* Preserve earlier research and the existing dirty worktree. Investigation reported HEAD `4486b9a`, with 37 modified and three untracked files before this report, including diagnostic changes. Counts are an observation, not an instruction to stage those files.

## Outline

1. Conclusions and evidence limits
2. COMMODITIES calculation and provider contract
3. CRYPTOCURRENCY session denial
4. Saved state and superseded detail coverage
5. Recommended implementation and alternatives
6. Acceptance tests and remaining investigation

## Conclusions

| Observation | Verified explanation | Remaining uncertainty |
|---|---|---|
| COMMODITIES expected 70, received 41, page index 0 | The gateway compares the deserialized instruments array length against an offset-derived expected count before mapping or filtering. Under current default execution, its one smaller-page restart has already happened. | Actual initial/restarted metadata and IG's count semantics for this response are unavailable. |
| CRYPTOCURRENCY HTTP 403, operation `session` | A version-2 session POST was rejected. The category label does not identify the denied HTTP resource. The listing gateway discards the unsuccessful body. | Initial authentication versus reauthentication, and IG's actual error code. |
| Attempts: 1 | One category attempt includes internal login, paging, smaller-page restart, and possible 401 recovery. These two failure kinds are nonretryable under current policy. | It is not a count of HTTP calls. |
| Crypto collected yesterday but failed today | Last successful snapshot and latest attempt are separate persisted records. | The screenshot does not prove yesterday's snapshot is complete under a newly clarified provider contract. |
| Detail coverage `Superseded / RunProvenanceChanged` | Detail-run provenance changed; this is not the meaning of the session 403 and does not erase the retained listing. | Which revision changed, when, and whether a same-slot run-identity collision caused this instance. |

The additional UI fields expose existing failures; they do not repair the provider interaction. The warnings should not be suppressed or converted to successful collection.

## Research executed and evidence log

### Repository investigation

Read-only delegated investigation traced the current adapter, coordinator, persistence/API/UI mapping, tests, previous research, relevant repository instructions, and local history/status.

Principal files:

* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs`
  * Lines 11-23: page size and hard bounds.
  * Lines 61-161: session acquisition, initial page, bounded reauthentication, and smaller-page restart.
  * Lines 164-272: traversal, metadata/count checks, and aggregate completeness.
  * Lines 332-424: session and category HTTP requests and discarded unsuccessful bodies.
  * Lines 491-496, 517-588, 646-660, 683-690: metadata validation, mapping, count formula, status classification, and raw response model.
* `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs`
  * Lines 435-478: sequential categories and continuation after failure.
  * Lines 616-687, 713-759: attempt accounting and complete-only publication.
  * Lines 870-883: rejected 403 mapped to `ProviderAccessDenied`.
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketDetailsGateway.cs`
  * Lines 550-591, 674-706: existing bounded parsing and safe-code logging pattern.
* `src\TNC.Trading.Platform.Web\Components\Pages\MarketCategories\MarketCategories.razor`
  * Lines 105-181: separate successful collection, latest attempt, and detail coverage.

The independent contract investigation fetched IG's public documentation and pinned third-party implementation. A focused follow-up checked the raw category schema and separated previous empirical observations from synthetic test fixtures.

No automated tests were run: this request changes only research documentation. No fresh authenticated response was obtained. A non-network arithmetic check confirmed the reconstructed count calculation; it does not reproduce IG behavior.

### Public sources

| Source | What it establishes | Limit |
|---|---|---|
| [IG category instruments reference](https://labs.ig.com/reference/categories-category-id-instruments.html) | GET v1; zero-based page number; default page size 150, allowed 1-1000; documented metadata fields are `pageNumber` and `pageSize`. | Does not document `totalResults`, `totalPages`, full nonfinal pages, filtering order, or snapshot consistency. |
| [IG REST reference index](https://labs.ig.com/rest-trading-api-reference.html) | Categories and category instruments are current documented discovery routes. | Absence of the old market-navigation route from documentation does not prove server-side removal. |
| [IG session reference](https://labs.ig.com/reference/session.html) | Session versions and documented denial/allowance/API-key errors. | Status alone cannot identify the incident's cause. |
| [IG REST guide](https://labs.ig.com/rest-trading-api-guide.html) | Version-2 CST/security-token authentication and JSON `errorCode` response convention. | Not an incident log. |
| [IG FAQ](https://labs.ig.com/faq.html) | Token invalidation during weekend maintenance and token handling guidance. | Does not establish fresh-login 403 as a weekend-maintenance symptom. |
| [IG market search reference](https://labs.ig.com/reference/markets-searchterm.html) | A different paginated search API with documented totals. | No guarantee of category-set equivalence. |
| [IG batch markets reference](https://labs.ig.com/reference/markets.html) | Lookup of known EPICs, not complete category discovery. | Cannot discover missing identifiers. |
| [Pinned trading-ig category implementation](https://github.com/ig-python/trading-ig/blob/1f307f006aa5a8f06870c5b32c99e2189508bd13/trading_ig/rest.py#L1450-L1521) | Holds page size fixed, appends returned instruments, follows runtime `metadata.totalPages` through the last page even when an earlier page is short. | Third-party implementation, not a completeness guarantee; lacks this repository's strict validation. |

### Project conventions

Research followed `.github\copilot-instructions.md`, relevant configuration/test/architecture guidance, and research-output conventions. No unsolicited work package or implementation plan was created.

Future implementation must retain explicit failure reporting, secret-safe diagnostics, unit-first tests with requirement comments and repository-standard names, complete-only publication, and updates to affected `docs\wiki` pages. IG provider sessions are distinct from the application's Keycloak/Entra authentication; changing application identity providers is not a remedy.

## COMMODITIES: what 70 and 41 mean

### Exact execution

The current gateway requests:

```text
GET categories/COMMODITIES/instruments?pageNumber=0&pageSize=150
Version: 1
```

It computes:

```text
expectedRows = min(requestedPageSize,
                   max(0, metadata.totalResults - pageNumber * requestedPageSize))
actualRows   = response.instruments.Count
```

Source: `IgMarketCategoryInstrumentsGateway.cs:242-247,398-413,647-650`, at the full path recorded above.

An initially short nonempty first page triggers one restart using the initial raw array count as the new requested page size. Initial metadata is replaced with the restarted response's baseline. It does not repeatedly shrink.

Under the default size 150, an initial expectation of 70 with 41 returned would cause a restart at 41 rather than directly yield the reported terminal page-zero mismatch. Therefore the supplied terminal expected-70/received-41 result indicates that the restart was already consumed, assuming the running binary follows current source/defaults.

A plausible, not freshly captured, sequence is:

| Request | Requested size | Reported total | Returned count |
|---|---:|---:|---:|
| Initial page zero | 150 | 290 | 70 |
| Restarted page zero | 70 | 290 | 41 |

This yields the reported result. Other metadata combinations can also produce expected 70. The screenshot does not prove total 290, the original count 70, or the exact request sequence.

### This is not application filtering

`IgMarketCategoryInstrumentsGateway.cs:242-262,517-588,683-690` checks array cardinality before mapping. It does not remove nontradeable, expired, closed, duplicate, or invalid rows to produce 41.

* Null entries contribute to the array count, then fail field validation if cardinality passes.
* Duplicate EPIC/name values produce explicit duplicate failures, not fewer array entries.
* Malformed JSON produces a JSON failure.
* UI/database paging is a separate path and does not calculate provider expected rows.

The accurate diagnosis is a returned array that violates the collector's pagination assumptions, not proven loss of 29 instruments by the application or provider.

### The important contract gap

The raw official category-reference metadata table contains:

```text
metadata: Paging metadata
  pageNumber: Current page number
  pageSize: Number of items per page
```

The fetched HTML contained zero occurrences of `totalResults` and `totalPages`. The page says "Returns all instruments for the given category", but does not specify exact account-visible totals, full nonfinal pages, offset/filter ordering, or stability across requests.

Nevertheless, runtime totals appear in earlier recorded responses and are consumed by both this adapter and `trading-ig`. Missing documentation does not make those fields invalid; it makes their stronger interpretation unverified.

The current adapter requires all of the following:

* `totalPages = max(1, ceiling(totalResults / requestedSize))`
* Exact offset-derived count on each page
* Stable metadata across accepted pages
* Matching final aggregate count
* Valid fields and unique identities

Sources: `IgMarketCategoryInstrumentsGateway.cs:125-129,233-272,491-496,560-568,646-650`.

An API that pages a broad universe before filtering could return sparse pages and broad totals. A provider defect or changing universe could also create these symptoms. None is established. The current fail-closed policy is conservative, but its per-page assumptions are not proven IG requirements.

### Previous observations must not be conflated

`.copilot-tracking\research\2026-10-04\ig-market-collection-failures-research.md:58-60` records a previous attachment containing HTTP 200, page 0, size 150, total pages 2, total results 290, and 70 counted EPIC entries. That attachment was not independently recounted in this investigation.

The same report at lines 43, 99, and 115 records a separate 70/42 mismatch and explicitly does not prove that it belongs to the captured response.

`test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:238-274` constructs a synthetic page-one failure using total 220, first page 150, final page 42, expected 70. It does not establish a real 70-plus-42 traversal of a 290-result category.

Other smaller-page recovery tests fabricate coherent sequences. No complete live fixed-size traversal was established by the inspected evidence. Earlier references to 65/38 are historical context, not a verified reproduction of this 70/41 incident.

## CRYPTOCURRENCY: session rejection, not established crypto restriction

### Exact route and phase

The operation marker is set around initial authentication and reauthentication:

`src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:40-41,61-65,79-82,146-150,194-198,332-378`.

The request is:

```text
POST https://demo-api.ig.com/gateway/deal/session
Version: 2
```

Source for environment addresses: `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgEndpointProfileResolver.cs:7-8,26-38`.

It creates a session using the configured credentials/API key; it does not send a cryptocurrency category to this route. The recorded operation does not distinguish initial login from login after an instruments request returned 401.

Consequently:

* Do not conclude the account is prohibited from trading crypto.
* Do not claim yesterday's token was reused. Each category invocation starts a fresh session; tokens are local to that invocation.
* Do not diagnose invalid credentials, allowance exhaustion, or weekend maintenance solely from 403.
* Do not borrow an error code from a different market-details response sharing a correlation ID.

### Root cause of missing diagnosis

Session errors at lines 360-367 and listing errors at lines 421-424 are converted from HTTP status without reading IG's error body. A 403 becomes nonretryable `Rejected` with reason `HttpStatus`.

`src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentFailure.cs:4-14` has no provider-error-code member.

`src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs:955-969` sets saved provider code to null.

`src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentStatusReader.cs:77-90` returns null provider code.

Adding UI text alone cannot recover a body that the adapter discarded. The current "cause is not classified" message is honest.

### What IG may return

Documented session 403 categories include allowance exhaustion, API-key invalid/disabled/restricted/revoked states, and unsupported product/account types. The JSON `errorCode`, not the status alone, distinguishes them.

Prioritize bounded parsing of known codes such as:

```text
error.public-api.exceeded-account-allowance
error.public-api.exceeded-api-key-allowance
error.public-api.exceeded-account-trading-allowance
error.public-api.exceeded-account-historical-data-allowance
error.security.api-key-disabled
error.security.api-key-invalid
error.security.api-key-restricted
error.security.api-key-revoked
error.public-api.failure.product-code-not-allowed
error.public-api.failure.stockbroking-not-supported
```

Treat the session reference's shared error table as possible classifications, not a prediction that every code is likely on this POST.

The existing detail adapter and `src\TNC.Trading.Platform.Application\Features\MarketDetails\MarketDetailProviderErrorCodes.cs:5-25` provide prior art for bounded, allow-listed parsing. Extract reusable parsing where appropriate rather than coupling listing contracts to detail-specific types.

Unknown code, absent code, malformed JSON, non-JSON, and oversized bodies should remain distinguishable diagnostic states without exposing arbitrary body content. The present incident proves none of those body shapes: the body was not parsed.

### Session traffic and retry

`src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentRetryPolicy.cs:3-39` allows two further attempts at 2/5-second backoff only for eligible retryable failures. Neither reported failure is currently eligible.

Categories execute sequentially, but independent adapters create their own sessions. `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgProviderRequestThrottle.cs:11-40,65-124,146-200` already combines process spacing with SQL-backed reservations and default account/application limits of 30/60 requests per minute.

Existing throttling does not prove the provider's actual allowance has not been exhausted, especially with other clients or different quota scopes. Equally, repeated login does not prove that a quota caused this response.

The 2/5-second policy is not the only retry layer: the production HTTP client inherits a standard resilience handler. Its internal retries can occur after one gateway throttle/budget reservation. See the follow-up analysis below; category-attempt counts and gateway reservations must not be described as exact wire-request counts.

Session reuse/single-flight is a possible follow-on improvement, scoped by environment, profile, credential revision and account context. It needs tested invalidation and bounded 401 recovery; introducing a global token cache without those safeguards is not an incident fix.

## Follow-up: slower collection and page size 25

### Is there a break between commodities and crypto

There is no deliberate category-boundary pause. Interested category codes are sorted ordinally, so COMMODITIES precedes CRYPTOCURRENCY, regardless of checkbox selection order. The next category starts after the previous category's completion/failure and coordinator checks.

Evidence:

* `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCyclePolicy.cs:26-42`
* `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:435-490`

The current SQL-backed throttle defaults are:

| Control | Default | Meaning |
|---|---:|---|
| Process-local spacing | 250 ms | Spaces reserved request slots, not a guaranteed interval between actual network sends across all instances. |
| Account reservation cap | 30 per preceding minute | Scope is the normalized login-identifier hash. |
| Application reservation cap | 60 per preceding minute | Scope is the API-key hash. |

On local quota exhaustion, the throttle waits for old reservations to expire and rechecks capacity. SQL transactions coordinate counts across instances, but process-local spacing is not distributed smooth pacing. The no-database constructor only spaces requests.

Source: `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgProviderRequestThrottle.cs:11-40,66-232`.

[IG's published limits](https://labs.ig.com/faq.html#limits) describe account/application non-trading allowances per minute. They do not specify an exact reset-at-the-minute-boundary algorithm. Trading and historical-price allowances are different constraints; a 60-second delay is not a universal quota reset.

### Would waiting 60 seconds help

Yes, it is a reasonable conservative diagnostic measure given the user's latency preference. A wait after COMMODITIES finishes and before CRYPTOCURRENCY's session POST would let earlier reservations age out of the application's one-minute window, provided no other callers consume those scopes meanwhile.

However, it is not a sufficient rate-limit strategy:

* Other adapters and external clients can use the same allowance during that pause.
* A large category can consume many requests before the boundary pause.
* A 403 caused by key/account configuration will not be repaired by waiting.
* It does not explain or guarantee resolution of the commodities row-count mismatch.
* It does not replenish the separate daily approved request budget.

Use the pause as a cushion, not as proof of available allowance. Prefer steady pacing of the background non-trading workload, including login, every page and retries, while retaining shared quota enforcement. Do not introduce a global sleep that unnecessarily delays time-sensitive trading operations.

For the next diagnostic run, select a fixed page size of 25, a target minimum interval of 5 seconds between relevant request starts, and a 60-second pause between categories. The five-second interval is a deliberately conservative experiment, approximately 12 requests per minute when continuously active, not an IG requirement or a guarantee under shared traffic. Apply pacing at the actual-send/retry boundary and retain quota accounting; merely delaying coordinator calls is insufficient.

If the combined experiment succeeds, a controlled comparison varying one setting at a time is needed before claiming which change resolved which symptom. Retain the successful conservative profile during diagnosis rather than optimizing latency prematurely.

### Smaller pages: useful, but not fewer requests

Page size 25 is within IG's documented 1-1000 range and is worth trying. Smaller responses may avoid a size-dependent provider problem, but cannot correct broad/pre-filter totals or account restrictions by themselves.

Assuming an exact visible total of 290 and conventional coherent paging:

| Page size | Page GETs | Requests including one successful login |
|---|---:|---:|
| 150 | 2 | 3 |
| 25 | 12 | 13 |

The 25-row case would contain eleven pages of 25 and one of 15. It generates six times as many page requests, so slowing requests becomes more important, not less. Thirteen requests at five-second start intervals take at least roughly 60 seconds from first start to last start, plus final response time and any additional waits/retries. The category-boundary pause is additional.

These are conditional calculations, not measured IG results. The supplied screenshot still does not establish that the incident's total was 290 or that every advertised result is account-visible.

The next diagnostic must keep size 25 fixed. Merely changing the constructor default does not do that: the current short-page branch would still restart at the smaller returned count. Bypass that adaptive restart deliberately in the diagnostic path.

Page size currently defaults to 150 through a constructor parameter used by tests; production dependency injection does not bind a page-size setting. Adding an appsettings entry alone would have no effect.

Evidence:

* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:11-21,125-155,595-603`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\DependencyInjection\PlatformInfrastructureServiceCollectionExtensions.cs:148-150`

### What iterating until the correct total must mean

Request the bounded advertised page sequence at the fixed size, even when a page is short. Check returned page identities, metadata stability, row validity and cross-page duplicates; compare the final unique count with the expected total only under a verified exact-visible-total contract.

Do not:

* Stop at the first short page.
* Keep requesting pages beyond the advertised range indefinitely until the counter reaches 290.
* Count duplicate rows toward the target.
* Merge unrelated retries/restarts into one apparently complete snapshot.
* Publish a short final union as complete merely because every advertised page was visited.

If all advertised pages have been fetched and the union is still short, report incomplete/unknown and retain the last-good snapshot. That outcome is evidence about the provider contract, not an instruction to keep downloading forever.

### Lifecycle, page bounds and hidden retries

Slower collection is a valid requirement, but adding sleeps alone is unsafe.

1. The current limit is 100 pages and 15,000 results. With conventional 25-row paging, the page bound limits a collection to 2,500 results. Preserving the existing 15,000-result ceiling could require 600 pages; that requires an explicit bounded capacity/budget decision, not silently removing the limit.
2. The category cycle lease is two minutes and the parent full-run lease is five minutes. Request reservations renew the cycle lease, but per-request parent validation does not renew the parent lease. A long category can therefore outlive the parent lease. Sleeps and paging need cancellation-aware renewal/checkpoints or a tested heartbeat covering both leases.
3. Schedule continuation is not uniform: coordinator logic permits some admitted work after schedule close, while the real gateway still uses `ScheduleWindowEndUtc` for throttle waits and expiry checks. Clarify and test actual HTTP-path semantics before making collection substantially slower.
4. The daily approved request budget counts login and pages. Size 25 can exhaust it sooner; waiting does not restore daily capacity.
5. The standard HTTP resilience handler can retry within `SendAsync`, after a gateway reservation. Quota accounting/pacing must cover these actual attempts or explicitly eliminate competing retry ownership for these clients.

Evidence:

* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:20-21,341-346,394-399,467-473`
* `src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:30-31,189-193,303-310,765-793`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentRequestBudget.cs:21-35,84-108`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDataFullRunLeaseGuard.cs:61-75`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDataFullRunStore.cs:347-379`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs:524-552`
* `src\TNC.Trading.Platform.Api\Program.cs:36`
* `src\TNC.Trading.Platform.ServiceDefaults\Extensions.cs:42-48`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\DependencyInjection\PlatformInfrastructureServiceCollectionExtensions.cs:143-167`

[Microsoft documents standard resilience defaults](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience#standard-resilience-handler-defaults) including retries for transient responses/exceptions, a 10-second attempt timeout and a 30-second total timeout. [Retry-After handling](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.http.resilience.httpretrystrategyoptions.shouldretryafterheader) is not a shared SQL cooldown. Confirm the effective installed pipeline when implementing pacing; a deliberate long wait must not inadvertently consume a short HTTP-operation timeout or cause another layer to retry it.

Existing gateway unit tests use a bare `HttpClient` and a zero-delay throttle, so those tests alone do not establish production wire pacing or handler retry accounting:

`test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:958-971`.

### Revised practical recommendation

Adopt the user's low-urgency requirement. The next experiment should use 25-row fixed pages, conservative five-second request pacing, and a 60-second inter-category pause, with safe 403 parsing and metadata diagnostics enabled. Keep it bounded and nonpublishing until completeness is established.

For production, prefer sustained, shared-scope request pacing and classified cooldown over dependence on category-boundary pauses. Keep page size and any pause operator-managed under existing SQL configuration conventions, validate their interaction with bounds/budgets, and preserve historical snapshots on incomplete outcomes. The 60-second pause can remain if its small latency cost is acceptable, but must not replace the limiter.

## Saved snapshots and superseded detail coverage

Failed attempts do not replace the successful instrument snapshot:

* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentSnapshotStore.cs:296-367`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs:907-974`
* `src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentStatusReader.cs:45-90`

This explains the October 3 crypto success beside the October 4 failure.

Detail-run acquisition uses environment/day/scheduled-slot identity and compares endpoint profile, catalogue/interest/schedule revisions, and window end. A difference supersedes the existing run, releases its lease, and returns no replacement under that identity:

`src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailRunStore.cs:121-140,170-176`.

The displayed coverage is selected for the retained listing collection, not necessarily the latest attempt:

`src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailReadStore.cs:346-405`.

A related recovery risk deserves a separate reproduction: category refresh increments catalogue revision even for unchanged content (`EfMarketCategorySnapshotStore.cs:181-197`, same persistence directory), while the detail coordinator reuses a scheduled slot (`src\TNC.Trading.Platform.Application\Features\MarketDetails\MarketDetailCollectionCoordinator.cs:69-73,120-146`). Another update in the same slot can supersede an old run without admitting a replacement.

If reproduced, use an explicit new update/run generation while preserving immutable historical provenance. Do not remove provenance checks or rewrite the old run to appear current. This is a source-established possible mechanism, not a confirmed explanation of this screenshot.

## Selected resolution strategy

### Stage 1: implement safe diagnosis and exact regressions

1. Add bounded provider-error parsing to listing and session failures, reuse existing safe-code policy, and carry the classified code through failure contracts, saved attempts, API responses, logs, and UI.
2. Distinguish initial session creation from reauthentication. Keep operation and phase separate so existing operation semantics remain stable.
3. Record both original and restarted pagination evidence: requested size/page, returned page/size/totals, raw array count, restart ordinal and safe correlation.
4. Do not log credentials, tokens, authentication headers, account identifiers, raw response bodies, instrument payloads, or arbitrary provider text.
5. Add the exact page-zero 70/41 regression and both initial/relogin 403 regressions before changing collection semantics.

Example of a proposed safe page diagnostic shape, not an existing response contract:

```json
{
  "operation": "instruments",
  "requestOrdinal": 2,
  "restartOrdinal": 1,
  "requestedPageNumber": 0,
  "requestedPageSize": 70,
  "returnedPageNumber": 0,
  "returnedPageSize": 70,
  "reportedTotalPages": 5,
  "reportedTotalResults": 290,
  "rawRowCount": 41,
  "expectedRowsUnderCurrentPolicy": 70,
  "published": false
}
```

The numbers illustrate the reconstructed scenario; they are not a captured payload. An authentication diagnostic should contain only operation, phase, status, permitted code, and bounded parse outcome, with no response text.

### Stage 2: establish the category contract before altering success criteria

Conduct one approved, bounded diagnostic traversal:

1. Hold requested page size fixed at 25 for the next diagnostic, without adaptive shrinking. Keep the provider account/session context stable.
2. Request each advertised page in order even if an intermediate page is short, retaining existing throttle, cancellation, metadata sanity and hard request/result bounds.
3. Keep the traversal out of production publication. Compute counts of raw rows, unique EPICs, duplicates and cross-page overlaps locally; do not export raw identities.
4. Compare page metadata and final counts. Do not sum initial and restarted responses into one collection.
5. Pace relevant request starts conservatively at five-second intervals and wait 60 seconds between categories, with lease renewal, cancellation and budgets enforced. If needed, run one deliberate comparison at size 150, not an automated sweep or shrinking loop.
6. Ask IG to confirm whether totals describe account-visible results, whether filtering happens before or after paging, whether short nonfinal pages are valid, and what proves category exhaustion.

Do not stop at the first short page: that is precisely the behavior under investigation.

Conditional production decisions:

| Established provider behavior | Recommended production behavior |
|---|---|
| Full-page, exact-total contract confirmed; responses violate it | Treat as provider inconsistency, retain last-good data, escalate with sanitized paired metadata. Retry only where justified by bounded transient policy. |
| Sparse pages valid; advertised page sequence complete; total is exact visible unique count | Traverse the fixed-size sequence; relax only unsupported per-page/derived-page-count assumptions; retain final exact unique count, metadata, field and identity checks. |
| Totals are broad/pre-filter but IG defines an authoritative visible completion rule | Implement that explicit completion rule and document the meaning of completeness. Do not compare unlike totals. |
| No authoritative completion rule | Keep outcome incomplete/unknown. A separate partial-data feature would require explicit product requirements; it must not impersonate a complete snapshot. |

This selects fixed-size, contract-driven traversal as the direction to investigate, not as an already-proven unconditional patch.

### Stage 3: resolve the classified session cause

* Confirmed allowance: coordinate throttling/cooldown for the actual scope across operations. Do not label it invalid credentials or retry blindly after 2 seconds. Historical-data limits may require different recovery from per-minute request limits.
* Confirmed key/product/account configuration: show the safe reason and validate the configured Demo environment and supported account/API-key setup. Do not repeatedly log in.
* Unknown denial: retain nonretryable unclassified failure, with bounded parse diagnostics. Escalate through approved support channels using safe timing/route/status information.
* Test a later eligible collection for recovery after the condition clears. Do not replay trading/order requests as part of this investigation.
* Evaluate scoped session reuse only after diagnosis or measurements justify it.

Investigate same-slot detail-run recovery separately if the `Superseded` state persists after listings recover.

## Alternatives evaluated

| Alternative | Decision and rationale |
|---|---|
| Hide warnings or downgrade to success | Reject. Would conceal missing collection and undermine complete-only persistence. |
| Accept 41 because they are probably entitled/tradeable rows | Reject. Filtering explanation and completion rule are unverified. |
| Repeatedly shrink page size until a page passes | Reject. Existing one-restart recovery already failed; changing page boundaries does not prove coverage. |
| Stop on the first short page | Reject. May abandon later pages under the observed behavior. |
| Remove all totals/duplicate validation and copy a third-party loop | Reject. Third-party usage is useful evidence, not this application's completeness guarantee. |
| Replace categories with market search or old navigation | Reject as a direct fix. Search is not proven category-equivalent; old navigation is no longer documented. |
| Diagnose crypto entitlement or weekend maintenance from the category label/date | Reject. Failure is at a session POST; actual provider code is unknown. |
| Add a shared session cache immediately | Defer. May reduce traffic, but is not a demonstrated cure and adds invalidation/account-scope risks. |
| Depend only on a 60-second inter-category pause | Reject as the sole limiter. Useful cushion, but does not control within-category requests, other callers or non-minute quotas. |
| Fixed 25-row pages with conservative request pacing and a category pause | Select for the next bounded diagnostic. Fits the low-urgency requirement; retain completeness, budget and lease protections. |
| Bounded diagnosis followed by contract-specific traversal and code-specific session recovery | Select. Addresses the missing evidence and real classification defect without publishing partial data. |

## Acceptance tests

| Test scenario | Required result |
|---|---|
| Initial size150/count70, restarted size70/count41, otherwise coherent totals290 | Under present policy: page0 expected70 actual41; no later pages or publication; one category attempt and three HTTP requests including login when there is no reauthentication or internal HTTP-handler retry. |
| Raw count mismatch versus malformed/duplicate/invalid rows | Counts remain raw; mapping failures remain explicit and are not represented as filtered rows. |
| Fixed-size diagnostic traversal receives a short first page | Later advertised pages requested within bounds; no production snapshot publication solely because traversal ended. |
| Metadata drift, repeated page, incorrect returned page/size, bounds exceeded | Explicit failure; bounded requests; no silent fallback. |
| Initial session POST returns classified 403 | No instruments GET; correct phase/status/safe code persists to UI. |
| Page GET401 followed by session POST403 | Reauthentication phase recorded; bounded replay; no mistaken instruments-entitlement classification. |
| Known/unknown/missing/malformed/non-JSON/oversized/unsafe error body | Known codes retained; unknown states explicit; no raw body, token or arbitrary text leakage. |
| Allowance versus API-key/configuration denial | Separate classifications and appropriate retry/cooldown behavior; unknown403 never blind-retried. |
| Earlier successful crypto snapshot plus new denied attempt | Last-good listing remains readable; latest failure and timestamps remain distinct. |
| Category refresh then a new update in the same slot | Reproduce current supersession; any generation fix admits new work without changing historical observations. |
| Later run after provider condition clears | New complete snapshot and detail coverage progress; no stale failure falsely presented as a fresh attempt. |
| Fixed size 25, exact visible total 290, coherent metadata | Twelve page GETs at unchanged size 25, eleven arrays of 25 and one of 15; final 290 unique valid rows, excluding login/retry requests from row counts. |
| Fixed size 25 with a short page or short final union | Diagnostic continues only through bounded advertised pages; no adaptive shrink, endless fetch or false complete publication. |
| Size 25 with totals 2,500 and 2,501 | Explicitly validate the existing 100-page capacity boundary; any revised bound preserves approved result/request budgets. |
| Five-second request pacing and 60-second category pause | Fake-time/handler tests verify pacing, including login/retries; cancellation and lease loss stop further sends; other callers still obey shared quota caps. |
| Long category exceeds parent lease duration | Renew parent and cycle safely while paging/waiting, or stop with explicit lease failure; no stale owner publication. |
| Production DI-built HTTP pipeline retries a request | Every actual attempt is accounted for and paced; no hidden reservation bypass, double retry ownership or timeout caused by intentional cooldown. |
| Slower traversal reaches schedule close or daily budget exhaustion | Apply documented continuation semantics consistently in coordinator and actual gateway; never silently truncate or exceed budget. |

Existing test anchors:

* `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:95-155,165-274,282-320,849-914`
* `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketDetailsGatewayTests.cs:404-510`
* `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\MarketCategoryInstrumentSqlIntegrationTests.cs:569-690`
* `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\MarketDetailSqlIntegrationTests.cs:223-232`
* `test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\MarketCategoriesTests.cs:47-85`
* `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\IgProviderRequestThrottleSqlIntegrationTests.cs:15-87`
* `test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinatorTests.cs:18-37,164-211,237-255`

Current adapter 403 tests principally cover denied instrument requests after successful login, not the exact session-stage incident. Current UI mismatch examples are also not an end-to-end reproduction of page-zero 70/41.

Future implementation should update `docs\wiki\runtime-behavior.md:249-265` and `docs\wiki\operator-guide.md:126-133` as needed, preserving the distinction between saved attempts, fresh requests, provider codes and separate detail coverage.

## Potential next research

1. Capture safe initial/restarted metadata and a paced, bounded fixed-size-25 traversal with a 60-second category pause to determine actual category paging behavior. Verify runtime retry accounting and lease safety before that experiment. This is the blocking evidence for changing completeness policy.
2. Parse the next session403 safely to identify IG's actual denial. The discarded historical body cannot be recovered from the new UI fields.
3. Compare persisted detail-run provenance and reproduce same-slot recovery to determine whether the source-identified identity risk caused this coverage state.

## Handoff summary

| Item | Result |
|---|---|
| Selected approach | Preserve complete-only publication; close safe error-code/phase gaps; test fixed size 25 with five-second request pacing and 60-second category spacing, after lifecycle/budget checks; implement only the confirmed contract. |
| Critical findings | Six: pre-mapping mismatch, consumed restart, undocumented count semantics, discarded session error body, attempt/HTTP distinction, independent superseded detail state. |
| Alternatives evaluated | Eleven, including the selected overall strategy and its slower diagnostic profile. |
| Follow-up evidence items | Three bounded questions above. |
| Verification boundary | Source traces and public contracts verified; no fresh provider reproduction, runtime fingerprint, or executable changes. |
