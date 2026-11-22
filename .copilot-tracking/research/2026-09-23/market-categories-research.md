<!-- markdownlint-disable-file -->
# Task Research: IG Market Categories

Research the addition of an operator-facing Market Categories page backed by
IG's `GET /categories` endpoint and durable SQL Server storage. This is a
research recommendation only; it does not define a delivery plan or change the
application.

## Requested outcome

The application must retrieve the market-category catalogue from IG, store the
returned values in SQL Server, and display the stored values on a new UI page.
The expected provider response is a `categories` array containing:

| Provider field | Example | Meaning |
| --- | --- | --- |
| `code` | `FX` | IG's stable category code. |
| `nonTradeable` | `false` | Whether IG marks that category as not tradable. |

The supplied examples (`INDICES`, `FX`, `CRYPTOCURRENCY`, `EQUITIES`,
`COMMODITIES`, `BONDS_RATES`, `ETF`, and `OPTIONS`) are examples of provider
data, not a fixed application enum or a complete catalogue. The implementation
must retain newly introduced codes without a deployment.

## Recommendation

Implement Market Categories as a dedicated vertical slice that owns an
environment-scoped **current reference-data snapshot**:

* Infrastructure opens a fresh IG session, calls `GET categories` with API
  version 1, validates the complete response, and maps it to an
  Application-owned result. CST, `X-SECURITY-TOKEN`, API key, identifier,
  password, HTTP response body, and provider DTOs stay inside Infrastructure.
* Application owns separate query and refresh operations. The query reads only
  persisted categories; it never calls IG. The refresh coordinates provider
  retrieval and atomic persistence through narrow inward-owned ports.
* SQL Server holds the current complete catalogue and the timestamp of the
  last successful refresh for the applied broker environment. A successful
  provider response replaces that environment's entire catalogue in one
  transaction. A failed or malformed response changes neither existing rows
  nor the successful-refresh timestamp.
* The API exposes a Viewer-readable `GET` route for the persisted catalogue and
  an Operator-only `POST` route to refresh it. The Blazor page is Viewer
  readable, with a Refresh action displayed only to Operators.
* The page renders the last saved result, including a clear empty state and its
  last-successful-refresh timestamp. It must not make a provider request as a
  side effect of rendering.

This uses the same security and layering model as Account Details while using
current-state persistence rather than immutable account-history snapshots.
Market categories are a small catalogue used for navigation/discovery; no
requirement requests its historical evolution. A current snapshot is therefore
the smallest model that truthfully represents the complete latest response,
including categories that IG has removed.

## Provider contract and integration behavior

### Request

Use the configured applied IG Demo environment only. The current application
intentionally rejects Live provider authentication and routes typed IG clients
to `https://demo-api.ig.com/gateway/deal/`; this feature must preserve that
guard. Do not add Live routing as part of category discovery.

The call sequence should be:

1. Resolve the applied broker environment and reject unavailable or non-IG
   contexts before provider I/O.
2. Read the protected credentials in Infrastructure.
3. Create an IG session with `POST session`, `Version: 2`,
   `X-IG-API-KEY`, and `Accept: application/json; charset=UTF-8`.
4. Call `GET categories` with the API key, CST,
   `X-SECURITY-TOKEN`, `Version: 1`, and the same `Accept` header.
5. If the categories request is `401 Unauthorized`, create one fresh session
   and replay the categories request once. Do not retry other status codes
   blindly.

IG Labs documents that CST and `X-SECURITY-TOKEN` are invalidated by scheduled
maintenance and recommends handling rejected calls. The one-refresh-session
replay above follows the existing Account Details behavior and is bounded; it
does not create an unbounded retry loop.

The supplied response has no pagination. Treat it as an authoritative complete
snapshot only after it passes validation. Do not hard-code the eight supplied
codes, infer ordering from the example, or persist the raw JSON payload.

### Validation and safe failure

Use nullable Infrastructure wire DTO fields, then validate before mapping:

* The root `categories` member must exist and contain at least one item.
* Every `code` must be non-empty after trimming, must fit the chosen SQL column,
  and must be unique using `StringComparer.Ordinal`.
