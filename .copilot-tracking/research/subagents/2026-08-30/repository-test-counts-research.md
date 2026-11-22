---
title: Repository test counts research
description: Evidence-based investigation of VS Code test discovery and execution count differences
author: GitHub Copilot
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Status

Complete. Research finished on 2026-08-30. No product or test source was modified.

## Scope

Investigate why VS Code initially displays 661 tests and later reports 515 passed out of 516 for this workspace. Determine the meaning of 661, 516, 515, the 145-test difference, and the single non-passing result.

The investigation covers:

* Solution and test-project membership
* Test SDKs, frameworks, and adapters
* Run settings and VS Code test settings
* Traits, categories, skip metadata, data-driven tests, and conditional tests
* Local runner logs and artifacts
* Focused `dotnet test` discovery and execution reproduction

No product or test source code will be modified.

## Research questions

1. Which projects and test assemblies participate in solution-level discovery and execution?
2. Which framework, adapter, and runner versions control discovery and reporting?
3. Does configuration filter, suppress, duplicate, or defer any tests?
4. How many test cases does each project discover and execute?
5. What produces the counts 661, 516, 515, and the difference of 145?
6. What is the identity and result state of the single non-passing test?

## Evidence

### Count reconciliation

The observation combines test-tree items with executable test cases:

| Number | Meaning |
|---:|---|
| 661 | Every persisted C# Dev Kit tree item except the controller root: 516 executable leaves, 142 hierarchy ancestors, and 3 stale leaves |
| 516 | Current executable xUnit cases: 422 facts plus 94 inline-data rows |
| 515 | Executable cases that passed in each inspected VS Code run |
| 145 | Items omitted from the execution denominator: 142 hierarchy ancestors plus 3 stale leaves |
| 1 | One failed executable fact, `StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy` |

The latest two persisted full-run result files each contain 662 JSON items. One is the C# Dev Kit controller root. Excluding it produces the displayed 661. Task-state counts are 515 passed, 1 failed, and 145 skipped-own-state. The executable denominator is therefore `515 + 1 = 516`; the skipped-own-state tree items are excluded.

The 145-item difference is not 145 missing tests. Its exact hierarchy distribution is:

```text
10 project nodes
20 intermediate grouping nodes
89 class nodes
23 theory parent nodes
3 stale leaf nodes
145 total non-executed tree items
```

The 516 executable leaves independently reconcile to source metadata. The repository contains 422 `[Fact]` methods, 23 `[Theory]` methods, and 94 `[InlineData]` rows. xUnit exposes each fact and each theory row as a runnable case, so `422 + 94 = 516`. At source-method level there are `422 + 23 = 445` methods; replacing 23 theory methods with 94 rows adds 71 executable cases. Data-driven expansion therefore explains 71, not the observed 145.

The persisted result hierarchy confirms 421 passed fact leaves, 94 passed theory-row leaves, and 1 failed fact leaf. These sum to 516. The 23 theory methods remain non-executing parent nodes.

### Solution and project membership

`TNC.Trading.Platform.slnx:11-43` includes all ten test projects found under `test/`; no additional test project was found outside the solution. Current direct discovery counts are:

| Test project | Cases | Project evidence |
|---|---:|---|
| API Integration | 29 | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:4-27` |
| API Unit | 57 | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:4-22` |
| AppHost Unit | 40 | `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:4-27` |
| Application Unit | 128 | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:4-22` |
| Architecture Integration | 13 | `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj:4-14` |
| Infrastructure Integration | 22 | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:4-26` |
| Infrastructure Unit | 112 | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj:4-23` |
| Web E2E | 1 | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:4-23` |
| Web Functional | 6 | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:4-23` |
| Web Unit | 108 | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:4-27` |
| Total | 516 | Direct VSTest discovery |

All ten target `net10.0` and reference `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3, and `xunit.runner.visualstudio` 2.8.2. Web E2E adds `Microsoft.Playwright.Xunit` 1.58.0; Web Functional adds `Microsoft.Playwright` 1.58.0. The API Integration project alone copies `xunit.runner.json`; that file only disables collection parallelism at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:1-4`.

Direct assembly discovery reports VSTest 18.7.0. `global.json:1-6` requests SDK 10.0.200 with `latestFeature`; the active CLI selected `10.0.400-preview.0.26322.102`. This preview roll-forward affected one `dotnet test --list-tests` attempt but does not change the direct VSTest count or persisted VS Code result arithmetic.

### Configuration and metadata

No repository `*.runsettings`, `*.testsettings`, `.vscode/settings.json`, or `*.code-workspace` file exists. The user-level VS Code settings file contains no test, VSTest, runsettings, filter, or C# Dev Kit test setting. No central `Directory.Build.*` or `Directory.Packages.props` file was found.

No `[Trait]`, `[ConditionalFact]`, `[ConditionalTheory]`, `SkippableFact`, `SkippableTheory`, `TestCategory`, or `Skip =` metadata exists under `test/**/*.cs`. The 145 skipped-own-state entries are therefore not source-declared skipped tests.

