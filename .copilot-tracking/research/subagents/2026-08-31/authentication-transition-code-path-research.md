---
title: Authentication Transition Code Path Research
description: Evidence for the startup OutOfSchedule self-transition exception
author: GitHub Copilot
ms.date: 2026-08-31
ms.topic: troubleshooting
---

## Research scope

Investigate the startup exception `Authentication state cannot transition from
'OutOfSchedule' to 'OutOfSchedule'.` without changing product or test files.

Status: Complete.

Questions:

* What exact startup and reconciliation control flow attempts the self-transition?
* Which state rules, persistence adapters, schedule decisions, side effects, and
  concurrency protections govern that path?
* Which neighboring tests cover or omit the reported state combination?
* What falsifiable root-cause hypothesis and cheap regression check follow from
  the evidence?
* Which `docs/wiki` pages would a future fix affect?

## Findings

### Executive finding

The exception is reproducible in principle without concurrency. A durable
`OutOfSchedule` state is treated as idempotent only when its persisted
`BlockedReason` exactly equals the newly evaluated schedule reason. The schedule
policy can legitimately change that reason while remaining inactive, for example
from "current time window" on Friday evening to "current day" on Saturday. The
reconciler then requests `OutOfSchedule -> OutOfSchedule`, but the explicit state
graph rejects that pair.

### Exact startup and reconciliation flow

1. `src/TNC.Trading.Platform.Api/Program.cs:27-35` initializes schema,
   configuration, and retention, creates a scope, resolves
   `ReconcilePlatformAuthenticationHandler`, and awaits the initial command.
  `src/TNC.Trading.Platform.Api/Program.cs:36-40` treats any exception as fatal
  and rethrows it before `RunAsync`.
2. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandler.cs:5-28`
   serializes the command through a static `SemaphoreSlim` and invokes
   `IPlatformAuthenticationReconciler.ReconcileAsync`.
3. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:90-95`
   acquires the reconciliation lease and enters `ReconcileCoreAsync`.
4. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:164-184`
  loads the durable runtime state, resolves runtime configuration, evaluates
  the schedule, and routes an inactive schedule to
  `TransitionToOutOfScheduleAsync`.
5. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:322-328`
  returns only when both the state is already `OutOfSchedule` and the persisted
  reason is ordinal-equal to the newly evaluated reason.
6. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:330-338`
  clears retry fields and calls `ApplyAuthenticationTransitionOrThrow` with
  target `OutOfSchedule` when the status matches but the reason differs.
7. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:764-772`
  delegates to the transition engine and throws its rejection reason.
8. `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:7-31`
   checks the graph before mutating authentication-state fields.
  `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:64-75`
  permits `OutOfSchedule` to move to `Active`, `Degraded`, or `Blocked`, but not
  to itself. It therefore returns the reported exception text.
9. Because the throw occurs before
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:188-189`,
  the runtime state is not saved and startup fails. The retry-field mutations
  made to the detached application model before the transition call are also
  not persisted.

### Schedule decision and normal trigger

`src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:26-49`
evaluates inactive conditions in this precedence order and emits distinct
reasons:

* configured bank holiday
* inactive current day
* outside the current time window

`src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:7-18`
turns every inactive result into `BlockedBySchedule` while preserving its
reason. Therefore these ordinary sequences can trigger the defect:

* Friday after-hours persists "current time window"; Saturday startup evaluates
  "current day"
* A weekday bank-holiday configuration is added while an after-hours reason is
  persisted; the next reconciliation evaluates "configured bank holiday"
* A bank-holiday exclusion is removed while the date remains outside the time
  window; the reason changes but the state remains inactive

### State model and persistence

* `src/TNC.Trading.Platform.Application/Configuration/PlatformSessionStatus.cs:3-10`
  defines `Unknown`, `Active`, `Degraded`, `OutOfSchedule`, and `Blocked`.
* `src/TNC.Trading.Platform.Application/Services/AuthenticationStateTransition.cs:5-10`
  carries target status, blocked reason, transition time, and optional active
  session interval.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:77-100`
  requires non-active target states to have a nonblank blocked reason and no
  active session interval.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:3-45`
  holds the durable application model, including status and reason as separate
  mutable fields.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformRuntimeStateStore.cs:21-41`
  creates one default `Unknown` row when absent. Lines 43-73 overwrite the one
  row on save, and lines 75-101 map persisted status and reason back into a new
  application object.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AuthRuntimeStateEntity.cs:3-47`
  persists the fields. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:58-69`
  limits status strings and the blocked reason but defines no row-version or
  optimistic concurrency token.

