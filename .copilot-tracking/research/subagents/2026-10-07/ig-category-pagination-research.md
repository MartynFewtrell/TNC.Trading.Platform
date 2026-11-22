<!-- markdownlint-disable-file -->
# IG category-instrument pagination failure research

Research date: 2026-10-07. Repository HEAD inspected: `4486b9a`.

This report investigates the supplied COMMODITIES failure and saved category UI. It separates source-verified failure mechanics from unverified provider behavior and recommends a fail-closed, evidence-gated resolution. Only this report was authored; existing product, documentation, configuration and user changes were not edited.

## Key findings

1. **The failing resource is not market search or market navigation.** The gateway calls GET `/categories/COMMODITIES/instruments?pageNumber=0&pageSize=25`, version 1, then repeats page zero with size 14. Demo uses `https://demo-api.ig.com/gateway/deal/`. Session creation is a separate POST `/session`, version 2.
2. **The observed 14 and 7 are deserialized provider array lengths, before mapping, validation or identity checks.** This is not a local `Where`, deduplication, tradeability filter or category/type filter dropping rows.
3. **The restart assumes a fixed provider cap, but this incident does not behave like one.** Size 25 produces 14 rows; requesting 14 produces 7 rather than 14. The gateway performs only one shrink and rejects the second response. The code proves why collection failed, not why IG returned fewer rows.
4. **Public category documentation does not establish the strict completeness assumptions.** It documents zero-based paging and page size, but its metadata schema omits `totalPages` and `totalResults`. The incident demonstrates those totals are present at runtime. Public evidence does not establish whether they count account-visible instruments, underlying candidates before filtering, or another universe.
5. **Simply removing the per-page row check cannot safely solve this exact incident.** Under the current formula, 294 results at size 25 requires 12 pages. A first page of 14 and at most 25 rows in each remaining page can yield at most 289 rows, fewer than 294. The analogous size-14 traversal has at most 287. Final exact-total validation would still fail.
6. **A diagnostic fixed-size traversal already exists, but production does not use it.** It is internal, count-only and nonpublishing. Repository source search found no product call site outside its declaration. Tests call it; it is not evidence that the running application traversed later pages.
7. **There is a relevant UI mixed-freshness mechanism.** Latest listing attempt/success comes from periodically refreshed collection status; detail coverage is rendered from the initially loaded category catalogue. That can show a new listing result alongside old detail coverage. It is a verified source behavior, not proof of this screenshot's exact runtime cause.

## Scope and evidence limits

- User incident: slot 0, Demo COMMODITIES, initial request ordinal 1 at size 25 returning 14 of total 294; restarted ordinal 2 at size 14 returning 7 of total 294; expected 14, actual 7; category attempts 1 and gateway HTTP calls 3.
- Second supplied event: `Gateway`, `InvalidResponse`, `IncompleteCollection`, `PageRowCountMismatch`, operation `instruments`, page 0, expected 14, actual 7, attempt 1. The supplied owner/fence/full-run join keys identify an attempt; they are not instrument identifiers or collection IDs.
- Screenshot description: catalogue refreshed 07/10/2026 15:49; interest revision 2; COMMODITIES followed, never successfully collected, latest attempt failed at 15:49; CRYPTOCURRENCY followed and succeeded at 15:51; its displayed detail completion date is 2026-10-05 10:48:58 UTC. Other categories are unfollowed.
- Source evidence is the **current dirty workspace**, not a fingerprint of the deployed process. The adapter alone differs from HEAD by 692 additions and 51 deletions; its recent diagnostics and traversal are uncommitted. Do not infer that every inspected feature is deployed.
- No live broker API was called, no credential/secret material was read, no application was started, no database was accessed or modified, and no telemetry was required.
- Public documentation and a pinned public client were fetched without transmitting repository contents. A supplementary public web search failed with HTTP 504; legacy navigation documentation fetches returned HTTP 500/404. These failures establish no broker endpoint availability fact.
- Automated tests were **inspected, not executed**. Arithmetic was executed independently in PowerShell; it validates the reconstruction, not IG behavior.

