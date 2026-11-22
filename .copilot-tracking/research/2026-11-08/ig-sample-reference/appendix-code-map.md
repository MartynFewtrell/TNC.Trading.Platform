# Appendix: code map

## Purpose
Provide a curated map of the repository with “start here” pointers for engineers reading the code.

## Key points (TL;DR)
- Start with `IGWebApiDotNetSample.sln` and `README.md`.
- UI composition is in `SampleWPFTrader/MainWindow.xaml`.
- REST + streaming integration is in `IGWebApiClient` (`IgRestApiClient`, `IgRestService`, `IGStreamingApiClient`).

## Details
## Top-level
- `README.md`: repository overview + basic run steps.
- `IGWebApiDotNetSample.sln`: solution containing both projects.
- `packages/3rdPartyDlls/`: Lightstreamer and crypto-related DLLs.

## `SampleWPFTrader/` (WPF app)
Start here:
- `SampleWPFTrader/App.xaml`: WPF startup (`StartupUri`).
- `SampleWPFTrader/MainWindow.xaml`: tab UI, bindings to view models.

View models:
- `SampleWPFTrader/ViewModel/ViewModelBase.cs`: owns `IgRestApiClient`, `IGStreamingApiClient`, and shared `LoggedIn` state.
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs`: login + account/trade subscriptions.
- `SampleWPFTrader/ViewModel/PositionsViewModel.cs`: positions REST load + L1 price subscription.
- `SampleWPFTrader/ViewModel/OrdersViewModel.cs`: working orders REST load + L1 subscription.
- `SampleWPFTrader/ViewModel/WatchlistsViewModel.cs`: watchlists REST load + L1 + chart subscriptions.
- `SampleWPFTrader/ViewModel/BrowseViewModel.cs`: market navigation REST load + L1 subscription.

Models/utilities:
- `SampleWPFTrader/Model/IgPublicApiData.cs`: UI-bound model classes.
- `SampleWPFTrader/Common/SmartDispatcher.cs`: UI-thread marshalling and event message forwarding.
- `SampleWPFTrader/ICommand.cs`: `RelayCommand`.

## `IGWebApiClient/` (integration library)
Start here:
- `IGWebApiClient/IGRestApiClient.cs`: REST endpoint wrapper methods.
- `IGWebApiClient/IGRestService.cs`: base URL + request headers + JSON serialization.
- `IGWebApiClient/IGStreamingApi.cs`: streaming client, listener adapters, item/field naming.
- `IGWebApiClient/Security/Rsa.cs`: RSA encryption wrapper.

DTOs:
- `IGWebApiClient/Model/dto/endpoint/...`: request/response payload types grouped by endpoint.

## Evidence (repo references)
- `README.md` (lines 7–35): repo overview and project mapping.
- `IGWebApiDotNetSample.sln` (lines 6–9): solution projects.
- `SampleWPFTrader/MainWindow.xaml` (lines 41–305): tab layout and view model bindings.
- `IGWebApiClient/IGRestApiClient.cs` (lines 55–631): endpoint wrapper inventory.
- `IGWebApiClient/IGStreamingApi.cs` (lines 527–837): streaming client and subscription methods.

## Open questions / assumptions (if any)
- Assumption: The DTO directory structure under `Model/dto/endpoint` is complete for the endpoints implemented.
  - Missing evidence: No code generation scripts or API schema sources are included.
  - Risk/impact: Reimplementation might choose to regenerate DTOs from an external OpenAPI/spec rather than hand-maintain them.