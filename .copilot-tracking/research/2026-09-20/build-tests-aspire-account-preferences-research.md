<!-- markdownlint-disable-file -->
# Task Research: Build, Tests, Aspire Upgrade, and Account Preferences

Research the reported build warnings, failing tests, available .NET Aspire 13.5.4 upgrade, and the requested account preferences layout adjustment.

## Task Implementation Requests

* Identify and recommend resolutions for the build warnings.
* Identify and recommend resolutions for currently failing tests.
* Assess the recommended upgrade path to Aspire 13.5.4.
* Recommend the account preferences layout change: Disabled to the right of Enabled and Save preferences beneath Enabled.

## Scope and Success Criteria

* Scope: Evidence gathering and implementation recommendations only. No product, test, package, or UI files were changed.
* Assumptions: The observations apply to the workspace state on 2026-09-20 using SDK `10.0.302` from `global.json`.
* Success criteria:
  * Each reported observation is traceable to reproducible evidence.
  * Each scenario has one recommended approach, alternatives, risks, and focused validation.
  * The recommendations preserve existing Clean Architecture, Aspire, Blazor, and test conventions.

## Outline

1. Establish a reproducible build and test baseline.
2. Correct the four distinct failing test causes.
3. Upgrade the coordinated Aspire package set.
4. Apply the presentation-only preferences-layout adjustment.

## Research Executed

### File Analysis

* `global.json:2-3`
  * Pins the installed .NET SDK to `10.0.302` with `latestPatch` roll-forward.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-7`
  * Owns the Aspire AppHost SDK, SQL Server, and Keycloak package versions.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:41-54`
  * Contains the Trailing stops radio fieldset and Save preferences action.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:57-105`
  * Controls the current horizontal editor layout.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-118`
  * Deliberately asserts the current horizontal desktop geometry and must change with the requested UI layout.

### Command Evidence

* `dotnet build .\TNC.Trading.Platform.slnx --no-restore -m:1 -v:minimal`
  * Succeeded with two warnings: `NU1903` for SSH.NET and `NU1902` for AngleSharp.
* `dotnet test "TNC.Trading.Platform.slnx"`
  * Completed with 610 passing and 4 failing tests.
* The focused E2E account-preferences warning test failed reproducibly after 53.5 seconds because `account-preferences-warning` was absent.

### External Research

