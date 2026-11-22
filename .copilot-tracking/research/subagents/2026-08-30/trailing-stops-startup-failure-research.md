<!-- markdownlint-disable-file -->
# Trailing Stops Startup Failure Research

## Research Questions

* What is the established AppHost startup command and which prerequisites does it require?
* What concrete failure prevents the application from running after the trailing-stops implementation?
* Is the failure a code regression, a missing local prerequisite, stale database state, or a combination?
* Which solution alternatives and focused checks can verify the diagnosis without applying a fix?

## Scope And Constraints

* Research only
* No product, test, configuration, database, or infrastructure fixes
* Runtime processes and containers started for reproduction must be stopped cleanly

## Commands And Evidence

### Established Startup Path And Prerequisites

The repository requires the SDK selected by `global.json` and Docker Desktop.
The supported composition root starts SQL Server, `platformdb`, Mailpit, and
Keycloak. Evidence:

* `global.json:2-6` pins SDK `10.0.200` with `latestFeature` roll-forward. The
	resolved SDK was `10.0.400-preview.0.26322.102`.
* `docs/wiki/local-development.md:5-23` requires the pinned .NET SDK and Docker
	Desktop, and identifies SQL Server, `platformdb`, Mailpit, and Keycloak as the
	supported local topology.
* `docs/wiki/local-development.md:94-105` gives the established command:

	```powershell
	dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj
	```

	The optional alternative is `aspire run` when the Aspire CLI is installed.
* `src/TNC.Trading.Platform.AppHost/AppHost.cs:1-14` is the thin composition
	root.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:13-37`
	declares persistent SQL, `platformdb`, persistent Mailpit, and Keycloak with
	stable port 8080 and realm import.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:13-41` gives
	API a `platformdb` reference, waits for SQL and Mailpit, and makes Web wait for
	API.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:33-58` selects
	Keycloak for API unless the explicit test override is selected.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:154-188` makes
	API and Web wait for Keycloak in the normal provider path.

### Initial Machine And Runtime State

Read-only inspection before reproduction found:

* Docker daemon available, server version `29.5.3`
* No running TNC Trading AppHost, API, Web, or DCP application process
* Existing persistent resources:
	* `sql-1d2c2e33`, running for approximately 22 hours
	* `mailpit-1d2c2e33`, running and healthy for approximately 22 hours
	* `keycloak-1d2c2e33`, running on host port 8080
* SQL, Mailpit, and Keycloak therefore existed before this reproduction. Docker
	availability and missing infrastructure were not blockers.

The worktree contained the uncommitted trailing-stops implementation. No user
changes were reverted or modified.

### AppHost Reproduction

The documented command built API, Web, ServiceDefaults, and AppHost
successfully. Aspire 13.2.3 started and exposed its dashboard at a dynamic HTTPS
port. Live process inspection then found:

* AppHost and DCP were running.
* Web was running but had no usable application listener because it waits for
	API.
* No API process remained.
* SQL, Mailpit, and Keycloak remained running.

This localizes the failure to API startup after successful AppHost composition
and infrastructure startup. The AppHost terminal did not forward the child API
exception.

The AppHost terminal was terminated after evidence capture. Its DCP controller
and Web child remained orphaned, so the specific captured DCP root was stopped.
Final inspection reported no TNC Trading AppHost, API, Web, or related DCP
processes. Persistent infrastructure was deliberately retained, matching
`ContainerLifetime.Persistent` in
`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:16-26`.

### Direct API Failure Capture

To expose the child exception, the API was run directly against the unchanged
Aspire SQL container. The connection used the repository fixture's working
shape (`127.0.0.1`, published SQL port, `Encrypt=False`) and obtained the SA
password in memory from Docker metadata without printing it. Test auth removed
Keycloak from this diagnostic run as a confounder.

The API completed SQL startup initialization far enough to build the HTTP host,
then exited with code 1:

```text
System.InvalidOperationException: Body was inferred but the method does not allow inferred body parameters.
Parameter | Source
handler   | Body (Inferred)
Did you mean to register the "Body (Inferred)" parameter(s) as a Service or apply the [FromServices] or [FromBody] attribute?
```

The stack starts in `RequestDelegateFactory.CreateArguments`, proceeds through
route endpoint construction and authorization middleware, and ends at
`src/TNC.Trading.Platform.Api/Program.cs:84`. The retention service's
`TaskCanceledException` occurs after host startup fails and shutdown begins; it
is secondary cancellation noise, not the initiating failure.

The direct API process exited on its own. Final process inspection found no TNC
Trading or Aspire application processes.

### Dependency Injection And Route Evidence

