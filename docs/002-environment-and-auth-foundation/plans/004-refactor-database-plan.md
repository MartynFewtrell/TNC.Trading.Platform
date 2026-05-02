# Refactor database coupling delivery plan

This document plans an architectural refactor to reduce database and EF Core coupling in the current platform implementation.

## Summary

- **Source**: Refactor objective derived from the current implementation review in this conversation.
- **Status**: complete
- **Inputs**:
  - `src/TNC.Trading.Platform.Api/Program.cs`
  - `src/TNC.Trading.Platform.Api/Infrastructure/Persistence/PlatformDbContext.cs`
  - `src/TNC.Trading.Platform.Api/Infrastructure/Platform/PlatformServices.cs`
  - `docs/002-environment-and-auth-foundation/requirements.md`
  - `docs/002-environment-and-auth-foundation/technical-specification.md`

## Description of work

Deliver an internal refactor that separates transport, application, and persistence responsibilities without changing externally visible behavior.

This refactor will:

- keep the API project focused on endpoint mapping, request and response DTOs, and DI composition;
- move persistence implementation details into a dedicated infrastructure project;
- introduce an application layer for feature handlers, orchestration logic, and persistence-facing abstractions;
- remove direct EF Core coupling from feature and orchestration code where application abstractions are more appropriate; and
- preserve the current runtime behavior while improving maintainability, testability, and future extensibility.

This plan does not add new business capabilities. It restructures existing code so later work can evolve environment/auth, Blazor UI, and persistence more safely.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: complete the work as staged work items within the existing `002-environment-and-auth-foundation` branch and merge as one validated PR
- **Dependencies**: current API feature slices, existing persistence model, existing hosted services, current solution structure
- **Key risks**:
  - Risk: moving persistence types across assemblies can break namespaces, project references, and DI registration.
    - Mitigation: introduce the new projects first, then move code incrementally with build and test validation after each step.
  - Risk: refactoring shared services can change current behavior unintentionally.
    - Mitigation: keep public API contracts stable and validate current API behavior after each work item.
  - Risk: abstraction work can drift into a generic repository layer that hides behavior.
    - Mitigation: use small feature-oriented interfaces only, such as configuration, status, auth-state, and event readers or writers.

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
| Work Item 1: Create layer boundaries | Add new application and infrastructure projects, wire project references, and define target ownership of API, application, and persistence responsibilities without changing behavior yet. | Refactor objectives 1-5; keep API thin; isolate persistence; depend on abstractions | None | `dotnet build`; `dotnet test`; verify solution loads and current endpoints still build | Revert added projects and solution or project reference changes | Review the proposed project split before migration continues |
| Work Item 2: Move persistence into infrastructure | Move `PlatformDbContext`, persistence entities, and EF mappings into infrastructure while keeping runtime behavior unchanged through updated registrations. | Move EF and SQL-specific code out of API; keep `Program.cs` as composition root | Work Item 1 | `dotnet build`; `dotnet test`; verify startup and persistence initialization still work | Revert moved files and restore original namespaces and project placement | Confirm EF Core types no longer live in the API project except for startup or composition usage |
| Work Item 3: Introduce application abstractions | Add small application-facing interfaces for configuration, status, auth-state, and event access; refactor handlers and services to depend on those abstractions instead of `PlatformDbContext`. | Use-case logic in application layer; no direct `DbContext` dependency from handlers | Work Item 2 | `dotnet build`; `dotnet test`; verify handlers compile and run without direct EF dependency | Revert interface and service refactors while keeping infrastructure move intact | Keep interfaces explicit and feature-oriented; do not introduce a generic repository layer |
| Work Item 4: Thin API composition | Refactor `Program.cs` and endpoint composition so the API remains transport and composition only, with registrations delegated to layer-specific extensions where useful. | Keep API transport-only; keep endpoints thin; keep `Program.cs` focused on orchestration | Work Item 3 | `dotnet build`; `dotnet test`; verify endpoints still return the same contract shapes | Revert DI and composition changes and restore previous registrations | Confirm API project contains transport and composition concerns, not persistence behavior |
| Work Item 5: Stabilize and validate | Add or update tests around the new layer boundaries, document the architecture split, and confirm there is no behavior regression after the refactor. | Improve testability and maintainability; preserve external behavior | Work Items 1-4 | `dotnet build`; `dotnet test`; targeted manual API verification | Revert final cleanup or test changes if needed while preserving validated layer split | Use this as the final checkpoint before merging the PR |

### Work Item 1 details

- [x] Work Item 1: Create layer boundaries
  - [x] Build and test baseline established
  - [x] Task 1: Add new projects
    - [x] Step 1: Create `src/TNC.Trading.Platform.Application`
    - [x] Step 2: Create `src/TNC.Trading.Platform.Infrastructure`
    - [x] Step 3: Add project references with API depending on application and infrastructure as the composition root
  - [x] Task 2: Define ownership boundaries
    - [x] Step 1: Keep endpoint mapping and DTOs in API
    - [x] Step 2: Reserve handlers, orchestration contracts, and application models for application
    - [x] Step 3: Reserve EF Core and SQL-specific types for infrastructure
  - [x] Build and test validation

