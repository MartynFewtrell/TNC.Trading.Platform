---
title: Out-of-Schedule Plan Verification Research
description: Verifies current codebase anchors for an actionable OutOfSchedule startup reconciliation implementation plan
ms.date: 2026-08-31
ms.topic: reference
---

## Research Questions

* Which current methods own out-of-schedule reconciliation, transition validation, runtime-state mutation, and persistence?
* Which test fixtures, helpers, time controls, assertions, naming rules, comments, and insertion points support focused regression coverage?
* Which sections in the four named wiki pages need implementation-aligned updates?
* Which repository-established scoped build, test, formatting, and Markdown validation commands should an implementation plan use?
* Which applicable repository instructions constrain the implementation and tests?
* Does the supplied preferred metadata-refresh approach remain correct against the current workspace?

## Status

Complete.

## Findings

### Production Ownership

The current code confirms the supplied root cause and ownership boundary.

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:90-100`
    exposes lease-serialized reconciliation and the test-facing tick wrapper.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:164-190`
    owns the complete reconciliation decision. It loads durable state, resolves
    runtime configuration, captures `now` once at line 168, evaluates the schedule
    with that instant, routes `BlockedBySchedule` to the out-of-schedule helper at
    line 179, and saves the returned state at lines 188-189.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:322-359`
    owns out-of-schedule orchestration. The current guard returns only when status
    and reason both match. A changed reason clears retry fields, requests another
    `OutOfSchedule` transition, upserts an existing retry cycle when present,
    writes `auth/TradingScheduleInactive`, clears the process-local degraded
    notification marker, and clears the current retry-cycle identifier.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:395-403`
    is the nearest same-state precedent. Continued `Active` reconciliation updates
    `LastValidatedAtUtc` directly without changing transition time or invoking
    transition side effects.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:764-772`
    applies a transition and converts a typed rejection into the reported
    `InvalidOperationException`.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:7-31`
    validates the graph and transition shape before mutating any auth-state field;
    accepted transitions set both validation and transition timestamps to the
    transition instant.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:64-75`
    permits `OutOfSchedule` to move to `Active`, `Degraded`, or `Blocked`, but not
    to itself. The self-edge remains intentionally absent.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:77-100`
    requires `OutOfSchedule` and other non-active targets to carry a nonblank
    blocked reason and no active session interval.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:26-49`
    can legitimately change the reason while remaining inactive. Its precedence
    is bank holiday, inactive day, then inactive time window.

The runtime model and persistence path already support an in-state refresh with
no schema or contract change.

* `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:3-43`
    stores mutable `BlockedReason`, `LastValidatedAtUtc`, and
    `LastTransitionAtUtc` independently, along with retry and session fields.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AuthRuntimeStateEntity.cs:3-45`
    has corresponding independent persisted columns.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformRuntimeStateStore.cs:21-41`
    creates and immediately saves the singleton runtime row when absent.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformRuntimeStateStore.cs:43-73`
    copies every runtime field, including reason and both timestamps, into the
    tracked entity and calls `SaveChangesAsync`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformRuntimeStateStore.cs:75-101`
    maps those values back into a fresh Application runtime model.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs:13-45`
    makes retry-cycle upsert a no-op when there is no cycle identifier.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs:48-76`
    persists operational events.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs:80-109`
    exposes recovery, failure, and blocked-live notifications only. The
    out-of-schedule path does not dispatch a notification.

The smallest production edit is confined to
`PlatformAuthenticationReconciler.cs:164-179,322-359`:

1. Pass the `now` already captured by `ReconcileCoreAsync` into
    `TransitionToOutOfScheduleAsync`.
2. At the start of that helper, handle every existing `OutOfSchedule` state by
    assigning the current reason and `LastValidatedAtUtc = now`, then return.
3. Use the same supplied `now` for a real entry transition.
4. Leave the existing retry reset and side-effect block unchanged for genuine
    status changes.

The outer save at lines 188-189 persists both changed-reason and exact-repeat
validation refreshes. No change belongs in the transition engine, runtime model,
EF adapter, entity, startup host, notification dispatcher, or database schema.

### Test Anchors

