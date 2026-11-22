---
title: Trailing Stops Remediation Plan Verification
description: Current-workspace verification for planning repairs after trailing-stops post-implementation failures
author: GitHub Copilot
ms.date: 2026-08-30
ms.topic: reference
keywords:
  - trailing stops
  - remediation
  - verification
  - testing
---

## Research scope

Verify the primary trailing-stops failure conclusions against the current
workspace and identify execution-ready repair boundaries without changing
product, test, configuration, or wiki files.

Questions:

* Which exact current files and line ranges own missing API registrations and
  endpoint composition?
* Which existing test projects and patterns fit route composition, fixture
  fail-fast cancellation, provider gateway, handler outcomes, EF migration and
  history behavior, environment mapping, and Blazor cancellation?
* Which bounded build and test commands does the repository support?
* Which migration artifacts exist, and what deployment-history fact still gates
  the migration repair choice?
* Which wiki and delivery-record files need follow-up changes?
* In what phase and file dependency order should repair work proceed?

## Status

Complete. The primary conclusions are verified against the 2026-08-30
worktree. No product, test, configuration, wiki, or delivery-record file was
changed. The complete trailing-stops implementation remains uncommitted user
work and must be preserved during remediation.

## Verified findings

### API registration and endpoint composition

The initiating startup defect remains present.

* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:18-46`
  owns the explicit Application registration list. It ends after
  `RefreshAccountDetailsHandler` and omits `GetAccountPreferencesHandler`,
  `UpdateAccountPreferencesValidator`, `UpdateAccountPreferencesHandler`, and
  `GetTrailingStopsPreferenceObservationsHandler`.
* `src/TNC.Trading.Platform.Api/Program.cs:19-23` calls
  `AddPlatformApplication` before Infrastructure registration. Adding the four
  services to that extension, rather than adding isolated registrations to
  `Program.cs`, matches current composition ownership.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:51-61`
  maps the three account-preferences routes.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:129-147`
  composes those routes with the four missing concrete services. The GET
  delegates make an unregistered handler look like an inferred body parameter,
  which reproduces the recorded `Body was inferred` endpoint-construction
  failure.
* Infrastructure ports are already registered at
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:94-98`.
  This is an API-owned Application registration gap, not an Infrastructure gap.

The nearest composition-test home is
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests`. Its project
references the API project at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:22-24`,
and `PlatformApiAuthenticationServiceCollectionExtensionsTests.cs:14-44`
already builds a `WebApplication` directly and resolves registered services.
Current account-preferences tests only call static endpoint helpers, including
`null!` handlers at `AccountPreferencesEndpointTests.cs:9-32` and
`AccountPreferencesEndpointMappingTests.cs:39-59`; they cannot construct or
validate the route graph.

### Test projects and reusable patterns

No new test project is needed.

