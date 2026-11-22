<!-- markdownlint-disable-file -->
# Task Research: Manual Test Runner Count Mismatch

The VS Code Testing view initially showed 661 items, then reported 515 passed out of 516. Retained VS Code root-run snapshots reconcile those values as different kinds of count: 661 result-tree items versus 516 current executable test cases. The snapshots do not independently prove the exact composition of the live Test Explorer tree at the instant it was observed.

## Task Implementation Requests

* Explain the difference between the initial discovery count and the completed-run count.
* Ground the explanation in repository configuration, test metadata, runner behavior, and reproducible evidence.

## Scope and Success Criteria

* Scope: All ten solution test projects, xUnit and VSTest configuration, VS Code C# Dev Kit result storage, source test metadata, historical TRX evidence, and focused reproductions
* Assumptions: “Test runner” refers to the VS Code Testing view and the observed run corresponds to the matching retained root-run result files from 2026-08-30
* Success Criteria:
  * Account for 661 in the retained run snapshots as 516 executable leaves, 142 hierarchy nodes, and 3 obsolete persisted leaves
  * Account for 516 as 422 facts plus 94 expanded inline-data rows
  * Identify the one failed case behind 515 passed out of 516
  * Distinguish direct evidence from the narrower inference supplied by a later reproduction

## Outline

1. Reconcile the persisted VS Code tree and result-state counts.
2. Verify the executable inventory independently through VSTest and source metadata.
3. Identify and reproduce the non-passing test.
4. Evaluate filtering, skipping, data expansion, stale discovery, and hierarchy accounting as alternatives.
5. Recommend a repeatable verification procedure.

## Potential Next Research

* Capture the original Testing output if it remains available.
  * Reasoning: The persisted result identifies the failed test but contains no assertion message; the later reproduction cannot prove the original failure had the same message.
  * Reference: VS Code workspace test result records described below

## Research Executed

### File Analysis

* TNC.Trading.Platform.slnx:11-43
  * The solution includes all ten test projects found under test/.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:58-71
  * The failed test starts the supervisor, immediately stops it, and expects exactly one tick.
* src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:32-48
  * StartAsync performs the initial tick before starting BackgroundService; ExecuteAsync can then perform a second tick before StopAsync cancellation wins scheduling.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:1-4
  * The only xUnit runner configuration disables collection parallelism and does not filter or skip tests.
* global.json:1-6
  * The repository uses the VSTest path; no Microsoft.Testing.Platform runner selection is configured.

### Code Search Results

* `[Fact]`, `[Theory]`, and `[InlineData]`
  * Source contains 422 fact methods, 23 theory methods, and 94 inline-data rows. The executable count is 422 facts plus 94 theory rows, or 516 cases.
* `Skip`, `Explicit`, `Trait`, and conditional test attributes
  * No source-declared skips, explicit tests, traits, categories, or conditional test attributes explain the discrepancy.
* Test configuration files
  * No repository runsettings, testsettings, workspace settings, test filters, or additional test-adapter extension were found.

### External Research

