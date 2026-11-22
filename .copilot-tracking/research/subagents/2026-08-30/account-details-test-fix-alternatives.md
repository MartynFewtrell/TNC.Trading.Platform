---
title: Account details test fix alternatives
description: Phase 2 alternatives analysis for the verified account details unit-test failure
ms.date: 2026-08-30
ms.topic: concept
---

## Research questions

* Compare a fixed test `TimeProvider` with runtime-derived seed dates.
* Assess whether production de-duplication behavior or its time source should change.
* Explicitly assess hard-coded future dates and periodic fixture maintenance.
* Decide whether fake-store trigger-source fidelity belongs in the immediate fix.
* Select the smallest deterministic fix consistent with repository conventions.

## Verified evidence

The verified evidence is recorded in
`.copilot-tracking/research/subagents/2026-08-30/account-details-test-failure-evidence.md`.
Three isolated executions failed identically: the duplicate assertion expected
`Deferred` but received `Saved`.

The mismatch is direct:

* The test starts at
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:103`.
	It seeds the fixture day at line 106.
* The fixture hard-codes `2026-08-07` at line 154 but injects
	`TimeProvider.System` at line 162.
* The handler calculates the configured trading day from its injected clock at
	`src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:22`.
	Both de-duplication checks use that day at lines 23 and 36.
* The fixture's UTC schedule makes the handler's day `2026-08-30` on the
	verified run. The fake store requires exact day equality at
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:201`.
* The test therefore reaches the successful gateway and save path at
	`src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:41,79-93`.

The test has a second, independent fidelity gap. Its snapshot helper always
creates `Manual` snapshots at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:166`,
and its fake store ignores trigger source at line 201. The production EF store
only counts `Automatic` snapshots for this de-duplication check at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:19-20`.
This gap did not cause the observed failure because the dates already differed,
but it would let a fixed-date test pass for a scenario production rejects.

## Alternatives

### Alternative 1: Fixed test clock and derived trading day

Principles and flow: construct the fixture with a fixed UTC instant, inject a
small `TimeProvider` subclass into the handler, and derive the fixture's seeded
day from that same provider. Under the fixture's UTC schedule, the seed and
handler calculate the same date without consulting the wall clock.

| Criterion | Assessment |
|-----------|------------|
| Benefits | Tests the existing injected-clock contract, removes calendar drift, and makes the snapshot timestamp stable. It changes no production behavior. |
| Limitations | The fixture's direct UTC `DateOnly` derivation relies on its configuration remaining UTC. A future non-UTC fixture should derive through `TradingScheduleGate`, as the contract test does. |
| Implementation complexity | Low. One optional fixture constructor argument, one nested fixed provider, and replacement of the selected test's wall-clock timestamp are sufficient. |
| Test determinism | High. The result is independent of run date, machine clock, test order, and clock adjustment. |
| Midnight and timezone risk | None for this fixture. The fixed instant is noon UTC and the schedule timezone is UTC. The separate timezone contract remains covered at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsContractTests.cs:16-29`. |
| Repository alignment | Strong. Deterministic tests are required by `.github/instructions/tests.instructions.md:58-60`. A nearby application test uses an optional fixed instant and nested provider at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs:147-149,275-278`. |

### Alternative 2: Runtime-derived seed day

Principles and flow: read `TimeProvider.System.GetUtcNow()` or
`DateTimeOffset.UtcNow` during arrange, derive a UTC `DateOnly`, and seed that
day before invoking the handler, which independently reads the system clock.

