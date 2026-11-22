# API Reference

## Purpose

Document the IG operations exposed by the client, identify calls used by the
WPF app, and show how the sample composes REST and Lightstreamer interactions.

## Key points (TL;DR)

* REST URLs are `https://demo-api.ig.com` for the configured demo environment
  and `https://api.ig.com` for live.
* Requests carry `X-IG-API-KEY` and `VERSION`; authenticated calls also carry
  `CST` and `X-SECURITY-TOKEN`.
* The client deserializes JSON only when the status is exactly HTTP 200.
* The UI uses authentication, browse, list watchlists, watchlist markets, open
  positions V2, and working orders V2.
* Streaming subscriptions use Lightstreamer items `ACCOUNT:<accountId>`,
  `TRADE:<accountId>`, `L1:<epic>`, and `CHART:<epic>:<scale>`.

## Details

### REST request and response conventions

`IgRestService` sets the API key, session tokens, and API version header. A body
is JSON-serialized and sent with `Content-Type: application/json`. The generic
response wrapper exposes `StatusCode` and a typed `Response`.

The code currently blocks on `.Result` for HTTP tasks inside `async` methods.
It only reads and deserializes the response body for exactly HTTP 200, so a
successful endpoint returning 201, 202, or 204 will not yield a parsed response.
Non-200 response bodies are discarded by the generic method. Its status check
and permissive deserializer therefore do not provide the error detail a new
client should rely on. These are sample behaviors, not recommended production
semantics.

For DELETE operations with a request body, the generic service sends POST and
adds `_method: DELETE` to the content headers. Bodyless DELETE requests use
HTTP DELETE. This distinction affects the close-position and delete-working-
order methods.

### Minimal login example

This shows the sample's orchestration pattern, not a recommendation to retain
passwords in application configuration. Supply credentials from a secret
provider or secure user-input flow in a new app. Treat this as a method example;
keep the `IgRestApiClient` and its conversation context in the application's
session service so later requests and streaming setup can reuse them.

```csharp
using System;
using System.Net;
using System.Threading.Tasks;
using dto.endpoint.auth.session.v2;
using IGWebApiClient;
using IGWebApiClient.Common;

public async Task<IgResponse<AuthenticationResponse>> LoginAsync(
    string username,
    string password,
    string apiKey,
    PropertyEventDispatcher eventDispatcher)
{
    var client = new IgRestApiClient("demo", eventDispatcher);
    var request = new AuthenticationRequest
    {
        identifier = username,
        password = password
    };

    IgResponse<AuthenticationResponse> result =
        await client.SecureAuthenticate(request, apiKey);

    if (result.StatusCode != HttpStatusCode.OK || result.Response == null)
    {
        throw new InvalidOperationException(
            $"IG authentication failed: {result.StatusCode}");
    }

    return result;
}
```

The secure helper first calls `GET /gateway/deal/session/encryptionKey` (API
version 1). When a key is returned, it combines the password and timestamp,
base64-encodes that UTF-8 string, and RSA-encrypts the encoded bytes. It sets
`encryptedPassword=true`; otherwise it sends the original password with the
flag false. It then posts `/gateway/deal/session` with version 2. Response
headers are parsed into the shared conversation context.

### Read a REST snapshot

```csharp
using System.Net;
using dto.endpoint.positions.get.otc.v2;
using IGWebApiClient;

IgResponse<PositionsResponse> positions =
    await client.getOTCOpenPositionsV2();

if (positions.StatusCode == HttpStatusCode.OK &&
    positions.Response?.positions != null)
{
    foreach (var item in positions.Response.positions)
    {
        string epic = item.market.epic;
        decimal size = item.position.size;
        // Map into a view model, then subscribe to distinct streamable EPICs.
    }
}
```

The sample uses position V2 and working order V2 for these screens. Check nulls
and status codes before mapping; do not assume all fields are populated.

### Open Lightstreamer and subscribe