### Work Item 2 details

- [x] Work Item 2: Move persistence into infrastructure
  - [x] Build and test baseline established
  - [x] Task 1: Move EF Core ownership
    - [x] Step 1: Move `PlatformDbContext`
    - [x] Step 2: Move persistence entities
    - [x] Step 3: Move EF configuration and mappings
  - [x] Task 2: Preserve runtime behavior
    - [x] Step 1: Update namespaces and references
    - [x] Step 2: Keep DI registrations working from API composition
    - [x] Step 3: Keep hosted services and current feature flows compiling
  - [x] Build and test validation

### Work Item 3 details

- [x] Work Item 3: Introduce application abstractions
  - [x] Build and test baseline established
  - [x] Task 1: Add feature-oriented interfaces
    - [x] Step 1: Define configuration read and write abstractions
    - [x] Step 2: Define status, event, and auth-state abstractions
    - [x] Step 3: Implement infrastructure adapters for those abstractions
  - [x] Task 2: Refactor orchestration code
    - [x] Step 1: Refactor `PlatformConfigurationService`
    - [x] Step 2: Refactor `PlatformStateCoordinator`
    - [x] Step 3: Update handlers to depend on application services only
  - [x] Build and test validation

### Work Item 4 details

- [x] Work Item 4: Thin API composition
  - [x] Build and test baseline established
  - [x] Task 1: Reduce API coupling
    - [x] Step 1: Keep `Program.cs` focused on composition
    - [x] Step 2: Move service registration into layer-specific extensions where appropriate
    - [x] Step 3: Keep endpoints thin and handler-driven
  - [x] Task 2: Validate transport boundary
    - [x] Step 1: Ensure request and response DTOs remain transport-only
    - [x] Step 2: Ensure API does not expose persistence models
  - [x] Build and test validation

### Work Item 5 details

- [x] Work Item 5: Stabilize and validate
  - [x] Build and test baseline established
  - [x] Task 1: Strengthen validation
    - [x] Step 1: Add or update tests for refactored services and handlers
    - [x] Step 2: Run the full build and test suite
    - [x] Step 3: Perform targeted endpoint regression checks
  - [x] Task 2: Document the new architecture
    - [x] Step 1: Summarize project responsibilities
    - [x] Step 2: Record migration notes for future features
  - [x] Build and test validation

## Target structure

- `src/TNC.Trading.Platform.Api`
  - Minimal API endpoint mapping only
  - request and response DTOs
  - DI composition root
- `src/TNC.Trading.Platform.Application`
  - feature handlers
  - application models
  - interfaces such as `IPlatformConfigurationReader`, `IPlatformConfigurationWriter`, `IPlatformStatusReader`, and other feature-oriented contracts
- `src/TNC.Trading.Platform.Infrastructure`
  - `PlatformDbContext`
  - EF entities and mappings
  - implementations of application interfaces
  - SQL Server-specific configuration
- `src/TNC.Trading.Platform.Domain` optional
  - pure domain rules and value objects if trading rules grow enough to justify it

## Cross-cutting validation

- **Build**: `dotnet build`
- **Tests**: `dotnet test`
- **Manual checks**:
  - Verify existing platform endpoints still start and respond
  - Verify current configuration and status flows still work after each work item
  - Verify no persistence model is returned from API responses
  - Verify `Program.cs` remains focused on startup orchestration
- **Security checks**:
  - Verify secret-handling behavior is unchanged
  - Verify no refactor step causes persistence models or protected values to leak across layers

## Acceptance checklist

- [x] API project is transport and composition only
- [x] EF Core persistence implementation lives outside the API project
- [x] Feature handlers and orchestration code depend on application abstractions instead of `PlatformDbContext`
- [x] No generic repository layer was introduced
- [x] Existing behavior remains intact after the refactor
- [x] The solution builds and tests pass after each work item

## Notes

- Application responsibilities now live under `src/TNC.Trading.Platform.Application`, including platform models, feature handlers, scheduling/auth orchestration, and the feature-oriented persistence interfaces used by those handlers and coordinators.
- Infrastructure responsibilities now live under `src/TNC.Trading.Platform.Infrastructure`, including `PlatformDbContext`, EF entities, SQL-backed store implementations, notification providers and dispatch, data redaction, and operational record retention processing.
- API responsibilities now remain under `src/TNC.Trading.Platform.Api` for endpoint mapping, transport DTOs, request validation, and composition-root startup wiring that delegates service registration into the application and infrastructure layer extensions.
- Test coverage was updated to reflect over the new application and infrastructure assemblies so the existing unit and integration checks continue validating the same platform flows after the refactor.
- This plan is intentionally scoped to the database and architecture refactor rather than the broader feature scope in work package 002.
- The recommended rule of thumb remains: if a class needs `Microsoft.EntityFrameworkCore`, it usually should not live in the API project unless it is purely startup or composition code.
- The Blazor-related work in this repository benefits from this split because UI-facing contracts can be shared without exposing database types.