The new routes require four application services:

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-60`
	maps the three account-preferences routes.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:130-146`
	requires `GetAccountPreferencesHandler`,
	`UpdateAccountPreferencesValidator`, `UpdateAccountPreferencesHandler`, and
	`GetTrailingStopsPreferenceObservationsHandler` as route parameters.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:18-46`
	explicitly registers existing application services but none of those four
	new types.
* `src/TNC.Trading.Platform.Api/Program.cs:20-21` invokes that application
	registration and Infrastructure registration before building the app.

ASP.NET Core therefore does not recognize the unregistered complex `handler`
parameter as a service. On the first GET route, request-delegate inference
treats it as a request body, which GET does not allow, producing the reproduced
exception.

Infrastructure registration is not missing:

* `src/TNC.Trading.Platform.Api/Program.cs:9-21` imports and invokes
	`TNC.Trading.Platform.Infrastructure.DependencyInjection`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:91-108`
	registers the EF observation store, its application port, and the typed IG
	account-preferences gateway.

### Configuration, SQL, And Migration Evidence

The API invokes the blocking startup initializer before mapping endpoints at
`src/TNC.Trading.Platform.Api/Program.cs:24-37`. SQL startup runs
`MigrateAsync` and wraps schema failures at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:39-64`.
The direct API reached later HTTP host construction, so SQL migration was not
the initiating failure in this database state.

The persistent database contained these applied migrations:

```text
20260727202238_InitialPlatformSchema
20260728093000_AddSharedDataProtectionKeys
20260728094818_AddDurableIgProofData
20260807103334_AddAccountDetailsSnapshots
20260830112227_SnapshotRepair
```

`TrailingStopsPreferenceObservations` existed with its primary key and both
broker/time index shapes. The migration state is nevertheless incoherent:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:12-16`
	gives `SnapshotRepair` valid DbContext and migration metadata.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs:11-54`
	creates the trailing-stops table and two indexes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs:8-36`
	also creates the same table, but it has no generated designer partial and no
	`[Migration]` metadata. EF does not discover it as a migration.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:1-27`
	has no compile exclusion for `_SnapshotRepair`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:150-162`
	and
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:179-184`
	configure the same entity twice and generate two index shapes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs:673-730`
	records both indexes.

An in-container SQL probe of the intended migration's first `CREATE TABLE`
returned SQL Server error 2714:

```text
There is already an object named 'TrailingStopsPreferenceObservations' in the database.
```

The failed statement made no state change. Migration history still contained
only `20260830112227_SnapshotRepair` for this feature, and the table count
remained one. This proves that making the later migration discoverable without
first reconciling the duplicate artifact and applied history would introduce a
schema failure. It does not mean error 2714 is today's initiating API failure,
because the later file is currently undiscoverable.

The feature SQL test at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:14-26`
asserts only that some migration creates the table and desired index. It does
not assert the intended migration identifier, so `SnapshotRepair` can satisfy
it. The broader migration test's expected table list and applied-ID assertion
at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:68-90`
were not extended for the new feature.

### Validation Attempt Notes

* The AppHost build completed successfully. Package vulnerability warnings did
	not stop the build.
* A direct API attempt using `localhost` and encrypted SQL timed out at the
	host-published port. Retrying with the fixture's established `127.0.0.1` and
	`Encrypt=False` shape connected and exposed the route failure. The first
	timeout is diagnostic-command configuration, not an AppHost prerequisite
	failure.
* A requested focused test run through the editor test tool unexpectedly ran
	broader AppHost-backed suites and reported 15 failures, including API URL
	discovery timeouts. It was not treated as focused migration evidence. The
	session Keycloak container it created was stopped and removed; no test-created
	application process remained.

## Findings

1. The immediate application startup regression is missing API application
	 service registrations for the new account-preferences routes.
2. AppHost, SDK selection, Docker, SQL, Mailpit, and Keycloak startup are not the
	 primary failure in the reproduced environment.
3. Existing SQL state is not the immediate blocker. The API passes blocking
	 migration/initialization and fails later while constructing endpoints.
	 Resetting SQL alone would not register the missing route services.
4. The feature's migration artifacts are independently defective. The generic
	 `SnapshotRepair` migration is the only discoverable migration, while the
	 intended named migration is an undiscoverable duplicate.
5. Keycloak is not the immediate blocker. It was running in the AppHost path,
	 and the same route failure reproduced with test auth after SQL initialization.
6. Infrastructure DI for the observation store and gateway exists. The missing
	 registrations are specifically in the API-owned application composition
	 list.
7. The completed plan status overstates validation. The execution log already
	 records unresolved distributed startup failures at
	 `.copilot-tracking/plans/logs/2026-08-17/trailing-stops-features-log.md:68-70`.

## Root Causes And Confidence

### Primary Root Cause: Missing Application DI Registrations

Confidence: **Very high**.

The route signatures require four new services, the explicit registration list
contains none of them, and the direct API run produces ASP.NET Core's exact
unregistered-complex-parameter inference error. This is a code regression from
the trailing-stops implementation.

### Secondary Root Cause: Incoherent EF Migration Artifacts

Confidence: **High**.

The metadata-bearing `SnapshotRepair` is applied and owns the current table.
The intended named migration duplicates the table but lacks metadata and is not
discoverable. This does not initiate the current startup exception, but it
invalidates the intended migration identity, masks validation, and makes a
naive repair capable of causing SQL error 2714 against existing state.

### Missing Local Prerequisites

