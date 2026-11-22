<!-- markdownlint-disable-file -->
# Trailing Stops Static Integration Audit Research

## Status

Complete. Static audit completed against the uncommitted worktree on
2026-08-30. No production or test files were changed.

## Research Questions

* Does the completed implementation match the plan, detail, research, and execution log?
* Are application contracts, infrastructure integration and persistence, API endpoints, web client and UI, dependency injection, migrations, configuration, and tests connected consistently with established neighboring patterns?
* Are there integration defects likely to prevent startup or cause broad test failures, especially service registration failures, fixture constructor churn, provider-incompatible migrations, invalid options, route conflicts, mock mismatches, lifecycle or disposal errors, and shared-state or parallelism hazards?
* What is the smallest coherent remediation sequence, without applying fixes?

## Initial Scope

The current worktree contains uncommitted production, test, migration, configuration, and wiki changes for account preferences and trailing-stop preference observations. The audit will compare those changes with the nearest established implementations and focused static or cheap executable checks.

## Inputs And Inventory

The audit read the governing repository instructions, including
`.github/copilot-instructions.md`, `.github/AGENTS.md`, `.github/lessons.md`,
and the scoped architecture, C#, configuration, .NET stack, Radzen Blazor,
refactoring, Scalar, test, Aspire test, folder, Markdown, and writing-style
instructions.

The implementation source is:

* `.copilot-tracking/plans/2026-08-17/trailing-stops-features-plan.instructions.md:1-122`
* `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:1-320`
* `.copilot-tracking/research/2026-08-17/trailing-stops-features-research.md:1-39`
* `.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:1-319`
* `.copilot-tracking/research/subagents/2026-08-17/trailing-stops-feature-plan-context-research.md:1-270`
* `.copilot-tracking/plans/logs/2026-08-17/trailing-stops-features-log.md:1-70`

Reliable `git status --short --untracked-files=all` output shows 18 tracked
modifications and 40 untracked files. The production inventory is:

* Six modified wiki pages under `docs/wiki/`
* Modified API routes and configuration in
	`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` and
	`src/TNC.Trading.Platform.Api/appsettings.json`
* Four new API mapping/handler files under
	`src/TNC.Trading.Platform.Api/Features/Platform/`
* Seventeen new Application feature files under
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/`, plus
	`Configuration/BrokerEnvironmentPresentation.cs` and
	`Services/TrailingStopsPreferenceObservationRetentionPolicy.cs`
* Modified Infrastructure DI, retention, DbContext, and model snapshot files;
	new gateway, store, entity, intended migration, and two `_SnapshotRepair`
	migration files
* Modified Web API client, UI DI, and navigation; three new Account Preferences
	page/presenter/view-model files

The test inventory is:

* Two new API unit-test files
* Three new Application unit-test files
* One new Infrastructure SQL integration test, two new Infrastructure unit
	test files, and modified SQL fixture and retention tests
* One new Web unit-test file and one shared bUnit fixture registration change

Recent history establishes the baseline as commit `ac8d6f3` on branch
`012-add-account-details`; all trailing-stop work is uncommitted.

## Key Findings

### Critical: API composition omits every new Application service

`PlatformEndpoints` adds three route delegates that require
`GetAccountPreferencesHandler`, `UpdateAccountPreferencesValidator`,
`UpdateAccountPreferencesHandler`, and
`GetTrailingStopsPreferenceObservationsHandler` at
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:51-61`
and `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:129-146`.
The explicit Application registration list ends with the Account Details
handlers and registers none of these new services at
`src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:32-46`.

This can prevent Minimal API endpoint delegate construction because unregistered
complex parameters may be inferred as body inputs, especially on GET routes. At
minimum, all three routes fail service resolution on invocation. The new API
tests invoke static helper methods with `null!` handlers and never construct the
application route graph at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs:9-34`.

### Critical: the IG wire contract uses the wrong property name

The official contract recorded by the accepted research is
`trailingStopsEnabled` for both GET and PUT at
`.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:125-152`.
The adapter sends `{ trailingStops = ... }` and deserializes
`trailingStops` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-17`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:78-99`.
The mock tests repeat and assert the incorrect shape at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:13-38`
and
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:67-78`.

The implemented gateway therefore rejects the documented GET payload as
malformed and sends a PUT body that IG does not document.

### High: migration history is incoherent and provider validation is masked

The intended
`20260830120000_AddTrailingStopsPreferenceObservations.cs` migration has no
generated designer partial, `[DbContext]`, or `[Migration]` metadata at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs:1-42`.
The only metadata-bearing new migration is the earlier, generically named
`20260830112227_SnapshotRepair` under `_SnapshotRepair` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:1-18`.
That repair migration creates the feature table and two indexes at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs:11-54`.

