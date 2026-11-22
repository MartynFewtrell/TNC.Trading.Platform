---
title: Failing Tests Remediation Alternatives Research
description: Independent evaluation of Phase 2 remediation alternatives for the five failing tests
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Research status

Complete. The five failing tests were reproduced and traced. Repository evidence
supports one preferred remediation strategy, conditional on deployment-history
confirmation for the migration lineage.

## Research questions

* Do the five baseline failures match the current files, Git state and history, generated EF migration metadata, model snapshot, and focused behavior?
* Which migration artifact is canonical, discoverable, and safe to retain without rewriting deployed migration history?
* Which Web disposal remediation best matches bUnit, component, presenter, and transport ownership semantics?
* Which GUID keyset paging contract preserves deterministic SQL Server page boundaries at proportionate cost?
* Which fixture changes correct invalid setup, and which changes would weaken assertions?
* What implementation order minimizes risk and provides the fastest falsification points?

## Evidence ledger

The baseline's symptoms are accurate. Its terminology needs one correction:
there are five failing tests grouped into four causal clusters. This research
treats the two migration failures as subclusters 4a and 4b.

Git evidence:

```powershell
git status --short
git log -12 --oneline --decorate
git log --all --oneline -- src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations
git diff -- src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs
git blame -L 282,323 -L 330,373 -- test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs
```

The Account Preferences source, tests, and both migration candidates are
untracked. The model snapshot is modified. Neither migration appears in Git
history; committed migration history ends with
`20260807103334_AddAccountDetailsSnapshots`. The no-history fixture originated
in `8df68d8`; Account Details table cleanup and the four-migration count came
from `8863cd9`. This proves worktree causality, but not whether a dirty build or
an unpublished artifact reached a database.

Focused behavior evidence:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~GetTrailingStopsPreferenceObservations_ShouldReturnDeterministicPageCursor_WhenPageIsFull" --verbosity minimal
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError" --verbosity minimal
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ListAsync_ShouldPageEqualTimestampsDeterministically_WhenRowsShareTimestamp|FullyQualifiedName~MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned|FullyQualifiedName~InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject" --verbosity minimal
```

Results were 0/1, 0/1, and 0/3 passed respectively. The Web test also failed
10/10 repeated `--no-build --no-restore` runs. The SQL run produced the same
GUID-order reversal, duplicate-table exception, and expected-four/actual-five
migration count as the baseline.

EF discovery and model evidence:

```powershell
dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --no-connect
dotnet ef migrations has-pending-model-changes --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj
dotnet ef migrations script 20260807103334_AddAccountDetailsSnapshots 20260830112227_SnapshotRepair --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --no-transactions
```

The design-time factory and Design package are available at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContextFactory.cs:1-18`
and `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:18-25`.
Using Infrastructure as both target and startup project lists five migrations,
ending at `20260830112227_SnapshotRepair`. The pending-model check reports no
changes. Generated SQL creates the table, both indexes, and only the
`SnapshotRepair` history row.