```csharp
// Assume accountId, session, epics, and listeners come from the authenticated
// session and the active view model.
using IGWebApiClient;
using Lightstreamer.DotNet.Client;

var stream = new IGStreamingApiClient();
bool connected = stream.Connect(
    accountId,
    session.cst,
    session.xSecurityToken,
    session.apiKey,
    streamingEndpoint);

if (!connected)
{
    throw new InvalidOperationException("Could not open IG stream");
}

var accountKey = stream.SubscribeToAccountDetails(accountId, accountListener);
var tradeKey = stream.SubscribeToTradeSubscription(accountId, tradeListener);
var priceKey = stream.SubscribeToMarketDetails(epics, priceListener);

// Keep each returned key and release it when its view is no longer active:
// stream.UnsubscribeTableKey(priceKey);
```

The client configures the Lightstreamer `DEFAULT` adapter, account ID as the
connection user, and password formatted as
`CST-<CST>|XST-<X-SECURITY-TOKEN>`. The `apiKey` argument to `Connect` is not
used to configure `ConnectionInfo` in the checked-in method. Verify current IG
streaming requirements before reproducing this behavior.

Default subscriptions use:

| Subscription | Item names | Mode | Default fields |
|--------------|------------|------|----------------|
| Account details | `ACCOUNT:<accountId>` | MERGE | `PNL`, `DEPOSIT`, `USED_MARGIN`, `AMOUNT_DUE`, `AVAILABLE_CASH` |
| Trade | `TRADE:<accountId>` | DISTINCT | `CONFIRMS`, `OPU`, `WOU` |
| L1 prices | `L1:<epic>` | MERGE | `MID_OPEN`, `HIGH`, `LOW`, `CHANGE`, `CHANGE_PCT`, `UPDATE_TIME`, `MARKET_DELAY`, `MARKET_STATE`, `BID`, `OFFER` |
| Chart ticks | `CHART:<epic>:TICK` | DISTINCT | `BID`, `OFR`, `LTP`, `LTV`, `TTV`, `UTM`, daily summary fields |
| Chart candles | `CHART:<epic>:<scale>` | MERGE | bid, offer, last-traded OHLC, volumes, update time, consolidation and daily summary fields |

Supported candle scales map to Lightstreamer item suffixes `SECOND`, `1MINUTE`,
`5MINUTE`, and `HOUR`. The Watchlists view requests `1MINUTE`.

### Endpoint catalogue

All paths below are appended to `/gateway/deal`. API version is carried in the
`VERSION` header. “UI” marks operations called by the WPF sample.

