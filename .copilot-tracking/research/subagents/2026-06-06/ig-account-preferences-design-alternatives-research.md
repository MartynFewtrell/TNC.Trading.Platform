---
title: IG Account Preferences Design Alternatives Research
description: Independent evaluation of account preference integration alternatives for IG trailing stops
author: GitHub Copilot Researcher Subagent
ms.date: 2026-06-06
ms.topic: concept
keywords:
  - IG
  - account preferences
  - trailing stops
  - architecture
---

## Research Scope

Evaluate how the operator UI should expose the IG trailing-stops preference. The alternatives
are an editable account-details section, a separate live account-preferences page, locally
persisted desired state reconciled with IG, and direct feature-specific IG operations. This is
an independent design evaluation, not a delivery plan or implementation.

## Executive Finding

Use a separate, Operator-only `/account-preferences` page backed by direct live,
feature-specific read and write operations. This combines alternatives 2 and 4. It preserves
the existing meaning of account details as historical snapshots, establishes no new source of
truth, avoids a database migration, and matches the repository's vertical-slice conventions.

Do not add the setting to `/account-details`, and do not persist desired state until a product
requirement explicitly demands eventual enforcement, scheduled drift repair, offline writes, or
an audit of every requested state transition.

## Evidence

### Existing Page Ownership and Authorization

* `/account-details` is a Viewer-authorized page whose heading describes "Saved account
  snapshots from the configured IG environment." It renders retrieval timestamp, trading day,
  trigger, historical pagination, and account records. The refresh button is the only
  Operator-gated action. `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:1-69`
* The account-details GET is deliberately provider-free. Its unit test states that Viewer reads
  must not establish or use a provider session. `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:25-50`
* The current API applies Viewer policy to `GET /account-details` and Operator policy to
  `POST /account-details/refresh`. `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:32-42`
* Configuration is the existing operator-owned mutation surface. Its page is Operator-authorized
  and its form edits platform configuration, protected credentials, schedule, retry policy, and
  notifications. `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:1-181`
* The sidebar exposes account details only to Viewers and configuration only to Operators.
  `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:18-36`
* The role model deliberately lets the Viewer policy include Viewer and Operator roles, while the
  Operator policy is distinct. The corresponding delegated scopes are `platform.viewer` and
  `platform.operator`. `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:62-120`

### Existing Account-Details Architecture

* Account-details refresh coordinates a gateway, snapshot store, distributed lease, trading-day
  de-duplication, and in-process coalescing. It then persists a snapshot. This is appropriate for
  an auditable historical capture, not a one-bit broker preference. `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:6-100`
* The EF store writes retrieval and account rows transactionally and queries history by broker
  environment and composite cursor. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:8-70`
* The data model has a unique automatic capture index per environment and trading day. Reusing it
  for a preference would make a current mutable value look like history and would require an
  unrelated schema expansion. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:102-133`
* The endpoint maps typed refresh failures to 429, 502, 503, 504, and 409 outcomes. This is the
  nearest precedent for provider-error translation. `src/TNC.Trading.Platform.Api/Features/Platform/AccountDetailsEndpointMapping.cs:11-44`
* Existing unit tests already protect no-provider reads, duplicate-click coalescing, lease
  contention, durable-success de-duplication, and redaction. `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:25-148`

### IG Session and Provider Boundary

* The account-details IG adapter creates a session with the API key, CST, and
  `X-SECURITY-TOKEN`, then sends those headers to IG. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:38-73`
* It creates one replacement session and replays the accounts read once after a 401. Timeout,
  network, and JSON failures are typed; incomplete account objects and duplicate account IDs fail
  closed. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:17-35`
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:74-111`
* Its adapter tests verify session headers, exactly one replay after unauthorized, provider error
  redaction, and rejection of missing properties. `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs:10-69`
* The application boundary is a narrow, feature-owned gateway port with typed success and failure
  results. `src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsGateway.cs:1-12`
  This supports adding a separate `AccountPreferences` feature port rather than extending the
  account-details port with unrelated methods.

### Environment Constraints

* Both registered IG clients point to `https://demo-api.ig.com/gateway/deal/`.
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:83-102`
* The account-details adapter directly requests Demo credentials even when configuration could
  describe another broker environment. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:17-23`