* Aspire 13.5.4 release notes: [Aspire 13.5.4](https://github.com/microsoft/aspire/releases/tag/v13.5.4)
  * This is a patch release from the current 13.5.3 package train; no migration affecting the current AppHost APIs was identified.
* Keycloak integration: [Aspire Keycloak integration](https://aspire.dev/integrations/security/keycloak/)
  * The matching `Aspire.Hosting.Keycloak` package remains preview-only.
* Package advisory: [SSH.NET GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284)
  * The resolved `SSH.NET 2024.2.0` version has a high-severity path traversal vulnerability.
* Package advisory: [AngleSharp GHSA-pgww-w46g-26qg](https://github.com/advisories/GHSA-pgww-w46g-26qg)
  * The resolved `AngleSharp 1.2.0` version has a moderate-severity mXSS vulnerability.

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`, `.github/instructions/aspire.instructions.md`, `.github/instructions/aspire-tests.instructions.md`, `.github/instructions/tests.instructions.md`, `.github/instructions/playwright.instructions.md`, `.github/instructions/radzen-blazor.instructions.md`, and `.github/instructions/configuration.instructions.md`.
* The recommended changes retain Docker-backed Keycloak, Aspire-owned local orchestration, isolated integration fixtures, semantic radio controls, and web-first Playwright assertions.

## Key Discoveries

### Build Warning Count

The current reproducible serial solution build produces two warnings, despite the report of four. They are package-vulnerability warnings, not compiler warnings:

| Diagnostic | Direct owner | Resolved vulnerable package | Recommended resolution |
|------------|--------------|-----------------------------|------------------------|
| `NU1903` | `Testcontainers.MsSql 4.8.1` in `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:16` | `SSH.NET 2024.2.0` | Upgrade `Testcontainers.MsSql` to `4.15.0` |
| `NU1902` | `bunit.web 1.40.0` in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:11` | `AngleSharp 1.2.0` | Add a direct `AngleSharp` reference at `1.5.0` |

The first warning flows through `Testcontainers.MsSql -> Testcontainers -> SSH.NET`. Upgrade the owning test-container package rather than adding an unvalidated SSH.NET 2026 leaf override. The second is resolved by a direct reference to the minimum patched AngleSharp release, because bUnit's declared AngleSharp dependency has no upper version bound. Validate the web unit suite because bUnit also depends on older AngleSharp.Css and AngleSharp.Diffing packages.

The two unobserved warnings must not be guessed. First perform a clean restore and serial build with a binary log, then inventory the remaining distinct diagnostic IDs, projects, dependency paths, and source locations:

```powershell
dotnet clean .\TNC.Trading.Platform.slnx
dotnet restore .\TNC.Trading.Platform.slnx
dotnet build .\TNC.Trading.Platform.slnx --no-restore -m:1 -bl:artifacts\build-baseline.binlog
```

If this still produces only two, the original count likely included a prior restore, an IDE design-time build, or repeated emissions. Compare diagnostic IDs before opening remediation work.

### Failing Test Root Causes

| Failing test | Classification | Local cause | Recommended resolution |
|--------------|----------------|-------------|------------------------|
| `AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation` | Fixture isolation defect | E2E fixture enables a one-second background reconciler that races the explicit Check status scenario | Set `AccountPreferences:Reconciliation:Enabled` to `false` in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-23` |
| `CheckStatus_ShouldReturnSafeMismatchWithoutPreferencesIo_WhenAuthenticatedAccountChanges` | Product response-projection defect | `CheckAccountPreferencesStatusHandler` returns `VerificationFailed`, but `PlatformEndpoints` serializes the retained `InSync` state | Project `VerificationStatus = VerificationFailed` and the safe failure summary into the immediate response without persisting a failure |
| `ToResponse_ShouldSerializeDesiredTrailingStopsValue_WhenStateIsProjected` | Test defect | Direct `JsonSerializer.Serialize` uses PascalCase defaults, unlike ASP.NET Core's web serializer | Use `new JsonSerializerOptions(JsonSerializerDefaults.Web)` in the unit test |
| `StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy` | Product background-loop defect | `PlatformAuthenticationSupervisor.StartAsync` ticks before readiness and `ExecuteAsync` ticks again before its first delay | Make the started loop delay before its first execution when the readiness tick already ran |

The user-visible defect is the mismatch response: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:20` produces the failure category, while `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:151-154` leaves the existing verification status unchanged. The reconciliation change should preserve direct `RunUntilStoppedAsync` coverage while ensuring the hosted path does not repeat startup work.

### Aspire Package Alignment

The repository targets `net10.0`; SDK `10.0.302` is compatible with Aspire 13.5.4. Upgrade the following direct Aspire references together, and leave `Microsoft.Extensions.Http.Resilience/10.1.0` and `Microsoft.Extensions.ServiceDiscovery/10.1.0` unchanged because they are .NET Extensions packages, not Aspire 13.x packages.

| Package group | Current | Recommended |
|---------------|---------|-------------|
| `Aspire.AppHost.Sdk` | `13.5.3` | `13.5.4` |
| `Aspire.Hosting.SqlServer` | `13.5.3` | `13.5.4` |
| `Aspire.Hosting.Keycloak` | `13.5.3-preview.1.26425.3` | `13.5.4-preview.1.26464.4` |
| Four direct `Aspire.Hosting.Testing` test-project references | `13.5.3` | `13.5.4` |

No source migration is expected. The meaningful regression risk is the preview Keycloak integration. Validate it against Docker with a clean start and existing persisted local state, including realm import, OIDC sign-in, API authentication, database access, named endpoint readiness, and asynchronous cleanup.

### Account Preferences Layout

The requested page is `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:2`. The native radio controls already appear in correct DOM and tab order: Enabled, Disabled, Save. The only required production change is to make `.account-preferences-control-row` a start-aligned flex column in `AccountPreferences.razor.css`:

```css
.account-preferences-control-row {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 0;
    margin: 1.25rem 0;
}
```

Retain the existing wrapping radio group. It keeps Disabled to the right of Enabled at ordinary desktop and mobile widths, while the existing action margin puts Save preferences below the choices at the configuration panel's content edge. Do not use CSS `order` or markup reordering: it could make visual and keyboard order diverge.

Update the desktop Playwright geometry assertions to expect: choices on one row with Disabled to the right; Save below; aligned left edges; and no horizontal overflow at 1280px. Preserve the 360px overflow and ArrowRight keyboard-radio tests.

## Technical Scenarios

### Remediate Build Warnings

**Requirements:** Remove confirmed high- and moderate-severity vulnerable test dependencies without unnecessarily widening the package graph.

**Preferred approach:** Upgrade `Testcontainers.MsSql` and add the direct patched AngleSharp override, then verify the dependency graph and owning test projects.

**Considered alternatives:** A direct SSH.NET 2026 override has greater compatibility uncertainty than upgrading Testcontainers. Waiting for a new bUnit release leaves AngleSharp vulnerable. Neither is preferred.

### Resolve Failing Tests

**Requirements:** Fix the relevant behavior rather than weakening assertions, and keep the browser scenario isolated from background work.

**Preferred approach:** Correct the response projection, prevent the duplicate startup tick, align the JSON unit test with HTTP serializer defaults, and disable reconciliation in the E2E fixture.

**Considered alternatives:** Changing expected tick count normalizes duplicate effects. Persisting account mismatch changes durable retry semantics. A cross-process reconciler pause mechanism is disproportionate to this UI test. None is preferred.

### Upgrade Aspire

**Requirements:** Keep all Aspire-hosting packages on one patch train and preserve Docker-backed Keycloak coverage.

**Preferred approach:** Upgrade the seven direct packages as one patch set, then run focused distributed tests and real AppHost validation.

**Considered alternatives:** Deferral is reasonable only when Docker-backed Keycloak validation cannot be scheduled. Partial upgrades are not recommended because the Keycloak preview package needs matching release alignment.

### Adjust Account Preferences Layout

**Requirements:** Put Disabled beside Enabled and Save below the choice group while preserving semantic controls, keyboard order, and narrow-view behavior.

**Preferred approach:** Use the CSS flex-column adjustment and update only geometry assertions.

**Considered alternatives:** A grid works but offers no benefit for two ordered blocks. A new wrapper adds avoidable markup. CSS ordering compromises reading-order clarity.

## Recommended Implementation Order

1. Capture the clean build baseline to resolve the warning-count discrepancy.
2. Fix the four failing test causes and run each focused test immediately after its change.
3. Apply the account-preferences CSS and matching desktop E2E assertion update; run the focused E2E class and Web unit tests.
4. Upgrade the coordinated Aspire 13.5.4 package set after the baseline failures are resolved, so upgrade regressions remain distinguishable.
5. Restore, build, run all tests, and run the AppHost with Docker to verify Keycloak and SQL Server resources become healthy.

## Focused Validation

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj" --filter "FullyQualifiedName~CheckStatus_ShouldReturnSafeMismatchWithoutPreferencesIo_WhenAuthenticatedAccountChanges"
dotnet test "test\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.UnitTests\TNC.Trading.Platform.Api.UnitTests.csproj" --filter "FullyQualifiedName~ToResponse_ShouldSerializeDesiredTrailingStopsValue_WhenStateIsProjected|FullyQualifiedName~StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy"
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj" --filter "FullyQualifiedName~AccountPreferences"
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj"
dotnet build .\TNC.Trading.Platform.slnx --no-restore -m:1
dotnet test .\TNC.Trading.Platform.slnx --no-restore
```

## Potential Next Research

* None. The remaining work is implementation and validation.
