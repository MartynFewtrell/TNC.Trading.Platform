---
title: Failing Tests Plan Verification Research
description: Verifies implementation surfaces, dependencies, validation, and planning disposition for four failing-test clusters
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Research scope

* Verify the current architecture and exact implementation surfaces for the Application pagination fake contract.
* Verify the current architecture and exact implementation surfaces for Web disposal cancellation synchronization.
* Verify the current architecture and exact implementation surfaces for the SQL Server GUID keyset-order test contract.
* Verify the current architecture and exact implementation surfaces for EF migration lineage and recovery fixtures.
* Compare current evidence with the primary root-cause research.
* Determine whether the existing remediation plan, details, and log should be extended or replaced.
* Identify phase dependencies, safe parallelization, focused and full validation commands, unresolved decision gates, and discrepancies.

## Status

Complete. Current code, tests, EF discovery, model-snapshot alignment, and the
three existing planning artifacts were verified on 2026-08-30. No product or
test implementation files were changed.

## Executive findings

The five failures remain four independent clusters. Three clusters require
test-only corrections because current production behavior already implements
the intended contract. The migration cluster combines stale recovery fixtures
with an unresolved source-lineage decision.

EF Core currently discovers these five migrations in order:

1. `20260727202238_InitialPlatformSchema`
2. `20260728093000_AddSharedDataProtectionKeys`
3. `20260728094818_AddDurableIgProofData`
4. `20260807103334_AddAccountDetailsSnapshots`
5. `20260830112227_SnapshotRepair`

`20260830120000_AddTrailingStopsPreferenceObservations` remains undiscoverable.
The current model matches `PlatformDbContextModelSnapshot`, but the latest
discoverable migration designer has an extra broker-only index. Therefore,
`has-pending-model-changes` is green without proving migration-designer and
snapshot equivalence.

The existing remediation plan should be extended, not replaced. It covers the
same feature and its final validation is still incomplete. Replacing it would
discard useful implementation history. Its completion markers are no longer
credible for persistence coverage, Web cancellation coverage, or focused
validation, so an appended residual-failure phase and discrepancy-log entries
are required before implementation resumes.

## Verified architecture and implementation surfaces

### Application pagination fake contract

The Application feature owns the page contract through the inward persistence
port. `ITrailingStopsPreferenceObservationStore.ListAsync` returns observations,
the next cursor, and invalid-cursor state in
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs:5-10`.
The handler retrieves configuration, clamps page size, calls that port, and
forwards all three result members without recomputing a cursor in
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:5-16`.

The failing test sets only `ListResult` at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:140-153`.
Its fake always constructs `TrailingStopsPreferenceObservationPage` with a null
cursor and `HasInvalidCursor == false` at the same file's lines 190-198.

Required implementation surface:

* Change only `AccountPreferencesHandlerTests.cs`.
* Replace the rows-only fake property with a complete
  `TrailingStopsPreferenceObservationPage` result, or add explicit next-cursor
  and invalid-cursor fake state.
* Configure the expected non-null cursor in the full-page test.
* Preserve assertions that the handler forwards cursor, observations, invalid
  state, and the clamped page size. Do not add cursor generation to the handler.

Architecture consequence: this remains an isolated use-case unit test. The
outbound adapter owns cursor creation; the handler owns orchestration and
forwarding.

### Web disposal cancellation synchronization

The component owns one request-lifetime token and passes it to load, save, and
next-page operations before cancelling and disposing it in
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:28-44`.
The presenter treats matching cancellation as control flow and resets
`IsLoading` in `finally` at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:10-27`.
The API client forwards cancellation to HTTP send and JSON deserialization for
preferences and history at
`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:136-166`.

The failing bUnit test disposes the component, releases normal completion,
awaits an unrelated dispatcher task, and immediately reads presenter state at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:70-88`.
Its `DelayedJsonContent` overrides only the cancellation-insensitive stream
serializer and waits on a plain task at the same file's lines 150-165.

Required implementation surface:

* Change only `AccountPreferencesTests.cs`.
* Make `DelayedJsonContent` override the serializer overload that receives a
  `CancellationToken`, wait with that token, and write with that token.
* Expose a `TaskCompletionSource` completed when cancellation is observed.
* Dispose the component, await the explicit cancellation signal with a finite
  timeout, then use bUnit's eventual assertion pattern for presenter idle state.
* Retain the null-error assertion and explicitly assert cancellation occurred.

No production change is justified unless this cancellation-aware test still
shows `IsLoading == true` after the presenter continuation completes.

### SQL Server GUID keyset-order contract

The EF adapter filters by environment partition, orders by timestamp and GUID,
uses the same tuple for the keyset predicate, and returns the last row ID as the
next cursor in
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-56`.
The failing integration test instead computes expected order with .NET
`Guid.CompareTo` at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:65-83`.

Required implementation surface:

* Change only `AccountPreferencesSqlIntegrationTests.cs` for this cluster.
* Seed fixed GUIDs whose SQL Server order differs from .NET order.
* Query expected IDs through SQL Server with `ORDER BY ObservedAtUtc DESC,
  TrailingStopsPreferenceObservationId DESC`.
