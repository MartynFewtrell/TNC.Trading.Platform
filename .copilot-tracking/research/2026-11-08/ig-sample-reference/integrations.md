# Integrations

## Purpose
List and describe external systems and libraries this repo integrates with, and how those integrations are used.

## Key points (TL;DR)
- Primary external systems:
  - IG REST API (`/gateway/deal/...`)
  - IG Streaming via Lightstreamer (server URL returned by login)
- Primary external client libraries:
  - Lightstreamer .NET client (`DotNetClient_N2.dll`)
  - Newtonsoft.Json (JSON)
  - PCLCrypto (RSA)

## Details
## IG REST API integration
- All REST endpoints are under `/gateway/deal/...`.
- Session is established via `/gateway/deal/session` and tokens are stored in memory.
- The app uses REST to retrieve:
  - watchlists
  - watchlist instruments
  - positions
  - working orders
  - browse hierarchy

## IG Streaming (Lightstreamer) integration
- Lightstreamer server URL is provided by REST login (`lightstreamerEndpoint`).
- The app subscribes to:
  - account summary updates (`ACCOUNT:<accountId>`)
  - trade updates (`TRADE:<accountId>`)
  - market prices (`L1:<epic>`)
  - chart candle data (`CHART:<epic>:<scale>`)

## Third-party libraries (not exhaustive)
- `Lightstreamer.DotNet.Client` via `DotNetClient_N2.dll` (streaming connection + subscriptions)
- `Newtonsoft.Json` (REST DTO deserialization and trade subscription JSON parsing)
- `PCLCrypto` (RSA encryption)

## Evidence (repo references)
- `IGWebApiClient/IGRestService.cs` (lines 30–37, 130–220): REST base URL, request headers, and JSON deserialization.
- `IGWebApiClient/IGStreamingApi.cs` (lines 6–7, 527–537): Lightstreamer client usage.
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` (lines 380–387, 447–463): trade subscription parsing uses `Newtonsoft.Json`.
- `SampleWPFTrader/SampleWPFTrader.csproj` (lines 44–46, 62–65): references Lightstreamer DLL and Newtonsoft.Json.
- `IGWebApiClient/IGWebApiClient.csproj` (lines 211–238): references Lightstreamer DLL, PCLCrypto, Newtonsoft.Json, and Validation.

## Open questions / assumptions (if any)
- Assumption: The Lightstreamer DLL version in `packages/3rdPartyDlls` matches the schema and behavior expected by the IG streaming endpoint.
  - Missing evidence: No pinned version documentation or compatibility matrix.
  - Risk/impact: A reimplementation may need to upgrade/replace the streaming client library.