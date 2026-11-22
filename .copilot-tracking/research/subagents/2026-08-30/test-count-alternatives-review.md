---
title: Test Count Alternatives Review
description: Critical review of the explanation for the VS Code test count mismatch
author: GitHub Copilot
ms.date: 2026-08-30
ms.topic: concept
---

## Research Questions

* Is the reconciliation of 661 initially displayed items with 515 passed out of 516 fully supported?
* Does the arithmetic distinguish hierarchy nodes, current executable tests, stale leaves, and skipped states correctly?
* Is direct VSTest inventory sufficient to establish the current executable count?
* Does the primary report overstate the cause of the original failure when its message is unavailable?
* Which alternative explanations remain viable, and what precise corrections are required?

## Status

Complete. The selected numerical explanation is the best-supported account of
the retained runs, but the primary report should narrow claims about the live
discovery tree, the meaning of state-5 hierarchy records, and the cause of the
original failure.

## Findings

### Arithmetic Is Correct

Both retained VS Code result files contain 662 item records. Their exact stored
task-state distribution is:

```text
1 controller root with state 0
515 URI-bearing leaves with state 3 (passed)
1 URI-bearing leaf with state 4 (failed)
142 URI-less containers with state 5
3 URI-bearing leaves with state 5
```

Removing the controller root leaves 661 records. Separately, the current case
inventory is 516, and the retained run has 515 passed leaves plus one failed
leaf. The reconciliation is therefore arithmetically sound:

```text
Persisted non-root records: 516 passed-or-failed leaves + 142 containers + 3 obsolete leaves = 661
Run denominator:             515 passed + 1 failed = 516
```

The 142 containers also reconcile internally as 10 project nodes, 20 grouping
nodes, 89 class nodes, and 23 theory-parent nodes. The source inventory of 422
facts and 94 inline-data rows independently totals 516. The 23 theory methods
are represented by their 94 cases, so theory expansion adds 71 cases to 445
source methods and does not explain the difference of 145.

### Hierarchy Nodes Can Carry State 5

The persisted evidence proves that C# Dev Kit and VS Code stored task state `5`
on all 142 URI-less hierarchy records. This representation is legitimate. The
VS Code API defines `TestItem` as either a suite or a test, exposes
`TestRun.skipped(TestItem)`, and does not restrict result-state methods to leaf
items. Its execution guidance also says providers need not manually set parent
states because VS Code manages them automatically.

The semantic label needs care. A container's stored own state of `5` is not
evidence that an executable test was skipped. The primary report is strongest
when it calls these records "state-5 hierarchy containers" or
"skipped-own-state containers," then immediately says they are not skipped test
outcomes. Calling all 145 records "skipped" without that qualification would be
incorrect.

### The Three Leaves Are Obsolete Result Records

The three state-5 leaves are materially different from the 142 containers:
they have source URIs and ranges but no duration or message. Direct VSTest
discovery from fresh assemblies excludes each old display identity and includes
its current replacement:

* The former account-preferences update test is absent, while
	`UpdateAccountPreferences_ShouldReturnNotAppliedAndPersistObservation_WhenReadbackDiffers`
	is present
* The former environment-policy test is absent, while
	`IsSupported_ShouldRequireTestPlatformAndDemoBroker_WhenCheckingFeatureEnvironment`
	is present
* The former `trailingStops` inline-data row is absent, while the current
	`trailingStopsEnabled` row is present

Source search found no current `Skip`, `Explicit`, conditional-test, skippable
test, or `Assert.Skip` usage. The retained request targeted the C# Dev Kit root
controller and had an empty exclusion list. These facts strongly distinguish
the three records from current declared or filtered skips.

The evidence proves stale or obsolete records in the persisted run snapshot.
It does not independently prove that the same records remained in the live Test
Explorer discovery tree at the moment the initial count was observed. The exact
match to 661 makes that linkage reasonable under the primary report's stated
assumption, but "persisted result-item count matching the observed 661" is more
precise than "persisted test-tree item count." Likewise, clearing results and
refreshing tests is a sensible verification step, not a proven outcome until a
post-refresh capture confirms the count.

### Direct VSTest Inventory Is Corroborating Authority

`dotnet vstest <assembly> /ListTests` is sufficient to enumerate the cases in
the supplied assemblies. It is not sufficient by itself to establish the
current source inventory because an assembly can be stale, output parsing can
be lossy, and discovery does not establish execution outcomes.

Here the 516 conclusion is adequately supported by combined evidence:

* All ten test projects are in the solution
* All ten inspected test DLLs are newer than the latest C# source in their
	project
* Direct per-assembly VSTest discovery totals 516
* Independent source metadata totals 422 facts plus 94 inline-data rows
* A recorded serial solution run also reports 516 total with no skips
* Both retained VS Code runs contain exactly 516 passed-or-failed current leaves

The primary report should call direct VSTest the executable assembly-inventory
authority, corroborated by build freshness, source metadata, and run evidence.
Its repeatable procedure should prefer a successful build immediately before
`/ListTests`, or a single test command that performs build and discovery, rather
than relying on pre-existing DLLs without a freshness check.

### Failed Identity Is Proven but Original Cause Is Not

