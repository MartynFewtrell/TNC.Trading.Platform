---
title: Account Preferences Remediation Alternatives Research
description: Phase 2 analysis of contract, endpoint, resilience, and UI alternatives for delayed account preferences loading
ms.date: 2026-08-31
ms.topic: concept
---

## Research scope

Evaluate implementation alternatives that jointly correct the missing SQL
current-state contract and the deterministic HTTP 503 retry delay. Research
only; no product files are changed.

## Questions

1. What external contract should represent a missing SQL current-state row?
2. How should the endpoint use `GetAccountPreferencesResponse.State`, and what
   compatibility role remains for `Outcome`?
3. Which .NET 10 HTTP resilience configuration prevents deterministic domain
   failures from causing multi-second retries while preserving transient
   transport and API-outage recovery?
4. Would parallel or progressive UI loading materially improve the reported
   delay, and should it be part of the remediation?
5. Which status codes and operator messages honestly distinguish domain state,
   invalid requests, authentication, throttling, provider failures, and
   infrastructure outages?
6. What compatibility, migration, and validation work does the selected
   approach require?

## Working hypothesis

The coherent minimum fix is expected to project a missing SQL row as a
successful in-memory `Unconfigured` state, map the response through `State`,
and specialize the Web API client's retry predicate so deterministic API
responses do not retry while transport failures and genuine transient API
outages remain retryable. Repository and authoritative API evidence must
confirm or disconfirm each part.

## Repository evidence

### Contract and persistence path

Production dependency injection supplies `IAccountPreferencesCurrentStateStore`,
so `GetAccountPreferencesHandler` takes its SQL branch. The branch reads the
current row and returns `State`, but it also synthesizes a legacy `Outcome`.
When no row exists, `State` is null and the legacy outcome is
`UnsupportedEnvironment`, even though the safe reason says that preferences
are not configured. Evidence:
src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:14-24.

The response carries both representations:
`GetAccountPreferencesResponse(AccountPreferencesGatewayOutcome Outcome,
AccountPreferencesCurrentState? State = null)`. This dual shape records an
unfinished migration from direct provider reads to the SQL projection.
Evidence:
src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs:1-3.

`GetAccountPreferencesEndpointHandler` ignores `State` and maps only `Outcome`.
The mapping converts the synthesized `UnsupportedEnvironment` failure to the
default HTTP 503 and title `Account preferences provider is unavailable.`
Evidence:
src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:5-12 and
src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:21-53.

The SQL migration creates the current-state and audit tables but inserts no
bootstrap row. The store intentionally returns null for an absent row and
creates the current-state entity on the first desired-state commit. No-row is
therefore a normal pre-intent state, not corrupt persistence. Evidence:
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs:13-91 and
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:9-26.

`AccountPreferencesCurrentState` is a persistence-oriented model. It requires
a row identity, platform and broker keys, retry state, and a row-version byte
array in addition to operator-facing fields. Constructing a synthetic instance
with `Guid.Empty` and an empty concurrency token would work mechanically but
would present invented persistence identity as domain state. Evidence:
src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs:5-25.

Repository documentation already defines the external contract. The GET is a
SQL-only projection, `Unconfigured` is a valid status, and desired state must
remain available during IG failure. Evidence:
docs/wiki/api-reference.md:550-566,
docs/wiki/runtime-behavior.md:727-744, and
docs/wiki/operator-guide.md:566-585.

### Resilience and UI path

`AddServiceDefaults` applies `AddStandardResilienceHandler` to every registered
HTTP client. `PlatformApiClient` is then registered without an override, so its
API calls inherit retries for HTTP 408, 429, and 500 or greater, plus transport
and Polly attempt-timeout exceptions. Evidence:
src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:39-49,
src/TNC.Trading.Platform.Web/Program.cs:6-20, and
src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:11-16.

The current-state and observation-history calls use the same typed client.
`AccountPreferencesPagePresenter.LoadAsync` awaits current state first, starts
history only after current state succeeds, and keeps `IsLoading` true until
both operations finish. A retried current-state 503 therefore prevents history
from starting and holds the whole page behind the loading state. Evidence:
src/TNC.Trading.Platform.Web/PlatformApiClient.cs:140-219 and
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:20-51.

