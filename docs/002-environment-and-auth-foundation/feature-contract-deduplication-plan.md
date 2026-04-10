# Feature contract deduplication delivery plan

This document plans a focused refactor to remove redundant feature-contract files and clarify intentional API-to-application contract boundaries in the current platform implementation.

## Summary

- **Source**: Refactor objective derived from the current implementation review in this conversation.
- **Status**: draft
- **Inputs**:
  - `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
  - `src/TNC.Trading.Platform.Api/Features/*/*Request.cs`
  - `src/TNC.Trading.Platform.Api/Features/*/*Response.cs`
  - `src/TNC.Trading.Platform.Application/Features/*/*Request.cs`
  - `src/TNC.Trading.Platform.Application/Features/*/*Response.cs`
  - `docs/002-environment-and-auth-foundation/requirements.md`
  - `docs/002-environment-and-auth-foundation/technical-specification.md`

## Description of work

Deliver an internal refactor that reduces confusion caused by duplicate feature-contract filenames across the API and Application layers while preserving the current vertical-slice architecture and externally visible API behavior.

The current solution intentionally keeps transport DTOs in the API project and handler contracts in the Application project, but the implementation also contains redundant request shells that are not used by the endpoint layer. The most visible examples are the API-side `GetPlatformStatusRequest`, `GetPlatformConfigurationRequest`, `GetPlatformEventsRequest`, and `TriggerManualAuthRetryRequest` records, which duplicate Application request names without being used as the actual endpoint-bound input types. This makes it harder to tell which contracts are active, which ones are transport-only, and which methods each feature file serves.

This refactor will:

- keep the API project focused on endpoint binding, response shaping, validation, and composition;
- keep the Application project focused on handler request and response contracts;
- remove API request files that do not participate in endpoint binding or mapping;
- preserve API response DTOs where they are the external HTTP contract and differ from Application responses;
- make request and response ownership per feature explicit enough that future contributors can quickly identify which files are active and why they exist.

This plan does not introduce new business behavior, collapse the API and Application layers into one set of contracts, or change the existing endpoint routes.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: complete the refactor within the existing `002-environment-and-auth-foundation` branch and merge as one validated PR
- **Dependencies**: current API feature slices; current Application handlers; current endpoint mappings in `PlatformEndpoints.cs`; existing unit and integration coverage
- **Key risks**:
  - Risk: removing similarly named request files could accidentally break endpoint binding or tests if a file is referenced indirectly.
    - Mitigation: build a feature-by-feature usage matrix first, then remove only files proven to be unused.
  - Risk: over-deduplication could blur the transport/application boundary and leak internal models into API contracts.
    - Mitigation: keep API response DTOs and any truly transport-specific request DTOs where their shapes intentionally differ from Application contracts.
  - Risk: partial cleanup could leave naming and ownership rules inconsistent for later features.
    - Mitigation: define a contract-ownership rule and apply it consistently across the affected feature slices in the same PR.

## Delivery Plan

### Execution gates

Before starting any work item, and again before marking a work item as complete, run the build and test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build and tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Inventory feature contract ownership | Catalogue all request and response files under the API and Application feature folders, map each one to its consuming endpoint or handler, and classify each contract as transport-only, application-only, or redundant | Refactor objective: clarify active contracts and preserve layer ownership | None | `dotnet build`; `dotnet test`; review endpoint-to-contract mapping for all five current platform features | Restore the original inventory notes and stop before any code changes if ownership is still unclear | Review the inventory before removal work starts so the keep/remove decisions stay explicit |
| Work Item 2: Remove redundant request shells | Delete API request files that are not used for endpoint binding and keep only the Application request contracts for those operations | Refactor objective: remove redundant files without changing behavior | Work Item 1 | `dotnet build`; `dotnet test`; confirm endpoints still compile and bind correctly | Restore removed files and namespaces if any endpoint or test proves to depend on them | Expect the API feature folders to retain only request DTOs that are actually bound from HTTP input |
| Work Item 3: Clarify transport mapping boundaries | Keep API response DTOs that define the external HTTP contract, keep Application responses as handler outputs, and move any inline endpoint mapping into feature-local helpers where that improves discoverability without broad architecture churn | Refactor objective: preserve valid layer separation while making active mappings easier to follow | Work Item 2 | `dotnet build`; `dotnet test`; targeted endpoint checks for status, configuration, events, and manual retry flows | Revert helper extraction or mapping cleanup while preserving the redundant-file removal if needed | Expect response ownership to remain layered, not collapsed |
| Work Item 4: Document and validate the deduplicated shape | Update any affected tests or work-package notes so the remaining contract set and ownership rules are discoverable for future contributors | Refactor objective: prevent the same confusion from returning | Work Items 1-3 | `dotnet build`; `dotnet test`; verify docs or implementation notes match the final contract layout | Revert documentation or test-only updates if needed while keeping validated code cleanup | Use this as the final checkpoint before merge |

### Work Item 1 details

- [x] Work Item 1: Inventory feature contract ownership
  - [x] Build and test baseline established
  - [x] Task 1: Map current feature slices
    - [x] Step 1: Inventory `*Request.cs` and `*Response.cs` files under `src/TNC.Trading.Platform.Api/Features`
    - [x] Step 2: Inventory `*Request.cs` and `*Response.cs` files under `src/TNC.Trading.Platform.Application/Features`
    - [x] Step 3: Map each contract to the endpoint or handler that consumes it
    - [x] Step 4: Classify each contract as transport-only, application-only, or redundant
  - [x] Build and test validation

### Work Item 2 details

- [x] Work Item 2: Remove redundant request shells
  - [x] Build and test baseline established
  - [x] Task 1: Remove proven-unused API request records
    - [x] Step 1: Remove `GetPlatformStatusRequest.cs` from `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/` if the inventory confirms direct Application-request dispatch remains the only path
    - [x] Step 2: Remove `GetPlatformConfigurationRequest.cs` from `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/` if the inventory confirms direct Application-request dispatch remains the only path
    - [x] Step 3: Remove `GetPlatformEventsRequest.cs` from `src/TNC.Trading.Platform.Api/Features/GetPlatformEvents/` if the endpoint continues to bind query values directly rather than an API request DTO
    - [x] Step 4: Remove `TriggerManualAuthRetryRequest.cs` from `src/TNC.Trading.Platform.Api/Features/TriggerManualAuthRetry/` if the endpoint continues to use a body-less command flow
  - [x] Task 2: Preserve intentional transport inputs
    - [x] Step 1: Keep `UpdatePlatformConfigurationRequest.cs` in the API project because it is the actual HTTP input contract
    - [x] Step 2: Keep `UpdatePlatformConfigurationRequest.cs` in the Application project because it is the handler contract mapped from the transport request
    - [x] Step 3: Update namespaces, usings, and tests to reflect the reduced request-file set
  - [x] Build and test validation

### Work Item 3 details

- [x] Work Item 3: Clarify transport mapping boundaries
  - [x] Build and test baseline established
  - [x] Task 1: Make response ownership explicit
    - [x] Step 1: Confirm API response DTOs remain transport-only and are not reused as Application models
    - [x] Step 2: Confirm Application response contracts remain handler outputs and are not exposed directly from endpoints
    - [x] Step 3: Keep one request type and one response type per operation per layer only where both layers have distinct responsibilities
  - [x] Task 2: Improve mapping discoverability
    - [x] Step 1: Extract feature-local mapping helpers where endpoint response construction is currently embedded inline and obscures contract ownership
    - [x] Step 2: Keep `PlatformEndpoints.cs` thin by delegating repetitive request-to-handler or result-to-response mapping where that reduces ambiguity
    - [x] Step 3: Ensure any helper placement stays inside the existing feature folders rather than adding shared generic mapping infrastructure
  - [x] Build and test validation

### Work Item 4 details

- [x] Work Item 4: Document and validate the deduplicated shape
  - [x] Build and test baseline established
  - [x] Task 1: Stabilize validation
    - [x] Step 1: Update or add tests that assert endpoint binding still works after redundant request-file removal
    - [x] Step 2: Run the full build and test suite
    - [x] Step 3: Perform targeted regression checks for `GET /api/platform/status`, `GET /api/platform/configuration`, `PUT /api/platform/configuration`, `POST /api/platform/auth/manual-retry`, and `GET /api/platform/events`
  - [x] Task 2: Record ownership rules
    - [x] Step 1: Add or update implementation notes that explain when a feature needs both API and Application contracts versus one active contract in only one layer
    - [x] Step 2: Record the final keep/remove decisions for the current platform feature slices
  - [x] Build and test validation

## Target structure

- `src/TNC.Trading.Platform.Api`
  - endpoint mapping
  - request DTOs only when bound from HTTP input
  - response DTOs that define external HTTP contracts
  - validators and composition concerns
- `src/TNC.Trading.Platform.Application`
  - handler request contracts
  - handler response contracts
  - feature handlers and application orchestration
- `src/TNC.Trading.Platform.Infrastructure`
  - persistence and integration implementation only

## Cross-cutting validation

- **Build**: `dotnet build`
- **Tests**: `dotnet test`
- **Manual checks**:
  - Verify each platform endpoint still resolves the intended handler after redundant request-file removal
  - Verify no endpoint starts returning Application response models directly
  - Verify feature-folder contents make it obvious which request or response files are active
  - Verify `PlatformEndpoints.cs` remains focused on thin endpoint orchestration
- **Security checks**:
  - Verify the refactor does not bypass existing validation or error handling on HTTP inputs
  - Verify no internal models, persistence types, or protected values leak across the API boundary during response-shaping cleanup

## Acceptance checklist

- [x] Redundant API request files are removed only where they are proven unused.
- [x] Intentional API transport contracts remain in place for externally visible request and response shapes.
- [x] Application request and response contracts remain handler-facing and distinct from API transport models.
- [x] The contract ownership rule is documented clearly enough for future feature work.
- [x] The solution builds and tests pass after the cleanup.

## Notes

- The current review identified duplicate request and response filenames for `GetPlatformStatus`, `GetPlatformConfiguration`, `GetPlatformEvents`, `TriggerManualAuthRetry`, and `UpdatePlatformConfiguration` across the API and Application layers.
- Not every duplicate filename is a defect. API response DTOs often remain valid because they define HTTP-specific shapes, while Application responses wrap application models.
- The strongest current redundancy signal is unused API request shell records for non-body or directly bound endpoints.
- This plan is intentionally scoped to feature-contract clarity and redundancy removal rather than to broader endpoint reorganization or architecture changes.
- Execution outcome:
  - Removed API request shell records for `GetPlatformStatus`, `GetPlatformConfiguration`, `GetPlatformEvents`, and `TriggerManualAuthRetry`.
  - Kept `UpdatePlatformConfigurationRequest` in both layers because the API transport DTO and Application handler contract have distinct responsibilities.
  - Added feature-local mapping helpers under each affected API feature folder so `PlatformEndpoints.cs` now delegates request and response translation instead of building contracts inline.
  - Validated the remaining contract set with targeted API unit and integration tests plus a full repo-root `dotnet build` and `dotnet test` run.