| Criterion | Assessment |
|-----------|------------|
| Benefits | Very small edit and it normally follows the current date. It leaves production untouched. |
| Limitations | Arrange and act use separate clock reads. The test no longer has a stable input, and failures cannot be reproduced from source alone without recording the execution instant. |
| Implementation complexity | Very low. A local `now` and derived `DateOnly` replace the hard-coded day. |
| Test determinism | Medium to low. It usually passes but still depends on external wall-clock state. |
| Midnight and timezone risk | Non-zero. A run crossing UTC midnight between the seed calculation and handler read compares adjacent dates. Local timezone does not affect this UTC fixture, but changing the fixture timezone would make direct UTC derivation wrong near the configured local boundary. |
| Repository alignment | Weak. It avoids stale fixture data but does not satisfy the repository's deterministic-test intent as well as its established fixed-provider pattern. |

### Alternative 3: Change production behavior or time source

Principles and flow: possible changes include weakening or removing daily
de-duplication, counting manual snapshots as automatic successes, replacing the
injected provider with a static clock, or changing trading-day calculation from
the configured schedule timezone to UTC.

| Criterion | Assessment |
|-----------|------------|
| Benefits | None for the verified defect. A production change could only make this particular stale fixture pass by changing a valid product contract. |
| Limitations | It risks breaking the Phase 1.2 at-most-once automatic-capture rule, trigger semantics, or configured-timezone behavior. Static time access would also make handlers less testable. |
| Implementation complexity | Medium to high because behavior changes require product, unit, persistence, and likely integration-test review. |
| Test determinism | Poor if production moves to `DateTimeOffset.UtcNow`; unchanged if a different injected source is used. Neither repairs the fixture's mixed clocks by itself. |
| Midnight and timezone risk | Potentially high. UTC-only product logic would violate the demonstrated configured-timezone contract at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsContractTests.cs:16-29`. |
| Repository alignment | Poor. The API already registers `TimeProvider` at `src/TNC.Trading.Platform.Api/Program.cs:19`, and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Time/PlatformTimeProviderFactory.cs:7-24` supplies system or controlled incrementing time. The handler is already using the intended abstraction. |

Changing the EF predicate to count manual snapshots is also unsupported by the
test's stated requirement. Automatic capture is at-most-once after durable
automatic success; an operator's manual refresh should not silently consume
that automatic capture opportunity.

### Alternative 4: Future date or periodic fixture updates

Principles and flow: replace `2026-08-07` with a later literal, or update the
literal to the current date whenever the test fails.

| Criterion | Assessment |
|-----------|------------|
| Benefits | No new helper type. Periodic updates can produce a temporary green run when the literal exactly matches the run date. |
| Limitations | A future literal fails before that date as well as after it. A periodically updated literal is recurring maintenance, can fail on every unattended date transition, and conceals the mixed-clock defect. |
| Implementation complexity | Superficially trivial but operationally unbounded because updates recur. |
| Test determinism | None. Passing depends on the execution date matching source text. |
| Midnight and timezone risk | Maximum. The test is date-sensitive by design and still has boundary races if updated from local rather than configured schedule time. |
| Repository alignment | Rejected. It directly conflicts with deterministic, repeatable test guidance in `.github/instructions/tests.instructions.md:58-60`. |

## Trigger-source fidelity decision

Include the fake-store trigger-source correction in the immediate test-only
fix, but treat it as an orthogonal validity repair rather than part of the root
cause.

The selected test says that an existing successful automatic day is skipped.
Leaving the helper's seed as `Manual` and the fake predicate broader than the EF
predicate would make the fixed-clock test green while proving behavior that is
not present in production. The immediate patch should therefore:

* Seed the selected duplicate with `AccountDetailsTriggerSource.Automatic`.
* Let other snapshot-helper callers continue to default to `Manual`.
* Add the production `Automatic` condition to the in-memory existence check.

This addition remains in the same test file, adds no production behavior, and
does not alter the recommendation to use a fixed clock. A separate negative
test proving that a manual snapshot does not suppress automatic capture could
be added later if the contract needs more explicit regression coverage. It is
not required to repair the verified failure.

## Selected approach

Select Alternative 1 and include the trigger-source fidelity correction above.