| Repair concern | Existing project and pattern | Required extension |
|---|---|---|
| Route composition | API UnitTests; direct `WebApplication.CreateBuilder`, build, scope, and service resolution in `PlatformApiAuthenticationServiceCollectionExtensionsTests.cs:14-44` | Add one account-preferences composition test that calls the real `AddPlatformApplication`, maps `MapPlatformEndpoints`, forces endpoint metadata construction, and resolves all four services |
| Fixture fail-fast cancellation | API IntegrationTests; `SyntheticTokenIntegrationTestFixture.cs:19-36` owns create/build/start/readiness and cleanup-on-catch; shared `test/Shared/Authentication/RealAppHostProcessFactory.cs:45-110` demonstrates a 90-second startup token and cleanup | Use one sub-60-second linked token for create, build, start, and readiness; preserve child diagnostics and stop when API exits instead of waiting five minutes |
| Provider gateway | Infrastructure UnitTests; `Integrations/Ig/IgAccountPreferencesGatewayTests.cs:10-78` uses a sequenced `HttpMessageHandler` and captures headers and bodies | Change fixtures and assertions from `trailingStops` to `trailingStopsEnabled`; add malformed 2xx PUT acknowledgement as indeterminate and caller-cancellation coverage |
| Handler outcomes | Application UnitTests; `Features/AccountPreferences/AccountPreferencesHandlerTests.cs:11-154` covers environment rejection, PUT/GET confirmation, indeterminate reconciliation, observations, and telemetry | Add mismatch/not-applied outcomes, valid mismatch observation, initial PUT plus confirmation telemetry, real GET failure wiring, platform-and-broker guard, and cancellation propagation |
| EF history and cursor | Infrastructure UnitTests; `EfTrailingStopsPreferenceObservationStoreTests.cs:8-43` covers equal-time ordering and cursor paging | Replace the test that approves unknown-cursor fallback; cover partitioned reads and malformed, unknown, and cross-partition rejection |
| EF migration and SQL behavior | Infrastructure IntegrationTests; `AccountPreferencesSqlIntegrationTests.cs:12-66` covers empty migration, restart readback, and retention; `PlatformSqlServerIntegrationTests.cs:327-422` covers fail-closed conflicts and recovery | Assert applied migration identity, empty database and already-applied `SnapshotRepair` upgrade paths, both intended indexes, equal-time SQL paging, non-null cursor, partition isolation, and configurable retention |
| Environment mapping | Application UnitTests; `Features/AccountPreferences/AccountPreferencesEnvironmentPolicyTests.cs:7-29` covers `Demo` presentation and broker-only rejection; API UnitTests mapping suite is the transport home | Require `PlatformEnvironmentKind.Test` plus `BrokerEnvironmentKind.Demo`, retain stored `Demo`, and assert API history presents `Test` through `BrokerEnvironmentPresentation` |
| Blazor cancellation | Web UnitTests; `AccountPreferencesTests.cs:11-149` uses bUnit and controllable delayed content; `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:58-80` is the local owned-token disposal pattern | Make delayed HTTP work cancellation-aware, dispose the component, assert cancellation reaches the request, and assert teardown does not render an error banner |

### Provider, outcome, environment, and UI defects

The static correctness conclusions remain current.

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-18`
  serializes `trailingStops`; lines 77-89 use reflection and deserialize the
  same wrong property. The provider contract and accepted research require
  `trailingStopsEnabled`. Tests encode the same wrong contract at
  `IgAccountPreferencesGatewayTests.cs:10-35` and lines 69-78.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-31`
  reports any successful confirmation GET as success even when its Boolean
  differs from the request. The current test explicitly approves request
  `false` followed by observed `true` at
  `AccountPreferencesHandlerTests.cs:37-55`.
* `IgAccountPreferencesGateway.cs:74-83` classifies a malformed successful PUT
  acknowledgement as definite failure even though write application is
  uncertain. It should enter the existing indeterminate reconciliation path.
* `GetAccountPreferencesHandler.cs:22-41` omits the injected event store on the
  normal GET failure path and permits the helper to silently return when the
  store is null. `UpdateAccountPreferencesHandler.cs:20-38` does not emit a
  separate event for every failed or indeterminate provider operation.
* `AccountPreferencesEnvironmentPolicy.cs:5-10` checks only legacy broker
  `Demo`; current configuration also supplies platform environment. The policy
  should require platform `Test` and broker `Demo`.
* `AccountPreferencesEndpointMapping.cs:9-13` presents persisted broker `Demo`
  directly. `BrokerEnvironmentPresentation.cs:3-13` already owns the accepted
  `Demo` to `Test` compatibility mapping.
* `AccountPreferences.razor:27-35` passes `CancellationToken.None` to load, save,
  and paging and has no disposal lifetime. Its presenter catches HTTP and
  invalid-operation failures at `AccountPreferencesPagePresenter.cs:10-39` but
  should allow owned teardown cancellation without an error banner.

### Persistence, cursor, and retention defects

* `ITrailingStopsPreferenceObservationStore.cs:3-7` accepts only cursor and page
  size, so the port cannot express the current platform and broker partition.
* `GetTrailingStopsPreferenceObservationsHandler.cs:7-16` loads configuration but
  does not pass either environment to the store.
* `EfTrailingStopsPreferenceObservationStore.cs:28-50` reads globally. A
  malformed GUID, missing cursor row, or cursor from another partition silently
  restarts at the newest global page.