* The established broker-authentication adapter rejects Live outright. Its tests and behavior
  should not be mistaken for Live broker support. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:19-29`
* Local authentication is Keycloak in Aspire and Azure authentication is Microsoft Entra ID. This
  controls platform user authorization, not IG broker access. `.github/instructions/auth.instructions.md:20-57`

### Official IG Documentation

* IG documents the account preference resource, including the trailing-stops preference, at
  [Accounts preferences](https://labs.ig.com/rest-trading-api-reference/accounts-preferences).
  The intended provider contract is a live `GET /accounts/preferences` and a live
  `PUT /accounts/preferences`, using the current authenticated IG account and a
  `trailingStopsEnabled` boolean.
* IG documents session creation and the CST and security-token session model at
  [Session](https://labs.ig.com/rest-trading-api-reference/session).
* IG publishes REST API operational guidance at [REST API trading FAQ](https://labs.ig.com/faq/rest-api-trading).

The automated fetcher returned HTTP 500 for these Labs pages and later an embedding-service 503
on 2026-08-17. The URLs are canonical references, but the exact version, account eligibility,
allowance behavior, and PUT response body must be manually rechecked against the authenticated
IG Labs documentation before implementation. Do not treat a missing response property as false.

## Alternatives Evaluation

### Option 1: Editable Section on `/account-details`

This option gives a familiar location but is a poor ownership match. The page is explicitly a
snapshot history owned by Viewer reads, whereas a preference read must contact IG and a write
changes broker state. Giving the section live semantics creates a mixed page where a Viewer can
open an otherwise local history page yet cannot obtain its new live section. Hiding the section
for Viewers also leaves an Operator-only control inside a viewer-oriented resource.

The page already asks for a second Operator scope solely to show Refresh. Adding preference edit
state, optimistic/concurrent refresh states, and a separate error surface increases the mixed
responsibility. It would also tempt reuse of the snapshot gateway, lease, store, and history DTO,
which are inappropriate for a mutable account-level setting.

Assessment: reject. It is a small routing change but has poor user semantics, authorization
clarity, and architecture fit.

### Option 2: Separate Live `/account-preferences` Page

This option gives the current broker setting its own page and navigation item. It aligns the UI
with the platform's current split: account details and status are viewer surfaces, configuration
is operator-only. Place the new navigation item in the existing Operator block and guard both the
route and requests with the Operator policy and `platform.operator` scope.

The page should clearly show live-state UX rather than snapshot history:

* Initial loading state while the Operator-scoped GET obtains the current IG preference
* Ready state showing a labeled boolean control and the effective broker environment
* Save-in-progress state that disables the control and duplicate submissions
* Successful-save state confirmed from the authoritative returned value, preferably followed by
  a re-read when IG's PUT response does not contain a complete representation
* Explicit, retryable unavailable, timeout, rate-limited, rejected-session, and malformed-provider
  states without leaked tokens, raw JSON, credentials, or IG diagnostics
* No stale value presented as current after a failed initial read; after a failed save, preserve
  the user's edited value and show that the broker outcome is unknown or failed

Assessment: choose as the presentation alternative. On its own it does not decide source of
truth, so it must be paired with either direct operations or desired-state reconciliation.

### Option 3: Persist Desired State and Reconcile with IG

This introduces a platform-owned desired preference, durable write status, reconciliation worker,
lease/concurrency policy, retry schedule, failure events, and an explicit conflict rule for an
operator or an external IG user changing the setting. It has a valid use when the business
requires eventual enforcement, an auditable command log, resilience during IG downtime, or
automatic repair after out-of-band edits.

It is not warranted by a request to edit a single preference. The current configuration persistence
is intentionally platform configuration, not a mirror of mutable IG settings. Adding desired state
would require a new entity and EF migration, a DB uniqueness/concurrency strategy keyed by broker
environment and likely account identity, a reconciliation lease, retry and notification policy,
and documentation of whether local desired state or live IG state wins. It also changes a Save
button from "setting changed at IG" to "change accepted for later delivery," a more demanding UX
and support contract.

Assessment: defer. This is the highest migration and operational complexity, with no evidenced
product requirement to justify a second source of truth.

### Option 4: Direct Live IG Reads and Writes Through Feature-Specific Operations

This creates separate query and command operations in an `AccountPreferences` vertical slice.
The query obtains the current IG value only for an Operator. The command validates the requested
boolean, establishes an IG session, applies the preference, and returns the authoritative result.
No local preference row is stored.

This is consistent with the repository's required CQRS-style one-request/one-response handlers,
narrow inward-owned ports, external translation in infrastructure, and thin endpoints.
`.github/instructions/architecture.instructions.md:51-112` It avoids putting HTTP, IG DTOs, CST,
or persistence types into the application handler.

Assessment: choose as the state-management alternative. It has a small implementation footprint
and accurately represents the requested live broker behavior.

## Recommended Design

Implement a separate `/account-preferences` page using direct live IG operations. The following
is the minimum coherent shape.

### Page and Authorization

* Add `/account-preferences` as an Operator-only route and navigation item. A live IG read creates
  an IG session, so it must not be inherited by Viewers merely because the result is a boolean.
* Require `PlatformAuthenticationDefaults.Policies.Operator` at the Blazor route and
  `platform.operator` when `PlatformApiClient` acquires the delegated platform token.
* Preserve the existing account-details page as a read-only snapshot history. Do not add a
  trailing-stops section there.

### Platform Operations and HTTP Surface

* Add `GetAccountPreferencesRequest`, `GetAccountPreferencesResponse`, and
  `GetAccountPreferencesHandler` under
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/`.
* Add `UpdateAccountPreferencesRequest`, `UpdateAccountPreferencesResponse`, a validator, and
  `UpdateAccountPreferencesHandler` in the same feature folder. The update request has one
  required `TrailingStopsEnabled` value.
