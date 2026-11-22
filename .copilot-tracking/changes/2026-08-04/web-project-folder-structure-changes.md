<!-- markdownlint-disable-file -->
# Release Changes: Web Project Folder Structure

**Related Plan**: web-project-folder-structure-plan.instructions.md
**Implementation Date**: 2026-08-04

## Summary

Reorganize the Web project around existing feature ownership boundaries while preserving startup, authentication, routes, static assets, and Web-to-API contracts.

## Changes

### Added

* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/`
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationPagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModel.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModelMapper.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Home/`
* `src/TNC.Trading.Platform.Web/Components/Pages/Home/Home.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/Home/HomePagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/`
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/StatusPagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/`
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/IgLoginHistory.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/`
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor`

### Modified

* Implementation plan checkboxes for Phase 1 and Steps 1.1-1.3 completed after evidence capture.
* Planning log updated with the baseline, consumer classification, move constraints, and namespace strategy.
* Configuration page-local slice moved into `Components/Pages/Configuration/`; the Razor component explicitly declares `TNC.Trading.Platform.Web.Components.Pages` and retains `@page "/configuration"`.
* Home page slice moved into `Components/Pages/Home/`; the Razor component explicitly declares `TNC.Trading.Platform.Web.Components.Pages` and retains `@page "/"`.
* Status page slice moved into `Components/Pages/Status/`; the Razor component explicitly declares `TNC.Trading.Platform.Web.Components.Pages` and retains `@page "/status"`.
* IG login history page moved into `Components/Pages/IgLogin/`; the Razor component explicitly declares `TNC.Trading.Platform.Web.Components.Pages` and retains `@page "/ig-login/history"`. Direct ownership inspection found no presenter or form model to move; the shared `IgLoginHistorySnapshotViewModel` remains at the Web transport boundary.
* Step 2.3 completed after verifying moved presenters, Razor imports, service registration, bUnit composition, direct presenter tests, test data, and `InternalsVisibleTo`. No namespace, import, registration, test, or assembly-accessibility changes were necessary because all moved slices preserved their existing namespaces.
* Step 3.1 completed with the root Web project retained as the shared client/contracts boundary. All 17 response models and 5 `Update*ViewModel` request models remain single-source root types because `PlatformApiClient`, multiple page presenters/components, `PlatformWebTestData`, and `PlatformApiClientTests` consume them across feature boundaries. No generic `Models/` folder or duplicate type was introduced.
* Step 4.1 completed after source inspection and a focused contract check. All seven route templates remain present; `Routes.razor` still discovers `typeof(Program).Assembly` and uses `MainLayout`; `App.razor` retains the root base href, `app.css`, Radzen script, `platform-theme.js`, and Blazor bootstrapping; `MainLayout.razor` and `PlatformHeader.razor` retain root-relative navigation; and `wwwroot` remains limited to `app.css` and `platform-theme.js`.

### Phase 4 Route and Static-Asset Coverage Map

| Surface | Existing evidence | Status |
| --- | --- | --- |
| `/` | Functional anonymous-entry redirect test; Home unit tests; E2E operator-home journey | Covered |
| `/status` | Status unit tests; E2E sign-in return URL and heading assertion; navigation/access-token unit coverage | Covered |
| `/configuration` | Functional operator sign-out and viewer-denial journeys; Configuration unit tests; navigation/access-token unit coverage | Covered |
| `/ig-login/history` | Route source verification only; page unit tests cover rendering and API states | Partial: no distributed route navigation |
| `/administration/authentication` | API client, authorization redirect, and navigation unit coverage; route source verification only | Partial: no distributed route navigation |
| `/authentication/access-denied` | Functional viewer-denial redirect; Home and auth-audit unit coverage | Covered |
| `/error` | Route source verification only | Gap: no existing route smoke |
| `app.css` | `App.razor` source reference and `wwwroot` inventory | Partial: no HTTP asset smoke |
| `platform-theme.js` | `App.razor` source reference and `wwwroot` inventory; theme unit tests cover state mapping | Partial: no HTTP asset smoke |
| Radzen and Blazor bootstrapping | `App.razor` source references; existing rendered component and E2E suites exercise the shell | Covered by shell/runtime evidence |

No focused browser or distributed smoke test was added. The existing test conventions have no shell-asset HTTP smoke pattern, and adding broad route journeys would expand the folder-layout scope. The partial and gap entries are recorded as WI-02 follow-on coverage.

### Removed

* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationPagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModel.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModelMapper.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/HomePagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/StatusPagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLoginHistory.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor`

