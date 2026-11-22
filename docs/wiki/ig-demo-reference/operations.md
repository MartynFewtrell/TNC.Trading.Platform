# Operations and Observability

## Purpose

Describe diagnostics, failure visibility, and operational procedures present in
the client.

## Key points (TL;DR)

* Diagnostics are mostly timestamped strings shown in the WPF view model.
* REST responses log method, full URL, and status code; response error bodies
  are not retained by the generic service.
* There are no health endpoints, metrics, tracing, alert definitions, or
  operator runbooks.
* Some streaming parsing and unsubscribe exceptions are silently swallowed.

## Details

### Logging and status

`PropertyEventDispatcher` abstracts status handling. The WPF `SmartDispatcher`
routes actions to the UI dispatcher and appends messages to
`ApplicationDebugData`. Login, view-model fetches, subscriptions, and caught
exceptions add diagnostic messages.

### Failure semantics

The generic REST call reports the status through `IgResponse<T>` but only reads
the JSON body for status 200. It logs the status and URL, then handles JSON
deserialization errors by marking them handled and separately logging the
exception message. The non-generic request overload only reads the response
content and does not return status data.

Lightstreamer connection callbacks log through `Console.WriteLine`, while
`UnsubscribeTableKey`, typed listener parsers, and the listener-construction path
catch and suppress some exceptions. These behaviors limit diagnosis and should
not be copied for production operations.

### Operating procedures implied by the UI

* If login fails, inspect the displayed status text and verify environment,
  API key, and credentials.
* When a view is left, its view model should unsubscribe using the saved
  `SubscribedTableKey`.
* On exit, the app attempts to unsubscribe account/trade streams and call REST
  logout.

The repository does not define alert thresholds, reconnect policy, retry
strategy, token refresh, health checks, or persistent logs.

## Evidence (repo references)

* `IGWebApiClient/Common/PropertyEventDispatcher.cs`: status-dispatch interface.
* `SampleWPFTrader/Common/SmartDispatcher.cs:25-52`: WPF status and dispatcher.
* `IGWebApiClient/IGRestService.cs:190-222`: response logging and deserialization.
* `IGWebApiClient/IGStreamingApi.cs:774-823`: unsubscribe and connection callbacks.
* `SampleWPFTrader/ViewModel/ApplicationViewModel.cs:199-224`: exit behavior.

## Open questions / assumptions (if any)

* Assumption: Status text is the only operator-visible logging channel in the
  shipped app.
  * Missing evidence: No runtime logs or other logging configuration.
  * Risk/impact: Failures could be invisible outside the UI session.
