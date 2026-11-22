# Web Project Folder Structure Plan Research

## Scope

Research for an implementation-ready plan based on `.copilot-tracking/research/2026-08-04/web-project-folder-structure-research.md`. Production code was not modified.

## Planning Gaps

### 1. Classify root view models by consumer before moving them

The root models are not one homogeneous UI model layer. The plan should create a consumer matrix before any namespace change:

| Consumer classification | Evidence | Planning consequence |
| --- | --- | --- |
| Web-to-API wire contracts | `PlatformApiClient` deserializes status, configuration, events, authentication administration, IG history, and manual retry responses at `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:9-129`; configuration update models are serialized at `:41-53`. | Keep these in a deliberate shared client/contracts boundary initially, or move them as a complete contract group. Do not place them in a feature folder solely because a page displays them. |
| Configuration feature request/response data | `ConfigurationFormModel` is a page-local editing state at `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModel.cs:1-55`; its mapper translates to and from API models at `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModelMapper.cs:1-76`. | Keep the form model and mapper with the Configuration feature. Treat `Update*` models separately from the page-local form state because they are also serialized by `PlatformApiClient`. |
| Status/Home display data | `Home.razor` and `Status.razor` consume status/events at `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor:109-110` and `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor:230-231`; presenters use them at `src/TNC.Trading.Platform.Web/Components/Pages/HomePagePresenter.cs:34-76` and `StatusPagePresenter.cs:35-110`. | Decide whether shared response contracts remain outside feature folders while presenters and page-only state move under `Home`/`Status`. Avoid duplicating the same response types. |
| IG login history data | `PlatformApiClient.GetIgLoginHistoryAsync` returns `IgLoginHistorySnapshotViewModel` at `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:103-129`; the page consumes it at `src/TNC.Trading.Platform.Web/Components/Pages/IgLoginHistory.razor:94`. | This is a likely IG feature group, but confirm whether the snapshot is an API contract used elsewhere before changing namespace. |
| Authentication administration data | `PlatformApiClient.GetAuthAdministrationAsync` returns `AuthAdministrationViewModel` at `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:89-101`; Administration consumes it at `src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor:40`. | Keep authentication administration data aligned with the page or shared API contract decision; do not confuse it with `Authentication/` service implementation types. |
| Test-only construction/fixtures | `PlatformWebTestData` constructs nearly every response model at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs:9-151`; `PlatformApiClientTests` constructs update requests at `:106-139`. | Every model move requires corresponding test namespace/import updates. Test data should move only if the production model boundary moves; do not use test fixture location as the ownership authority. |

The implementation plan should explicitly decide whether the first slice preserves namespaces while changing physical paths. That is the lowest-risk way to prove folder organization independently from API contract redesign.

### 2. Sequence moves by dependency and rollback checkpoint

Recommended sequence:

1. Capture the working-tree state and baseline `dotnet build` plus the affected Web unit tests.
2. Move only files with unambiguous ownership while preserving namespaces. Start with `ConfigurationFormModel.cs` and `ConfigurationFormModelMapper.cs` under a Configuration feature folder because both already declare `TNC.Trading.Platform.Web.Components.Pages` and are only page-local.
3. Move the paired page, presenter, and directly owned form files together. Preserve every `@page` value.
4. Move Home, Status, IG history, and Administration slices one at a time. Keep shared API response contracts in their chosen shared boundary until the consumer matrix proves they are feature-local.
5. Change namespaces only as an intentional second pass. Update `Components/_Imports.razor` and the affected C# imports in the same commit/slice.
6. Move composition registrations last, if needed. `PlatformWebUiServiceCollectionExtensions.AddPlatformWebUi` registers page presenters and layout services at `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:7-17`; this is the main registration checkpoint.
7. Run a focused build and Web unit tests after each slice, then run the Web E2E and Functional projects before declaring the plan complete.

The Web SDK project has no explicit compile/content/Razor item globs (`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-21`), so physical moves do not require project-file item edits. Any namespace migration still affects Razor imports, C# `using` directives, service registration, and bUnit component discovery.

### 3. Namespace, import, and test impacts

The shared Razor imports include the root Web namespace, Pages, Layout, Authentication, Authorization, and Components at `src/TNC.Trading.Platform.Web/Components/_Imports.razor:1-15`. A namespace-preserving move should not require import churn; a namespace-changing move must update this file and any component-level imports.

The unit-test composition root directly imports and registers page presenters and layout services at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:17-18,116-130`. Direct presenter tests import `TNC.Trading.Platform.Web.Components.Pages`, for example `ConfigurationTests.cs:4`, `HomeTests.cs:4`, and `StatusTests.cs:4`; layout tests import `Components.Layout` at `MainLayoutTests.cs:4`. The plan must include these as an explicit namespace-change checklist, not rely on compiler errors as the only discovery mechanism.

