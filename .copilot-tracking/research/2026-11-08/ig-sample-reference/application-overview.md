# Application overview — IG WPF Trader sample

## Purpose
Explain what the application does from a user and product perspective.

## Key points (TL;DR)
- `SampleWPFTrader` is a WPF desktop UI that demonstrates:
  - logging into IG via REST
  - viewing account summary and receiving live account/trade updates via streaming
  - viewing open positions and working orders
  - listing watchlists, streaming L1 prices for watchlist instruments, and streaming 1-minute candle data
  - browsing the IG market navigation hierarchy and streaming L1 prices for browsed instruments
- `IGWebApiClient` is a reusable client library that wraps the IG REST endpoints and Lightstreamer subscriptions.

## Details
### What the user sees
The main window is a tabbed UI with:
- **Home**: account list and trade subscription updates (OPU/WOU/CONFIRMS) plus a debug/status log.
- **Positions**: open OTC positions and streaming L1 price updates for the unique epics.
- **Working Orders**: open working orders and streaming L1 updates for their epics.
- **Watchlists**: watchlists, watchlist instruments with L1 streaming, and chart candle data.
- **Browse**: market navigation nodes and leaf markets, with L1 streaming for leaf markets.

### What it’s intended to demonstrate
- How to authenticate against IG’s REST API and capture session tokens.
- How to connect to IG’s Lightstreamer endpoint returned by login.
- How to subscribe to key Lightstreamer item namespaces (`ACCOUNT`, `TRADE`, `L1`, `CHART`) and map updates into UI-bound models.

## Evidence (repo references)
- `README.md` (lines 7–35): describes the repository as a .NET WPF sample for IG REST + Streaming APIs and identifies the two projects.
- `SampleWPFTrader/MainWindow.xaml` (lines 41–305): defines the tabbed UI and binds each tab to a view model (`ApplicationViewModel`, `PositionsViewModel`, `OrdersViewModel`, `WatchlistsViewModel`, `BrowseViewModel`).
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` (lines 229–326): login flow reads config and establishes REST + streaming sessions.
- `SampleWPFTrader/ViewModel/PositionsViewModel.cs` (lines 125–176): loads open positions via REST and subscribes to L1 prices.
- `SampleWPFTrader/ViewModel/OrdersViewModel.cs` (lines 210–242): loads working orders via REST and subscribes to L1 prices.
- `SampleWPFTrader/ViewModel/WatchlistsViewModel.cs` (lines 348–425, 463–509): loads watchlists/instruments via REST, then subscribes to L1 prices and candle charts.
- `SampleWPFTrader/ViewModel/BrowseViewModel.cs` (lines 278–321, 182–251): browses the market hierarchy via REST and subscribes to L1 prices for leaf markets.

## Open questions / assumptions (if any)
- Assumption: UI tabs represent the primary supported workflows; there are no hidden features outside these view models.
  - Missing evidence: There is no formal requirements document in-repo.
  - Risk/impact: A reimplementation might miss non-UI scenarios if they existed externally.