# Work Package Refactoring Mitigation Plan

> Use this plan to mitigate the issues identified in the authentication and authorisation refactoring review. The work is scoped to the confirmed runtime-mode, test-composition, duplication, and documentation problems identified in `../001-work-package-refactoring-review-report.md`.

## Summary

- **Source review**: `../001-work-package-refactoring-review-report.md`
- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Status**: `completed`
- **Inputs**:
  - `../001-work-package-refactoring-review-report.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - existing numbered plan files in this folder (for example `001-delivery-plan.md`)

## Description of work

This mitigation plan removes drift between the delivered authentication architecture and the remaining lightweight local runtime path, isolates test-only authentication and persistence behavior from product startup, consolidates duplicated Web and API authentication registration logic, and updates documentation so the supported local development model is unambiguous.

The work is explicitly behavior-preserving for the delivered authentication feature set: protected routes and APIs must continue to enforce the same role matrix, sign-in and sign-out semantics must remain unchanged, the dedicated access-denied behavior must remain unchanged, and Docker plus Keycloak must become the clearly supported local development baseline.

## Mitigation approach

- **Delivery model**: `phased refactoring`
- **Branching**: keep the work on `003-authentication-and-authorisation` and deliver the mitigation work items in sequence so each slice remains buildable, testable, and reviewable.
- **Dependencies**:
  - `src/TNC.Trading.Platform.AppHost/`
  - `src/TNC.Trading.Platform.Web/`
  - `src/TNC.Trading.Platform.Api/`
  - `src/TNC.Trading.Platform.Infrastructure/`
  - `test/TNC.Trading.Platform.Web/`
  - `test/TNC.Trading.Platform.Api/`
  - `docs/wiki/architecture.md`
  - `docs/wiki/local-development.md`
- **Behavior-preservation boundaries**:
  - Protected Web routes must continue redirecting anonymous operators to sign-in and signed-in underprivileged users to access-denied.
  - Protected API endpoints must continue returning `401` or `403` without redirects.
  - The Administrator, Operator, Viewer, and no-role behaviors must remain unchanged.
  - The Keycloak-backed local sign-in flow must remain available and valid for the seeded local accounts.
  - Any retained synthetic auth or persistence support must become explicit test harness behavior only, not a normal local application runtime mode.
- **Key risks**:
  - Removing lightweight runtime branches could break tests that implicitly depend on `Authentication:Provider=Test` or the in-memory database fallback.
    - **Mitigation**: first identify and move those dependencies into explicit test composition before removing product runtime branches.
  - Consolidating shared auth registration could accidentally alter policy behavior between Web and API hosts.
    - **Mitigation**: preserve lower-level unit coverage around policies and provider resolution and rerun role-matrix tests after extraction.
  - Documentation updates could lag implementation and leave mixed instructions in the wiki.
    - **Mitigation**: include wiki updates and link verification in the same work items that change runtime or testing guidance.

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | Local auth runtime topology | Boundary leakage / Coupling | `src/TNC.Trading.Platform.AppHost/AppHost.cs` | Remove the normal-runtime `Test` provider branch and make Docker plus Keycloak the supported local runtime baseline. |
| `F2` | Persistence startup behavior | Complexity / Hidden behavior | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs` | Remove or isolate the Development-time in-memory persistence fallback so the real app runtime requires SQL-backed persistence. |
| `F3` | Test-only auth in product Web host | Boundary leakage / Weak cohesion | `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs` | Move synthetic sign-in behavior into explicit test composition and keep product endpoints focused on delivered auth flows. |
| `F4` | Shared policy registration | Duplication | `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`, `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs` | Extract shared authorization policy registration used by both hosts. |
| `F5` | Shared provider resolution | Duplication / Coupling | `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`, `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs` | Extract shared provider validation and authority or audience resolution helpers into a common authentication registration layer. |
| `F6` | AppHost composition readability | Complexity | `src/TNC.Trading.Platform.AppHost/AppHost.cs` | Split AppHost composition into focused methods after runtime cleanup removes obsolete branches. |
| `F7` | Wiki and local guidance drift | Boundary leakage / Naming | `docs/wiki/local-development.md`, `docs/wiki/architecture.md` | Update wiki guidance to state Docker plus Keycloak is required and remove lightweight-mode and in-memory SQL guidance. |
| `F8` | Test safety net drift | Testability / Boundary leakage | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`, `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformAuthenticationE2ETests.cs` | Rebalance tests so the synthetic provider is explicit test infrastructure and the real Docker plus Keycloak path remains the local runtime validation baseline. |
| `F9` | Lower-level tests centered on synthetic provider | Testability | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs` | Retain lower-level coverage, but narrow it to explicit test-harness behavior and shared registration rules rather than implicit product runtime paths. |

