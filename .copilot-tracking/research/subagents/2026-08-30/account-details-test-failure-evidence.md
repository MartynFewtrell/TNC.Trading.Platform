---
title: Account details test failure evidence
description: Evidence for the failing automatic account details capture de-duplication and retry unit test
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Research scope

* Capture the exact failure from `CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure` and establish whether it is repeatable.
* Trace automatic capture through trading-day calculation, schedule configuration, lease acquisition, existence checks, gateway access, and persistence.
* Compare the fixture trading day, system time use, current date, timezone, and weekend behavior.
* Identify repository-consistent deterministic `TimeProvider` test patterns.
* Use focused history evidence to assess date drift and evaluate alternate causes.
* Recommend the cheapest source-free discriminating experiment.

## Working hypothesis

The fixture persists trading day `2026-08-07`, but the handler computes its de-duplication key from `TimeProvider.System`. On 2026-08-30, the stored snapshot therefore does not match the computed trading day, so the first automatic capture reaches the gateway and returns `Saved` instead of `Deferred`. A selected test run can disconfirm this immediately if the failure occurs elsewhere or reports a different outcome.

## Commands and results

The test was run twice through the VS Code test tool by selecting only
`CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure`. Both runs
reported one failed test and no passed tests. Both failures were identical:

```text
Assert.IsType() Failure: Value is not the exact type
Expected: typeof(TNC.Trading.Platform.Application.Features.AccountDetails.AccountDetailsRefreshOutcome+Deferred)
Actual:   typeof(TNC.Trading.Platform.Application.Features.AccountDetails.AccountDetailsRefreshOutcome+Saved)
at TNC.Trading.Platform.Application.UnitTests.Features.AccountDetails.AccountDetailsHandlerTests.CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure()
AccountDetailsHandlerTests.cs:line 109
```

The following explicit command then ran only the same fully qualified test:

```powershell
dotnet test "test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj" --no-restore --filter "FullyQualifiedName=TNC.Trading.Platform.Application.UnitTests.Features.AccountDetails.AccountDetailsHandlerTests.CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure" --logger "console;verbosity=normal"
```

The command discovered one test. It failed in 8 ms with the same expected and
actual types at line 109. The summary was `total: 1, failed: 1, succeeded: 0,
skipped: 0`. The command exited with code 1. Reproduction is therefore
consistent across three isolated executions.

The clock context command was:

```powershell
Get-Date -Format "o"; [System.TimeZoneInfo]::Local | Select-Object Id, DisplayName, BaseUtcOffset | Format-List
```

It reported `2026-08-30T11:30:51.5918143+01:00`, local zone
`GMT Standard Time`, display name `(UTC+00:00) Dublin, Edinburgh, Lisbon,
London`, and base UTC offset `00:00:00`. The active daylight offset was `+01:00`.

## Code-path evidence

The selected test occupies
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:96-120`.
It creates a fixture, inserts a snapshot whose trading day is the fixture's
hard-coded day, invokes automatic capture, and expects `Deferred` before it
mutates the gateway for the failure-and-retry portion.

The executed automatic path is:

1. `RefreshAccountDetailsHandler.CaptureAutomaticAsync` reads the current
	 configuration through `PlatformConfigurationService` at
	 `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:19-22`.
	 The service is a direct pass-through to the configured store at
	 `src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs:5-10`.
2. The fake configuration at
	 `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:206-211`
	 returns platform `Test`, broker environment `Demo`, a UTC schedule covering
	 `00:00` through `23:59`, no explicit trading days, and
	 `WeekendBehavior.IncludeFullWeekend`.
3. The handler calls `TradingScheduleGate.GetTradingDay` with
	 `timeProvider.GetUtcNow()` at
	 `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:21-22`.
	 `GetTradingDay` resolves the configured timezone, converts the supplied
	 instant, and returns the local calendar date at
	 `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:53-57`.
4. The first existence check uses broker environment `Demo` and that computed
	 date at
	 `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:23-26`.
	 The in-memory fake checks environment and exact `DateOnly` equality at
	 `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:192-203`.
5. Because `2026-08-07 != 2026-08-30`, the first check returns false. The fake
	 lease is acquired at
	 `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:28-34`.
	 Its default result is `new(true)` at
	 `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:186-190`,
	 with no handle and no mutable acquisition behavior.
6. The second existence check at
	 `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:36-39`
	 returns false for the same
	 reason. This check protects against another process saving after the first
	 check and before lease acquisition; it cannot change the result in this
	 single-threaded fake.
7. `SaveSnapshotAsync` is called at
	 `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:41`.
	 It calls the gateway,
	 accepts a non-empty `Succeeded` result, creates a snapshot using another
	 clock read, and saves it at
	 `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:82-95`.
8. The fixture initializes the gateway to a successful one-account result at
	 `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:157-163`.
	 The fake increments `CallCount` and returns that result at lines 168-183.
	 The in-memory save appends the snapshot at lines 192-203. The first call
	 therefore returns `Saved`, exactly matching the assertion output.

The lease and persistence contracts are defined at
`src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsRefreshLease.cs:5-14`
and
`src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsSnapshotStore.cs:5-17`.
The production SQL lease obtains an environment-scoped, zero-timeout
`sp_getapplock`, reports automatic contention as not acquired, and releases the
lock on disposal at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlAccountDetailsRefreshLease.cs:9-64`.
The production EF store checks environment, day, and `Automatic` trigger at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:19-21`,
then saves retrieval and account rows transactionally in the same file at lines
23-43.

The direct automatic method does not call `TradingScheduleGate.Evaluate` or
`DecideTickAction`. Those schedule-active checks are separate methods at
`src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:7-51`.
For this test, the gate is used only to calculate a timezone-local date.

## Time and schedule evidence

The fixture hard-codes `TradingDay = new(2026, 8, 7)` and injects
`TimeProvider.System` into `RefreshAccountDetailsHandler` at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:145-163`.
The selected test also uses `DateTimeOffset.UtcNow` only for the seeded
snapshot's retrieval timestamp at lines 103-106. Retrieval time does not
participate in `ExistsForTradingDayAsync`; the hard-coded `TradingDay` does.

