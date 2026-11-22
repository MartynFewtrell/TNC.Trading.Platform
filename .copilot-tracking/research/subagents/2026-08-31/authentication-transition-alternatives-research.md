---
title: Authentication Transition Alternatives Research
description: Phase 2 alternatives analysis for the OutOfSchedule startup failure
author: GitHub Copilot
ms.date: 2026-08-31
ms.topic: concept
---

## Research scope

Evaluate repair alternatives for the verified startup failure caused by an
`OutOfSchedule -> OutOfSchedule` request when the inactive schedule reason
changes. Research only; product and test files remain unchanged.

Status: Complete.

Questions:

* Should every existing `OutOfSchedule` result become a no-op regardless of a
  changed reason?
* Should the reconciler refresh changed out-of-schedule metadata without a state
  transition, and what should each timestamp and side effect mean?
* Should the transition graph permit `OutOfSchedule -> OutOfSchedule` and reuse
  the current transition side effects?
* Does the repository establish a superior distinct metadata-refresh pattern?
* Which implementation surface, regression tests, operational consequences,
  startup behavior, and wiki updates follow from each alternative?

## Evidence baseline

The verified root cause is a contract mismatch, not a concurrent-write race.
`PlatformAuthenticationReconciler` returns only when the current status and
reason both match. A changed inactive reason reaches the transition engine as
`OutOfSchedule -> OutOfSchedule`, which the graph rejects before persistence:

* `.copilot-tracking/research/subagents/2026-08-31/authentication-transition-code-path-research.md:25-55`
  traces the fatal startup path from `Program.cs` through reconciliation to the
  rejected graph edge.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:164-189`
  evaluates the schedule, routes inactivity to the out-of-schedule method, and
  saves only after that method returns.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:322-359`
  contains the reason-sensitive guard, retry reset, transition request,
  retry-cycle upsert, `TradingScheduleInactive` event, process-local notification
  marker cleanup, and current-cycle cleanup.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:7-31`
  rejects an invalid graph pair before mutating state.
  `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:64-75`
  omits the out-of-schedule self-edge.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:26-49`
  emits three legitimate inactive reasons in bank-holiday, current-day, and
  current-time-window precedence. A reason can therefore change while the
  schedule remains inactive.

The model already distinguishes validation from transition time.
`src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:11-45`
stores `BlockedReason`, `LastValidatedAtUtc`, and `LastTransitionAtUtc`
independently. The transition engine writes both timestamps together only when
it applies a graph edge
(`src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:20-29`).
By contrast, an already-active reconciliation updates validation time directly
without changing transition time, recording an event, or sending a notification
(`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:395-403`).
This is the closest established repository pattern.

Persistence can save the fields independently. The EF adapter maps both
timestamps and the reason in both directions
(`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformRuntimeStateStore.cs:43-73,75-101`).
The context exposes runtime state, retry cycles, events, and notifications to the
focused infrastructure test harness
(`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:8-28`).

History supports treating this as an integration defect. Focused blame found the
reason-sensitive guard and inactive event in `bd31f3d6` from 2026-03-31. Commit
`8df68d8` from 2026-07-28 replaced direct state mutation with the strict
transition engine and introduced the graph that omitted the self-edge. The
pre-refactor method allowed a changed reason by rewriting the state and both
timestamps, but that behavior predated the later semantic distinction enforced
by the graph. No commit evidence states that an inactive-reason rollover should
be a new state transition.

## Alternatives

### Approach A: Ignore every existing out-of-schedule result

Principles and flow:

* Change the current guard to return whenever `SessionStatus` is
  `OutOfSchedule`, without comparing the reason.
* Preserve all state, timestamps, retry data, events, and notifications.

Advantages:

* Removes the exception with the smallest code change.
* Preserves the strict graph and avoids repeated transition side effects.
* Leaves exact-repeat behavior unchanged.

Limitations:

* Leaves `BlockedReason` stale across ordinary time-window, weekend, and bank
  holiday boundaries even though it is the current operator-facing explanation.
* Leaves `LastValidatedAtUtc` stale despite the completed reconciliation. The
  wiki defines that field as projection freshness at
  `docs/wiki/runtime-behavior.md:306-321`.
* Can display a reason contradicted by current configuration and schedule policy.