## Mitigation Plan

### Execution gates (required)

Before starting *any* mitigation work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (review findings) | Traceability (requirements) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Remove lightweight runtime drift and align local-dev guidance | Remove the normal local runtime `Test` auth branch, remove or isolate the Development-time in-memory database fallback, and update wiki guidance so Docker plus Keycloak is the clear local development baseline. | `F1, F2, F7` | `IR1, NF3, OR1, OR2, TR3` | Baseline item; should happen before shared-auth extraction so later work targets the intended runtime topology. | `dotnet build`; `dotnet test`; manual Docker plus Keycloak local validation walkthrough; wiki link verification. | Revert AppHost runtime composition, infrastructure startup behavior, and wiki updates together if the real local runtime is not stable. | Validate Docker is available locally before running the manual AppHost checks. |
| Work Item 2: Isolate synthetic auth and persistence support into explicit test composition | Move test-only sign-in, provider, and persistence behavior out of product runtime paths and make the safety net explicit about when synthetic infrastructure is used. | `F3, F8, F9` | `TR1, TR2, TR3, NF2, NF4` | Depends on Work Item 1 so test composition targets the cleaned-up runtime baseline. | `dotnet build`; `dotnet test`; targeted Web and API auth test runs; manual verification that Docker plus Keycloak local runtime still behaves the same. | Revert synthetic test harness changes and restore previous test-only composition only if automated coverage becomes unstable. | Keep lower-level tests fast, but do not reintroduce product-runtime branching to achieve that speed. |
| Work Item 3: Extract shared authentication registration and simplify AppHost composition | Consolidate duplicated Web and API auth registration logic, then split AppHost composition into focused methods that reflect the final supported topology. | `F4, F5, F6` | `FR7, FR9, NF3, OR1` | Depends on Work Items 1-2 so extracted code reflects the intended runtime and test boundaries. | `dotnet build`; `dotnet test`; targeted auth unit and integration tests; manual verification of sign-in, access-denied, and protected API outcomes. | Revert shared registration extraction and AppHost composition refactors together if policy or provider behavior changes unexpectedly. | Review diffs carefully for policy parity between Web and API after extraction. |

### Work Item 1 details

