# Web ViewModel Organization Alternatives Research

- Research date: 2026-08-04
- Scope: Evaluate root-level Web ViewModel organization alternatives A-D using current source, tests, repository guidance, and prior research.
- Constraint: Research only. Do not modify product source or tests.
- Status: Complete

## Research questions

- Which alternative gives the clearest ownership boundary for the current Web models?
- What namespace/import and test/build risks does each alternative create?
- Which approach should be selected, and what staged implementation is practical?

## Verified current state

The Web project currently has 22 root-level `*ViewModel.cs` files: 17 response/read models and five configuration update/request models. Each file contains one top-level type in the namespace `TNC.Trading.Platform.Web`. The prior inventory is recorded in `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-folder-structure-research.md` under `Current Structure` and cites the individual model files.

These types are transport-facing Web client contracts rather than one homogeneous page-state layer:

- `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:9-129` deserializes status, configuration, events, authentication administration, IG history, and manual-retry responses, and serializes `UpdatePlatformConfigurationViewModel`.
- `src/TNC.Trading.Platform.Web/Components/Pages/Home/Home.razor:110-111` and `Status/Status.razor:231-232` share `PlatformStatusViewModel` and `PlatformEventsViewModel`.
- `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModel.cs` and `ConfigurationFormModelMapper.cs` are already page-local editing state and map to/from the shared configuration contracts. This is evidence that UI-only state can be colocated without moving the wire models.
- `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor:41` and `IgLogin/IgLoginHistory.razor:95` consume authentication and IG history contracts, while `Components/Layout/PlatformShellContextProvider.cs:5-38` consumes status through the client for shell state.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs:9-151` constructs nearly every response model. `PlatformApiClientTests.cs:106-151` constructs all five update models and verifies request serialization.

The Web project is an SDK Web project with no explicit compile or Razor item globs (`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-21`). A physical `.cs` move should therefore be discovered recursively without a project-file edit. That does not make a namespace move free: `src/TNC.Trading.Platform.Web/Components/_Imports.razor:11-16` imports `TNC.Trading.Platform.Web` and the test fixture currently resolves root-namespace contracts through the test project’s normal imports/visibility.

The repository’s architecture guidance says responsibility ownership matters more than canonical folder names and requires preserving valid boundaries incrementally (`.github/instructions/architecture.instructions.md:37-76`). Prior research also found that the repository rejects broad technical dumping grounds and favors feature-local page code while keeping shared API contracts deliberate (`.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-folder-structure-research.md`, `Prior Cleanup Evidence`).

## Alternative evaluation

### A. Leave shared contracts at the Web project root

**Ownership clarity:** Good for the current evidence. The root is already the boundary containing `PlatformApiClient` and its wire models. The location is less visually explicit than a `Contracts` folder, but it does not falsely imply that status, IG, or authentication types belong to one page feature.

**Namespace/import impact:** Minimal. Existing C# namespaces and the Razor root import remain unchanged. No test fixture or bUnit import churn is expected.

**Test/build risk:** Lowest. No project-file change is needed, and the client/test data continue to compile against the current types. Existing route and static-asset behavior is outside this change.

**Assessment:** Select for the current implementation state. Continue moving demonstrably page-local types into their existing feature folders, but do not mechanically move the shared wire contracts merely to reduce root file count.

### B. Move shared contracts to `Contracts` or `Client/Contracts`

**Ownership clarity:** Potentially strongest if the folder is explicitly defined as the Web-to-API contract boundary. `Client/Contracts` communicates both transport ownership and proximity to `PlatformApiClient`; `Contracts` is shorter but more vulnerable to becoming a generic shared bucket. Neither name alone resolves whether these are stable API DTOs or UI-specific models.

**Namespace/import impact:** Low for a folder-only move if the existing `TNC.Trading.Platform.Web` namespaces are retained. Moderate to high if namespaces change: update `Components/_Imports.razor`, C# `using` directives, `PlatformApiClient`, presenters/pages, `PlatformWebTestData`, and `PlatformApiClientTests`. The prior research identifies the same consumer matrix in `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-folder-structure-research.md`, `References And Usages`.

**Test/build risk:** Low-to-moderate for a namespace-preserving physical move; moderate for a namespace migration because test fixtures and generated Razor compilation are affected. The SDK project makes the physical move straightforward, but every consumer must still be validated.

**Assessment:** Keep as a conditional follow-on, not the immediate blanket move. Use it only after a reviewed contract inventory confirms that the models are intentionally owned by the client boundary and the team chooses a namespace policy. If adopted, prefer `Client/Contracts` over a bare `Contracts` folder because `PlatformApiClient` is the owning adapter and the name narrows the boundary.

### C. Group models by feature

**Ownership clarity:** Good only for genuinely page-local state. It is misleading for the current wire models: status and events are used by both Home and Status; configuration contracts are used by the client, configuration page, mapper, and tests; status also feeds shell environment state.

**Namespace/import impact:** High relative to the value. A model used by multiple features would either require cross-feature imports or be duplicated, and feature namespace changes would propagate into Razor, presenters, client code, and test data.

**Test/build risk:** Moderate-to-high. The model files are exercised centrally by `PlatformWebTestData` and client tests, so a broad move creates a large compile/test surface without changing behavior. Duplication would create a more serious contract-drift risk.

**Assessment:** Reject for the shared `*ViewModel` set. Continue using feature folders for `ConfigurationFormModel`, presenters, and other UI-only types whose ownership is already proven.

### D. Create generic `ViewModels` or `Models` folders

**Ownership clarity:** Weak. It groups by technical suffix and mixes response contracts with update requests, hiding the client boundary and the distinction between wire data and page editing state.

**Namespace/import impact:** Low-to-moderate for a namespace-preserving move, but the apparent simplicity is organizational rather than architectural. A later split would cause another move and another import wave.

**Test/build risk:** Low for a pure physical move, but high long-term refactoring risk because the folder encourages new unrelated types to accumulate there. It conflicts with the repository’s feature/ownership guidance and prior rejection of technical dumping grounds.

**Assessment:** Reject. Root clutter alone is not sufficient reason to introduce a generic bucket.

## Selected approach

Select **A for the current state**, with a deliberately staged path that may adopt **B** later. This is the smallest approach consistent with verified ownership: the root-level models form a shared Web-to-API client boundary, while page-local UI state already has a feature owner. The selection preserves the current public/internal type surface and avoids treating a transport contract as page state.

This is not a claim that the root is the ideal permanent visual layout. It is a decision to avoid a speculative namespace and ownership migration until the contract boundary is explicitly named and reviewed. If the team later wants a clearer physical boundary, `Client/Contracts` is the preferred B variant, with namespaces initially preserved and the client contract inventory moved as one cohesive group.

## Practical staged implementation plan

1. **Baseline and inventory.** Record the current working-tree state. Confirm the 22-model inventory, classify each model as shared wire contract, feature-local UI state, or shell/auth state, and record all consumers. Use `PlatformApiClient.cs`, the four Razor page consumers, `PlatformShellContextProvider.cs`, `PlatformWebTestData.cs`, and `PlatformApiClientTests.cs` as the minimum matrix.
2. **Keep shared contracts stable.** Leave the current root `*ViewModel.cs` files and their `TNC.Trading.Platform.Web` namespaces unchanged. Do not create `Models` or `ViewModels` as a temporary dumping ground.
3. **Continue proven feature ownership.** Keep `ConfigurationFormModel` and its mapper under `Components/Pages/Configuration`. For future slices, move only UI-only presenters/forms/page state into the owning feature folder. Do not move a shared response model beside whichever page displays it first.
4. **Validate each feature slice.** For physical C# moves, build `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore`. For namespace or component changes, run the Web unit-test project `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore` as well. The earlier research records the existing Web build baseline and the higher-level Functional/E2E suites.
5. **Gate a future B migration.** Before moving contracts, approve the target name (`Client/Contracts` preferred), namespace policy, and whether the types remain Web-specific or become a separately shared API contract assembly. Produce a consumer checklist covering `_Imports.razor`, `PlatformApiClient`, presenters/pages, `PlatformWebTestData`, and `PlatformApiClientTests`.
6. **Move B in one narrow contract slice.** If approved, first perform a namespace-preserving physical move of a cohesive contract group, build, and run focused client tests. Only then consider changing namespaces, with explicit import updates and a second focused validation checkpoint. Do not combine this with route, base-path, or static-asset changes.
7. **Final regression gate.** After any namespace migration, run the complete Web unit tests and the existing Web Functional/E2E coverage. Preserve all `@page` values, `Routes.razor` assembly discovery, `App.razor` static asset references, and root-relative navigation.

## Remaining gaps

- The repository does not yet state whether `*ViewModel` names are intended to remain Web-specific or should become API/client DTO names. That decision affects whether B should be `Client/Contracts` or a separate shared contract assembly.
- There is no explicit team decision on namespace policy for a future physical move. Preserving `TNC.Trading.Platform.Web` namespaces is the lowest-risk first phase; matching folders requires a deliberate import migration.
- The current evidence proves transport use, but not whether every model is a stable API contract versus a UI projection. That classification should be completed before any feature grouping or renaming.
- Existing Functional/E2E tests cover authentication and protected routes, but this research did not rerun them. A future route-component move should use the existing suites as regression gates; a new route/static-asset test is only needed if those suites do not expose the relevant failure.
- The current working tree contains pre-existing Web folder changes noted by prior research. Any implementation must reconcile that state before moving additional files and must not overwrite unrelated changes.

## Conclusion

The verified lowest-risk choice is to leave the shared contracts at the root for now and keep feature organization focused on page-owned code. `Client/Contracts` is a credible later refinement because it makes the client boundary explicit, but it should be an approved, namespace-aware migration rather than a cleanup sweep. Feature grouping and generic `ViewModels`/`Models` folders are rejected for the current shared contract set because they obscure ownership and create avoidable coupling or future migration cost.