* Add an inward-owned `IAccountPreferencesGateway`, implemented by an IG infrastructure adapter.
  The adapter, not the handler, owns `HttpClient`, `HttpRequestMessage`, IG headers, JSON DTOs,
  and provider response codes.
* Expose `GET /api/platform/account-preferences` and
  `PUT /api/platform/account-preferences`, both requiring Operator authorization. Keep endpoint
  methods limited to binding, validation, handler dispatch, and typed-result mapping, following
  the existing endpoint pattern. `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:24-112`
* Add client methods and a small page presenter. Model the page after the configuration presenter,
  which returns typed load and save results and preserves form data on save failure.
  `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationPagePresenter.cs:5-43`

### DTO and Missing-Property Safety

* Keep raw IG `trailingStopsEnabled` as `bool?` in the infrastructure-only response DTO. A missing
  property, `null`, a wrong JSON kind, or malformed JSON must produce a typed
  `MalformedProviderData` failure, never a default `false` preference.
* Keep the platform update input nullable until its validator proves the property is present. Do
  not bind a missing JSON property directly to a non-nullable `bool`, because JSON deserialization
  can turn absent input into `false` and accidentally disable trailing stops.
* Return a non-nullable boolean only after validation to the application and web view models.
* Treat an empty or unrecognized PUT response as a protocol concern. Either accept only the IG
  documented success status and follow it with a fresh GET, or parse a documented complete
  response. Do not fabricate success from a body that omitted the preference.

### IG Session, Retry, and Error Policy