### Side effects and idempotency

On a successful transition to out-of-schedule,
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:340-359`
updates an existing retry cycle if one exists, writes one
`auth/TradingScheduleInactive` operational event, forgets the process-local
degraded-notification marker, and clears the current retry cycle. It does not
dispatch a notification. An exact status-and-reason repeat returns before all
side effects.

`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs:13-45`
skips retry-cycle persistence when no cycle exists. Lines 47-76 persist and log
operational events. Notification methods at lines 78-109 cover recovery,
failure, and blocked-live outcomes only.

Concurrency protections are present but do not repair this defect:

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandler.cs:5-28`
  uses a process-wide static writer semaphore.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:90-95`
  acquires an outward lease for the complete reconciliation.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlPlatformReconciliationLease.cs:8-45`
  takes a SQL Server session-owned exclusive `sp_getapplock` with zero wait.
  Lines 47-66 release it and close the connection.

These protections serialize writers. They do not make a reason update within
the same state legal or suppress it.

### Relevant invocation paths

* Fatal initial command: `src/TNC.Trading.Platform.Api/Program.cs:27-40`
* Hosted initial and one-second supervision ticks:
  `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:31-39,41-61,69-76`
* Post-commit configuration reconciliation:
  `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:13-21`
* The reconciler's `TickAsync`, status, and event methods also reconcile at
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:33-35,75-80,97-100`.
  Current product call-site search found the command handler as the production
  entry point and these methods primarily exercised directly by tests.

Current status and event product handlers do not call those legacy reconciler
methods. `docs/wiki/runtime-behavior.md:306-321` documents them as pure
projection reads with no reconciliation, notification, state creation, or write
behavior.

The hosted service does not race the explicit `Program.cs` command in the
reported startup path because hosted services start during `RunAsync`, which is
reached only after the explicit command succeeds. The duplicate initial hosted
tick is still a separate invocation once host startup begins.

### Focused automated tests and missing case

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:5-49`
  enumerates the documented valid graph. It intentionally omits
  `OutOfSchedule -> OutOfSchedule` while allowing `Degraded -> Degraded`.
  Lines 88-129 verify rejection and non-mutation only for `Active -> Unknown`.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs:31-50`
  verifies inactive reason preservation. Lines 88-105, 142-176, and 178-194
  verify the three distinct inactive reasons and their precedence separately.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:763-815`
  verifies one transition from `Active` to `OutOfSchedule` for the time-window
  reason. It does not reconcile again while inactive and does not change the
  inactive reason.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:157-192`
  provides a neighboring persisted-state, recreated-coordinator test pattern,
  but only for degraded notification replay.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:49-72`
  proves the hosted adapter awaits one initial tick. It uses a fake runner and
  does not exercise persisted out-of-schedule state.

No focused test found covers an already persisted `OutOfSchedule` state whose
new inactive schedule evaluation produces a different reason. Local test names
use `MethodName_ShouldExpected_WhenCondition` or the equivalent
`MethodName_WhenCondition_ShouldExpected`. Every neighboring test has an XML
`summary` with `Trace`, `Verifies`, `Expected`, and `Why` lines; there are no
Arrange/Act/Assert comments.

### Falsifiable root-cause hypothesis

The root cause is a contract mismatch introduced when the explicit transition
engine was added: `TransitionToOutOfScheduleAsync` treats only an exact
status-plus-reason repeat as idempotent, while the transition graph treats every
`OutOfSchedule -> OutOfSchedule` request as illegal. A normal change between two
inactive schedule reasons bypasses the guard and deterministically throws.

