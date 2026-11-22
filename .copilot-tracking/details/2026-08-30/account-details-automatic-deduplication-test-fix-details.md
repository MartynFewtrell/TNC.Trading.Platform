<!-- markdownlint-disable-file -->
# Implementation Details: Account Details Automatic Deduplication Test Fix

## Context Reference

Sources: `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md` and `.copilot-tracking/research/subagents/2026-08-30/account-details-plan-context.md`

## Implementation Phase 1: Repair the Deterministic Fixture

<!-- parallelizable: false -->

### Step 1.1: Align the Test Clock and Seeded Snapshot

Modify the existing Account Details fixture and the failing test as one atomic edit. Add a fixture-owned `UtcNow` value that defaults to `2026-08-07T12:00:00Z`, construct a nested `FixedTimeProvider`, derive `TradingDay` from the fixed provider, and inject the provider into `RefreshAccountDetailsHandler`. Keep the fixture constructor optional argument so all existing callers remain valid.

Update `CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure` to seed its existing successful snapshot with `fixture.UtcNow`, `fixture.TradingDay`, and `AccountDetailsTriggerSource.Automatic`. Extend the `Snapshot` helper with an optional trigger argument that defaults to `Manual`, preserving unrelated history-read scenarios.

Files:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs` - Replace mixed wall-clock and fixed-date setup with one deterministic fixture clock and an explicit automatic snapshot seed

Discrepancy references:

* None. This step implements the selected research recommendation without deviation.

Success criteria:

* The fixture no longer injects `TimeProvider.System` into `RefreshAccountDetailsHandler`
* `UtcNow`, `TradingDay`, and the handler clock derive from one fixed instant
* The de-duplication test seeds a successful `Automatic` snapshot for that trading day
* Existing `Snapshot` callers retain `Manual` behavior by default

Context references:

* `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md` (Lines 88-154) - Failure mechanism and recommended fixed-clock shape
* `.copilot-tracking/research/subagents/2026-08-30/account-details-plan-context.md` (Lines 42-73) - Exact live edits and source-compatibility constraints
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs` (Lines 96-166) - Failing test and fixture before implementation

Dependencies:

* .NET SDK `10.0.200` selected by `global.json`
* Existing xUnit test project restore assets

### Step 1.2: Match the Production Automatic-Trigger Predicate

Update `InMemoryStore.ExistsForTradingDayAsync` to require `AccountDetailsTriggerSource.Automatic` in addition to the existing environment and trading-day checks. Use the production EF adapter as the behavioral fidelity reference. Add the nested `FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider` helper near the fixture and override `GetUtcNow`.

Keep these changes in the same edit as Step 1.1. The fixed clock removes date drift, while the trigger predicate prevents a false-positive test against behavior production does not implement.

Files:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs` - Add the fixed clock helper and align the in-memory existence predicate

Discrepancy references:

* None. This step implements the production-fidelity portion of the selected recommendation.

Success criteria:

* The in-memory store ignores manual snapshots when checking automatic daily de-duplication
* The predicate matches the environment, trading day, and automatic trigger semantics of the EF adapter
* No production source file is modified

Context references:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs` (Lines 19-20) - Production existence predicate
* `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md` (Lines 156-183) - Preferred approach and implementation requirements
* `.copilot-tracking/research/subagents/2026-08-30/account-details-plan-context.md` (Lines 24-40) - Current test and production anchors

Dependencies:

* Step 1.1 is applied in the same atomic test-file edit

### Step 1.3: Run the Focused Regression Test

Run the fully qualified failing test immediately after the atomic edit. Do not continue to wider changes until this check passes.

Validation command:

```powershell
dotnet test "test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj" --no-restore --filter "FullyQualifiedName=TNC.Trading.Platform.Application.UnitTests.Features.AccountDetails.AccountDetailsHandlerTests.CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure" --logger "console;verbosity=normal"
```

Success criteria:

* The selected test passes on the current date without reading the system clock in the tested handler
* The first automatic call returns `Deferred` without a gateway call
* The failed attempt remains retryable and the later successful attempt returns `Saved`

Dependencies:

* Steps 1.1 and 1.2 completed atomically

## Implementation Phase 2: Validate and Close Scope

<!-- parallelizable: false -->

### Step 2.1: Run the Affected Unit-Test Project

Execute the complete Application unit-test project after the focused regression passes. `dotnet test` builds the touched project, so a separate build command is unnecessary.

Validation command:

```powershell
dotnet test "test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj" --no-restore --nologo
```

If restore assets are unavailable, rerun without `--no-restore`. Repair only failures attributable to the changed fixture and rerun the same command.

Success criteria:

* All tests in `TNC.Trading.Platform.Application.UnitTests.csproj` pass
* Existing Account Details history, manual refresh, lease, and event tests retain their prior behavior

Dependencies:

* Step 1.3 passes

### Step 2.2: Verify Scope and Report Blocking Issues

Review the final change set and confirm it modifies only `AccountDetailsHandlerTests.cs`. Do not modify product code, API or schema contracts, `docs/wiki`, or the incomplete hosted-validation work in `.copilot-tracking/plans/2026-08-07/account-details-plan.instructions.md`.

Record a separate manual-snapshot negative regression test as follow-on work. Do not add it during this fix unless the task scope changes. If validation reveals a defect requiring production behavior changes or broad fixture refactoring, stop and request additional research and planning.

Success criteria:

* The final implementation is confined to one unit-test file
* No runtime behavior, architecture, API, schema, or documentation changes are introduced
* Unrelated failures and broader improvements are reported rather than folded into this fix

Context references:

* `.copilot-tracking/research/subagents/2026-08-30/account-details-plan-context.md` (Lines 75-91) - Follow-on regression-test boundary
* `.copilot-tracking/research/subagents/2026-08-30/account-details-plan-context.md` (Lines 124-160) - Wiki and existing-plan scope decisions

Dependencies:

* Step 2.1 completes successfully

## Dependencies

* .NET SDK `10.0.200`
* Restored Application unit-test project dependencies
* No new package, database, container, or network dependency

## Success Criteria

* The previously failing de-duplication test is deterministic across dates and time zones
* The in-memory store models production automatic-trigger de-duplication
* The full Application unit-test project passes
* Production and documentation files remain unchanged