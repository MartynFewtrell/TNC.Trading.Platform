<!-- markdownlint-disable-file -->
# Task Research: Out-of-Schedule Authentication Transition Failure

Investigate the startup failure where authentication reconciliation attempts an `OutOfSchedule` to `OutOfSchedule` transition and throws `InvalidOperationException`.

## Task Implementation Requests

* Identify the verified root cause of the repeated authentication-state transition.
* Recommend one implementation approach, including focused tests and affected documentation.

## Scope and Success Criteria

* Scope: The authentication reconciliation path, transition rules, runtime-state persistence, startup invocation, schedule evaluation, and relevant automated tests and documentation. No production code changes are included.
* Assumptions: The supplied stack trace reflects the current workspace source; the failure occurred during API startup; repository-local behavior and conventions control the preferred fix.
* Success Criteria:
  * Explain why `TransitionToOutOfScheduleAsync` is called while the current state is already `OutOfSchedule`.
  * Determine whether idempotency belongs in reconciliation routing, transition application, or the state model.
  * Identify a focused regression-test strategy and implementation impact.

## Outline

1. Verified the fatal startup path and deterministic reason-rollover trigger.
2. Verified transition, persistence, side-effect, and serialization semantics.
3. Identified the missing composed regression case and historical integration mismatch.
4. Evaluated no-op, metadata-refresh, self-transition, and dedicated-policy alternatives.
5. Selected an in-reconciler metadata refresh and specified implementation, tests, and wiki updates.

## Potential Next Research

* Inspect the reported environment's persisted `BlockedReason` and startup-time schedule result.
  * Reasoning: Repository evidence establishes the deterministic defect; runtime evidence would identify the concrete reason rollover that triggered it.
  * Reference: User-supplied stack trace and `.copilot-tracking/research/subagents/2026-08-31/authentication-transition-code-path-research.md`.
* Evaluate duplicate explicit and hosted initial reconciliation separately.
  * Reasoning: It is redundant startup work, but it cannot cause the reported pre-`RunAsync` exception.
  * Reference: `src/TNC.Trading.Platform.Api/Program.cs:27-40` and `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:31-39`.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Api/Program.cs:27-40`
  * Runs a fatal authentication reconciliation before `RunAsync`; the hosted supervisor has not started when the reported exception occurs.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:164-189`
  * Loads durable state, evaluates the trading schedule, routes every inactive result to the out-of-schedule helper, and saves only after that helper succeeds.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:322-359`
  * Treats an inactive result as idempotent only when both status and reason match. A changed reason requests another `OutOfSchedule` transition and would repeat event/retry side effects if accepted.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:7-31,64-100`
  * Enforces an explicit state graph that rejects `OutOfSchedule -> OutOfSchedule`; the reported exception is the intended rejection text.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:7-49`
  * Produces distinct inactive reasons for bank holidays, inactive days, and times outside the active window, so the reason can legitimately change while the state remains inactive.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformRuntimeStateStore.cs:21-101`
  * Persists status and blocked reason separately in the single runtime-state row.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:763-815`
  * Covers a single `Active -> OutOfSchedule` reconciliation but not a second inactive reconciliation with a changed reason.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:5-49,88-129`
  * Encodes the graph without an out-of-schedule self-edge and verifies rejection behavior.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs:31-50,88-105,142-194`
  * Covers reason preservation and each inactive reason independently, but not their composition with persisted runtime state.

### Code Search Results

* Authentication reconciliation invocations
  * Fatal startup command: `src/TNC.Trading.Platform.Api/Program.cs:27-40`.
  * Hosted initial and periodic reconciliation: `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:31-76`.
  * Post-configuration-update reconciliation: `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:13-21`.
* Writer serialization
  * A static semaphore in `ReconcilePlatformAuthenticationHandler.cs:5-28` and SQL application lease in `SqlPlatformReconciliationLease.cs:8-66` serialize writers but do not make a same-state reason refresh legal.
* Git history
  * Commit `bd31f3d6` introduced the status-plus-reason guard on 2026-03-31.
  * Commit `8df68d8` added the rejecting transition graph on 2026-07-28 without adapting that guard, supporting an integration-contract mismatch.

### External Research

* None required. The defect and preferred semantics are fully controlled by repository-owned code and documented contracts.

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`, applicable scoped C# and test instructions, and Markdown authoring instructions.
* Instructions followed: Research-only changes are confined to `.copilot-tracking/research/`; all repository investigation is delegated to `Researcher Subagent`.
* Architectural alignment: Application owns reconciliation policy and state transitions; Infrastructure persists the resulting state; startup remains fail-fast on reconciliation errors.

