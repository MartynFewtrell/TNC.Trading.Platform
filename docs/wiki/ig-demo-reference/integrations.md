# Integrations

## Purpose

Inventory external services, protocols, and runtime dependencies.

## Key points (TL;DR)

* The app integrates directly with IG's HTTPS REST API and its Lightstreamer
  endpoint.
* Lightstreamer is supplied by the IG login response and accessed through a
  bundled `DotNetClient_N2.dll`.
* Newtonsoft.Json handles REST and trade update JSON.
* The projects rely on older NuGet packages and committed third-party DLLs.
* No message queue, schedule, file exchange, or database integration appears.

## Details

### IG REST

The client selects `https://demo-api.ig.com` unless the configured environment
equals `live`, in which case it uses `https://api.ig.com`. It calls versioned
`/gateway/deal` routes and sends API key plus session headers as applicable.
See the [API reference](./api-reference.md) for the endpoint catalogue.

### Lightstreamer

After REST login, the app uses the returned Lightstreamer endpoint and session
tokens to establish a push connection. It subscribes to account, trade, L1
price, and chart items. Listener adapters map fields into typed records and
notify view models.

### Libraries

The client project references Newtonsoft.Json 9.0.1, PCLCrypto, and
`DotNetClient_N2.dll`, among other dependencies. The WPF project references
MVVM Light 5.3 and Newtonsoft.Json 9.0.1. NuGet manifests use the legacy
`packages.config` format. A set of DLLs under `packages/3rdPartyDlls` is
committed in the repository.

No evidence shows a separate telemetry exporter, database, external payment
provider workflow, or deployment-time service integration. The DTO tree has
funding and settings model classes, but this is not evidence that the app uses
those services.

## Evidence (repo references)

* `IGWebApiClient/IGRestService.cs:28-37`: environment-specific REST host.
* `SampleWPFTrader/ViewModel/ApplicationViewModel.cs:279-300`: use of the
  returned Lightstreamer endpoint.
* `IGWebApiClient/IGStreamingApi.cs:544-557,622-773`: stream connection and
  subscriptions.
* `IGWebApiClient/IGWebApiClient.csproj:208-244` and
  `IGWebApiClient/packages.config`: client dependencies.
* `SampleWPFTrader/SampleWPFTrader.csproj:42-88` and
  `SampleWPFTrader/packages.config`: WPF dependencies.

## Open questions / assumptions (if any)

* Assumption: No other runtime integration is hidden in binaries or configuration.
  * Missing evidence: Third-party DLL internals are not inspected here.
  * Risk/impact: Bundled libraries may have undocumented runtime behavior.
