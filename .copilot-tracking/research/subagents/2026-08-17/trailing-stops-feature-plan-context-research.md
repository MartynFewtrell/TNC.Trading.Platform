---
title: Trailing Stops Feature Plan Context Research
description: Current repository architecture evidence for planning the trailing stops feature.
author: GitHub Copilot
ms.date: 2026-08-17
ms.topic: reference
keywords:
  - trailing stops
  - IG account preferences
  - implementation planning
  - repository architecture
estimated_reading_time: 8
---

## Research Status

Complete. This research is limited to planning evidence and does not modify
product code.

## Research Questions

* Verify the present feature, provider, API, web, persistence, retention, test,
  and documentation architecture that an Account Preferences and trailing-stops
  implementation must use.
* Identify concrete files and line ranges for each implementation surface.
* Evaluate implementation sequencing, dependencies, and risks, with particular
  attention to retention and legacy `Demo` to `Test` mapping.

## Source Proposal

The supplied baseline is
`.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md`.
It recommends an Operator-only live Account Preferences feature, a narrow
application-owned IG gateway, confirmed observations in append-only SQL history,
and explicit resolution of retention and legacy provider-environment mapping.

## Current Architecture Evidence

### Application Feature Pattern

* `src/TNC.Trading.Platform.Application/Features/AccountDetails/GetAccountDetailsRequest.cs:3`
  and `GetAccountDetailsHandler.cs:5-35` show a read request with an optional
  keyset cursor. The handler loads the current configuration, reads only the
  application-owned snapshot store, and creates adjacent cursors.
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsRequest.cs:3`
  and `RefreshAccountDetailsHandler.cs:6-89` show the nearest provider-backed
  command. It loads configuration, coalesces same-process requests, obtains a
  replica-safe lease, calls the gateway, and persists only a successful result.
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsGateway.cs:3-11`
  is the inward-owned port pattern. Its `Succeeded` and typed `Failed` records
  prevent provider types from entering Application.
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:5-18`
  validates, commits through an operation-specific port, then reconciles. This
  is the closest mutation-handler pattern but the trailing-stops command should
  remain a separate Account Preferences operation rather than extend the broad
  configuration update request.

Plan consequence: add an `AccountPreferences` vertical slice under
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/` with
separate `GetAccountPreferences` and `UpdateAccountPreferences` request,
response, handler, and validator files. Add a narrow
`IAccountPreferencesGateway` and a separate observation-store port there. Do
not put a live mutable preference in `AccountDetails`, whose existing purpose
is saved retrieval history.

### Provider Gateway and Dependency Injection

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:13-103`
  establishes the concrete IG adapter convention: protected credentials,
  session `POST session` at Version 2, `CST` and `X-SECURITY-TOKEN` extraction,
  request Version 1, strict JSON validation, one `401` session replay, and
  normalized timeout, unavailable, and malformed-data outcomes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:85-103`
  registers scoped persistence and retention components, then the typed
  `IAccountDetailsGateway` HTTP client using
  `https://demo-api.ig.com/gateway/deal/`. The current adapter and HTTP client
  are hard-coded to `BrokerEnvironmentKind.Demo`.
* `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs:3-7`
  contains persisted technical values `Demo` and `Live`; meanwhile
  `PlatformEnvironmentKind.cs:3-7` contains application values `Test` and
  `Live`. `TradingScheduleGate.cs:22-27` currently blocks only `Test` combined
  with legacy `Live`.

Plan consequence: implement `IgAccountPreferencesGateway` as an additional
feature-specific typed client and register it in the same DI extension. Reuse
the session behavior but do not extract a generic IG client unless shared code
becomes material after the second adapter. The gateway must use account
preferences Version 1 and must classify provider outcomes without forwarding
provider bodies, tokens, or credentials. Use `Test` and `Live` in new
application, API, UI, and documentation models; preserve `Demo` only as a
legacy provider/persisted-key compatibility concern until a deliberate
migration decision is made.

