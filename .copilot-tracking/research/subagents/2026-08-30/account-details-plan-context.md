---
title: Account details plan context
description: Live implementation and repository convention evidence for planning the account-details automatic deduplication test fix
author: GitHub Copilot
ms.date: 2026-08-30
ms.topic: reference
---

## Status

Complete.

## Research questions

* What exact symbols and current line ranges control the failing account-details unit test?
* Which production EF snapshot-store behavior should the test use as its fidelity reference?
* What exact test-only edits are required by the selected recommendation?
* Is a separate manual-snapshot regression test required now or appropriate as follow-on work?
* Which test naming, documentation, validation, and wiki-update rules apply?
* Which same-day plan conventions or conflicts constrain the delivery plan?

## Findings

### Selected recommendation and live anchors

The selected recommendation remains valid against the live workspace. The fix
is test-only and belongs entirely in
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs`.
No product interface, handler, persistence adapter, schema, or package change is
needed.

The exact current anchors in the test file are:

* Lines 96-120 contain the documented test
  `CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure`.
* Line 106 seeds the duplicate with `DateTimeOffset.UtcNow` and
  `fixture.TradingDay` without specifying a trigger source.
* Lines 145-167 contain `Fixture`.
* Line 154 hard-codes `TradingDay` to `2026-08-07`.
* Lines 157-164 contain the parameterless fixture constructor and inject
  `TimeProvider.System` at line 162.
* Line 166 defines `Snapshot` and always creates a `Manual` snapshot.
* Lines 192-203 contain `InMemoryStore`.
* Line 201 defines `ExistsForTradingDayAsync` and checks only environment and
  trading day.

The production fidelity reference is
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs`.
Its `ExistsForTradingDayAsync` symbol is at lines 19-20. The EF predicate requires
the same environment and trading day plus
`TriggerSource == AccountDetailsTriggerSource.Automatic.ToString()`. The in-memory
test store must preserve this trigger predicate for the selected test to prove
production behavior.

The controlling product path remains
`src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs`.
`CaptureAutomaticAsync` is at lines 18-43. It derives `tradingDay` from the
injected `TimeProvider` at line 21, checks the store before and after lease
acquisition at lines 22-25 and 35-38, and saves an `Automatic` snapshot at line
40. This confirms that the mixed fixture clock, rather than product logic, owns
the failure.

### Exact edits required

Apply these changes as one atomic edit to `AccountDetailsHandlerTests.cs`:

1. At line 106, seed the duplicate with `fixture.UtcNow`,
   `fixture.TradingDay`, and `AccountDetailsTriggerSource.Automatic`.
2. In `Fixture`, add a `DateTimeOffset UtcNow` field and change `TradingDay` from
   a hard-coded initializer to a field initialized by the constructor.
3. Change `Fixture()` to `Fixture(DateTimeOffset? utcNow = null)` so existing
   callers remain source-compatible.
4. Default `UtcNow` to `2026-08-07T12:00:00Z`, construct a nested
   `FixedTimeProvider`, and derive `TradingDay` from that provider. Noon UTC
   avoids a day-boundary scenario, and direct UTC derivation is faithful while
   the fixture configuration remains UTC at lines 206-211.
5. Inject the fixed provider into `RefreshAccountDetailsHandler` instead of
   `TimeProvider.System`.
6. Add an optional `AccountDetailsTriggerSource trigger` parameter to
   `Snapshot`, default it to `Manual`, and pass it to `AccountDetailsSnapshot`.
   The default preserves the three history-read callers at lines 40-42.
7. Add a nested `FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider` after
   `Fixture`, overriding `GetUtcNow`.
8. Add `x.TriggerSource == AccountDetailsTriggerSource.Automatic` to the
   `InMemoryStore.ExistsForTradingDayAsync` predicate at line 201.

No other call site requires an edit. The optional fixture argument and snapshot
trigger default preserve all neighboring tests, while the selected test opts
into the production-relevant automatic trigger explicitly.

### Manual-snapshot regression scope

A separate negative regression test proving that a manual snapshot does not
suppress automatic capture is not required for the selected recommendation.
The confirmed task is to repair the date-drifting test and ensure that its
positive de-duplication case matches the production EF predicate. Adding the
negative test would improve explicit contract coverage, but it is independent
follow-on work and should not expand this focused fix.

The follow-on test, if separately approved, should use the existing fixed
fixture, seed a `Manual` snapshot for `fixture.TradingDay`, invoke
`CaptureAutomaticAsync`, and assert `Saved`, one gateway call, and both the
manual and automatic snapshots. Its name must follow the repository convention,
for example
`CaptureAutomaticAsync_ShouldSaveAutomaticSnapshot_WhenManualSnapshotExistsForTradingDay`.

### Test naming and documentation rules

`.github/instructions/tests.instructions.md` lines 29-45 require xUnit,
descriptive test files, `MethodName_StateUnderTest_ExpectedResult` names, and
XML documentation immediately above every test. Comments must record applicable
requirement traceability, what is verified, the expected outcome, and why the
behavior matters. `.github/instructions/csharp.instructions.md` also prohibits
`Arrange`/`Act`/`Assert` comments.