Both retained runs prove that
`StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy` was
the failed leaf. They preserve state `4` and a 5 ms duration, but no message or
stack trace.

Current code contains a concrete scheduling race. `StartAsync` performs one
tick and starts `BackgroundService`; the execute loop can perform a second tick
before `StopAsync` cancellation wins. A later owning-project stress run failed
once in 20 attempts with expected count 1 and actual count 2. Historical TRX
files also show this exact test alternating between that assertion failure and
passing. This makes the race the leading and strongly supported explanation.

It still does not prove the immediate cause of either retained August 30
failure. Another exception path in the same test would produce the same stored
failed state once the message was lost. Statements that identify the failed
test are definitive; statements that attribute the original failure to the
two-tick race must use "likely," "consistent with," or equivalent language.

## Alternatives Review

| Alternative | Assessment | Reason |
|---|---|---|
| 145 current tests were skipped or filtered | Rejected | The request targeted the root with no exclusions; 142 records are URI-less containers, and the three leaves are absent from current discovery |
| A selected-node run omitted 145 tests | Rejected for the retained runs | The persisted request targets the controller root and has no exclusions |
| Theory expansion caused the mismatch | Rejected | It explains 71 additional executable cases, not 145 presentation records |
| Some test projects were omitted | Rejected | All ten projects are present and their assembly inventories sum to 516 |
| A canceled or incomplete 516th test caused 515 of 516 | Rejected | The remaining current leaf is explicitly stored as failed and the run is completed |
| All 145 extra records were stale tests | Rejected | Exactly 142 are URI-less hierarchy containers and only three are obsolete leaves |
| The retained 661 records did not produce the originally observed UI count | Still possible but weak | No screenshot or original output binds the observation to a specific run; the exact count match and two identical retained runs strongly support the linkage |
| The original failed test hit the known two-tick race | Leading hypothesis, not proven | Reproduction, code flow, and historical TRX support it, but the original message is missing |

## Selected Explanation

Select the primary report's count reconciliation with narrower provenance and
causal wording:

> The retained root-run snapshots each contain 661 non-controller records: 516
> current passed-or-failed leaves, 142 hierarchy containers whose stored own
> state is 5, and three obsolete state-5 leaves absent from fresh discovery.
> This exactly matches the observed initial 661 under the report's run-matching
> assumption. The completed-run denominator is the 516 current leaves, of which
> 515 passed and one failed. The failed identity is the authentication
> supervisor startup test. Its known two-tick scheduling race is the leading
> explanation for the failure, but the retained run does not preserve enough
> detail to prove that immediate cause.

This explanation accounts for every persisted record without treating
containers as executable skips or treating an unrecorded assertion message as
observed fact.

## Required Primary Report Changes

1. Replace "661 is a persisted test-tree item count" with "661 matches the
	 non-controller item count in each retained persisted root-run snapshot."
	 Preserve the stated assumption that the observation corresponds to those
	 runs.
2. Replace broad references to "145 skipped" with "145 records whose stored own
	 state is 5: 142 hierarchy containers and three obsolete leaf records." State
	 that container own state is not an executable skip outcome.
3. Call the three old identities "obsolete persisted result leaves" unless a
	 fresh Test Explorer capture proves they also remain in the live discovery
	 tree. Change "refresh should remove them" to a verification hypothesis.
4. Qualify VSTest as the authority for the supplied, fresh assembly inventory.
	 State that 516 is established by VSTest plus assembly freshness, source
	 metadata, retained run leaves, and the recorded complete run. Add a build or
	 freshness check to the verification procedure.
5. Change definitive causal wording such as "The failure was ... timing-sensitive
	 because" to "The failed test was ...; a known timing race is the leading
	 explanation." Retain the explicit limitation that the original message and
	 stack trace are unavailable.
6. Separate the two conclusions wherever they are currently joined: the failed
	 test identity is proven, while the expected-1/actual-2 cause is strongly
	 supported but inferred for the retained August 30 failures.

## Evidence

### Repository and Local Evidence

* .copilot-tracking/research/2026-08-30/manual-test-runner-count-mismatch-research.md
* .copilot-tracking/research/subagents/2026-08-30/repository-test-counts-research.md
* .copilot-tracking/research/subagents/2026-08-30/vscode-test-runner-semantics-research.md
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs
* src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs
* artifacts/quality-gates/phase-3-5-restart-classification/20260727-192141/trx/martynfewtrell_TNCITWOR001_2026-07-27_19_21_50_net10.0.trx
* .copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md
* VS Code workspace `testResults/e51e2b64-d489-4b1c-afc1-629781c7a45b.json`
* VS Code workspace `testResults/45d49018-2514-4320-8065-2b380822cc59.json`

### External References

* [VS Code Testing API](https://code.visualstudio.com/api/extension-guides/testing)
* [VS Code API TestItem and TestRun](https://code.visualstudio.com/api/references/vscode-api#TestRun)

## Follow-On Questions

No further research is required to select the best explanation. Two optional
captures would convert remaining assumptions into direct evidence:

* Capture the live Test Explorer count before and after clearing results and
	refreshing discovery
* Preserve the original Testing output or a TRX from a future recurrence so the
	immediate failure message is available