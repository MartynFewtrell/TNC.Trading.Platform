<!-- markdownlint-disable-file -->

# Failing Tests Baseline and Causality Research

## Research status

Complete for baseline reproduction and provisional causality. Two implementation
intent questions remain for the remediation owner.

## Research questions

* What validation command does the repository intend contributors to run?
* Which tests currently fail, and which failures are environmental versus behavioral?
* Which symptoms share a controlling source, configuration, or test dependency?
* Which recent commits or worktree changes have evidence of causing each failure?
* Which passing tests delimit the affected behavior?
* What is the cheapest check that can disconfirm each root-cause hypothesis?

## Applicable guidance

* `.github/copilot-instructions.md:18-24` requires Docker for the supported local
  Keycloak runtime and recommends keeping Docker-backed SQL Server and Keycloak
  running between validations.
* `.github/instructions/tests.instructions.md:27-29` prefers isolated unit tests
  before higher-level suites. Lines 41-57 distinguish in-memory unit tests from
  infrastructure-backed integration, end-to-end, and functional suites. Line
  92 defines `dotnet test` as the repository test gate.
* `.github/instructions/aspire-tests.instructions.md:21-40` requires
  `Aspire.Hosting.Testing`, `DistributedApplicationTestingBuilder`, closed-box
  external interactions, and resource cleanup for AppHost-backed suites.
* `.github/instructions/architecture.instructions.md:50-70` places business
  decisions in policy and use-case code and requires those behaviors to be
  tested independently of infrastructure where possible.
* `.github/instructions/csharp.instructions.md:31-57` requires repository C#
  language/style consistency, intentional cancellation/error handling, and
  behavior tests.
* `.github/instructions/dotnet-stack.instructions.md:19-45` requires the SDK
  pinned by `global.json`, SQL Server, migrations, and Aspire local
  orchestration. `global.json:1-6` pins SDK `10.0.200` with
  `latestFeature` roll-forward.
* `docs/wiki/testing-and-quality.md:45-53` demonstrates project-scoped
  `dotnet test <csproj>` as the focused feedback loop. Lines 66-87 enumerate
  the test projects and their boundaries. Lines 89-108 state that distributed
  suites require Docker-backed SQL Server, Mailpit, Keycloak, and AppHost.
* `docs/wiki/local-development.md:5-25` identifies the .NET SDK and Docker
  Desktop as prerequisites. Lines 88-94 define `dotnet build` from the
  repository root. No repository-owned general test script exists; the only
  PowerShell script found is the architecture-specific
  `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1`.

Intended validation sequence: use the changed project-specific
`dotnet test <csproj>` commands for fast discrimination, then run unfiltered
repository-root `dotnet test` as the solution gate.

## Git context

Commands:

```powershell
git status --short --branch
git log -12 --date=iso-strict --pretty=format:"%h%x09%ad%x09%an%x09%s"
git diff --stat
git diff --cached --stat
git diff --name-only
git ls-files --others --exclude-standard
```

Branch `012-add-account-details` tracks `origin/012-add-account-details`.
There are no staged changes. The worktree contains 27 modified tracked files
(`475` insertions, `36` deletions in tracked diff stats) plus 42 untracked files
for Account Preferences across Application, API, Infrastructure, Web, and
tests. Shared auth-runtime and SQL-fixture files are also modified. No changes
were altered or reverted during research.

Recent committed history begins with:

```text
ac8d6f3 2026-08-30T12:09:37+01:00 feat: Refactor AccountDetailsHandlerTests to use FixedTimeProvider for consistent time handling
5617c48 2026-08-17T15:48:30+01:00 Updated order on Navigation
155d719 2026-08-08T13:50:54+01:00 feat: Enhance Account Details page with tabbed account type display and improved layout
8863cd9 2026-08-07T16:21:12+01:00 feat: Implement Account Details feature with presenter and view models
```

The failures found so far occur in untracked Account Preferences tests or in
existing SQL tests whose migration assumptions are changed by the untracked
migration. Recency alone is not treated as causality; each assessment below is
based on the executing path and changed artifact.

## Validation commands and results

