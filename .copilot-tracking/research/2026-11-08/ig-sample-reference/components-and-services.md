# Components & services

## Purpose
Document each project/module, its responsibilities, and the key “start here” types for a clean-room reimplementation.

## Key points (TL;DR)
- `SampleWPFTrader` is the WPF UI project; its view models orchestrate REST calls + streaming subscriptions.
- `IGWebApiClient` is the integration library; it contains:
  - `IgRestApiClient` (endpoint wrapper)
  - `IgRestService` (HTTP + headers + JSON)
  - `IGStreamingApiClient` (Lightstreamer connection + subscription helpers)
  - DTOs for REST payloads under `Model/dto/endpoint/...`

## Details
## `SampleWPFTrader` (WPF desktop app)
### Responsibilities
- Startup and view composition
- UI bindings for accounts, positions, orders, watchlists, browse tree, charts
- Converting streaming updates into UI model updates

### Key types
- `MainWindow` (XAML): defines tabbed UI and binds each tab to a view model resource.
- `ApplicationViewModel`: login + account/trade streaming subscriptions; provides the status/debug text.
- `PositionsViewModel`, `OrdersViewModel`, `WatchlistsViewModel`, `BrowseViewModel`: each tab’s REST load + streaming subscribe/unsubscribe logic.
- `IgPublicApiData`: UI-bound models (instrument, position, order, chart, etc.).
- `SmartDispatcher`: marshals property change and “event messages” to the WPF UI thread.

### Dependencies
- MVVM Light assemblies are referenced, but command handling in this repo uses a local `RelayCommand` implementation.
- Lightstreamer client is referenced via `DotNetClient_N2.dll` from `packages/3rdPartyDlls`.

## `IGWebApiClient` (REST + streaming client library)
### Responsibilities
- Authenticate to IG (including optional password encryption using `/session/encryptionKey`).
- Maintain and apply session tokens via `ConversationContext`.
- Provide strongly-typed DTOs for REST payloads.
- Provide Lightstreamer helper subscription methods with typed table listeners.

### Key types
- `IgRestApiClient`: single entry point for REST operations.
- `IgRestService`: base URL selection from environment + request headers + JSON serialization/deserialization.
- `IGStreamingApiClient`: connection management and subscription helpers for account, trade, L1, and chart topics.
- `Rsa`: wraps PCLCrypto RSA PKCS1 encryption for password encryption.

## Evidence (repo references)
- `IGWebApiDotNetSample.sln` (lines 6–9): declares the two projects.
- `SampleWPFTrader/SampleWPFTrader.csproj` (lines 8–12, 44–46, 170–173): WPF WinExe targeting .NET Framework 4.6.1; references `DotNetClient_N2.dll`; references `IGWebApiClient`.
- `IGWebApiClient/IGWebApiClient.csproj` (lines 9–16, 41–47): library targeting .NET 4.5 portable profile; includes `IgRestApiClient`, `IgRestService`, `IGStreamingApi`, and DTOs.
- `SampleWPFTrader/ICommand.cs` (lines 9–46): local `RelayCommand` implementation used by view models.
- `SampleWPFTrader/Common/SmartDispatcher.cs` (lines 9–56): dispatcher implementation used for UI thread marshalling.

## Open questions / assumptions (if any)
- Assumption: All required Lightstreamer client binaries are present under `packages/3rdPartyDlls` and are compatible with the target environment.
  - Missing evidence: No build pipeline or package verification exists in-repo.
  - Risk/impact: Reimplementation may require updating streaming dependencies and API compatibility testing.