`AuthRetryCycleTests` is the correct composed regression surface because it
constructs the actual reconciler with in-memory EF persistence, recorded
notifications, event storage, and a deterministic clock.

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:763-815`
    is the existing `Active -> OutOfSchedule` projection test. It proves one
    time-window transition but does not tick again while inactive.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:817-871`
    begins the next blocked-live scenario. The exact insertion area for two
    continued-inactivity tests is between lines 815 and 817.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:18-27,80-88,763-773`
    demonstrates the required XML `summary` convention: `Trace`, `Verifies`,
    `Expected`, and `Why` immediately precede each test.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:874-880`
    creates a recorded notification dispatcher using the supplied time provider.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:883-921`
    builds bootstrap configuration. Its defaults explicitly include all seven
    trading days and `IncludeFullWeekend`.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:923-977`
    creates protected credentials, SQL-backed configuration, and the reconciler
    with EF state, snapshot, retry-cycle, and event stores plus a no-op lease.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:979-986`
    exposes notification and operational-event records for assertions.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:1041-1047`
    defines the mutable `TestTimeProvider`; `Advance` changes the instant without
    sleeping.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:503-674`
    repeatedly demonstrates loading persisted runtime state through
    `EfPlatformRuntimeStateStore.GetOrCreateAsync` for direct field assertions.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:20-76,112-157`
    demonstrates direct retry-cycle, notification, and event count or absence
    assertions.

Add these tests between the existing out-of-schedule and blocked-live cases:

* `TickAsync_ShouldRefreshOutOfScheduleMetadataWithoutTransition_WhenInactiveReasonChanges`
* `TickAsync_ShouldRefreshValidationWithoutSideEffects_WhenOutOfScheduleReasonIsUnchanged`

The changed-reason test should start at Friday 2026-04-03 after the configured
end time and configure Saturday 2026-04-04 as
`Bootstrap:TradingSchedule:BankHolidayExclusions:0`. The first tick then yields
the time-window reason; advancing into Saturday yields the higher-precedence
bank-holiday reason. This works with the existing all-days fixture without
changing shared helpers. A Friday-to-Saturday current-day test would not work
by overriding only `WeekendBehavior`, because Saturday remains explicitly
present in the default `TradingDays` collection and
`TradingScheduleGate.cs:55-68` accepts an explicitly configured day before
consulting weekend behavior.

For each workflow test, call `TickAsync` directly, load persisted runtime state,
and assert:

* no exception and status remains `OutOfSchedule`
* reason is current
* `LastValidatedAtUtc` equals the advanced test time
* `LastTransitionAtUtc` remains the first tick's value
* retry fields and `CurrentRetryCycleId` remain unchanged
* `AuthRetryCycles` remains empty or at its captured count
* exactly one `TradingScheduleInactive` event remains
* `NotificationRecords` remains empty or at its captured count

The exact-repeat test should advance by one minute while staying in the same
inactive condition. It specifically locks the intentional change from the
current return-without-validation behavior.