`Properties/InternalsVisibleTo.cs:1-3` grants the Web unit-test assembly access to internal types. Folder moves do not change this assembly boundary, but moving or renaming internal contracts must preserve the project reference and accessibility.

The test projects are separate by test type and already follow the repository convention:

* Unit tests reference the Web project at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:22-28`.
* Functional tests use `Aspire.Hosting.Testing`, Playwright, and the AppHost project at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:9-28`.
* E2E tests use `Microsoft.Playwright.Xunit`, Aspire testing, and the AppHost project at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:9-27`.

No project-file compile glob changes should be planned unless the implementation introduces a new project or deliberately changes linked files.

### 4. Route and static-asset validation

Physical component folders should not change route templates. The route inventory is:

* `/` in `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor:1`
* `/status` in `Status.razor:1`
* `/ig-login/history` in `IgLoginHistory.razor:1`
* `/configuration` in `Configuration.razor:1`
* `/administration/authentication` in `Administration.razor:1`
* `/authentication/access-denied` in `AccessDenied.razor:1`
* `/error` in `Error.razor:1`

`Routes.razor` discovers routable components from `typeof(Program).Assembly` and uses `MainLayout` at `src/TNC.Trading.Platform.Web/Components/Routes.razor:1-10`. Each route move must verify both facts remain unchanged.

Static assets are independent of component source folders. `App.razor` defines `<base href="/">`, `app.css`, Radzen assets, `platform-theme.js`, and Blazor bootstrapping at `src/TNC.Trading.Platform.Web/Components/App.razor:9-19`. Keep `src/TNC.Trading.Platform.Web/wwwroot/` unchanged. Existing root-relative navigation contracts are in `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:19-34` and `PlatformHeader.razor:15`; do not turn a source-folder move into a hosting base-path change.

The implementation plan should add or identify a cheap route/static smoke check that requests every route or verifies the rendered shell, and should explicitly state whether non-root deployment is out of scope. Current high-level tests cover authentication redirects, not a complete route and asset matrix.

### 5. Available browser and distributed coverage

Contrary to the initial research note, this repository already has relevant higher-level coverage:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:1-47` uses Playwright `PageTest`, discovers the Web runtime from an AppHost-started process through `RealAuthenticationE2ETestFixture.cs:1-42`, and proves a real Keycloak sign-in reaches `/` or `/status`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs:1-190` covers anonymous root redirect, authenticated route access, sign-out, unsafe return URLs, legacy GET sign-out, and missing antiforgery tokens.
* `PlatformProtectedRouteFunctionalTests.cs:1-61` covers a viewer being redirected from `/configuration` to access denied.
* The E2E and Functional projects share AppHost process helpers through linked source files in their project files and run against distributed infrastructure, while the Unit project remains isolated bUnit coverage.

For a folder-only move, the existing E2E and Functional suites are the primary regression gate after each completed feature slice or at the end of the migration. The plan should add a route/static-asset test only if the existing suites do not already expose the required failure; do not duplicate the expensive real-auth journey for every moved page. If the implementation changes namespaces only, focused unit tests plus a Web build are sufficient for intermediate checkpoints; if it changes hosting paths, add browser validation for base href, assets, and all navigation links.

## Required Plan Decisions

* Select the first set of models that will remain shared API contracts and document their owning boundary; explicitly reject a generic `Models/` dumping ground.
* Decide whether phase one preserves namespaces and phase two changes them to match feature folders.
* Define the exact move order and checkpoint command for each feature slice.
* List all affected `_Imports.razor`, service-registration, presenter-test, and test-data imports before changing namespaces.
* Preserve all `@page` values, `Routes.razor` assembly discovery, `App.razor` static-asset references, and root-relative navigation unless a separate hosting-path requirement is approved.
* Use the existing Web unit, Functional, and E2E projects according to their boundaries; do not merge test types.

## Repository Rules Used

* `.github/instructions/folders.instructions.md:16-24` requires preserving top-level layout and updating references after moves.
* `.github/instructions/architecture.instructions.md:37-76` favors feature-oriented organization while preserving valid boundaries incrementally.
* `.github/instructions/csharp.instructions.md:21-29` requires one top-level type per file with matching file names.
* `.github/instructions/tests.instructions.md:24-70` separates unit, integration, E2E, and functional test responsibilities.

## Unresolved Questions

* Should the first implementation phase preserve existing namespaces, or is matching physical feature folders a required end state?
* Which `Platform*`, `Ig*`, `Auth*`, retry, and schedule models are intended as stable Web-to-API contracts versus UI-only representations? The current code shows transport use but not the desired long-term ownership.
* Is deployment below a non-root path prefix a real requirement? If yes, base href, static assets, and absolute links need a separate change and browser validation.
* Should a new route/static-asset smoke test be added, or are the existing Functional/E2E tests sufficient for the accepted migration scope?

## Status

Complete. Evidence is sufficient for an implementation-ready planning pass; no production code was changed.
