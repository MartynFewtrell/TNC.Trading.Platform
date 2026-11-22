---
title: IG Account Preferences API and UI Research
description: Research into IG account preferences REST contracts and the current platform integration surface.
author: GitHub Copilot
ms.date: 2026-08-17
ms.topic: reference
keywords:
  - IG Labs
  - account preferences
  - REST API
  - Blazor
---

a compatible integration design.
## Status

Complete. Research was limited to the requested IG REST account-preferences
surface and the repository's closest account and configuration flows. No product
or test files were modified.

## Scope

* Official IG Labs contracts for `GET /accounts/preferences` and
  `PUT /accounts/preferences`
* IG authentication, API versioning, headers, errors, rate/allowance concerns,
  semantics, and caveats relevant to those operations
* The local Account Details UI-to-IG call chain, editable settings pattern,
  boundary conventions, tests, and scoped instructions
* A recommended integration design with alternatives, validation, error handling,
  and identified gaps

## Executive Findings

* IG documents both operations at API version `1`. The only documented preference
  is `trailingStopsEnabled`; `dealingEnabled` is not part of this endpoint and
  must not be modelled or displayed as one.
* `GET /accounts/preferences` returns an object with
  `trailingStopsEnabled: Boolean`. `PUT /accounts/preferences` accepts
  `trailingStopsEnabled: Boolean` and returns an outcome object whose currently
  documented `status` value is `SUCCESS`.
* The repository already has a coherent Account Details vertical slice, but it is
  a persisted history/read model. Account preferences are a live, mutable provider
  setting, so they should be a separate feature and page rather than a snapshot
  field in the history model.
* The existing IG account adapter already proves the needed v2-session flow:
  obtain `CST` and `X-SECURITY-TOKEN`, then send those values with
  `X-IG-API-KEY` and `Version: 1` on the provider request.
* A Boolean-only update is semantically idempotent, but failures must still be
  surfaced carefully: the caller cannot tell whether a timed-out provider request
  reached IG. On an ambiguous write failure, reload the authoritative preference
  before showing a retry action.

## Official IG REST Contract

The requested companion page indexes the REST reference, where the endpoint is
published as `reference/accounts-preferences.html`.

| Operation | Version | Request | Successful response | Semantics |
| --- | --- | --- | --- | --- |
| `GET /accounts/preferences` | `1` | No body | `{ "trailingStopsEnabled": true }` | Returns account-related preferences |
| `PUT /accounts/preferences` | `1` | `{ "trailingStopsEnabled": true }` | `{ "status": "SUCCESS" }` | Edits the account preference; `SUCCESS` is the only currently documented status value, but IG reserves the right to add values |

`trailingStopsEnabled` means the user wants to be allowed to define trailing-stop
rules for trade operations. It is an account-level permission/preference, not an
indication that a specific position currently has a trailing stop, and the
endpoint does not amend existing deals.

### Authentication, Headers, and Versioning

For the repository's current session-v2 approach, the provider request should
include all of the following:

* `X-IG-API-KEY: <API key>`
* `CST: <client session token>`
* `X-SECURITY-TOKEN: <account session token>`
* `Version: 1`
* `Accept: application/json; charset=UTF-8`
* `Content-Type: application/json` for `PUT`

IG's general guide says a version defaults to `1` when absent, but the endpoint
reference explicitly specifies `1`; send it explicitly to prevent silent contract
drift. IG session v1/v2 returns `CST` and `X-SECURITY-TOKEN`; the guide says they
are initially valid for six hours and can be extended while in use up to a
72-hour maximum. A provider `401` can therefore reflect an expired/invalid
session, not necessarily a bad stored API key.

The guide also describes session-v3 OAuth as an alternative: `Authorization:
Bearer <token>` plus `IG-ACCOUNT-ID`. This is not a reason to mix modes in the
proposed implementation. Reuse the repository's v2-token convention unless the
application deliberately migrates all IG calls to OAuth.

### Errors, Allowances, and Retries

