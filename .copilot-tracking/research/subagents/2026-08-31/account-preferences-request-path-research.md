---
title: Account Preferences Request Path Research
description: Phase 1 evidence for the delayed account preferences load and provider-unavailable warning
ms.date: 2026-08-31
ms.topic: troubleshooting
---

## Research scope

Investigate the account preferences initial-load request path from the Blazor
page through Web, API, Application, Infrastructure, and provider boundaries.
Explain the observed 5-10 second delay, the exact provider-unavailable warning,
the simultaneous default or unconfigured state, and the empty observed history.

## Questions

1. Which initial-load asynchronous calls execute, in what order, and through
   which boundaries?
2. Which timeout, retry, cancellation, authentication, service-discovery,
   readiness, fallback, and exception mappings can produce the symptom?
3. Where is the exact warning generated, and which outcomes map to it?
4. Why can provider unavailability and an unconfigured state appear together?
5. How are provider dependencies registered and wired by Aspire locally?
6. What behavior do tests, documentation, and history establish?
7. What runtime evidence supports or challenges the code-level hypotheses?

## Request path

The initial Razor lifecycle is sequential:

1. `AccountPreferences.OnInitializedAsync` first calls
   `PlatformNavigationAccessCoordinator.EnsureRequiredScopesAsync` for the
   Operator scope. Only a successful scope check calls `Presenter.LoadAsync`.
   Evidence: src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:60-68.
2. The coordinator reads the current operator context and then validates the
   existing access token. An unauthenticated context, missing scope, missing
   token, or invalid token redirects to sign-in rather than rendering the
   reported warning. Evidence:
   src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:13-39 and
   src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:14-59.
3. `AccountPreferencesPagePresenter.LoadAsync` awaits the current-state call.
   It requests history only when the current-state call did not set
   `CurrentError`; the two calls are not parallel. Evidence:
   src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:22-53.
4. `PlatformApiClient.GetAccountPreferencesAsync` gets the same bearer token,
   sends `GET /api/platform/account-preferences`, maps a non-success Problem
   Details response into `HttpRequestException`, and deserializes a successful
   response. Evidence: src/TNC.Trading.Platform.Web/PlatformApiClient.cs:140-147,
   188-219.
5. The Operator-authorized API endpoint invokes
   `GetAccountPreferencesHandler` and maps only `result.Outcome`; it does not
   use `result.State`. Evidence:
   src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:53-55 and
   src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:7-13.
6. The application handler reads platform configuration from SQL. Because DI
   always supplies `IAccountPreferencesCurrentStateStore`, it then reads the
   account-preference state from SQL. In this branch it returns immediately and
   does not call `IAccountPreferencesGateway`, append an observation, or record
   a provider event. Evidence:
   src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:14-27 and
   src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:90-108.
7. `SqlPlatformConfigurationStore.GetCurrentAsync` ensures and maps the single
   configuration row. `EfAccountPreferencesCurrentStateStore.GetAsync` issues
   an EF Core `SingleOrDefaultAsync` filtered by platform and broker
   environment. Evidence:
   src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:32-36,
   112-150 and
   src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:8-12.

The conditional history path, reached only after a successful current-state
response, calls `GET /api/platform/account-preferences/observations?pageSize=25`.
`PlatformApiClient.GetAccountPreferencesHistoryAsync` sends the authorized
request and uses the same non-success mapping as the current-state request.
`GetTrailingStopsPreferenceObservationsEndpointHandler` validates page size
and cursor, then invokes `GetTrailingStopsPreferenceObservationsHandler`. The
application handler reads current configuration, applies the environment
guard, and calls `EfTrailingStopsPreferenceObservationStore.ListAsync`, which
queries the SQL observation partition with keyset pagination. No IG provider
call occurs on this path. Evidence:
src/TNC.Trading.Platform.Web/PlatformApiClient.cs:179-219,
src/TNC.Trading.Platform.Api/Features/Platform/GetTrailingStopsPreferenceObservationsEndpointHandler.cs:7-27,
src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:5-17, and
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-57.

