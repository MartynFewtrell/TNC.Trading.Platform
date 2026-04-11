# Codebase guideline alignment delivery plan

This document plans how the current solution will be brought into alignment with the updated Copilot instruction files, the repository `.editorconfig`, and the Microsoft Learn guidance adopted for C# type organization and naming.

## Summary

- **Source**: See [README](../../README.md) for the current solution overview. See [repo Copilot instructions](../../.github/copilot-instructions.md), the user-level Copilot instructions, [C# instructions](../../.github/instructions/csharp.instructions.md), and [`.editorconfig`](../../.editorconfig) for the target conventions.
- **Status**: done
- **Inputs**:
  - [README](../../README.md)
  - [repo Copilot instructions](../../.github/copilot-instructions.md)
  - [C# instructions](../../.github/instructions/csharp.instructions.md)
  - [`.editorconfig`](../../.editorconfig)
  - Microsoft Learn guidance for C# identifier naming, namespace declarations, and EditorConfig naming rules

## Description of work

Bring the existing codebase into alignment with the new repository guidance without changing runtime behavior.

Current assessment:

- The solution already follows several desirable conventions:
  - feature-oriented folders are established across API, Application, Infrastructure, and Web;
  - `Program.cs` files are narrowly focused on startup orchestration;
  - file-scoped namespaces are already in broad use; and
  - interface prefixes and async naming appear largely consistent.
- The main delta is type organization. A current solution scan identified **12 non-generated C# files** containing **99 top-level type declarations**. These files currently bundle multiple enums, records, classes, interfaces, or delegates into single files, which conflicts with the updated instruction files.
- The largest concentration of work is in:
  - API request/response DTO files;
  - Application configuration and service contract files;
  - Infrastructure persistence, notification, and DI/bootstrap files; and
  - the Blazor web client API/view-model file.

Non-goals for this plan:

- changing the target framework, SDK, or language version;
- redesigning application architecture beyond what is needed for guideline alignment;
- rewriting feature behavior; and
- introducing new abstractions unless they are required to preserve existing behavior after file splits.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: deliver all plan work on the existing branch in one PR to the default branch
- **Dependencies**: current build/test baseline must remain green throughout; developer discipline around rename/move operations is required to avoid namespace or reference drift
- **Key risks**:
  - Risk: large-scale file splitting can create unnecessary noise and merge conflicts.
    - Mitigation: sequence the work internally by layer and feature folder within the branch, keep behavior unchanged, and use build/test gates after each work item.
  - Risk: DTO and EF model moves can break references or serialization assumptions.
    - Mitigation: preserve names, namespaces, and accessibility; use compiler-guided fixes and rerun tests after each batch.
  - Risk: the new `.editorconfig` may surface latent style inconsistencies that are broader than this initiative.
    - Mitigation: prioritize rules that directly support the new guidance; defer unrelated style cleanup unless it is touched by the refactor.
  - Risk: friend assembly usage may hide coupling between layers.
    - Mitigation: review `InternalsVisibleTo` usage during the validation phase and only reduce exposure where it is low-risk and well-covered by tests.

## Delivery Plan

### Execution gates (required)

Before starting any work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (guidelines) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Baseline inventory and rename map | Produce a precise inventory of every non-generated file that violates one-top-level-type-per-file or matching-file-name guidance, then define the target file map before moving code. | Repo Copilot MUST rules, C# instructions MUST rules, Microsoft Learn type/file organization guidance | None | `dotnet build`; `dotnet test`; inventory reviewed against current file tree | Revert inventory-only changes or discard draft mapping documents | Review and approve the file split scope before implementation starts |
| Work Item 2: Align API and Web contracts | Split API request/response models and Blazor web view models into one top-level type per file, preserving namespaces and wire contracts. | One type per file, matching filenames, PascalCase type names, file-scoped namespaces | Work Item 1 | `dotnet build`; targeted API/Web tests; manual compile check for endpoint mappings and Blazor pages | Revert the PR or restore the original grouped DTO files | Review renamed files for navigation and discoverability |
| Work Item 3: Align Application layer models and services | Split application configuration models, IG auth contracts, and service contracts/extensions into focused files with matching names. | One type per file, matching filenames, feature-based organization, least-exposure naming consistency | Work Items 1-2 | `dotnet build`; application and API unit tests | Revert the PR or restore previous grouped files | Validate that DI registrations and handler references remain unchanged |
| Work Item 4: Align Infrastructure types and persistence models | Split infrastructure DI/bootstrap types, notification providers, EF entities, and supporting platform services into focused files while keeping folder boundaries intact. | One type per file, matching filenames, feature-based organization, file-scoped namespaces | Work Items 1-3 | `dotnet build`; infrastructure and API tests; smoke run if needed | Revert the PR or restore previous grouped files | Review entity and provider file placement for maintainability |
| Work Item 5: Enforce and verify repository-wide compliance | Apply code cleanup where needed, verify the `.editorconfig` behavior on touched files, reassess `InternalsVisibleTo` usage, and close any remaining naming or organization gaps. | `.editorconfig`, C# instructions, repo instructions, Microsoft Learn naming guidance | Work Items 1-4 | `dotnet build`; `dotnet test`; final multi-type file scan; spot-check naming compliance | Revert the final cleanup PR | Confirm no remaining non-generated multi-type files and no unexpected visibility changes |

### Work Item 1 details

- [x] Work Item 1: Baseline inventory and rename map
  - [x] Build and test baseline established
  - [x] Task 1: Capture the current multi-type file inventory
    - [x] Step 1: Re-run the repository scan for non-generated `.cs` files that contain multiple top-level type declarations
    - [x] Step 2: Record each violating file and the contained type names
    - [x] Step 3: Confirm whether any additional files violate matching-file-name expectations even when they contain a single type
  - [x] Task 2: Define the target move strategy
    - [x] Step 1: Group work by project and feature folder so files stay near related code
    - [x] Step 2: Define the target filename for each extracted type
    - [x] Step 3: Identify any partial classes, DI registrations, or serialization contracts that need special handling
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Api/Features/**/*`: inventory API contract files that bundle multiple request/response types
    - `src/TNC.Trading.Platform.Application/**/*`: inventory configuration, IG, and service files that bundle multiple types
    - `src/TNC.Trading.Platform.Infrastructure/**/*`: inventory persistence, notification, and platform bootstrap files that bundle multiple types
    - `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`: inventory client and view-model types bundled in the Blazor web project
  - **Work Item Dependencies**: None
  - **User Instructions**: Review the inventory before refactoring begins to confirm the desired PR boundaries.

### Work Item 2 details

- [x] Work Item 2: Align API and Web contracts
  - [x] Build and test baseline established
  - [x] Task 1: Split API request and response contract files
    - [x] Step 1: Extract nested request/response records from `UpdatePlatformConfigurationRequest.cs` into matching files
    - [x] Step 2: Extract nested response records from `GetPlatformStatusResponse.cs`, `GetPlatformConfigurationResponse.cs`, `GetPlatformEventsResponse.cs`, and `UpdatePlatformConfigurationResponse.cs`
    - [x] Step 3: Verify mapping extensions and endpoint handlers continue to compile without namespace drift
  - [x] Task 2: Split Blazor web client models
    - [x] Step 1: Keep `PlatformApiClient.cs` for the client only
    - [x] Step 2: Move each view model and update model into its own file with a matching name
    - [x] Step 3: Verify all consuming Razor components compile and bind correctly
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationResponse.cs`
    - `src/TNC.Trading.Platform.Api/Features/GetPlatformEvents/GetPlatformEventsResponse.cs`
    - `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusResponse.cs`
    - `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationRequest.cs`
    - `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationResponse.cs`
    - `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
  - **Work Item Dependencies**: Complete after Work Item 1 inventory confirms the target file map.
  - **User Instructions**: Re-review API contract shapes after the split to ensure client/server naming remains intuitive.

### Work Item 3 details

- [x] Work Item 3: Align Application layer models and services
  - [x] Build and test baseline established
  - [x] Task 1: Split application models by responsibility
    - [x] Step 1: Extract enums from `PlatformModels.cs` into individual files with singular/plural naming preserved
    - [x] Step 2: Extract records and classes from `PlatformModels.cs` into focused files that match each type name
    - [x] Step 3: Keep the existing configuration namespace and feature adjacency intact
  - [x] Task 2: Split IG auth contracts and service contracts
    - [x] Step 1: Extract request/response/sanitized response types from `IgAuthenticationContracts.cs`
    - [x] Step 2: Keep the sanitizer in its own file with a matching name
    - [x] Step 3: Split `PlatformServices.cs` so each interface, service, and extension type has its own file
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Application/Configuration/PlatformModels.cs`
    - `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticationContracts.cs`
    - `src/TNC.Trading.Platform.Application/Services/PlatformServices.cs`
  - **Work Item Dependencies**: Complete after API/Web contract moves stabilize compile-time references.
  - **User Instructions**: Pay special attention to file naming and discoverability during review because this layer currently holds the highest concentration of shared models.

### Work Item 4 details

- [x] Work Item 4: Align Infrastructure types and persistence models
  - [x] Build and test baseline established
  - [x] Task 1: Split DI/bootstrap and platform infrastructure types
    - [x] Step 1: Extract `PlatformInfrastructureServiceCollectionExtensions`, `PlatformTimeProviderFactory`, `IncrementingTimeProvider`, `ProtectedCredentialService`, and the store implementations into matching files
    - [x] Step 2: Preserve internal visibility and DI registrations while moving types
    - [x] Step 3: Keep platform-related files in the existing infrastructure feature folders
  - [x] Task 2: Split notification providers and persistence entities
    - [x] Step 1: Extract each notification contract/provider from `NotificationProvider.cs`
    - [x] Step 2: Extract each EF entity from `PlatformDbContext.cs` into matching files while keeping the DbContext focused on EF configuration
    - [x] Step 3: Verify EF model configuration still references the extracted entity types correctly
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructure.cs`
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/NotificationProvider.cs`
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs`
  - **Work Item Dependencies**: Complete after application-layer types are stable.
  - **User Instructions**: Review the final infrastructure file tree to ensure it remains feature-based and not reorganized by type kind alone.

### Work Item 5 details

- [x] Work Item 5: Enforce and verify repository-wide compliance
  - [x] Build and test baseline established
  - [x] Task 1: Apply repository conventions to touched files
    - [x] Step 1: Run code cleanup or formatting for touched files under the new `.editorconfig`
    - [x] Step 2: Verify file-scoped namespaces remain in use for all touched files
    - [x] Step 3: Verify type names, enum naming, interface prefixes, and async suffixes remain compliant
  - [x] Task 2: Close remaining structural gaps
    - [x] Step 1: Re-run the multi-type file scan and confirm zero non-generated violations remain
    - [x] Step 2: Review `InternalsVisibleTo` declarations and reduce any no-longer-needed friend access where practical
    - [x] Step 3: Update top-level documentation if the resulting file layout materially changes contributor navigation
  - [x] Build and test validation

  - **Files**:
    - `.editorconfig`
    - `src/**/*.cs`
    - `test/**/*.cs`
    - `src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs`
    - `src/TNC.Trading.Platform.Infrastructure/Properties/InternalsVisibleTo.cs`
    - `README.md` (only if navigation guidance needs a small refresh)
  - **Work Item Dependencies**: Complete after all refactor batches are merged or ready together.
  - **User Instructions**: Confirm whether friend assembly usage should remain as-is if it is still required for tests and layer boundaries.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`; `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`; `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj`
- **Integration tests**: `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`; `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`; `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`
- **Manual checks**:
  - Confirm Solution Explorer navigation is clearer after each file split
  - Confirm API endpoint mappings and Blazor pages still compile after model extraction
  - Confirm no generated files were edited
- **Security checks**:
  - Review moves for accidental exposure changes from `internal` to broader visibility
  - Confirm no secrets or secret-like values are introduced while splitting credential-related types
  - Confirm any retained `InternalsVisibleTo` usage remains justified

## Acceptance checklist

- [x] All non-generated C# files follow one top-level type per file.
- [x] Top-level C# file names match the contained type names.
- [x] File-scoped namespaces remain in use for touched files where one namespace per file applies.
- [x] Interface prefixes, type PascalCase naming, and enum naming remain compliant with the adopted Microsoft Learn guidance.
- [x] `.editorconfig` conventions are applied to all touched files.
- [x] Feature-based folder organization is preserved while splitting files.
- [x] Build and all relevant test suites pass after each work item.
- [x] Any remaining `InternalsVisibleTo` declarations are intentional and documented.

## Notes

- Initial scan findings to drive implementation planning:
  - `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationResponse.cs`
  - `src/TNC.Trading.Platform.Api/Features/GetPlatformEvents/GetPlatformEventsResponse.cs`
  - `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusResponse.cs`
  - `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationRequest.cs`
  - `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationResponse.cs`
  - `src/TNC.Trading.Platform.Application/Configuration/PlatformModels.cs`
  - `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticationContracts.cs`
  - `src/TNC.Trading.Platform.Application/Services/PlatformServices.cs`
  - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/NotificationProvider.cs`
  - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs`
  - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructure.cs`
  - `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
- The current solution appears substantially aligned already on namespace style and startup composition; this plan therefore focuses on high-value structural cleanup instead of broad stylistic churn.
- Because the workspace contains a Blazor project, the Web work item should preserve component bindings and page discoverability while splitting view-model files.
- Retained `InternalsVisibleTo` declarations after review:
  - `TNC.Trading.Platform.Application` remains visible to `TNC.Trading.Platform.Api` and `TNC.Trading.Platform.Infrastructure` because the API consumes internal application handlers/contracts and Infrastructure implements internal application-side abstractions.
  - `TNC.Trading.Platform.Infrastructure` remains visible to `TNC.Trading.Platform.Api` because the API startup path uses internal infrastructure registration and runtime services.
  - Test-only friend assembly entries were removed because the API test project reaches internal framework types through reflection helpers rather than compile-time friend access.
- Final execution outcome:
  - `dotnet build` passed after the refactor work.
  - Relevant API, Application, Infrastructure, and Web functional tests passed (51/51).
  - Repository scan confirmed no remaining non-generated C# files contain multiple top-level type declarations.