IG's guide defines error responses as HTTP `4xx`/`5xx` with a JSON
`{ "errorCode": "..." }` body. Relevant documented examples include missing or
invalid tokens (`401`), invalid/disabled/revoked API keys and endpoint access
(`403`), and account/API-key allowance errors (`403`, for example
`error.public-api.exceeded-account-allowance` and
`error.public-api.exceeded-api-key-allowance`). The reference page does not
publish a numeric allowance for account-preference calls or a `Retry-After`
contract.

Consequences for the integration:

* Do not assume IG signals every allowance exhaustion as HTTP `429`; classify the
  documented `403` allowance error codes separately from other `403` outcomes.
* Do not forward IG response bodies, session tokens, API keys, or arbitrary
  `errorCode` values to the browser. Keep a bounded, allow-listed diagnostic for
  server logs/audit events and a generic user message.
* Retry an expired session at most once by creating a fresh session and replaying
  the request. Do not add blind repeated retries, delays, or client-side polling.
* A preference `PUT` expresses desired final state and is idempotent for the same
  Boolean. Nevertheless, after timeout, connection failure, or response parsing
  failure, use a `GET` to reconcile before claiming the update failed or inviting
  a duplicate submission.
* Map genuine allowance failures to the platform's stable rate-limited outcome
  and local HTTP `429`; use a clear generic message because IG gives no reliable
  wait duration. Map malformed provider JSON to `502`, provider timeouts to
  `504`, and unreachable/provider-authentication/entitlement failures to a
  non-secret `503`-class outcome, consistent with the existing account mapping.

## Local Repository Findings

### Existing Account Details UI and Call Chain

The present Account Details route is `/account-details`, protected by the Viewer
policy. It loads saved account snapshots, exposes a refresh action only after
checking Operator scope, groups accounts by type with Radzen tabs, and retains
the selected tab across refresh/history navigation.

* UI route, authorization, snapshot rendering, and refresh button:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:1-77`
* Scope checks and initial presenter load:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:82-111`
* Presenter state and narrowly handled load/refresh errors:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs:5-64`
* Web models mirror the saved retrieval, not a live setting:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsViewModels.cs:3-26`
* `PlatformApiClient` attaches the application bearer token and uses Viewer for
  the read request and Operator for the refresh request:
  `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:118-153`
* API maps `GET /api/platform/account-details` to Viewer and
  `POST /api/platform/account-details/refresh` to Operator:
  `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:49-52`
* Application refresh coordinates a per-environment lease, invokes an
  application-owned gateway, and persists a snapshot:
  `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:9-87`
* The application-owned port returns either successful inward data or a typed
  failure category:
  `src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsGateway.cs:3-14`

### IG REST Boundary Conventions

`IgAccountDetailsGateway` gets protected Demo credentials, creates a v2 session,
uses the required request headers, retries exactly once after `401`, rejects
malformed data, and keeps credentials/tokens outside application result types.

* Session creation and token extraction:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:17-50`
* Authenticated provider request headers and one-time unauthorized replay:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:53-80`
* The existing broker-auth adapter already extracts bounded `errorCode` and
  `X-REQUEST-ID` diagnostics and maps `429` distinctly:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:178-216`
* The infrastructure composition root binds the account gateway to the Demo IG
  base address:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:91-97`

Important gap: `IgAccountDetailsGateway` currently hard-codes
`BrokerEnvironmentKind.Demo`, even though its application feature retrieves the
configured broker environment. A new preferences feature must use the configured
environment consistently and must not broaden live support implicitly. The
existing gateway's contract should be corrected separately or a shared session
component should take the selected environment explicitly.

### Existing Editable Settings Pattern

The `/configuration` page is the closest local operator-write design: it is
Operator-authorized, uses `EditForm` with a disabled save state, maps a form model
through a presenter and `PlatformApiClient`, then displays an accessible status
message. Its credential inputs also demonstrate the repository rule not to render
stored secrets.

* Form, save state, and user-visible outcome:
  `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:32-187`
* Presenter maps the form to a client request and returns success/failure state:
  `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationPagePresenter.cs:20-47`
* Existing web client uses an Operator bearer token for the configuration `PUT`:
  `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:39-56`