Confidence that this is not the primary cause: **High**.

The required SDK resolved, Docker was available, all three infrastructure
containers ran, AppHost built and started, SQL initialization completed, and
the failure reproduced without Keycloak-dependent API auth.

### Stale Database State

Confidence that this is not the primary cause: **High**.

The database is persistent and contains the accidentally applied
`SnapshotRepair`, so its history must be considered during remediation.
However, the API progresses past `MigrateAsync` and fails during endpoint
construction. A disposable reset may simplify migration cleanup, but it cannot
fix the missing service registrations.

## Solution Alternatives

### Alternative 1: Repair Composition And Regenerate One Canonical Migration

Preferred when the trailing-stops migration has not reached any shared or
production database.

* Register the three handlers and validator in the API application composition
	root, following the neighboring explicit registrations.
* Add a route-graph or service-resolution startup test that builds the real API
	registration set.
* Remove the accidental `_SnapshotRepair` artifact and the undiscoverable manual
	duplicate, consolidate the duplicate DbContext mapping, and generate one
	canonical `AddTrailingStopsPreferenceObservations` migration with designer
	metadata from the corrected model.
* Reset only disposable local `platformdb` state before validating the newly
	generated sequence. Preserve production/shared data unless an approved
	migration transition exists.

This gives the cleanest source history but requires acknowledging that the
current local `SnapshotRepair` migration record will disappear with the
disposable database reset.

### Alternative 2: Preserve Applied Migration Identity And Add A Corrective Successor

Preferred if `20260830112227_SnapshotRepair` may exist outside disposable local
state.

* Repair application DI first.
* Treat `SnapshotRepair` as an immutable applied migration, remove or exclude
	the undiscoverable duplicate file, and add a later corrective migration only
	for genuine model/schema differences.
* Remove duplicate model configuration and verify the resulting snapshot
	against the already-applied schema.

This avoids rewriting applied history but permanently retains a poorly named
migration and requires careful model-difference validation.

### Alternative 3: Annotate The Existing Named Migration Without Reconciling History

Reject.

Adding designer metadata to
`20260830120000_AddTrailingStopsPreferenceObservations` while leaving
`SnapshotRepair` applied causes the later migration to execute its duplicate
`CREATE TABLE`, which the SQL probe proves fails with error 2714.

### Alternative 4: Reset SQL Only

Reject as a complete solution.

A reset might alter which migration artifact creates the table, but the API
will still fail endpoint construction because application services remain
unregistered. It also destroys local Data Protection keys, protected IG
credentials, history, and configuration, as described by
`docs/wiki/local-development.md:146-164`.

## Focused Verification Steps

After a separate implementation task applies an approved fix:

1. Build the API and AppHost projects.
2. Add and run a focused API composition test that calls the real
	 `AddPlatformApplication` registration, maps `MapPlatformEndpoints`, builds
	 endpoint metadata, and resolves all four account-preferences services.
3. Run the API against SQL and verify the host reaches a listening state with no
	 `Body (Inferred)` exception.
4. List EF migrations and assert exactly one discoverable trailing-stops schema
	 migration with the approved identifier.
5. On an empty temporary SQL database, run `MigrateAsync`, assert the feature
	 migration ID is applied, and assert the table plus approved indexes exist.
6. On a database representing the supported pre-feature state, run
	 `MigrateAsync` and assert an in-place upgrade with no data loss.
7. If applied `SnapshotRepair` databases must be supported, create that exact
	 history/schema state and prove the corrective sequence is idempotent and does
	 not issue a duplicate `CREATE TABLE`.
8. Run the documented AppHost command with Docker available. Verify API and Web
	 both expose listeners, readiness is healthy, and normal Keycloak sign-in is
	 reachable.
9. Stop AppHost and verify no API, Web, AppHost, or session DCP processes and no
	 session-only test containers remain.
10. Run the focused Infrastructure SQL command specified at
		`.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:187-205`,
		then the relevant API/AppHost integration smoke before broader solution
		tests.

## Remaining Gaps

* No product fix was applied, by request.
* The editor test tool did not honor the requested single SQL test file; the
	clean-database migration test was therefore not obtained as an isolated run
	in this session.
* The current work is uncommitted. It is not known whether
	`20260830112227_SnapshotRepair` has been applied to any shared, CI, test, or
	deployed database outside this local persistent volume.
* Real IG Test account endpoint behavior was not exercised. It is downstream of
	startup and irrelevant to the reproduced host failure.
* Other behavioral defects identified by the same-day static audit, including
	the provider JSON property name and confirmation semantics, were outside this
	startup-focused reproduction.

## Clarifying Questions

* Has `20260830112227_SnapshotRepair` reached any non-disposable database, or is
	it confined to this uncommitted local implementation?
* Is local `platformdb` disposable for the eventual migration repair, including
	its Data Protection keys, protected IG credentials, configuration, and
	history?
* Should the final canonical migration retain the currently generated
	broker-only index as well as the broker-plus-platform index, or was the first
	index an accidental consequence of duplicate model configuration?