`PlatformDbContext` configures the same entity twice and creates both index
shapes at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:147-159`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:176-182`.
The snapshot consequently contains both indexes at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs:673-727`.

The SQL test proves that some migration creates the table and desired index,
but it does not assert that the intended migration identifier was applied at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:14-26`.
This allows the accidental `_SnapshotRepair` artifact to make tests green while
the named migration is undiscoverable. The EF CLI could not provide an
independent list because the repository's design-time setup attempted a missing
SQL connection.

### High: update success is reported when the requested state was not applied

After PUT, any successful GET is returned as success without comparing its
Boolean with the requested Boolean at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-31`.
The test explicitly requests `false`, confirms `true`, and expects success at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:39-54`.
The Web presenter then always says "saved and confirmed" after any successful
response at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:27-40`.

This violates the plan's authoritative-confirmation semantics. A reconciled GET
that differs from the requested value is valuable observed evidence, but it is
not confirmation that the requested update succeeded.

### High: required failure telemetry is silently skipped

On a failed GET, the handler calls `RecordFailureAsync` without its injected
event store at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:22-31`.
The method's optional store defaults to `null` and returns immediately at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:36-41`.
The telemetry test bypasses the handler and calls the helper directly with a
fake store at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:116-131`.

The update handler records only when the selected terminal outcome is
`Failed`; terminal `Indeterminate` outcomes produce no event at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:33-38`.
It also records at most one event when an indeterminate PUT is followed by a
failed reconciliation GET, although the plan requires a bounded event for each
failed or indeterminate provider operation.

### High: history is neither partitioned nor strictly cursor-validated

The Application store port has no platform or broker partition arguments at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs:3-7`.
The EF reader queries the entire table, and an unknown or malformed GUID cursor
falls back to the newest global page at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-50`.
The handler loads current configuration but never passes it to the store at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:7-16`.

The HTTP boundary rejects only empty or overlong cursors, so ordinary malformed
values are accepted at
`src/TNC.Trading.Platform.Api/Features/Platform/GetTrailingStopsPreferenceObservationsEndpointHandler.cs:10-23`.
The tests cover empty and overlong values, not invalid GUID text, at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:47-59`.

This can mix historical partitions and lets a cursor from another partition
influence paging. It also contradicts the planned malformed-cursor validation.

### Medium: Test-only policy checks the legacy broker key, not application status

The policy accepts `BrokerEnvironmentKind.Demo` and rejects only the broker
`Live` key at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:5-10`.
All three handlers pass only `configuration.BrokerEnvironment` at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:13-18`,
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:12-18`,
and
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:7-13`.

A misconfigured `PlatformEnvironmentKind.Live` plus legacy `Demo` broker key
therefore passes the feature guard. The policy test encodes broker-key behavior
only at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesEnvironmentPolicyTests.cs:19-31`.

### Medium: legacy Demo presentation leaks through the new API and UI

`BrokerEnvironmentPresentation.ForAccountPreferences` correctly maps `Demo` to
`Test` at
`src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentPresentation.cs:3-13`,
but production code never calls it. History mapping serializes
`observation.BrokerEnvironment.ToString()` at
`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:9-13`.
The page displays platform and broker values together at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:21-24`.

The new feature therefore presents `Test / Demo`, contrary to the plan's rule
that new API and UI models never present Demo as an application status.

### Medium: the new retention configuration key is inert

`Retention:TrailingStopsPreferenceObservationsDays` is declared at
`src/TNC.Trading.Platform.Api/appsettings.json:32-37`, but no production code
reads it. The policy is a fixed constant at
`src/TNC.Trading.Platform.Application/Services/TrailingStopsPreferenceObservationRetentionPolicy.cs:3-8`,
and the processor calls that constant path at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:52-55`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:86-89`.

There is no options binding or startup validation for the new setting. The
declared operator configuration can be changed without changing behavior.

### Medium: malformed PUT acknowledgements are not reconciled

The adapter classifies a non-`SUCCESS` 2xx PUT acknowledgement as definite
`Failed(MalformedProviderData)` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:83-89`.
The update handler reconciles only `Indeterminate` outcomes and skips GET after
all `Failed` outcomes at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-23`.

Because IG may have applied the write before returning a malformed or unexpected
acknowledgement, this path should be treated as uncertain and reconciled. No
test covers it.

### Low: page operations are not cancelled on component disposal

The page passes `CancellationToken.None` for initial load, save, and next-page
operations at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:27-34`.
Unlike the shared layout's owned cancellation source at
`src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:52-75`, the
page does not cancel in-flight HTTP work when navigation disposes the component.
Delayed bUnit tests release their gates before disposal and do not cover this
lifecycle at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:52-88`.

## Negative Findings And Coverage Gaps

* No route conflict was found. The three method/path combinations are unique at
	`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:51-61`.
* Infrastructure store and typed HTTP client registrations are present at
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:91-108`.
* Web presenter registration exists in production and the shared bUnit context
	at `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:11-20`
	and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:120-127`.
	The fixture constructor did not change, so broad constructor churn is not
	supported by the diff.