The existing selected test name already follows the naming convention. Its XML
comment at lines 96-101 records the Phase 1.2 trace, verified behavior, expected
outcome, and regression rationale. No rename or documentation edit is needed.
The private fixed-clock helper does not require public API documentation.

### Validation commands

Run the focused check immediately after the single-file edit:

```powershell
dotnet test "test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj" --no-restore --filter "FullyQualifiedName=TNC.Trading.Platform.Application.UnitTests.Features.AccountDetails.AccountDetailsHandlerTests.CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure" --logger "console;verbosity=normal"
```

Then run the entire affected project:

```powershell
dotnet test "test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj" --no-restore --nologo
```

The project targets `net10.0` and references only the Application project plus
its test packages, as shown in
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`
lines 1-22. `global.json` selects SDK `10.0.200`. `dotnet test` builds the touched
test project, so a separate build command is redundant for this one-file fix.
If assets have not been restored in a fresh environment, run the same commands
without `--no-restore`.

### Wiki documentation decision

No `docs/wiki` update is warranted. The fix changes neither implemented
behavior nor the documented testing approach. The existing runtime contract is
already accurate in `docs/wiki/runtime-behavior.md` lines 11-18: only the first
successful automatic capture for a configured trading day suppresses later
automatic requests, while failures remain retryable. The existing SQL coverage
summary in `docs/wiki/testing-and-quality.md` lines 164-170 also remains true.

Repository rules require wiki updates when completed plan work changes behavior,
architecture, API surface, runtime behavior, operator guidance, local
development, or testing approach. A deterministic fixture correction and fake
fidelity alignment change none of those surfaces.

### Tracking-plan conventions and conflicts

No `.copilot-tracking/plans/2026-08-30` directory or plan declaring
`2026-08-30` exists. The plan inventory currently ends at
`.copilot-tracking/plans/2026-08-17/trailing-stops-features-plan.instructions.md`,
so there is no same-day filename or scope collision.

Current conventions use:

* A date folder and kebab-case `*-plan.instructions.md` filename
* YAML `applyTo` frontmatter and a file-level markdownlint disable
* `Implementation Plan: ...` as the H1
* Overview, objectives, context, references, implementation checklist,
  planning log, dependencies, and success criteria sections
* Numbered checklist phases and steps
* `<!-- parallelizable: false -->` below each serial phase
* A paired details file under `.copilot-tracking/details/<date>/`
* A paired log under `.copilot-tracking/plans/logs/<date>/`

The closest remediation precedent is
`.copilot-tracking/plans/2026-06-30/web-authentication-test-failure-remediation-plan.instructions.md`.
For this task, the non-conflicting name is
`.copilot-tracking/plans/2026-08-30/account-details-automatic-deduplication-test-fix-plan.instructions.md`.
If the established multi-artifact workflow is used, pair it with matching
`account-details-automatic-deduplication-test-fix-details.md` and
`account-details-automatic-deduplication-test-fix-log.md` stems in their
respective date folders.

The original
`.copilot-tracking/plans/2026-08-07/account-details-plan.instructions.md` has
partially complete Phase 5 integration and final-validation work at lines
114-126. This focused unit-test correction neither completes nor reopens those
items. A new plan must not alter their status or claim hosted validation as part
of this fix.

## Recommended phase decomposition

### Phase 1: Apply the atomic deterministic-fixture correction

* Update only `AccountDetailsHandlerTests.cs` with all eight edits listed above.
* Keep the fixed clock and trigger-source fidelity changes in the same patch.
  Splitting them would temporarily leave the selected test either date-dependent
  or green against behavior that production does not implement.
* Run the exact fully qualified focused test before any further edit.

### Phase 2: Validate the affected project and close scope

* Run the complete Application unit-test project.
* Repair only regressions attributable to the test-file change, then rerun the
  same project command.
* Confirm that product code, other test projects, wiki pages, and the original
  Account Details plan remain unchanged.
* Record the manual-snapshot negative test as follow-on work rather than adding
  it to the selected fix.

## Parallelization constraints

Both phases are serial (`parallelizable: false`). All implementation edits touch
one test class and jointly define one valid fixture contract. The focused test
must pass before project-level validation begins, and project-level validation
must complete before closure. There is no independent production, documentation,
or second-test-file stream to delegate safely.

The optional manual-snapshot regression is logically independent follow-on work,
but it would touch the same test file. It should run after this fix lands, not in
parallel with it.

## Unresolved questions

No question blocks planning or implementation. The only evidence gap remains the
absence of a retained historical green run from 2026-08-07; the three isolated
current failures, unchanged blame, and mixed-clock code path are sufficient to
proceed.

## Recommended next research

* None required for the selected test-only fix.
* Optional follow-on: decide whether the manual-snapshot negative contract merits
  its own regression test after the confirmed failure is repaired.