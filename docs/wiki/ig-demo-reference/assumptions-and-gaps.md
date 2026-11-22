# Assumptions and Gaps

## Purpose

Separate source-backed facts from conclusions that need additional verification.

## Key points (TL;DR)

* The README calls the sample deprecated.
* Current IG API compatibility is unknown; no current vendor specification is
  checked in.
* The build is blocked in this environment by missing legacy reference packs.
* Streaming reconnect, token refresh, region rerouting, error recovery, and
  production deployment are not described.

## Details

### Verified repository facts

* Root solution includes `SampleWPFTrader` and `IGWebApiClient`.
* WPF app targets .NET Framework 4.6.1; client targets Portable Class Library
  Profile259 and .NET 4.5.
* Login, REST snapshots, and streaming subscriptions are implemented in source.
* No test project, persistence layer, deployment definition, or current IG API
  schema is tracked.
* The README's deprecation notice is dated August 2024.

### Assumptions to validate

* API routes, API versions, JSON shapes, and streaming field names still match
  current IG service contracts.
* A sample API key value in `App.config` is not needed in a new project. It is
  intentionally not reproduced here.
* The presence of funding, settings, and other DTO files does not establish
  end-to-end feature support.
* Travis configuration may be stale because its solution name differs from the
  checked-in root solution.

### Known implementation risks to avoid copying

* REST calls block on `Task.Result`, create a new `HttpClient` per request, and
  only deserialize status-200 responses.
* Error response bodies are discarded by the generic request method.
* Several parsing and cleanup paths suppress exceptions.
* Login stores credentials in config and does not implement all response
  rerouting or silent-login flows described in DTO comments.
* `IGStreamingApiClient.Connect` takes an API key but does not use the parameter
  when filling Lightstreamer connection details.
* UI callbacks do not consistently marshal model updates onto the WPF dispatcher.

### Verification tasks before building a similar application

1. Consult current IG REST and streaming documentation for endpoints, API
   versions, authentication, rate limits, and contract changes.
2. Confirm demo and live environment selection and any account-region routing.
3. Replace legacy project targets with a supported runtime and dependency set.
4. Design secure credential storage, session expiration, reconnect, and
   subscription recovery.
5. Define deployment, packaging, diagnostics, and test coverage; none is
   specified here.

## Evidence (repo references)

* `README.md`: deprecation notice.
* `IGWebApiDotNetSample.sln`, `SampleWPFTrader/SampleWPFTrader.csproj`, and
  `IGWebApiClient/IGWebApiClient.csproj`: solution and target frameworks.
* `IGWebApiClient/IGRestService.cs:80-222`: REST transport implementation.
* `IGWebApiClient/IGStreamingApi.cs:527-823`: connection and stream handling.
* `IGWebApiClient/Model/dto/endpoint/auth/session/v2/AuthenticationResponse.cs`:
  rerouting and Lightstreamer endpoint fields.
* `.travis.yml`: configured solution name.

## Open questions / assumptions (if any)

* The largest unresolved question is whether the documented API behavior remains
  valid against the current IG platform.
  * Missing evidence: Current vendor contract and a safely provisioned demo
    account.
  * Risk/impact: A technically faithful clone of this sample may not authenticate
    or receive updates today.
