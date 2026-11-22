---
title: Build Tests Aspire Account Preferences Implementation Research
description: Verification of the proposed build, test, Aspire, and account preferences implementation path
ms.date: 2026-09-20
ms.topic: reference
---

## Status

Complete. Research only; no product, test, package, or UI code was changed.

## Scope

* Verify package versions and the proposed Aspire patch alignment
* Verify the four reported failing-test root causes and their owning code paths
* Verify the account preferences layout structure and its focused test coverage
* Verify the proposed validation commands against the current project layout

## Verified Facts

### Package Baseline

* `global.json:2-3` pins SDK `10.0.302` with `latestPatch` roll-forward.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1,4-5`
	uses Aspire AppHost SDK and SQL Server `13.5.3`, plus Keycloak
	`13.5.3-preview.1.26425.3`.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11`,
	`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:11`,
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11`,
	and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11`
	each directly reference `Aspire.Hosting.Testing` `13.5.3`.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:16`
	directly references `Testcontainers.MsSql` `4.8.1`. A current dependency
	listing reproduces `NU1903` and resolves transitive `SSH.NET` `2024.2.0`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:15`
	directly references `bunit.web` `1.40.0`. A current dependency listing
	reproduces `NU1902` and resolves transitive `AngleSharp` `1.2.0`.

### Four Test Causes

* The E2E fixture omits the reconciliation configuration at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-23`.
	The AppHost accepts `AccountPreferences:Reconciliation:Enabled` at
	`src/TNC.Trading.Platform.AppHost/AppHostSettings.cs:27-31` and passes it
	to the API at `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:141-143`.
	The reconciliation service defaults to enabled and immediately runs a tick
	at `src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:14-27`.
	Therefore, setting this value to `false` in the E2E fixture is a valid,
	fixture-scoped fix for the warning test at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:65-78`.
* The account-mismatch handler returns `VerificationFailed` separately from
	its retained state at
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:20`.
	The endpoint only overlays `FailureSummary` at
	`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:151-154`.
	Since `ToResponse` uses the retained state's verification status at
	`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:9-12`, it returns `InSync` instead of the
	`VerificationFailed` required by
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:161-172`.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:112-122`
	serializes with default `JsonSerializer` options, then expects the web
	camel-case property name. Use `JsonSerializerDefaults.Web` in the test;
	no production contract change is indicated.
* `PlatformAuthenticationSupervisor.StartAsync` performs an initial tick at
	`src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:30-34`, then its hosted loop immediately performs another at lines 41-49.
	This confirms the duplicate-tick cause of the assertion at
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:59-71`.
	Delay once in the hosted `ExecuteAsync` path before calling the existing
	loop, retaining `RunUntilStoppedAsync`'s immediate-tick behavior for its
	direct tests.

### Account Preferences Layout

* The DOM order is fieldset, Enabled, Disabled, then Save at
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:43-52`.
* The outer row is currently wrapped horizontal flex at
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:76-82`.
	The nested radio group remains horizontal flex at lines 89-93, and the
	action container already supplies vertical spacing at lines 57-62.
* A column direction on the outer row is therefore sufficient: it keeps
	Enabled and Disabled side-by-side through the nested radio group, while
	placing Save below in both DOM and visual order. The desktop geometry test
	at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-116`
	currently asserts the opposite layout and must change. Preserve the narrow
	overflow test at lines 126-136 and keyboard test at lines 146-158.

### Validation Commands

* All four focused project paths from the source research are included in
	`TNC.Trading.Platform.slnx:20,33-35`; the exact named tests exist in their
	cited files above. The proposed focused `dotnet test` commands are valid.
* A serial build and solution test remain appropriate final validation. The
	two `dotnet list package --include-transitive` commands used here provide
	a cheaper package-warning baseline before package changes.

## Plan Discrepancy

The workspace evidence confirms the current Aspire `13.5.3` package train and
all seven direct references, but cannot independently verify that the proposed
`13.5.4`, `13.5.4-preview.1.26464.4`, `Testcontainers.MsSql 4.15.0`, and
`AngleSharp 1.5.0` targets are still available and compatible on configured
NuGet sources. Confirm those exact versions during implementation before
editing package references. This does not alter the proposed sequence.

The two targeted dependency inspections each emitted one warning. They confirm
the two reported vulnerability paths, but do not replace a clean serial
solution build when determining the total current warning count.

## Recommended Implementation Sequence

1. Capture a clean serial restore/build baseline and record every diagnostic.
2. Fix the response projection, hosted-loop initial delay, JSON test options,
	 and fixture-scoped reconciliation setting; run each named focused test.
3. Change the outer layout row to a flex column and update the desktop E2E
	 geometry contract; run account preferences E2E and Web unit tests.
4. Confirm package target availability, then upgrade the coordinated Aspire
	 train and remediate both vulnerable test dependency paths.
5. Restore, serial-build, run the solution tests, and validate the Docker
	 AppHost resources, Keycloak sign-in, API authentication, and SQL access.

## Unanswered Questions

* Which configured NuGet source and current advisory policy should govern the
	final package targets? The repository has no lock file or package-target
	metadata that answers this locally.
* Does the clean serial solution build still emit only the two confirmed
	vulnerability warnings, or were the originally reported extra warnings from
	a different restore or design-time build?