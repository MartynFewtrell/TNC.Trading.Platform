# Application Overview

## Purpose

Describe the user-visible capabilities and limits of the sample app.

## Key points (TL;DR)

* A desktop WPF sample demonstrates IG REST calls plus Lightstreamer updates.
* The Home view authenticates, shows account balances, and displays trade
  confirmations and order or position update events.
* Browse, Positions, Working Orders, and Watchlists views load snapshots by REST
  and refresh market fields using streaming subscriptions.
* Data grids are read-only. The sample UI does not create, edit, or close trades.
* The repository README labels this project deprecated (August 2024).

## Details

### User value and capabilities

The app is an interactive example of joining IG's request-response and push
interfaces. REST establishes an initial view of account and market data.
Lightstreamer then supplies changing account values, L1 prices, chart candles,
and trade lifecycle updates while the relevant view is active.

The visible workflow covers:

* Login and logout, with account list and account summary information.
* Navigating the IG market hierarchy and watching streaming prices for markets
  that advertise streaming availability.
* Displaying open OTC positions and working orders, with live L1 fields.
* Listing watchlists, selecting one, and showing its markets, L1 prices, and
  one-minute candle values.
* Showing CONFIRMS, OPU, and WOU trade-stream payloads and affected deals.

The WPF grids in `MainWindow.xaml` are configured `IsReadOnly="True"`. Although
the REST library contains create, edit, and delete methods for positions,
working orders, and watchlists, the view models shown here only call the
read-oriented operations. The client library's API surface must not be mistaken
for UI capability.

### Boundaries

This repository has no server-side API of its own, database, persistence layer,
deployment infrastructure, or automated test project. The WPF executable talks
directly to IG endpoints from the desktop process. The sample is useful for
learning endpoint mapping and streaming event shapes, but not a current
production blueprint.

## Evidence (repo references)

* `README.md`: deprecation notice and project summary.
* `SampleWPFTrader/MainWindow.xaml`: account, trade update, positions, working
  orders, watchlist, and chart grids; grids are read-only.
* `SampleWPFTrader/ViewModel/ApplicationViewModel.cs:229-308`: login and
  Lightstreamer startup.
* `SampleWPFTrader/ViewModel/BrowseViewModel.cs:182-310`:
  market hierarchy workflow.
* `SampleWPFTrader/ViewModel/PositionsViewModel.cs:125-186` and
  `SampleWPFTrader/ViewModel/OrdersViewModel.cs:210-255`: read-only snapshot
  retrieval and subscriptions.
* `IGWebApiClient/IGRestApiClient.cs:375-628`: additional write methods in the
  client that the sample UI does not call.

## Open questions / assumptions (if any)

* Assumption: No unlisted trading workflows are intended. The checked-in view
  models and XAML are the evidence for the shipped UI.
  * Missing evidence: No product specification or runtime screenshots.
  * Risk/impact: A future replica may add functionality that this sample never
    implemented.