The three stale leaves in the persisted run are:

* `UpdateAccountPreferences_ShouldConfirmWithAuthoritativeGet_WhenPutSucceeds`, formerly at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:42`; current source has `UpdateAccountPreferences_ShouldReturnNotAppliedAndPersistObservation_WhenReadbackDiffers` at lines 40-58
* `IsSupported_ShouldAllowDemoAndRejectLive_WhenCheckingFeatureEnvironment`, formerly at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesEnvironmentPolicyTests.cs:25`; current source has `IsSupported_ShouldRequireTestPlatformAndDemoBroker_WhenCheckingFeatureEnvironment` at lines 21-27
* `GetAsync_ShouldRejectMalformedJson_WhenTrailingStopsIsNotBoolean(payload: "{\"trailingStops\":\"true\"}")`, formerly one row under `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:32-37`; current rows use `{}` and `trailingStopsEnabled`

These three account for only three of the 145. The other 142 are current hierarchy nodes, not stale tests.

One unrelated adapter presentation collision exists: two long inline-data cases at `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs:43-49` are truncated to the same VSTest display string. Direct discovery still gives them distinct cases, and neither is the recorded failed result. This collision does not explain 515 of 516.

### The one non-passing result

Both persisted VS Code runs, named `Test run at 8/30/2026, 5:11:23 PM` and `Test run at 8/30/2026, 5:17:53 PM`, identify the same failed leaf:

```text
StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy
test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:58-71
VS Code duration: 5 ms
```

The exact owning-project reproduction failed once in 20 runs:

```text
ITERATION=3 EXIT=1
Failed: 1, Passed: 56, Skipped: 0, Total: 57
Assert.Equal() Failure: Values differ
Expected: 1
Actual:   2
PlatformAuthenticationSupervisorTests.cs:line 71
```

The other 19 project runs passed. Ten isolated executions of the exact fact also passed. The result is a timing-sensitive test failure.

The race follows directly from current code. The test calls `StartAsync`, immediately calls `StopAsync`, and expects one tick at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:58-71`. Production `StartAsync` performs the initial tick and then calls `base.StartAsync`; `ExecuteAsync` starts `RunUntilStoppedAsync`, whose first loop action performs another tick at `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:32-48`. If `StopAsync` cancellation wins scheduling, the count remains 1. If the background loop starts first, the count becomes 2. The injected delay cancels only after that second tick at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:64-67` and `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:105-116`.

Historical TRX files show this fact usually passing and occasionally failing with the same assertion. Examples include a failure at `artifacts/quality-gates/phase-3-5-restart-classification/20260727-192141/trx/martynfewtrell_TNCITWOR001_2026-07-27_19_21_50_net10.0.trx:15-21` followed by a pass at `artifacts/quality-gates/phase-3-5-restart-classification/20260727-192445/trx/martynfewtrell_TNCITWOR001_2026-07-27_19_24_53_net10.0.trx:31`.

### Local runner and artifact evidence

The current VS Code workspace storage is `C:\Users\martynfewtrell\AppData\Roaming\Code\User\workspaceStorage\d380cd56fde34a0ec47784a74216e247`. Its `testResults` directory contains the two full-run JSON files used above:

```text
e51e2b64-d489-4b1c-afc1-629781c7a45b.json  completed 2026-08-30T17:14:10
45d49018-2514-4320-8065-2b380822cc59.json  completed 2026-08-30T17:20:44
```

Each persisted file independently reports the same 662-item and state distribution. The active C# Dev Kit test service log is under the current VS Code logs in `window1/exthost/ms-dotnettools.csdevkit/ServiceHub/` and contains the corresponding result notifications. Its payload is a binary-framed protocol, so the normalized persisted JSON is the clearer count authority.

The durable remediation record states that a serial solution gate passed all 516 cases with no failures or skips in 157.1 seconds at `.copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md:69-74`. This is compatible with the flaky fact: a later full VS Code run can fail even though the earlier CLI gate passed.

## Commands and exact outputs

### Direct adapter discovery

```powershell
$projects = Get-ChildItem test -Recurse -Filter '*Tests.csproj' | Sort-Object FullName
foreach ($project in $projects) {
    $assembly = Join-Path $project.DirectoryName "bin/Debug/net10.0/$($project.BaseName).dll"
    $output = & dotnet vstest $assembly /ListTests 2>&1
    $count = @($output | Where-Object { $_ -match '^    \S' }).Count
    "$($project.BaseName)|$count"
}
```

```text
TNC.Trading.Platform.Api.IntegrationTests|29
TNC.Trading.Platform.Api.UnitTests|57
TNC.Trading.Platform.AppHost.UnitTests|40
TNC.Trading.Platform.Application.UnitTests|128
TNC.Trading.Platform.Architecture.IntegrationTests|13
TNC.Trading.Platform.Infrastructure.IntegrationTests|22
TNC.Trading.Platform.Infrastructure.UnitTests|112
TNC.Trading.Platform.Web.E2ETests|1
TNC.Trading.Platform.Web.FunctionalTests|6
TNC.Trading.Platform.Web.UnitTests|108
TOTAL|516
```

