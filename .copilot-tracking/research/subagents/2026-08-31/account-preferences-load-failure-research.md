---
title: Account preferences load failure research
description: Traces the account preferences UI load path and identifies the strongest evidenced cause of the load failure message.
ms.date: 2026-08-31
ms.topic: troubleshooting
---

## Research scope

* Trace the account preferences Web page or component through the API client, endpoint, application operation, infrastructure persistence, schema and migrations, and response handling.
* Investigate recent Git history and diffs relevant to the latest update where useful.
* Identify the concrete or strongest evidenced cause of the UI message `Unable to load account preferences.`
* Record exact workspace-relative paths and 1-based line ranges, relevant tests, runtime or log evidence, and a minimal discriminating verification.

## Status

Complete, with one runtime evidence gap: no AppHost, API, or Web process was
running during this investigation, and no retained application request log
identified the status code from the reported browser session.

## Executive finding

The UI text `Unable to load account preferences.` is not a distinct server
error. It is a catch-all for `HttpRequestException` or `InvalidOperationException`
across two sequential requests made by the initial page load:

1. `GET /api/platform/account-preferences`
2. `GET /api/platform/account-preferences/observations?pageSize=25`

The strongest evidenced primary-path cause is a non-success response from the
first request because the API performs a live IG read and maps every expected
provider failure to a non-2xx response. The most likely concrete trigger in an
unconfigured or restarted local environment is missing or undecryptable Demo
credentials: the credential service converts those values to empty strings,
the IG session request is rejected, the gateway classifies that as unavailable,
the API returns `503`, and `EnsureSuccessStatusCode` throws. This is strongly
supported by the code path but is not proven for the reported browser session
because its network response and API log are unavailable.

A second indistinguishable cause is failure of the observations request after
the live preference request succeeds. The presenter applies the confirmed
preference before requesting history, then catches a failure from either call
under the same message. If the page showed a confirmed preference editor as
well as the error, this second path is the better diagnosis.

The historical EF migration problem is concrete but is not the strongest cause
for the current checkout. A retained August 30 test result records
`PendingModelChangesWarning` during `MigrateAsync`, while later reruns and the
current focused test run prove the repaired migration chain creates and reads
the observation table successfully. Startup also fails before serving requests
when migration application fails.

## Vertical trace

### Web component and response handling

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-12`
  declares the Operator-only route and renders the loading state.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:14-18`
  renders `Presenter.Error`; lines 49-54 invoke `Presenter.LoadAsync` only after
  the required scope check succeeds.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:19-36`
  first applies current preferences, then loads history. Lines 31-34 replace
  either an HTTP failure or an invalid/empty JSON result with the reported text.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-146` sends the authorized
  current-preferences GET, calls `EnsureSuccessStatusCode`, and deserializes the
  response. Lines 158-166 do the same for history.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:168-179` obtains an Operator
  access token and adds it as a bearer token. Token acquisition can also throw
  an `InvalidOperationException`, which the presenter reports with the same text.

### API endpoint and response mapping

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-59`
  maps all three account-preference routes and requires the Operator policy.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:130-145`
  delegates the current, update, and history requests to their endpoint handlers.
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:6-12`
  invokes the application handler and maps its typed outcome.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:9-23`
  returns `200` only for `Succeeded`. Lines 25-48 map expected failures to
  `429`, `502`, `503`, or `504` Problem Details.

### Application operation

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:13-19`
  reads configuration and rejects every environment except Test plus Demo.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:21-35`
  calls the live gateway and synchronously appends a verified observation before
  returning a successful result.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:7-17`
  performs a SQL-only, partitioned history query after applying the same
  environment policy.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:5-11`
  defines Test plus Demo as the only supported pair.

### IG gateway and credentials

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:20-54`
  reads protected Demo credentials, creates a session, and translates timeout,
  connectivity, and JSON failures to typed outcomes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:56-66`
  posts credentials to `session`; any non-success response becomes an
  `HttpRequestException` and therefore an unavailable outcome.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:69-89`
  calls `accounts/preferences` with Version 1, maps `401`, `403`, and other
  failures, and requires a Boolean `trailingStopsEnabled` value.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs:73-102`
  returns empty strings when credential rows are missing or cannot be decrypted.
  The account-preference gateway does not preflight `CredentialPresence` before
  attempting the IG session.
* `src/TNC.Trading.Platform.Api/appsettings.json:42-45` bootstraps Test plus Demo,
  so the environment guard normally permits the local request.

### Persistence, schema, and migrations

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:10-26`
  inserts and saves one observation on every successful live read.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-61`
  queries the same table for the immediately following history request.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:150-162`
  configures the key, bounded columns, and deterministic partition index.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs:12-45`
  creates `TrailingStopsPreferenceObservations` and its indexes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830160041_RemoveTrailingStopsBrokerEnvironmentIndex.cs:8-17`
  removes the redundant broker-only index.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:42-63`
  applies SQL migrations at startup and terminates startup with a critical error
  if migration application fails.

## Cause ranking

1. Strongest primary-path cause: `GET /api/platform/account-preferences` returned
   non-success because its mandatory live IG credential/session/provider request
   failed. Missing or undecryptable Demo credentials are the strongest specific
   local trigger supported by code, but no live response proves which provider
   category occurred.
2. Equally compatible secondary-path cause: the current-preferences request
   returned `200`, but the observations request failed. This is distinguishable
   by request order and whether a confirmed editor was rendered.
3. Historical but currently disconfirmed cause: the observation schema was not
   migratable because EF detected pending model changes. The repaired migration
   tests now pass, and an API that completed current startup should have applied
   the table migration.
4. Less likely alternatives: access-token acquisition returned no usable token,
   a successful response had empty or incompatible JSON, or an unhandled EF or
   credential exception caused HTTP `500`. All are folded into the same UI text.

## Observability defect

`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:26-33`
calls `RecordFailureAsync` without the injected `eventStore`. The helper at
lines 41-45 returns immediately when that optional argument is null. Expected
gateway failures therefore produce their HTTP Problem Details but no persisted
Account Preferences operational event. The unsupported-environment path does
pass the store at lines 47-51. This defect does not cause the load failure, but
it removes the server-side evidence that would normally identify its category.

## Evidence

* Commit `f4b3b3b4669a8e783e063b881403ee5bcd17dcd5` on 2026-08-30 introduced
  the complete account-preferences slice, including Web, API, application,
  gateway, persistence, migrations, and tests.
* The current uncommitted changes modify account-preference presentation,
  pending/confirmed UI state, and save/history feedback. They do not modify
  `LoadAsync`, `PlatformApiClient`, API mapping, application GET handling,
  gateway behavior, or persistence, so they do not introduce the reported load
  failure path.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TestResults/phase4-4-migration-identity.trx:8-17`
  records the initial `PendingModelChangesWarning` failure on 2026-08-30.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TestResults/phase4-4-migration-identity-rerun.trx:8-24`
  records the same migration identity test passing after repair.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TestResults/phase4-4-snapshot-repair-rerun.trx:8-24`
  records the idempotent migration rerun test passing.
