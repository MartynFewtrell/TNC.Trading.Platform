# IG REST API Gap Analysis

This analysis compares callable REST operations in the read-only `ig-webapi-dotnet-sample` client with TNC.Trading.Platform's IG gateway coverage. The endpoint comparison records the baseline before the gateway extension in this change; the delivery status below identifies what has since been implemented. The demo is used as an endpoint, version, header, and payload reference only. Lightstreamer streaming is out of scope.

## Summary

At baseline, TNC authenticated with `POST /session` v2, read account details with `GET /accounts` v1, and read or updated account preferences through `GET`/`PUT /accounts/preferences` v1. It also had category and instrument catalog gateways on `/categories` and `/categories/{categoryCode}/instruments`. The authentication gateway made an internal `GET /positions` v2 call only to count positions for proof data; it did not provide a positions port or trading gateway.

The demo client exposed callable REST operations for quotes and history, positions, working orders, deal confirmations, watchlists, account history, market navigation and search, sentiment, sprint markets, and application administration. Nearly all of those capabilities were absent from the baseline. Similar-purpose catalog functionality was not endpoint-equivalent: the demo uses `/marketnavigation`, while TNC uses `/categories` and `/categories/{categoryCode}/instruments`.

## Baseline endpoint comparison

| Area | Demo REST operations and versions | TNC status at baseline |
| --- | --- | --- |
| Session and accounts | `POST /session` v1 (client's private auth method) and v2 (secure auth); `GET /session/encryptionKey` v1; `DELETE /session` v1; `GET /accounts` v1; `PUT /session` v1 for account switch | `POST /session` v2 is implemented by authentication; `GET /accounts` v1 is implemented by account details. Encryption-key retrieval, logout, and account switch are absent. Account preferences `GET`/`PUT /accounts/preferences` v1 exist in TNC but are not exposed by the demo client methods. |
| Market navigation and catalog | `GET /marketnavigation` v1; `GET /marketnavigation/{nodeId}` v1; `GET /markets?searchTerm={term}` v1; `GET /sprintmarkets` v1 | TNC has `GET /categories` v1 and paged `GET /categories/{categoryCode}/instruments` v1, but not the demo's market-navigation or search routes. Sprint-market search is absent. |
| Market details and prices | `GET /markets/{epic}` v1/v2; `GET /markets?epics={csv}` v1; `GET /prices/{epic}/{resolution}` v1 by date or count; `GET /prices/{epic}/{resolution}/{start}/{end}` v2; `GET /prices/{epic}/{resolution}/{numPoints}` v2 | No market-detail, batch-market, or historical-price gateway is present. REST prices are snapshots/history, not a substitute for the excluded streaming feed. |
| Positions | `GET /positions` v1/v2; `GET /positions/{dealId}` v1/v2; `POST /positions/otc` v1/v2; `PUT /positions/otc/{dealId}` v1/v2; `DELETE /positions/otc` v1 | Only a proof-only `GET /positions` v2 call exists inside authentication; it maps enough data to count open positions. There is no application port, full position mapping, detail lookup, or position mutation gateway. |
| Working orders | `GET /workingorders` v1/v2; `POST /workingorders/otc` v1/v2; `PUT /workingorders/otc/{dealId}` v1/v2; `DELETE /workingorders/otc/{dealId}` v1 | No working-order gateway exists. |
| Deal confirmations | `GET /confirms/{dealReference}` v1 | No confirmation gateway exists. This is a material gap for the platform's submit-then-confirm lifecycle. |
| Watchlists | `GET /watchlists` v1; `GET /watchlists/{watchlistId}` v1; `POST /watchlists` v1; `PUT /watchlists/{watchlistId}` v1; `DELETE /watchlists/{watchlistId}` v1; `DELETE /watchlists/{watchlistId}/{epic}` v1 | No watchlist gateway exists. |
| Activity and transaction history | `GET /history/activity/{lastPeriod}` v1; `GET /history/activity/{fromDate}/{toDate}` v1; `GET /history/transactions/{type}/{lastPeriod}` v1; `GET /history/transactions/{type}/{fromDate}/{toDate}` v1 | No history gateway exists. |
| Client applications | `GET /operations/application` v1; `PUT /operations/application` v1; `PUT /operations/application/disable` v1 | No gateway exists. These are application-key administration, not trading operations, so they are not an initial priority. |

Funding and account-settings DTOs in the demo model folder are not counted as callable endpoints: the demo REST client has no public REST methods for them. The separate Lightstreamer API is explicitly excluded.

## Prioritized implementation plan

1. **Market details and REST prices.** Align this slice with the existing `008-market-data-and-freshness-gating` roadmap item. Add Application-owned ports and result models for single/batch market details and historical prices, then Infrastructure IG gateways using `System.Text.Json`. Prefer the demo's v2 price contract and v2 market details where available; retain endpoint versions where only v1 is exposed. Cover input validation, response mapping, endpoint/version headers, provider failures, and malformed payloads. Treat this as REST market context and history only; streaming and the freshness guarantee remain separate work.
2. **Read-side trading state and confirmations.** Add focused ports for listing/retrieving positions, listing working orders, and retrieving deal confirmations. Move beyond the authentication gateway's proof-only position count; do not make Application depend on provider DTOs. These queries establish the state needed to reconcile trades and determine whether an action completed.
3. **Order and position mutations.** Align this slice with the existing `012-order-execution-and-traceability` roadmap item. Add distinct operations for create/edit/close position and create/edit/cancel working order, following the endpoint versions above. Associate each submission with its returned deal reference and confirmation lookup; preserve an indeterminate outcome when a mutation may have succeeded but its response is unavailable, and do not blindly retry mutations. Validate payloads and test request serialization, version headers, and success/rejection/timeout handling.
4. **Account activity and transaction history.** Add period- and date-range queries for activity and transactions to support reconciliation and operator review; keep the two query families and their validation explicit.
5. **Catalog utilities and watchlists.** Add market navigation/search and watchlist read/write only as needed by tracked-instrument workflows. The existing TNC categories/instruments implementation is related but is not a direct implementation of the demo's navigation paths. Defer sentiment and sprint-market operations unless a strategy requirement calls for them.
6. **Optional administration.** Defer account switching, session encryption-key/logout operations, and client-application management. Account selection should continue to follow TNC's applied broker environment/account controls rather than adding an independent account-switch path without a concrete use case.

## Delivery status

The implementation added the following Application ports and Infrastructure gateways:

| Area | Delivered REST capability |
| --- | --- |
| Market data | Market search, single and batch market details, and v2 historical prices. The existing category/instrument gateways remain separate from the demo's market-navigation routes. |
| Positions and orders | Position list/detail/create/amend/close; working-order list/create/amend/cancel; and deal confirmations. Mutations are not retried, and ambiguous results remain indeterminate. |
| Account history | Activity and transaction queries by period or date range. |
| Watchlists | List/get/create/update/delete watchlists and add/remove market items. |

Market-data requests follow the applied market-data profile. Account, trading, history, and watchlist operations require an executable IG Demo profile, create an operation-scoped session, and verify that the authenticated account matches the caller's expected account. Provider tokens remain inside Infrastructure. Read requests may be replayed once after an unauthorized response; state-changing requests are not retried. These gateways do not add Lightstreamer streaming or establish that a REST snapshot meets a live-price freshness requirement.

Still deferred are the demo's direct market-navigation hierarchy, sprint-market and sentiment endpoints, session encryption-key/logout/account-switch operations, and client-application administration. The gateways are infrastructure capabilities; this change does not add public platform HTTP endpoints or strategy orchestration. Validate routes, versions, and payload contracts against authoritative current IG documentation and IG Demo before any production use.

## Implementation constraints and validation

- Keep provider DTOs and HTTP details in Infrastructure; define narrow Application ports and platform-owned result types per operation.
- Follow existing IG conventions for the applied broker environment, endpoint profile selection, protected credentials, request throttle/budget where applicable, cancellation, safe failure outcomes, and dependency-injection registration. Do not bypass the existing environment safeguards.
- Reuse the demo for routes, versions, headers, and payload-shape clues only. Verify each behavior against an authoritative/current IG contract before relying on it in production.
- Add focused infrastructure unit tests using controllable HTTP handlers. Verify method, route, `Version`, session/API-key headers, JSON body/response mapping, cancellation, malformed data, authorization/rate-limit failures, and ambiguous mutation outcomes.
- Update relevant `docs/wiki/` pages when implementation changes API surface, runtime trading behavior, safety controls, or operator guidance. Do not create a numbered work package as part of this analysis; repository instructions say not to draft one unless explicitly requested.

## Evidence

**Demo reference** (read-only, rooted at `D:\Repos\ig-webapi-dotnet-sample`):

- `docs/api-reference.md:168-218` catalogs the REST routes, versions, and client methods.
- `IGWebApiClient/IGRestApiClient.cs:55-128` covers authentication, encryption-key retrieval, and logout.
- `IGWebApiClient/IGRestApiClient.cs:134-365` covers navigation, positions/working-orders reads, watchlists, history, account operations, confirmations, and market details.
- `IGWebApiClient/IGRestApiClient.cs:375-506` covers position mutations and price history.
- `IGWebApiClient/IGRestApiClient.cs:516-628` covers market search, watchlist mutations, and working-order mutations.

**TNC baseline implementation** (repository-relative paths):

- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:55-169` implements session v2, accounts v1, and proof-only positions v2.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:43-91` implements account details v1.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:121-169` implements account preferences v1.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoriesGateway.cs:101-152` implements session creation and `GET /categories` v1.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:248-304` implements session creation and paged category instruments.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:129-153` registers the baseline IG gateways.
- `docs/systems-analysis.md:37,63,65,399-403` states the market-data freshness and submit-then-confirm requirements and identifies roadmap items `008-market-data-and-freshness-gating` and `012-order-execution-and-traceability`.

**Delivered implementation**:

- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgRestGatewayClient.cs:10-212` owns operation-scoped session handling, environment checks, account binding, request throttling, and safe retry behavior.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketDataGateway.cs:9-115` implements market search, details, and price-history operations.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgTradingGateway.cs:8-218` implements positions, working orders, and deal confirmations.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountHistoryGateway.cs:9-106` implements activity and transaction history; `IgWatchlistsGateway.cs:8-152` implements watchlist reads and mutations.
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:133-137` registers the shared client and new Application ports.
- `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/` contains focused request/response and safety tests.
