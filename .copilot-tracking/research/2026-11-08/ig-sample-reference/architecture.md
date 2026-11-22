# Architecture

## Purpose
Describe the system architecture, boundaries, and runtime data flows between the WPF app, IG REST API, and IG Lightstreamer streaming endpoint.

## Key points (TL;DR)
- This is a **client-side desktop app**; it does not host server APIs and has no database.
- REST calls go to `https://{env}api.ig.com/gateway/deal/...` and rely on session headers (`CST`, `X-SECURITY-TOKEN`) plus an API key.
- Streaming uses the Lightstreamer .NET client with item namespaces like `ACCOUNT:<accountId>`, `TRADE:<accountId>`, `L1:<epic>`, and `CHART:<epic>:<scale>`.

## Details
### System context
```mermaid
flowchart LR
  User[User] -->|UI interactions| WPF[SampleWPFTrader (WPF)]
  WPF -->|REST JSON over HTTPS| IGREST[IG REST API\nhttps://{env}api.ig.com/gateway/deal]
  WPF -->|Lightstreamer streaming| LS[IG Lightstreamer endpoint\n(from login response)]
  WPF -->|uses| ClientLib[IGWebApiClient (.NET library)]
  ClientLib --> IGREST
  ClientLib --> LS
```

### Component boundaries
- `SampleWPFTrader` (WPF UI)
  - owns user interactions and view models
  - binds observable collections to `DataGrid` controls
  - uses a simple in-app status log (`ApplicationDebugData`)
- `IGWebApiClient` (library)
  - `IgRestApiClient` wraps REST endpoints and returns `IgResponse<T>`
  - `IgRestService` handles base URL selection, header management, and JSON serialization
  - `IGStreamingApiClient` wraps Lightstreamer connection and subscriptions
  - DTOs live under `IGWebApiClient/Model/dto/...`

### REST call architecture
- A `ConversationContext` holds:
  - `apiKey`
  - `cst` token
  - `xSecurityToken`
- `IgRestService.SetDefaultRequestHeaders` adds the headers for every call, including a `VERSION` header representing the IG API version.

### Streaming architecture
- Streaming uses `Lightstreamer.DotNet.Client.LSClient`.
- The Lightstreamer connection uses:
  - `ConnectionInfo.User` = value passed to `Connect` (the WPF app passes `currentAccountId`)
  - `ConnectionInfo.Password` = `CST-{cst}|XST-{xSecurityToken}`
  - `ConnectionInfo.PushServerUrl` = `lightstreamerEndpoint` returned by login

## Evidence (repo references)
- `IGWebApiClient/IGRestService.cs` (lines 14–26, 30–37, 57–75): `ConversationContext`, base URL selection by environment, and REST header construction (`X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, `VERSION`).
- `IGWebApiClient/IGRestApiClient.cs` (lines 55–118): secure authentication uses `/gateway/deal/session/encryptionKey` and `/gateway/deal/session`.
- `IGWebApiClient/IGStreamingApi.cs` (lines 527–566, 610–673, 696–721, 750–772): Lightstreamer connection and subscriptions (`ACCOUNT`, `TRADE`, `L1`, `CHART`).
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` (lines 277–302): connects streaming using `currentAccountId`, `cst`, `xSecurityToken`, and `lightstreamerEndpoint`.

## Open questions / assumptions (if any)
- Assumption: The Lightstreamer adapter name `DEFAULT` and item/field names are stable and valid for the returned Lightstreamer endpoint.
  - Missing evidence: No Lightstreamer schema documentation is included in-repo.
  - Risk/impact: A reimplementation may need to adjust item names/fields to match current IG streaming schemas.