* `artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/quality-gate.log:686-734`
  records passing application, API, Web, infrastructure unit, infrastructure
  integration, and Web functional suites. Lines 817-821 record unrelated API
  integration failures caused by a Keycloak port lease timeout.
* A focused current-checkout run of `AccountPreferencesTests.cs`,
  `IgAccountPreferencesGatewayTests.cs`, and
  `AccountPreferencesSqlIntegrationTests.cs` passed 39 of 39 tests.
* No AppHost, API, or Web process was running during investigation. No retained
  application log or network capture contained the reported request.

## Relevant tests

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:155-170`
  proves a `503` on the first request renders the exact message and hides an
  unconfirmed editor.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:299-309`
  provides retained coverage for generic initial-load failure feedback.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:12-27`
  verifies the successful IG session and preferences contract; lines 29-40,
  42-52, and 54-64 verify malformed data, one-time `401` replay, and `403`
  classification.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:10-24`
  verifies stable non-success mapping and diagnostic redaction.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:15-33`
  verifies table, index, and migration identities; lines 50-64 verify append and
  read after replacing the context.

## Minimal discriminating verification

With the AppHost running and an Operator signed in, preserve the browser Network
entries and reload `/account-preferences`. Inspect these two requests in order:

1. If `GET /api/platform/account-preferences` is non-2xx, the primary live read
   failed. Its Problem Details title distinguishes allowance (`429`), malformed
   IG data (`502`), unavailability/rejection/environment (`503`), and timeout
   (`504`). A bare `500` points instead to an unhandled credential, configuration,
   or observation-insert exception.
2. If that request is `200`, inspect
   `GET /api/platform/account-preferences/observations?pageSize=25`. A non-2xx
   response proves the generic message came from history/persistence, not IG.
3. If both are `200`, inspect their JSON bodies and the Web server log for token
   acquisition or deserialization failure.

For the strongest credential hypothesis, check the Configuration page/API for
the Demo credential presence and usability flags before repeating step 1. For
a `500` or failed observations request, query SQL for both
`20260830112227_SnapshotRepair` and
`20260830160041_RemoveTrailingStopsBrokerEnvironmentIndex` in
`__EFMigrationsHistory`, then confirm `OBJECT_ID('TrailingStopsPreferenceObservations')`
is non-null.

## Remaining gaps

* The status, Problem Details body, and request order from the reported browser
  session are unavailable.
* The current Demo credential presence/usability state was not observable from
  a running API or database connection.
* No real-IG verification exists in the automated evidence by design.
* Expected gateway failure telemetry is absent because the GET handler omits
  the event store argument.

## Recommended next research

* Capture the two browser network responses using the minimal verification above.
* Correlate the first non-2xx response with API structured logs and Aspire traces.
* Verify Demo credential presence/usability without exposing secret values.
* If the response is `500`, inspect the exception and SQL migration history/table
  state before changing code.
* Add a narrow test that returns `200` for current preferences and `503` for
  history to explicitly document the initial-load ambiguity.
* Investigate the omitted event-store argument as a separate observability fix.

## Clarifying questions

* Did the failed page render a confirmed Enabled/Disabled editor beneath the
  error? If yes, the live IG read succeeded and the history request failed.
* What were the status codes and Problem Details titles for the two account-
  preference GET requests in the failing browser session?