## Timing and failure behavior

The page-owned cancellation source is canceled only when the component is
disposed; no page-level `CancelAfter` exists. The scope coordinator uses
`CancellationToken.None`, while the presenter passes the component token to
the current-state and conditional history calls. Evidence:
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:61-82 and
src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:13-30.

Both Web and API call `AddServiceDefaults`. That method applies
`AddStandardResilienceHandler` and service discovery to every `HttpClient`.
The Web API client targets the logical URI `https+http://api`. Evidence:
src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:34-50 and
src/TNC.Trading.Platform.Web/Program.cs:7-19.

The installed `Microsoft.Extensions.Http.Resilience` version is 10.1.0. The
standard handler treats HTTP 500 and above, 408, 429, `HttpRequestException`,
and `TimeoutRejectedException` as transient. Its documented defaults include
three retries after the initial attempt, a two-second exponential retry delay
with jitter, a 30-second total timeout, a 10-second attempt timeout, a circuit
breaker, and a rate limiter. A fast deterministic 503 can therefore be sent up
to four times, with nominal waits of two, four, and eight seconds before
jitter. This readily produces the reported 5-10 second wait and can run longer
within the 30-second total bound. Evidence:
src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:11-16 and
[Build resilient HTTP apps: Standard resilience handler defaults](https://learn.microsoft.com/dotnet/core/resilience/http-resilience#standard-resilience-handler-defaults).

The account-preference reconciliation schedule of 5, 30, 120, and 600 seconds
belongs to asynchronous verification after desired-state changes. It is not
awaited by the initial SQL query and does not explain this page delay. Evidence:
src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:19-29 and
src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:12-27.

Aspire readiness gates process startup, not each page request. AppHost waits
for API before starting Web, but the shared readiness endpoint has no SQL or IG
provider check unless another component registers one; service defaults add
only the self liveness check. An API process can therefore be ready while the
account-preference contract returns 503. Evidence:
src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:28-44 and
src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:52-63,173-190.

## Warning mapping and rendered state

When no SQL current-state row exists, the application creates
`Failed(UnsupportedEnvironment, "Account preferences are not configured.")`.
The API category mapping has special cases only for `RateLimited` (429),
`MalformedProviderData` (502), and `Timeout` (504). Every other category,
including `UnsupportedEnvironment`, `Unavailable`, `Unauthorized`,
`Rejected`, `Unsupported`, `AccountMismatch`, and `Transient`, becomes a 503
Problem Details title of `Account preferences provider is unavailable.`
Evidence:
src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:18-24,
src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesFailureCategory.cs:1-16, and
src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:28-52.

The Web client extracts that Problem Details title and throws it as an
`HttpRequestException`. The presenter prepends `Unable to load account
preferences.`, producing the exact screenshot warning. Evidence:
src/TNC.Trading.Platform.Web/PlatformApiClient.cs:199-219 and
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:29-37,
115-116.

The warning and default state are not contradictory and do not prove stale or
fallback data. `AccountPreferencesPageViewModel` starts with null account,
desired, and observed values, `VerificationStatus = "Unconfigured"`, and an
empty history. Because the 503 prevents `State.Apply`, those constructor
defaults remain. The Razor markup translates null desired state to
`Unconfigured`, null account to `Not configured`, null observation to `No
observation`, and iterates the still-empty history. Evidence:
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-21,
23-40 and
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:14-31,
55-58. The presenter also suppresses the history request after the primary
error, so empty history does not establish that SQL contains no observations.

## Dependency injection and Aspire wiring

The API composition root registers the SQL current-state store and a typed
`IAccountPreferencesGateway` backed by `IgAccountPreferencesGateway`. The IG
base URI must be an absolute HTTP(S) URI ending in `/`; invalid configuration
fails API startup. Evidence:
src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:90-121.

The local AppHost gives API a `platformdb` reference and waits for SQL and
Mailpit; Web references API and waits for API. API and Web wait for Keycloak
when local Keycloak authentication is selected. Evidence:
src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:10-44 and
src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:164-193.

The AppHost forwards `Ig:AccountPreferencesBaseUrl` only when its own setting
is non-empty. Otherwise the API uses its appsettings value. This provider URI
cannot explain the reported initial GET in the current SQL-store branch because
that branch never invokes the gateway. Evidence:
src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:105-145 and
src/TNC.Trading.Platform.Api/appsettings.json:24-30.

## Tests, documentation, and history

The focused tests executed during this research all passed:

- API category mapping, including category value 5 to the exact 503 title.
- Web view-model initialization and failed-initial-load rendering.
- The application environment test for Test and Live behavior.

The passing results confirm isolated pieces but do not cover the production
contract. `AccountPreferencesEndpointMappingTests` covers an `Unavailable`
failure, not the no-row `UnsupportedEnvironment` case.
`AccountPreferencesEndpointTests` has no successful or unconfigured current
GET test. `AccountPreferencesHandlerTests` constructs the handler without
`IAccountPreferencesCurrentStateStore`, selecting the obsolete live-provider
branch rather than production DI. Evidence:
test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:8-24,
test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs:1-58, and
test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AccountPreferencesHandlerTests.cs:9-34,160-188.

Infrastructure integration coverage explicitly proves that a migrated or
fresh database with no desired state returns null from the current-state
store. That is the condition the application currently converts into the
failed legacy outcome. Evidence:
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:176-188.

The closed-box functional test states that GET remains 200 during provider
outage, but its fixture does not reset SQL or seed this state in the test. The
AppHost SQL resource uses a persistent data volume, and the real AppHost
process fixture does not reset it. Retained state can therefore mask the
fresh-database failure. The E2E test checks only that desired and observed
labels render; it does not assert GET status, absence of the warning, state
values, or elapsed time. Evidence:
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:18-50,
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:19-39,
test/Shared/Authentication/RealAppHostProcessFactory.cs:55-123, and
src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:7-23.

Repository documentation consistently describes the target contract: the
initial GET is SQL-only, stays available during IG outage, and returns an
`Unconfigured` projection for new or migrated installations. The testing wiki
claims API coverage for that SQL-only GET, but no corresponding endpoint test
was found. Evidence: docs/wiki/api-reference.md:551-568,
docs/wiki/application-overview.md:195-214,
docs/wiki/runtime-behavior.md:728-747,
docs/wiki/operator-guide.md:565-600,
docs/wiki/local-development.md:146-162, and
docs/wiki/testing-and-quality.md:553-588.

Git history shows commit `f4b3b3b4669a8e783e063b881403ee5bcd17dcd5`
on 2026-08-30 introduced the original live-provider account-preference
feature. The SQL current-state branch, response state, and updated wiki
contract are uncommitted working-tree changes. The current diff adds
`AccountPreferencesCurrentState.ToResponse`, but
`GetAccountPreferencesEndpointHandler` still maps only `result.Outcome`.
This provenance is consistent with an incomplete contract migration rather
than a long-standing provider outage.

## Runtime evidence

No platform processes were running during this investigation, so no cheap,
safe authenticated live timing was available without starting the full
distributed application and modifying persistent runtime state. No such run
was performed under the research-only scope.

Existing distributed-test TRX output proves that the standard Polly retry
pipeline is active on both API provider clients and the Web
`PlatformApiClient`. For example, the latest account-preferences quality-gate
artifact records `Source: '-standard//Standard-Retry'` for Web API requests.
It records attempt 0 with successful 200 responses, not the screenshot's 503
sequence, so it proves pipeline activation but not the failing request timing.
Evidence:
artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/trx/martynfewtrell_TNCITWOR001_2026-08-31_12_47_50_net10.0.trx:4639-4769.

The same quality gate records the exact 503 title only as a passing unit-test
case, not as a live HTTP failure. No direct artifact containing the reported
full UI warning plus retry attempts was found. Evidence:
artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/trx/martynfewtrell_TNCITWOR001_2026-08-31_12_47_27_net10.0.trx:40-80.

### Failure-mode matrix

| Failure mode | Expected behavior | Fit to symptom |
| --- | --- | --- |
| Fresh or migrated SQL database with no current-state row | Application returns failed `UnsupportedEnvironment`; API maps it to provider-unavailable 503; Web retries and retains defaults | Exact fit |
| Existing SQL current-state row | Application fabricates successful legacy outcome; endpoint still ignores richer `State`, risking loss of account and revision metadata | Does not produce exact warning |
| Invalid provider base URL | API composition throws during startup | Poor fit |
| API unavailable or service discovery failure | Web throws a connection-oriented `HttpRequestException`; presenter suffix differs from exact API title | Delay can fit, warning does not |
| API authentication or scope failure | Page redirects or API returns 401/403; standard retry does not handle 401/403 | Poor fit |
| SQL command failure or latency | Can delay or cause retried 500 responses, but normally yields generic API failure text rather than the exact provider title | Secondary possibility |
| Actual IG outage | Affects reconciliation, observation, or explicit remediation; production initial GET does not call IG while the SQL store is registered | Strongly inconsistent |

## Root-cause hypotheses

1. **Very high confidence: incomplete SQL-projection contract migration.** A
    fresh/no-row store result is converted to legacy
    `UnsupportedEnvironment`; the endpoint ignores the new `State`; broad API
    mapping labels that outcome as provider unavailable. This explains the
    exact warning despite no initial IG call, and the working-tree diff shows
    the migration stopped at this boundary.
2. **High confidence: global standard HTTP resilience causes the delay.** The
    Web client retries deterministic 503 responses with exponential jittered
    delays. This explains why a fast SQL/API contract failure appears after
    several seconds rather than immediately.
3. **Low confidence: SQL startup or query latency contributes to elapsed
    time.** Persistent SQL and startup ordering can add latency, but the
    observed exact Problem Details title requires the category-mapping path and
    is more specifically explained by hypothesis 1.
4. **Very low confidence: service-discovery or API readiness failure is the
    primary cause.** Such failures can trigger the same retry pipeline, but do
    not naturally produce the exact API-authored provider warning.
5. **Very low confidence: live IG provider outage is the primary cause.** The
    production initial request returns from the registered SQL-store branch
    before `IAccountPreferencesGateway` can execute.

Hypotheses 1 and 2 are complementary, not alternatives. Hypothesis 1 explains
the response and misleading wording; hypothesis 2 explains its latency and
repetition.

## Unresolved questions

- Was the screenshot captured from the exact dirty working tree inspected, or
   from a build with additional unpublished changes?
- Does the affected persistent database lack an
   `AccountPreferencesCurrentStates` row for the active platform and broker
   partition? This is strongly predicted but was not queried directly.
- What exact attempt timestamps and jitter delays occur for the failing GET in
   the user's run? Existing artifacts do not contain that live 503 sequence.
- Does the intended unconfigured representation require a physical SQL row,
   or should a missing row be projected into an in-memory `Unconfigured`
   response? Documentation establishes the external behavior but not this
   internal choice.
- Which pending owner or work package is completing the new `State` response
   contract? Working-tree provenance cannot establish ownership.

## Recommended next research

- Capture one authenticated page load with structured Web and API logs for
   `GET /api/platform/account-preferences`, including Polly attempt numbers,
   timestamps, final status, and correlation identifiers.
- Read the active database partition with a non-mutating query to confirm
   whether the current-state row is absent and whether observation rows exist.
- Reproduce against an isolated fresh SQL volume to remove retained-state
   ambiguity, then compare with a volume containing a current-state row.
- Trace the pending work item or branch that introduced
   `GetAccountPreferencesResponse.State` to confirm the intended endpoint
   mapping and missing-row semantics.
- Inventory any resilience override expected specifically for
   `PlatformApiClient`; none was found in the current tree, but a runtime
   configuration source could alter documented defaults.