<!-- markdownlint-disable-file -->
# Task Research: Failing Tests Root Cause

Five tests fail in the current worktree. They form four independent causal
clusters rather than one production regression. Four failures come from test
fixtures or assertions that no longer represent the executing contract. The
migration cluster also exposes two conflicting unpublished migration artifacts
that must be reconciled before the feature is committed or deployed.

## Task Implementation Requests

* Reproduce and inventory the failing tests
* Identify the causal code, configuration, or test assumptions behind each
  failure cluster
* Recommend one remediation strategy, including validation order and impact

## Scope and Success Criteria

* Scope: Current repository test failures, their relationship to recent
  Account Preferences work, and remediation options. Production and test code
  changes are excluded from this research task.
* Assumptions: The dirty worktree is the state under investigation. Git recency
  is not treated as causality without executing-path evidence.
* Success Criteria:
  * Reproduce every failure with exact command evidence.
  * Separate environment failures from behavior and fixture failures.
  * Group symptoms by shared root cause.
  * Trace each cluster to controlling code with line references.
  * Evaluate alternatives and select one remediation sequence.

## Executive Finding

The repository-root gate produced 515 tests: 510 passed, 5 failed, and 0 were
skipped. Docker, SQL Server, Keycloak, AppHost startup, API integration tests,
API unit tests, and Infrastructure unit tests all passed. The failures are not
caused by unavailable infrastructure.

The five failures divide into these four clusters:

| Cluster | Failed tests | Root cause | Confidence |
|---------|-------------:|------------|------------|
| Application pagination | 1 | The fake returns rows but always supplies a null cursor | High |
| Web disposal | 1 | The fake ignores cancellation and the assertion races completion | High |
| SQL GUID paging | 1 | The test applies .NET ordering to SQL Server `uniqueidentifier` values | High |
| EF migration recovery | 2 | Legacy fixtures assume four migrations and omit the new table | High |

The recommended strategy is to correct the four test contracts and regenerate
one clean migration, but migration cleanup is conditional. First query every
non-disposable `__EFMigrationsHistory` and inspect released artifacts. Never
delete or rename a migration that has been applied to a shared database.

## Research Executed

### Git and Worktree Analysis

The branch is `012-add-account-details`, tracking its remote. There are no
staged changes. The worktree contains 27 modified tracked files and 42
untracked files spanning the new Account Preferences slice. Both candidate
migrations and the affected Account Preferences tests are untracked.

Recent commit `ac8d6f3` changes Account Details test time setup. No current
failure traces to that change. The full Application project passes except for
the untracked Account Preferences cursor test.

Commands used:

```powershell
git status --short --branch
git log -12 --date=iso-strict --pretty=format:"%h%x09%ad%x09%an%x09%s"
git diff --stat
git diff --name-only
git log --all --oneline -- src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations
git blame -L 282,323 -L 330,373 -- test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs
```

### Test Baseline

Focused project results:

| Project | Total | Passed | Failed | Classification |
|---------|------:|-------:|-------:|----------------|
| Application unit | 128 | 127 | 1 | Fake contract |
| API unit | 57 | 57 | 0 | Passing boundary |
| Infrastructure unit | 112 | 112 | 0 | Passing boundary |
| Web unit | 107 | 106 | 1 | Async test timing |
| Infrastructure integration | 22 | 19 | 3 | SQL and migration assertions |