On the current date, 2026-08-30, the handler computes `2026-08-30` because the
fixture's schedule timezone is `UTC`. The machine's British Summer Time offset
does not affect that conversion. Timezone matters to the product contract in
general: the neighboring test proves that `2026-08-08T03:30:00Z` maps to
`2026-08-07` in `America/New_York` at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsContractTests.cs:7-29`.
It does not matter to this failure because the configured timezone is UTC and
the observed instant was far from UTC midnight.

2026-08-30 is a Sunday. Weekend behavior does not alter `GetTradingDay`; it is
consulted only by `IsTradingDayActive` during `Evaluate` at
`src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:59-73`.
The fixture also selects `IncludeFullWeekend`, which would make Sunday active
if `Evaluate` were called. Weekend behavior therefore neither causes nor
prevents this mismatch.

An exact workspace search found 25 `TimeProvider.System` occurrences in eight
files:

* Production fallback:
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Time/PlatformTimeProviderFactory.cs:14`
* The two account-details unit-test injections:
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:134,162`
* Infrastructure integration tests:
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:585,631-632,648`
* Infrastructure unit tests:
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:31-32,41,45`,
	`IgProofDataTests.cs:167,193`, `NotificationProviderTests.cs:404`,
	`ProtectedCredentialServiceTests.cs:31,51,81,109,116,142,175,210,235`, and
	`SqlPlatformConfigurationStoreTests.cs:298,304`

Only the account-details test combines a fixed calendar expectation with the
system clock on the path under investigation. Other system-clock uses should
be assessed independently based on whether their assertions depend on the
calendar or elapsed time.

## Deterministic-time patterns

Nearby repository tests use small `TimeProvider` subclasses rather than a
dedicated fake-time package. The application unit-test project references
xUnit and test SDK packages only; see
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:1-22`.

Representative fixed-clock conventions are:

* A constructor-injected fixed instant in
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs:147-150,275-278`
* A parameterless fixed clock in
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/RecordAuthAuditEvent/RecordAuthAuditEventHandlerTests.cs:136-143,190-193`
* A mutable clock with `Advance` for elapsed-time scenarios in
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:1041-1049`

The constructor-injected fixed clock matches this scenario. A complete proposed
shape for the selected test and fixture clock is:

```csharp
[Fact]
public async Task CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure()
{
		var now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
		var fixture = new Fixture(now);
		fixture.Store.Snapshots.Add(fixture.Snapshot(now, fixture.TradingDay));

		var duplicate = await fixture.Refresh.CaptureAutomaticAsync(CancellationToken.None);
		Assert.IsType<AccountDetailsRefreshOutcome.Deferred>(duplicate);
		Assert.Equal(0, fixture.Gateway.CallCount);

		fixture.Store.Snapshots.Clear();
		fixture.Gateway.Result = new AccountDetailsGatewayResult.Failed(
				AccountDetailsFailureCategory.Unavailable,
				"secret payload");
		var failed = await fixture.Refresh.CaptureAutomaticAsync(CancellationToken.None);
		fixture.Gateway.Result = new AccountDetailsGatewayResult.Succeeded([fixture.Account]);
		var retried = await fixture.Refresh.CaptureAutomaticAsync(CancellationToken.None);

		Assert.IsType<AccountDetailsRefreshOutcome.Failed>(failed);
		Assert.IsType<AccountDetailsRefreshOutcome.Saved>(retried);
		Assert.Equal(2, fixture.Gateway.CallCount);
}

private sealed class Fixture
{
		private readonly FixedTimeProvider timeProvider;

		public Fixture(DateTimeOffset now)
		{
				timeProvider = new FixedTimeProvider(now);
				Gateway.Result = new AccountDetailsGatewayResult.Succeeded([Account]);
				Configuration = new PlatformConfigurationService(new FakeConfigurationStore());
				Refresh = new RefreshAccountDetailsHandler(
						Configuration,
						Gateway,
						Store,
						Lease,
						new TradingScheduleGate(),
						timeProvider);
				Get = new GetAccountDetailsHandler(Configuration, Store);
		}