## Additional or Deviating Changes

### Phase 1 Evidence

* Working tree was clean before the Phase 1 documentation edits. No source or test product files were changed.
* Web build baseline passed with the existing .NET preview SDK and package vulnerability warnings.
* Web unit-test baseline passed: 88 total, 0 failed.
* `TNC.Trading.Platform.Web.csproj` uses `Microsoft.NET.Sdk.Web` with no explicit compile, content, or Razor globs. Default SDK discovery is recursive, so physical moves under the project directory remain discoverable.
* Existing C# namespaces remain `TNC.Trading.Platform.Web`, `TNC.Trading.Platform.Web.Components.Pages`, and `TNC.Trading.Platform.Web.Components.Layout`. The first migration will preserve those namespaces.
* Razor namespace strategy: add `@namespace TNC.Trading.Platform.Web.Components.Pages` explicitly to each moved page component. Do not add a scoped `_Imports.razor`; retain the shared imports in `Components/_Imports.razor` and update only when a deliberate namespace change requires it.

### Root Model Consumer Matrix

| Type | Boundary | Direct consumers | Phase 2 disposition |
| --- | --- | --- | --- |
| `AuthAdministrationViewModel` | Shared transport response | `PlatformApiClient.GetAuthAdministrationAsync`, `Administration.razor`, `PlatformWebTestData`, `PlatformApiClientTests` | Retain in the shared client/contracts boundary initially |
| `AuthStateViewModel` | Shared transport response | `PlatformStatusViewModel`, `PlatformWebTestData`, `Home.razor`, `Status.razor` | Retain in the shared client/contracts boundary initially |
| `CredentialPresenceViewModel` | Shared transport response | `PlatformConfigurationViewModel`, `ConfigurationFormModel`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `IgLoginHistorySnapshotViewModel` | Shared transport response | `PlatformApiClient.GetIgLoginHistoryAsync`, `IgLoginHistory.razor`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `IgLoginSnapshotViewModel` | Shared transport response | `IgLoginStatusViewModel`, `PlatformWebTestData`, `Status.razor` | Retain in the shared client/contracts boundary initially |
| `IgLoginStatusViewModel` | Shared transport response | `PlatformStatusViewModel`, `PlatformWebTestData`, `Status.razor` | Retain in the shared client/contracts boundary initially |
| `IgProofDataViewModel` | Shared transport response | `IgLoginStatusViewModel`, `Status.razor` through `PlatformStatusViewModel` | Retain in the shared client/contracts boundary initially |
| `ManualRetryViewModel` | Shared transport response | `PlatformApiClient.TriggerManualRetryAsync`, `Status.razor`, `PlatformWebTestData`, `PlatformApiClientTests` | Retain in the shared client/contracts boundary initially |
| `NotificationSettingsViewModel` | Shared transport response | `PlatformConfigurationViewModel`, `ConfigurationFormModel`, `ConfigurationFormModelMapper`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `PlatformConfigurationViewModel` | Shared transport response | `PlatformApiClient`, `ConfigurationPagePresenter`, `ConfigurationFormModelMapper`, `PlatformWebTestData`, `PlatformApiClientTests` | Retain in the shared client/contracts boundary initially |
| `PlatformEventItemViewModel` | Shared transport response | `PlatformEventsViewModel`, `PlatformWebTestData`, `Home.razor`, `Status.razor`, `PlatformApiClientTests` | Retain in the shared client/contracts boundary initially |
| `PlatformEventsViewModel` | Shared transport response | `PlatformApiClient`, `HomePagePresenter`, `StatusPagePresenter`, `Home.razor`, `Status.razor`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `PlatformStatusViewModel` | Shared transport response | `PlatformApiClient`, `HomePagePresenter`, `StatusPagePresenter`, `Home.razor`, `Status.razor`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `RetryPolicyViewModel` | Shared transport response | `PlatformConfigurationViewModel`, `ConfigurationFormModel`, `ConfigurationFormModelMapper`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `RetryStateViewModel` | Shared transport response | `PlatformStatusViewModel`, `IgLoginStatusViewModel`, `StatusPagePresenter`, `Status.razor`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `TradingScheduleStateViewModel` | Shared transport response | `PlatformStatusViewModel`, `IgLoginStatusViewModel`, `Status.razor`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `TradingScheduleViewModel` | Shared transport response | `PlatformConfigurationViewModel`, `PlatformStatusViewModel`, `ConfigurationFormModelMapper`, `Status.razor`, `PlatformWebTestData` | Retain in the shared client/contracts boundary initially |
| `UpdateCredentialsViewModel` | Configuration update request | `ConfigurationFormModel`, `ConfigurationFormModelMapper`, `PlatformApiClient.UpdateConfigurationAsync`, `PlatformApiClientTests` | Move with the Configuration page-local slice only if the shared request boundary is not selected |
| `UpdateNotificationSettingsViewModel` | Configuration update request | `ConfigurationFormModel`, `ConfigurationFormModelMapper`, `PlatformApiClient.UpdateConfigurationAsync`, `PlatformApiClientTests` | Move with the Configuration page-local slice only if the shared request boundary is not selected |
| `UpdatePlatformConfigurationViewModel` | Configuration update request | `ConfigurationFormModelMapper`, `PlatformApiClient.UpdateConfigurationAsync`, `PlatformApiClientTests` | Move with the Configuration page-local slice only if the shared request boundary is not selected |
| `UpdateRetryPolicyViewModel` | Configuration update request | `ConfigurationFormModel`, `ConfigurationFormModelMapper`, `PlatformApiClient.UpdateConfigurationAsync`, `PlatformApiClientTests` | Move with the Configuration page-local slice only if the shared request boundary is not selected |
| `UpdateTradingScheduleViewModel` | Configuration update request | `ConfigurationFormModel`, `ConfigurationFormModelMapper`, `PlatformApiClient.UpdateConfigurationAsync`, `PlatformApiClientTests` | Move with the Configuration page-local slice only if the shared request boundary is not selected |

