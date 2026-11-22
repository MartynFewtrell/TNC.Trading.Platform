# API reference

## Purpose
Document the external API surface this repo integrates with (IG REST endpoints + IG Lightstreamer streaming topics), based on the client code.

## Key points (TL;DR)
- REST base URL is derived from `environment` and always targets `https://{env}api.ig.com`.
- Every REST call includes a `VERSION` header (typically `1` or `2`).
- Auth/session headers are managed via `ConversationContext` (`CST`, `X-SECURITY-TOKEN`, `X-IG-API-KEY`).
- Streaming uses Lightstreamer items: `ACCOUNT:<accountId>`, `TRADE:<accountId>`, `L1:<epic>`, and `CHART:<epic>:<scale>`.

## Details
## REST API
### Base URL
- Demo: `https://demo-api.ig.com`
- Live: `https://api.ig.com`
- Other envs (derived): `https://{environment}-api.ig.com`

### Required headers
- `X-IG-API-KEY`: API key
- `CST`: session token (parsed from response headers after login)
- `X-SECURITY-TOKEN`: session token (parsed from response headers after login)
- `VERSION`: IG API version string used by the endpoint wrapper

### Endpoint inventory (from `IgRestApiClient`)
> Naming below uses `METHOD path (VERSION)`.

#### Session / auth
- `GET /gateway/deal/session/encryptionKey (1)`
- `POST /gateway/deal/session (2)` — used by `SecureAuthenticate`
- `DELETE /gateway/deal/session (1)` — logout

#### Market navigation / search
- `GET /gateway/deal/marketnavigation (1)`
- `GET /gateway/deal/marketnavigation/{nodeId} (1)`
- `GET /gateway/deal/markets?searchTerm={searchTerm} (1)`
- `GET /gateway/deal/markets/{epic} (1)`
- `GET /gateway/deal/markets/{epic} (2)`
- `GET /gateway/deal/markets?epics={commaSeparatedEpics} (1)`

#### Prices
- `GET /gateway/deal/prices/{epic}/{resolution}?startdate={start}&enddate={end} (1)`
- `GET /gateway/deal/prices/{epic}/{resolution}/{numPoints} (1)`
- `GET /gateway/deal/prices/{epic}/{resolution}/{startDate}/{endDate} (2)`
- `GET /gateway/deal/prices/{epic}/{resolution}/{numPoints} (2)`

#### Accounts
- `GET /gateway/deal/accounts (1)`
- `PUT /gateway/deal/session (1)` — switch active account

#### Positions
- `GET /gateway/deal/positions (1)`
- `GET /gateway/deal/positions (2)`
- `GET /gateway/deal/positions/{dealId} (1)`
- `GET /gateway/deal/positions/{dealId} (2)`
- `POST /gateway/deal/positions/otc (1)`
- `POST /gateway/deal/positions/otc (2)`
- `PUT /gateway/deal/positions/otc/{dealId} (1)`
- `PUT /gateway/deal/positions/otc/{dealId} (2)`
- `DELETE /gateway/deal/positions/otc (1)` — close position(s) (implemented via request body)

#### Working orders
- `GET /gateway/deal/workingorders (1)`
- `GET /gateway/deal/workingorders (2)`
- `POST /gateway/deal/workingorders/otc (1)`
- `POST /gateway/deal/workingorders/otc (2)`
- `PUT /gateway/deal/workingorders/otc/{dealId} (1)`
- `PUT /gateway/deal/workingorders/otc/{dealId} (2)`
- `DELETE /gateway/deal/workingorders/otc/{dealId} (1)` — delete working order (implemented via request body)

#### Watchlists
- `GET /gateway/deal/watchlists (1)`
- `GET /gateway/deal/watchlists/{watchlistId} (1)`
- `POST /gateway/deal/watchlists (1)`
- `PUT /gateway/deal/watchlists/{watchlistId} (1)` — add instrument
- `DELETE /gateway/deal/watchlists/{watchlistId} (1)` — delete watchlist
- `DELETE /gateway/deal/watchlists/{watchlistId}/{epic} (1)` — remove instrument

#### Activity / history
- `GET /gateway/deal/history/activity/{lastPeriodMs} (1)`
- `GET /gateway/deal/history/activity/{fromDate}/{toDate} (1)`
- `GET /gateway/deal/history/transactions/{type}/{lastPeriodMs} (1)`
- `GET /gateway/deal/history/transactions/{type}/{fromDate}/{toDate} (1)`

#### Confirms / sentiment / operations
- `GET /gateway/deal/confirms/{dealReference} (1)`
- `GET /gateway/deal/clientsentiment/{marketId} (1)`
- `GET /gateway/deal/clientsentiment/related/{marketId} (1)`
- `GET /gateway/deal/operations/application (1)`
- `PUT /gateway/deal/operations/application (1)`
- `PUT /gateway/deal/operations/application/disable (1)`

## Streaming API (Lightstreamer)
### Connection
- Adapter: `DEFAULT`
- User: value passed to `Connect` (WPF passes `currentAccountId`)
- Password: `CST-{cst}|XST-{xSecurityToken}`
- Server URL: `lightstreamerEndpoint` from login response

### Subscriptions
| Purpose | Items | Mode | Notable fields |
|---|---|---|---|
| Account details | `ACCOUNT:<accountId>` | `MERGE` | `PNL`, `DEPOSIT`, `USED_MARGIN`, `AMOUNT_DUE`, `AVAILABLE_CASH` |
| Trade updates | `TRADE:<accountId>` | `DISTINCT` | `CONFIRMS`, `OPU`, `WOU` (JSON payloads) |
| Market L1 | `L1:<epic>` | `MERGE` | `BID`, `OFFER`, `CHANGE`, `CHANGE_PCT`, `MARKET_STATE`, ... |
| Chart ticks | `CHART:<epic>:TICK` | `DISTINCT` | `BID`, `OFR`, `LTP`, `UTM`, ... |
| Chart candles | `CHART:<epic>:<scale>` | `MERGE` | bid/offer/LTP OHLC, `UTM`, daily stats |

## Evidence (repo references)
- `IGWebApiClient/IGRestService.cs` (lines 30–37, 57–75): base URL selection and headers including `VERSION`.
- `IGWebApiClient/IGRestApiClient.cs` (lines 105–119, 134–186, 420–631): endpoint wrappers and paths.
- `IGWebApiClient/IGStreamingApi.cs` (lines 544–553, 610–673, 675–721, 750–772): Lightstreamer connection info and subscription item naming.

## Open questions / assumptions (if any)
- Assumption: IG’s API versions `1` and `2` correspond to the expected payload shapes in the included DTOs.
  - Missing evidence: No explicit API versioning documentation is included.
  - Risk/impact: A modern integration may require different versions/headers or updated DTOs.