## Exact request and response trace

### Endpoint and authentication

Evidence:

- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgEndpointProfileResolver.cs:7-39
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:54-102
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:781-829
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:832-910

The adapter resolves the applied active/available IG environment, uses that environment's protected credentials and acquires a version-2 session. Listing GETs include version 1, JSON Accept, API-key and session-token headers. Category code is URI-escaped. No search term, market-status, instrument-type, tradeability, maturity or underlying-EPIC filter is attached for COMMODITIES. The request code does not switch to `/markets` or `/marketnavigation`.

The first size comes from the constructor override or `requestBudgetContext.CategoryPageSize`. The coordinator supplies SQL-derived execution settings, rather than hardcoding this incident's size.

- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:85-102
- src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:656-674

Each outbound call waits through shared throttling, reserves a request, increments the HTTP counter immediately before sending, and rechecks execution validity after the response. A 401 can trigger bounded reauthentication and page replay; none is needed to explain the supplied three-call sequence.

- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:103-136,199-224,849-902,960-1005

### DTO mapping and absence of local filtering

Evidence:

- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:902-910,1315-1345
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:328-375,1053-1073,1076-1158

JSON is deserialized case-insensitively into `instruments: List<IgInstrumentRaw?>` and `metadata`, with explicitly named numeric nullable metadata properties. Unknown JSON fields do not supply missing declared metadata. A JSON null row remains a list entry; it does not silently disappear. Raw count is `Body.Instruments.Count`.

Mapping happens **after** the row-count check. Invalid required fields/expiry, invalid numeric values, duplicate trimmed EPICs or duplicate trimmed names cause a typed failure; they are not silently discarded. Optional fields stay null when absent. Neither `otcTradeable` nor `marketStatus` is used to filter membership.

Name uniqueness is an additional local invariant, separate from this failure: official category docs identify EPIC as unique but describe instrument name merely as a name. Do not automatically remove that invariant in this investigation; verify provider identity requirements and storage constraints before changing it.

### Restart and completeness reconstruction

Evidence:

- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:138-224
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:226-407
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:1017-1022,1208-1212

Current policy requires:

```text
returnedPageNumber == requestedPageNumber
returnedPageSize == requestedPageSize
totalPages >= 1 and totalResults >= 0
totalPages == max(1, ceiling(totalResults / requestedPageSize))
pageRows == min(requestedPageSize, max(0, totalResults - pageNumber * requestedPageSize))
all advertised zero-based pages fetched once
final validated instruments.Count == totalResults
```

Hard bounds are 100 pages and 15,000 results. A short, nonempty first page triggers exactly one restart with its raw row count as the new size. The restarted response replaces the traversal baseline. The two page-zero responses are not unioned or added together.

| Step | Request | Observed response | Current code consequence |
| --- | --- | --- | --- |
| Session | POST `/session`, v2 | Successful session inferred from listing requests | HTTP call 1 |
| Initial listing | GET category page 0, size 25, v1 | Page 0, size 25, 14 rows, total 294 | Expected first-page rows 25; restart at size 14; HTTP call 2 |
| Restarted listing | GET category page 0, size 14, v1 | Page 0, size 14, 7 rows, total 294 | Expected 14; fail before mapping or page 1; HTTP call 3 |

The supplied text omits returned `totalPages`. If this deployed logic generated the final row-count failure, preceding checks must have accepted 12 initial pages and 21 restarted pages, respectively. Those values are **source-derived inference**, not captured payload evidence.

Independent arithmetic executed: `ceiling(294/25)=12`, `ceiling(294/14)=21`, and restarted page-zero expected rows `min(14,294)=14`.

Even allowing the final page to contain the full requested size, size 25 can retrieve at most `14 + 11*25 = 289`; size 14 can retrieve at most `7 + 20*14 = 287`. Thus, with those page counts and row upper bounds, sparse-page acceptance alone cannot reach 294. If final page rows follow the normal residual formula, the deficits are larger. This is a decisive constraint on proposed fixes.