* Compare the concatenated keyset pages with that provider-ordered sequence.
* Assert exact count, uniqueness, no omission, no duplication, and cursor
  termination after the final page.

Do not change the production query to use .NET ordering or client evaluation.
The database provider defines `uniqueidentifier` ordering, and the query and
seek predicate are internally consistent.

### EF migration lineage and recovery fixtures

The discoverable migration is identified by generated metadata at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:12-16`.
Its main file creates the observation table plus broker-only and partitioned
indexes at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs:9-49`.
The standalone duplicate at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs:1-39`
has no designer metadata and is not discovered.

The current model defines only the partitioned index at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:150-160`.
The snapshot agrees at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs:673-725`,
while the `SnapshotRepair` target model includes the additional broker-only
index at its designer's lines 724-730.

The no-history test creates the current model with `EnsureCreatedAsync`, drops
four post-initial tables, retains `TrailingStopsPreferenceObservations`, inserts
only the initial migration history row, and then migrates at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:282-323`.
This is not an initial-schema fixture. The discoverable migration correctly
collides with the retained current-model table.

The conflict-recovery test expects exactly four applied migrations after a
successful retry at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:333-373`.
The initializer itself checks applied history, recognizes the eight-table
initial platform baseline, inserts the initial migration ID when history is
absent, and then migrates at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:10-23`
and lines 44-119.

Required implementation surfaces after the decision gate:

* Resolve migration source lineage in the two candidate migration files, the
  discoverable designer, and `PlatformDbContextModelSnapshot.cs`.
* In `PlatformSqlServerIntegrationTests.cs`, construct the historical fixture
  by targeting `20260727202238_InitialPlatformSchema` through EF's relational
  migrator, seed preserved data, then remove or recreate history according to
  the scenario. Do not derive history by subtracting tables from the current
  `EnsureCreatedAsync` model.
* Replace the numeric migration count with sequence equality between
  `GetMigrationsAsync()` and `GetAppliedMigrationsAsync()`, plus an empty
  `GetPendingMigrationsAsync()` result.
* Keep fresh-database and rerun identity coverage in
  `AccountPreferencesSqlIntegrationTests.cs:13-44`, but make expected migration
  IDs follow the selected canonical lineage.
* Continue using `SqlServerDatabaseFixture.cs:8-113` for its disposable
  database, finite operation token, command timeout, reset, and SQL Server
  collection serialization.

## Phase dependencies and parallelization

### Phase 0: Migration history decision

Query every non-disposable `__EFMigrationsHistory` and inspect released
Infrastructure assemblies for both candidate IDs. This phase blocks deletion,
renaming, regeneration, or migration-ID assertions. It does not block the
Application, Web, or SQL ordering test corrections.

### Phase 1: Independent contract corrections

The Application fake, Web cancellation harness, and SQL GUID ordering assertion
can proceed in parallel because they have separate ownership and production
behavior is unchanged.

The SQL ordering worker and migration worker must not concurrently edit
`AccountPreferencesSqlIntegrationTests.cs`. Assign that whole file to one
worker or serialize those edits. Infrastructure integration tests also share a
collection with `DisableParallelization = true`, so their executions must be
serialized even when source edits are independent.

### Phase 2: Canonical migration lineage

After Phase 0, choose one of these paths:

* If neither candidate reached a non-disposable database or released artifact,
  remove both candidates through the EF workflow and scaffold one canonical
  migration whose main file, designer, and snapshot agree.
* If `20260830112227_SnapshotRepair` was applied or released, preserve that ID
  and historical migration files, remove the inert duplicate only when release
  evidence permits, and add a forward corrective migration if the extra index
  is not intended.
* If `20260830120000_AddTrailingStopsPreferenceObservations` appears in any
  history or release despite being undiscoverable in the current worktree,
  recover its exact released migration triplet before any source cleanup.

### Phase 3: Recovery fixtures

Update the historical and conflict-recovery fixtures only after the canonical
sequence is stable. The assertions should derive completion from discovered,
applied, and pending sets rather than from a hard-coded count.

### Phase 4: Validation

Run exact tests first, then their owning projects, then EF metadata checks and
the serial solution gate. Stop at the first failure in each progression.

## Planning artifact disposition

Extend the three current artifacts; do not replace them.

The plan marks persistence Phase 4 complete at
`.copilot-tracking/plans/2026-08-30/trailing-stops-remediation-plan.instructions.md:107-117`,
Web cancellation complete at line 128, and focused validation complete at line
146. Phase 7 and the serial solution gate remain incomplete at lines 142-148.
The details already own migration identity and upgrade coverage at
`.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md:301-380`,
Web cancellation at lines 428-447, and final validation at lines 498-530.

Recommended planning changes during the next planning pass:

* Append a residual-failure phase covering the four clusters and their exact
  files. Do not reopen unrelated completed provider, API, retention, or
  environment work.
* Mark persistence coverage, Web cancellation validation, and final focused
  validation as incomplete or superseded by the appended corrective phase.