* VS Code Testing API
  * Discovery is a provider-owned hierarchy of TestItem objects. A test run is a separate request whose result denominator need not equal the number of items in the hierarchy.
  * Source: [VS Code Testing API](https://code.visualstudio.com/api/extension-guides/testing)
* C# Dev Kit testing
  * Test recognition depends on builds and Refresh Tests triggers build and rediscovery, which is relevant to the three stale leaves.
  * Source: [Testing with C# Dev Kit](https://code.visualstudio.com/docs/csharp/testing)
* VSTest options
  * Discovery and execution selection are separate operations; filters affect execution rather than ListTests inventory.
  * Source: [VSTest.Console options](https://learn.microsoft.com/visualstudio/test/vstest-console-options)
* xUnit runner configuration
  * xUnit pre-enumerates theory data by default for the Visual Studio adapter, so each InlineData row is an executable case.
  * Source: [xUnit runner JSON configuration](https://xunit.net/docs/config-xunit-runner-json)

### Project Conventions

* Standards referenced: .github/copilot-instructions.md, .github/instructions/tests.instructions.md, and .github/instructions/folders.instructions.md
* Instructions followed: Research-only changes are confined to .copilot-tracking/research/; no product or test source was modified

## Key Discoveries

### Exact Count Reconciliation

The two retained VS Code root-run result files each contain 662 records. One record is the C# Dev Kit controller root, leaving 661 non-controller result-tree items, matching the observed initial number.

| Count | Meaning |
|---:|---|
| 661 | All retained root-run snapshot items except the controller root |
| 516 | Current executable cases: 422 facts plus 94 InlineData rows |
| 142 | Non-executable hierarchy containers: 10 projects, 20 groups, 89 classes, and 23 theory parents |
| 3 | Obsolete state-5 result leaves from renamed or changed Account Preferences tests |
| 515 | Executable cases recorded as passed |
| 1 | Executable case recorded as failed |

The arithmetic is therefore:

```text
Retained snapshot: 516 executable + 142 hierarchy + 3 obsolete = 661
Run result:       515 passed + 1 failed = 516
```

The 145-item difference is not 145 tests omitted from execution. In each retained snapshot it is 142 structural tree containers plus three obsolete test identities. The containers carry persisted state 5, but they are not skipped executable tests.

### Executable Inventory

Direct `dotnet vstest <assembly> /ListTests` discovery against fresh built assemblies produced these counts. Assembly timestamps, matching source metadata, persisted results, and a recorded complete solution run corroborate that 516 represented the current source suite rather than only an old binary inventory.

| Project | Cases |
|---|---:|
| API Integration | 29 |
| API Unit | 57 |
| AppHost Unit | 40 |
| Application Unit | 128 |
| Architecture Integration | 13 |
| Infrastructure Integration | 22 |
| Infrastructure Unit | 112 |
| Web E2E | 1 |
| Web Functional | 6 |
| Web Unit | 108 |
| Total | 516 |

All test projects reference Microsoft.NET.Test.Sdk 17.14.1, xUnit 2.9.3, and xunit.runner.visualstudio 2.8.2. Theory expansion is already included in the 516 cases.

### Failed Test and Likely Timing Race

Both retained VS Code runs identify the same failed executable test:

```text
PlatformAuthenticationSupervisorTests.StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy
```

The persisted VS Code results recorded its failed state and 5 ms duration but did not retain the assertion message. A repeated API unit-project run later reproduced this test failing once in 20 runs:

```text
Failed: 1, Passed: 56, Skipped: 0, Total: 57
Assert.Equal() Failure: Values differ
Expected: 1
Actual:   2
```

The exact test passed 10 of 10 isolated runs. This pattern and the implementation expose a scheduling race. `StartAsync` performs one tick, starts the background service, and the background loop can perform a second tick before `StopAsync` cancellation takes effect. The test passes when cancellation wins and can fail with a count of two when the loop wins.

Historical TRX evidence shows the same test both passing and failing with this assertion in earlier runs. A separate serial solution gate also passed all 516, which is consistent with flakiness rather than contradictory evidence. Because the original VS Code snapshots lack assertion messages, the race is the leading explanation for those failures, not a proven immediate cause.

### Obsolete Persisted Leaves

The three obsolete leaves are old identities for Account Preferences tests whose names or inline data changed. They no longer occur in current VSTest discovery and have no duration or message. This proves they are obsolete persisted results, but does not prove they remained as live discovery nodes. Clearing results and refreshing discovery should remove any corresponding stale presentation state.

## Technical Scenarios

### Persisted Test Tree Versus Executable Run

VS Code uses a hierarchical test model. Projects, grouping nodes, classes, and theory parents can all be result-tree entries even though only leaf cases execute. A retained result inventory can therefore be larger than the run denominator.

**Requirements:**

* Count only current executable leaves when validating suite size.
* Treat hierarchy and stale discovery state separately from skipped test outcomes.
* Inspect result details to identify the state of any non-pass.

**Preferred Approach:**

* Use fresh-build VSTest discovery as the executable-count authority and use VS Code result details to identify per-run outcomes. Refresh the Testing view when its presentation contains obsolete leaves.

```text
Current executable authority: 516 direct VSTest cases
Observed VS Code outcome:      515 passed, 1 failed
Snapshot-only extras:          142 hierarchy containers, 3 obsolete leaves
```

**Implementation Details:**

```powershell
$projects = Get-ChildItem test -Recurse -Filter '*Tests.csproj' | Sort-Object FullName
$total = 0
foreach ($project in $projects) {
    $assembly = Join-Path $project.DirectoryName "bin/Debug/net10.0/$($project.BaseName).dll"
    $output = & dotnet vstest $assembly /ListTests 2>&1
    $count = @($output | Where-Object { $_ -match '^    \S' }).Count
    $total += $count
    "$($project.BaseName)|$count"
}
"TOTAL|$total"
```

Expected total: 516.

#### Considered Alternatives

* Solution omission is rejected because all ten test projects are in the solution and direct per-assembly discovery totals 516.
* Filters or runsettings are rejected because none exist in repository or user configuration relevant to the run.
* Source-declared skips are rejected because no applicable skip or conditional metadata exists.
* Theory expansion is not the 145-item difference. It changes 445 source methods into 516 executable cases, an increase of 71.
* Treating all 145 extras as stale tests is rejected. Persisted IDs classify 142 as URI-less hierarchy nodes and only three as stale leaves.
* A canceled or undiscovered 516th test is rejected because persisted state explicitly marks it failed.

## Selected Approach and Recommended Verification

Interpret 661 as the matching retained VS Code result-tree inventory and 516 as the runnable suite. The final summary is accurate: 515 current cases passed and one current case failed. The failed identity was the authentication supervisor startup test. Its known timing race is the most likely cause, but the original assertion message is unavailable.

1. Record the live Test Explorer count, run **Test: Clear All Results**, then **Refresh Tests**, and record the count again to verify whether obsolete live nodes disappear.
2. Rerun all tests and inspect the Test Results details. The current runnable denominator should remain 516 unless source tests changed.
3. Confirm the inventory independently with direct VSTest discovery; expect the per-project counts above to total 516.
4. Preserve a TRX for future complete runs so pass, failure, skip, and execution counters survive VS Code state cleanup.
5. During remediation, synchronize the supervisor test's initial-tick/background-loop handoff, then validate through repeated API unit-project runs rather than isolated-test runs alone.

## Evidence Limitations

* The original VS Code result retained the failed test identity but not its assertion message. The later one-in-20 reproduction strongly supports the same timing race but does not prove the original assertion text was identical.
* VS Code workspace result files are mutable local state. Clearing results or reloading can remove the stale identities that produced the original 661 display.
* No source remediation was attempted because this task is research-only.