## Key Discoveries

### Project Structure

Authentication schedule evaluation and reconciliation live in Application; EF persistence and the SQL reconciliation lease live in Infrastructure; API startup and supervision are adapters. The recommended repair should remain in the Application-owned reconciliation policy and preserve the transition engine's explicit graph.

### Implementation Patterns

The system distinguishes authentication status from `BlockedReason`, persists both, and uses an explicit transition engine to validate status changes. Reconciliation is serialized by both an in-process semaphore and a SQL application lock. Exact status-and-reason repeats are currently side-effect-free, while successful entry into `OutOfSchedule` emits `auth/TradingScheduleInactive` and closes a retry cycle when present.

The closest same-state precedent is the already-active branch at `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:395-403`: it refreshes `LastValidatedAtUtc` directly without changing `LastTransitionAtUtc`, recording an event, or sending a notification. The runtime model stores both timestamps independently at `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:11-45`, and EF persists them independently at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformRuntimeStateStore.cs:43-101`.

### Complete Examples

The cheapest full-path regression follows the existing in-memory EF and test-time-provider pattern in `AuthRetryCycleTests.cs:763-815`:

```text
1. Reconcile from Active during Friday after-hours, producing OutOfSchedule with a time-window reason.
2. Advance the test clock into Saturday, which remains inactive but produces a current-day reason.
3. Reconcile again.
4. Assert no exception, OutOfSchedule remains current, and the selected reason/timestamp/event semantics are applied once.
```

Recommended implementation shape, derived from the existing method:

```csharp
var now = timeProvider.GetUtcNow();

if (currentState.SessionStatus == PlatformSessionStatus.OutOfSchedule)
{
  currentState.BlockedReason = reason;
  currentState.LastValidatedAtUtc = now;
  return;
}