Official EF guidance states that a scaffolded migration consists of the main
file, its `.Designer.cs` metadata file, and the model snapshot. It also warns
not to remove a migration applied to a shared or production database:
[Managing Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing).
SQL Server documents that `uniqueidentifier` ordering does not compare raw bit
patterns, while .NET documents its component-by-component `Guid.CompareTo`
algorithm: [uniqueidentifier](https://learn.microsoft.com/en-us/sql/t-sql/data-types/uniqueidentifier-transact-sql)
and [Guid.CompareTo](https://learn.microsoft.com/en-us/dotnet/api/system.guid.compareto).

## Failure cluster assessment

### Application fake pagination metadata

The handler forwards all three page fields without deriving a cursor at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:7-15`.
The contract explicitly owns `Observations`, `NextCursor`, and
`HasInvalidCursor` at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs:5-11`.
The test supplies only rows at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:137-152`,
while its fake always returns a null cursor at the same file's lines 186-198.
The failure is a fixture-contract error, not a handler defect.

### Web disposal ownership and synchronization

The component owns and synchronously cancels its request lifetime at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:28-44`.
The presenter catches cancellation and clears `IsLoading` in `finally` at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:10-27`.
The API client propagates the token through `SendAsync` and JSON reading at
`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:130-138` and lines 161-170.

The test disposes the bUnit component, releases an uncancellable content gate,
then awaits an unrelated completed dispatcher task at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:70-87`.
Its delayed content overrides only the overload without a cancellation token at
lines 150-164. The shared fake handler returns responses synchronously at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/SequencedHttpMessageHandler.cs:13-35`.
Component disposal therefore requests cancellation but does not await the
renderer-owned initialization task, and the fake prevents cancellation from
ending content serialization. The immediate assertion races the presenter's
`finally` deterministically.

### SQL GUID keyset ordering

The store orders and filters on SQL Server's `uniqueidentifier` comparison at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-56`.
The test compares the two pages to .NET `Guid` ordering at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:63-83`.
The focused run returned SQL order
`252995d8-9f6e-47db-9931-e4ed2526667b`, then
`a8774d3f-b0e1-4de6-927b-8ef7e3447cb1`; .NET expected the reverse. The query
and keyset predicate use the same provider ordering, so the observed failure
does not show a missing or duplicate page boundary.

### Migration lineage and recovery fixtures

`SnapshotRepair` is technically valid and discoverable. Its generated metadata
contains `[Migration("20260830112227_SnapshotRepair")]` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:12-23`.
Its `Up` creates the table and two indexes at the matching main file's lines
10-48. The designer target model also has both indexes at lines 724-730.

It is not a clean final lineage. The current model declares only the
four-column partition index at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:150-160`,
and the snapshot also contains only that index at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs:673-725`.
No later discoverable migration removes the broker-only index. The successful
pending-model check cannot detect this because it compares the current model to
the snapshot, not the latest migration designer to the snapshot.

`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs:1-39`
is not a valid EF migration artifact in its current form. It has no generated
designer file or `[Migration]` metadata, and the EF list proves it is not
discovered. It also creates only the current model's four-column index. Keeping
it beside `SnapshotRepair` creates false confidence without affecting runtime
migration order.

Subcluster 4a is fixture drift. `EnsureCreatedAsync` creates the current table,
but the legacy setup drops only earlier post-initial tables before applying
migrations at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:282-323`.
Subcluster 4b is assertion drift: the recovery path succeeds and applies all
five discovered migrations, but line 372 still hard-codes four.

## Alternatives and selected strategy

### Canonical migration

Selected strategy if deployment checks show neither candidate ID was applied:
remove both candidate artifacts, restore the snapshot through EF's latest-
migration removal workflow, and scaffold one migration named
`AddTrailingStopsPreferenceObservations` into the established Migrations
directory. Review the regenerated main file, designer target model, snapshot,
discovery list, pending-model check, and SQL script as one unit. The canonical
migration should reflect the current model's single partition index unless a
measured query requires the broker-only index.

Retaining `SnapshotRepair` unchanged was rejected for an unpublished lineage:
its name and subfolder preserve a repair accident, and its target model differs
from the current snapshot without a subsequent migration. Promoting the
`20260830120000` file was rejected because hand-adding metadata would preserve a
manually assembled artifact when deterministic scaffolding is available.

If `20260830112227_SnapshotRepair` was applied anywhere shared, keep its ID and
historical code. Do not rename, delete, or regenerate it. Remove the inert
duplicate only after confirming no released assembly contained matching
metadata, then add a forward corrective migration if the extra index must be
dropped. If `20260830120000_AddTrailingStopsPreferenceObservations` appears in
any history table, recover the exact migration triplet from the deployed
artifact or source before proceeding; the current workspace cannot safely
reconstruct that lineage.

### Web disposal

Selected alternative: cancellation-aware fake transport plus explicit test
synchronization. Override the cancellation-token-aware content serialization
path and await the gate with that token, or use a dedicated handler that blocks
in `SendAsync` with `WaitAsync(cancellationToken)`. Expose a
`CancellationObserved` task. After `cut.Dispose()`, await that signal and an
eventual assertion that `presenter.IsLoading` is false. Pair the existing
`Assert.Null(presenter.Error)` with an assertion that cancellation was actually
observed; otherwise the test can pass after ordinary gate release without
proving disposal cancellation.

Fixing only synchronization was rejected because releasing the uncancellable
gate and waiting for idle verifies eventual completion, not cancellation.
Changing production disposal to await completion was rejected as
disproportionate: the component currently implements synchronous `IDisposable`
and has no task handle. Moving to `IAsyncDisposable` would require tracking all
load/save/page tasks and could make disposal hang on a dependency that ignores
cancellation. No reproduced production behavior requires that stronger
contract.

### GUID keyset paging

Selected alternative: assert database order and page continuity. Seed fixed
GUIDs known to sort differently in SQL Server and .NET, query the expected IDs
with the same SQL `ORDER BY ObservedAtUtc DESC,
TrailingStopsPreferenceObservationId DESC`, then assert concatenated pages
equal that sequence with no duplicates or omissions. Keep the provider query
and cursor unchanged. This feature is explicitly SQL Server-backed, and the
GUID is an opaque deterministic tie-breaker rather than a user-visible order.

Adding a provider-neutral sortable key, such as an identity or monotonic
sequence included with the timestamp in the cursor, would give cross-provider
semantics but requires schema, migration, index, cursor-versioning, and API
compatibility work. Reserve it for a real cross-provider or chronological-order
requirement. Forcing .NET ordering or client evaluation was rejected because it
materializes the partition before paging, defeats the index and retention-scale
behavior, and makes keyset boundaries less efficient without improving this
feature's contract.

### Fixture correction versus weaker assertions

For the Application fake, replace the rows-only `ListResult` with a complete
`TrailingStopsPreferenceObservationPage` result. Configure the expected cursor
and invalid-cursor flag in the test, then assert the handler forwards all page
metadata. Do not change the production handler to derive a cursor from rows;
that duplicates store policy and would conceal the fake defect.

For no-history migration recovery, create the historical schema by migrating
an empty database specifically to
`20260727202238_InitialPlatformSchema`, seed the preserved row, then remove and
recreate the history table with the guarded baseline row. This is more stable
than `EnsureCreatedAsync` followed by an ever-growing list of dropped current-
model tables. If the existing shape is retained, at minimum also drop
`TrailingStopsPreferenceObservations`; that is a fixture correction, not a
weaker assertion.

For fail-closed recovery, replace `Assert.Equal(4, applied.Count())` with exact
sequence equality between `GetMigrationsAsync()` and
`GetAppliedMigrationsAsync()`, plus `GetPendingMigrationsAsync()` empty. This
strengthens the test's actual invariant, complete recovery, while the dedicated
Account Preferences migration test continues to assert the canonical migration
ID, table, and index. Changing the expected count from four to five alone was
rejected because it will become stale at the next migration and never proves
which migrations ran.

## Implementation impact and validation order

Use this order so each step has a narrow falsification point:

1. Query every shared, staging, and production `__EFMigrationsHistory`, and
	inspect released binaries or manifests for both candidate IDs. Stop migration
	cleanup until this is resolved.
2. Resolve the migration lineage using the unpublished or deployed branch above.
	Validate `migrations list`, `has-pending-model-changes`, and the generated SQL
	before running tests.
3. Correct the no-history and fail-closed recovery fixtures. Run their two
	filtered SQL tests, then the migration create/rerun tests.
4. Correct the Application fake page result. Run only the cursor-forwarding
	test, then the Application unit project.
5. Make the Web fake cancellation-aware and add the cancellation-observed plus
	eventual-idle assertions. Run the disposal test repeatedly, then the Web unit
	project.
6. Replace .NET GUID ordering with SQL-order/page-continuity assertions. Run the
	equal-timestamp test, all Account Preferences SQL tests, then the full
	Infrastructure integration project.
7. Run `dotnet test --no-restore --verbosity minimal` as the repository gate.

Migration cleanup has the highest deployment risk and must precede fixture
updates because the canonical ID determines exact assertions. The Application
and Web changes are test-only and low blast radius. The GUID test correction is
also test-only if the provider-specific order remains the accepted contract.
Schema/cursor redesign is the only alternative with API and data-migration
impact, and current evidence does not justify it.

## Decision blockers

Repository evidence cannot determine whether a dirty local build, manually
published artifact, shared test environment, or production deployment applied
either migration. Required confirmation:

* The distinct `MigrationId` values in every non-disposable database
* The migration classes and metadata embedded in every released build since the
  Account Preferences work began
* Whether the broker-only index was intentional and has an operational query
  requirement

Until those checks complete, deleting or renaming `SnapshotRepair` is unsafe.
The Web and paging decisions have no remaining product blocker unless the
requirement explicitly demands disposal to await idle state or cross-provider
GUID order.

## Recommended next research

* [ ] Confirm production deployment migration history before deleting, renaming, or regenerating any migration that may have been applied.
* [ ] Confirm whether query telemetry or an execution plan justifies the extra
  broker-only observation index.

## Clarifying questions

* Has either `20260830112227_SnapshotRepair` or
  `20260830120000_AddTrailingStopsPreferenceObservations` appeared in a shared,
  staging, or production `__EFMigrationsHistory` table?
* Is the required component contract cancellation without teardown errors, or
  must disposal itself wait until presenter state is idle?
* Is provider-neutral ordering a stated requirement, or is deterministic SQL
  Server ordering the intended persistence contract?