The page view model already defaults to `VerificationStatus = "Unconfigured"`
and supports nullable desired and observed values. The HTTP DTO can therefore
represent no-row without a schema change. Evidence:
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-42 and
src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:56-70.

### Test coverage

The closed-box functional test states the intended invariant: GET remains
successful while the provider is unavailable. It does not establish that the
SQL partition is empty before the read, and its provider-request assertion is
made only after update and explicit retry. Persistent Aspire SQL can therefore
mask the no-row defect. Evidence:
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:12-48.

Existing Web component tests inject a bare `HttpMessageHandler`; they exercise
presenter behavior but not the production resilience pipeline. There is no
registration-level test proving request attempt count or unsafe-method retry
suppression. Evidence:
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:74-150 and
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:1-299.

## Authoritative external evidence

The installed `Microsoft.Extensions.Http.Resilience` 10.1.0 package targets
`net10.0` and exposes the APIs required by the selected approach:

* `RemoveAllResilienceHandlers(IHttpClientBuilder)` removes handlers registered
   earlier, preventing the global and specialized pipelines from stacking
* `AddStandardResilienceHandler(Action<HttpStandardResilienceOptions>)` adds a
   replacement standard pipeline with configurable retry options
* `HttpRetryStrategyOptions.ShouldHandle` is the Polly outcome predicate
* `HttpRetryStrategyOptions.ShouldRetryAfterHeader` defaults to true
* `DisableForUnsafeHttpMethods()` disables retries for POST, PATCH, PUT, DELETE,
   and CONNECT
* Default transient outcomes are HTTP 408, 429, and 500 or greater,
   `HttpRequestException`, and `TimeoutRejectedException`

Evidence: installed package XML at
`%USERPROFILE%/.nuget/packages/microsoft.extensions.http.resilience/10.1.0/lib/net10.0/Microsoft.Extensions.Http.Resilience.xml`, members
`HttpRetryStrategyOptions.#ctor`, `ShouldRetryAfterHeader`,
`DisableForUnsafeHttpMethods`, `RemoveAllResilienceHandlers`, and
`AddStandardResilienceHandler`.