### API Endpoint and Problem Details Pattern

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:29-50`
  is the sole registration point for platform endpoints. It assigns Viewer to
  saved account-detail reads and Operator to account-detail refresh and
  configuration updates.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:105-120`
  keeps handler dispatch thin and delegates refresh outcome conversion.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountDetailsEndpointMapping.cs:10-36`
  maps typed provider outcomes to `429`, Problem Details `502`, Problem Details
  `503`, and Problem Details `504`; it maps command contention to `409`.
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdatePlatformConfigurationEndpointHandler.cs:9-30`
  validates its HTTP request and returns `ValidationProblem` for API or
  Application validation failures.

Plan consequence: add `GET /api/platform/account-preferences` and
`PUT /api/platform/account-preferences` in `PlatformEndpoints`, both requiring
the Operator policy. The update HTTP model must use `bool?` so omitted input is
not silently converted to `false`; the validator converts a present Boolean to
the non-null application command. Reuse the typed mapping shape with stable
status handling: invalid input `400`, allowance `429`, malformed provider
response `502`, unavailable or rejected upstream `503`, and timeout `504`.
An indeterminate `PUT` must read authoritative provider state before returning
a final preference model; it must not blindly retry the write.

### Blazor Operator, Presenter, Client, and Navigation Pattern

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:1-120`
  has Viewer route authorization, route-scope confirmation through
  `PlatformNavigationAccessCoordinator`, conditional Operator refresh, Radzen
  tabs, and history controls. It explicitly describes saved account snapshots.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs:5-58`
  keeps load/refresh state and turns selected client exceptions into display
  errors.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:1-232`
  is the closest Operator-only mutation page. It uses `EditForm`, disabled save
  state, status feedback, and write-only credential presentation.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationPagePresenter.cs:5-42`
  returns explicit load/save result records instead of exposing HTTP details to
  Razor markup.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:27-55` and `118-153`
  create scoped bearer requests, send API calls, and deserialize view models.
* `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:8-23`
  is the presenter registration composition point.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:17-37`
  renders Viewer navigation separately from Operator navigation.

Plan consequence: implement a dedicated Operator-only
`Components/Pages/AccountPreferences/AccountPreferences.razor` and matching
presenter/view-model files. Register the presenter in
`PlatformWebUiServiceCollectionExtensions`, add typed client methods and view
models to `PlatformApiClient`, and add one Operator navigation item in
`MainLayout`. Use an accessible labelled trailing-stops input, disabled loading
and save states, and visible outcome feedback. Keep Account Details unchanged.
The prior research's optional history view can be phased after the live control
unless the approved requirement explicitly demands retrospective UI in the
first delivery.

### EF Core, History, Migration, and Retention Pattern

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:26-147`
  contains DbSets and fluent mappings. Account-details mapping at `113-147`
  uses a broker-environment, timestamp, and identity index, plus explicit
  lengths and cascade relationships.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountDetailsRetrievalEntity.cs:3-14`
  is the simple immutable-header entity precedent.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:8-66`
  uses transactional save and deterministic environment-scoped keyset reads.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260807103334_AddAccountDetailsSnapshots.cs:11-105`
  is the additive migration precedent. The model snapshot must be updated by
  the EF migration command as part of a new table migration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/OperationalEventEntity.cs:3-29`
  and `EfPlatformEventStore.cs:10-57` support bounded, redacted operational
  failures, but the event query limits output to 50 items. They are not a typed
  preference-history model.
* `src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPolicy.cs:5-21`
  has one `Retention:OperationalRecordsDays` setting with a default of 90 days.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:9-105`
  applies that plan with SQL `ExecuteDeleteAsync` and in-memory fallback. Its
  account-details clause preserves the newest row per broker environment.

Plan consequence: add a dedicated append-only
`TrailingStopsPreferenceObservationEntity`, DbSet, mapping, migration, and
`EfTrailingStopsPreferenceObservationStore`. Use a non-unique index scoped by
the needed environment/account partition, observed time, and identity to retain
repeated equal observations and provide deterministic history ordering. Persist
only a successfully parsed provider observation; failed or indeterminate calls
must use a bounded operational event and must never create a nullable/false
history value. Retention must be decided before implementation. The generic
90-day operational configuration is not sufficient evidence that a trading
analysis record should expire at 90 days. If retention is approved, extend both
SQL and in-memory paths and test the approved newest-row or archive behavior.

