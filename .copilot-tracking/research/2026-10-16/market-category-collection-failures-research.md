<!-- markdownlint-disable-file -->
---
title: Market-category collection failures research
description: Evidence, diagnosis, alternatives, and recommended investigation for the 16 October 2026 IG listing failures.
date: 2026-10-16
---

## Recommendation

Keep production collection fail closed. Use the existing fixed-size, nonpublishing
diagnostic endpoint to establish the provider's actual page shape and cross-page
identity behaviour before changing pagination or completeness validation.

The supplied logs identify two distinct gateway validation failures:

| Category | Confirmed failure mechanism | Unresolved question |
| --- | --- | --- |
| COMMODITIES | A requested 25-row page contains 12 rows; a restart requesting 12 contains five, against an advertised total of 290. The restart still violates expected row counts. | Why does the provider return sparse pages with those totals? |
| CRYPTOCURRENCY | Page 1, row 0 repeats a trimmed EPIC already accepted from page 0. | Is the overlap caused by provider pagination semantics, instability, or a provider defect? |

Do not repeatedly shrink pages, silently deduplicate and publish, or replace
last-good snapshots with an unverified partial union.

## Scope and success criteria

* Scope: the supplied 16 October screenshot and warnings, the current collection
  gateway, diagnostics, publication safeguards, status UI, and relevant fixtures.
* Deliverable: one authoritative research document consolidating this chat.
* Exclusions: runtime implementation, live IG requests, database inspection or
  modification, collection resets, and an implementation plan.
* Success criteria: distinguish verified mechanisms from hypotheses, identify
  an actionable evidence-gathering path, evaluate alternatives, and define the
  verification needed before accepting a repair.

## Provenance and limitations

* Incident source: screenshot and warning text supplied by the user in this chat.
* Code source: current working tree inspected on 16 October 2026. The verification
  reported 127 Git status entries, including relevant modified source, tests,
  and wiki files. References describe inspected working-tree content, not a
  verified deployed build or necessarily committed HEAD.
* Historical source: docs/market-category-collection-failures-research-2026-10-03.md.
  Its earlier database and Aspire observations were not repeated.
* No live IG response, deployed binary identity, or provider support confirmation
  was obtained. The official public guides and resource reference were inspected
  in the follow-up below, but do not settle the disputed completeness semantics.
* Initial research inspected tests without executing them. The subsequent
  outgoing-request verification below ran five focused synthetic test cases
  successfully. These establish implementation behaviour, not live IG semantics.
* This is a standalone research artifact, not an approved implementation plan.
  Earlier chat responses did not create a research document; this file supplies
  the missing durable deliverable without overwriting earlier research.
* Repository references below are workspace-relative paths with line ranges.

## Incident evidence

### Commodities

The supplied warning reports:

```text
Trading day: 2026-10-16; slot: 0
Initial page request #1: page 0, size 25
Returned metadata: page 0, size 25, total 290; returned rows: 12
Restarted page request #2: page 0, size 12
Returned metadata: page 0, size 12, total 290; returned rows: 5
Failure: IncompleteCollection / PageRowCountMismatch
Expected: 12; actual: 5; gateway HTTP calls: 3
```

Three HTTP calls are consistent with one session request plus two page
requests, not three category collection attempts. The screenshot reports
one category attempt. An authentication or transport failure is not the
reported reason for this rejection.

The production evidence field named `RawCount` is populated from the
deserialized `Instruments.Count`. It is not an independent measurement of the
JSON array. Only the diagnostic path separately measures raw JSON elements.

### Cryptocurrency

The supplied warnings report `InvalidCollection`, reason `DuplicateEpic`,
operation `instruments`, page 1, row 0. EPIC identity comparisons are ordinal
and use trimmed values. The duplicate is therefore a cross-page repeat within
the category collection, not merely a repeated instrument display name or
membership in two different categories.