// Existing entry-transition logic continues with the same captured `now`.
```

The outer save at `PlatformAuthenticationReconciler.cs:188-189` persists the metadata refresh. The existing transition and side-effect block at lines 330-359 remains unchanged for actual entry into `OutOfSchedule`.

### API and Schema Documentation

No API schema change is required. The runtime row already stores authentication status, blocked reason, transition timestamp, and validation timestamp. The behavior contract is documented primarily in `docs/wiki/runtime-behavior.md:248-289,359-390`, with supporting ownership and startup semantics in `docs/wiki/architecture.md:154-180,341-390` and operator behavior in `docs/wiki/operator-guide.md:450-493`.

### Configuration Examples

No configuration shape change is required. Existing schedule rules can naturally produce the defect when crossing from a time-window exclusion to an inactive-day exclusion, or when bank-holiday configuration changes while authentication remains out of schedule.

## Technical Scenarios

### Preventing a Repeated Out-of-Schedule Transition

The verified defect is a contract mismatch. `TransitionToOutOfScheduleAsync` uses status plus reason to decide idempotency, while `PlatformStateTransitionEngine` treats status alone as the transition identity and rejects every `OutOfSchedule -> OutOfSchedule` edge. Distinct valid schedule reasons make that mismatch reachable without concurrency.

**Requirements:**

* Preserve valid transition enforcement.
* Make reconciliation safe when persisted state already matches the schedule-derived target.
* Avoid unnecessary side effects and notifications.
* Add a regression test reproducing startup reconciliation from `OutOfSchedule`.

**Preferred Approach:**

Keep `OutOfSchedule -> OutOfSchedule` invalid in the transition graph. In `TransitionToOutOfScheduleAsync`, handle every already-out-of-schedule result as a successful in-state validation:

* Set `BlockedReason` to the newly evaluated schedule reason.
* Set `LastValidatedAtUtc` to the reconciliation's single captured time.
* Preserve `LastTransitionAtUtc`, because no status transition occurred.
* Preserve session, degradation, and retry fields.
* Do not upsert or close a retry cycle.
* Do not emit another `TradingScheduleInactive` event.
* Do not dispatch a notification.

This approach fixes startup, keeps the operator-facing reason and freshness truthful, preserves transition timestamp semantics, and follows the existing active-state same-state refresh pattern. It requires no transition-engine, model, persistence, API, notification, host, or schema change.

**Focused Regression Tests:**

* Add `TickAsync_ShouldRefreshOutOfScheduleMetadataWithoutTransition_WhenInactiveReasonChanges` beside `AuthRetryCycleTests.cs:763-815`. Reconcile Friday after-hours, advance to Saturday, reconcile again, and assert current-day reason, advanced validation time, unchanged transition time, one inactive event, and unchanged retry/notification state.
* Add `TickAsync_ShouldRefreshValidationWithoutSideEffects_WhenOutOfScheduleReasonIsUnchanged`. Reconcile twice with the same inactive reason and assert only validation time advances.
* Add `Apply_ShouldRejectSelfTransition_WhenCurrentStateIsOutOfSchedule` beside the transition-engine rejection tests to make the strict-graph decision explicit and verify complete non-mutation.

**Startup Scope:**

Do not catch or suppress this exception in `Program.cs`; startup intentionally fails on genuine migration, persistence, configuration, and state-policy failures. Do not remove either initial reconciliation in this repair. The hosted initial tick occurs only after the explicit command succeeds, so duplication is separate architectural scope and not the cause. With metadata refresh semantics, the second invocation is harmless and side-effect-free.

**Documentation Impact:**

* `docs/wiki/runtime-behavior.md:248-321,359-390,510-537`: define reason rollover, timestamp semantics, unchanged graph, and entry-only inactive events.
* `docs/wiki/architecture.md:154-180,341-390`: preserve startup ownership and assign same-state metadata refresh to the reconciler.
* `docs/wiki/operator-guide.md:450-493`: state that the reason tracks the current inactive condition and normal rollover cannot block startup.
* `docs/wiki/testing-and-quality.md:54-72`: record the persisted reason-rollover regression coverage.

#### Considered Alternatives

**Return immediately for every existing `OutOfSchedule` state:** rejected because it leaves `BlockedReason` and `LastValidatedAtUtc` stale. Operators can see a time-window reason after the governing condition has changed to an inactive day or bank holiday.

**Permit `OutOfSchedule -> OutOfSchedule` in the transition graph:** rejected because it turns a metadata refresh into a state transition, advances `LastTransitionAtUtc`, emits another inactive event, and executes retry-cycle cleanup. Those side effects misrepresent what occurred.

**Add a dedicated metadata-refresh policy operation:** not selected because no reusable repository pattern or second caller justifies a new request/result contract. Reconsider if additional same-state metadata rules appear.

## Evidence-Based Conclusion

The morning failure is most plausibly a normal inactive-reason rollover against durable state, such as Friday after-hours to Saturday. The schedule remained inactive, but `TransitionToOutOfScheduleAsync` compared the reason as part of idempotency and submitted a self-transition that the newer strict state graph correctly rejected. This mismatch was introduced when commit `8df68d8` added the transition engine without adapting the pre-existing reason-sensitive guard from commit `bd31f3d6`.

The defect should be fixed at the reconciliation decision point, not by weakening startup handling or the transition graph. Treat continued inactivity as validation of the current state, refresh its current reason and validation timestamp, and reserve transition timestamps and side effects for actual status changes.

## Validation Record

* Existing `Active -> OutOfSchedule` infrastructure workflow test passed: 1 test.
* Complete transition-engine and schedule-gate test files passed: 29 tests.
* Both delegated research documents passed editor diagnostics; the alternatives document also passed whitespace validation.
* No product, test, configuration, or wiki files were modified during research.