Project-convention alignment:

* It aligns with idempotent suppression of side effects, but conflicts with the
  status projection's current-state purpose and the active-state validation
  refresh at
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:395-403`.

Implementation surface:

* One guard in
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:322-328`.
* One focused infrastructure regression test.

Regression tests:

* A Friday-after-hours to Saturday rollover would assert no exception but would
  have to assert that the old time-window reason remains. That expectation would
  codify stale state rather than current schedule truth.

Operational consequences:

* Startup succeeds, no retry cycle changes, no events are added, and no
  notifications are sent.
* Operators can receive inaccurate blocked-reason and freshness data until the
  schedule becomes active and later transitions out of schedule again.

Decision: reject. Avoiding an exception is insufficient when it makes the
persisted status projection knowingly stale.

### Approach B: Refresh metadata in the reconciler

Principles and flow:

* Keep the transition graph strict. A state transition means the status changes
  along a documented graph edge.
* When reconciliation finds the state already `OutOfSchedule`, update the
  current schedule reason and validation timestamp directly, then return before
  transition side effects.
* Treat the operation as in-state validation, matching the already-active path.

Exact semantics:

* `BlockedReason` becomes the newly evaluated inactive reason, even when it
  differs from the persisted value.
* `LastValidatedAtUtc` becomes the single `now` captured for that reconciliation.
  Updating it for exact repeats as well as changed reasons makes persisted
  freshness truthful and matches active-state reconciliation.
* `LastTransitionAtUtc` remains unchanged because no graph edge occurred.
* `SessionStatus`, `IsDegraded`, `EstablishedAtUtc`, and `ExpiresAtUtc` remain
  unchanged. A legitimately persisted out-of-schedule state already satisfies
  the transition engine's non-active invariants.
* Retry fields and `CurrentRetryCycleId` remain unchanged. The operation does not
  create, close, clear, or upsert a retry cycle. The actual entry transition has
  already reset retry state and cleared the current cycle at
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:330-359`.
* No operational event is written. `TradingScheduleInactive` remains an entry
  event, not a reason-refresh event. Current reason and freshness remain visible
  on the status projection.
* No notification is dispatched. The existing out-of-schedule path has no
  notification call, and the side-effect adapter exposes only recovery, failure,
  and blocked-live notification operations at
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs:78-109`.

Advantages:

* Fixes startup while preserving a truthful current reason and freshness time.
* Preserves the meaning of `LastTransitionAtUtc` and the documented graph.
* Avoids duplicate inactive events and retry-cycle side effects.
* Uses the nearest established same-state pattern rather than adding an
  abstraction for one branch.
* Requires no persistence schema, API contract, notification, or host changes.

Limitations:

* Operational event history does not show every inactive-reason rollover.
  Operators see the latest reason through status, while the original entry event
  remains the only inactivity event.
* The direct mutation is specific to this reconciler. If more same-state metadata
  operations emerge, a policy abstraction may later become worthwhile.
* It does not repair arbitrarily corrupted out-of-schedule retry fields. That is
  a data-integrity concern outside this verified defect.

Project-convention alignment:

* Strong. The strict transition policy remains the owner of graph edges and
  transition invariants, as documented at
  `docs/wiki/architecture.md:371-390`.
* Strong. The reconciler already directly applies runtime context at
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:620-625`
  and refreshes active-state validation at
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:395-403`.
* Strong. Status and events remain separate projections, with
  `LastValidatedAtUtc` serving as persisted freshness
  (`docs/wiki/runtime-behavior.md:306-321`).

Implementation surface:

* One branch in
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:322-328`.
* Focused additions in
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs`
  beside the existing schedule transition test at
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:763-815`.
* An explicit transition-policy assertion in
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs`
  may protect the decision that self-transition remains invalid.

Regression tests and operational consequences are specified in dedicated
sections below.

Decision: select.

### Approach C: Permit the self-edge and reuse transition side effects

Principles and flow:

* Add `(OutOfSchedule, OutOfSchedule)` to the graph at
  `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:64-75`.
* Leave the reconciler unchanged. A changed reason passes through the transition
  engine and then executes all existing out-of-schedule side effects.

Advantages:

* Restores the pre-refactor observable behavior for changed reasons with a small
  graph edit.
