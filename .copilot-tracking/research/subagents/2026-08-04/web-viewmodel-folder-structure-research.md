# Web ViewModel Folder Structure Research

- Research date: 2026-08-04
- Scope: `src/TNC.Trading.Platform.Web`, especially root-level files and ViewModel files; references/usages, architectural conventions, tests, and prior cleanup/work-package evidence relevant to relocating ViewModels.
- Status: Complete
- Method: Read actual source, project, test, documentation, tracking, and history files. No product source or test files were modified.

## Current Structure

The Web project is an ASP.NET Core Web SDK project targeting `net10.0`, referencing Application and ServiceDefaults, with OpenID Connect and Radzen dependencies. It has no explicit compile/content/Razor globs, so normal SDK recursive file discovery applies. Evidence: `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-21`.

The established folders are:

- `Authentication/`: OIDC/cookie registration, token and scope decisions, sign-in/sign-out endpoints, operator context, and auth audit. Its files use `TNC.Trading.Platform.Web.Authentication`.
- `Components/Layout/`: shell, header/navigation, environment state, and theme state.
- `Components/Authorization/`: authorization redirect behavior.
- `Components/Pages/`: routable pages and page-specific presenters/form models. The working tree currently contains pre-existing moves of pages into feature subfolders such as `Configuration/`, `Home/`, `Status/`, `IgLogin/`, and `Administration/`; these changes were not made by this research and were left untouched.
- `wwwroot/`: browser assets. `Components/App.razor` keeps the root-relative asset contract and `Components/Routes.razor` discovers routes from `typeof(Program).Assembly`.

The ViewModels are currently flat at the project root. The inventory found 22 `*ViewModel.cs` files, each with one top-level type and the namespace `TNC.Trading.Platform.Web` at line 1. The concrete files are:

- Response/read models: `AuthAdministrationViewModel.cs:1-5`, `AuthStateViewModel.cs:1-7`, `CredentialPresenceViewModel.cs:1-7`, `IgLoginHistorySnapshotViewModel.cs:1-12`, `IgLoginSnapshotViewModel.cs:1-12`, `IgLoginStatusViewModel.cs:1-14`, `IgProofDataViewModel.cs:1-7`, `ManualRetryViewModel.cs:1-3`, `NotificationSettingsViewModel.cs:1-5`, `PlatformConfigurationViewModel.cs:1-11`, `PlatformEventItemViewModel.cs:1-12`, `PlatformEventsViewModel.cs:1-5`, `PlatformStatusViewModel.cs:1-14`, `RetryPolicyViewModel.cs:1-9`, `RetryStateViewModel.cs:1-9`, `TradingScheduleStateViewModel.cs:1-6`, and `TradingScheduleViewModel.cs:1-10`.
- Configuration update/request models: `UpdateCredentialsViewModel.cs:1-9`, `UpdateNotificationSettingsViewModel.cs:1-9`, `UpdatePlatformConfigurationViewModel.cs:1-17`, `UpdateRetryPolicyViewModel.cs:1-13`, and `UpdateTradingScheduleViewModel.cs:1-15`.

The count is 22 files if the five update models are counted in addition to the 17 response/read models. The repository's earlier tracking evidence calls this same set 17 read models plus 5 update models: `.copilot-tracking/plans/logs/2026-08-04/web-project-folder-structure-log.md:78-86`.

`ConfigurationFormModel` and `ConfigurationFormModelMapper` are already page-local under `Components/Pages/Configuration/` in the current working tree. They translate between the shared configuration response and editable request model, including text parsing for times, trading days, and bank holidays: `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModel.cs:1-47` and `ConfigurationFormModelMapper.cs:1-87`.

## References And Usages

`PlatformApiClient` is the serialization boundary for the root ViewModels. It deserializes status, configuration, events, administration, IG history, and manual-retry responses, and serializes `UpdatePlatformConfigurationViewModel`: `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:9-129`.

Production consumers are distributed across the Web feature surface:

- `HomePagePresenter` consumes `PlatformStatusViewModel` and `PlatformEventsViewModel`; `StatusPagePresenter` consumes the same models plus `RetryStateViewModel`; `ConfigurationPagePresenter` and `ConfigurationFormModelMapper` consume configuration and update models. Evidence: `src/TNC.Trading.Platform.Web/Components/Pages/Home/HomePagePresenter.cs:5-76`, `Status/StatusPagePresenter.cs:6-112`, `Configuration/ConfigurationPagePresenter.cs:5-31`, and `Configuration/ConfigurationFormModelMapper.cs:5-87`.
- `PlatformShellContextProvider` consumes `PlatformApiClient` and loads status for shell environment state: `src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs:5-38`.
- Razor pages directly hold `AuthAdministrationViewModel`, `PlatformStatusViewModel`, `PlatformEventsViewModel`, and `IReadOnlyList<IgLoginHistorySnapshotViewModel>` state. Evidence: `Components/Pages/Administration/Administration.razor:41`, `Home/Home.razor:110-111`, `IgLogin/IgLoginHistory.razor:95`, and `Status/Status.razor:231-232`.
- `Components/_Imports.razor:1-15` imports `TNC.Trading.Platform.Web` for all Razor components. A namespace move would therefore require an intentional import update, even where physical movement alone would not affect compilation.
- `Program.cs:1-18` registers the Web client and UI services. `PlatformWebUiServiceCollectionExtensions.cs:1-20` is the composition point for page presenters and layout/theme services.

