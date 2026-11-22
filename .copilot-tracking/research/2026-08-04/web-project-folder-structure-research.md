<!-- markdownlint-disable-file -->
# Task Research: Web Project Folder Structure

Research whether the loose files in `src/TNC.Trading.Platform.Web` should be reorganized into subfolders, and propose a tidy-up approach that preserves build behavior, Blazor conventions, and the repository's existing architecture.

Status: Complete. Research-only; no product source or test files were modified.

## Task Implementation Requests

* Inventory the files currently located directly under `src/TNC.Trading.Platform.Web`.
* Identify natural ownership boundaries using namespaces, routes, registrations, component relationships, and tests.
* Recommend a low-risk folder structure and migration/validation plan.

## Scope and Success Criteria

* Scope: `src/TNC.Trading.Platform.Web`, directly related project configuration, and relevant Web tests and repository conventions.
* Assumptions: This is a research request only; no production files should be moved during this task.
* Success Criteria:
  * Every proposed move is supported by repository evidence.
  * The recommendation distinguishes safe organizational moves from changes that could affect routing, generated assets, or runtime behavior.
  * A staged implementation and validation plan is provided.

## Outline

1. Current layout and evidence
2. Ownership and dependency analysis
3. Candidate structures and trade-offs
4. Recommended structure and migration plan

## Potential Next Research

* Classify the 22 root ViewModels as shared transport contracts, UI projections, feature-local state, or shell/auth state before any physical move.
  * Reasoning: `PlatformApiClient`, Home, Status, shell state, and centralized test fixtures share much of the model set.
  * Reference: `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-folder-structure-research.md`.
* Decide whether a future shared boundary should be named `Client/Contracts` and whether namespaces should remain `TNC.Trading.Platform.Web`.
  * Reasoning: a physical move is low risk under SDK recursive discovery, but a namespace migration affects Razor imports, presenters, and tests.
  * Reference: `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-organization-alternatives-research.md`.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-21`
  * ASP.NET Core Web SDK project targeting `net10.0`; no explicit compile/content/Razor globs, so SDK recursive discovery is authoritative.
  * References Application and ServiceDefaults, plus OpenID Connect and Radzen.
* `src/TNC.Trading.Platform.Web/Program.cs:1-78`
  * Owns host startup, service registration, middleware, the root sign-in/session-token gate, static assets, authentication endpoints, Razor component mapping, and default endpoints.
* `src/TNC.Trading.Platform.Web/Components/App.razor:1-62`
  * Owns the document shell, `<base href="/">`, root-relative `app.css` and `platform-theme.js`, Radzen assets, and Blazor bootstrapping.
* `src/TNC.Trading.Platform.Web/Components/Routes.razor:1-10`
  * Owns router discovery through `typeof(Program).Assembly`, the default `MainLayout`, authorization route rendering, and unauthorized fallback behavior.
* `src/TNC.Trading.Platform.Web/Components/_Imports.razor:1-15`
  * Supplies shared Razor namespaces, including the root Web namespace, Pages, Layout, authentication, authorization, Application, and Radzen namespaces.
* `src/TNC.Trading.Platform.Web/Authentication/`
  * Is already a cohesive boundary for OIDC/cookie registration, sign-in/sign-out endpoints, token and scope decisions, operator context, and auth audit.
* `src/TNC.Trading.Platform.Web/Components/Layout/`
  * Is already a cohesive shell boundary for `MainLayout`, header/navigation controls, environment state, and theme state.
* `src/TNC.Trading.Platform.Web/Components/Pages/`
  * Already groups routable pages and page-specific presenters/form models. Current routes are in `Home.razor`, `Status.razor`, `Configuration.razor`, `IgLoginHistory.razor`, `Administration.razor`, `AccessDenied.razor`, and `Error.razor`.
* `src/TNC.Trading.Platform.Web/wwwroot/`
  * Contains published browser assets. `App.razor:9-10` and `wwwroot/app.css:1-100` establish root-relative static asset contracts.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/`
  * Mirrors page/layout ownership with bUnit tests and centralizes setup in `PlatformComponentTestContext.cs:20-190`.

### Direct Root Inventory

The root contains 28 direct files. The useful classification is:

