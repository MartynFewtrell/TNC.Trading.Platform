# Appendix: Code Map

## Purpose

Provide a curated path through the source for engineers rebuilding this sample.

## Key points (TL;DR)

* Start with project configuration, then authentication and the transport layer.
* Follow each tab view model to its REST operation and streaming subscription.
* Use endpoint DTOs for wire shapes and `IgPublicApiData` for UI projections.
* Confirm current IG API contracts before copying any protocol details.

## Details

### Start here

| Need to understand | Source |
|-------------------|--------|
| Overall purpose and old setup | `README.md` |
| Complete project composition | `IGWebApiDotNetSample.sln` |
| WPF runtime dependencies | `SampleWPFTrader/SampleWPFTrader.csproj` |
| IG client dependencies and target | `IGWebApiClient/IGWebApiClient.csproj` |
| App config and account credentials | `SampleWPFTrader/App.config` |
| Main UI tabs and bindings | `SampleWPFTrader/MainWindow.xaml` |

### IG protocol implementation

| Concern | Source |
|---------|--------|
| Named REST methods and routes | `IGWebApiClient/IGRestApiClient.cs` |
| HTTP transport and headers | `IGWebApiClient/IGRestService.cs` |
| API response wrapper | `IGWebApiClient/IGResponse.cs` |
| Password encryption | `IGWebApiClient/Security/Rsa.cs` |
| Lightstreamer connection and tables | `IGWebApiClient/IGStreamingApi.cs` |
| Login and session setup | `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` |

### UI workflows

* Market browse: `SampleWPFTrader/ViewModel/BrowseViewModel.cs`
* Account, trade stream, login, and exit:
  `SampleWPFTrader/ViewModel/ApplicationViewModel.cs`
* Open positions: `SampleWPFTrader/ViewModel/PositionsViewModel.cs`
* Working orders: `SampleWPFTrader/ViewModel/OrdersViewModel.cs`
* Watchlist snapshots and chart subscriptions:
  `SampleWPFTrader/ViewModel/WatchlistsViewModel.cs`
* Shared session state and client initialization:
  `SampleWPFTrader/ViewModel/ViewModelBase.cs`
* Observable UI projections: `SampleWPFTrader/Model/IgPublicApiData.cs`
* UI-thread dispatch: `SampleWPFTrader/Common/SmartDispatcher.cs`

### Wire DTOs

Endpoint models are under `IGWebApiClient/Model/dto/endpoint/`. Useful starting
folders include:

* `auth/session/v2/` for login request and response.
* `browse/` for market hierarchy.
* `positions/get/otc/v2/` for open positions.
* `workingorders/get/v2/` for working orders.
* `watchlists/retrieve/` for watchlist and market responses.
* `marketdetails/v1/` and `marketdetails/v2/` for market snapshots.
* `prices/v1/` and `prices/v2/` for historical prices.

## Evidence (repo references)

* `IGWebApiDotNetSample.sln`: solution entry point.
* `IGWebApiClient/IGRestApiClient.cs`: REST mapping.
* `IGWebApiClient/IGStreamingApi.cs`: stream mapping.
* `SampleWPFTrader/MainWindow.xaml`: view composition.
* `IGWebApiClient/Model/dto/endpoint/`: wire contract tree.
* `SampleWPFTrader/Model/IgPublicApiData.cs`: UI model tree.

## Open questions / assumptions (if any)

* Assumption: The file map is sufficient to locate the primary behavior.
  * Missing evidence: No external application or design specification.
  * Risk/impact: This map does not cover every individual DTO property or
    transitive behavior inside the bundled Lightstreamer library.