- [x] Work Item 1: Remove lightweight runtime drift and align local-dev guidance
  - [x] Build and test baseline established
  - [x] Task 1: Prepare the safety net
    - [x] Step 1: Confirm which current tests and runtime checks still depend on `AppHost__EnableInfrastructureContainers=false`, the `Test` auth provider, or the Development-time in-memory database fallback.
    - [x] Step 2: Identify the minimum test coverage that must remain green while the real local runtime baseline is made Docker plus Keycloak only.
    - [x] Step 3: Record any assumptions about retaining synthetic support strictly for automated tests.
    - [x] Step 4: Stabilize the Docker-backed E2E baseline if the required cross-cutting validation cannot complete reliably before runtime cleanup begins.
  - [x] Task 2: Apply the refactor
    - [x] Step 1: Remove the normal AppHost branch that configures the Web and API to run with `Authentication__Provider=Test` when infrastructure containers are disabled.
    - [x] Step 2: Remove or isolate the Development-time `UseInMemoryDatabase` fallback from `AddPlatformInfrastructure` so the real app runtime no longer silently swaps persistence mode.
    - [x] Step 3: Ensure the supported local runtime path clearly requires Docker-backed Keycloak and SQL-backed persistence.
  - [x] Task 3: Align supporting assets
    - [x] Step 1: Update affected tests or test comments where runtime assumptions change.
    - [x] Step 2: Update `docs/wiki/local-development.md` and `docs/wiki/architecture.md` to remove the lightweight local mode narrative and state that Docker is required because Keycloak is part of the local auth stack.
    - [x] Step 3: Verify affected wiki links still resolve after the documentation updates.
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: remove normal-runtime test-provider composition and keep supported runtime wiring focused on Docker plus Keycloak.
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`: remove or isolate the Development-time in-memory database fallback.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessHandle.cs`: stabilize dashboard-driven Web endpoint discovery if baseline E2E validation is timing out before runtime cleanup starts.
    - `docs/wiki/local-development.md`: update local prerequisites, run instructions, and validation guidance.
    - `docs/wiki/architecture.md`: update runtime topology to reflect the supported local runtime.
  - **Work Item Dependencies**: Must complete before test-composition isolation and shared-auth extraction so later refactors target the supported runtime model.
  - **User Instructions**: After implementation, start the AppHost with Docker available and validate the seeded Keycloak accounts through the documented local walkthrough.

### Work Item 2 details

