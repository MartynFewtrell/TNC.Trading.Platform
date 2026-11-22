---
title: IG category collection completeness research
description: Evidence-based diagnosis and resolution gates for the COMMODITIES pagination failure and saved UI state.
date: 2026-10-07
---
<!-- markdownlint-disable-file -->

## Recommended resolution

Preserve complete-only publication. Add the exact incident regression and extend the existing nonpublishing, fixed-size diagnostic traversal to capture safe per-page evidence. Establish IG's category-specific completion contract before changing production completeness validation.

Separately address the UI's mixed-freshness data sources: collection status refreshes, but the detail-coverage column uses the initially loaded catalogue. Render a coherent refreshed projection, with an explicit fallback and historical timestamp labeling.

The application-side failure is explained. The provider-side reason for returning fewer rows is not yet established. Neither accepting seven rows as complete nor repeatedly reducing page size is a safe resolution.

## Scope and success criteria

* Investigate the supplied Demo COMMODITIES failure: requested size 25 returned 14 rows; restarted size 14 returned seven rows; both reported total 294.
* Explain `InvalidResponse`, `IncompleteCollection`, `PageRowCountMismatch`, one category attempt and three gateway HTTP calls.
* Investigate the supplied screenshot's saved attempt, never-collected state and older detail-coverage timestamp.
* Evaluate alternatives and recommend changes with source evidence, regression requirements and explicit decision gates.
* Exclude implementation, live broker calls, app lifecycle changes, secret access and database mutation.
* Treat current workspace evidence as distinct from the deployed application: the inspected gateway contains substantial uncommitted changes.
* Research succeeds when the failure mechanics, remaining uncertainties and safe next implementation steps are documented. This does not require claiming that COMMODITIES can already be collected successfully.

## Evidence and investigation

The detailed delegated investigation is retained in .copilot-tracking\research\subagents\2026-10-07\ig-category-pagination-research.md. It covers endpoint construction, raw DTO counts, restart logic, persistence, scheduling, UI refresh, existing tests, repository history and public IG references.

### Exact failure sequence

| Step | Request | Result | Effect |
| --- | --- | --- | --- |
| Session | POST `/session`, version 2 | Session available for listing requests | One HTTP call is consistent with the observed accounting |
| Initial listing | GET `/categories/COMMODITIES/instruments?pageNumber=0&pageSize=25`, version 1 | Page 0, size 25, 14 rows, total 294 | Short first page triggers a restart at size 14 |
| Restart | Same category, page 0, size 14 | Page 0, size 14, seven rows, total 294 | Expected 14 rows; rejects before mapping or requesting page 1 |

Evidence:

* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgEndpointProfileResolver.cs:7-39
* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:54-224
* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:781-910

Three HTTP calls do not mean three category attempts. The coordinator reserves one category attempt; session and restart requests consume HTTP allowance within it. The precise three-call attribution is consistent with the inspected flow, not a captured HTTP trace.

### The restart assumption is not supported by this response

The gateway interprets a short first page as a possible fixed provider page-size cap. That interpretation predicts that requesting the observed cap of 14 should return 14 rows. Receiving seven disproves that simple cap explanation for this incident.

The raw count is taken from the deserialized `instruments` array before mapping, field validation or identity checks. There is no local tradeability filter or silent deduplication explaining 14 becoming seven.

Evidence:

* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:226-407
* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:902-910,1053-1158,1315-1345

The endpoint is category discovery, not `/markets` text search or known-EPIC detail lookup. Their response schemas and total definitions cannot be substituted.

### Removing the short-page check is insufficient

Current validation assumes:

```text
totalPages = max(1, ceiling(totalResults / requestedPageSize))
pageRows = min(pageSize, max(0, totalResults - pageNumber * pageSize))
final validated row count = totalResults
```

For 294 results:

* Size 25 implies 12 pages. With 14 rows on page zero and at most 25 on each remaining page, the maximum is `14 + 11 * 25 = 289`.
* Size 14 implies 21 pages. With seven rows on page zero and at most 14 on each remaining page, the maximum is `7 + 20 * 14 = 287`.

Both maxima are below 294. Merely traversing later pages while retaining these assumptions cannot produce a complete collection.

The supplied logs do not include `totalPages`. The values 12 and 21 are inferred from preceding validation in the inspected implementation, not verified response payload values. The capacity argument is conditional on those page counts and row upper bounds.

Evidence: src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:226-407,1017-1022,1208-1212.

### Provider contract remains the blocking uncertainty

