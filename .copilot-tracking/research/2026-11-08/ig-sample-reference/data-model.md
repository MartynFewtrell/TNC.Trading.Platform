# Data model

## Purpose
Describe the application’s data model (persistent and in-memory), key entities, and how data flows from REST/streaming into UI-bound state.

## Key points (TL;DR)
- There is **no persistence layer** (no database, no migrations); everything is in-memory.
- REST data uses DTOs under `IGWebApiClient/Model/dto/endpoint/...`.
- UI state is represented by `SampleWPFTrader/Model/IgPublicApiData.cs` classes and `ObservableCollection<T>` instances.
- Streaming updates are mapped into lightweight “update data” classes (`L1LsPriceData`, `StreamingAccountData`, `ChartCandelData`, etc.).

## Details
## In-memory UI model (WPF)
The WPF view models expose `ObservableCollection<T>` collections bound to `DataGrid`s:
- `Accounts`: account summary rows.
- `TradeSubscriptions`: trade update rows (OPU/WOU/CONFIRMS).
- `AffectedDeals`: affected deal rows extracted from trade updates.
- `Positions`: open positions (from REST) with L1 streaming updates.
- `Orders`: working orders (from REST) with L1 streaming updates.
- `Watchlists`, `WatchlistMarkets`: watchlists and their instruments (from REST) with L1 updates.
- `ChartMarketData`: chart candle data per epic (from streaming).
- `BrowseNodes`, `BrowseMarkets`: browse tree nodes and leaf markets.

UI models are implemented as nested classes inside `IgPublicApiData` and generally derive from a property-change base so WPF bindings update.

## REST DTO model (client library)
- REST request/response payloads are modeled under `IGWebApiClient/Model/dto/endpoint/...`.
- `IgRestApiClient` methods return `IgResponse<T>`, which wraps `HttpStatusCode` and the deserialized `T`.

## Streaming update model
The streaming layer converts Lightstreamer field updates into typed objects:
- Account fields: `PNL`, `DEPOSIT`, `USED_MARGIN`, `AMOUNT_DUE`, `AVAILABLE_CASH` -> `StreamingAccountData`.
- L1 price fields: `MID_OPEN`, `HIGH`, `LOW`, `CHANGE`, `CHANGE_PCT`, `UPDATE_TIME`, `MARKET_DELAY`, `MARKET_STATE`, `BID`, `OFFER` -> `L1LsPriceData`.
- Chart candle fields: bid/offer/LTP OHLC, plus daily stats -> `ChartCandelData`.
- Trade updates: JSON payload in `CONFIRMS`, `OPU`, `WOU` fields -> `LsTradeSubscriptionData` (JSON) -> WPF `TradeSubscriptionModel`.

## Evidence (repo references)
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` (lines 34–38, 85–90): observable collections for Accounts, TradeSubscriptions, AffectedDeals.
- `SampleWPFTrader/ViewModel/PositionsViewModel.cs` (lines 69–85): positions collection.
- `SampleWPFTrader/ViewModel/OrdersViewModel.cs` (lines 24–36, 96–97): orders collection.
- `SampleWPFTrader/ViewModel/WatchlistsViewModel.cs` (lines 31–35, 127–132): watchlists + markets + chart data collections.
- `IGWebApiClient/IGResponse.cs` (lines 10–29): `IgResponse<T>` wrapper and its implicit conversion to `bool`.
- `IGWebApiClient/IGStreamingApi.cs` (lines 84–92, 70–82, 470–509): streaming update data classes (`StreamingAccountData`, `L1LsPriceData`, `ChartCandelData`).
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` (lines 447–463): trade updates read from streaming fields and parsed as JSON.

## Open questions / assumptions (if any)
- Assumption: DTOs under `Model/dto` align with IG’s API responses for the versions used (`VERSION` header values `1` and `2`).
  - Missing evidence: No contract tests or captured JSON samples are included.
  - Risk/impact: Reimplementation may need to adjust for missing/renamed fields over time.