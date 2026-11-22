# Web Structure Alternatives Research

- Date: 2026-08-04
- Scope: Research-only review of `src/TNC.Trading.Platform.Web` reorganization options.
- Constraint: Do not modify production code or tests.

## Research questions

- What conventions currently govern the Web project and its tests?
- Which folder structures are viable for this Blazor Web project?
- What risks exist for routing, static assets, Razor component discovery, generated files, namespaces, and test layout?
- Which staged, low-risk approach should be used?

## Findings

### Repository rules

- `.github/instructions/folders.instructions.md:16-24` requires product code to remain under `src/`, tests under `test/`, and all references to be updated when files move. It recommends mirroring production structure in tests.
- `.github/instructions/architecture.instructions.md:55-76` requires feature-oriented organization for product code, but explicitly treats folders as one possible boundary and says existing boundaries should be preserved during incremental work.
- `.github/instructions/csharp.instructions.md:21-29` requires one top-level C# type per file and the file name to match the type. This favors moving complete type files rather than combining models during reorganization.
- `.github/instructions/tests.instructions.md:24-35` standardizes tests under `test/<Service>/<Service>.<TestType>/`; the existing Web unit-test project already follows that shape.
- The repository references `.github/instructions/markdown.instructions.md` from the provided instruction set, but that file is absent in the workspace. The external HVE Markdown instruction path supplied by the environment was also unavailable, so Markdown-specific rules could not be verified.

### Current Web project shape

- `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-19` is an SDK Web project targeting `net10.0`, with nullable and implicit usings enabled. It has project references to Application and ServiceDefaults and package references to OpenID Connect and Radzen. It contains no explicit `Compile`, `Content`, or Razor item globs, so SDK default recursive discovery is currently doing the work.
- `src/TNC.Trading.Platform.Web/Program.cs:13-15` registers Razor Components with interactive server components; `src/TNC.Trading.Platform.Web/Program.cs:73-75` maps static assets and Razor components, adding interactive server render mode.
- `src/TNC.Trading.Platform.Web/Components/App.razor:10-19` references root static URLs `app.css` and `platform-theme.js`, Radzen's package static asset, and `_framework/blazor.web.js`. These files must remain published as static assets even if component folders change.
- `src/TNC.Trading.Platform.Web/Components/Routes.razor:1-12` owns the router, uses `typeof(Program).Assembly` for route discovery, and sets `Layout.MainLayout` as the default layout. Moving routable `.razor` files changes no URL by itself, but changing namespaces, assembly, or layout imports can break discovery or rendering.
- `src/TNC.Trading.Platform.Web/Components/_Imports.razor:1-15` supplies framework, Radzen, application, authentication, component, and layout namespaces. It imports `TNC.Trading.Platform.Web` globally for Razor, which currently exposes all root-namespace view models to components.
- Routable components are currently limited to eight `@page` declarations: `Home` (`/`), `Status` (`/status`), `IgLoginHistory` (`/ig-login/history`), `Configuration` (`/configuration`), `Administration` (`/administration/authentication`), `AccessDenied` (`/authentication/access-denied`), and `Error` (`/error`). The declarations are in `src/TNC.Trading.Platform.Web/Components/Pages/*.razor`, all at line 1.
- The existing physical organization is already partly coherent: `Components/Layout/` contains shell components and state, `Components/Pages/` contains routes and page presenters/form models, and `Components/Authorization/` contains authorization redirect code. Authentication infrastructure is separately under `Authentication/`.
- Root-level C# files still contain API boundary/view models and shared services, including `PlatformApiClient.cs`, `*ViewModel.cs`, `PlatformWebUiServiceCollectionExtensions.cs`, and `Program.cs`. The root view models use namespace `TNC.Trading.Platform.Web` at line 1 of each file; this is a namespace choice, not a requirement imposed by their current physical location.

### Namespace and usage evidence

- `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:2-3` imports `Components.Pages` and `Components.Layout`, then registers `ConfigurationPagePresenter`, `HomePagePresenter`, `StatusPagePresenter`, `PlatformThemeState`, and `PlatformShellContextProvider` in `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:9-17`. Moving these types requires updating this composition point or preserving namespaces.
- `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationPagePresenter.cs:1-3`, `HomePagePresenter.cs:1-3`, and `StatusPagePresenter.cs:2-4` already use the `TNC.Trading.Platform.Web.Components.Pages` namespace, matching their folder. `Components/Layout/*.cs` similarly use `TNC.Trading.Platform.Web.Components.Layout`.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:18-19` imports Pages and Layout namespaces, and `:116-130` registers and constructs presenters, shell context, and theme state directly. Namespace changes therefore affect test compilation and test dependency setup even when runtime behavior is unchanged.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs:3` imports Layout, while its root-namespace view-model references resolve through the test project's implicit/root visibility. Moving models to a new namespace will require explicit test imports or fully qualified names in this shared test fixture.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs:4` imports Pages and renders `Status` at `:54`, so route-component moves require test type/import updates even though bUnit does not route by URL in this test.
- `src/TNC.Trading.Platform.Web/Properties/InternalsVisibleTo.cs:3` grants internals to the Web unit-test assembly. A folder move does not affect this, but splitting projects or changing assembly boundaries would.