Microsoft's HTTP resilience guidance confirms that the standard handler uses
rate limiting, total timeout, retry, circuit breaker, and attempt timeout; its
default retry count is three and retries use exponential backoff with jitter.
It also warns that retries for unsafe methods can duplicate side effects and
documents method-based disabling. Evidence:
[Build resilient HTTP apps](https://learn.microsoft.com/dotnet/core/resilience/http-resilience#standard-resilience-handler-defaults).

RFC 9110 defines `Retry-After` as the server's estimate of how long a client
should wait before a follow-up request. A 503 response may include it to state
how long service unavailability is expected. Absence of the header therefore
must not be treated as an explicit server invitation to retry a deterministic
503. Evidence:
[RFC 9110 Retry-After](https://www.rfc-editor.org/rfc/rfc9110.html#name-retry-after).

RFC 9457 says `status` is the HTTP status for an occurrence, `title` should be
stable for the problem type, and occurrence-specific explanation belongs in
`detail`. This supports distinct, truthful problem categories rather than a
single provider-unavailable title for unrelated states. Evidence:
[RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457.html).

ASP.NET Core's Problem Details support maps API errors to the RFC shape and
allows status, title, detail, type, and extensions to be populated at the HTTP
adapter boundary. Evidence:
[Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).

## Technical scenario analysis

### Missing current-state alternatives

| Alternative | Benefits | Costs and risks | Decision |
|---|---|---|---|
| Return an in-memory `Unconfigured` query projection | Matches documentation, requires no write, works on a fresh database, and keeps first intent creation atomic | A synthetic `AccountPreferencesCurrentState` would invent persistence identity; use an explicit query projection or result case instead | Select |
| Insert a bootstrap row in migration or startup | Makes the existing nullable store read return a row | Requires invented identity and rowversion, needs one row per environment partition, complicates upgrades and tests, and conflates no operator intent with persisted state | Reject |
| Continue returning failure for no row | Smallest code change | Contradicts documented behavior, reports a false outage, blocks initial setup, and remains retry-prone | Reject |

The preferred application contract is an explicit SQL query result with two
successful cases: configured state and unconfigured state. The unconfigured
case needs only the environment partition and `VerificationStatus.Unconfigured`;
all desired, observed, revision, retry, account, and failure fields remain
null or zero in the API projection. It must not carry row identity or a
concurrency token.

Two implementation shapes are viable:

1. Add a dedicated application read model such as
    `AccountPreferencesQueryState` and make `GetAccountPreferencesResponse.State`
    that type. Supply a static `Unconfigured` factory. This is the clean target.
2. As a migration step, add an explicit `IsUnconfigured` result case and let
    the API construct the existing `AccountPreferencesResponse` with null fields.
    This changes fewer files but should not become the permanent query model.

Constructing `AccountPreferencesCurrentState(Guid.Empty, ..., [])` is not
recommended. It is superficially minimal but exposes a false persisted entity
to future application consumers.

### Endpoint mapping alternatives

| Alternative | Behavior | Decision |
|---|---|---|
| Prefer `State`, then fall back to legacy `Outcome` | SQL-backed reads return the rich projection; legacy callers continue working during migration | Select as transition |
| Map only `State` and delete `Outcome` immediately | Removes ambiguity | Defer until legacy handler tests and direct gateway mode are retired |
| Keep mapping only `Outcome` | Preserves current bug and loses revision, retry, account, and observed fields | Reject |

The transition mapping in
src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs
should follow this order:

1. Return `200 OK` with `result.State.ToResponse()` when configured state is
    present.
2. Return `200 OK` with the explicit unconfigured response when the query
    result identifies no persisted state.
3. Map `result.Outcome` only for the legacy provider-backed path.

The handler must not infer unconfigured state from every null `State`, because
legacy failures also have null state. The application response needs an
explicit discriminator.

Once all production and test composition uses the SQL query contract, remove
the gateway, observation store, event store, time provider, optional state
store, and legacy `Outcome` from the GET handler. Provider interaction belongs
to verification/remediation commands, not this query.

### HTTP status and message alternatives

| Condition | HTTP result | Operator-facing meaning | Retry signal |
|---|---|---|---|
| No persisted preference | 200 with `Unconfigured` | No desired setting has been saved | None |
| Configured SQL state | 200 with actual verification status | Current durable desired and observed state | None |
| Invalid request | 400 Problem Details | Request fields are invalid | None |
| Missing or expired caller authentication | 401 Problem Details | Sign-in is required | None |
| Insufficient caller role | 403 Problem Details | Operator permission is required | None |
| Unsupported environment or state conflict | 409 Problem Details | Operation is not valid in the current platform state | None |
| Stale revision or account mismatch | 409 Problem Details | Refresh before retrying the operation | None |
| Provider session unauthorized | 503 Problem Details | The platform cannot currently authenticate to the provider | `Retry-After` only when session recovery is scheduled |
| Provider allowance exhausted | 429 Problem Details | Provider throttled the operation | `Retry-After` when known |
| Malformed or invalid upstream response | 502 Problem Details | Provider returned an unusable response | Optional automatic retry only for safe reads |
| API or provider temporarily unavailable | 503 Problem Details | A dependency is temporarily unavailable | `Retry-After` when the server invites retry |
| Upstream timeout | 504 Problem Details | A dependency did not respond in time | Automatic retry only for safe reads |

`UnsupportedEnvironment`, `Rejected`, `Unsupported`, and `AccountMismatch`
must no longer fall through to the generic 503 mapping. They are deterministic
conflicts, normally 409 for this API. Caller authentication and authorization
remain 401 and 403 at the ASP.NET Core boundary. The enum's `Unauthorized`
value is different: it represents an IG 401 in
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:130.
It should remain a dependency failure with a provider-session-specific title,
not instruct the operator to sign into the platform again. An automatic retry
is appropriate only when the API supplies `Retry-After` because provider
session recovery is expected.

Problem Details titles should identify stable categories, for example
`Account preferences are not supported in the current environment.` and
`Account preferences provider is temporarily unavailable.` Safe, specific
context belongs in `detail`; secrets and raw provider payloads remain excluded.

### Resilience alternatives

| Alternative | Benefits | Costs and risks | Decision |
|---|---|---|---|
| Fix only the false 503 | Removes the reported retry sequence | Leaves every future deterministic 503 retryable and unsafe writes exposed to duplicate attempts | Insufficient alone |
| Disable all retries for `PlatformApiClient` | Fast and simple | Loses recovery from connection resets, attempt timeout, and genuine API outage | Reject |
| Keep the global defaults | Preserves broad recovery | Retries deterministic 500-class responses and unsafe methods | Reject |
| Replace the typed client's handler with a custom predicate | Preserves selected transient recovery, suppresses deterministic responses, and prevents duplicate writes | Requires a registration-level policy test and careful handler replacement | Select |
| Split account preferences into a new typed client | Gives the narrowest policy ownership | Larger refactor and does not solve other unsafe `PlatformApiClient` operations | Defer unless the client is already being decomposed |

At the `PlatformApiClient` registration in
src/TNC.Trading.Platform.Web/Program.cs:13-16, retain the builder, call
`RemoveAllResilienceHandlers()`, then call
`AddStandardResilienceHandler(options => ...)`. Removal is required because
`AddServiceDefaults` already registered a handler globally; adding another
without removal would stack retries and timeouts.

Configure the replacement retry strategy to:

* Retry `HttpRequestException` and Polly `TimeoutRejectedException`
* Retry HTTP 408, 502, and 504 for safe methods
* Retry HTTP 429 or 503 only when a valid `Retry-After` header is present
* Leave 4xx responses other than explicitly signaled 429 unhandled
* Call `options.Retry.DisableForUnsafeHttpMethods()` so POST, PUT, PATCH,
   DELETE, and CONNECT are never automatically replayed
* Keep `ShouldRetryAfterHeader = true` so an accepted server delay controls the
   retry interval

The predicate should also check the request method, even with method disabling,
so its intent is directly testable. A response-only predicate that retries all
503 responses would recreate the delay under another deterministic 503.

The API side completes this contract by attaching `Retry-After` to 429 and 503
only when retrying later is meaningful. A deterministic provider rejection,
unsupported environment, account mismatch, validation failure, or absent SQL
row must not carry the header.

This policy preserves recovery from transport failures and safe-read timeouts.
It also preserves explicit service-unavailable recovery when the API signals a
retry window, while avoiding guesses about deterministic 503 responses.

### UI loading alternatives

| Alternative | Effect | Decision |
|---|---|---|
| Keep sequential current-state then history load | Maintains current simple ownership, but history latency extends the whole loading state | Accept for root fix |
| Start both requests concurrently and await both | Reduces healthy-load critical path to the slower request | Optional follow-up |
| Render current state when ready and load history progressively | Best perceived latency and independent failure states | Preferred later UX enhancement |

Parallelizing or progressively rendering history cannot correct the false
provider warning. Under the reported failure, current state still retries and
the editable section still has no authoritative state. It is therefore not
part of the minimum remediation.

After the contract and policy fixes, progressive history loading is a sensible
separate improvement because current state and history are independent SQL
queries and the presenter already owns separate `CurrentError` and
`HistoryError` values. It requires separate loading flags and rendering states,
so it should have dedicated component tests rather than being bundled into the
root-cause patch.

## Selected approach

Select one coordinated contract and transport change:

1. Represent an absent SQL row as an explicit successful `Unconfigured` query
    projection. Do not write a bootstrap row and do not synthesize persistence
    identity.
2. Change the GET endpoint to prefer the query `State` result and map the
    explicit unconfigured case to `200 OK`. Retain `Outcome` only as a temporary
    fallback for the legacy provider-backed branch.
3. Correct category-to-status mapping so deterministic domain and authorization
    outcomes use 4xx responses, while 502, 503, and 504 remain dependency or
    infrastructure failures.
4. Replace the globally inherited resilience handler for `PlatformApiClient`.
    Retry safe transport and timeout failures, plus selected safe response
    statuses; require `Retry-After` for 429 and 503; disable retries for unsafe
    methods.
5. Keep current UI sequencing for this remediation. Track progressive history
    rendering separately after healthy-load measurements justify it.

This is the smallest coherent approach because the application owns no-row
semantics, the API owns HTTP representation, and Web composition owns client
resilience. It removes both the false warning and the wait without weakening
the provider gateway's own resilience policy.

## Implementation order

1. Introduce the explicit configured/unconfigured application query result and
    add store-backed handler tests for both cases.
2. Update GET endpoint mapping to prefer state and add HTTP mapping tests for
    configured, unconfigured, legacy success, and each failure category.
3. Correct status/title mapping and add `Retry-After` only to genuinely
    retryable 429/503 responses.
4. Replace the typed client's inherited resilience handler and add deterministic
    attempt-count tests before changing UI behavior.
5. Reset the account-preference SQL partition in the closed-box fixture and
    prove the fresh-database GET returns `Unconfigured` without an IG request.
6. Update affected wiki pages in the implementation change if code shape or
    retry behavior differs from the currently documented contract.
7. Remove the legacy GET `Outcome` branch in a later cleanup after all callers
    and tests use the SQL query result.

## Focused validation

### Application and API

* Add a `GetAccountPreferencesHandler` unit test with a store returning null.
   Assert an explicit unconfigured success, null desired/observed data, and zero
   gateway calls.
* Add a configured-store test asserting the complete state survives through
   `GetAccountPreferencesResponse.State`.
* Add endpoint tests asserting configured and unconfigured results are 200 and
   contain `verificationStatus` plus legacy aliases.
* Parameterize all failure categories and assert status, stable title, safe
   detail, and `Retry-After` presence or absence.

### Resilience

* Resolve `PlatformApiClient` from a service collection using production-style
   `ConfigureHttpClientDefaults`, a counting primary handler, and zero test
   delays. Assert one attempt for deterministic 409 and unsignaled 503.
* Assert safe GET retries for `HttpRequestException`,
   `TimeoutRejectedException`, 408, 502, and 504.
* Assert 429 and 503 retry only with valid `Retry-After`.
* Assert PUT and POST make exactly one attempt for every exception and response
   sequence. Attempt count is the primary regression assertion; avoid broad
   wall-clock thresholds.
* Inspect the handler chain or behavior to prove only one resilience pipeline
   is active after `RemoveAllResilienceHandlers`.

### Integration and closed-box behavior

* Add an infrastructure integration test proving `GetAsync` returns null on an
   empty migrated database and first commit creates one row with revision one.
* Reset or uniquely partition account-preference tables before the functional
   test. Assert the initial GET is 200 `Unconfigured` while the provider is
   unavailable and the provider request list is still empty immediately after
   that GET.
* Preserve the existing update, recovery, and verification assertions to prove
   genuine provider recovery still works.
* Add one browser assertion that a fresh account-preferences page reaches the
   unconfigured state without the provider-unavailable alert. Do not use the
   browser test to prove retry timing.

## Clarifying questions

No blocking questions remain for selecting the approach.

Before implementation, the team should confirm whether the legacy
provider-backed GET mode has any supported runtime composition outside tests.
If not, it can be removed in the same change instead of retained as fallback.

## Recommended next research

The original alternatives questions are answered. No additional research is
required before implementation.

Optional follow-on work:

* Measure healthy current-state and history durations after the root fix before
   deciding whether progressive history rendering is worth its state complexity
* Inventory all methods on `PlatformApiClient` before finalizing one shared
   response predicate; split clients only if distinct service-level policies are
   materially different
* Confirm the API's source of a reliable `Retry-After` duration for temporary
   SQL/API unavailability and provider allowance exhaustion