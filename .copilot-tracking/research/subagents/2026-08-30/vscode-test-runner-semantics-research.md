---
title: VS Code Test Runner Semantics Research
description: Repository-specific research into differences between discovered and executed .NET test counts in VS Code
author: GitHub Copilot
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Research questions

* What IDE and adapter semantics can explain 661 tests in VS Code discovery but a completed run summary of 515 passed out of 516?
* How does the repository configure VS Code testing, .NET test SDKs, test frameworks, adapters, filters, and generated result artifacts?
* How are skipped, not-run, filtered, explicitly selected, data-driven, and stale tests represented across VS Code, VSTest, Microsoft.Testing.Platform, and xUnit?
* What does the specific summary "515 passed out of 516" establish about the remaining executed test?

## Initial workspace observations

* The workspace has no `.vscode/` directory, so there is no repository-owned
	`settings.json` or `extensions.json`. A read-only `code --list-extensions
	--show-versions` check found `ms-dotnettools.csdevkit@3.20.199`,
	`ms-dotnettools.csharp@2.140.9`, and
	`ms-dotnettools.vscode-dotnet-runtime@3.1.0`; it found no second test-adapter
	extension. The only user-settings line matching test/.NET terms was terminal
	auto-approval for `dotnet build`, not test discovery or execution behavior.
* The initial non-ignored filename scan missed generated evidence. Targeted
	ignored-file searches found VSTest diagnostic logs, quality-gate logs, and
	many TRX files under `artifacts/` and generated `TestResults/` directories.
* TNC.Trading.Platform.slnx:1-37 contains ten test projects: one Architecture,
	two API, one AppHost, one Application, two Infrastructure, and three Web test
	projects. No non-solution test project is currently present.
* global.json:1-6 selects .NET SDK `10.0.200` with `latestFeature` roll-forward.
	It has no `test.runner` property, so the .NET 10 `dotnet test` default remains
	VSTest rather than Microsoft.Testing.Platform (MTP).

## Findings

### Repository test stack