The matrix rejects a generic `Models/` destination. Read models and update requests are serialized at the Web-to-API boundary, while `ConfigurationFormModel` and its mapper are page-local editing state. No API contract is duplicated merely because a page displays it.

### Move Constraints And Test Layers

* Preserve `Program.cs`, `Authentication`, `Components/Layout`, `Components/Authorization`, `Components/App.razor`, `Components/Routes.razor`, and `wwwroot` in this phase and during later moves.
* `PlatformWebUiServiceCollectionExtensions` registers `ConfigurationPagePresenter`, `HomePagePresenter`, `StatusPagePresenter`, `PlatformThemeState`, and `PlatformShellContextProvider`. Any intentional namespace change must update this registration file.
* `Components/_Imports.razor` supplies the current Pages and Layout imports. Physical moves with preserved namespaces require no import change; an intentional namespace change must update `_Imports.razor` and component-level imports.
* `PlatformComponentTestContext` registers the three page presenters and imports `TNC.Trading.Platform.Web.Components.Pages`. Direct presenter tests and `PlatformWebTestData` are the unit-test impact points.
* Web Unit tests are available in `TNC.Trading.Platform.Web.UnitTests`, Aspire-backed distributed Functional tests are available in `TNC.Trading.Platform.Web.FunctionalTests`, and Playwright/Aspire-backed E2E tests are available in `TNC.Trading.Platform.Web.E2ETests`. They remain separate validation layers.
* The first move list is physical-only: Configuration page files move together, with explicit Razor namespace directives. C# namespace changes and shared contract relocation remain separate decisions for later phases.

## Release Summary

Completed the staged Web folder reorganization with 10 page files and 4 Configuration support files moved into feature-owned folders. Existing C# namespaces, routes, startup/authentication boundaries, shared transport contracts, registrations, imports, test composition, and `wwwroot` assets were preserved.

Validation passed across the required layers:

* Web build passed.
* Web unit tests passed: 88/88.
* Configuration, Home, Status, IG login history, and Administration-focused checks passed: 9/9, 4/4, 17/17, 4/4, and 5/5 respectively.
* Web Functional tests passed: 6/6.
* Web E2E tests passed: 1/1.
* Solution build passed.
* Solution tests passed with zero failures across all projects.

The validation runs emitted existing .NET preview SDK, package vulnerability, and infrastructure health-check warnings. These did not fail the migration. WI-02 remains as follow-on work for direct distributed route coverage of `/ig-login/history`, `/administration/authentication`, and `/error`, plus HTTP assertions for `app.css` and `platform-theme.js`; no broad smoke suite was added to this folder-only change.

### Phase 2 Step 2.3 Evidence

