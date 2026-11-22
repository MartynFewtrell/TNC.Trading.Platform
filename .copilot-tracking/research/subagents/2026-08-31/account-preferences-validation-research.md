---
title: Account Preferences Validation Research
description: Test and observability strategy for proving the account preferences initial-load fix
ms.date: 2026-08-31
ms.topic: troubleshooting
---

## Research scope

Determine the focused test and observability strategy needed to prove the
account-preferences initial-load fix on a fresh database and prevent retry
latency regressions without relying on flaky timing assertions.

## Questions

1. Which focused unit, integration, functional, and end-to-end tests should
   expose the fresh-database no-row failure and retry latency regression?
2. How can tests assert request attempt count and elapsed behavior without
   flaky wall-clock thresholds?
3. Which test should prove that the initial GET does not call the IG gateway
   and remains available during a provider outage?
4. How should persistent Aspire SQL state be isolated or reset?
5. Which existing telemetry verifies attempts, timing, status mapping, and
   correlation, and what minimal additions are needed?
6. Does the page lifecycle and history sequencing need a performance test?
7. What ordered validation matrix maps to the likely implementation files?

## Working hypothesis

The production-shaped tests can mask the no-row failure because their Aspire
SQL volume is persistent and the fixture does not reset the account-preference
partition. A deterministic handler or transport probe should expose the retry
regression by counting attempts and controlling virtual or injected delay,
rather than by asserting a broad wall-clock threshold.

## Findings

### Key conclusions

1. The cheapest root-cause test is an application unit test that constructs
    `GetAccountPreferencesHandler` with an `IAccountPreferencesCurrentStateStore`
    returning `null`. The existing fixture omits that dependency and therefore
    exercises the obsolete provider branch. Evidence:
    test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:9-34,
    151-176 and
    src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:5-27.
2. The public contract must be proved at the endpoint boundary. A fresh SQL
    partition is a successful `200 OK` projection with nullable desired and
    observed values and `verificationStatus = "Unconfigured"`; it is not a
    provider failure. The endpoint currently ignores `result.State`, while the
    richer state mapper already exists. Evidence:
    src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:7-13,
    src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:7-13,
    16-44, and docs/wiki/api-reference.md:551-568.
3. The retry-latency regression should be represented as an attempt-count
    invariant, not a stopwatch budget. `AddServiceDefaults` puts the standard
    resilience pipeline on every client, and a handled `503` receives three
    retries after the initial attempt. A valid no-row `200` must therefore
    produce exactly one transport attempt. Evidence:
    src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:34-50 and
    src/TNC.Trading.Platform.Web/Program.cs:7-19.
4. The existing closed-box functional test states the right outage contract,
    but retained SQL can make it pass without testing the fresh-database case.
    Its fixture starts the real AppHost without resetting SQL, while the AppHost
    gives SQL both a named data volume and persistent lifetime. Evidence:
    test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:18-50,
    test/Shared/Authentication/RealAppHostProcessFactory.cs:55-123, and
    src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:7-23.
5. The initial GET must be proved independent of IG at two levels: a handler
    fake must report zero gateway calls for missing and populated SQL state, and
    a closed-box test must observe no new
    `GET /gateway/deal/preferences` request while the provider returns `503`.
    The shared WireMock provider already records every request but lacks a reset
    operation. Evidence:
    test/Shared/AccountPreferences/ControllableIgProvider.cs:5-28,38-62 and
    src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:90-121.
6. No dedicated browser performance test is warranted. The page deliberately
    authorizes, loads current state, and then loads history in sequence. A
    deterministic presenter test should prove ordering and failure isolation;
    OpenTelemetry traces should measure deployment latency. Evidence:
    src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:60-82 and
    src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:22-53.

### Focused tests by layer

#### Application unit tests

Change the fixture in
test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:151-188
so it can supply a fake `IAccountPreferencesCurrentStateStore`. Keep the legacy
provider-path tests only if that path remains intentionally supported.

Add these focused cases:

* `GetAccountPreferences_ShouldReturnUnconfiguredProjection_WhenCurrentStateRowIsMissing`
   supplies a store that returns `null`; assert a successful SQL-read contract,
   no failed provider outcome, zero gateway calls, zero observations, and zero
   provider-failure events. The exact internal representation may be either an
   explicit unconfigured application result or a nullable state plus an
   explicit SQL-projection discriminator, but the test must reject the current
   `UnsupportedEnvironment` failure.
* `GetAccountPreferences_ShouldReturnStoredProjectionWithoutGatewayCall_WhenCurrentStateExists`
   supplies a complete `AccountPreferencesCurrentState`; assert identity,
   desired revision, desired and observed values, verification status, retry
   metadata, and zero gateway calls.