* Every test project references `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3,
	and `xunit.runner.visualstudio` 2.8.2. For a representative exact reference,
	see test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:1-24.
	This is the VSTest/xUnit v2 adapter path documented by C# Dev Kit and xUnit;
	no project reference to an MTP runner package was found.
* The only repository xUnit runner configuration is
	test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/xunit.runner.json:1-4,
	copied by that project at
	test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:13-19.
	It disables test-collection parallelism only. It does not filter, skip, stop
	on failure, or change theory pre-enumeration.
* A source search found no xUnit `Skip` or `Explicit` declaration and no
	runtime `Assert.Skip` use. The few `.Skip(...)` matches are LINQ calls.
	Therefore, repository metadata does not intentionally suppress 145 tests or
	explain one non-passing item as a declared skip.
* The suite has extensive `[Theory]` data: 94 `InlineData`, `MemberData`,
	`ClassData`, `TheoryData`, or `DataAttribute` references were found across 16
	files. Examples include 17 rows on one theory at
	test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs:14-30.
	xUnit treats each data row as a separate test case. Its Visual Studio adapter
	defaults `preEnumerateTheories` to `true`, so these rows normally appear as
	separate Testing-view children as well as separate executed cases.

### Discovery inventory versus run total

* VS Code's Testing API represents discovery as a provider-owned hierarchy of
	`TestItem` objects. Discovery can be eager, lazy, or triggered by opening the
	Testing view or expanding nodes. The provider must keep the collection
	synchronized, including deleting obsolete items. The displayed inventory is
	therefore a current IDE model, not a promise that every item belongs to the
	next run.
* A run is a separate `TestRunRequest`. `include` is absent for "run all" or
	names selected items; `exclude` can remove descendants. Run profiles and tags
	can further limit eligibility. The provider reports states only for items in
	that run. Consequently, 661 discovered items and 516 run items can both be
	correct without 145 skip results.
* VSTest makes the same distinction: `/ListTests` lists discovered tests, while
	`/Tests` and `/TestCaseFilter` select execution. Microsoft explicitly notes
	that `/TestCaseFilter` has no effect on `/ListTests`; filtered-out tests are
	absent from the run total, not counted as skipped.
* Explicitly selecting a project, namespace, class, method, or subset in VS
	Code sends an include-scoped request. Tests outside that request retain their
	discovery nodes and possibly prior state, but are not members of the new
	run's denominator.
* Stale tests are also possible in principle because the provider owns item
	removal and C# Dev Kit discovers from built test projects. Official C# Dev Kit
	guidance says tests require a build to be recognized and Refresh Tests builds
	and rediscovers them. A stale built assembly, removed project, changed theory
	rows, or provider item not yet deleted can inflate the tree until a successful
	refresh/reload. In this case, persisted run data provides a more specific
	explanation than this general possibility: the 661 entries include hierarchy
	containers and three stale skipped test-case records.

### Repository run evidence

* .copilot-tracking/changes/2026-08-30/trailing-stops-remediation-changes.md:69
	records a later complete serial run: all 516 tests passed, with no failures or
	skips, in 157.1 seconds. This establishes 516 as the repository's then-current
	complete executable case count, including theory rows.
* VS Code workspace storage retains two matching root runs in
	`testResults/e51e2b64-d489-4b1c-afc1-629781c7a45b.json` and
	`testResults/45d49018-2514-4320-8065-2b380822cc59.json`. Each contains 661
	non-controller result entries: 519 URI-bearing test-case records and 142
	URI-less hierarchy containers. The 519 test cases comprise 515 passed, one
	failed, and three skipped records.
* VS Code's internal `TestResultState` enum maps persisted state `3` to
	`Passed`, state `4` to `Failed`, and state `5` to `Skipped`. The 145 entries
	with an own state of `Skipped` comprise the 142 hierarchy containers and three
	skipped test cases. They are not 145 skipped executable test cases.
* The failed test case in both root runs is
	`StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy` in
	test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs.
	Its persisted duration is 5 ms. Its message collection is empty, so the IDE
	result identifies the failed test but does not retain the failure cause.
* The three skipped test cases are stale result records for
	`UpdateAccountPreferences_ShouldConfirmWithAuthoritativeGet_WhenPutSucceeds`,
	`IsSupported_ShouldAllowDemoAndRejectLive_WhenCheckingFeatureEnvironment`, and
	`GetAsync_ShouldRejectMalformedJson_WhenTrailingStopsIsNotBoolean(payload:
	"{\"trailingStops\":\"true\"}")`. They have no duration or message. Their IDs
	no longer occur in current discovery, which distinguishes them from the 516
	executable test cases counted by the summary.
* The retained directory
	artifacts/quality-gates/trailing-stops-remediation/solution-tests-serial/
	is an earlier timed-out attempt, not that completed run. Its stdout at lines
	1-2 only begins the API integration assembly, and `timeout.txt` records the
	watchdog event. No retained TRX represents the exact IDE display, but the two
	persisted VS Code result files do.
* Existing TRX files demonstrate VSTest's richer status model. Their
	`<Counters>` elements independently record `total`, `executed`, `passed`,
	`failed`, `error`, `timeout`, `aborted`, `inconclusive`, `notRunnable`, and
	`notExecuted`. For example,
	artifacts/quality-gates/trailing-stops-remediation/application-account-preferences/trx/application-account-preferences.trx:100
	records 12 total, 12 executed, 11 passed, and 1 failed.

### Meaning of 515 passed out of 516

* The statement establishes a selected-run denominator of 516 and exactly 515
	items in VS Code's passed state. It does not state that the discovery
	inventory was 516 and does not mean the other 145 discovered items were
	skipped.
* Arithmetic alone establishes one non-passing selected item. The wording alone
	does not identify its terminal state. Depending on what the adapter reported
	and whether the run ended or was canceled, the item could be failed, errored,
	skipped, canceled, or left queued/started without a terminal result. The Test
	Results details or run output is required to distinguish those cases.
* The persisted result resolves what the wording alone cannot: VS Code recorded
	515 passed test cases, one failed test case, and three skipped test cases. The
	UI denominator of 516 is therefore the passed-plus-failed executable set;
	skipped test cases and hierarchy containers are excluded. The one counted
	non-passing item was
	`StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy`.
* A focused rerun passed 10 of 10 times, while the owning 57-case API unit test
	project reproduced the same test failure once in 20 runs. That reproduction
	reported expected tick count 1 and actual tick count 2 at
	test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:71.
	This supports a timing-sensitive test failure rather than a declared skip,
	permanent defect, or merely incomplete runner state.

## Alternatives and gaps

* Filtered or explicitly unselected tests can explain inventory versus run
	scope, but no workspace runsettings, test filter, or retained IDE run request
	proves that the observed run used one.
* Data-driven expansion explains why test-method counts and test-case counts
	differ, but it does not by itself explain 661 versus 516 because this adapter
	normally pre-enumerates theory rows for both discovery and execution.
* The two persisted runs reconcile the apparent 661 count as 519 URI-bearing
	test cases plus 142 URI-less hierarchy containers. Three stale skipped
	test-case records account for the difference between 519 retained test cases
	and the 516 executable denominator. This direct evidence is stronger than the
	earlier stale-inventory hypothesis.
* The exact failed state and test identity survive, but the original assertion
	message and stack trace do not. The later one-in-20 reproduction supplies
	evidence of the observed assertion mismatch, but it cannot prove that the
	original 5 ms IDE failures had the identical immediate cause.

## References

* [Testing with C# Dev Kit](https://code.visualstudio.com/docs/csharp/testing)
* [VS Code Testing API](https://code.visualstudio.com/api/extension-guides/testing)
* [VS Code internal test result states](https://github.com/microsoft/vscode/blob/main/src/vs/workbench/contrib/testing/common/testTypes.ts)
* [dotnet test command](https://learn.microsoft.com/dotnet/core/tools/dotnet-test)
* [VSTest.Console options](https://learn.microsoft.com/visualstudio/test/vstest-console-options)
* [Microsoft.Testing.Platform overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
* [xUnit runner JSON configuration](https://xunit.net/docs/config-xunit-runner-json)
* [xUnit getting started and theory case expansion](https://xunit.net/docs/getting-started/v3/getting-started)
* [xUnit runner and framework responsibilities](https://xunit.net/docs/running-tests-in-parallel)

## Clarifying questions

* Was the observed command VS Code's Testing toolbar "Run Tests" at the root,
	"Run All Tests" from the Command Palette, or a run action on a selected node?
* Is the original Testing output or screenshot available outside the workspace?
	It could establish whether the original failure message matched the later
	expected-1-versus-actual-2 reproduction.