Commands used:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --verbosity minimal
dotnet test --no-restore --verbosity minimal
```

The broad command completed in 134.9 seconds with the same five failures. The
AppHost-backed API integration project passed with live HTTP interactions.
Build warnings include package advisories and existing analyzer warnings, but
none caused a test failure.

### EF Migration Analysis

These commands used the Infrastructure project as both target and startup
project because it contains the design-time factory and EF Design package:

```powershell
dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --no-connect
dotnet ef migrations has-pending-model-changes --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj
dotnet ef migrations script 20260807103334_AddAccountDetailsSnapshots 20260830112227_SnapshotRepair --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --no-transactions
```

EF discovers five migrations, ending in
`20260830112227_SnapshotRepair`. The pending-model command reports no changes.
The generated SQL creates the observations table, creates two indexes, and
records only the `SnapshotRepair` history row.

The current model and snapshot contain only the four-column partition index at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:150-160`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs:673-725`.
The `SnapshotRepair` designer contains that index plus an extra broker-only
index at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:724-730`.
The pending-model check does not catch differences between the latest migration
designer and the snapshot.

### External Research

* EF Core migration management states that a scaffolded migration comprises
  the main migration, designer metadata, and model snapshot. It warns against
  removing a migration applied to shared or production databases.
  Source: [Managing Migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing)