* Completed on 2026-08-04. The moved C# files retain `TNC.Trading.Platform.Web.Components.Pages`; each moved Razor page explicitly declares the same namespace.
* `Components/_Imports.razor`, `PlatformWebUiServiceCollectionExtensions.cs`, `PlatformComponentTestContext.cs`, direct presenter tests, `PlatformWebTestData.cs`, and `Properties/InternalsVisibleTo.cs` were inspected. Existing imports, presenter registrations, test registrations, fixture construction, and friend-assembly access remain correct, so no source or test boundary edits were required.

### Phase 3 Step 3.1 Evidence

* Completed on 2026-08-04. Retained all 17 response models and 5 `Update*ViewModel` request models at the Web project root as one shared transport boundary.
* The consumer evidence is `PlatformApiClient` JSON serialization/deserialization, cross-page presenter/component usage, and direct construction/assertion in `PlatformWebTestData` and `PlatformApiClientTests`. Moving these types would create a broader client/contracts namespace decision without reducing duplication or clarifying page ownership.
* `ConfigurationFormModel` and `ConfigurationFormModelMapper` remain feature-local in `Components/Pages/Configuration/`; they translate the retained transport contracts into page editing state. No transport model or update request was duplicated or moved.
* Required phase validation passed: the Web build succeeded and the full Web Unit test suite completed with 88 passed, 0 failed, 0 skipped. Existing preview SDK and package vulnerability warnings remain non-blocking.

### Phase 2 Step 2.1 Evidence

* The four Configuration page-local files were physically moved together; no unrelated files, shared API contracts, service registrations, or test files were changed.
* Existing C# namespaces were preserved. `Configuration.razor` now declares `@namespace TNC.Trading.Platform.Web.Components.Pages` and retains `@page "/configuration"`.
* `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore` passed with the existing preview SDK and package vulnerability warnings.
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter FullyQualifiedName~Configuration` passed: 9/9 tests succeeded.

### Phase 2 Step 2.2 Home Evidence

* Completed on 2026-08-04. Physically moved `Home.razor` and `HomePagePresenter.cs` together into `Components/Pages/Home/`.
* Preserved the existing C# namespace, service registration, shared transport contracts, behavior, and test ownership. Added the approved explicit Razor namespace and retained the `/` route.
* Required Web build passed: `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore`.
* Narrow Home validation passed: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter FullyQualifiedName~Home` selected 4 tests; 4 succeeded.
* Existing preview SDK and package vulnerability warnings remain non-blocking; no unrelated files were changed.

### Phase 2 Step 2.2 IG Login History Evidence

* Completed on 2026-08-04. Physically moved `IgLoginHistory.razor` into `Components/Pages/IgLogin/`; no directly owned presenter or form code exists.
* Preserved `TNC.Trading.Platform.Web.Components.Pages`, the shared `IgLoginHistorySnapshotViewModel` transport contract, page behavior, authorization, and the `/ig-login/history` route. Added the selected explicit Razor namespace directive.
* Required Web build passed: `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore`.
* Narrow IG history validation passed: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter FullyQualifiedName~IgLoginHistory` with 4/4 tests succeeding.
* The initial build exposed a duplicate generated Razor input because the legacy file remained physically present after the patch operation. Removing that single duplicate source path resolved the issue; no product behavior change was required.
* Existing preview SDK and package vulnerability warnings remain non-blocking. No unrelated files were changed.

### Phase 2 Step 2.2 Administration Evidence

* Completed on 2026-08-04. Physically moved `Administration.razor` into `Components/Pages/Administration/`; direct ownership inspection found no presenter or form model to move.
* Preserved `TNC.Trading.Platform.Web.Components.Pages`, the shared `AuthAdministrationViewModel` transport contract, authorization behavior, page behavior, and `/administration/authentication`.
* Required Web build passed: `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore`.
* No Administration-specific unit-test class or matching filter exists. The closest affected test class passed: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter FullyQualifiedName~PlatformNavigationAccessCoordinatorTests`, with 5/5 tests succeeding.
* Existing preview SDK, package vulnerability, and nullable-analysis warnings remain non-blocking; no unrelated files were changed.

### Phase 2 Step 2.2 Status Evidence

* Completed on 2026-08-04. Physically moved `Status.razor` and `StatusPagePresenter.cs` together into `Components/Pages/Status/`.
* Preserved the existing C# namespace, service registration, shared transport contracts, behavior, and test ownership. Added the approved explicit Razor namespace and retained the `/status` route.
* Required Web build passed: `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore`.
* Narrow Status validation passed: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter FullyQualifiedName~Status` selected 17 tests; 17 succeeded.
* Existing preview SDK and package vulnerability warnings remain non-blocking; no unrelated files were changed.