- [x] Work Item 2: Isolate synthetic auth and persistence support into explicit test composition
  - [x] Build and test baseline established
  - [x] Task 1: Prepare the safety net
    - [x] Step 1: Inventory the Web and API tests that currently assume the synthetic `Test` provider or non-container AppHost runtime.
    - [x] Step 2: Decide which tests should move to explicit test-host setup, direct service registration, or narrower lower-level composition.
    - [x] Step 3: Preserve or add focused regression tests for protected route behavior, protected API status codes, and Keycloak-backed smoke coverage before removing implicit product-runtime dependencies.
  - [x] Task 2: Apply the refactor
    - [x] Step 1: Move synthetic sign-in behavior out of `PlatformAuthenticationEndpointRouteBuilderExtensions` into explicit test harness composition where practical.
    - [x] Step 2: Ensure synthetic JWT or auth-provider support remains available only through test-specific composition rather than normal product startup.
    - [x] Step 3: Adjust functional, integration, and E2E tests so synthetic behavior is intentional and the Docker plus Keycloak path remains the real local runtime validation path.
  - [x] Task 3: Align supporting assets
    - [x] Step 1: Update any new or changed test comments so requirement traceability and rationale remain explicit.
    - [x] Step 2: Update `docs/wiki/` testing or local-development guidance if the test harness setup or validation expectations change.
    - [x] Step 3: Verify affected wiki links still resolve after any documentation updates.
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs`: remove mixed product and test harness responsibilities.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/*.cs`: update functional coverage to use explicit test composition where needed.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/*.cs`: keep the Docker plus Keycloak path as the real end-to-end runtime validation.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/*.cs`: update API auth tests to reflect isolated synthetic composition.
    - `docs/wiki/local-development.md`: update any test-harness notes that could be confused with product runtime guidance.
  - **Work Item Dependencies**: Depends on Work Item 1 runtime cleanup.
  - **User Instructions**: Prioritize preserving deterministic automated tests, but avoid restoring implicit product-runtime branches to do so.

### Work Item 3 details
- [x] Work Item 3: Extract shared authentication registration and simplify AppHost composition
  - [x] Build and test baseline established
  - [x] Task 1: Prepare the safety net
    - [x] Step 1: Confirm the current Web and API policy matrices and provider-resolution outcomes through lower-level tests.
    - [x] Step 2: Add or strengthen focused regression coverage around shared role-policy registration and provider validation before consolidating the logic.
    - [x] Step 3: Record any AppHost composition assumptions that must remain stable after extraction.
  - [x] Task 2: Apply the refactor
    - [x] Step 1: Extract shared role-policy registration into a common authentication registration module used by both Web and API hosts.
    - [x] Step 2: Extract shared provider validation and authority or audience resolution helpers so Web and API do not duplicate that logic.
    - [x] Step 3: Split `AppHost.cs` into focused methods for infrastructure resources, project registration, and authentication environment wiring.
  - [x] Task 3: Align supporting assets
    - [x] Step 1: Update affected auth unit or integration tests and their comments so they validate the shared registration layer clearly.
    - [x] Step 2: Update `docs/wiki/architecture.md` if the final implementation structure or composition guidance becomes clearer after extraction.
    - [x] Step 3: Verify affected wiki links still resolve after documentation updates.
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`: remove duplicated policy and provider logic in favor of shared helpers.
    - `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`: remove duplicated policy and provider logic in favor of shared helpers.
    - `src/TNC.Trading.Platform.Application/Authentication/` or a nearby shared auth module: host shared registration and provider-resolution helpers.
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: split composition responsibilities into focused methods.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/*.cs`: update Web auth unit coverage.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/*.cs`: update API auth unit coverage.
  - **Work Item Dependencies**: Depends on Work Items 1-2 so the extracted code matches the intended runtime and test boundaries.
  - **User Instructions**: Confirm that policy names, role matrices, and provider validation messages stay unchanged after the extraction.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
- **Integration tests**:
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
- **Functional tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`
- **E2E tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`
- **Manual checks**:
  - Start the AppHost with Docker available and verify Keycloak, the Web UI, and the API start successfully.
  - Verify anonymous access to `/`, `/health/live`, and `/health/ready`.
  - Verify `local-viewer`, `local-operator`, `local-admin`, and `local-norole` behaviors still match the delivered role matrix.
  - Verify sign-in, sign-out, access-denied, and session-recovery behavior for protected routes.
  - Verify affected wiki links resolve after documentation changes.
- **Behavior-preservation checks**:
  - Protected Web routes still redirect anonymous users to `/authentication/sign-in`.
  - Signed-in underprivileged users still reach `/authentication/access-denied` for protected UI surfaces.
  - Protected API endpoints still return `401` or `403` without redirects.
  - Delegated scope handling for viewer, operator, and admin areas still behaves as before.
- **Security checks**:
  - Confirm no authority, client ID, client secret, tenant value, or secret is newly hard-coded in product code.
  - Confirm logs, audit events, and UI output still exclude tokens, client secrets, and raw sensitive protocol values.
  - Confirm shared policy extraction does not change the Administrator, Operator, and Viewer access matrix.

## Acceptance checklist

- [x] Every planned mitigation maps back to one or more findings in `001-work-package-refactoring-review-report.md`.
- [x] High-priority maintainability and change-safety issues are addressed before lower-priority cleanup.
- [x] The plan prefers the smallest safe refactoring that resolves each confirmed issue.
- [x] Required safety-net tests or validation steps are defined for each work item.
- [x] Relevant `docs/wiki/` pages are updated to reflect delivered implementation, architecture, or testing changes.
- [x] Affected wiki links resolve after documentation updates.
- [x] Rollback/backout plan documented for each work item.

## Notes

- This mitigation plan intentionally starts with runtime-topology cleanup because the review identified the remaining lightweight local mode as the highest-risk source of implementation drift.
- The plan assumes Docker plus Keycloak is now the required local development baseline and that the local in-memory SQL option is no longer a supported application runtime.
- Synthetic authentication or persistence support may remain for automated tests only if that support is clearly isolated from normal product startup and documentation.