* API configuration read/write operations show the current authorization split:
  `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:39-42`

### Relevant Tests

* UI tests cover empty history, account-type tabs, selected-tab retention, and
  retaining a saved retrieval while a refresh error is shown:
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:12-143`
* IG boundary tests assert v2 token headers, a single unauthorized replay,
  malformed-payload rejection, and non-disclosure of provider bodies:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs:12-76`
* API mapping tests establish the local provider-failure status conventions,
  including `429`, `502`, `503`, and `504`:
  `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountDetailsEndpointMappingTests.cs:35-96`
* Configuration UI/client tests already exercise form save errors and validation
  problem behavior:
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:80-132`
  and
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:98-255`

## Applicable Scoped Instructions

* `.github/instructions/architecture.instructions.md:49-116` requires feature
  folders, one request/response/handler per operation, an application-owned
  outbound port, thin endpoints, boundary translation, and direct handler and
  validator tests.
* `.github/instructions/csharp.instructions.md:23-65` requires one top-level
  type per matching file, null-safe boundary validation, intentional typed error
  handling, and standardized HTTP errors.
* `.github/instructions/configuration.instructions.md:19-45` requires
  operator-facing configuration updates to use the Blazor UI and retains the
  existing protected-credential/write-only secret discipline.
* `.github/instructions/radzen-blazor.instructions.md:28-69` prefers Radzen
  controls, `EditForm`-based validation, accessible labels/feedback, and tests
  for changed interactive behavior.
* `.github/instructions/tests.instructions.md:20-108` requires xUnit, unit-first
  coverage, descriptive `MethodName_Should..._When...` names, requirement-aware
  test comments, and deterministic isolated tests.

## Recommended Integration Design

### Product Shape

Create a separate live `/account-preferences` page rather than adding mutable
controls to account-snapshot history. Viewer access can read the current IG
preference; Operator access can edit it. This preserves a clear distinction:
`/account-details` explains what was captured at a point in time, while
`/account-preferences` is an immediately effective broker setting.

Use one preference toggle, labelled "Allow trailing stops", with current state,
load state, disabled save state, an accessible success/error status, and an
explicit reload after uncertain writes. A Viewer should see a read-only value;
an Operator should see the edit control. Keep the current Account Details page
unchanged except, if requested, a navigation link to the preferences page.

### Application and Provider Design

Use two operations in a new
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/` feature
folder:

* `GetAccountPreferencesRequest`, `GetAccountPreferencesResponse`, and
  `GetAccountPreferencesHandler`
* `UpdateAccountPreferencesRequest`, `UpdateAccountPreferencesResponse`,
  `UpdateAccountPreferencesValidator`, and `UpdateAccountPreferencesHandler`
* `IAccountPreferencesGateway`, an inward-owned port with simple records such
  as `AccountPreferences(bool TrailingStopsEnabled)` and typed success/failure
  results

Implement `IgAccountPreferencesGateway` in Infrastructure. It should reuse a
small, focused IG session helper only if the helper removes duplication across
the existing account-details and new preferences adapters; otherwise keep the
adapter self-contained. The gateway must:

1. Resolve protected credentials for the selected supported environment.
2. Create an IG v2 session and retain tokens only in local adapter variables.
3. Send `GET` or `PUT` to `accounts/preferences` with `Version: 1`, the session
   headers, API key, JSON accept header, and JSON content for `PUT`.
4. Strictly parse `trailingStopsEnabled` for `GET` and `status == "SUCCESS"` for
   `PUT`; classify missing/unknown success payload as malformed provider data.
5. On `401`, create one new session and replay once. For a write with an
   ambiguous result, let the handler/UI reconcile with a subsequent `GET`.
6. Normalize status and allow-listed IG error codes into stable application
   failure categories without returning raw provider content.

Register the two handlers in
`src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:35-43`
and register the new typed HTTP client in
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:91-97`.

### HTTP and DTO Design

Add thin platform endpoints:

* `GET /api/platform/account-preferences`, requiring Viewer
* `PUT /api/platform/account-preferences`, requiring Operator

Keep platform-facing DTOs separate from IG raw models:

```json
// GET 200 and PUT 200
{ "trailingStopsEnabled": true }

// PUT request
{ "trailingStopsEnabled": true }
```

The inbound `PUT` DTO should use `bool? TrailingStopsEnabled`, then validate it
is present before mapping to a non-nullable application command. This prevents a
missing JSON property from being silently interpreted as `false`. Do not expose
IG's `status` as product state; the platform should return the resulting current
preference after it has verified success, so the web UI has one stable model.

Return RFC 9457 Problem Details for validation and upstream failures. Suggested
mapping is `400` for omitted/malformed application input, `429` for classified IG
allowance exhaustion, `502` for malformed provider success/error payloads,
`503` for provider rejection, entitlement, unreachable service, or unavailable
session recovery, and `504` for timeouts. Preserve the application user's
authorization semantics: an upstream IG `403` is not automatically an API-user
`403`.

### Test Plan

* Application unit tests: Get success/failure translation; Update validation;
  selected environment; successful update; allowance; malformed provider result;
  timeout; and session-recovery outcome handling.
* Infrastructure unit tests using an `HttpMessageHandler`: request method/path,
  `Version: 1`, required headers, JSON body, strict GET/PUT parsing, a single
  `401` retry, `403` allowance-code classification, no token/provider-body
  disclosure, and no blind retry after an indeterminate write.
* API unit tests: Viewer/Operator endpoint registration or focused endpoint
  handler tests, DTO mapping, `400` validation Problem Details, and typed status
  mappings.
* Web unit tests: Viewer read-only render; Operator toggle/save; disabled save;
  API error message; successful state replacement; and a post-write reconciliation
  message when completion is uncertain.
* Add an authenticated Aspire/functional test only if acceptance criteria require
  end-to-end authorization and browser interaction. It should not contact real
  IG; use a controllable local provider boundary.

## Alternatives and Tradeoffs

| Alternative | Benefit | Cost and recommendation |
| --- | --- | --- |
| Add a preferences panel to `/account-details` | One page for account-oriented information | Blends a live write model into snapshot history and complicates Viewer/Operator state. Do not use as the primary design. |
| Store preferences as SQL configuration | Could make the UI fast and survive IG outage | Creates a potentially stale desired-state shadow and requires reconciliation/auditing rules. Use only if offline operator workflows are an explicit requirement. |
| Use IG OAuth session v3 | Modern bearer-token model | Requires account-id selection and a broader authentication migration. Do not introduce it for one endpoint. |
| Extract a shared IG v2 session client now | Reduces duplicate session/header/error code | Sensible only when it is a small tested helper used by at least the two concrete adapters. Avoid a generic provider abstraction. |
| Permit a best-effort automatic write retry | Might hide transient network failures | A timeout leaves completion uncertain. Prefer one session-refresh retry for `401` and reconcile all other ambiguous writes with `GET`. |

## Gaps and Clarifying Questions

* IG's endpoint reference does not state whether account preferences are available
  for every account type, regional entity, or Demo account. Confirm with the
  configured Demo account before committing a production UX.
* IG does not publish a per-operation numeric allowance or retry delay on the
  endpoint page. Capture observed rate headers/error codes in a non-production
  environment before choosing backoff or operator guidance.
* The current repository IG account-details gateway hard-codes Demo. Decide
  whether preferences must support only Demo initially or whether the existing
  environment-selection gap should be addressed as a prerequisite.
* The preferred page location/navigation item and whether Viewer users should
  see the setting or only Operators can be decided from product requirements. The
  recommendation above chooses Viewer read and Operator write to match the
  existing account-details/configuration policy split.

## References

* [IG Labs REST Companion](https://labs.ig.com/companion/api-rest-companion-release/index.html)
* [IG Labs REST reference: /accounts/preferences](https://labs.ig.com/reference/accounts-preferences.html)
* [IG Labs REST Trading API Guide](https://labs.ig.com/rest-trading-api-guide.html)
* [IG Labs REST reference: /session](https://labs.ig.com/reference/session.html)
* Local source evidence is cited inline using workspace-relative paths and
  1-based line ranges.