* Append new discrepancy records for the incomplete migration inventory,
  rows-only Application fake, cancellation-insensitive Web content fake,
  provider-mismatched GUID assertion, and current-model recovery fixture.
* Preserve prior decisions and status history in the log rather than rewriting
  the artifact as though the failures were known before implementation.

The log currently says no critical or major gaps remain at
`.copilot-tracking/plans/logs/2026-08-30/trailing-stops-remediation-log.md:6`,
while still recording unknown deployment scope at lines 12-17 and WI-05 at
line 108. That contradiction must be corrected. The migration inventory cannot
be both incomplete and an already satisfied prerequisite for completed Phase 4.

## Validation commands

The pinned SDK is `10.0.200` in `global.json:1-6`. The solution includes all
product and test projects through `TNC.Trading.Platform.slnx:1-34`.

### Focused tests

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~GetTrailingStopsPreferenceObservations_ShouldReturnDeterministicPageCursor_WhenPageIsFull" --verbosity minimal

dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError" --verbosity minimal

dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ListAsync_ShouldPageEqualTimestampsDeterministically_WhenRowsShareTimestamp" --verbosity minimal

dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned|FullyQualifiedName~InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject" --verbosity minimal
```

Repeat the repaired Web disposal test to detect synchronization regressions:

```powershell
1..10 | ForEach-Object {
    dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError" --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Web disposal repetition $_ failed." }
}
```

### Owning project tests

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --verbosity minimal
```

### EF lineage checks

```powershell
dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext --no-connect

dotnet ef migrations has-pending-model-changes --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext

dotnet ef migrations script 20260807103334_AddAccountDetailsSnapshots 20260830112227_SnapshotRepair --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --context PlatformDbContext --no-transactions
```

Replace the script target with the selected canonical migration ID if the
unpublished-lineage path is approved. Review the generated SQL for one table,
the intended indexes, and one history insert.

### Full repository validation

```powershell
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test TNC.Trading.Platform.slnx --no-restore --no-build -m:1 --verbosity minimal
```

The primary research measured the full test command at 134.9 seconds. This
conflicts with the existing plan's universal sub-60-second command deadline.
Keep finite hang detection and process cleanup, but do not classify a healthy
serial solution run as failed solely because its aggregate duration exceeds 60
seconds. If the 60-second operational policy is mandatory, use the owning
project commands as bounded gates and record that they are a split substitute
for the aggregate solution invocation.

## Discrepancies from primary research

The primary causal conclusions remain correct. Current verification found these
important refinements:

* Current line locations have shifted slightly. The Application test begins at
  line 140, the Web disposal test at line 70, and the SQL GUID test at line 65.
* The current worktree still has both migration candidates untracked and the
  snapshot modified. The three newly failing test files are also untracked.
* EF discovery and pending-model checks reproduced the primary result: five
  migrations are discovered, the standalone migration is ignored, and no model
  changes are pending.
* The existing plan is not neutral background. Its completed Steps 4.3, 5.3,
  and 7.1 claim validation that the five current failures disprove.
* The existing details favor preserving `SnapshotRepair` by default, while the
  primary research prefers a clean canonical migration when all environments
  prove both candidates unpublished. These are compatible conditional paths,
  but neither may be selected until deployment and release history is known.
* The no-history fixture should use EF's historical migration path, not an
  expanding table-drop list. This is also closer to the initializer's actual
  guarded-baseline architecture.

## Remaining gaps and decision gates

One decision blocks migration implementation: establish whether either
candidate migration ID or its compiled metadata reached any non-disposable
database or released Infrastructure assembly.

Collect sanitized evidence for:

* Distinct `MigrationId` values in CI, shared development, staging, and
  production `__EFMigrationsHistory` tables
* Migration metadata embedded in every released Infrastructure assembly since
  Account Preferences work began
* Operational ownership of the extra broker-only index in `SnapshotRepair`

An inaccessible environment is a blocking result, not evidence that the
migration is unpublished. This gate blocks migration file deletion, rename,
regeneration, and final migration-ID assertions. It does not block the
Application fake, Web cancellation harness, or SQL provider-order test fixes.

No other unresolved architecture decision blocks implementation. A product
decision would be required only if provider-neutral GUID ordering or
synchronous component disposal becomes a new requirement; neither is supported
by the current contracts.

## Recommended next research

* [ ] Inventory both candidate migration IDs across all non-disposable database
  histories
* [ ] Inspect released Infrastructure assemblies for migration metadata
* [ ] Confirm whether the broker-only index serves an intentional operational
  query or should be removed by a forward migration

These items were not completed because the workspace contains no credentials,
environment inventory, or released binary catalog for external deployments.

## References

* `.copilot-tracking/research/2026-08-30/failing-tests-root-cause-research.md`
* `.copilot-tracking/plans/2026-08-30/trailing-stops-remediation-plan.instructions.md`
* `.copilot-tracking/details/2026-08-30/trailing-stops-remediation-details.md`
* `.copilot-tracking/plans/logs/2026-08-30/trailing-stops-remediation-log.md`