* Every `nonTradeable` value must be present.
* Malformed JSON, an empty list, duplicate code, or incomplete item is a typed
  malformed-provider-data failure. Do not partially persist valid items.
* Cancellation requested by the caller propagates. Timeouts, network failures,
  rate limits, rejected provider responses, and malformed data map to safe,
  stable failure categories. Do not include headers or provider response
  content in a failure message, API response, event, or UI.

The existing account gateway already demonstrates the important parts:
session creation, headers, a one-time unauthorised replay, nullable JSON DTOs,
and redacted typed failures. Reuse that pattern, but do not couple this
operation to `IgAccountDetailsGateway` or expand account-details contracts.
The two provider operations have distinct response schemas and lifecycle
semantics.

### Refresh cadence

For the initial feature, make the persisted result refreshable by an explicit
Operator action and keep all reads provider-free. This meets the retrieval
requirement without making page loads, Viewer access, or every authentication
cycle consume IG's non-trading quota.

If product requires automatic freshness later, add a separate scheduled or
post-auth capture operation with an explicit interval and cross-replica lease.
Do not silently add a daily or startup request in this change: the requested
freshness target, acceptable stale age, and behavior after a prolonged IG
outage have not been specified. The page can expose the successful-refresh
time so an Operator can make an informed refresh decision.

## Proposed boundaries and change surface

The names below follow the repository's feature naming convention. Exact
names may vary, but responsibilities should remain separate.

| Area | Recommended responsibility | Existing reusable precedent |
| --- | --- | --- |
| Application `Features/MarketCategories` | Inward-owned category record, refresh/query request and response records, handlers, failure result, and ports such as `IMarketCategoriesGateway` and `IMarketCategorySnapshotStore`. | `Features/AccountDetails` separates provider gateway, saved model, query handler, and refresh handler. |
| Infrastructure `Integrations/Ig` | `IgMarketCategoriesGateway`: session mechanics, provider headers/version, private JSON records, validation, one `401` replay, and error translation. | `IgAccountDetailsGateway`. |
| Infrastructure EF persistence | Current-state entity/entities, `PlatformDbContext` mapping, EF store, DI registration, and additive migration/model snapshot. | `EfPlatformIgProofDataStore` for environment-scoped current state; `EfAccountDetailsSnapshotStore` for explicit transaction and applied-catalog identifier resolution. |
| API | Thin protected endpoint registrations, transport response mapping, and typed Problem Details/error mapping. | `PlatformEndpoints` and `AccountDetailsEndpointMapping`. |
| Web | `PlatformApiClient` methods and view models, a focused presenter, a protected Razor page, and Viewer navigation item. | `AccountDetailsPagePresenter`, `AccountDetails.razor`, and `MainLayout.razor`. |
| Tests | Unit, controlled-provider contract, EF/SQL integration, API mapping, and bUnit coverage at their existing project boundaries. | The Account Details and Account Preferences test suites. |
| Wiki | Update the IG integration, runtime behavior, architecture, API reference, and testing pages when behavior is implemented. | [IG API guidance](../../../docs/wiki/ig-day-trading-with-ig-apis.md), [runtime behavior](../../../docs/wiki/runtime-behavior.md), [architecture](../../../docs/wiki/architecture.md), and [testing and quality](../../../docs/wiki/testing-and-quality.md). |

Do not put the provider call in a Minimal API handler, Blazor component,
`PlatformApiClient`, or `Program.cs`. Application must not reference EF Core,
`HttpClient`, `HttpRequestMessage`, or IG wire types. The composition roots
should only register concrete adapters and handlers.

## Persistence design

Use two SQL Server tables, both scoped to the broker-environment catalogue ID,
not merely the legacy environment enum/string:

```text
MarketCategoryCatalogStates
  BrokerEnvironmentId uniqueidentifier PK/FK -> BrokerEnvironments
  LastRefreshedAtUtc datetimeoffset NOT NULL

MarketCategories
  BrokerEnvironmentId uniqueidentifier NOT NULL FK -> BrokerEnvironments
  Code nvarchar(128) NOT NULL
  NonTradeable bit NOT NULL
  PRIMARY KEY (BrokerEnvironmentId, Code)
```

The `MarketCategoryCatalogStates` row makes three UI states unambiguous:

1. No row: no successful category retrieval has occurred.
2. Row plus category rows: current saved catalogue, with its refresh time.
3. A failed later refresh: retain and display the preceding saved catalogue and
   timestamp, while the command reports its typed failure.

Use one EF Core transaction to delete the environment's old child rows, insert
the fully validated new rows, and upsert the state row. The foreign key must
use restrictive delete behavior, matching other broker-scoped operational data.
No history or retention job is needed for this current-reference projection.

`Code` should be preserved as supplied after non-empty validation. Sorting for
display should be ordinal/culture-invariant by code rather than based on an
application enum or a claimed IG display name. No display name appears in the
stated provider contract, so the initial UI should label the column “Code”.

Add an additive EF Core migration and update the model snapshot. The migration
must be part of Infrastructure's source-controlled migrations, allowing the
existing startup initializer to apply it before the API becomes available.

## API and UI behavior

Suggested API surface:

| Route | Authorization | Behavior |
| --- | --- | --- |
| `GET /api/platform/market-categories` | Viewer | Returns the stored categories, ordered by code, and nullable `lastRefreshedAtUtc`. It never calls IG or writes SQL. |
| `POST /api/platform/market-categories/refresh` | Operator | Requests a fresh IG catalogue, validates it, atomically replaces the stored snapshot, and returns the saved result. Typed safe failures map to non-success responses. |

The exact route names should be checked against the API naming conventions when
the work is implemented, but the read/write authorization split should match
the existing Account Details endpoints: lower-risk observers can view saved
data; only Operators can incur provider I/O and update the database.

Add a new `[Authorize(Policy = Viewer)]` page at
`/market-categories`. Follow the Account Details pattern:

* show an explicit loading and no-saved-categories state;
* show “Last refreshed” from persisted data;
* render an accessible two-column table or simple list of `Code` and
  `Non-tradeable`;
* show a Refresh button only after the Operator scope has been obtained;
* disable it while a refresh is in flight and present safe failure feedback;
* add a Viewer navigation entry near Account Details/other IG reference data.

This result is small and bounded, so a paged or virtualized `RadzenDataGrid` is
not required. Semantic table markup is sufficient. If a standard grid is
preferred for visual consistency, it must use the existing free Radzen setup
and retain accessible headings and labels.

## Test recommendation

No automated test should call IG. Use the controlled HTTP handler/provider
approach already used for Account Details and Account Preferences.

| Test level | Tests to add |
| --- | --- |
| Application unit | Query returns only the saved snapshot and does not call the gateway; refresh maps a valid gateway result to one store replacement; typed provider failures cause no write; absent/empty state is explicit. |
| Infrastructure unit | Required request method, relative path, version, and session headers; fresh-session replay happens once only on `401`; token values do not cross the inward result; all malformed examples (missing array, empty array, null code, null flag, duplicate code) fail closed; timeout/network/rate-limit status classifications are safe. |
| Infrastructure SQL integration | EF migration creates both tables and constraints; an initial save, a later replacement, and a failed refresh preserve atomicity; the composite key prevents duplicate codes per environment; data stays isolated across broker environments. Use the fixture-owned SQL Server already used by Infrastructure integration tests. |
| API unit | Route mapping returns the selected response shape; Viewer/Operator authorization follows the existing policy model; each refresh failure maps to the expected safe status/Problem Details shape; a read does not invoke refresh orchestration. |
| Web unit | `PlatformApiClient` requests the correct scoped API endpoints; bUnit renders the empty, saved, and stale-after-failure states; Viewer has no refresh control; Operator can invoke Refresh; navigation contains the Viewer item. |
| Aspire/API integration | Add only if an existing protected endpoint integration fixture can cheaply establish the required role and verify SQL-backed retrieval across the API boundary. Do not use a real IG account or real secrets. |
| Browser E2E | Not required for the initial change if bUnit covers rendering and no new browser-only behavior is introduced. Add Playwright only if the delivered page adds a browser-specific workflow that bUnit cannot validate. |

Test methods must use the repository's
`MethodName_StateUnderTest_ExpectedResult` naming convention and include the
required XML documentation that records traceability, expected outcome, and
regression rationale.

## Reuse and non-reuse decisions