* `GetTrailingStopsPreferenceObservationsEndpointHandler.cs:10-23` validates
  only cursor length, not syntax or semantic validity.
* `PlatformDbContext.cs:150-161` contains the complete entity mapping, while
  lines 179-184 configure the same entity again and add a second broker-only
  index.
* `src/TNC.Trading.Platform.Api/appsettings.json:32-37` declares both
  `OperationalRecordsDays` and inert
  `TrailingStopsPreferenceObservationsDays`. The processor creates the
  operational plan at
  `OperationalRecordRetentionProcessor.cs:16-21`, but trailing-stops deletion
  uses a fixed policy at lines 52-55 and 86-89. The existing unit test uses 90
  days, so it does not discriminate between the two sources; the SQL test sets
  one day but inserts a 91-day-old row, which also cannot discriminate.

### Migration artifacts and deployment-history gate

Current source artifacts are:

* Discoverable `SnapshotRepair` migration:
  `Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs:1-55` and
  `Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:1-16`
* Undiscoverable duplicate source:
  `Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs:1-42`
* Current model snapshot:
  `Migrations/PlatformDbContextModelSnapshot.cs:673-727`

`SnapshotRepair` creates the table and both broker-only and
broker-plus-platform indexes at lines 11-46. The later source creates the same
table and only the broker-plus-platform index at lines 11-36, but has no
designer or `[Migration]` metadata. A no-connect EF CLI check using the
Infrastructure design-time factory listed five migrations through
`20260830112227_SnapshotRepair` and did not list
`20260830120000_AddTrailingStopsPreferenceObservations`:

```powershell
dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext --no-build --no-connect
```

Using API as the startup project is not currently supported because API does
not reference `Microsoft.EntityFrameworkCore.Design`; Infrastructure owns that
private package and `PlatformDbContextFactory.cs:6-16`.

The unresolved gate is deployment history: no workspace evidence establishes
whether `20260830112227_SnapshotRepair` reached CI, shared development, staging,
or any deployed/non-disposable database. Inventory `__EFMigrationsHistory` in
every such database before deleting or renaming discoverable migration history.
Until owners prove it is local-only, preserve `SnapshotRepair`, remove only the
undiscoverable duplicate, deduplicate the model, and create a successor
corrective migration only for a real schema delta. This gate blocks migration
edits, not API registration or fixture hardening.

### Affected wiki and delivery records

The six modified wiki sources must be updated after behavior is repaired:

* `docs/wiki/api-reference.md:551-582`
* `docs/wiki/application-overview.md:195-210`
* `docs/wiki/architecture.md:587-601`
* `docs/wiki/operator-guide.md:35` and lines 557-578
* `docs/wiki/runtime-behavior.md:11-23` and lines 714-733
* `docs/wiki/testing-and-quality.md:541-553`

The durable delivery records also require reconciliation:

* `.copilot-tracking/changes/2026-08-17/trailing-stops-features-changes.md:64-83`
  overstates strict gateway behavior, deterministic history, EF metadata, and
  completed validation, and contains duplicate `Release Summary` headings.
* `.copilot-tracking/plans/logs/2026-08-17/trailing-stops-features-log.md:56-74`
  should resolve WI-04 with the verified API composition cause and retain WI-05
  as manual provider evidence.
* `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:279-313`
  is the final documentation/validation contract. Preserve it as planning
  history unless the remediation workflow uses it as the active delivery
  record; do not rewrite its original requirements to match defective code.

## Bounded build and test commands

The solution and all named projects exist in `TNC.Trading.Platform.slnx`. The
repository pins .NET SDK `10.0.200` in `global.json:1-6`, and
`.github/scripts/Invoke-CleanArchitectureQualityGate.ps1:43-61` confirms the
solution build/test plus TRX pattern. Use an external 60-second process-tree
watchdog for every command. Test commands should include both `--blame-hang`
and `--blame-hang-timeout 45s`; after one successful build, add `--no-build`.