Different full-run and lease identifiers occur in the supplied warnings.
These identify separate executions; they do not prove an automatic retry
inside a single category attempt. The lease fence value is not a retry count.

### Screenshot interpretation

* COMMODITIES has no successful listing and no collected detail coverage.
* CRYPTOCURRENCY retains a successful listing dated 7 October and reports
  complete market-detail coverage on 16 October.
* Listing freshness and market-detail coverage are independent. Detail work
  can use the last validated listing when a current listing fails. Complete
  detail coverage does not prove that today's category membership is current.
* The UI reads saved collection outcomes. Refreshing that view does not itself
  issue another category download.

## Verified implementation evidence

| Area | Source and lines | Finding |
| --- | --- | --- |
| Adaptive restart | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:189-204 | A short nonempty page zero permits one smaller-page restart using the observed row count. |
| Per-page completeness | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:328-357 | A still-short page fails as `PageRowCountMismatch`. |
| Collection identity | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:227-228,1426-1481 | Sets span the collection; trimmed duplicate EPICs and names are rejected. |
| Final completeness | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:378-385 | Page coverage and final instrument count must match advertised totals. |
| Production evidence count | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:1404-1423 | `RawCount` comes from the deserialized list. |
| Diagnostic bounds | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:23-26,769-789 | Fixed size 25; 100-page ceiling; reserve reauthentication capacity. Effective diagnostic row bound is 2,500. |
| Diagnostic traversal | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:810-939 | Visits the advertised sequence and records sparse counts and cross-page overlap without production publication. |
| Diagnostic outcome | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:955-961 | Exhaustion explicitly makes no completeness claim. |
| Raw JSON measurement | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:1163-1171,1298-1308 | Diagnostic JSON array length is measured separately from deserialization. |
| Operator endpoint | src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentEndpoints.cs:37-41,123-164 | Operator-protected, bounded, count-only diagnostic with explicit refusals. |
| Diagnostic guards | src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/DiagnoseMarketCategoryCollectionHandler.cs:39-146,168-218 | Checks applied environment, schedule, catalogue, execution admission, and approved allowance. |
| Publication boundary | src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs:676-745 | Only a complete gateway result reaches publication; publication exceptions use a different failure branch. |
| Safe failure mapping | src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs:932-933 | Invalid and incomplete collections both appear as `InvalidResponse`. |
| Writer invariants | src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs:296-344 | Writer independently requires matching totals, sequential pages, coherent page formula, valid provenance, and unique EPICs. |
| UI diagnostics | src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor:134-205 | Failure kind and selected evidence render, but reason and row index do not; detail coverage renders independently. |
| Stale listing fallback | docs/wiki/runtime-behavior.md:149-156 | Detail collection may use a previous complete listing marked stale. |

## Diagnosis and remaining hypotheses

### Commodities

The failure is fully explained by the current validation contract. Reducing
the requested size from 25 to 12 did not yield a coherent 12-row page, so the
simple provider-cap recovery assumption does not resolve this incident.

Potential explanations include filtering after pagination over a larger
candidate universe, inaccurate provider metadata, or an unstable provider
result set. These remain hypotheses. The supplied production count alone
does not distinguish raw provider sparsity from response-shape interpretation.

### Cryptocurrency

The gateway accepted the earlier page, then rejected a repeated EPIC at the
start of page 1. This explains the warning before any SQL snapshot write.
It does not establish why IG returned overlapping identities.

Possible explanations include unstable ordering, repeated page contents,
or pagination over overlapping candidates. Silently dropping the repeated
EPIC could conceal another missing instrument.

### Historical findings that must remain separate

The 3 October document records an earlier crypto `UnexpectedFailure` with
an unresolved branch and a separate frequency-validation mismatch. Neither
is the diagnosed cause of this incident's explicit `DuplicateEpic` failure.
Historical SQL/Aspire observations are not new evidence for 16 October.

## Selected approach

### Gather bounded nonpublishing evidence

