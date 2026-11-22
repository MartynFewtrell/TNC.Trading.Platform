# Components and Services

## Purpose

Map each project and major module to its responsibility.

## Key points (TL;DR)

* `IGWebApiClient` contains the REST wrapper, Lightstreamer wrapper, DTOs, and
  encryption helper.
* `SampleWPFTrader` contains the WPF shell, view models, binding models, and
  dispatcher glue.
* The REST class exposes more operations than the UI uses.
* There are no independent service processes.

## Details

| Component | Responsibility |
|-----------|----------------|
| `IGWebApiClient/IGRestApiClient.cs` | Named methods for authentication, market browsing, watchlists, positions, working orders, pricing, account operations, and history |
| `IGWebApiClient/IGRestService.cs` | Base URL, request headers, JSON transport, session header capture, response status, and deserialization |
| `IGWebApiClient/IGStreamingApi.cs` | Lightstreamer connection, table item names, fields, listeners, typed updates, and subscription keys |
| `IGWebApiClient/Model/dto/endpoint/` | Request and response shapes grouped by endpoint and API version |
| `IGWebApiClient/Security/Rsa.cs` | RSA encryption helper used by secure login |
| `SampleWPFTrader/MainWindow.xaml` | WPF tabs, data grids, resources, and view-model bindings |
| `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` | Login/logout, account rows, and trade event display |
| `SampleWPFTrader/ViewModel/BrowseViewModel.cs` | Market navigation and live browse prices |
| `SampleWPFTrader/ViewModel/PositionsViewModel.cs` | Open-position snapshot and live market prices |
| `SampleWPFTrader/ViewModel/OrdersViewModel.cs` | Working-order snapshot and live market prices |
| `SampleWPFTrader/ViewModel/WatchlistsViewModel.cs` | Watchlist and market snapshots, live L1, and candle streams |
| `SampleWPFTrader/Model/IgPublicApiData.cs` | UI-specific observable models distinct from API DTOs |
| `SampleWPFTrader/Common/SmartDispatcher.cs` | Routes selected callback actions onto the WPF dispatcher and formats status messages |

`ViewModelBase.InitialiseViewModel` reads the environment and creates the REST
and streaming clients. The REST client uses a dispatcher abstraction to append
status messages. The WPF app supplies that abstraction with `SmartDispatcher`.

`ViewModelLocator` registers only `MainViewModel`; the actual tab view models
are declared as XAML resources in `MainWindow.xaml`. Do not assume the locator
is a full dependency-injection composition root.

## Evidence (repo references)

* `IGWebApiClient/IGWebApiClient.csproj:12-18,208-263`: portable target and
  module references.
* `SampleWPFTrader/SampleWPFTrader.csproj:8-22,97-173`: WPF target, view-model
  compilation, and client project reference.
* `SampleWPFTrader/ViewModel/ViewModelBase.cs:8-42`: client creation.
* `SampleWPFTrader/MainWindow.xaml:1-20`: XAML-owned view-model instances.

## Open questions / assumptions (if any)

* Assumption: The UI model is a presentation projection rather than persisted
  domain state.
  * Missing evidence: There is no data access or persistence component.
  * Risk/impact: A rebuild requiring durable history needs a new storage design.