* SQL Server states that `uniqueidentifier` ordering is not implemented by
  comparing raw bit patterns.
  Source: [uniqueidentifier](https://learn.microsoft.com/sql/t-sql/data-types/uniqueidentifier-transact-sql)
* .NET documents the component-by-component `Guid.CompareTo` algorithm.
  Source: [Guid.CompareTo](https://learn.microsoft.com/dotnet/api/system.guid.compareto)

### Project Conventions

* `.github/instructions/tests.instructions.md:27-29` prefers isolated unit
  tests before higher-level suites and line 92 defines `dotnet test` as the
  repository gate.
* `.github/instructions/aspire-tests.instructions.md:21-40` requires closed-box
  AppHost-backed tests and resource cleanup.
* `.github/instructions/architecture.instructions.md:50-70` places business
  decisions in policy and use-case code, independently testable where possible.
* `docs/wiki/testing-and-quality.md:45-53` documents project-scoped test runs as
  the focused feedback loop.
* `global.json:1-6` pins SDK `10.0.200` with `latestFeature` roll-forward.

## Root-Cause Analysis

### Application Pagination Metadata

Failing test:

`AccountPreferencesHandlerTests.GetTrailingStopsPreferenceObservations_ShouldReturnDeterministicPageCursor_WhenPageIsFull`

The assertion expects cursor
`00000000000000000000000000000002` but receives null. The handler forwards
the page contract unchanged at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:7-15`.
The contract owns observations, cursor, and invalid-cursor state at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs:5-11`.

The test supplies only `ListResult`, while its fake always creates a page with
a null cursor at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:137-152`
and lines 186-198. The fake does not model the store contract it replaces.

Disconfirming check: configure a complete fake page with a non-null cursor and
rerun only this test. A continued null result would implicate handler mapping.

### Web Disposal Synchronization

Failing test:

`AccountPreferencesTests.Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError`

The component cancels its owned token during disposal at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:28-44`.
The presenter catches matching cancellation and clears `IsLoading` in `finally`
at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:10-27`.
The API client passes the token to HTTP send and JSON reading at
`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:130-138` and lines 161-170.

The test disposes the component, releases an uncancellable content gate, awaits
an unrelated completed dispatcher task, and immediately asserts idle state at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:70-87`.
Its delayed content ignores cancellation at lines 150-164. The assertion can
run before the HTTP continuation and presenter `finally` block. The test failed
10 out of 10 repeated isolated runs, confirming a deterministic harness flaw
rather than intermittent infrastructure.

Disconfirming check: make the fake HTTP operation observe cancellation, expose
a cancellation-completion task, await it, and eventually assert idle state. If
loading remains true after observed completion, investigate production task
ownership.

### SQL GUID Keyset Ordering

Failing test:

`AccountPreferencesSqlIntegrationTests.ListAsync_ShouldPageEqualTimestampsDeterministically_WhenRowsShareTimestamp`

The store orders and filters equal timestamps with SQL Server
`uniqueidentifier` comparison at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-56`.
The test constructs its expected order with .NET `Guid` comparison at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:63-83`.

The focused run returned the same IDs in the reverse of .NET order. The query
and keyset predicate use the same database ordering, so the failure does not
demonstrate a missing or duplicated page boundary.

Disconfirming check: query expected IDs using SQL Server's actual `ORDER BY`,
then compare that result with the concatenated keyset pages and assert no
duplicates or omissions.

### EF Migration Lineage and Recovery Fixtures

Failing tests:

* `PlatformSqlServerIntegrationTests.MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned`
* `PlatformSqlServerIntegrationTests.InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject`

The first test uses `EnsureCreatedAsync`, which creates the current model, then
drops several post-initial tables but leaves
`TrailingStopsPreferenceObservations`. Applying the new migration attempts to
create the retained table again. The setup is at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:282-323`.

The second recovery path succeeds and applies all discovered migrations, but
the test hard-codes an expected count of four at the same file's lines 330-373.
Five migrations are now discoverable.

`SnapshotRepair` is executable because its designer declares
`[Migration("20260830112227_SnapshotRepair")]` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:12-23`.
Its main file creates the new table and two indexes at lines 10-48.

`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs:1-39`
is inert. It lacks a designer file and generated `[Migration]` metadata, so EF
does not discover or apply it. Keeping both artifacts creates false confidence
and an inconsistent lineage.

Disconfirming check: after selecting one canonical migration, compare
`GetMigrationsAsync()` with `GetAppliedMigrationsAsync()`, require no pending
migrations, and rerun both recovery scenarios.

## Technical Scenarios

### Canonical Migration

Requirements:

* Preserve any migration already applied outside disposable local databases
* Keep main migration, designer metadata, and snapshot synchronized
* Avoid duplicate or undiscoverable migration artifacts
* Retain only indexes represented by the intended model or justified by data

Preferred approach: first inspect every shared, staging, and production
`__EFMigrationsHistory` plus released binaries. If neither candidate was
deployed, remove both candidates through the EF migration workflow and
scaffold one clean `AddTrailingStopsPreferenceObservations` migration in the
established migrations directory. Review its main file, designer, snapshot,
discovery list, pending-model check, and SQL script as one unit.

If `SnapshotRepair` was applied anywhere shared, preserve its ID and historical
code. Remove the inert duplicate only after checking released artifacts, then
add a forward corrective migration if the extra index should be dropped. If
the inert migration ID appears in any history table, recover its exact released
migration triplet before proceeding.

Considered alternatives:

* Retain `SnapshotRepair` unchanged for an unpublished lineage. Rejected
  because its accidental name and extra index disagree with the current model.
* Add metadata manually to the inert migration. Rejected because deterministic
  EF scaffolding is safer than reconstructing generated artifacts by hand.
* Delete or rename migrations before checking deployment history. Rejected
  because it can irreparably fork schema history.

### Web Disposal Contract

Requirements:

* Prove disposal propagates cancellation
* Avoid post-disposal rendering or user-visible errors
* Synchronize assertions with operation completion

Preferred approach: use a cancellation-aware fake transport with an explicit
`CancellationObserved` task. Dispose the component, await that signal, and use
an eventual assertion for `IsLoading == false`. Retain the null-error assertion
and add an explicit cancellation-observed assertion.

Considered alternatives:

* Wait for the existing uncancellable gate. Rejected because this proves normal
  eventual completion, not cancellation.
* Convert production disposal to asynchronous disposal and await presenter
  work. Rejected as disproportionate because current production code forwards
  cancellation correctly and no task handle exists. It could also hang on a
  dependency that ignores cancellation.

### SQL Keyset Contract

Requirements:

* Preserve deterministic page order and boundary continuity
* Avoid loading a full partition into application memory
* Match the supported SQL Server provider contract

Preferred approach: seed fixed GUIDs known to sort differently in SQL Server
and .NET. Obtain expected IDs with SQL Server's `ORDER BY`, then assert that
concatenated pages match exactly with no duplicates or omissions. Keep the
provider query and cursor unchanged.

Considered alternatives:

* Add a provider-neutral monotonic key. Rejected for the current requirement
  because it adds schema, index, migration, cursor-versioning, and API impact.
  Reconsider only if cross-provider or chronological tie-break ordering becomes
  a product requirement.
* Force .NET ordering or client evaluation. Rejected because it defeats indexed
  keyset paging and materializes the partition before pagination.

### Fixture Corrections

Requirements:

* Preserve behavioral assertions while removing stale implementation counts
* Recreate historical schema states directly rather than subtracting current
  tables from `EnsureCreatedAsync`
* Model complete operation contracts in fakes

Preferred approach:

1. Replace the Application fake's rows-only result with a complete page result
   and assert forwarding of observations, cursor, and invalid-cursor state.
2. Build the no-history database by migrating to
   `20260727202238_InitialPlatformSchema`, seed preserved data, and recreate the
   guarded baseline history state. Avoid maintaining a growing table-drop list.
3. Replace the numeric migration count with exact sequence equality between
   discovered and applied migrations plus an empty pending set.

Changing the expected count from four to five is rejected. It will become
stale at the next migration and does not prove which migrations ran.

## Selected Remediation Strategy

Apply the remediation in this order:

1. Query shared and deployed migration histories and inspect released
   artifacts for both candidate migration IDs. Stop migration cleanup until
   this is resolved.
2. Resolve the migration lineage using the unpublished or deployed branch
   described above. Validate migration discovery, pending model changes, and
   generated SQL before modifying recovery tests.
3. Correct the no-history fixture and replace the hard-coded migration count.
   Run the two filtered recovery tests and migration create/rerun tests.
4. Correct the Application fake to return the full page contract. Run the
   cursor-forwarding test, then the Application unit project.
5. Make the Web fake cancellation-aware and synchronize on observed
   cancellation and eventual idle state. Run the disposal test repeatedly,
   then the Web unit project.
6. Assert SQL GUID order and page continuity. Run the equal-timestamp test,
   all Account Preferences SQL tests, then the Infrastructure integration
   project.
7. Run `dotnet test --no-restore --verbosity minimal` as the repository gate.

This order isolates the highest-risk decision first. The Application, Web, and
GUID changes are test-only corrections. The migration cleanup may affect
schema history and must not proceed on assumption.

## Complete Validation Commands

```powershell
dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --no-connect
dotnet ef migrations has-pending-model-changes --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~GetTrailingStopsPreferenceObservations_ShouldReturnDeterministicPageCursor_WhenPageIsFull"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ListAsync_ShouldPageEqualTimestampsDeterministically_WhenRowsShareTimestamp|FullyQualifiedName~MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned|FullyQualifiedName~InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject"
dotnet test --no-restore --verbosity minimal
```

## Risks and Open Decisions

The only blocking decision is migration deployment history. Repository state
cannot prove whether a dirty local build or unpublished artifact reached a
shared database. Confirm these items before implementation:

* Distinct `MigrationId` values in every non-disposable database
* Migration classes and metadata embedded in released builds since Account
  Preferences work began
* Whether the extra broker-only index has an intentional operational query

The Web and paging recommendations require reconsideration only if disposal
must synchronously guarantee idle state or provider-neutral GUID ordering is a
stated product requirement.

## Potential Next Research

* Inspect shared and deployed migration histories
  * Reasoning: This determines whether migration artifacts may be regenerated
    or must be preserved and corrected forward.
  * Reference: `__EFMigrationsHistory` in each non-disposable database and
    released Infrastructure assemblies.
* Examine query telemetry or execution plans for the broker-only index
  * Reasoning: No current model query justifies retaining the extra index.
  * Reference:
    `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:724-730`

## Evidence Documents

* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-baseline-and-causality-research.md`
* `.copilot-tracking/research/subagents/2026-08-30/failing-tests-remediation-alternatives-research.md`