The initial Test Explorer run targeted the three new Account Preferences
Application test files: 12 total, 11 passed, 1 failed. The exact documented CLI
reproduction and subsequent focused runs were:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --verbosity minimal
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore --verbosity minimal
```

Results:

| Project | Total | Passed | Failed | Classification |
| --- | ---: | ---: | ---: | --- |
| Application unit | 128 | 127 | 1 | Assertion/test-double contract |
| API unit | 57 | 57 | 0 | Passing delimiter |
| Infrastructure unit | 112 | 112 | 0 | Passing delimiter |
| Web unit | 107 | 106 | 1 | Async test-harness timing |
| Infrastructure integration | 22 | 19 | 3 | SQL behavior/migration assertions |

The SQL integration fixture successfully found the running Aspire SQL Server
container, read its managed credential, created a unique database, and executed
SQL. Its failures are not missing-Docker or connection failures. Focused unit
suites require no infrastructure. Builds emitted package vulnerability and
nullable/analyzer warnings, but those warnings did not cause test failures.

The required broad gate was then run:

```powershell
dotnet test --no-restore --verbosity minimal
```

Result: 515 total, 510 passed, 5 failed, 0 skipped, duration 134.9 seconds.
The five failures were the same one Application unit, one Web unit, and three
Infrastructure integration failures from the focused runs. The
`TNC.Trading.Platform.Api.IntegrationTests` project passed after starting the
real AppHost; logs showed successful API startup and live HTTP interactions.
No additional infrastructure or assertion cluster appeared. The gate emitted
100 warnings, primarily known package advisories and existing compiler/analyzer
warnings.

Additional discriminating commands:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName~Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError" --verbosity minimal
dotnet ef migrations list --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-connect
git blame -L 282,323 -L 330,373 -- test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs
git show HEAD:src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs
```

The filtered Web test failed again: 1 total, 0 passed, 1 failed, reproducing
`IsLoading == true`. The EF CLI check did not execute migration discovery
because the API startup project does not reference
`Microsoft.EntityFrameworkCore.Design`; this is a tooling prerequisite failure,
not a product test failure. Static `[Migration]` metadata, applied migration
results, and integration assertions provide the migration-discovery evidence.

## Failure clusters

### Cluster 1: Application history cursor fake returns null

Failing test:
`AccountPreferencesHandlerTests.GetTrailingStopsPreferenceObservations_ShouldReturnDeterministicPageCursor_WhenPageIsFull`.

Key message:

```text
Assert.Equal() Failure: Strings differ
Expected: "00000000000000000000000000000002"
Actual:   null
at AccountPreferencesHandlerTests.cs:152
```

The handler forwards `page.NextCursor` unchanged at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsHandler.cs:7-15`.
The test fills `ListResult` and expects a derived cursor at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:137-153`,
but its fake store always constructs
`TrailingStopsPreferenceObservationPage(ListResult, null, false)` at lines
190-198. The failure is deterministic and infrastructure-free.

Likely shared root cause: the fake models only rows, while the production store
contract returns rows plus pagination metadata. Confidence: high.

### Cluster 2: Web disposal assertion races blocked response content

Failing test:
`AccountPreferencesTests.Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError`.

Key message:

```text
Assert.False() Failure
Expected: False
Actual:   True
at AccountPreferencesTests.cs:86
```

The component cancels and disposes its owned token at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:25-39`.
The presenter catches token cancellation and clears `IsLoading` in `finally` at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:10-27`.
The API client passes the token through `SendAsync` and JSON deserialization at
`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:130-138`.

The test releases its response gate, invokes an unrelated completed task, then
asserts immediately at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:70-87`.
Its `DelayedJsonContent.SerializeToStreamAsync` awaits only the uncancellable
gate at lines 150-164. The handler returns that response synchronously at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/SequencedHttpMessageHandler.cs:13-35`.
Therefore the assertion can run before the HTTP continuation and presenter's
`finally` execute. Confidence: high for test-harness timing; a focused repeated
run is still needed to quantify flakiness.

### Cluster 3: SQL GUID ordering differs from .NET GUID ordering

