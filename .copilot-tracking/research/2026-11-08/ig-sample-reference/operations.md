# Operations & observability

## Purpose
Describe runtime diagnostics, logging, and operational concerns (connectivity, failures) for this client application.

## Key points (TL;DR)
- Observability is minimal and primarily surfaced in a UI “debug/status” text area.
- REST requests log basic status via `PropertyEventDispatcher.addEventMessage`.
- Error handling is mostly `try/catch` with messages appended to the UI log; there is no structured logging.

## Details
## Status log
- `ViewModelBase` maintains a `ApplicationDebugData` string property.
- `UpdateDebugMessage` prepends timestamped messages to `ApplicationDebugData`.
- The main window binds a `TextBlock` to `ApplicationDebugData`.

## REST diagnostics
- `IgRestService.RestfulService<T>` logs `"{METHOD} request to {url} returned status {StatusCode}"` via `eventDispatcher`.
- REST failures generally propagate as a non-OK `StatusCode` and/or `Response == null`.

## Streaming diagnostics
- `IGStreamingApiClient` implements Lightstreamer `IConnectionListener` callbacks, but the default implementations are empty.
- Subscription listeners convert field updates to typed objects and update UI models.

## Operational risks / tech debt (observed)
- REST execution uses synchronous `.Result` on async HTTP calls inside an `async` method.
  - This can cause thread pool blocking and can deadlock in some synchronization contexts.
- `HttpClient` is instantiated per call and disposed only in some code paths.
- Exception handling in several places catches and discards exceptions without logging.

## Basic runbook
- Login fails:
  - verify `IgWebApiConnection` values in `App.config`
  - verify network access to `https://{env}api.ig.com`
- Streaming shows no updates:
  - verify login returned `lightstreamerEndpoint`
  - verify CST/XST tokens were captured
  - verify the instrument has `streamingPricesAvailable == true`

## Evidence (repo references)
- `SampleWPFTrader/MainWindow.xaml` (lines 298–300): binds UI status area to `ApplicationDebugData`.
- `SampleWPFTrader/ViewModel/ViewModelBase.cs` (lines 74–97): `ApplicationDebugData` and `UpdateDebugMessage` implementation.
- `SampleWPFTrader/Common/SmartDispatcher.cs` (lines 27–30): REST/event messages forwarded to `viewModel.AddStatusMessage`.
- `IGWebApiClient/IGRestService.cs` (lines 192–197): REST request status logging.
- `IGWebApiClient/IGRestService.cs` (lines 100–121, 154–186): uses `.Result` for HTTP calls.
- `IGWebApiClient/IGStreamingApi.cs` (lines 790–835): streaming connection listener callbacks are empty.

## Open questions / assumptions (if any)
- Assumption: Minimal diagnostics are acceptable for a sample, but a reimplementation should add structured logging and reconnection logic.
  - Missing evidence: No logging framework usage or operational requirements.
  - Risk/impact: Troubleshooting production issues would be difficult without added telemetry.