Run the existing endpoint as an authorized Operator during an eligible
schedule, with sufficient approved allowance and no conflicting full run:

```http
POST /api/platform/market-categories/COMMODITIES/collection-diagnostic
Content-Type: application/json

{"maximumHttpRequests":15}
```

For a reported total of 290 at size 25, expect 12 advertised pages and 13
normal HTTP calls including authentication. A ceiling of 15 allows two
additional calls for reauthentication. The handler requires available
manual allowance at least equal to the requested ceiling, not merely the
estimated normal usage.

Repeat separately for CRYPTOCURRENCY, sizing its ceiling to its own
advertised sequence and approved allowance. The supported ceiling is
2 through 103; do not assume 15 suffices for every category.

Save the sanitized result with execution time and environment. Compare:

* Requested and returned page numbers, page sizes, and advertised totals.
* Raw array counts versus deserialized counts on each page.
* Unique valid EPIC count versus advertised total.
* Invalid rows, duplicate names, duplicate EPICs, and cross-page overlap.
* Metadata drift and whether the entire advertised sequence was visited.
* Reason code, actual HTTP usage, and any execution refusal.

The diagnostic consumes request allowance and uses shared execution admission.
It cannot publish a listing or establish completeness merely by finishing.
Do not reset slots, alter interest, or restart applications as a substitute
for interpreting this evidence.

### Choose the repair from the result

| Observed diagnostic evidence | Recommended action |
| --- | --- |
| Raw JSON and deserialized counts agree but pages are short | Confirm provider filtering and total semantics with IG; treat raw sparsity as provider response evidence. |
| Raw and deserialized counts differ | Investigate response schema and mapping before changing page traversal. |
| EPICs overlap across pages | Establish whether ordering, repeated responses, or contractual overlap explains it; do not deduplicate and claim completeness. |
| Metadata changes during traversal | Preserve failure and investigate unstable membership or provider pagination; a changing target cannot establish a coherent snapshot. |
| IG confirms sparse or overlapping pagination is contractual | Define a provider-backed completeness criterion, then change gateway, metadata/provenance, writer validation, and tests together. |
| IG confirms the existing dense unique-page contract | Retain rejection and raise sanitized page/count evidence with IG support. |

Preserve atomic publication, prior complete snapshots, execution fencing,
environment/profile checks, and request accounting in every repair.

### Improve operator diagnostics

Expose an allow-listed reason such as `DuplicateEpic` and the page/row
location consistently through persistence, status contracts, mapping, and UI.
Verify existing surfaces before adding fields. Never expose credentials,
session headers, unrestricted provider bodies, or offending EPIC values.

Also disambiguate production `RawCount` from independently measured raw JSON
count in future diagnostics or documentation. This terminology improvement
is not itself a fix for either collection failure.

## Alternatives evaluated

| Alternative | Assessment |
| --- | --- |
| Keep shrinking until pages fill | Rejected. The observed 12-to-five reduction disproves a simple cap assumption; repeated shrinking changes traversal and consumes budget without proving completeness. |
| Increase retries or reduce traffic further | Not selected as the primary repair. The reported failures are validation outcomes, not demonstrated throttling. Existing pacing does not establish a complete provider listing. |
| Deduplicate crypto and publish the remaining rows | Rejected. A unique union can still omit instruments, and the writer requires complete matching totals. |
| Accept sparse pages after visiting all advertised pages | Rejected without confirmed semantics. Exhaustion is not proof of a complete category universe. |
| Immediately replace the endpoint or change indexing | Deferred. No evidence establishes the existing resource or zero-based request indexing is wrong. |
| Fixed-size diagnostic, provider confirmation, coordinated repair | Selected. Already implemented, bounded, nonpublishing, and suited to distinguish the unresolved causes without weakening safety. |

## Verification required for implementation