The [official category instruments reference](https://labs.ig.com/reference/categories-category-id-instruments.html) documents version 1, zero-based paging, a default page size of 150 and maximum of 1,000. Its published metadata schema documents page number and size, but not `totalResults` or `totalPages`.

It does not establish that:

* Every nonfinal page contains the requested number of rows.
* The runtime total counts account-visible unique instruments.
* Account/product filtering happens before or after pagination.
* The result ordering is stable across requests.
* A short or empty page signifies completion.

Post-pagination filtering is compatible with the observed counts: the first 14 underlying candidates might contain seven visible instruments, while the first 25 contain 14. This is a hypothesis, not a verified IG behavior. Snapshot churn, unstable ordering and Demo-specific behavior also remain possible.

Other verified public references:

* [Market search](https://labs.ig.com/reference/markets-searchterm.html): version 2, text search, a `markets` response array and documented total matching results.
* [Batch market details](https://labs.ig.com/reference/markets.html): known EPICs, maximum 50 per request; enrichment cannot discover missing instruments.
* [REST API index](https://labs.ig.com/rest-trading-api-reference.html): category, search and detail links.
* [Pinned public trading-ig client](https://github.com/ig-python/trading-ig/blob/1f307f006aa5a8f06870c5b32c99e2189508bd13/trading_ig/rest.py#L1450-L1521): keeps category page size fixed and follows runtime page metadata. This is useful prior art, not an authoritative completeness guarantee.

### Saved results and screenshot interpretation

`IncompleteCollection` maps to safe UI code `InvalidResponse`. This is a collection acceptance failure, not evidence of a provider HTTP error or throttling. Null HTTP status and field diagnostics are consistent with parsed response content failing a row-count check.

Only complete collections reach snapshot publication. Failure diagnostics are stored separately; prior successful data is retained. With no previous successful COMMODITIES collection, `Never collected` and detail coverage `NeverCollected` are expected.

A failed category does not automatically prevent the next selected category from succeeding. COMMODITIES failure followed by CRYPTOCURRENCY success is consistent with coordinator behavior.

Evidence:

* src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:416-510,647-779,929-947
* src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentRetryPolicy.cs:3-39
* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentSnapshotStore.cs:19-232,291-329
* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs:1029-1096

The page refreshes collection status independently of catalogue state. Listing outcome and last success use refreshed status; the detail column uses `category.DetailCoverage` from initially loaded state. This can juxtapose a new listing success with old detail coverage. It is a verified mechanism, not proof of the exact screenshot's deployed cause.

Detail reads associate coverage with the current listing collection/version. If a coherent refreshed response still shows unexpected chronology, examine listing/version and detail-run provenance rather than attributing it to pagination.

Evidence:

* src\TNC.Trading.Platform.Web\Components\Pages\MarketCategories\MarketCategories.razor:73-94,107-125,135-204
* src\TNC.Trading.Platform.Web\Components\Pages\MarketCategories\MarketCategoriesPagePresenter.cs:36-43,116-140
* src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailReadStore.cs:86-149,373-410

The log date `10/07/2026` and UI date `07/10/2026` are consistent with different cultures displaying 7 October. Runtime culture was not verified. Listing times use local formatting while detail completion explicitly uses UTC. Prefer unambiguous dates and explicit zones in future diagnostic presentation.

## Implementation recommendation

### First: reproducible regression and safe diagnosis

1. Add the exact 25/14/7/294 regression. Assert both response evidence records, expected 14/actual seven, three HTTP calls, one category attempt, no complete publication and continued processing of another selected category.
2. Extend the existing internal nonpublishing fixed-size diagnostic traversal rather than inventing another collector. It currently uses size 25, bounds pages at 100 and diagnostic capacity at 2,500 rows, and still validates formula-derived metadata and final totals. There is no identified production invocation.
3. Capture safe per-page evidence before mapping: requested and returned paging, metadata field presence/type, raw JSON array count, DTO count, null rows, validated/unique counts and duplicate/overlap counts. Do not log response bodies, instrument identities, account IDs, credentials or session headers.
4. Provide an explicitly authorized diagnostic invocation with a precomputed call budget. Preserve existing throttling, cooldown, lease/environment checks and cancellation. Do not run a full diagnostic automatically on every scheduled failure.
5. Keep size fixed while following a bounded provider-advertised page sequence. Diagnostic metadata discrepancies must be reportable rather than silently treated as success. Do not use an unbounded "until empty" loop.
6. Obtain IG clarification on total-count meaning, filter order, valid sparse pages, stable ordering and authoritative end-of-category rules, including Demo differences.

Existing implementation evidence: src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:462-501,603-729.

### Then: choose production behavior from verified semantics

| Verified provider contract | Production resolution |
| --- | --- |
| Totals count visible unique instruments; sparse pages are valid | Fixed-size traversal of all authoritative pages, stable metadata and exact final unique total; remove only unsupported per-page/formula assumptions |
| Totals count broader candidates; authoritative visible completion is available | Model candidate and visible counts separately; implement that completion rule and update persistence/provenance validation coherently |
| IG guarantees full visible pages, but responses violate the contract | Preserve incomplete failure and last-good data; escalate sanitized evidence; retry only if transience is established |
| No authoritative completion rule, or unstable result universe | Remain incomplete/unknown; do not publish as complete |

Provider-specific semantics belong in the outbound adapter. Any changed inward completeness metadata must propagate through persistence and diagnostics; replacing provider totals with observed counts is not a legitimate shortcut.

### Related UI correction

Use a refreshed, coherent source for collection status and detail coverage. Confirm whether the status DTO supplies coverage; if not, refresh the catalogue or add the appropriate read projection. Define a deliberate fallback for older or unavailable projections rather than silently mixing snapshots.

Test initial 5 October detail coverage followed by a 7 October listing/status refresh. The UI must show refreshed current-listing coverage, or explicitly identify historical coverage.

## Alternatives evaluated

| Alternative | Assessment |
| --- | --- |
| Accept 14 or seven rows as complete | Reject: unproven completeness and silent missing membership |
| Remove only the short-page check | Reject: page-capacity contradiction remains; final/persistence validation still fails |
| Replace 294 with collected count | Reject: changes the meaning of completeness without evidence |
| Repeated shrinking or stop on empty | Reject: possible filtered gaps, increased allowance consumption and false exhaustion |
| Increase size to 150 or 1,000 | Not a diagnosis: prior larger-page incidents exist; page size cannot define completeness |
| Union initial and restarted results | Reject: changed offsets and overlapping baselines do not prove coverage |
| Switch to text search or batch details | Reject: neither guarantees category-universe discovery |
| Switch to market navigation | Defer: current authoritative contract and category equivalence were not verified |
| Add broad retries or slower pacing | Reject as the primary remedy: no evidence that allowance pressure explains this mismatch |
| Bounded nonpublishing evidence, then contract-backed fixed paging | Select: preserves data safety and resolves the actual uncertainty |

## Required validation and documentation

* Adapter tests: exact incident, raw-versus-mapped counts, stable totals, sparse candidate simulation, insufficient capacity, malformed metadata, null rows, duplicate EPICs, replayed pages and explicit empty-category contract.
* Termination tests: page/row/call bounds, empty nonfinal pages, cancellation, timeouts, bounded reauthentication, cooldown, schedule close and stale execution context.
* Coordinator tests: attempt-versus-HTTP accounting, later-category success and last-good retention.
* Persistence tests: no writes on incomplete/diagnostic results, immutable history, atomic rollback and late fence/profile/catalogue changes.
* UI tests: diagnostics round-trip, nullable older evidence, saved-read behavior, refreshed detail coverage and culture/time-zone clarity.
* Use existing test projects and tooling. Run focused unit/bUnit tests first; use Docker-backed SQL integration tests when publication or metadata semantics change.
* Update affected docs\wiki runtime, operator, architecture/API and testing pages when implementation changes. State precisely what completeness means and distinguish diagnostics from production publication.

Existing fixture references:

* test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs:93-161,351-403,536-717
* test\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.UnitTests\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinatorTests.cs:11-118
* test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\MarketCategoryInstrumentSqlIntegrationTests.cs:244-345,377-528,580-719,837-922
* test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\MarketCategoriesTests.cs:46-123
* docs\wiki\runtime-behavior.md:278-301

## Verification and remaining questions

Research validation included source and test inspection, relevant workspace/history checks, public contract verification and independent incident arithmetic. Automated tests and builds were not run. No broker probes, database access or app restarts occurred.

Outstanding evidence needed for implementation:

* Sanitized fixed-size per-page response evidence and local overlap statistics.
* IG's authoritative definition of category totals and visible completion.
* Deployed build and runtime context, because source evidence includes uncommitted work.
* A coherent refreshed projection to reproduce the screenshot's detail-coverage chronology.

## Research summary

| Item | Result |
| --- | --- |
| Immediate failure | One-time page-size restart assumes a fixed cap; restarted raw response remains short |
| Provider root cause | Unverified; post-pagination filtering is a compatible hypothesis |
| Data safety | Complete-only publication correctly prevents partial replacement |
| UI finding | Independently refreshed status can be combined with stale catalogue coverage |
| Selected approach | Exact regression and bounded nonpublishing evidence, then contract-backed fixed paging |
| Implementation status | Research only; no application changes or recovery claim |