Test consumers are concentrated rather than spread across every test:

- `PlatformWebTestData` constructs nearly every response model, including nested status/IG/retry/schedule records, configuration records, events, manual retry, snapshots, and auth administration: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs:7-160`.
- `PlatformApiClientTests` constructs all five update models and exercises response parsing and request serialization: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:106-151`.
- The Web unit-test project references the Web project and uses `InternalsVisibleTo`; no project-file change is required for a physical move, but namespace changes affect test source and test fixture setup. Evidence: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:1-28` and `src/TNC.Trading.Platform.Web/Properties/InternalsVisibleTo.cs:1-3`.
- Separate Web Functional and E2E projects exist for host, route, authentication, and browser behavior. The current folder-structure log records 6/6 Functional and 1/1 E2E passes after page moves, while noting limited direct coverage for some routes and static assets: `.copilot-tracking/plans/logs/2026-08-04/web-project-folder-structure-log.md:123-144`.

## Architectural Conventions

- The repository favors responsibility ownership and dependency direction, not mechanically named folders: `.github/lessons.md:8`.
- C# files follow one top-level type per matching file, which supports moving complete ViewModel files without bundling unrelated types. The prior research records this convention at `.copilot-tracking/research/2026-08-04/web-project-folder-structure-research.md:67-72`.
- Existing Web namespaces already express architecture: root Web contracts, `Authentication`, `Components.Layout`, `Components.Authorization`, and `Components.Pages`. Page presenters and page-local form types use `TNC.Trading.Platform.Web.Components.Pages`, matching their owning boundary: `.copilot-tracking/research/2026-08-04/web-project-folder-structure-research.md:45-55`.
- Work package `004-ui-update-and-refactor` explicitly says to preserve the existing API/ViewModel surface while improving the UI shell: `docs/004-ui-update-and-refactor/technical-specification.md:50-56`.
- Work package `006-refactor-app` requires small, behavior-preserving refactors at the nearest owning boundary and rejects broad redesign: `docs/006-refactor-app/technical-specification.md:34-53`.
- The wiki describes Web as the Blazor operator UI and records `PlatformApiClient.cs` as the location of the client and ViewModel inventory: `docs/wiki/application-overview.md:27-67` and `docs/wiki/codebase-guideline-alignment-delivery-plan.md:95-126`.

## Prior Cleanup Evidence

The prior folder-structure research and plan already investigated this question. The strongest conclusions are:

- Do not create broad technical dumping grounds such as `Models/`, `Services/`, or `Pages/` solely to reduce root file count.
- Move feature-owned page code incrementally below `Components/Pages/<Feature>`.
- Keep shared Web/API contracts in a deliberate shared boundary until consumer classification proves a smaller owner.
- Preserve namespaces during initial physical moves; make namespace changes a separate deliberate step.
- Start with Configuration because its form model and mapper are demonstrably page-local.

Evidence: `.copilot-tracking/research/2026-08-04/web-project-folder-structure-research.md:95-163` and `.copilot-tracking/plans/logs/2026-08-04/web-project-folder-structure-log.md:5-30,78-122`.

The earlier cleanup work package also explicitly split each Web ViewModel/update model into its own matching file while keeping `PlatformApiClient.cs` client-only: `docs/wiki/codebase-guideline-alignment-delivery-plan.md:106-126`. That is prior evidence for file granularity, not evidence that all models should move into a new technical folder.

Git history is consistent with incremental cleanup. Commit `8df68d8` is titled `Refactor Solution towards a Clean Architecture`; surrounding Web history includes presenter extraction and page/auth shell work. The current repository guidance interprets Clean Architecture by responsibility ownership, so this history supports feature adjacency and explicit boundaries rather than a blanket `ViewModels/` relocation.

## Grouping Options

### Option A: Keep the root ViewModel boundary

Keep the 17 response models and 5 update models at the Web project root, retain `PlatformApiClient.cs` there, and continue moving page-owned components/presenters under feature folders.

- Advantages: zero namespace/import churn; preserves the existing serialization boundary; matches the current consumer matrix and prior plan decision.
- Risks: root remains visually mixed and the contract grouping is implicit rather than named.
- Assessment: lowest risk and currently supported by the strongest evidence.

### Option B: Add a deliberate shared contract boundary

Move all shared API response/update models, still namespace-preserving initially, into a deliberately named boundary such as `Client/Contracts/` or `Contracts/`, with `PlatformApiClient.cs` either beside them or in `Client/`.

- Advantages: makes the transport boundary visible and reduces root clutter without pretending models are page-local.
- Risks: every C# consumer and the Razor `_Imports` surface may need updates if the namespace changes; a poorly chosen name can become another dumping ground; public/internal contract ownership remains a design decision.
- Assessment: plausible follow-on, but only after a reviewed consumer matrix and a decision on whether the namespace should change.

### Option C: Group ViewModels by feature

Move status/auth/retry/schedule/IG models near `Components/Pages/Status`, configuration models near Configuration, and administration models near Administration.

- Advantages: feature locality is visible.
- Risks: many models are shared by multiple pages, the API client, shell context, and test fixtures; duplicate or cross-feature dependencies would increase; transport contracts would be confused with page state.
- Assessment: reject for the current set except for already proven page-local types such as `ConfigurationFormModel`.

### Option D: Create a generic `ViewModels/` or `Models/` folder

- Advantages: easy mechanical move and immediate root cleanup.
- Risks: hides ownership, mixes response contracts and editable request state, and conflicts with prior repository research rejecting technical dumping grounds.
- Assessment: reject.

## Recommended Structure

Use the existing staged structure:

```text
src/TNC.Trading.Platform.Web/
	Program.cs
	PlatformApiClient.cs
	PlatformWebUiServiceCollectionExtensions.cs
	*ViewModel.cs                    # shared Web/API transport boundary initially
	Authentication/
	Components/
		App.razor
		Routes.razor
		_Imports.razor
		Layout/
		Authorization/
		Pages/
			Configuration/
				Configuration.razor
				ConfigurationPagePresenter.cs
				ConfigurationFormModel.cs
				ConfigurationFormModelMapper.cs
			Home/
			Status/
			IgLogin/
			Administration/
	Properties/
	wwwroot/