| Classification | Files | Recommendation |
| --- | --- | --- |
| Host/build/configuration | `Program.cs`, `TNC.Trading.Platform.Web.csproj`, `appsettings.json`, `Properties/*` | Keep at established paths. These are host contracts, not loose UI files. Treat `.csproj.lscache` as generated tooling cache and do not organize it manually. |
| Composition and shared transport | `PlatformWebUiServiceCollectionExtensions.cs`, `PlatformApiClient.cs` | Candidate for a deliberately named `Composition/` or `Client/` boundary only if the team wants that distinction; do not move mechanically before checking consumers. |
| Shared Web/API view models | `Auth*ViewModel.cs`, `CredentialPresenceViewModel.cs`, `Ig*ViewModel.cs`, `Platform*ViewModel.cs`, `Retry*ViewModel.cs`, `TradingSchedule*ViewModel.cs`, and update request models | Classify by actual consumer first. Some are shared API boundary contracts; others are feature-local UI state. A blanket `Models/` move would hide ownership. |
| Existing cohesive UI boundaries | `Components/*`, `Authentication/*`, `wwwroot/*` | Preserve initially. These folders already express the strongest local architecture. |

The current root inventory contains 22 `*ViewModel.cs` files: 17 response/read models and five configuration update/request models. The five update models are `UpdateCredentialsViewModel`, `UpdateNotificationSettingsViewModel`, `UpdatePlatformConfigurationViewModel`, `UpdateRetryPolicyViewModel`, and `UpdateTradingScheduleViewModel`. The full file-level inventory and consumer notes are recorded in `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-folder-structure-research.md` under `Current Structure` and `References And Usages`.

### Code Search Results

* `PlatformWebUiServiceCollectionExtensions.cs:2-17` imports and registers page presenters plus layout/theme services, making it the main composition point affected by namespace changes.
* `Components/Pages/ConfigurationPagePresenter.cs:1-3`, `HomePagePresenter.cs:1-3`, and `StatusPagePresenter.cs:2-4` already use namespaces matching their folder.
* `test/.../PlatformComponentTestContext.cs:18-19,116-130` imports and directly constructs page/layout services; namespace changes affect shared test setup even when runtime behavior is unchanged.
* `Properties/InternalsVisibleTo.cs:1-3` grants the Web test assembly access to internal types; folder moves do not require changing the assembly boundary.

### External Research

No external research was required. The recommendation is grounded in repository instructions, the Web project, its closest tests, and local history.

### Project Conventions

* `.github/instructions/folders.instructions.md:16-24` requires preserving top-level layout, keeping product code under `src/`, tests under `test/`, and updating references after moves.
* `.github/instructions/architecture.instructions.md:55-76` favors feature-oriented organization while preserving established boundaries during incremental work.
* `.github/instructions/csharp.instructions.md:21-29` favors one top-level type per file with matching file names, so moves should relocate complete files rather than combine models.
* `.github/instructions/tests.instructions.md:24-35` supports the existing Web unit-test project shape and production/test mirroring.

## Key Discoveries

### The root is mixed, but not all root files are misplaced

The root is carrying three different concerns: host startup/configuration, shared Web-to-API contracts/services, and feature data models. The existing `Components`, `Authentication`, and `wwwroot` boundaries are already coherent. The cleanup should therefore target the mixed C# surface, not move every file merely because it is not nested.

### The current project is sensitive to namespace/import changes, not physical folder names alone

The Web SDK recursively discovers `.cs` and `.razor` files, so physical moves do not require project-file globs. However, Razor imports and C# namespaces are coupled through `Components/_Imports.razor:1-15`, while tests directly construct presenters and render component types. A namespace migration can break compilation and test setup even when route strings are unchanged.

The model boundary is shared rather than uniformly page-owned. `PlatformApiClient.cs:9-129` serializes and deserializes most of the set; Home and Status share status/event models; `PlatformShellContextProvider.cs:5-38` consumes status for shell state; and `PlatformWebTestData.cs:9-151` plus `PlatformApiClientTests.cs:106-151` centralize model construction and request verification. By contrast, `ConfigurationFormModel.cs` and `ConfigurationFormModelMapper.cs` are already demonstrably page-local under `Components/Pages/Configuration`.

### Routing URLs and hosting paths are separate concerns

The `@page` templates are textual contracts and can survive a folder move. The higher-risk path contracts are `<base href="/">`, root-relative assets, and absolute navigation in `Components/Layout/MainLayout.razor:19-34`, `PlatformHeader.razor:15,29`, and page components. Folder cleanup must not be presented as a path-base change; that would be a separate feature requiring browser/deployment validation.

### Static assets and generated output should remain stable

Keep `wwwroot` unchanged. `obj/` contains generated Razor/static-web-assets files and is ignored by `.gitignore:17-20`; it is not source material and must not be moved or used as an inventory.

### Existing history favors incremental feature ownership

Local history shows `Components/Layout` and `Components/Pages` becoming stable boundaries, with page presenters/form models introduced beside pages while shared API/view models remained at the root. This supports incremental feature slices rather than a one-shot technical-layer reorganization.