* Keeps authentication-field mutation inside the transition engine.
* Records a `TradingScheduleInactive` event for each reason change.
* Reuses existing retry-cycle cleanup when a non-null cycle is unexpectedly
  present.

Limitations:

* Sets `LastTransitionAtUtc` to the refresh time even though status did not
  transition. This weakens the timestamp's meaning.
* Treats an explanatory metadata change as a graph edge and expands the valid
  state machine for one caller's orchestration need.
* Executes retry-cycle upsert, event creation, and process-local notification
  marker cleanup on reason rollover
  (`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:340-359`).
* Reuses the generic `TradingScheduleInactive` event without distinguishing
  initial entry from reason refresh.

Project-convention alignment:

* Mixed. `Degraded -> Degraded` is an established graph edge for refreshed
  failure or retry details
  (`docs/wiki/runtime-behavior.md:359-390`), so self-edges are not categorically
  forbidden.
* Weak for this state. The active-state path demonstrates that successful
  same-state validation does not require a self-edge or transition side effects
  (`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:395-403`).

Implementation surface:

* One graph pair in `PlatformStateTransitionEngine`.
* Transition-engine theory coverage plus the full infrastructure rollover test.
* Wiki graph and timestamp semantics would need revision.

Regression tests:

* Add the self-edge to the valid-transition theory at
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:5-49`.
* Assert a reason rollover updates both timestamps and creates a second inactive
  event. Assert no notification because the path has no dispatch call.

Operational consequences:

* Startup succeeds and current reason is accurate.
* Every reason boundary rewrites transition time and emits another inactive
  event. A retry-cycle row could also be upserted if a cycle ID is present.
* Exact same-reason ticks still return early and create no side effects.

Decision: reject. It fixes the exception but conflates metadata freshness with a
state transition and carries side effects that are unnecessary for the defect.

### Approach D: Add a distinct metadata-refresh policy operation

Principles and flow:

* Add an operation such as `RefreshOutOfScheduleMetadata` to the Application
  transition policy, with a typed request/result that validates current status
  and reason shape before updating `BlockedReason` and `LastValidatedAtUtc`.
* Call it from the reconciler and keep transition side effects suppressed.

Advantages:

* Makes the distinction between transition and refresh explicit and testable in
  a pure Application unit test.
* Centralizes state-shape validation if several workflows later need the same
  operation.
* Prevents arbitrary direct mutation by callers.

Limitations:

* No existing reusable metadata-refresh operation was found. Repository search
  found direct timestamp refresh in the active-state branch and direct runtime
  context mutation in the reconciler instead.
* Adds a request/result contract and policy method for one private branch without
  removing meaningful duplication.
* Naming the existing `PlatformStateTransitionEngine` as owner of non-transition
  refreshes would broaden its stated responsibility.

Project-convention alignment:

* Conceptually compatible with explicit Application policies, but unsupported as
  an established pattern for this state model. The architecture says the engine
  owns transitions, while the reconciler owns orchestration and runtime updates
  (`docs/wiki/architecture.md:341-390`).

Implementation surface:

* `PlatformStateTransitionEngine`, a new inward-owned refresh request/result or
  method contract, the reconciler branch, Application unit tests, infrastructure
  regression tests, and architecture documentation.

Regression tests:

* Pure policy tests would cover valid out-of-schedule refresh, invalid source
  state, blank reason rejection, timestamp preservation, and non-mutation on
  rejection.
* The same infrastructure rollover test would still be required for persistence
  and side-effect semantics.

Operational consequences:

* Can match approach B exactly at runtime, but increases code and maintenance
  surface without additional current behavior.

Decision: do not recommend now. Reconsider only if a second caller or additional
same-state metadata rules make direct reconciler mutation meaningfully
duplicative.

## Selected approach

Select approach B: keep `OutOfSchedule -> OutOfSchedule` invalid and perform a
direct in-state metadata refresh in `TransitionToOutOfScheduleAsync`.

Critical rationale:

* A changed reason does not change `SessionStatus`; representing it as a graph
  edge would make `LastTransitionAtUtc` inaccurate.
* `BlockedReason` is current operator-facing state and must not remain stale.
* `LastValidatedAtUtc` is the documented freshness marker and should advance
  when reconciliation successfully validates continued inactivity.
* The existing active-state branch proves the repository already accepts direct
  same-state validation refresh without events, retry changes, or notifications.
* Suppressing transition side effects avoids duplicate inactive events and
  preserves retry-cycle meaning.

Rejected alternatives:

* Approach A preserves graph strictness but knowingly persists stale reason and
  freshness data.
* Approach C restores execution by weakening state and timestamp semantics and
  running unnecessary transition side effects.
* Approach D offers stronger encapsulation but has no established repository
  pattern or second caller to justify its additional contracts.

## Proposed implementation

The change should capture time once and handle all already-out-of-schedule
reconciliations as validation refreshes. The outer save at
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:188-189`
persists the mutation.