* Start each operation with a fresh IG session using protected credentials for the configured
  broker environment, then attach `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, and the documented
  endpoint version. Mirror the existing secure header behavior rather than exposing IG tokens to
  the Web or API projects.
* For GET, one 401-triggered session replacement and one replay is a reasonable bounded match to
  the existing accounts adapter. For PUT, permit at most one equivalent replay only after the
  documented idempotency and outcome behavior are confirmed. Never blindly retry an ambiguous
  transport failure after a write; re-read the preference first to determine effective state.
* Map IG allowance/rate limits to 429, malformed provider data to 502, timeout to 504, and
  reachability, rejected sessions, unsupported environments, or other unavailable service states
  to 503. Preserve a typed conflict outcome only if the product later adds optimistic concurrency
  or reconciliation.
* Redact request bodies, API keys, passwords, CST values, security tokens, and raw provider error
  bodies from UI, API responses, logs, and operational events. Existing adapter tests demonstrate
  the intended secret-boundary pattern. `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs:10-69`

### Tests

* Application unit tests: valid query and command dispatch, validator rejection of missing input,
  complete mapping of typed gateway failures, cancellation propagation, and no persistence calls.
* Infrastructure unit tests with a sequenced `HttpMessageHandler`: exact session and preferences
  requests, required headers and documented versions, raw DTO property missing or null, malformed
  JSON, 401 replacement session, 429, timeout, transport failure, and no secret material in typed
  failures.
* API unit tests: both endpoints require Operator authorization, valid results map to documented
  statuses, and every typed failure maps predictably. Existing account-details mapping tests are
  the closest template. `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountDetailsEndpointMappingTests.cs:10-94`
* Web unit tests: Viewer redirect or scope challenge, loading, live value display, successful
  update, disabled duplicate save, and preservation of the pending toggle after an API failure.
  Configuration tests demonstrate the last behavior. `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:80-132`
* Add one closed-box or manual environment test only when Demo IG credentials and a dedicated
  disposable account are available. It must set the prior preference back, run serially, and avoid
  asserting timing or rate-limit behavior. Unit tests remain the primary deterministic coverage.

## Migration and Delivery Complexity

| Alternative | Database migration | Worker or lease | Page and API work | Operational complexity |
| --- | --- | --- | --- | --- |
| Account-details section | None initially, but risks later snapshot model distortion | Existing mechanics are a mismatch | Moderate | Medium |
| Separate live page | None | None | Moderate | Low |
| Desired-state reconciliation | Required | Required | High | High |
| Feature-specific live operations | None | None | Moderate | Low |

The recommendation is deliberately not a local desired-state implementation. It requires no
preference schema migration, no persistence-store registration, no recurring reconciliation, and
no new retention policy. It does require a focused vertical slice across Application,
Infrastructure, API, Web, and their unit-test projects.

## Assumptions and Conflicts

* Assumption: the business intent is an Operator changing the current IG account's live
  trailing-stops preference immediately, not setting a platform policy to be enforced later.
* Assumption: IG's documented account-preferences resource is available to the configured Demo
  account and the provider contract includes `trailingStopsEnabled`. Confirm account and regional
  eligibility before delivery.
* Conflict: the current configuration UI permits a Live broker environment, but the registered IG
  clients target Demo, account-details hard-codes Demo credentials, and broker authentication
  rejects Live. The new feature must either reject Live explicitly or correct platform-wide Live
  broker support first. It must not silently apply a supposedly Live setting to Demo.
* Conflict: a GET on the recommended page is a live broker action, unlike the present Viewer
  account-details GET. This is why the new page must be Operator-only.
* Conflict: the account-preferences resource is current-account scoped in the expected IG contract.
  If product requirements need a selected non-current account, research the IG account-switching
  contract and session consequences before designing the platform API.
* Limitation: IG Labs pages could not be retrieved by the automated research tool. The official
  documentation URLs are recorded, but version numbers, payload shape, status codes, allowances,
  and regional restrictions need a human authenticated-docs confirmation immediately before code
  is written.

## Follow-On Research

* Confirm the current IG Labs endpoint versions, request/response examples, PUT idempotency,
  allowances, regional/account restrictions, and response-body semantics using authenticated
  IG documentation.
* Obtain an explicit product decision on Live broker support. If Live is in scope, resolve the
  current Demo-only base URL, credential selection, and authentication rejection before exposing
  the page.
* Confirm whether an out-of-band change made in IG should remain authoritative. A requirement to
  repair it automatically would change the recommendation to a designed desired-state and
  reconciliation feature.
* Confirm whether preference changes require platform audit events beyond normal API logs. That is
  smaller than desired-state reconciliation but should be a deliberate compliance decision.