## Technical Scenarios

### Recommended: prepare by role, then move toward feature slices

**Requirements:**

* Reduce root-level clutter without destabilizing startup, authentication, routing, or static assets.
* Make ownership visible and keep the test surface understandable.
* Align with existing feature-oriented architecture without forcing premature model decisions.

**Preferred Approach:**

Use a two-stage approach. First classify root models and make only small, namespace-preserving physical moves where ownership is unambiguous. Then migrate one page at a time into feature folders below the existing `Components/Pages` boundary.

Target shape after incremental migration:

```text
src/TNC.Trading.Platform.Web/
  Program.cs
  PlatformApiClient.cs                 # retain at root initially; move only with clear client boundary
  Components/
    App.razor
    Routes.razor
    _Imports.razor
    Layout/
    Authorization/
    Pages/
      Overview/
        Home.razor
        HomePagePresenter.cs
      Status/
        Status.razor
        StatusPagePresenter.cs
      Configuration/
        Configuration.razor
        ConfigurationPagePresenter.cs
        ConfigurationFormModel.cs
        ConfigurationFormModelMapper.cs
      IgLogin/
        IgLoginHistory.razor
      Administration/
        Administration.razor
    # shared API contracts remain in a deliberate shared boundary
  Authentication/
  wwwroot/
```

The exact shared-contract folder should be decided from consumer analysis. Do not create a generic `Models/` folder as a dumping ground.

**Implementation Details:**

1. Record the current working-tree state before each move; preserve unrelated user changes.
2. Classify each root view model as shared API boundary data, feature-local UI state, shell state, or authentication state.
3. Move one cohesive C# group while preserving its namespace. Build the Web project immediately.
4. Move one page and its directly owned presenter/form models into `Components/Pages/<Feature>/`; keep `@page` values unchanged.
5. Update namespaces and `_Imports.razor` only when the feature boundary is intentional. Update the corresponding bUnit imports/setup at the same time.
6. Repeat per feature, reviewing whether a model is truly feature-local before relocating it.

**Validation:**

```text
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx
```

After route moves, verify that all route templates remain present, `Routes.razor` still uses `typeof(Program).Assembly` and `MainLayout`, and `App.razor` still resolves every static asset. Add or run a browser/distributed smoke check for the main routes because the current Web unit tests do not exercise the full host, middleware, static assets, or root sign-in gate. The baseline Web build succeeded during research with the existing `NU1903` `Microsoft.OpenApi` advisory; that warning is unrelated to folder organization.

#### Considered Alternatives

* **Technical folders (`Pages`, `Shared`, `Services`, `Models`)**: easy to apply, but conflicts with the repository's feature-oriented direction and would make shared model ownership less clear.
* **Immediate full `Features/<Feature>` vertical slices**: architecturally strong, but creates the largest namespace/import/test migration and obscures whether current root models are API contracts or UI state.
* **Only add generic `Client/` and `Models/` folders**: lowest immediate risk, but risks creating new dumping grounds and does not resolve feature ownership.

The selected staged approach preserves the stable shell/authentication boundaries, keeps static asset and route contracts unchanged, and lets each move provide a small compile/test checkpoint.

For the immediate cleanup decision, leave the shared ViewModels at the root. This is an intentional stable client boundary, not a claim that the root is the ideal permanent visual layout. If a clearer physical boundary is later approved, prefer `Client/Contracts` over a bare `Contracts` folder, initially preserve namespaces, and move one cohesive contract group before considering namespace changes.

## Open Questions and Follow-Up Research

* Should feature folders adopt namespaces matching their physical folders, or should the first phase preserve namespaces to minimize churn?
* Which root view models are shared API contracts versus feature-local display/update models? This requires a consumer matrix before moving them.
* Is hosting under a non-root path prefix a real deployment requirement? If so, base href, asset URLs, and absolute navigation need a separate audited change.
* No Playwright or distributed Web-host coverage was found in the inspected Web unit-test project; locate any broader tests before the first route move.

The alternative evaluation in `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-organization-alternatives-research.md` rejects feature grouping for the shared model set because Home, Status, shell state, the API client, and tests overlap. It also rejects generic `Models` or `ViewModels` folders because they mix wire contracts with UI state and create a future dumping ground.

## Research Summary

The Web project needs targeted organization, not a wholesale relocation. Keep startup/configuration, `Authentication`, `Components/Layout`, `Components/Authorization`, `Components/App.razor`, `Components/Routes.razor`, and `wwwroot` stable. Classify root view models, then move feature-owned page code incrementally into `Components/Pages/<Feature>` with namespace and test updates verified after each small step.