### Diagnosed failure versus provider hypotheses

**Verified immediate cause:** production enforces offset/full-page arithmetic against the raw response and the restarted page violates it. The gateway reports an incomplete collection; it does not publish a partial one.

**Not verified:** whether IG's response itself violates its intended contract, or the gateway applies a contract IG never guaranteed for categories.

| Hypothesis | Compatibility with 25 → 14 → 7 | Evidence needed |
| --- | --- | --- |
| Provider pages a broad candidate set, then removes unavailable/unsupported instruments | Compatible: the first 14 candidates may contain only 7 visible rows, while the first 25 contain 14 | IG confirmation of filter order and total/page semantics; fixed-size per-page evidence and local overlap measurements |
| Different visible/tradeable products or account restrictions affect the total | Compatible, but no denial/filter flag is captured | Provider definition of account/category result universe and account-context consistency |
| Fixed row cap of 14 | Incompatible with the simple cap model: a size-14 request should return 14 when total is 294 | Actual documented cap and evidence of any other filter |
| Snapshot churn, unstable ordering, caching or intermittent provider inconsistency | Possible, not established | Same-context bounded repeat evidence, stable metadata and identity-set hashes, provider request correlation |
| Application deduplication or tradeability filtering removes half the rows | Ruled out for this row-count failure by check ordering and DTO mapping | No additional probe needed to establish local ordering |
| Wrong `/markets` response model accidentally mapped to category DTO | No such route in inspected adapter; observed array and metadata were present | A deployed request-route/version fingerprint if runtime/source drift is suspected |

Reducing page size again, to 7 or 1, is not a diagnosis. Under post-pagination filtering, it can produce empty nonfinal pages and many more calls without proving exhaustion. Increasing page size can also hide page boundaries without resolving unlike-count semantics.

## Public provider contract verification

Public sources accessed on 2026-10-07:

| Source | Verified contract | What it does not establish |
| --- | --- | --- |
| [IG category instruments reference](https://labs.ig.com/reference/categories-category-id-instruments.html) | GET v1; returns instruments for the category; `pageNumber` default/min 0; `pageSize` default 150, min 1, max 1000; OPTIONS-only `referenceEpic` and `maturityType`; `instruments` array; metadata documents page number and size | Raw HTML contains no `totalResults` or `totalPages`; does not guarantee full nonfinal pages, visible-count totals, filter order or consistent snapshots |
| [IG market search reference](https://labs.ig.com/reference/markets-searchterm.html) | GET v2 `/markets?searchTerm=...`; supports paging; defaults page 0 and size 50; returns `markets`; documents total pages and total matching results | No guarantee a category code used as a search term yields the same category universe; no documented category pre-filter count behavior |
| [IG markets detail reference](https://labs.ig.com/reference/markets.html) | GET v2 takes mandatory known EPICs, maximum 50; optional `ALL` or `SNAPSHOT_ONLY` detail filter | Not discovery; filter controls detail representation, not category completeness; cannot discover missing EPICs |
| [IG REST reference index](https://labs.ig.com/rest-trading-api-reference.html) | Raw links list categories, category instruments, markets details, market-by-EPIC and market search | No current navigation link found; this does not prove `/marketnavigation` has been removed server-side |
| [Pinned public trading-ig client](https://github.com/ig-python/trading-ig/blob/1f307f006aa5a8f06870c5b32c99e2189508bd13/trading_ig/rest.py#L1450-L1521) | Version 1 category route; fixed page size; appends returned arrays; increments zero-based page number until runtime metadata says last page; no short-page stop | Third-party behavior, not an authoritative completeness rule; does not validate exact final totals or establish pre-filter semantics |

The category page's simplified fetch initially returned only its heading. The raw official HTML was therefore fetched and reduced **in memory**; the request schema and metadata table were verified directly. Search documentation must not be transplanted into category collection merely because both endpoints have paging parameters.

Legacy market navigation is conceptually a hierarchy discovery route rather than text search or known-EPIC detail lookup, but its current public IG documentation could not be verified during this task. Do not assume category IDs and navigation node IDs are interchangeable, that recursive traversal has identical membership, or that switching to navigation preserves completeness. Require verified current route/schema, tree termination, node/EPIC identity and category-set equivalence before considering it.

**No fetched authoritative source confirms that category counts represent pre-filter/raw candidates.** The shrinking counts strongly motivate that question; they do not answer it.

## Scheduling, persistence and saved UI

### Category independence and attempt accounting

- src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:416-510,647-779
- src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentRetryPolicy.cs:3-39
- src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:929-947

Selected categories execute sequentially. A failed category increments its failure count and does not inherently prevent a later category from succeeding; an inter-category pause may follow. That explains the compatibility of COMMODITIES failure and CRYPTOCURRENCY success.

The coordinator reserves one category attempt before invoking the gateway. Internal session/paging/restart calls consume HTTP budget, not extra category attempts. This failure is created nonretryable, so attempts 1 and HTTP calls 3 are consistent. `IncompleteCollection` maps to the safe UI code `InvalidResponse`; it is not an HTTP status or a synonym for a provider 403.

The final structured warning's field order matches the second supplied event. Null row index, field name and HTTP status are expected for a response-content row-count failure: the HTTP response was successful enough to parse, but the acceptance check failed. Null HTTP status does not prove no HTTP response.

### Complete-only atomic persistence

- src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:676-747
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentSnapshotStore.cs:19-128,137-192,193-232,291-329
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs:1029-1096

Only a `Complete` gateway result reaches `SaveCompleteAsync`. Failed attempts persist safe diagnostics separately without replacing listing rows or the last-success timestamp. With no previous complete COMMODITIES listing, “Never collected” is therefore expected; the failure does not clear a snapshot that never existed.

Publication uses a serializable transaction, SQL locks and applied-environment/catalogue/lease fences. History, current listing state, version and successful attempt are committed together, with final validity checks before commit. Exceptions clear the change tracker.

**Persistence repeats the offset-derived page-count invariant** and exact final result count. A future gateway-only change that permits a different authoritative page-count contract will still be rejected by `ValidateCompleteCollection`. Any confirmed contract change must be coordinated across collection metadata, provenance validation and persistence, without weakening transaction/fence guarantees.

Failures save failure kind, operation, page, expected/actual, bounded HTTP count and both page-evidence JSON records. The status reader deserializes bounded evidence and returns it with separate successful/attempt times. API mapping forwards these fields.

- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs:1054-1096
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentStatusReader.cs:45-108,120-137
- src\TNC.Trading.Platform.Api\Features\Platform\MarketCategoryInstrumentEndpointMapping.cs:56-108

### Dates, independent coverage and mixed freshness

- src\TNC.Trading.Platform.Web\Components\Pages\MarketCategories\MarketCategories.razor:73-94,107-125,135-185,194-204
- src\TNC.Trading.Platform.Web\Components\Pages\MarketCategories\MarketCategoriesPagePresenter.cs:36-43,116-140
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentStatusReader.cs:25-59
- src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\GetMarketCategoryInstrumentStatusHandler.cs:26-62
- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailReadStore.cs:86-149,373-410
- src\TNC.Trading.Platform.Api\Features\Platform\MarketCategoryInstrumentEndpointMapping.cs:166-176

Catalogue refresh, listing success, latest attempt and detail completion are different timestamps. The status reader filters attempts by the configured schedule's trading day, not arbitrary browser date. Listing and attempt display uses local `ToString("g")`; detail completion is explicitly UTC. The textual `10/07/2026` versus `07/10/2026` is consistent with US versus UK formatting for 7 October, but deployed cultures were not inspected. Do not relabel it as 10 July or silently convert the stored trading day. ISO date/time plus culture/zone information would disambiguate.

Followed categories without a detail run return `NeverCollected`. Unfollowed categories are mapped to `NotFollowed`. Current detail coverage is associated with the current listing collection ID/version, rather than inferred from the most recent listing attempt.

The Razor page chooses latest `attemptStatus` for outcome, safe failure and listing success. However, its detail column reads `category.DetailCoverage` from `Presenter.State`. `RefreshCollectionStatusAsync` updates `CollectionStatus` without updating that state. This is a concrete mechanism for a 15:51 listing result being juxtaposed with older, 5 October detail coverage.

If a fresh, same-snapshot API response still presents that chronology, investigate source collection/version and detail run provenance rather than attributing it to the pagination error. Under current read-store joins, the same current-listing coverage must match listing collection/version. The screenshot alone does not establish which projection was stale. A safe related UI correction is to prefer refreshed status detail coverage with an explicit historical/fallback policy, and test both data-source availability cases.

The page explicitly states that results are saved attempts, not live errors. Reopening it does not constitute a new provider call or prove recovery.

## Existing tests and historical evidence

| Area | Inspected evidence | Coverage and limitation |
| --- | --- | --- |
| Older exact restart regression | test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:351-403 | Fabricated 150 → 70 → 41 of 290; paired evidence, ordinals, three calls and no later page. Not the exact new 25 → 14 → 7 / 294 incident |
| Coherent versus incoherent shrinking | test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:536-717 | Synthetic recoverable cap, failed restart, 70/150 recovery and later-page failure; does not establish real IG filter semantics |
| Fixed diagnostic sparse pages | test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:93-161 | Visits later pages, rejects short final union, no shrinking |
| Mapping and duplicate identities | test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:785-838,1144-1193 | Text normalization, JSON null row rejection, duplicate EPIC/name rejection and explicit empty collection |
| Category independence | test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinatorTests.cs:11-118 | Last-good retention, later-category success, correlated gateway/publication logs |
| Persistence safeguards | test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\EfMarketCategoryInstrumentPersistenceTests.cs:109-154 | Incomplete/one-based metadata rejection |
| SQL publication and diagnostics | test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\MarketCategoryInstrumentSqlIntegrationTests.cs:244-345,377-528,580-719,837-922 | Listing history/current reads, version change, publication race, failure diagnostics retention and schedule-close rollback |
| UI diagnostics | test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\MarketCategoriesTests.cs:46-123 | Paired evidence and category/HTTP counters rendered independently; older numeric fixture, not 294 incident or mixed-freshness coverage reproduction |

Historical interpretation:

- .copilot-tracking\research\2026-10-04\ig-category-page-count-session-denial-research.md:32-43,122-149,403-434,501-509 already identified undocumented totals, no local filtering, and the need for nonpublishing fixed-size evidence. Its proposed diagnostics are partly implemented in the dirty workspace; its line references describe an older source layout.
- docs\wiki\runtime-behavior.md:278-301 records the earlier 150 → 65 → 38 event, explicitly rejects partial completeness and distinguishes persisted attempt chronology. It is historical operational evidence, not an authenticated payload for the present incident.
- Local adapter history showed HEAD `4486b9a` dated 2026-10-03, then `f9bfcce` dated 2026-09-26 and `8fc5174` dated 2026-09-24. The current paired diagnostics/fixed diagnostic implementation must not be attributed to those commits without diff verification.

No passing test result is claimed. Existing fixture success shows only what is intended and simulated.

## Selected implementation strategy

**Select bounded, nonpublishing contract diagnosis first, followed by a contract-backed fixed-size collector. Keep production fail-closed until authoritative completeness is established.**

This is implementable without declaring the 7-row response complete. It builds on the existing diagnostic method rather than introducing a speculative endpoint switch. It does not promise immediate successful COMMODITIES publication without missing provider evidence.

### Stage 1: exact regression and safe response evidence

1. Add the exact 25/14/7/294 controlled-handler regression. Preserve one attempt, three reserved HTTP calls, both page-zero evidence records, no mapping/paging past rejection and no `Complete` result.
2. Add success-response metadata instrumentation before DTO mapping: requested/returned page and size, field presence/type, actual raw array length, null-element count, total values and bounded local identity statistics. Compare JSON DOM array count with DTO count to exclude unexpected deployed parsing behavior.
3. Extend nonpublishing diagnostic traversal to return per-page evidence, not just the current final summary. Retain fixed size, hard bounds, budget reservations, shared throttle, cancellation, lease/environment checks and zero publication.
4. Ensure the diagnostic path can **report** metadata/formula disagreement without concealing it behind an early arithmetic failure. Any exploratory continuation must follow a bounded, explicit provider page sequence, never speculative “until empty” paging. Missing or unusable authoritative page sequence remains a blocker.
5. Do not automatically invoke this traversal after every production failure: it consumes real account allowance. Provide an explicitly authorized operator/research workflow and clear distinction between diagnostic exhaustion and validated collection success.

Existing diagnostic evidence and limitations:

- src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:462-501,603-729

It fixes size at 25, bounds pages at 100 and diagnostic capacity at 2,500 rows. It already tolerates short page arrays during traversal but still requires formula-derived page count and exact final unique count. It returns no production collection. Per-page rows/overlap evidence and a safe invocation surface are currently missing.

### Stage 2: approved probes and provider clarification

These are proposed future probes, **not executed actions**:

- On one authorized Demo category diagnostic, keep category, applied environment and session/account context stable; request size 25, page numbers 0 through the provider-advertised last page. Do not shrink or stop at a short or empty nonfinal page.
- For each successful response, locally parse the unmodified JSON and emit only an allow-listed evidence projection. Do not log or persist the original body, arbitrary headers, row names, EPICs, account IDs, credential material, API keys or session tokens.
- Safe projection: UTC request/response timestamps; deployment version; environment/profile; category; run/attempt ID; request ordinal; page/size; HTTP status; bounded provider correlation identifier if separately reviewed; metadata field presence/type and numeric values; raw count; null count; validated count; unique count; duplicate/overlap counts; truncation/parse indicators; publication flag `false`.
- Compute identity-set intersections locally. If needed for repeated-response comparison, use a per-diagnostic keyed hash that is not a credential and is not persisted with the rows; report only equality/subset/overlap results. This permits testing whether restarted size-14 visible rows are a subset of size-25 rows without exporting identities.
- A separately approved, bounded comparison may replay page zero at sizes 25 and 14, then repeat 25 under the same context. This distinguishes stable subset behavior from churn; it is not a production adaptive-shrink loop.
- Respect configured request spacing, category pause, cooldowns and daily allowance. Do not copy older research timing values into operator configuration or bypass full-run/lease admission. Precompute a bounded call budget, including session and any permitted reauthentication.
- Ask IG specifically: what do category `totalResults` and `totalPages` count; when are account/product/tradeability filters applied; are short/empty nonfinal pages valid; is ordering stable; what is the authoritative end-of-category and account-visible completeness rule; does Demo differ?

Decision gates:

| Verified answer/evidence | Safe production outcome |
| --- | --- |
| Totals are exact visible unique instruments and page sequence can contain sparse pages | Fixed-size traversal, all advertised pages, exact final unique total, valid rows and stable metadata; alter unsupported per-page/formula checks only as justified |
| Totals are broad candidates, but IG supplies an authoritative account-visible exhaustion rule | Represent candidate versus visible counts separately; implement that explicit completion rule and update persistence/provenance validation coherently |
| IG guarantees full-page visible-count contract but responses violate it | Retain incomplete failure and last-good data; escalate sanitized provider evidence; bounded retry only if genuinely transient |
| No authoritative completion rule or unstable membership/metadata | Continue incomplete/unknown; no complete publication, no fabricated totals |

For the present numbers, exact visible totals plus 12 pages capped at 25 cannot be reconciled; additional provider semantics or different authoritative page sequence must explain the deficit before success can be asserted.

### Stage 3: coordinated implementation and documentation

- Keep provider semantics in the outbound adapter and inward collection metadata explicit. Do not spread IG payload shapes into use-case/UI code.
- Make any new candidate/visible count distinction explicit in metadata and data-quality evidence. Do not overwrite provider totals with observed counts just to pass `SaveCompleteAsync`.
- Preserve complete-only transaction publication, old snapshot/history retention, per-category independence, applied environment/catalogue/run provenance and fence validation.
- Add the mixed-freshness UI regression and, if in scope for implementation, select latest status coverage with a deliberate fallback rather than always using initial category coverage.
- Update affected wiki runtime behavior, operator diagnostics, API/architecture metadata contract and testing pages when the actual implementation changes. Document whether completeness means account-visible or broader universe, retained last-good behavior, known provider limitations and diagnostic-vs-production distinctions.

### Alternatives rejected or deferred

| Alternative | Reason |
| --- | --- |
| Accept initial 14 or restarted 7 as complete | Hides missing rows; contradicts provider total and complete-only storage |
| Remove page-row checks only | Still cannot reach total 294 with inferred pages/row bounds; final/persistence checks fail |
| Replace total 294 with number collected | Changes the meaning of completeness without evidence and loses missing membership silently |
| Repeatedly shrink to 7, 1, or stop on empty page | No proof of exhaustion under post-page filtering; request explosion and termination/false-empty risk |
| Increase size to 150/1000 as a blind fix | Earlier 150-row incidents also shrank; large first page is not a completeness contract |
| Union initial and restarted page zero | Overlap and changed offset universes produce duplicates or gaps; mixes incompatible baselines |
| Switch category code into `/markets?searchTerm=...` | Text matches are not category-set equivalence; response DTO/version differs |
| Switch to market navigation | Current authoritative docs and category mapping not verified; hierarchy recursion/overlap cannot be guessed |
| Use batch `/markets?epics=...` | Enriches known EPICs only; cannot discover absent members |
| Retry or slow down until error disappears | Pacing addresses allowance, not a deterministic count-contract mismatch; no evidence this is rate limiting |
| Remove duplicate-name checks incidentally | Not the observed failure; requires identity/storage contract verification |

## Required tests and acceptance guarantees

Use fabricated provider responses, not live broker calls, for regression tests. Use repository-standard method names and requirement/expected-outcome/risk comments.

1. **Exact incident:** initial metadata page 0/25/12/294 with 14 rows; restarted 0/14/21/294 with 7 rows. Assert reason, operation, page, expected 14/actual 7, both raw counts/ordinals, three HTTP reservations and one coordinator attempt. Assert no complete snapshot write and later selected category can still succeed.
2. **No loss under candidate filtering simulation:** create 294 raw candidates with a deterministic visible predicate applied after offset slicing, including exactly 14 visible among the first 25 and 7 among the first 14. Diagnostic visits all fixed-size candidate pages, computes exact visible union and reports disagreement with candidate total; production remains incomplete until an authoritative rule is provided.
3. **No accidental sparse acceptance:** exact visible total plus sparse first page under insufficient advertised capacity must fail even after all pages. Verify the 289 maximum counterexample, missing totals, wrong metadata types, size/page mismatch and final short union.
4. **No duplicate publication:** repeated EPIC across/within pages, duplicated page response, replayed page zero and overlapping restart sets cannot silently inflate totals or construct completeness. Keep name behavior explicitly tested pending any separately justified identity change.
5. **Stable completeness:** full normal pages and residual final page; documented empty category; confirmed sparse-contract fixture only after provider contract is established; stable totals and no missing page indices.
6. **Termination:** page/row/call bounds; empty nonfinal page; last-page metadata; total/page count changes; 401 replay limits; timeout; cancellation; provider cooldown; schedule close; stale lease and applied-profile change. No unbounded shrink, recursive traversal or “until empty” algorithm.
7. **Diagnostic isolation/security:** no production collection or snapshot write; every call budgeted; safe projection contains no raw rows, body, headers or secrets; raw DOM/DTO count matches; invalid/duplicate rows produce explicit evidence rather than silent filtering.
8. **Atomic persistence:** both fresh contexts and concurrent writers; late environment/catalogue/schedule/full-run fence change; SQL failure after current-row replacement; rollback retains prior current snapshot, immutable history and correct attempt state. Update persistence validation tests if metadata semantics legitimately change.
9. **Saved diagnostics/UI:** numeric evidence round-trip through SQL/API/UI; old nullable diagnostic fields remain usable; safe code remains `InvalidResponse`; attempts and HTTP calls remain separate; no broker call on saved reads.
10. **Chronology/mixed freshness:** initial catalogue detail completion on 5 October, status refresh with 7 October new listing and changed detail coverage. Assert refreshed coverage is selected coherently or explicitly labelled historical; UK/US culture and UTC display do not imply different trading days.

Focused future validation should start with adapter/persistence/coordinator/bUnit tests; Docker-backed SQL tests only for changed persistence/fence semantics. Load the `run-tests` skill before executing .NET tests and use existing repository validation scripts/tasks where available. No such execution was performed for this report-only task.

## Applicable authoring constraints for the parent

Guidance read before authoring: repository Copilot instructions, AGENTS, lessons, folder rules, documentation authoring, work-package instructions, token discipline, architecture, configuration and test standards. No additional repository file named markdown/writing instructions was found; documentation instructions are the repository's available Markdown-writing standard.

- .github\copilot-instructions.md:27-60 requires evidence-backed diagnostic output, preserved report history, concise chat responses, no unsolicited work package and wiki updates before implemented plan completion.
- .github\AGENTS.md:35-46 and .github\lessons.md:3-8 favor file-and-line findings, concise execution responses and structured summaries in durable reports.
- .github\instructions\docs.instructions.md:21-59 supplies semantic ATX headings, one H1, ordered heading levels, language-tagged fences, descriptive HTTPS links, locale-free Microsoft Learn links and meaningful image alt text. Its formal scope is `docs/**/*.md`, not this explicitly requested tracking artifact; these writing conventions are followed where compatible.
- .github\instructions\folders.instructions.md:21-46 prohibits unnecessary new roots and defines asset locations. This report stays within the existing requested tracking root.
- .github\instructions\work-packages.instructions.md:27-63 prohibits unsolicited work packages and governs plans/wiki impact when that workflow is explicitly requested. This is research, not a new work package or implementation plan.
- .github\instructions\token-discipline.instructions.md:23-44 favors bounded evidence, parallel independent reads, new findings with precise lines and concise completion.
- Task-specific requirements override incompatible general layout advice: start with `<!-- markdownlint-disable-file -->`; write only the requested report; use workspace-relative plain-text Windows `path:line-range` evidence rather than Markdown links for local source; do not modify old reports, wiki pages, instructions, lessons or product assets in this research turn.
- Future C# or UI implementation must additionally load the relevant scoped C#/refactoring/auth/API/Blazor instructions before editing; tests require repository naming and fully documented traceability. This report does not select a .NET version or alter runtime authentication.

## Validation performed and remaining blockers

Executed read-only validation:

- Traced endpoint/version/header/resource construction, raw DTO mapping, one-time restart, per-page/final completeness, request accounting, coordinator retries/logging, atomic persistence, API mapping and UI data-source chronology.
- Inspected targeted existing unit, bUnit and SQL integration tests and prior dated research/wiki evidence.
- Checked current HEAD, adapter history and dirty diff statistics; did not reset, stage, commit or overwrite user changes.
- Verified official public request/schema facts from raw HTML and pinned public client paging behavior.
- Executed incident arithmetic and the fixed-page capacity contradiction.

Not executed: automated tests/builds, authenticated probes, telemetry inspection, database reads, app lifecycle operations or production deployment verification.

Blocking gaps: actual sanitized per-page metadata/identity statistics across fixed-size traversal; authoritative IG meaning of category totals/filtering/exhaustion; deployed build/culture/context fingerprint; coherent refreshed detail-coverage projection for the screenshot.

**Conclusion:** the rejection is accurately explained and preserves data safety. The provider-side reason remains unproven. Implement safe, bounded response evidence and the exact regression first; only adopt a fixed-size production completion contract once IG semantics support it. Do not turn the observed 7 or 14 rows into a falsely complete category.