```csharp
private async Task TransitionToOutOfScheduleAsync(
    PlatformConfigurationSnapshot currentConfiguration,
    PlatformRuntimeState currentState,
    string reason,
    CancellationToken cancellationToken)
{
    var now = timeProvider.GetUtcNow();

    if (currentState.SessionStatus == PlatformSessionStatus.OutOfSchedule)
    {
        currentState.BlockedReason = reason;
        currentState.LastValidatedAtUtc = now;
        return;
    }

    var retryCycleId = currentState.CurrentRetryCycleId;
    currentState.LatestFailureSummary = null;
    currentState.RetryPhase = AuthRetryPhase.None;
    currentState.AutomaticAttemptNumber = 0;
    currentState.NextRetryAtUtc = null;
    currentState.RetryLimitReached = false;
    ApplyAuthenticationTransitionOrThrow(
        currentState,
        new AuthenticationStateTransition(
            PlatformSessionStatus.OutOfSchedule,
            reason,
            now));

    // Existing entry-transition retry and event side effects remain unchanged.
}
```

The shown comment represents omitted existing code, not a proposed source
comment. The implementation should retain the current side-effect block at
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:340-359`
verbatim after the transition request.

No transition-engine, model, persistence, API, notification, or database-schema
change is required.

## Proposed regression tests

Add the following focused cases. Each should use the repository's required XML
test documentation format demonstrated at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:763-815`.

### Primary workflow regression

`TickAsync_ShouldRefreshOutOfScheduleMetadataWithoutTransition_WhenInactiveReasonChanges`

1. Configure UTC, Monday through Friday trading days, excluded weekends, and an
   end of day before 17:00.
2. Start the test time provider at Friday 2026-04-03 17:00 UTC and tick once.
   Assert `OutOfSchedule` with the time-window reason.
3. Capture the persisted `LastValidatedAtUtc`, `LastTransitionAtUtc`, retry-cycle
   row count, inactive-event count, and notification count.
4. Advance to Saturday 2026-04-04 17:00 UTC and tick again. The schedule gate's
  precedence at
  `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:31-44`
  now produces the current-day reason while status remains inactive.
5. Assert no exception, status remains `OutOfSchedule`, and `BlockedReason` is
   `Trading schedule is inactive for the current day.`
6. Assert `LastValidatedAtUtc` equals Saturday test time and is later than the
   first value.
7. Assert `LastTransitionAtUtc` equals the Friday value.
8. Assert retry fields remain `None`, zero, null, and false;
   `CurrentRetryCycleId` remains null; retry-cycle row count is unchanged.
9. Assert the `TradingScheduleInactive` event count remains one and notification
   count is unchanged.

This belongs beside
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:763-815`.
The harness already supplies mutable test time, EF runtime persistence, event and
notification stores, and helpers at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:874-985`.

### Exact-repeat freshness regression

`TickAsync_ShouldRefreshValidationWithoutSideEffects_WhenOutOfScheduleReasonIsUnchanged`

1. Tick twice on the same inactive day with time advanced by one minute.
2. Assert reason and `LastTransitionAtUtc` remain unchanged.
3. Assert `LastValidatedAtUtc` advances to the second tick.
4. Assert only one inactive event exists and retry-cycle and notification counts
   do not change.