| Method | Path | Version | Client method / usage |
|--------|------|---------|-----------------------|
| POST | `/session` | 2 | `authenticate`; UI login |
| GET | `/session/encryptionKey` | 1 | `fetchEncryptionKey`; UI secure login |
| DELETE | `/session` | 1 | `logout`; UI exit |
| GET | `/marketnavigation` | 1 | `browseRoot`; UI Browse |
| GET | `/marketnavigation/{nodeId}` | 1 | `browse`; UI Browse |
| GET | `/positions` | 1 or 2 | `getOTCOpenPositionsV1/V2`; UI uses V2 |
| GET | `/positions/{dealId}` | 1 or 2 | `getOTCOpenPositionByDealIdV1/V2` |
| POST | `/positions/otc` | 1 or 2 | `createPositionV1/V2`; library only |
| PUT | `/positions/otc/{dealId}` | 1 or 2 | `editPositionV1/V2`; library only |
| DELETE | `/positions/otc` | 1 | `closePosition`; body uses POST override |
| GET | `/workingorders` | 1 or 2 | `workingOrdersV1/V2`; UI uses V2 |
| POST | `/workingorders/otc` | 1 or 2 | `createWorkingOrderV1/V2`; library only |
| PUT | `/workingorders/otc/{dealId}` | 1 or 2 | `editWorkingOrderV1/V2`; library only |
| DELETE | `/workingorders/otc/{dealId}` | 1 | `deleteWorkingOrder`; body uses POST override |
| GET | `/watchlists` | 1 | `listOfWatchlists`; UI Watchlists |
| GET | `/watchlists/{watchlistId}` | 1 | `instrumentsForWatchlist`; UI Watchlists |
| POST | `/watchlists` | 1 | `createWatchlist`; library only |
| PUT | `/watchlists/{watchlistId}` | 1 | `addInstrumentToWatchlist`; library only |
| DELETE | `/watchlists/{watchlistId}` | 1 | `deleteWatchlist`; library only |
| DELETE | `/watchlists/{watchlistId}/{epic}` | 1 | `removeInstrumentFromWatchlist`; library only |
| GET | `/accounts` | 1 | `accountBalance`; library only |
| PUT | `/session` | 1 | `accountSwitch`; library only |
| GET | `/history/activity/{lastPeriod}` | 1 | `lastActivityPeriod`; library only |
| GET | `/history/activity/{fromDate}/{toDate}` | 1 | `lastActivityTimeRange`; library only |
| GET | `/history/transactions/{type}/{lastPeriod}` | 1 | `lastTransactionPeriod`; library only |
| GET | `/history/transactions/{type}/{fromDate}/{toDate}` | 1 | `lastTransactionTimeRange`; library only |
| GET | `/markets/{epic}` | 1 or 2 | `marketDetails` / `marketDetailsV2`; library only |
| GET | `/markets?epics={csv}` | 1 | `marketDetailsMulti`; library only |
| GET | `/markets?searchTerm={term}` | 1 | `searchMarket`; library only |
| GET | `/prices/{epic}/{resolution}?startdate={start}&enddate={end}` | 1 | `priceSearchByDate`; library only |
| GET | `/prices/{epic}/{resolution}/{numPoints}` | 1 | `priceSearchByNum`; library only |
| GET | `/prices/{epic}/{resolution}/{start}/{end}` | 2 | `priceSearchByDateV2`; library only |
| GET | `/prices/{epic}/{resolution}/{numPoints}` | 2 | `priceSearchByNumV2`; library only |
| GET | `/sprintmarkets` | 1 | `findAll`; library only |
| GET | `/clientsentiment/{marketId}` | 1 | `getClientSentiment`; library only |
| GET | `/clientsentiment/related/{marketId}` | 1 | `getRelatedClientSentiment`; library only |
| GET | `/confirms/{dealReference}` | 1 | `retrieveConfirm`; library only |
| GET | `/operations/application` | 1 | `findClientApplications`; library only |
| PUT | `/operations/application` | 1 | `update`; library only |
| PUT | `/operations/application/disable` | 1 | `disableApplication`; library only |

The API client exposes additional DTOs for funding, settings, and other features
that are not mapped by a public REST method in this class. DTO presence alone
does not prove a callable endpoint is implemented.

## Evidence (repo references)

* `IGWebApiClient/IGRestService.cs:28-76,130-222`: base URL, headers, verbs,
  status handling, and serialization.
* `IGWebApiClient/IGRestApiClient.cs:34-128`: context, secure authentication,
  and login endpoints.
* `IGWebApiClient/IGRestApiClient.cs:134-365`: browse, account, history,
  application, sentiment, confirmation, and market endpoints.
* `IGWebApiClient/IGRestApiClient.cs:375-506`: positions and price endpoints.
* `IGWebApiClient/IGRestApiClient.cs:516-628`: search, watchlist, and working
  order endpoints.
* `IGWebApiClient/IGStreamingApi.cs:544-823`: connection, subscriptions,
  field names, and callbacks.
* `SampleWPFTrader/ViewModel/ApplicationViewModel.cs:250-300`:
  UI authentication and stream startup.

## Open questions / assumptions (if any)

* Assumption: Routes and API versions document this repository's implementation,
  not the current IG API contract.
  * Missing evidence: No checked-in OpenAPI specification or current vendor
    reference.
  * Risk/impact: Endpoints, body formats, auth, field names, and streaming
    requirements may have changed.
* Assumption: `apiKey` is intentionally accepted by `IGStreamingApiClient.Connect`
  but unused in this implementation.
  * Missing evidence: No comments explain the omission.
  * Risk/impact: Copying the connection routine without checking vendor
    documentation may fail or omit required headers.