### History

- `cd20859` introduced the `Components/Layout/MainLayout.razor` path; `6e577eb` introduced `Components/Pages/Home.razor` and the role-based route surface. `f22d6a9` established the shared shell/theme structure. The path history for `Home.razor` and `MainLayout.razor` shows repeated modifications at those stable paths rather than subsequent relocations.
- `83d84f8` added page presenters and configuration form models under `Components/Pages/`, while leaving API/view models at the project root. This is evidence of an incremental boundary already in use: page-specific presentation code is colocated with pages, while shared API contract models remain root-level.
- `f5bcb9c` added IG login history UI under `Components/Pages/IgLoginHistory.razor` and related view models at the root. A future feature slice should account for this existing cross-page model reuse rather than blindly moving every model beside one page.
- The history therefore favors staged moves that preserve the established `Components` shell and route paths, with each feature migrated independently and tested before the next.

### Generated and static-file risks

- `src/TNC.Trading.Platform.Web/obj/` contains generated Razor/build/static-web-assets files, including `rjsmrazor.dswa.cache.json`, `staticwebassets.build.json`, and generated assembly/source files. `.gitignore:17-20` ignores `obj/`, `bin/`, and `artifacts/`; these must not be treated as source files or moved manually.
- `src/TNC.Trading.Platform.Web/wwwroot/app.css` and `wwwroot/platform-theme.js` are published static assets. `App.razor` uses root-relative URLs, so moving them into a feature folder would change the URL contract unless the markup and static asset pipeline are deliberately changed. Keeping `wwwroot/` stable is the lowest-risk choice.
- With default SDK item discovery, a physical move of `.cs` or `.razor` files should be picked up without editing the project file. An explicit `ItemGroup` or custom Razor configuration was not found in the Web project, but this assumption must be rechecked after every move by build and route/component tests.

## Alternatives

### Alternative A: Preserve `Components`, organize only shared C# files by role

```text
src/TNC.Trading.Platform.Web/
	Components/
		Authorization/
		Layout/
		Pages/
	Authentication/
	Client/                 # PlatformApiClient and HTTP-facing models
	Models/                 # shared Web view models, namespace may remain unchanged initially
	Composition/             # optional service-registration extension
	wwwroot/
```

- Benefits: smallest disruption; route `.razor` paths, `Routes.razor`, `App.razor`, `wwwroot`, and test project location remain unchanged. Folder-only moves can preserve namespaces, making the first step mostly physical.
- Risks: `Client`/`Models` can become a technical grouping that conflicts with the repository's feature-oriented architecture guidance. The root `PlatformApiClient` and models are used across pages and tests, so deciding ownership needs an inventory rather than mechanical moves.
- Best use: low-risk cleanup and a staging point before feature slices.

### Alternative B: Feature slices under `Components/Pages/<Feature>`

```text
Components/
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
		IgLogin/
			IgLoginHistory.razor
		Administration/
			Administration.razor
	Layout/
	Authorization/
```

- Benefits: aligns most closely with the existing page/presenter placement and `architecture.instructions.md`; feature ownership is visible without creating a new top-level UI root. It keeps shared shell and auth boundaries intact.
- Risks: moving route components may expose missing imports because `_Imports.razor` is currently at `Components/`, while nested `_Imports.razor` files could change namespace availability. Tests that render component types need namespace updates. Shared view models used by multiple pages need a deliberate shared location.
- Routing risk is moderate, not URL-level: `@page` strings remain stable, but generated component namespaces and `Routes.razor` discovery must be verified by build and bUnit/functional checks.
- Best use: preferred target after a low-risk preparatory phase.

### Alternative C: Technical UI layers under `Pages`, `Shared`, `Services`, and `Models`

```text
Pages/       # all routable components
Shared/      # layouts and reusable components
Services/    # API clients and presenters
Models/      # all Web view models
Authentication/
wwwroot/
```

- Benefits: familiar to many Blazor teams, simple to explain, and easy to apply mechanically.
- Risks: it makes technical type the primary organization, contrary to the repository's feature-slice guidance. It increases the chance of broad shared-model coupling and does not express which models belong to configuration, status, IG login, or administration. Moving presenters away from pages also weakens the existing local convention.
- Routing/static-asset risk is manageable, but namespace churn and test fixture churn are high because many files change category at once.
- Best use: viable only if the team explicitly chooses technical layering over the current feature-oriented direction.