This case locks the deliberate improvement from the current exact-repeat return
at
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:322-328`.

### Strict-graph policy regression

`Apply_ShouldRejectSelfTransition_WhenCurrentStateIsOutOfSchedule`

1. Seed an out-of-schedule state with distinct validation and transition times.
2. Apply an `OutOfSchedule` transition with a new reason and later instant.
3. Assert rejection and complete non-mutation.

This focused Application test makes the selected contract explicit beside the
valid graph theory at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:5-49`
and the existing rejection/non-mutation test at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:88-129`.

No API supervisor test is required for the functional fix. The workflow test
exercises the exact persisted state combination, while supervisor tests use a
fake tick runner and cannot observe state metadata
(`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:49-72`).

## Startup scope assessment

Do not alter startup failure handling as part of this fix.

`src/TNC.Trading.Platform.Api/Program.cs:27-40` deliberately treats initialization
or initial reconciliation failure as fatal before `RunAsync`. The selected fix
removes the invalid request at its source. Catching or suppressing that exception
at startup would conceal other migration, persistence, configuration, and state
policy failures and conflict with the documented readiness contract at
`docs/wiki/operator-guide.md:480-493`.

Do not remove either initial reconciliation in the same change. The composition
root runs one explicit command before host startup
(`src/TNC.Trading.Platform.Api/Program.cs:27-35`), and the hosted service runs one
tick in `StartAsync` before starting its loop
(`src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:31-39,41-61`).
The hosted initial tick cannot race the explicit command because host startup
begins only after the explicit command succeeds. It is duplicate work, but not
the cause of the verified failure.

Changing that ownership affects readiness semantics and the test at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:49-72`.
Track it as a separate architecture decision. Under approach B, the second
startup reconciliation is harmless: it refreshes validation time and emits no
additional transition event or notification.

## Wiki updates

When the product fix and regression tests are implemented, update these pages in
the same work:

* `docs/wiki/runtime-behavior.md:248-289` should state that inactive reason
  precedence can change while the runtime remains out of schedule.
* `docs/wiki/runtime-behavior.md:291-321` should define same-state validation:
  current reason and `LastValidatedAtUtc` refresh, while `LastTransitionAtUtc`
  remains the instant of the last status change.
* `docs/wiki/runtime-behavior.md:359-390` should keep the graph unchanged and
  explicitly state that an out-of-schedule reason refresh is not a self-edge.
* `docs/wiki/runtime-behavior.md:510-537` should clarify that
  `TradingScheduleInactive` records entry into inactivity and is not duplicated
  for reason-only refreshes.
* `docs/wiki/architecture.md:154-180` should preserve the startup and supervision
  ownership contract.
* `docs/wiki/architecture.md:341-390` should record that the reconciler owns
  same-state schedule metadata refresh while the transition policy remains
  responsible for graph edges and transition invariants.
* `docs/wiki/operator-guide.md:450-493` should tell operators that an
  out-of-schedule reason tracks the current inactive condition and that a normal
  reason rollover must not prevent startup.
* `docs/wiki/testing-and-quality.md:54-72` should add the persisted rollover
  regression to reconciliation coverage. The supervisor's hosting tests remain
  unchanged.

No API-reference update is required because the response shape does not change.

## Validation performed

* Inspected the verified Phase 1 report and current reconciler, transition policy,
  schedule policy, state model, EF adapter, side-effect adapter, startup host,
  supervisor, focused tests, and affected wiki sections.
* Searched product code for all direct writes to `BlockedReason`,
  `LastValidatedAtUtc`, and `LastTransitionAtUtc`. No reusable same-state metadata
  refresh operation was found.
* Inspected focused Git history and blame for the out-of-schedule method and
  transition graph, including the immediate pre-`8df68d8` implementation.
* Editor diagnostics reported no errors after the initial research-document
  creation.
* Product and test files were not modified.

## Recommended next research

* [ ] Confirm production data or logs identify the concrete old and new inactive
  reasons involved in the reported startup.
* [ ] Decide whether inactive-reason changes need a separate audit event in a
  future observability enhancement. Current evidence favors no event for this
  repair.
* [ ] Evaluate duplicate initial reconciliation ownership as a separate
  architecture task after the functional regression is fixed.

## Clarifying questions

None block implementation. The selected semantics are derived from the current
state model, active-state refresh precedent, strict graph, and side-effect
boundaries. Product ownership may separately choose to audit inactive-reason
changes, but that is not required to repair the startup failure safely.