### Source metadata count

The PowerShell inventory counted exact attribute lines under each project. Aggregate output:

```text
TOTAL|Fact=422|Theory=23|InlineData=94|MemberData=0|ClassData=0|Methods=445
```

### Persisted VS Code result count

```powershell
$j = Get-Content $resultPath -Raw | ConvertFrom-Json
$j.items | Group-Object { $_.tasks[0].state }
```

Exact output for both full-run JSON files:

```text
ITEMS=662
TASKSTATE|0|1
TASKSTATE|3|515
TASKSTATE|4|1
TASKSTATE|5|145
```

State 0 is the controller root, state 3 is passed, state 4 is failed, and state 5 is skipped-own-state. Removing the root leaves 661 tree items.

Hierarchy classification output:

```text
state=3;depth=4;range=True|421
state=3;depth=5;range=True|94
state=4;depth=4;range=True|1
state=5;depth=1;range=False|10
state=5;depth=2;range=False|20
state=5;depth=3;range=False|89
state=5;depth=4;range=False|23
state=5;depth=4;range=True|2
state=5;depth=5;range=True|1
```

The last two groups are the three stale leaves. The first four state-5 groups are the 142 hierarchy ancestors.

### Failure reproduction

```powershell
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-build --no-restore --verbosity minimal
```

Twenty repetitions produced 19 passes and one failure. The failing iteration output was:

```text
Failed!  - Failed: 1, Passed: 56, Skipped: 0, Total: 57, Duration: 92 ms
Assert.Equal() Failure: Values differ
Expected: 1
Actual:   2
at PlatformAuthenticationSupervisorTests.StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy()
```

The exact test filter passed ten of ten times:

```powershell
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName=TNC.Trading.Platform.Api.UnitTests.PlatformAuthenticationSupervisorTests.StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy" --verbosity minimal
```

Each iteration reported `Passed: 1, Skipped: 0, Total: 1` with exit code 0.

### Discovery command limitation

`dotnet test ... --list-tests --verbosity normal` selected preview SDK `10.0.400-preview.0.26322.102`, printed `The following Tests are available:`, then canceled the build with `MSB5021` and no names. Direct `dotnet vstest <assembly> /ListTests` succeeded and is the discovery evidence used here.

## Alternative hypotheses

* Solution omission is rejected. All ten test projects are both under `test/` and included in `TNC.Trading.Platform.slnx`; direct counts sum to 516.
* A repository filter or runsettings file is rejected. None exists, and persisted run requests target the C# Dev Kit controller with an empty exclusion list.
* Source-declared skips or categories are rejected. No applicable metadata exists.
* Data-driven expansion as the 145 difference is rejected. It adds exactly 71 runnable cases, from 445 source methods to 516 cases.
* All 145 entries being stale tests is rejected. Exact ID-prefix analysis identifies 142 hierarchy ancestors and only 3 stale leaves.
* The duplicate truncated inline-data display name is rejected as the missing pass. The recorded failed ID points to the API supervisor fact, and both duplicate-display cases are executable leaves.
* A canceled or undiscovered test is rejected. The remaining executable result is explicitly state 4 (failed), not unset, skipped, or absent.
* The earlier 516-pass CLI gate being contradictory is rejected. The supervisor fact is flaky: it passed the serial gate, failed both inspected VS Code runs, passed 10 isolated runs, and failed once in 20 owning-project runs.

## Gaps

* The retained `solution-tests-serial` artifact directory now has an empty TRX subdirectory and only a truncated stdout log, so its 516-pass result is supported by the durable changes record rather than a presently parseable final TRX.
* VS Code workspace result JSON and ServiceHub logs are local mutable state outside the repository. Reloading the window or clearing test results may remove the three stale leaves and alter the 661 presentation count.
* The investigation did not modify the test to prove a corrected synchronization contract. That would exceed the research-only scope.
* No complete fresh solution execution was needed after the persisted JSON gave exact run results and the focused owning-project reproduction exposed the same failure. Direct discovery and 30 focused/project executions were performed.

## Recommended verification

1. In VS Code, run **Test: Clear All Results**, refresh tests, and rerun all tests. Confirm the three stale leaves disappear; hierarchy nodes may still appear in progress accounting.
2. Capture the new workspace `testResults/<run-id>.json` before clearing results. Excluding the controller root, group `tasks[0].state`; executable total is passed plus failed plus genuinely skipped leaves, not all tree items.
3. Reconfirm the runnable inventory with direct VSTest discovery. The expected per-project total is 516.
4. Treat `StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy` as flaky until its test controls the handoff between the initial tick, background-loop start, and stop cancellation. Validate any future correction with repeated owning-project runs, not only an isolated fact.
5. Preserve a TRX for the full solution command so executable counters remain durable independently of VS Code workspace storage.

## Clarifying questions

None. The local persisted run records identify every displayed count and the non-passing test exactly.