This is the only option that addresses the root cause, preserves the product's
configured trading-day semantics, removes wall-clock dependence, and follows a
nearby repository pattern. Its blast radius is one unit-test file. Production
code and interfaces remain unchanged.

The fixed instant should be `2026-08-07T12:00:00Z`. Noon keeps the scenario
visibly away from a day boundary, while retaining the feature's original date.
The fixture should accept an optional instant so a later boundary test can
reuse the same setup without replacing its clock implementation.

## Minimal proposed patch

The following is a complete proposed change to the relevant portions of
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs`.
It is intentionally not applied during this research task.

```diff
@@ CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure
-        fixture.Store.Snapshots.Add(fixture.Snapshot(DateTimeOffset.UtcNow, fixture.TradingDay));
+        fixture.Store.Snapshots.Add(fixture.Snapshot(fixture.UtcNow, fixture.TradingDay, AccountDetailsTriggerSource.Automatic));

@@ private sealed class Fixture
-        public readonly DateOnly TradingDay = new(2026, 8, 7);
+        public readonly DateTimeOffset UtcNow;
+        public readonly DateOnly TradingDay;

-        public Fixture()
+        public Fixture(DateTimeOffset? utcNow = null)
         {
+            UtcNow = utcNow ?? new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
+            var timeProvider = new FixedTimeProvider(UtcNow);
+            TradingDay = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
             Gateway.Result = new AccountDetailsGatewayResult.Succeeded([Account]);
             Configuration = new PlatformConfigurationService(new FakeConfigurationStore());
             var gate = new TradingScheduleGate();
-            Refresh = new RefreshAccountDetailsHandler(Configuration, Gateway, Store, Lease, gate, TimeProvider.System);
+            Refresh = new RefreshAccountDetailsHandler(Configuration, Gateway, Store, Lease, gate, timeProvider);
             Get = new GetAccountDetailsHandler(Configuration, Store);
         }

-        public AccountDetailsSnapshot Snapshot(DateTimeOffset retrievedAt, DateOnly day) => new(Guid.NewGuid(), BrokerEnvironmentKind.Demo, retrievedAt, day, AccountDetailsTriggerSource.Manual, [Account]);
+        public AccountDetailsSnapshot Snapshot(DateTimeOffset retrievedAt, DateOnly day, AccountDetailsTriggerSource trigger = AccountDetailsTriggerSource.Manual) => new(Guid.NewGuid(), BrokerEnvironmentKind.Demo, retrievedAt, day, trigger, [Account]);
     }
+
+    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
+    {
+        public override DateTimeOffset GetUtcNow() => utcNow;
+    }

@@ private sealed class InMemoryStore
-        public Task<bool> ExistsForTradingDayAsync(BrokerEnvironmentKind environment, DateOnly tradingDay, CancellationToken cancellationToken) => Task.FromResult(Snapshots.Any(x => x.BrokerEnvironment == environment && x.TradingDay == tradingDay));
+        public Task<bool> ExistsForTradingDayAsync(BrokerEnvironmentKind environment, DateOnly tradingDay, CancellationToken cancellationToken) => Task.FromResult(Snapshots.Any(x => x.BrokerEnvironment == environment && x.TradingDay == tradingDay && x.TriggerSource == AccountDetailsTriggerSource.Automatic));
```

The existing test documentation already identifies the requirement, expected
outcome, and regression risk at lines 96-101, so no comment or method-name
change is needed.

## Remaining gaps and clarifying questions

* No retained historical result proves that the test passed on its introducing
	date, although source history and the literal date strongly support drift.
* The proposed patch was not applied or executed because this task is research
	only. Its first validation should be the same fully qualified filtered test,
	followed by the application unit-test project.
* The proposal derives `TradingDay` directly from UTC because the fixture
	configuration is UTC at
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:211`.
	If that configuration changes, the fixture should expose the schedule and
	derive through `TradingScheduleGate` instead.
* No clarifying question blocks implementation.