This hypothesis is falsified if a focused test seeds or produces
`OutOfSchedule` with one reason, advances or reconfigures the schedule so it
remains inactive with another reason, and a second `TickAsync` does not throw at
the transition engine.

The cheapest regression check belongs beside the existing infrastructure unit
test in
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:763-815`:
use its in-memory EF context and test time provider, first produce the
time-window out-of-schedule state, advance into an excluded weekend day (or
alter a bank-holiday exclusion), invoke a second tick, and assert no exception
plus the intended reason/timestamp/event semantics. A smaller
application-policy test could prove graph behavior, but it would not resolve
whether the desired contract is "same-state reason refresh" or "reconciler
no-op," including side effects.

### Historical evidence

Focused blame shows the status-plus-reason guard originated in commit
`bd31f3d6` on 2026-03-31. Commit `8df68d8` on 2026-07-28 introduced both the
transition engine call and its graph during the Clean Architecture refactor.
The graph omitted `OutOfSchedule -> OutOfSchedule`, while the pre-existing
reason-sensitive guard remained unchanged. This supports an integration
mismatch rather than a newly intended schedule rule.

### Validation performed

* The focused existing infrastructure test
  `GetStatusAsync_ShouldShowOutOfScheduleProjectionWithoutFailureSummary_WhenTradingScheduleBecomesInactive`
  passed (1 test). It covers `Active -> OutOfSchedule` with one reason.
* The complete
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs`
  and
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs`
  files passed (29 tests). They confirm the graph rejects the omitted self-edge
  by construction and that all documented schedule reason outcomes remain green
  independently.
* Editor diagnostics reported no errors in this research document.
* No product or test files were modified.

### Wiki pages affected by a future fix

* `docs/wiki/runtime-behavior.md:248-289` documents schedule precedence and all
  three inactive reasons; lines 359-390 document the transition graph. This is
  the primary page for defining whether an inactive reason refresh is a state
  transition or an idempotent reconciliation update.
* `docs/wiki/architecture.md:154-180` documents serialized reconciliation and
  blocking startup; lines 341-390 document reconciler ownership, side effects,
  and the SQL lease.
* `docs/wiki/operator-guide.md:450-493` defines `OutOfSchedule`, supervision,
  and startup failure behavior. Update it if operator-visible reason refresh or
  startup recovery semantics change.
* `docs/wiki/testing-and-quality.md:54-72` describes supervisor tests and
  reconciliation coverage. Update it when the rollover regression is added.

## Risks and ambiguities

* The intended semantics for a changed reason while remaining out-of-schedule
  are not explicit. Possible contracts include updating the reason and
  validation timestamp without emitting another transition event, recording a
  fresh informational event, or treating the reason as derived projection data.
* Merely allowing all self-transitions in the engine could duplicate
  `TradingScheduleInactive` events and retry-cycle side effects on each reason
  boundary. Merely returning for every existing `OutOfSchedule` state would
  leave a stale operator-visible reason.
* The explicit fatal `Program.cs` reconciliation and the hosted service's own
  blocking initial reconciliation are duplicated startup work. This does not
  cause the reported exception, but any fix should preserve the documented
  readiness and fatal-startup contract.
* The runtime row lacks optimistic concurrency, but the semaphore plus SQL
  application lock are the intended writer controls. Bypassing the command or
  using the no-op lease in tests would weaken that protection; no evidence found
  that such bypass causes this startup failure.

## Recommended next research

* [ ] Decide the desired same-state reason-refresh semantics, especially timestamp,
  event, and retry-cycle behavior.
* [ ] Add the focused red regression test before selecting an implementation.
* [ ] Check production data or the captured startup log for the persisted old
  `BlockedReason` and newly evaluated schedule condition to confirm the concrete
  rollover pair.
* [ ] Evaluate whether one of the two initial startup reconciliations should remain
  after the functional defect is fixed; treat this as separate scope.

## Clarifying questions

* Should a changed inactive reason be persisted immediately while remaining
  `OutOfSchedule`, or should the reason remain the one associated with the last
  actual state transition?
* Should that reason refresh emit a new `TradingScheduleInactive` operational
  event, and should `LastTransitionAtUtc` remain unchanged?