Failing test:
`AccountPreferencesSqlIntegrationTests.ListAsync_ShouldPageEqualTimestampsDeterministically_WhenRowsShareTimestamp`.

Key message: the two returned IDs are the reverse of
`new[] { first.Id, second.Id }.OrderByDescending(id => id)` at
`AccountPreferencesSqlIntegrationTests.cs:83`.

The EF query orders equal timestamps by SQL Server `uniqueidentifier` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:27-47`.
The test computes expected order with .NET `Guid` comparison at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:62-83`.
SQL Server and .NET compare GUID byte groups differently, so both sequences can
be internally deterministic but disagree. Confidence: high.

### Cluster 4: migration chain and legacy-schema assumptions drifted

Failing tests:

* `PlatformSqlServerIntegrationTests.InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject`
* `PlatformSqlServerIntegrationTests.MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned`

Key messages:

```text
Expected applied migration count: 4
Actual applied migration count:   5
at PlatformSqlServerIntegrationTests.cs:372

Microsoft.Data.SqlClient.SqlException:
There is already an object named 'TrailingStopsPreferenceObservations' in the database.
at PlatformSqlServerIntegrationTests.cs:321
```

The existing recovery test hard-codes four migrations at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:330-373`.
The untracked discoverable migration
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs:10-48`
creates the new table and becomes the fifth migration.

The no-history test calls `EnsureCreatedAsync`, which creates the current model
including the new table, then drops several newer tables but not
`TrailingStopsPreferenceObservations` before baselining only the initial
migration at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:282-323`.
The subsequent discoverable `SnapshotRepair` migration tries to create that
still-existing table.

An additional untracked
`20260830120000_AddTrailingStopsPreferenceObservations.cs:8-39` duplicates the
table creation but has no `.Designer.cs` metadata and did not appear in applied
migrations. The new integration test explicitly expects `SnapshotRepair` to be
applied and `AddTrailingStopsPreferenceObservations` not to be applied at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:15-29`.
Confidence: high that the two observed failures are migration-test assumption
drift caused by the uncommitted model/migration artifacts; the intended final
migration lineage remains unclear.

Blame shows the no-history scenario was introduced by commit `8df68d8`, its
Account Details cleanup additions by `8863cd9`, and the hard-coded migration
count of four by `8863cd9`. The committed `HEAD` context contains no
`TrailingStopsPreferenceObservations` set or model mapping. This is direct
evidence that the current worktree model and migration artifacts invalidate
previously correct test setup; it is not an inference from commit recency.

## Causality assessment

* Cluster 1 is caused by the untracked test fake and assertion in the same file.
  The committed handler baseline cannot cause this test because the entire
  Account Preferences slice is untracked. Confidence: high.
* Cluster 2 is caused by the untracked test's non-cancellable delayed content
  and lack of a completion synchronization point. Production cancellation is
  forwarded, but a production lifecycle defect is not fully disproven until a
  cancellation-aware handler check passes. The filtered test reproduced the
  same state in isolation. Confidence: high for the immediate assertion
  failure, medium for broader disposal correctness.
* Cluster 3 is introduced entirely by untracked store and integration-test
  files. The failure demonstrates an invalid cross-provider ordering
  assumption, not nondeterministic SQL. Confidence: high.
* Cluster 4 is caused by adding a discoverable fifth migration without updating
  existing hard-coded migration-count and no-history transition setup. The
  duplicate undiscoverable migration file is a separate migration hygiene risk.
  Confidence: high for current failures, medium on the intended artifact to
  retain because repository intent cannot be inferred from generated names.
* Commit `ac8d6f3` changes only existing Account Details test time setup. No
  current failure traced to that file. The passing full Application project
  delimits that commit's behavior except for the unrelated untracked cursor
  test.

## Passing boundary tests

* API unit: 57/57 pass. This delimits endpoint mapping, endpoint handlers, and
  service registration from current failures.
* Infrastructure unit: 112/112 pass. This delimits in-memory store mapping, IG
  gateway translation, and retention behavior from SQL provider/migration
  failures.