`PlatformStateTransitionEngineTests` should retain the strict graph decision.

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:6-49`
    defines the valid graph theory and intentionally omits the out-of-schedule
    self-edge.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:51-78`
    verifies successful mutation and timestamp behavior.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:80-119`
    verifies rejection and full non-mutation for `Active -> Unknown`.
* The exact insertion area is after line 119 at the end of the class. Add
    `Apply_ShouldRejectSelfTransition_WhenCurrentStateIsOutOfSchedule`, with the
    same XML comment format and assertions that status, reason, session interval,
    degraded flag, validation time, and transition time are all unchanged.

Both project files target `net10.0`, enable nullable reference types, use xUnit,
and reference the required production assembly at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:1-24`
and
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj:1-25`.
Application internals are already visible to both test assemblies at
`src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs:3-8`.

### Documentation Anchors

The four supplied wiki pages remain the correct documentation set.

* `docs/wiki/runtime-behavior.md:248-288`: extend trading schedule outcomes and
    reason examples to state that the reason can change while inactivity
    continues.
* `docs/wiki/runtime-behavior.md:289-334`: define continued inactivity as an
    in-state validation refresh that updates reason and validation freshness.
* `docs/wiki/runtime-behavior.md:366-393`: retain the graph exactly as drawn;
    explicitly state that `OutOfSchedule -> OutOfSchedule` is not an edge and
    does not advance `LastTransitionAtUtc`.
* `docs/wiki/runtime-behavior.md:514-557`: define
    `TradingScheduleInactive` as an entry event, not a reason-refresh event.
* `docs/wiki/runtime-behavior.md:558-599`: confirm that continued inactivity
    adds no notification attempt.
* `docs/wiki/architecture.md:161-180`: preserve fatal initial reconciliation and
    hosted supervision ownership; clarify that normal reason rollover no longer
    produces an invalid request.
* `docs/wiki/architecture.md:321-423`: assign same-state reason and validation
    refresh to the reconciler while retaining graph-edge and transition-invariant
    ownership in `PlatformStateTransitionEngine`.
* `docs/wiki/operator-guide.md:444-464`: clarify that `OutOfSchedule` displays
    the current governing inactive reason.
* `docs/wiki/operator-guide.md:465-487`: add an out-of-schedule troubleshooting
    subsection between `Degraded` and `Missing`, explaining normal reason rollover
    and the absence of retry or notification side effects.
* `docs/wiki/operator-guide.md:488-523`: retain genuine fail-fast startup
    guidance while distinguishing normal continued-inactive reconciliation from
    migration, initialization, persistence, and policy failures.
* `docs/wiki/testing-and-quality.md:13-73`: mention focused reconciliation
    coverage without changing supervisor-hosting scope.
* `docs/wiki/testing-and-quality.md:105-139`: retain pure Application policy
    coverage for the strict graph.
* `docs/wiki/testing-and-quality.md:140-232`: record the in-memory EF reason
    rollover, persistence, event, retry, notification, and timestamp assertions
    under Infrastructure unit coverage.
* `docs/wiki/testing-and-quality.md:400-411`: preserve descriptive method names
    and requirement-traceability comments.
* `docs/wiki/testing-and-quality.md:412-488`: retain the existing focused and
    complete validation workflow.

### Validation Commands

The repository pins SDK `10.0.200` with `latestFeature` roll-forward in
`global.json:1-6`. The following commands are established in repository docs or
scripts and are appropriate to an implementation plan.

Focused tests, using the repository's established project-qualified commands
and `FullyQualifiedName~...` filter form:

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AuthRetryCycleTests"
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~PlatformStateTransitionEngineTests"
```

Project-level regression tests already listed at
`docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:238-243`:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj
```

The solution gate is implemented at
`.github/scripts/Invoke-CleanArchitectureQualityGate.ps1:1-96` and documented at
`docs/wiki/testing-and-quality.md:412-445`:

```powershell
.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId out-of-schedule-reconciliation
```

The script runs these solution-qualified commands and writes logs plus TRX
results below `artifacts/quality-gates/<GateId>/<timestamp>/`:

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx
```

The documented serialized fallback is at
`docs/wiki/testing-and-quality.md:438-445`:

```powershell
dotnet test TNC.Trading.Platform.slnx -m:1
```

No checked-in `dotnet format` invocation, `.markdownlint` configuration,
`package.json`, `scripts/linting` directory, frontmatter validation script, or
executable Markdown link checker exists in the current workspace. The external
Markdown authoring instruction references `npm run validate:frontmatter` and
`pwsh scripts/linting/Validate-MarkdownFrontmatter.ps1`, but neither command is
available in this repository. Do not put those commands in an actionable plan
as if they are runnable. For changed wiki pages, the currently available checks
are editor diagnostics, trailing-whitespace inspection, and manual validation
of affected relative links. This is a repository tooling gap, not an
implementation blocker.

### Applicable Instructions

The implementation plan should cite and apply these current rules:

* `.github/copilot-instructions.md`: keep business policy in Application,
    preserve startup orchestration, use required test naming and full XML test
    documentation, update affected wiki pages before completion, and prefer
    repository scripts for repeatable validation.
* `.github/instructions/csharp.instructions.md:20-79`: follow `.editorconfig`,
    preserve nullable and async behavior, handle errors intentionally, add tests
    for behavior changes, and do not add Arrange/Act/Assert comments.
* `.github/instructions/tests.instructions.md:25-95`: use xUnit, keep unit tests
    in-process and in-memory, use `MethodName_StateUnderTest_ExpectedResult`
    naming, add `Trace`/verification/expected/risk comments, and keep tests
    deterministic without sleeps.
* `.github/instructions/architecture.instructions.md:28-166`: keep use-case
    policy independent of persistence and hosting, keep decisions in Application,
    test policy independently where possible, avoid speculative abstractions, and
    validate build and tests.