Existing fixtures in
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketCategoryInstrumentsGatewayTests.cs
cover sparse candidate pages at lines 136-183, diagnostic cross-page duplicates
at 227-253, still-short restarts at 694-716, and trimmed duplicate identity at
1251-1283. Initial research inspected these fixtures; subsequent focused
execution is recorded in the outgoing-request verification below.

Any subsequent repair should verify:

1. Exact incident fixture: initial size 25 returns 12; restart size 12 returns
   five; total remains 290. Current behaviour must fail closed after three
   HTTP calls without publication.
2. Crypto fixture: the first row of page 1 repeats a trimmed EPIC from page 0.
   Failure reason and zero-based location must survive to operator diagnostics.
3. Fixed diagnostic traversal visits all 12 advertised pages at size 25 despite
   sparse intermediate pages; count-only output remains sanitized.
4. Metadata drift, repeated page identities, malformed rows, budget exhaustion,
   authorization refusal, and lease/environment changes remain explicit.
5. Failed listings leave last-good snapshot pointers and observations unchanged.
   Detail coverage based on stale membership remains distinguishable from fresh
   category collection.
6. Any approved change to completeness semantics is reflected in writer
   invariants, provenance, API/status contracts, tests, and affected wiki pages.

## Official IG documentation follow-up

The two user-supplied starting guides and five directly relevant official pages
were inspected on 16 October 2026. All seven were accessible. Only public
documentation was retrieved; no authenticated provider requests were made.

### What the official sources establish