* Application unit: 127 tests pass. Other account-preference policy, validation,
  get/update, telemetry, and observation behaviors pass; only fake pagination
  metadata fails.
* Web unit: 106 tests pass. Initial load, toggle, save, pending-state, failure
  feedback, authorization, and next-page behavior pass; only teardown timing
  fails.
* Infrastructure integration: 19 tests pass, proving Docker/SQL connectivity,
  database creation/reset, most existing migration behavior, durable store
  readback, partition isolation, invalid cursor rejection, and retention.
* The broad gate's AppHost-backed API integration project passes, delimiting the
  modified shared authentication runtime and proving that current Docker,
  Keycloak, SQL Server, listener discovery, and protected API startup are
  operational in this environment.

## Hypotheses and disconfirming checks

| Hypothesis | Cheapest discriminating validation |
| --- | --- |
| Cluster 1 is only a fake-store contract mismatch. | Configure the fake page's `NextCursor` explicitly and rerun only the failing test. A continued null result would disconfirm forwarding behavior. |
| Cluster 2 is an assertion race caused by uncancellable delayed content. | Use a cancellation-aware handler/content completion signal and await the presenter's completion or condition, then rerun only the disposal test repeatedly. A persistent `IsLoading = true` after observed completion would disconfirm the timing hypothesis. |
| Cluster 3 is SQL/.NET GUID collation mismatch. | Query both IDs in the database's actual `ORDER BY ObservedAtUtc DESC, Id DESC` order and compare page concatenation to that sequence. A mismatch would implicate keyset filtering instead. |
| Cluster 4 is stale no-history/migration-count setup. | List `context.Database.GetMigrationsAsync()` and applied migrations, then run the two failures after making the test setup derive expectations from intended migration IDs. Duplicate creation after a single canonical migration remains would disconfirm assumption drift alone. |

## Provisional remediation direction

No production or test code was changed. Provisional directions are:

* Model pagination metadata explicitly in the Application fake instead of
  deriving a cursor from rows in the handler test.
* Make the Web disposal test synchronize on cancellation/completion and use a
  test transport that observes cancellation; preserve production token
  propagation unless the discriminating check finds a real lifecycle defect.
* Define the cursor ordering contract in provider terms. For SQL-backed keyset
  paging, assert against SQL order or store a provider-neutral sortable cursor
  component rather than comparing SQL `uniqueidentifier` order to .NET `Guid`.
* Retain one canonical generated EF migration for the feature, remove accidental
  duplicate/repair artifacts, and update migration recovery tests to preserve
  their scenario without hard-coding a stale total.

## Unresolved gaps

* The intended canonical migration (`SnapshotRepair` versus
  `AddTrailingStopsPreferenceObservations`) requires author confirmation or
  regeneration evidence.
* Cluster 2 needs a cancellation-aware focused check to distinguish harmless
  test timing from any residual component disposal lifecycle issue.
* The aggregate solution output confirms no additional failures, but its unified
  summary does not expose per-project totals for every passing distributed
  project. This does not affect the five-failure baseline.

## Recommended next research

* [ ] Confirm which migration artifact is intended, then inspect the canonical
  generated migration SQL and discovered migration list using a design-time
  startup path that supports EF CLI.
* [ ] Exercise the Web presenter through a cancellation-aware test HTTP handler
  with an explicit completion signal to settle production disposal behavior.
* [ ] Compare SQL-returned GUID order directly with two-page concatenation to
  prove the keyset predicate independently of .NET `Guid` ordering.
* [ ] After remediation, rerun the five focused tests, all affected projects,
  and the repository-root `dotnet test` gate.

## Clarifying questions

* Was `_SnapshotRepair/20260830112227_SnapshotRepair` an intentional migration
  name and location, or a temporary generation artifact that should be replaced
  by `20260830120000_AddTrailingStopsPreferenceObservations`?
* Should component disposal guarantee that the presenter has returned to an
  idle state before `Dispose` returns, or is the requirement limited to
  cancellation plus no post-disposal render/error? The current test asserts the
  stronger presenter-state guarantee without awaiting operation completion.