* `.github/instructions/refactoring.instructions.md:20-75`: keep the edit small,
    preserve public contracts, cancellation and exception semantics, add
    behavior-focused regression coverage, avoid unrelated cleanup, and run the
    relevant build and tests.
* `.github/instructions/dotnet-stack.instructions.md:20-90`: use the SDK pinned
    by `global.json`, retain SQL Server as the persistence boundary, and preserve
    explicit Application policy and Infrastructure adapter responsibilities.
* `.github/instructions/auth.instructions.md:20-78`: do not weaken auth safety,
    add time-based waits, expose secrets, or change identity-provider behavior.
    The proposed repair does not alter OIDC, OAuth, Keycloak, or Microsoft Entra
    ID configuration.
* `.github/instructions/configuration.instructions.md:20-49`: preserve the
    SQL-backed operator configuration boundary and secret handling. No
    configuration shape change is needed.
* `.github/instructions/docs.instructions.md:20-70`: update implemented wiki
    behavior, keep semantic headings and relative links, and validate affected
    navigation.
* `.github/instructions/aspire-tests.instructions.md:18-66`: this file applies by
    glob to tests, but its Aspire-specific rules are not triggered. Both proposed
    tests remain in unit projects and launch no AppHost, process, container,
    listener, or external boundary.
* `.github/instructions/folders.instructions.md`: keep production and tests in
    the existing roots. The proposed work creates no new project or top-level
    folder.

The Infrastructure workflow tests are correctly classified as unit tests by
`docs/wiki/testing-and-quality.md:74-86,140-160`: they use EF Core's in-memory
provider and direct adapters, not real SQL Server or Aspire. No separate
repository instruction file exists specifically for Infrastructure unit tests.

### Preferred Approach Verification

The supplied preferred approach remains correct: continued inactivity should be
handled in `PlatformAuthenticationReconciler` as a metadata refresh, while
`PlatformStateTransitionEngine` keeps rejecting the self-edge.

It satisfies all current ownership and behavioral constraints:

* `BlockedReason` tracks the current schedule decision.
* `LastValidatedAtUtc` advances after every successful reconciliation.
* `LastTransitionAtUtc` remains the time of the real status transition.
* retry and session fields are not rewritten during a same-state refresh.
* no retry-cycle upsert, event, or notification occurs for reason rollover or an
    exact repeat.
* the existing outer save persists the refresh.
* genuine invalid transition failures still propagate and can still fail
    startup.

Two planning corrections are required:

1. The supplied sample captures `now` inside
    `TransitionToOutOfScheduleAsync`, but `ReconcileCoreAsync` already captures
    the authoritative instant at line 168 for schedule evaluation. Pass that
    value into the helper so evaluation and persisted validation use exactly one
    instant.
2. The supplied Friday-to-Saturday test assumes weekdays-only configuration,
    while the local helper explicitly seeds all seven days. Use the bank-holiday
    rollover described above, or add a dedicated full configuration builder that
    truly omits weekend days. Overriding weekend behavior alone is insufficient.

## Research Gaps and Conflicts

* No product-behavior gap remains for an actionable plan. The owning edit,
    persistence path, regression fixture, side-effect assertions, docs sections,
    and validation projects are all identified.
* The concrete production rollover pair is still unverified because no reported
    runtime row or startup log showing the old and newly evaluated reasons was
    supplied. This does not block the deterministic code-level fix.
* The explicit pre-`RunAsync` reconciliation and hosted initial reconciliation
    remain duplicate work. Current evidence confirms they do not race in the
    reported failure, so changing them would be unrelated scope.
* Repository Markdown instructions conflict: the external rule requires
    frontmatter validation commands that are absent, while
    `.github/instructions/docs.instructions.md` requires an H1 as first content
    and the external instruction requires frontmatter before content. Existing
    wiki structure should be preserved; no heading/frontmatter normalization
    belongs in this functional change.
* There is no repository-established formatter command or automated Markdown
    validator. Record editor diagnostics and manual link/whitespace checks rather
    than claiming unavailable automation.

## Clarifying Questions

None required for the implementation plan. The supplied preferred semantics are
internally consistent and supported by current repository behavior.

## References

* `.copilot-tracking/research/2026-08-31/out-of-schedule-authentication-transition-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/authentication-transition-code-path-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/authentication-transition-alternatives-research.md`