These tests expose the defect before HTTP and prove the provider boundary is
not involved. The current fake already tracks `GetCallCount` at
test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:190-202.

#### API unit tests

Add endpoint cases beside
test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs:7-34:

* `HandleAsync_ShouldReturnUnconfiguredOk_WhenCurrentStateRowIsMissing`
   invokes the real application handler with a null-returning current-state
   store and asserts `Ok<AccountPreferencesResponse>`, null account/desired/
   observed fields, and `VerificationStatus == "Unconfigured"`.
* `HandleAsync_ShouldReturnStoredStateOk_WhenCurrentStateExists` proves that
   the endpoint uses `result.State`, including revision and retry metadata,
   rather than rebuilding the legacy provider response.

Retain provider failure mapping tests for operations that actually contact IG,
but add a direct mapping test for the no-row projection so
`UnsupportedEnvironment` cannot accidentally represent fresh SQL state.
Existing broad `503` coverage is at
test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:8-24.

#### Infrastructure integration tests

Keep
`GetAsync_ShouldReturnUnconfigured_WhenDatabaseHasNoDesiredState` at
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:176-188.
It correctly proves that a migrated database has no inferred desired intent.
Do not change the store to fabricate a persisted row merely to make the HTTP
contract easier; the application/API boundary should normalize the absence.

Add an integration test only if the chosen application normalization depends
on real EF behavior. In that case, compose the real store and handler over the
fixture database and assert the same successful unconfigured application
result. The fixture already creates a unique database and provides a complete
schema reset at
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:12-64,
67-94, and 96-115.

#### Web and resilience unit tests

Add a parsing test beside
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:6-64
for the unconfigured JSON payload. Assert one recorded GET, nullable desired
and observed values, and `VerificationStatus == "Unconfigured"`.

Add one in-process transport test that registers the real Service Defaults
pipeline, replaces only the primary transport with a counting
`HttpMessageHandler`, returns the fixed no-row `200` payload, and calls
`PlatformApiClient.GetAccountPreferencesAsync`. Assert exactly one transport
attempt and one successful result. This proves the production client pipeline
without sleeping. The existing direct unit harness at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:74-124
does not call `AddServiceDefaults`, so its `CallCount` cannot prove resilience
behavior. Its reusable counting seam is
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/SequencedHttpMessageHandler.cs:5-35.

If the implementation also adds a client-specific retry predicate, test that
predicate directly with `200`, `503`, `408`, `429`, and
`HttpRequestException` inputs. Do not wait through the standard handler's real
two-, four-, and eight-second nominal delays. Polly recommends testing
application-owned settings and delegates rather than its internal retry
algorithm.

Add or sharpen a presenter test in
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:89-126
using independently gated `TaskCompletionSource` responses:

* Before the current-state gate completes, history call count is zero.
* After a successful current-state completion, history starts exactly once.
* After current-state failure, history call count remains zero.
* A slow history response does not erase the already-applied current state.

This is a sequencing test, not a timing benchmark.

#### Closed-box functional test

Split or strengthen
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:18-50
with a fresh ephemeral SQL fixture:

* `GetAccountPreferences_ShouldReturnUnconfiguredWithoutProviderCall_WhenDatabaseIsFreshAndProviderIsUnavailable`
   authenticates, resets the provider request ledger, sets `Available = false`,
   calls GET, and asserts `200`, null desired/observed values,
   `verificationStatus = "Unconfigured"`, and no new
   `GET /gateway/deal/preferences` request.
* `GetAccountPreferences_ShouldReturnPersistedStateWithoutProviderCall_WhenProviderIsUnavailable`
   creates desired state through the public PUT boundary (or a documented test
   setup boundary), waits for the state commit, resets the provider ledger,
   disables the provider, calls GET, and asserts the persisted SQL fields plus
   zero GET requests to the provider. Keep asynchronous reconciler traffic
   distinguishable from the request under test; do not treat legitimate
   background verification as an initial-GET call.

The first scenario is the mandatory regression test. The second is useful if
the fixture can isolate background reconciliation deterministically.

#### Browser E2E test

Change the existing smoke assertion at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:19-39
to run against fresh ephemeral SQL with the provider unavailable and assert:

* the page leaves the loading state
* `Desired setting: Unconfigured` and `Status: Unconfigured` are visible
* the initial-load error is absent
* the provider-unavailable warning is absent

Do not add a millisecond threshold. This browser test proves rendering and
completion; the functional and transport tests prove status and attempt count.

### Attempt count and elapsed behavior

Use three deterministic signals:

1. Count calls at the owning boundary. Use `FakeGateway.GetCallCount` for
    Application, `SequencedHttpMessageHandler.CallCount` for Web-to-API
    transport, and the WireMock request ledger for IG.
