# Data Model

## Purpose

Explain the data contracts and how REST and streaming values appear in the UI.

## Key points (TL;DR)

* The repository defines endpoint DTOs; it has no database schema or migrations.
* REST DTOs closely follow IG JSON property names and are grouped by endpoint
  and API version.
* UI models in `IgPublicApiData.cs` project API objects into data-grid fields.
* Streaming data is partial: only non-empty values are converted to nullable
  values and updates patch existing models.

## Details

### REST DTOs

The `Model/dto/endpoint/` tree separates authentication, account, market,
position, pricing, watchlist, and order contracts. Version-specific DTO folders
preserve API-version differences, for example market details and positions V1
versus V2. Request DTOs are serialized as JSON; response DTOs are populated
through Newtonsoft.Json.

The login V2 request includes `identifier`, `password`, and `encryptedPassword`.
Its response includes the active account ID, a list of account summaries,
account information, client ID, and the Lightstreamer endpoint. The app reads
only a subset for its screens and connection startup.

Positions and working orders use nested response structures. The view models
read `positions[].market` and `positions[].position`, or
`workingOrders[].marketData` and `workingOrders[].workingOrderData`, then project
fields into UI-facing models.

### UI projections

`IgPublicApiData` defines nested presentation classes for accounts, instruments,
positions, working orders, watchlists, charts, affected deals, and trade
updates. Those models notify WPF when property values change. Observable
collections notify the UI when rows are added or removed.

The app does not cache or persist these objects. Switching tabs or watchlists
causes new snapshots to be loaded and previous stream subscriptions to be
removed.

### Streaming contracts

The Lightstreamer layer parses account fields such as `PNL`, `DEPOSIT`,
`USED_MARGIN`, `AMOUNT_DUE`, and `AVAILABLE_CASH`; L1 fields such as `BID`,
`OFFER`, `HIGH`, `LOW`, `CHANGE`, `CHANGE_PCT`, and `MARKET_STATE`; and candle
fields for bid, offer, and last-traded OHLC values. The `Trade` item exposes
`CONFIRMS`, `OPU`, and `WOU`; each non-empty field is JSON-deserialized as a
trade update with deal identifiers, direction, size, levels, status, and
affected deals.

### Persistence and migrations

There is no database, repository, entity framework usage, schema migration, or
local persistence flow in the checked-in application. `App.config` contains an
Entity Framework section, but the project does not declare Entity Framework
references or use a DbContext in the application code.

## Evidence (repo references)

* `IGWebApiClient/Model/dto/endpoint/auth/session/v2/AuthenticationRequest.cs`:
  login request fields.
* `IGWebApiClient/Model/dto/endpoint/auth/session/v2/AuthenticationResponse.cs`:
  account and streaming connection response fields.
* `IGWebApiClient/Model/dto/endpoint/positions/get/otc/v2/PositionsResponse.cs`:
  positions collection.
* `IGWebApiClient/Model/dto/endpoint/workingorders/get/v2/WorkingOrdersResponse.cs`:
  working order collection.
* `IGWebApiClient/IGStreamingApi.cs:24-48,335-502`: typed streaming fields.
* `SampleWPFTrader/Model/IgPublicApiData.cs`: observable UI models.
* `SampleWPFTrader/ViewModel/PositionsViewModel.cs:130-176` and
  `SampleWPFTrader/ViewModel/OrdersViewModel.cs:216-243`: mapping from API DTOs
  to UI collections.

## Open questions / assumptions (if any)

* Assumption: The absence of persistence extends to all normal execution paths.
  * Missing evidence: Runtime behavior was not executed against IG.
  * Risk/impact: Any server-side retention or telemetry would be external to the
    checked-in application.