### Reuse

* The protected-credential lookup and applied broker-environment resolution.
* The typed `HttpClient` registration pattern and Demo base URL.
* IG v2 session creation plus v1 resource request headers, one-time `401`
  replay, cancellation propagation, and safe exception classification in
  `IgAccountDetailsGateway`.
* The Application port direction, thin endpoint mapping, Web delegated-scope
  acquisition, page presenter, role-gated navigation, and page states used by
  Account Details.
* EF Core mappings, source-controlled migrations, `PlatformDbContext`, and
  current-state catalog-scoped persistence patterns.

### Do not reuse directly

* `BrokerAuthenticationProof` and `IgProofData`: those are authentication
  evidence, not a complete category-catalogue contract.
* Account Details' immutable retrieval/header history model and daily-capture
  logic: no category-history or automatic-cadence requirement is stated.
* Account Preferences' idempotent write/remediation workflow: categories are
  provider-owned reference data and this feature must not alter them.
* Raw response storage: it is unnecessary for the required columns and would
  create avoidable schema, privacy, and diagnostic exposure.

## Risks and decisions to confirm before implementation

| Topic | Recommendation | Reason |
| --- | --- | --- |
| Freshness | Operator-triggered refresh initially; show last successful refresh time. | Avoids inventing a polling/daily schedule and conserves IG non-trading quota. |
| Automatic refresh | Defer unless a product owner specifies maximum stale age and outage behavior. | A background call is materially different operational behavior. |
| History | Keep only the current complete snapshot. | No historical/audit requirement was supplied; replacing atomically handles provider removals. |
| Empty provider array | Treat as malformed and preserve the existing snapshot. | An empty catalogue is not useful and could erase valid discovery data due to an upstream fault. |
| Environment | Apply the existing Test/Demo-only gate. | Current repository policy blocks Live requests and credentials are environment scoped. |
| Category labels | Display raw code only. | The stated endpoint does not supply a display name; inferred labels would become an unsupported application taxonomy. |
| Concurrent refresh | Coalesce same-process requests; add a SQL application lock only if refresh is expected from multiple API replicas. | The catalogue is small and manual refresh is infrequent; cross-replica behavior should be explicit rather than copied speculatively. |

## Evidence

### Repository evidence

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs`
  implements current Demo session creation, v1 resource headers, a single
  unauthorised replay, private provider DTOs, strict validation, and redacted
  failures.
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs`
  separates refresh orchestration from provider I/O and persistence.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs`
  demonstrates an explicit atomic EF persistence transaction and resolution of
  the broker-environment catalogue ID.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`
  contains the current broker-scoped entity/index/foreign-key mapping
  conventions.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
  demonstrates Viewer reads and Operator mutations as thin protected endpoints.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor`,
  `AccountDetailsPagePresenter.cs`, and `Components/Layout/MainLayout.razor`
  demonstrate saved-data rendering, role-gated refresh, and Viewer navigation.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs`
  and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/Integrations/Ig/IgAccountPreferencesGatewayContractTests.cs`
  provide the provider-boundary testing patterns.
* [IG API guidance](../../../docs/wiki/ig-day-trading-with-ig-apis.md)
  records the repository's Demo/Live constraints, session-token handling,
  non-trading limits, and market-discovery context.
* [Architecture](../../../docs/wiki/architecture.md),
  [runtime behavior](../../../docs/wiki/runtime-behavior.md), and
  [testing and quality](../../../docs/wiki/testing-and-quality.md) define the
  applicable dependency direction, operational persistence, and test-boundary
  conventions.

### External evidence

* [IG: How to use the trading API](https://www.ig.com/uk/trading-platforms/trading-apis/how-to-use-ig-api)
  explains that API market instruments use identifiers such as EPICs, placing
  category data in the market-discovery path rather than trade execution.
* [IG Labs REST trading API guide](https://labs.ig.com/rest-trading-api-guide.html)
  documents the REST integration model. Recheck the `GET /categories` reference
  and supported API version immediately before coding because IG can revise
  endpoint contracts.
* [IG Labs FAQ](https://labs.ig.com/faq.html) documents session-token expiry
  and planned-maintenance invalidation, supporting bounded reauthentication
  handling.