| Source | Verified guidance | Relevance to this incident |
| --- | --- | --- |
| [IG UK API starting guide](https://www.ig.com/uk/trading-platforms/trading-apis/how-to-use-ig-api) | Explains API setup, API-specific demo credentials, and demo practice. States standalone demo accounts must be linked to a live account to create API accounts. | Check account setup when reproducing; it does not explain sparse pages or duplicate EPICs. |
| [IG Labs getting started](https://labs.ig.com/getting-started.html) | Describes live/demo key creation, application identification through the API key, and quotas. Recommends demo testing. | Account and environment must match during comparison. No documented silent category-page filtering rule. |
| [REST reference index](https://labs.ig.com/rest-trading-api-reference.html) | Categories are those enabled for the IG account. | Category availability is account-specific; this does not establish how instruments within a returned page are filtered. |
| [Category-instruments v1 reference](https://labs.ig.com/reference/categories-category-id-instruments.html) | GET returns instruments for a category. `pageNumber` defaults to zero and has minimum zero. `pageSize` defaults to 150, minimum one, maximum 1,000. EPIC identifies an instrument uniquely. | Supports current zero-based request indexing and explicit size 25. Does not resolve advertised total semantics, dense-page assumptions, overlap, or snapshot ordering. |
| [REST API guide](https://labs.ig.com/rest-trading-api-guide.html) | Documents environment-specific gateways, API key, JSON headers, resource versioning, session v1/v2 CST and security-token headers, session v3 OAuth headers, and response `X-REQUEST-ID`. | Compare exact environment, account, resource version and paging parameters; retain request IDs for investigation. Session version and resource version are separate. |
| [IG API FAQ](https://labs.ig.com/faq.html) | OTC instrument availability depends on account dealing-platform availability. Default non-trading limits are 60 requests/application/minute and 30 requests/account/minute. Provides Web API support contact. | Eligibility is a plausible investigation dimension, not proof of post-pagination filtering. Preserve pacing during comparisons. |
| [IG API companion](https://labs.ig.com/sample-apps/api-rest-companion-release/index.html) | Supports environment selection and inspection of requests and responses. | Provides an independent client comparison. It is an interactive API caller, not inherently read-only or automatically safe for Live. |

### Important gaps in the category reference

The inspected reference does not document the observed total-count fields or
define what the incident's total of 290 counts. It also does not explicitly
guarantee full intermediate pages, uniqueness across page responses, deterministic
ordering, or snapshot consistency across requests.

The phrase "unique instrument identifier" describes EPIC identity. It is not
an explicit guarantee that a paginated response sequence cannot repeat that
identity. Conversely, missing guarantees do not establish that sparse or
overlapping responses are intended or safe to accept.

The resource lists explicit API-key, session, allowance, account, and unsupported
product errors. Those error contracts do not prove successful responses silently
filter instruments after pagination. Account-based filtering remains a hypothesis.

The REST guide's generic link-based pagination example is not sufficient
authority to replace the category resource's documented page-number parameters.
The FAQ and REST guide also describe token lifetimes differently; avoid treating
a hard-coded lifetime as the explanation for these successful-response validation
failures.

### Additional recommended evidence

Use the existing nonpublishing diagnostic first because it already provides
bounded, sanitized sequence evidence. If ambiguity remains, an authorized operator
can compare read-only GET responses in the official API companion:

1. Select Demo and the same account, category, explicit resource version,
   page numbers, and page size as the application.
2. Compare raw page counts, metadata, EPIC overlap, and request parameters.
   Matching independent results would narrow the issue toward provider behaviour;
   differing results require investigation of exact request context.
3. Record UTC timestamps and `X-REQUEST-ID` where available. Do not assume the
   existing count-only diagnostic already captures request IDs; verify or add
   narrowly scoped safe capture if needed.
4. Keep API keys, passwords, CST, security tokens, and authorization headers out
   of research artifacts and support attachments. Use GET operations only and
   account for every request within approved allowances. Do not invoke orders or
   account-mutating actions.
5. Ask [IG Web API Support](mailto:webapisupport@ig.com) to confirm what totals
   represent, whether filtering occurs before or after page boundaries, and
   whether cross-page EPIC overlap or changing order is expected.

Do not switch to Live merely to obtain different results. The public guides do
not promise Demo/Live catalogue equivalence; any Live comparison requires
separate explicit authorization and the appropriate safeguards.

### Effect on the recommendation

The official pages strengthen the diagnosis-first approach and add a practical
independent comparison and escalation route. They do not justify accepting partial
pages, silently removing duplicate EPICs, changing indexing, or treating the
advertised total as either reliable or irrelevant without further evidence.

## Outgoing-request code verification

A further source trace and focused tests checked whether the application
constructs the documented request correctly. No outbound request-construction
defect was confirmed.

### Request sent by the current implementation

```http
POST https://demo-api.ig.com/gateway/deal/session
Version: 2
X-IG-API-KEY: <api-key>
Accept: application/json; charset=UTF-8
Content-Type: application/json; charset=utf-8

{"identifier":"<identifier>","password":"<password>","encryptedPassword":false}
```

The session response supplies CST and the account security token. The initial
category request is then:

```http
GET https://demo-api.ig.com/gateway/deal/categories/COMMODITIES/instruments?pageNumber=0&pageSize=25
Version: 1
X-IG-API-KEY: <api-key>
CST: <session-response-CST>
X-SECURITY-TOKEN: <session-response-account-token>
Accept: application/json; charset=UTF-8
```

CRYPTOCURRENCY uses the same shape with its category code. Placeholder secrets
are illustrative only. No actual authenticated wire capture was obtained.

### Source and contract checks

* Session construction and token extraction:
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:1041-1049,1079-1087.
* Category URL and headers:
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:1121-1127.
  The exact saved category code is escaped as one path segment. The relative
  URI retains the gateway base path `/gateway/deal/`.
* Environment:
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgEndpointProfileResolver.cs:7-38
  selects the official Demo or Live host from the applied profile.
* Page size:
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/InstrumentCollectionSettingsEntity.cs:11
  defaults production to 25. Persisted settings feed the request context through
  src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs:657-674.
  Size 25 is legal despite the provider default of 150; test helper defaults
  must not be confused with production configuration.
* Transport:
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:154-158
  sets a 15-second timeout and removes automatic resilience handlers for this
  client. No custom category URI/header rewrite was identified in the traced
  repository path.
* Account:
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:1079-1087,1640
  reads `currentAccountId` but does not retain or independently select it.
  The request uses the login's account security token. The official
  [session reference](https://labs.ig.com/reference/session.html) and REST guide
  support this session-v2 flow; a missing `IG-ACCOUNT-ID` header is not a
  demonstrated defect for this authentication mode. A requirement to target a
  different account would need separate validation.

The official category reference agrees with the GET resource version, parameter
names and casing, zero-based page number, and permitted page size. Session
version 2 and category resource version 1 are intentionally different.

### The remaining concern is response interpretation

The DTO expects `metadata.totalPages` and `metadata.totalResults` as well as
page number and size:
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:1642-1650.
Those total fields are not defined in the inspected public category reference.
Their precise meaning and the resulting dense-page formula remain unverified.

Deserialization and the page-count check occur before instrument mapping.
The application does not silently filter or deduplicate rows before checking
page length. Later mapping rejects duplicate EPICs and names instead.
Consequently, a valid outbound request does not prove that the application's
response-completeness assumptions match provider behaviour.

### Focused test results

Five synthetic cases passed in the infrastructure unit test project, without
dependency restoration:

* One outbound-contract test using `dotnet test --no-restore`.
* Four smaller-page restart, still-short rejection, and duplicate-identity cases
  using `dotnet test --no-build --no-restore` with focused selection.

Evidence:
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketCategoryInstrumentsGatewayTests.cs:21-48,656-718,1258-1282.
The fake response helper injects pagination totals at lines 1547-1555 and the
transport is controlled at 1702-1725. Passing these tests validates construction
and current rejection behaviour, not IG's actual metadata contract.

Conclusion: the request is consistent with the documented API. Confirming the
deployed request and resolving the response-contract mismatch still require
sanitized diagnostic evidence or an independent same-account comparison.

## Reassessment of instrument discovery and completeness

### Revised conclusion

The category request is not demonstrably wrong, but the current approach
conflates discovery with proof of a complete category snapshot. The dense-page
formula is a local validation assumption, not a verified IG guarantee. It can
therefore reject useful discovery observations even when the HTTP request is
correct.

The user's additional research describes 15 instruments for requested size 30
and total 290. The complete JSON response was not provided in this conversation;
that count and shape remain reported evidence, not independently recounted data.
`pageSize` commonly describes a limit, but that convention alone does not settle
this resource's termination or total-count semantics.

The earlier recommendation to preserve fail-closed behaviour applies to
authoritative complete-snapshot publication and removal of previous membership.
It must not be interpreted as proof that every sparse page is an invalid IG
response or that useful partial discovery observations must be discarded.

### Business goal versus current implementation

docs/business-requirements.md:93-98 requires instrument discovery and persistent
selection. It does not itself require exhaustive category enumeration or one
instrument per underlying. Complete category snapshots are a particular runtime
design, not the whole business objective.

Separate:

1. Discovery observations: EPIC candidates with category/source, page, time and
   explicit partial or inconclusive provenance.
2. Complete category membership: authoritative replacement or removal only
   after an endpoint-specific completion rule has been verified.
3. Selected execution instruments: operator-selected EPICs whose account
   availability, product support, market status, freshness and execution safety
   are checked independently.

Deduplicate provider targets by EPIC within the environment/profile while
preserving every source/category membership. Do not collapse distinct contracts
merely because they share an underlying market. Existing cross-category detail
targeting already follows this identity distinction:
docs/wiki/runtime-behavior.md:149-157.

`TRADEABLE` is a current market status, not sufficient trading authorization.
`CLOSED` does not establish permanent exclusion from discovery. Data access
and execution permission remain separate:
docs/wiki/runtime-behavior.md:104-105,322-324.

### Alternatives for discovery

| Resource | Appropriate purpose | Why it is not an automatic replacement |
| --- | --- | --- |
| Category instruments v1 | Category browsing and membership discovery. | Sparse-page, overlap and completion semantics still need confirmation. |
| Market search v2 | Targeted user-led discovery with documented matching-result pagination. | Keyword results are not automatically category membership or the entire market universe. |
| Account watchlists v1 | Importing or browsing curated instrument selections. | A watchlist is a selection, not evidence of exhaustive coverage or authorization to trade. |
| Bulk markets v1/v2 | Enrichment of known EPICs, up to 50 per request. | Requires EPICs already known; it is not universe discovery. |
| Market navigation | Potential hierarchical browsing taxonomy. | Current supported reference/version and taxonomy mapping were not verified; do not prescribe migration on assumption. |

The official [market search reference](https://labs.ig.com/reference/markets-searchterm.html)
documents `searchTerm`, not `q`. Its v2 supports `pageNumber` and `pageSize`
with matching-result totals; v1 does not document those paging parameters.
An illustrative request for targeted discovery is:

```http
GET /gateway/deal/markets?searchTerm=silver&pageNumber=0&pageSize=50
Version: 2
```

The [watchlists reference](https://labs.ig.com/reference/watchlists.html) and
[watchlist contents reference](https://labs.ig.com/reference/watchlists-watchlist-id.html)
describe the active account's lists and their markets. The
[bulk markets reference](https://labs.ig.com/reference/markets.html) describes
details for already-known EPICs.

The current reference index did not link market navigation during this
follow-up. Retrieval attempts for the conventional root and node reference
pages failed. That does not establish deprecation or removal; current support
remains unverified.

### Recommended design direction and next evidence

Retain categories as one discovery source when category membership matters;
use search v2 and optionally existing watchlists for targeted selection.
Do not replace one purported exhaustive source with another unverified one.

Keep raw rows, distinct EPICs, overlap, and traversal progress separate.
Repeated EPIC observations need not imply duplicate instruments: preserve
source observations and distinguish identity/static conflicts from legitimate
changes to quotes or update times. Do not silently choose a conflicting row.

Do not invent a terminal rule such as stopping at the first short page,
requesting until the count reaches 290 distinct EPICs, or scanning indefinitely
until empty. Bound pages, requests, duration and allowance; detect repeated
sequences or no progress. A bounded abort means inconclusive, not complete.

Inspect a full sanitized response offline first, including exact metadata
paths and array length. Then obtain fixed-size diagnostic evidence under
separate provider-call authorization. Search or watchlist observations can
corroborate particular EPICs but are not equality oracles for category coverage.

Any change to persistence of partial discovery observations is a proposed
architecture change, not implemented behaviour. Preserve last-good complete
snapshots and do not infer removals from partial or inconclusive observations.

## Open questions and next steps

* Does the deployed gateway match the inspected working tree?
* What do complete fixed-size diagnostic traversals report for both categories?
* Does IG define totals over returned instruments or a larger candidate set?
* Are duplicate EPICs legitimate in this resource or a provider defect?
* Is a stable snapshot/order guarantee available during a paged traversal?

First obtain sanitized diagnostic evidence and provider clarification. Then
prepare an implementation plan for the confirmed cause. This research does
not authorize weakening completeness or making live provider calls.

## Research outcome

| Item | Result |
| --- | --- |
| Confirmed failure mechanisms | Two: still-short restart and cross-page duplicate EPIC. |
| Primary recommendation | Existing bounded fixed-size diagnostics followed by a provider-contract-backed repair. |
| Runtime changes | None. |
| Validation performed | Current source, fixtures, repository documentation, authoring rules, target-path availability, official IG guides/resource/session references, and five passing focused synthetic unit test cases. |
| Validation not performed | Authenticated IG requests, database queries, deployed-build comparison, provider confirmation of disputed semantics, full test suite, standalone solution build, or lint. |
| Remaining dependency | Sanitized full-sequence evidence and confirmed provider semantics. |