### Alternative D: Full `Features/<Feature>` vertical slices with a thin `Components` shell

```text
Components/
	App.razor
	Routes.razor
	_Imports.razor
	Layout/
	Authorization/
Features/
	Overview/
	Status/
	Configuration/
	IgLogin/
	Administration/
Authentication/
wwwroot/
```

- Benefits: strongest alignment with the architecture instruction's vertical-slice model; each feature can own route component, presenter, UI models, and feature-local helpers.
- Risks: largest namespace and import migration; shared `PlatformApiClient` contracts and cross-page shell data need explicit boundaries. A full move can obscure whether a type is a UI model, an API contract, or shared infrastructure. It also creates the largest test update surface and makes intermediate builds harder to review.
- Routing risk is higher because both folder and component namespaces change, although route URLs remain textual `@page` contracts. Static assets should still remain in `wwwroot`; generated `obj` content must remain ignored and regenerated.
- Best use: long-term destination only after feature ownership is proven by smaller moves.

## Recommendation

Use **Alternative B as the target**, reached through **Alternative A's folder-only preparation**. Preserve `Components/App.razor`, `Components/Routes.razor`, `Components/_Imports.razor`, `Components/Layout`, `Components/Authorization`, `Authentication`, and `wwwroot` during the first stages.

Recommended stages:

1. Inventory each root-level model and classify it as shared API boundary data, feature-local UI state, or shell/auth state. Do not rename namespaces yet.
2. Move only a small, cohesive set of C# files physically, preserving their existing namespace. Start with a feature's presenter/form models or a clearly shared client/model group. Avoid moving routable `.razor` files in the first change.
3. Build the Web project and run the Web unit-test project after each move. Confirm generated files remain confined to `obj/` and no tracked artifacts appear.
4. Move one page and its directly owned presenter/form models into `Components/Pages/<Feature>/`, then update namespaces/imports deliberately. Keep `@page` values unchanged.
5. Update the mirrored test folders only when test ownership or namespaces actually change; do not create a second test project solely because production folders moved.
6. Repeat per feature, then consider Alternative D only if feature boundaries justify a broader namespace migration.

This balances the repository's feature-slice rule with its history of stable `Components` paths. It also isolates the highest-risk changes, namespace updates and route component moves, from low-risk physical organization.

## Validation checks

- Before each move: `git status --short -- src/TNC.Trading.Platform.Web test/TNC.Trading.Platform.Web`; record any pre-existing changes and do not overwrite them.
- After each C# move: `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore`.
- After each namespace or component move: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore`.
- Confirm all eight route templates remain present and unchanged by searching `src/TNC.Trading.Platform.Web/Components` (or the new feature folders) for `@page` declarations.
- Confirm `Routes.razor` still points at `typeof(Program).Assembly`, still uses `Layout.MainLayout`, and still renders the authorization fallback.
- Confirm `App.razor` still resolves `app.css`, `platform-theme.js`, Radzen's `_content` script, and `_framework/blazor.web.js`; optionally inspect the published output for all static assets.
- Run the relevant bUnit tests for each moved page, plus `dotnet test` for the Web unit-test project. For route/auth behavior, run the existing functional/E2E path if available rather than relying only on component rendering.
- Run the solution-level validation required by the repository after the staged work is complete: `dotnet build TNC.Trading.Platform.slnx` and `dotnet test TNC.Trading.Platform.slnx`.
- Current baseline check performed during research: `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore` succeeded. It emitted the existing `NU1903` high-severity `Microsoft.OpenApi` advisory through ServiceDefaults; this is unrelated to folder organization.

## Gaps and follow-on research

- The exact target namespace policy for feature folders is not declared. Decide whether the first phase preserves `TNC.Trading.Platform.Web` namespaces or adopts namespaces matching physical folders such as `TNC.Trading.Platform.Web.Components.Pages.Status`.
- No published-output or browser-level route/static-asset check was run in this research. Before moving route components, verify the project's hosting mode and add or run a functional smoke check for `/`, `/status`, `/configuration`, `/ig-login/history`, and `/administration/authentication` under the repository's auth setup.
- The inventory identifies direct type usages, but does not classify every root view model's intended ownership. A follow-up should map each model to API contract, feature, shell, or authentication responsibility before moving it.
- The absent repository Markdown instruction file should be restored or its intended location clarified before relying on Markdown-specific formatting rules for future research artifacts.
- No production code or tests were modified; only this ignored research document was created and populated.