		public DateOnly TradingDay => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

		// Existing fixture members remain unchanged.
}

private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
		public override DateTimeOffset GetUtcNow() => utcNow;
}
```

The production-like refinement is to make the seeded snapshot trigger
`Automatic` and align the in-memory existence predicate with the EF predicate.
That refinement is separate from fixing the date drift and may warrant its own
focused test because the current `Snapshot` helper always creates a `Manual`
snapshot at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:166`.

## History evidence

Focused history command:

```powershell
git log --follow --date=iso --format="%h %ad %an %s" -- "test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs"
```

The file has one commit:

```text
8863cd9 2026-08-07 16:21:12 +0100 Martyn Fewtrell feat: Implement Account Details feature with presenter and view models
```

`git blame -L 96,120` attributes the entire selected test, including the seeded
snapshot and failing assertion, to `8863cd9f`. `git blame -L 145,163` attributes
both the hard-coded `2026-08-07` day and the `TimeProvider.System` injection to
the same commit. `git blame -L 5,45` on the handler attributes the automatic
capture implementation, including both day checks, to that same commit.

The test's fixed date equals the introducing commit's calendar date. On that
date, UTC system time would compute `2026-08-07` for most of the day, allowing
the seeded snapshot to de-duplicate. The code and test have no later commits,
so unchanged code becoming red on a later date is fully consistent with date
drift.

No retained result under `artifacts/` and no documentation search result was
found for this test name or commit. History does not prove that CI executed and
passed this test on 2026-08-07. It establishes the stronger relevant fact that
the date-sensitive setup and production path were introduced together and have
not changed since.

## Alternate causes

The alternate-cause assessment is:

* Static coalescing state is ruled out for this failure. `InFlight` is declared
	and used only by manual `HandleCoreAsync` at
	`src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:14,45-60`.
	`CaptureAutomaticAsync` never reads or writes it.
* Test ordering and parallelism are ruled out as necessary causes. Three runs
	selected exactly one test, and all failed identically. Each run creates a new
	fixture and new in-memory collections.
* Lease behavior is less likely and incompatible with the observed `Saved`
	result as a primary cause. The fake always returns acquired, has no shared
	state, and its null handle disposes as a completed operation. A denied lease
	would have returned `Deferred`, which is the expected type rather than the
	actual type.
* Mutable gateway state behaves as authored. Before the failing assertion, the
	gateway is still the fixture's default successful result. Its failure result
	mutation occurs only after line 112, which execution never reaches. The
	initial date miss therefore explains why the observed outcome is specifically
	`Saved`.
* A gateway race is ruled out for the selected path. `WaitForRelease` defaults
	to false, so the result is returned synchronously. No concurrent operation is
	started in this test.
* Timezone resolution failure is ruled out for the current fixture. `UTC` is a
	valid system timezone; even the gate's fallback for a missing or invalid zone
	is UTC at
	`src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:75-90`.
* Weekend handling is ruled out because automatic capture calls
	`GetTradingDay`, not `Evaluate`, and the fixture explicitly includes the full
	weekend anyway.
* The fake store's omission of the production `Automatic` trigger filter is a
	fidelity gap, but not this failure's cause. It makes the fake more permissive.
	With equal dates, the manually tagged fixture snapshot would produce the
	expected `Deferred`; with the current unequal dates, both predicates return
	false.

## Cheapest discriminating experiment

Without editing source, set a debugger breakpoint at
`src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:23`
and inspect `tradingDay` and
`fixture.Store.Snapshots[0].TradingDay` during the isolated test. The predicted
values are `2026-08-30` and `2026-08-07`. This directly tests the proposed cause
before lease or gateway behavior can intervene.

If a temporary test-only edit is later permitted, inject a fixed provider whose
UTC instant is 2026-08-07 and rerun the same filtered command. The prediction is
that the first assertion passes, gateway count remains zero for the duplicate,
and the full test passes with two later gateway calls. A second discriminating
variant is to keep `TimeProvider.System` but seed the snapshot with
`DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime)`; that should
also pass under the UTC fixture, but it leaves a midnight race and is not the
recommended permanent test design.

## Preliminary recommendation

Treat this as a deterministic-test defect, not a product regression. Inject a
fixed `TimeProvider` into the fixture and derive the seeded trading day from the
same fixed instant. The repository already uses this pattern in neighboring
application tests, and it tests the production contract without dependence on
wall-clock date, local timezone, or execution date.

Consider a separate focused correction that makes the fake existence check
match the production EF store's automatic-trigger predicate and seeds an
automatic snapshot. That change would improve contract fidelity but should not
be conflated with the immediate date-drift fix.

## Gaps and clarifying questions

* No historical test result was retained to prove a successful 2026-08-07 run.
* The research did not modify or execute a fixed-clock variant because the task
	explicitly prohibited product and test source edits.
* The broader suite was not run because the requested scope was the selected
	test only.
* No clarifying question blocks the conclusion.