### Tests and Focused Validation

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:8-239`
  provides faked gateway/store tests for read isolation, concurrent commands,
  typed failures, and redacted event emission.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs:10-104`
  provides the sequenced HTTP-handler pattern for headers, one `401` replay,
  strict payload parsing, and redaction.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountDetailsEndpointMappingTests.cs:8-108`
  verifies typed response and `429/502/503/504` mappings.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:7-126`
  uses bUnit's `PlatformComponentTestContext` for scoped rendered-page and
  error-preservation tests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfAccountDetailsSnapshotStoreTests.cs:10-109`
  covers atomic persistence, environment-scoped keyset reads, model indexes,
  and in-memory retention.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:65-245`
  is the SQL migration/index/restart integration location. The repository also
  has API integration and Web functional projects, but the existing feature
  does not require them for its core behavior.

Recommended focused commands, after implementation, are:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences|FullyQualifiedName~TrailingStops|FullyQualifiedName~Retention"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet build TNC.Trading.Platform.slnx
```

The SQL integration command requires its established Docker-backed SQL test
configuration. No focused unit or integration test should call real IG.

### Documentation and Wiki Impact

* `docs/wiki/api-reference.md:12-61` needs the two new Operator endpoints,
  request/response semantics, authority of the live provider, and the exact
  Problem Details mappings.
* `docs/wiki/operator-guide.md:7-37` needs the new Operator route and its
  navigation, capability meaning, and scope requirement.
* `docs/wiki/runtime-behavior.md:11-30` needs the account-preferences command,
  confirmed observation behavior, indeterminate-write reconciliation, and the
  approved retention policy.
* `docs/wiki/application-overview.md`, `docs/wiki/architecture.md`, and
  `docs/wiki/testing-and-quality.md` need concise updates for the additional
  Application port, Infrastructure adapter/table, and focused test coverage.
* `docs/wiki/local-development.md` and `docs/wiki/ig-day-trading-with-ig-apis.md`
  need updates only if the plan changes configured credentials, supported
  environment behavior, or the documented IG endpoint contract.

## Recommended Implementation Sequence

1. Resolve the two blocking product decisions: preference-observation retention
   (including archive/export requirements) and the `Demo` to `Test` migration
   strategy. Confirm that Test is the only supported delivery environment and
   that preference changes do not authorize real trading or monetary exposure.
2. Add the Application `AccountPreferences` contracts, validator, typed gateway
   outcome, observation-store port, and handler tests. Define confirmed update
   behavior as `PUT` followed by authoritative `GET`.
3. Add the IG adapter, dependency registration, redacted diagnostics, and unit
   tests for Version 1, headers, strict JSON, one unauthorized replay,
   allowance classification, and no blind uncertain-write retry.
4. Add the append-only observation entity, DbContext mapping, store, retention
   behavior, and additive migration. Verify both in-memory and SQL Server
   integration behavior, including deterministic ordering and migration indexes.
5. Add the thin API routes, HTTP request model, endpoint mappings, policy
   enforcement, and API unit tests.
6. Add the Web view models, API-client methods, presenter, Operator page,
   presenter DI, navigation item, and bUnit coverage for load, toggle, save,
   error, disabled states, and scope handling.
7. Update the affected wiki source-of-truth pages and run the focused commands
   before the full solution build and the appropriate broader test suites.

## Plan Structure Recommendation

Use an implementation plan with these sections:

1. Scope, non-goals, and decisions required before coding.
2. Application vertical slices and typed outcome contract.
3. IG adapter and dependency-injection changes.
4. Observation history, migration, retention, and failure telemetry.
5. Platform API routes, authorization, input validation, and Problem Details.
6. Blazor page, presenter, API client, and role-based navigation.
7. Test matrix and execution gates.
8. Wiki and operator documentation updates.
9. Rollout constraints, including Test-only support and real-IG exclusion from
   automated tests.

## Follow-On Questions

* What retention and archive/export period does the business require for
  preference observations used in trading-behavior analysis?
* Should existing persisted `BrokerEnvironmentKind.Demo` values migrate to
  `Test`, or should reads remain backward compatible until a planned migration?
* Does the first delivery require an operator-visible observation timeline, or
  is durable capture plus a later history query sufficient?
* Has the configured IG Test account been verified to allow
  `GET` and `PUT /accounts/preferences`, including its allowance and rejection
  response codes?
