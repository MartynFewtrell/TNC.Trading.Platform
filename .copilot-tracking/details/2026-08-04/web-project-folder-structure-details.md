<!-- markdownlint-disable-file -->
# Implementation Details: Web Project Folder Structure

## Context Reference

Sources: `.copilot-tracking/research/2026-08-04/web-project-folder-structure-research.md`, `.copilot-tracking/research/subagents/2026-08-04/web-project-folder-structure-plan-research.md`, `.github/copilot-instructions.md`, `.github/instructions/folders.instructions.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/csharp.instructions.md`, `.github/instructions/tests.instructions.md`, and the user request dated 2026-08-04.

## Implementation Phase 1: Baseline and ownership classification

<!-- parallelizable: false -->

### Step 1.1: Capture working-tree state and validation baseline

Record the current working-tree state before any move and run the Web build plus focused unit tests. Preserve unrelated user changes and treat existing warnings or failures as baseline evidence.

Files:

* `src/TNC.Trading.Platform.Web/` - Source inventory and current ownership boundary
* `test/TNC.Trading.Platform.Web/` - Related unit, Functional, and E2E test projects

Success criteria:

* Working-tree changes are recorded and not overwritten.
* The Web project build and focused unit tests have known baseline results.

Validation commands:

```powershell
git status --short --untracked-files=all
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
```

### Step 1.2: Build the root-model consumer matrix

Classify every direct-root view model and update request by actual consumer before moving it. Keep Web-to-API wire contracts in a deliberate shared client/contracts boundary initially. Keep page-local form state with its page feature. Do not create a generic `Models/` dumping ground.

Files:

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - Serialization and deserialization ownership evidence
* `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModel.cs` - Page-local editing state
* `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModelMapper.cs` - Page-local translation boundary
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs` - Test construction consumers
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs` - Transport contract consumers

Discrepancy references:

* DR-01 and DD-01 in `.copilot-tracking/plans/logs/2026-08-04/web-project-folder-structure-log.md`

Success criteria:

* Each root model is labeled as shared transport contract, feature-local state, shell/authentication state, or deferred pending more evidence.
* The first move set contains only unambiguous page-local files.
* No API contract is duplicated or moved solely because one page displays it.

Dependencies:

* Step 1.1 baseline

### Step 1.3: Confirm move constraints and test coverage

Confirm the Web SDK's recursive item discovery, preserve the existing `Authentication`, `Components/Layout`, `Components/Authorization`, shell, and `wwwroot` boundaries, and inventory namespace/import/test impacts. Confirm the existing Web Unit, Functional, and E2E projects are available as separate validation layers. Decide the Razor namespace strategy before the first move: add an explicit existing namespace to each moved Razor component, add a scoped `_Imports.razor`, or approve the generated feature namespace and update all consumers.

Files:

* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` - No explicit compile/content/Razor globs
* `src/TNC.Trading.Platform.Web/Components/_Imports.razor` - Shared Razor imports
* `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs` - Presenter and layout registration
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs` - Shared bUnit composition
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/` - Distributed functional coverage
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/` - Playwright distributed coverage

Success criteria:

* The move list distinguishes physical moves from namespace changes.
* All known direct imports, service registrations, and test setup points are listed before namespace changes.
* The chosen Razor namespace strategy is recorded and has a focused compile check before the first feature move.
* Non-root hosting changes are explicitly out of scope unless separately approved.

## Implementation Phase 2: Namespace-preserving feature organization

<!-- parallelizable: false -->

### Step 2.1: Move the Configuration page-local slice

Create `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/` and move `Configuration.razor`, `ConfigurationPagePresenter.cs`, `ConfigurationFormModel.cs`, and `ConfigurationFormModelMapper.cs` together. Apply the Phase 1 Razor namespace decision before or during the move, preserve the `@page "/configuration"` template, and leave shared API request/response contracts in their selected shared boundary.

Files:

* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/` - New feature folder
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor` - Move into feature folder
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationPagePresenter.cs` - Move with page
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModel.cs` - Move with page
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModelMapper.cs` - Move with page

Success criteria:

* The page, presenter, form model, and mapper compile with the explicitly selected namespaces and imports.
* `/configuration` remains the same route.
* Focused Web build and Configuration tests pass before the next slice.

Dependencies:

* Phase 1 ownership matrix

Validation command:

```powershell
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter FullyQualifiedName~Configuration
```

### Step 2.2: Move the remaining page slices one at a time

Move each cohesive page and directly owned presenter/form code into the following folders, preserving route templates and existing namespaces unless an intentional namespace decision is approved: `Home/`, `Status/`, `IgLogin/`, and `Administration/`. Keep `AccessDenied.razor` and `Error.razor` in the existing Pages boundary unless their ownership is independently established.

Files:

* `src/TNC.Trading.Platform.Web/Components/Pages/Home/` - Home page slice
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/` - Status page slice
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/` - IG login history slice
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/` - Administration slice
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/` - Corresponding component and presenter tests

Success criteria:

* `/`, `/status`, `/ig-login/history`, and `/administration/authentication` remain unchanged.
* Each slice passes a Web build and its focused unit tests before the next slice begins.
* Shared response contracts remain single-source and are not copied into feature folders.

Dependencies:

* Step 2.1 completion

### Step 2.3: Update references only when the feature boundary changes

If namespaces remain unchanged, retain existing imports and update only paths affected by the move. If a namespace is intentionally changed to match a feature folder, update `Components/_Imports.razor`, component-level imports, `PlatformWebUiServiceCollectionExtensions.cs`, `PlatformComponentTestContext.cs`, direct presenter tests, and test data imports in the same slice. Preserve `Properties/InternalsVisibleTo.cs` and assembly accessibility.

Success criteria:

* No namespace update is made as an accidental side effect of a physical move.
* Every changed namespace has matching production registration and test updates.
* The Web project and affected unit tests compile after each namespace change.

Dependencies:

* Step 2.1 or Step 2.2, only when the slice requires namespace changes

## Implementation Phase 3: Deliberate shared-boundary decision

<!-- parallelizable: false -->

### Step 3.1: Finalize shared client/contracts ownership

Using the consumer matrix, decide whether transport models remain at the root temporarily or move as a complete, deliberately named client/contracts group. Do not use `Models/` as a generic destination. Move only complete one-type-per-file units and update all `PlatformApiClient`, presenter, component, and test consumers together.

Files:

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - Transport boundary to preserve or relocate deliberately
* `src/TNC.Trading.Platform.Web/` root view-model files - Move only after classification
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs` - Contract test consumers
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs` - Shared test fixtures

Discrepancy references:

* DR-02 and DD-02 in `.copilot-tracking/plans/logs/2026-08-04/web-project-folder-structure-log.md`

Success criteria:

* Each retained or moved model has one documented owner.
* No duplicate response/request types or broad technical dumping folder is introduced.
* Transport serialization tests continue to pass.

Dependencies:

* Phase 2 slices and completed consumer matrix

## Implementation Phase 4: Route, asset, and distributed validation

<!-- parallelizable: false -->

### Step 4.1: Verify component routing and static shell contracts

Verify all route templates remain present, `Routes.razor` still discovers `typeof(Program).Assembly` and uses `MainLayout`, and `App.razor` still references `<base href="/">`, `app.css`, Radzen assets, `platform-theme.js`, and Blazor bootstrapping. Keep `wwwroot/` unchanged and do not alter root-relative navigation in `MainLayout.razor` or `PlatformHeader.razor`.

Files:

* `src/TNC.Trading.Platform.Web/Components/Routes.razor` - Router contract
* `src/TNC.Trading.Platform.Web/Components/App.razor` - Document and asset contract
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` - Root-relative navigation
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor` - Header navigation
* `src/TNC.Trading.Platform.Web/wwwroot/` - Static assets to preserve

Success criteria:

* All seven researched route templates remain unchanged.
* The source move has not changed hosting base path or asset paths.
* Any missing route or asset is treated as a migration defect, not a folder-layout side effect.

### Step 4.2: Run layered Web validation

Run focused unit tests after each slice, then the Web Functional and E2E suites against Aspire/AppHost infrastructure. Before completion, produce a route/static-asset coverage map for all seven routes and the shell assets. Where existing tests do not provide evidence for a route or asset, add a focused browser or distributed smoke check. Run solution build and tests after all slices; do not duplicate expensive authentication journeys when existing evidence is sufficient.

Validation commands:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test TNC.Trading.Platform.slnx --no-restore
```

Success criteria:

* Web unit, Functional, and E2E tests pass, or exact environment/baseline failures are recorded.
* A route/static-asset coverage map exists, with focused smoke coverage added for every uncovered route or shell asset.
* Solution build and tests pass without unrelated scope expansion.
* Final diff contains only intended folder, namespace, import, and test updates.

Dependencies:

* All implementation phases complete

### Step 4.3: Record blockers and handoff

Document any Docker, Aspire, browser, SDK, or baseline failures with the exact command, affected scope, and next action. Defer non-folder product fixes to a separate work package.

Success criteria:

* The final handoff distinguishes migration defects from pre-existing or environment failures.
* No production behavior change is hidden inside the organizational cleanup.

## Dependencies

* .NET SDK selected by `global.json`
* Existing restore state or network access for `--no-restore` validation
* Docker/Aspire prerequisites for Functional and E2E tests
* Playwright browser installation for E2E tests

## Success Criteria

* Root clutter is reduced through feature-owned folders without a generic `Models/` dumping ground.
* Startup, authentication, routing, static assets, and root-relative navigation remain behaviorally unchanged.
* Every moved type has updated and passing production/test references.
* Layered validation proves the organization change does not regress unit, distributed functional, E2E, build, or solution test behavior.