* SQL integration tests share the existing nonparallel `SQL Server` collection
	at
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:10-12`
	and
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:124-127`.
	No new parallel shared-state defect was found.
* The new feature fakes and bUnit contexts are per-test. No static mutable test
	state was found.
* Gateway and Web HTTP messages are disposed through `using` declarations in
	production. No direct response disposal leak was found.
* The intended SQL keyset query was not exercised with a non-null cursor in the
	new SQL integration tests, so SQL Server translation and equal-timestamp
	paging remain unproved.
* API authorization is attached to all three new routes, but tests inspect
	helpers rather than endpoint metadata or authenticated requests. The plan's
	policy coverage claim is therefore unsupported.
* The plan marks Phase 5 and its full validation step complete at
	`.copilot-tracking/plans/2026-08-17/trailing-stops-features-plan.instructions.md:91-100`,
	while the log records unresolved distributed startup failures at
	`.copilot-tracking/plans/logs/2026-08-17/trailing-stops-features-log.md:68-70`.
	The completion state is inaccurate even before the defects above are fixed.

## Focused Validation

* `dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-restore`
	passed. It produced the pre-existing `NU1903` warning for
	`Microsoft.OpenApi` 2.0.0 in API and ServiceDefaults.
* Forty focused new tests across Application, API, Infrastructure gateway/store,
	and Web passed. Their passing state demonstrates the noted mock and coverage
	gaps rather than disproving the defects.
* A local API startup smoke check stopped before endpoint construction because
	shared Data Protection requires a configured `platformdb` SQL connection at
	`src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:71`. No Docker-backed
	or full-solution run was repeated.
* EF migration listing was inconclusive because the repository's available
	design-time startup attempted an unavailable SQL connection. Static migration
	metadata and source were used instead.

## Recommended Remediation Sequence

1. Repair the composition root first: register the three handlers and validator
	 in `AddPlatformApplication`, then add one route-graph or service-resolution
	 test that constructs the real API registrations. This removes the likely
	 startup blocker before behavioral work.
2. Correct the IG JSON property to `trailingStopsEnabled` and update the gateway
	 tests from the official contract. Classify a malformed successful PUT
	 acknowledgement as uncertain so the handler performs authoritative GET
	 reconciliation.
3. Make update confirmation explicit. Return confirmed success only when GET
	 matches the requested Boolean; represent a successful reconciliation that
	 disagrees as a typed not-applied or reconciled outcome, and make Web feedback
	 truthful.
4. Repair failure telemetry through the real handler paths. Remove the optional
	 event-store escape hatch, emit bounded events for each failed or indeterminate
	 provider operation, and test handler invocation rather than the helper.
5. Regenerate one coherent EF migration using the repository's normal migration
	 workflow. Remove `_SnapshotRepair`, remove duplicate model configuration,
	 ensure the intended migration has designer metadata, and assert its migration
	 identifier in SQL integration coverage.
6. Partition history reads by current platform and broker environment, define
	 and validate one cursor format at the API/Application boundary, and add a
	 SQL Server test for a non-null cursor with equal timestamps.
7. Enforce both application Test status and legacy Demo provider-key
	 compatibility. Use the presentation mapper in API output so new Web surfaces
	 never render Demo.
8. Either bind and validate the dedicated retention setting or remove it and
	 document the fixed policy. Tests must prove invalid, zero, negative, and
	 configured values according to the selected contract.
9. Add component-owned cancellation for load/save/page operations and one
	 disposal test. Then run focused projects, Docker-backed SQL integration,
	 route/auth integration, solution build, and the relevant broader suite.
10. Only after those gates pass, correct the plan checklist and log to distinguish
		completed validation from known AppHost or provider-verification blockers.

## Gaps And Clarifying Questions

* Should a confirmed GET that differs from the requested Boolean return a
	conflict/not-applied response, or a successful reconciled response with an
	explicit `Applied = false` contract? The current generic success is unsafe.
* Is `Retention:TrailingStopsPreferenceObservationsDays` intended to be
	operator-configurable, or should the approved 90-day policy remain a fixed
	product invariant? The implementation and configuration currently disagree.
* Should history be partitioned by both `PlatformEnvironment` and legacy
	`BrokerEnvironment`, and should `AccountId` also participate once the gateway
	exposes it? The schema stores all three concepts, but the reader ignores them.
* Real Test-account endpoint availability and allowance behavior remain
	intentionally unverified, as already recorded by DR-03. No automated test
	should resolve that question against live IG.

## Recommended Next Research Not Completed

* [ ] Capture non-secret manual evidence for the configured IG Test account's
	`GET` and `PUT /accounts/preferences` behavior and allowance codes
* [ ] Diagnose the pre-existing AppHost/service-discovery startup failures in a
	separate research task after this feature's local composition defects are fixed
* [ ] Confirm the business response semantics for a reconciled value that does
	not match the requested update
* [ ] Confirm whether retention is fixed policy or validated operator configuration