2. Represent elapsed behavior as scheduled or virtual delay, not wall time.
    When a custom retry delegate is introduced, set its test delay generator to
    zero and assert the predicate/attempt count, or inject
    `Microsoft.Extensions.Time.Testing.FakeTimeProvider` and advance it
    explicitly. The repository does not currently reference
    `Microsoft.Extensions.TimeProvider.Testing`, so adding it is justified only
    if production-owned timing requires timer advancement.
3. Keep a generous timeout only as a deadlock guard around task completion.
    Never assert that a CI request completes within a narrow stopwatch budget.
    A gate-based assertion can prove that no second attempt is requested before
    the first successful response without depending on scheduler speed.

The standard handler's documented defaults are three retries, exponential
backoff, jitter, a two-second initial delay, a 30-second total timeout, and a
10-second attempt timeout. Existing runtime evidence confirms attempt numbering
starts at zero and aggregate client timing is logged. Evidence:
artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/trx/martynfewtrell_TNCITWOR001_2026-08-31_12_47_50_net10.0.trx:4630-4660
and 4711-4782.

### Persistent Aspire SQL isolation

Add a test-oriented AppHost setting, such as
`AppHost:UsePersistentSqlState`, defaulting to `true` for local development.
When false, omit both `WithDataVolume()` and
`WithLifetime(ContainerLifetime.Persistent)` from the SQL resource. The test
factory should always pass the false override beside the existing nonpersistent
Keycloak override at
test/Shared/Authentication/RealAppHostProcessFactory.cs:16-27 and 63-74.

This is preferable to deleting a developer's named volume. A session-lifetime
container with `WithDataVolume()` is still unsafe because the named volume
survives container disposal. Aspire explicitly documents passing AppHost test
arguments to disable volumes.

For fixture reuse, choose one of these isolation levels in order:

1. One ephemeral SQL resource per functional/E2E collection.
2. One unique database name per test class on that ephemeral server.
3. A SQL setup step using the Aspire resource connection string to delete only
    the account-preference partition before each test, with the collection kept
    nonparallel and background workers controlled.

Also add `Reset()` or `ClearRequests()` to
test/Shared/AccountPreferences/ControllableIgProvider.cs:17-28 so provider
attempt assertions never include startup or a prior scenario. Never reuse the
integration fixture's destructive table-drop routine against the local
persistent `platformdb`.

### Existing observability and minimal additions

The following telemetry already exists:

* Incoming ASP.NET Core spans and outgoing `HttpClient` spans provide method,
   route/URL, response status, duration, and W3C trace correlation. Runtime and
   HTTP metrics are also enabled. Evidence:
   src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:84-115.
* OpenTelemetry logging includes formatted messages and scopes and exports
   through OTLP when Aspire supplies `OTEL_EXPORTER_OTLP_ENDPOINT`. Evidence:
   src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:91-99,118-128.
* The API request scope records `service.name`, `deployment.environment`, and
   `trace.id`. Evidence: src/TNC.Trading.Platform.Api/Program.cs:47-66.
* `HttpClient` logical/client handler logs show request start, physical send,
   response status and per-send duration, and aggregate completion duration.
   Polly logs `Handled`, zero-based `Attempt`, result, and execution time.
   Evidence:
   artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/trx/martynfewtrell_TNCITWOR001_2026-08-31_12_47_50_net10.0.trx:4630-4660
   and 4711-4782.
* Domain provider failures store a sanitized category, bounded reason, and
   request correlation ID, but the SQL current-state GET intentionally emits
   no provider event. Evidence:
   src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:29-61.

This is sufficient to verify status, total request timing, individual attempt
timing, attempt number, and distributed trace correlation in an OTLP backend.
Automated correctness tests should assert behavior directly rather than parse
logs.

No new domain event is recommended for successful SQL reads. If plain-text
test logs must correlate concurrent Polly attempts and the OTLP trace backend
is unavailable, the minimum addition is an `ILogger` scope around
`PlatformApiClient.GetAccountPreferencesAsync` at
src/TNC.Trading.Platform.Web/PlatformApiClient.cs:140-147 containing a stable
operation name and `Activity.Current.TraceId`. Existing Polly and `HttpClient`
logs then inherit the scope. Do not log payloads, account identifiers, tokens,
or a new event per successful read. A dedicated request-duration metric is
unnecessary unless an explicit service-level objective is adopted.

### Page lifecycle performance assessment

The lifecycle does not need a dedicated load/performance suite for this fix.
The observed multi-second regression comes from a deterministic `503` entering
the standard retry pipeline, not CPU, rendering, query volume, or an algorithm
whose scaling requires benchmarking.