```

Do not relocate the shared ViewModels as a blanket operation yet. Retain them as the shared client/contract owner until a separately approved consumer matrix proves that a named contract boundary improves ownership. Continue feature moves one slice at a time, preserve C# namespaces initially, keep route templates unchanged, and validate the Web build plus the narrowest affected unit tests after each move.

## Risks And Validation

- Physical folder moves are low risk because the Web SDK recursively discovers source files, but namespace/import changes are not low risk. The root `@using TNC.Trading.Platform.Web` in `Components/_Imports.razor:11` is a central compile dependency.
- Route strings are independent of physical folders, but `Routes.razor:1-10`, `App.razor:1-18`, root-relative navigation, and static assets must remain unchanged.
- Shared model relocation can affect `PlatformApiClient`, presenters, Razor pages, `PlatformWebTestData`, `PlatformApiClientTests`, and hidden generated Razor compilation.
- Existing working-tree moves must be preserved and reviewed as user changes; do not use cleanup work to revert or overwrite them.
- Validation should be staged: Web build, filtered Web unit tests for the slice, complete Web unit tests, then Functional/E2E and solution gates only when route or host behavior is touched. The prior log records the exact successful baseline and staged validation commands at `.copilot-tracking/plans/logs/2026-08-04/web-project-folder-structure-log.md:42-58,90-122`.

## Unanswered Questions / Next Research

- Should a future shared boundary be named `Contracts`, `Client/Contracts`, or remain at the project root? Current evidence does not distinguish these sufficiently.
- Should that future move preserve `TNC.Trading.Platform.Web` namespaces permanently, or introduce a contract namespace? This needs an explicit API/client ownership decision because namespace churn is the main migration cost.
- Are any deployment environments expected to host the Web app below a non-root path? That is unrelated to ViewModel placement but would require separate validation of base href, assets, and navigation.
- The current tracking log says the folder-structure implementation is complete, but the working tree still shows page moves. Before any further relocation, reconcile the tracked state and review those existing changes as the source of truth.

## Executive Conclusion

The current root ViewModels are not one accidental category: they form a shared Web-to-API contract surface consumed by the client, multiple presenters/pages, shell state, and test fixtures. The repository evidence supports keeping that shared boundary stable for now, while continuing the already-selected feature-folder migration for page-owned code. A generic `ViewModels/` folder is not recommended. Any future contract-folder move should be a separately scoped, namespace-aware change with a consumer matrix and focused build/test checkpoints.