```powershell
dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferences" --blame-hang --blame-hang-timeout 45s
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferences" --blame-hang --blame-hang-timeout 45s
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --filter "FullyQualifiedName~IgAccountPreferencesGatewayTests" --blame-hang --blame-hang-timeout 45s
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --filter "FullyQualifiedName~EfTrailingStopsPreferenceObservationStoreTests|FullyQualifiedName~OperationalRecordRetentionProcessorTests" --blame-hang --blame-hang-timeout 45s
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesSqlIntegrationTests" --blame-hang --blame-hang-timeout 45s --logger "trx;LogFileName=trailing-stops-sql.trx"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesTests" --blame-hang --blame-hang-timeout 45s
```

After the focused slices and one bounded AppHost-backed smoke test pass:

```powershell
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test TNC.Trading.Platform.slnx --no-restore -m:1 --blame-hang --blame-hang-timeout 45s --logger "trx"
```

Do not call real IG from automated validation. The no-connect EF command above
is suitable for migration identity; applied-history inventory requires
read-only SQL access to each actual environment.

## Evidence

Primary and independent research checked against current source:

* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md`
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md`
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-build-test-failures-research.md`
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-static-integration-audit-research.md`
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-alternatives-research.md`
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-final-synthesis-review.md`

Current-workspace checks:

* Scoped `git status --short` confirmed the feature, tests, migration artifacts,
  configuration, and all six wiki pages remain modified or untracked user work.
* EF no-connect migration listing confirmed `SnapshotRepair` is discoverable
  and the later duplicate is not.
* No build, test, application startup, database connection, or database write
  was performed during this verification.

## Unresolved questions

* Has `20260830112227_SnapshotRepair` reached any non-disposable CI, shared,
  staging, or deployed database? This must be answered before migration history
  is deleted or renamed.
* Does the API owner approve a typed not-applied outcome mapped to HTTP `409`
  when authoritative GET observes a Boolean different from the requested one?
* Does product ownership reaffirm shared
  `Retention:OperationalRecordsDays`, or require an independently configurable
  trailing-stops retention period? Current source exposes both choices but only
  the fixed 90-day policy is effective.
* Can the original 15-test TRX or equivalent retained test record be recovered?
  The common fixture cascade is high confidence, but exact membership remains
  unproven.
* Real IG Test endpoint availability and allowance behavior still need
  sanitized, controlled manual evidence. They must remain outside default tests.

## Recommended dependency ordering

1. Run the migration deployment-history inventory in parallel. It gates only
   migration source reconciliation.
2. Restore API composition in
   `PlatformApplicationServiceCollectionExtensions.cs`, then add the in-process
   route-composition test in API UnitTests. Run only that slice first.
3. Harden `SyntheticTokenIntegrationTestFixture.cs` and its readiness helper
   with one bounded cancellation chain and preserved diagnostics. Run one API
   integration smoke test, then one Web/AppHost smoke test.
4. Correct the provider boundary in `IgAccountPreferencesGateway.cs` and its
   sequenced-handler tests. This establishes trustworthy wire outcomes before
   changing Application semantics.
5. Extend Application outcome contracts, update handler confirmation and
   telemetry behavior, then update API response mapping and focused API tests.
6. Once the deployment-history gate is answered, reconcile migration artifacts
   and `PlatformDbContext` mapping. Test empty migration and already-applied
   `SnapshotRepair` upgrade before changing history query behavior.
7. Change the observation-store port, handler, EF query, cursor result contract,
   and API validation together. Add unit partition/cursor tests followed by SQL
   equal-timestamp and non-null-cursor tests.
8. Apply platform-plus-broker environment policy and API presentation mapping,
   then unify retention on the approved setting with discriminating one-day
   unit and SQL tests.
9. Add component-owned cancellation in `AccountPreferences.razor`, preserve
   cancellation through the presenter/client, and add bUnit disposal coverage.
10. Update all six wiki pages and the release changes/log only after final
    behavior is green. Run focused projects, one bounded distributed smoke per
    affected fixture, then the solution gates.

## Recommended next research

* [ ] Inventory `__EFMigrationsHistory` in every non-disposable environment
* [ ] Obtain API-contract approval for the mismatch outcome and payload
* [ ] Confirm the authoritative retention-setting decision
* [ ] Recover the original distributed-test result inventory when available
* [ ] Capture sanitized manual IG Test endpoint and allowance evidence