Retain the sequential current-state then history design because it preserves
primary-state error ownership and avoids history work after a failed primary
read. Prove it with gated unit tests. Use an OpenTelemetry trace during one
manual or quality-gate run to confirm the expected span sequence:

1. One Web-to-API current-state attempt returning `200`.
2. One subsequent history request returning `200`.
3. No account-preference IG span caused by the current-state request.

Reconsider a performance test only if the product adopts a numeric page-load
service-level objective or changes current-state/history loading to parallel or
streamed rendering.

## Recommended validation matrix

| Order | Validation | Required assertion | Likely implementation files |
|-------|------------|--------------------|-----------------------------|
| 1 | Application unit, missing row | Valid unconfigured result; gateway, observations, and provider events remain zero | src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:5-27; test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:9-34,151-202 |
| 2 | Application unit, populated row | Exact SQL state returned; gateway remains zero | Same files as order 1 |
| 3 | API endpoint unit | Missing row and populated row both map to `200`; full state and compatibility aliases are correct | src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:7-13; src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:7-44; test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs:7-34 |
| 4 | Web transport/resilience unit | No-row `200` is parsed with exactly one physical handler call and no scheduled retry | src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:34-50; src/TNC.Trading.Platform.Web/Program.cs:15-19; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:6-64 |
| 5 | Existing SQL integration | Fresh migrated database returns no state row and does not infer intent | test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:176-188 |
| 6 | Presenter/bUnit sequencing | Current request precedes history; failed current request suppresses history; unconfigured response renders without error | src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:22-53; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:89-126,173-188 |
| 7 | Closed-box functional, fresh ephemeral SQL | Provider-off GET returns unconfigured `200`; no IG preferences GET; no retained row can mask failure | src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:7-23; test/Shared/Authentication/RealAppHostProcessFactory.cs:55-123; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:18-50 |
| 8 | Browser E2E smoke | Page leaves loading, shows Unconfigured, and shows no provider warning | test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:19-39 |
| 9 | Runtime trace inspection | One current-state attempt, one later history request, correlated `200` spans, and no IG span attributable to initial GET | src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:84-128; src/TNC.Trading.Platform.Api/Program.cs:47-66 |

Run orders 1 through 4 first because they are fast and directly falsify the
root-cause hypothesis. Run 5 and 6 next. Run the Docker-backed functional and
E2E checks only after SQL isolation is fixed; otherwise a green result is not
credible evidence for the fresh-database case.

## Authoritative references

* [Microsoft Learn: Build resilient HTTP apps](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
   documents the standard handler's retryable outcomes, pipeline order,
   defaults, customization, and handler removal.
* [Microsoft Learn: FakeTimeProvider](https://learn.microsoft.com/dotnet/api/microsoft.extensions.time.testing.faketimeprovider)
   defines deterministic clock and timer advancement for tests.
* [Polly: Testing](https://www.pollydocs.org/advanced/testing.html) recommends
   testing application-owned resilience settings and delegates rather than
   Polly internals and provides pipeline descriptors.
* [Polly: Telemetry](https://www.pollydocs.org/advanced/telemetry.html) defines
   `ExecutionAttempt`, `OnRetry`, zero-based `attempt.number`, attempt duration,
   and total pipeline duration logs and metrics.
* [Aspire: Testing overview](https://aspire.dev/testing/overview/) defines
   Aspire tests as closed-box tests and recommends `WebApplicationFactory` for
   isolated in-process cases.
* [Aspire: Manage the AppHost in tests](https://aspire.dev/testing/manage-app-host/)
   documents passing test configuration to omit data volumes.
* [Aspire: Access resources in tests](https://aspire.dev/testing/accessing-resources/)
   documents HTTP clients, resource connection strings, and readiness waits.
* [Aspire: Persist data with volumes](https://aspire.dev/fundamentals/persist-data-volumes/)
   confirms that volumes live outside containers and survive container stop.

## Clarifying questions

* Should the application layer own an explicit `Unconfigured` result, or should
   the API synthesize the public unconfigured projection from an explicit
   SQL-read result? Tests should enforce the public `200` contract either way;
   the current nullable `State` alone does not distinguish every legacy path.
* Does CI require plain-text log evidence, or is the Aspire OTLP trace backend
   retained as a quality-gate artifact? The answer determines whether the
   optional Web logging scope is useful.

## Recommended next research

* [ ] Confirm the intended application-level representation for a missing
   current-state row with the owner of the pending SQL-projection migration.
* [ ] Capture one post-fix Aspire trace to verify the span sequence and whether
   current OTLP exports make the optional Web logging scope unnecessary.
* [ ] Confirm the CI container runtime removes test-created anonymous SQL
   storage after each fixture and add a cleanup assertion if it does not.