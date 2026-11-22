<!-- markdownlint-disable-file -->
---
title: Failing Tests Research
description: Investigation of currently failing automated tests in TNC.Trading.Platform
ms.date: 2026-09-20
ms.topic: troubleshooting
---

## Research Scope

Investigate the tests currently failing in the repository, determine exact test
names and diagnostics, trace each to the nearby production and test code, classify
the cause as environmental or a code defect, and recommend one resolution path with
verification commands.

## Initial Test Runner Evidence

* `global.json` pins SDK `10.0.302` with `latestPatch` roll-forward.
* The repository has ten test projects under `test/`.
* No `Directory.Build.props` or `Directory.Packages.props` file was found.

## Test Execution Evidence

The solution test run completed with 610 passing and 4 failing tests. The
environment uses SDK `10.0.302` from `global.json`.

The following focused E2E command reproduced its failure deterministically:

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --no-build --filter "FullyQualifiedName~AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation" --verbosity normal
```

It ran one test and failed after 53.5 seconds. The Playwright assertion waited
five seconds for `account-preferences-warning` and found no matching element.

## Findings

### E2E account-preference warning

* Exact test: `TNC.Trading.Platform.Web.E2ETests.AccountPreferences.AccountPreferencesE2ETests.AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation`
* Classification: test infrastructure and fixture-isolation defect; not an external IG, browser, Docker, or Keycloak availability failure
* Failure: `Microsoft.Playwright.PlaywrightException` at test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:77. The locator `account-preferences-warning` did not exist after the Check status action.
* Production evidence: src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-20 renders that locator only when the response has `FailureSummary`. src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:164-169 creates the asserted text when an unavailable preferences response is observed.
* Root cause: the E2E fixture starts the app without `AccountPreferences:Reconciliation:Enabled=false` at test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-23. The API registers the reconciliation hosted service at src/TNC.Trading.Platform.Api/Program.cs:25; it starts immediately and mutates account-preference state every second at src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:16-33. This test concurrently changes provider state and expects one exact transient UI state, violating the test rule that mutable integration dependencies must be fixture-owned. The comparable functional fixture disables reconciliation at test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:40-49.
* Recommendation: add `AccountPreferences:Reconciliation:Enabled = false` to the E2E fixture configuration. Keep the UI test focused on the explicit Check status operation, and cover the background reconciler independently.
* Alternative: coordinate the test with the hosted reconciler through a fixture-owned pause/control port. Risk: adds cross-process control complexity and broadens the E2E fixture surface without adding coverage for this UI scenario.
* Verification:

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation"
```

### Functional check-status account mismatch response

* Exact test: `TNC.Trading.Platform.Web.FunctionalTests.AccountPreferences.AccountPreferencesFunctionalTests.CheckStatus_ShouldReturnSafeMismatchWithoutPreferencesIo_WhenAuthenticatedAccountChanges`
* Classification: product code defect
* Failure: `Assert.Equal()` at test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:168. Expected `verificationStatus` to be `VerificationFailed`; actual value was `InSync`.
* Root cause: src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:20 returns the existing state alongside `VerificationFailed`, `AccountMismatch`, and the safe reason before any preferences I/O. The endpoint at src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:151-154 only overlays `FailureSummary`, so it serializes the unchanged state's `InSync` verification status. The response contradicts the handler outcome while the test correctly confirms that no provider preferences I/O occurs.
* Recommendation: when returning a state plus failure category from Check status, project the response with `VerificationStatus = VerificationFailed` and the safe `FailureSummary` without persisting an account-mismatch failure. This preserves the durable configured target while making the immediate response internally consistent.
* Alternative: persist the mismatch as a failed reconciliation through `FailAsync`. Risk: the current early mismatch check deliberately avoids provider preference I/O and may not be intended to alter durable retry state for an authentication-account transition.
* Verification:

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj" --filter "FullyQualifiedName~CheckStatus_ShouldReturnSafeMismatchWithoutPreferencesIo_WhenAuthenticatedAccountChanges"
```

### API response JSON naming assertion

* Exact test: `TNC.Trading.Platform.Api.UnitTests.AccountPreferencesEndpointMappingTests.ToResponse_ShouldSerializeDesiredTrailingStopsValue_WhenStateIsProjected`
* Classification: test code defect; production HTTP contract is unaffected
* Failure: `KeyNotFoundException` at test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:122 because JSON emitted by direct `JsonSerializer.Serialize` did not contain `desiredTrailingStopsEnabled`.
* Root cause: the test uses the serializer's default options, which retain the record's PascalCase `DesiredTrailingStopsEnabled` property from src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:104-118. The application HTTP serializer uses web JSON defaults, as the web client does at src/TNC.Trading.Platform.Web/PlatformApiClient.cs:13 and 222-245, so its API contract is camelCase.
* Recommendation: serialize with `new JsonSerializerOptions(JsonSerializerDefaults.Web)` in this test, then retain the current camelCase assertion. This validates the actual HTTP naming policy.
* Alternative: assert `state.ToResponse().DesiredTrailingStopsEnabled` directly. Risk: it no longer validates the public JSON property name.
* Verification:

```powershell
dotnet test "test\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.UnitTests\TNC.Trading.Platform.Api.UnitTests.csproj" --filter "FullyQualifiedName~ToResponse_ShouldSerializeDesiredTrailingStopsValue_WhenStateIsProjected"
```

### Authentication-supervisor double reconciliation at startup

* Exact test: `TNC.Trading.Platform.Api.UnitTests.PlatformAuthenticationSupervisorTests.StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy`
* Classification: product code defect
* Failure: `Assert.Equal()` at test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs:71. Expected one tick; actual count was two.
* Root cause: src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:32-36 runs one tick synchronously before readiness, then `base.StartAsync` launches `ExecuteAsync`. src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:39-48 immediately runs a second tick before its first delay. This contradicts the test's stated single initial-reconciliation requirement and can duplicate startup side effects.
* Recommendation: schedule the first loop tick after the cadence delay when `StartAsync` has already completed the readiness tick. Preserve `RunUntilStoppedAsync` behavior for direct-loop tests by adding a distinct post-start execution path or explicit initial-delay parameter.
* Alternative: change the assertion to expect two ticks. Risk: normalizes duplicate startup reconciliation and leaves unnecessary external/persistence work at startup.
* Verification:

```powershell
dotnet test "test\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.UnitTests\TNC.Trading.Platform.Api.UnitTests.csproj" --filter "FullyQualifiedName~StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy"
```

## Recommended Order

1. Fix the Check status response projection because it is a user-visible response-contract defect.
2. Remove the duplicate supervisor startup tick because it can cause duplicate side effects.
3. Correct the API mapping test to use web JSON serialization defaults.
4. Disable background preference reconciliation in the E2E fixture and rerun the focused browser scenario.

## Final Verification

```powershell
dotnet test "TNC.Trading.Platform.slnx"
```

## Follow-On Questions

None. The four failures have locally supported resolution paths.
