# Work Package Test Mitigation Plan

> This plan turns the findings in the work-package test review into sequenced mitigation work so the repository gains stronger lower-level coverage, reduced distributed-test cost, safer refactoring seams, and clearer traceability.

## Summary

- **Source review**: `../001-project-test-review-report.md`
- **Work package**: `./docs/005-refactor-app-host/`
- **Status**: `completed`
- **Inputs**:
  - `../001-project-test-review-report.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - existing numbered plan files in this folder (for example `001-delivery-plan.md`)

## Description of work

This mitigation plan addresses the project-wide test issues identified in the work-package review by prioritizing lower-level automated coverage before adding or retaining expensive distributed validation. The plan focuses on four outcomes: rebalance the Web test pyramid (`F1`), reduce reflection-driven test fragility (`F2`), close missing API and Blazor UI coverage gaps (`F3`), and restore repository-wide test traceability in code comments and wiki guidance (`F4`).

The work stays scoped to the review findings and the existing repository architecture. It may require small supporting implementation changes where the current code shape prevents strong tests, but it should prefer behavior-preserving seams, explicit fixtures, and documentation updates over broad production refactoring.

## Mitigation approach

- **Delivery model**: `phased hardening`
- **Branching**: continue on `005-refactor-app-host` and deliver the work items in sequence so each slice can be validated independently.
- **Dependencies**:
  - `src/TNC.Trading.Platform.Api/`
  - `src/TNC.Trading.Platform.Web/`
  - `test/TNC.Trading.Platform.Api/`
  - `test/TNC.Trading.Platform.Web/`
  - `test/TNC.Trading.Platform.Application/`
  - `test/TNC.Trading.Platform.Infrastructure/`
  - `docs/wiki/testing-and-quality.md`
  - `docs/wiki/local-development.md`
- **Key risks**:
  - Lower-level test additions may expose implementation seams that are currently hard to reach without reflection or real-runtime infrastructure.
    - **Mitigation**: allow small behavior-preserving supporting changes such as internal seams, extracted helpers, or shared fixtures where they directly enable cheaper automated coverage.
  - Rebalancing distributed tests could accidentally remove meaningful real-runtime confidence.
    - **Mitigation**: keep a narrow smoke set for real AppHost-plus-Keycloak validation and only trim scenarios after equivalent lower-level coverage exists.
  - Traceability updates could drift from delivered behavior if code comments and wiki pages are not updated alongside test changes.
    - **Mitigation**: include explicit test-comment and `docs/wiki/` tasks in each relevant work item and validate links before completion.

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | Web test pyramid cost and duplication | Weak / Expensive | `../001-project-test-review-report.md` identifies repeated auth validation across functional and E2E flows plus serialized real-runtime setup in Web auth suites. | Add lower-level Web and API-client tests first, then consolidate real-runtime functional/E2E flows to a narrow shared-fixture smoke set. |
| `F2` | Reflection-heavy unit tests | Weak / Fragile | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ApplicationReflection.cs`; `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/InfrastructureReflection.cs` | Replace reflection-driven access with typed seams or focused internal/public testable contracts where practical, and remove string-based invocation from targeted suites. |
| `F3` | Missing API edge coverage and Blazor UI behavior coverage | Missing / Partial | `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`; `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`; `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`; `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor`; `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`; `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor` | Add targeted API integration tests, Web client unit tests, and Blazor component tests for the identified negative-contract and rendered-behavior gaps. |
| `F4` | Test traceability and wiki accuracy | Partial / Stale | `../001-project-test-review-report.md`; `../wiki/testing-and-quality.md` | Update test comments, requirement/risk references, and wiki guidance so coverage ownership and traceability are explicit and current. |

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
| Work Item 1: Strengthen lower-level API and Web boundary coverage | Add the missing lower-level tests around `PlatformApiClient`, API negative contracts, and refreshed Blazor UI behavior so operator-facing risk is covered without extra distributed-runtime cost. | `F1, F3` | `FR3, FR4, NF2, NF5, SR1, SR3, TR1, TR2, TR3, OR1` | Baseline item; should land before trimming high-level auth duplication. | `dotnet build`; targeted Web unit tests; targeted API integration tests; focused component test run if added; repo-root regression pass. | Revert the new tests and any small enabling seams together if they prove unstable or alter delivered behavior. | Review the added tests for requirement comments, operator-facing assertions, and preference for lower-level validation over new browser flows. |
| Work Item 2: Replace fragile reflection-driven unit-test access | Remove the most fragile reflection-based test access and migrate the affected coverage onto typed seams or directly testable contracts. | `F2` | `FR2, NF1, NF5, TR1, TR2, OR1` | Depends on Work Item 1 only where shared helpers or patterns can be reused; otherwise can proceed independently once baseline is green. | `dotnet build`; targeted application and infrastructure unit tests; repo-root regression pass. | Revert the seam changes and corresponding test updates if they widen production API surface unnecessarily or break existing coverage intent. | Confirm the resulting tests break at compile time for renamed members instead of at runtime through string lookup. |
| Work Item 3: Rebalance expensive distributed auth coverage | Consolidate duplicated functional and E2E auth scenarios onto shared fixtures and retain only the smallest real-runtime smoke set that still proves the delivered AppHost-plus-Keycloak path. | `F1` | `FR3, FR4, NF2, NF5, SR1, SR3, IR2, TR2, TR3, OR2` | Depends on Work Item 1 so equivalent lower-level coverage exists before any high-level trimming occurs. | `dotnet build`; targeted Web functional tests; targeted Web E2E tests; repo-root regression pass; manual AppHost auth smoke if helper behavior changes. | Restore the removed scenarios or fixture changes if the reduced smoke set fails to cover a required protected-route or sign-in/sign-out contract. | Verify that at least one sign-in smoke, one sign-out smoke, and one role-boundary smoke remain against the real runtime. |
| Work Item 4: Restore repository-wide traceability and testing guidance | Update code comments and wiki documentation so test purpose, requirement mapping, and current suite responsibilities are explicit and current. | `F3, F4` | `FR4, NF3, OR1, OR2, TR3` | Depends on the earlier work items so documentation matches the delivered suite structure and coverage boundaries. | `dotnet build`; targeted test runs for touched projects; markdown link review; repo-root regression pass. | Revert the documentation updates with the associated test changes if the delivered implementation differs from the documented guidance. | Re-read the updated wiki guidance and confirm it matches the final suite boundaries and validation workflow. |

### Work Item 1 details

- [x] Work Item 1: Strengthen lower-level API and Web boundary coverage
  - [x] Build and test baseline established
  - [x] Task 1: Add direct `PlatformApiClient` coverage
    - [x] Step 1: Add focused unit tests for success parsing across status, configuration, events, manual retry, and auth administration requests.
    - [x] Step 2: Add explicit failure-path tests for `401 Unauthorized`, `403 Forbidden`, `409 Conflict`, validation-error payloads, and empty JSON payload handling.
    - [x] Step 3: Update test comments so each new test explains the requirement or operator-facing risk it protects.
  - [x] Task 2: Expand API negative-contract integration coverage
    - [x] Step 1: Add HTTP-level tests for unsupported auth audit event types returning validation problems.
    - [x] Step 2: Add tests for `ResolveUserName` fallback behavior through the `/api/platform/auth/audit` boundary.
    - [x] Step 3: Add invalid `/api/platform/configuration` request coverage that asserts validation-problem payload shape instead of status code only.
    - [x] Step 4: Add unsupported or malformed auth-audit request coverage when it changes returned contract details.
  - [x] Task 3: Add lower-level Blazor component coverage for refreshed UI surfaces
    - [x] Step 1: Add a component-test harness to `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` if one is not already present.
    - [x] Step 2: Add component tests for `MainLayout.razor` covering sidebar collapse behavior, environment indicator rendering, and signed-in versus signed-out shell transitions.
    - [x] Step 3: Add component tests for `Status.razor` covering degraded warnings, manual retry visibility, manual retry enabled/disabled states, and rendered status messaging.
    - [x] Step 4: Add component tests for `Configuration.razor` covering accordion defaults, preserved in-progress edits, and operator-facing validation or save-state behavior.
    - [x] Step 5: Add component tests for `Home.razor` when the refreshed landing surface has stateful or conditional behavior that is not already covered elsewhere.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`: may need small enabling seams only if current shape blocks direct failure-path testing.
    - `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`: target of new HTTP-level negative-contract coverage.
    - `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`: target of new layout component tests.
    - `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor`: target of new landing-page component tests if needed.
    - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`: target of new status rendering tests.
    - `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor`: target of new configuration rendering tests.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/*`: add or update unit/component test files and any required test harness setup.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: add or update integration tests for API negative contracts.
    - `docs/wiki/testing-and-quality.md`: update coverage responsibilities if the lower-level test mix changes materially.
  - **Work Item Dependencies**: Complete before reducing expensive functional or E2E auth duplication so lower-level coverage is already in place.
  - **User Instructions**: If a new component-test library is introduced, confirm it is the minimum needed dependency and keep the tests focused on rendered behavior rather than implementation details.

### Work Item 2 details

  - [x] Work Item 2: Replace fragile reflection-driven unit-test access
  - [x] Build and test baseline established
  - [x] Task 1: Inventory the reflection-heavy coverage that still matters
    - [x] Step 1: Identify which tests in the application and infrastructure unit projects still depend on `ApplicationReflection` and `InfrastructureReflection`.
    - [x] Step 2: Group those usages by intent: constructor access, non-public method invocation, enum parsing, property inspection, or in-memory DbContext setup.
    - [x] Step 3: Confirm which cases can move to existing public APIs immediately and which require a small enabling seam.
  - [x] Task 2: Replace string-based invocation with typed seams
    - [x] Step 1: Prefer direct tests through existing public contracts where coverage already exists but is routed through reflection helpers.
    - [x] Step 2: Where non-public logic is the true unit under test, introduce the smallest safe seam such as an `internal` type, extracted helper, or public contract already aligned with the design.
    - [x] Step 3: Update the affected tests to use compile-time references instead of `GetType`, `Invoke`, `InvokeAsync`, `GetProperty`, or `SetProperty` string access.
    - [x] Step 4: Remove or shrink the generic reflection helpers once targeted suites no longer require them.
  - [x] Task 3: Preserve traceability and test clarity
    - [x] Step 1: Update affected test comments so they document the requirement or risk the typed test now protects.
    - [x] Step 2: Confirm renamed or extracted test files still follow repository naming and one-top-level-type-per-file conventions.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ApplicationReflection.cs`: remove or narrow generic reflection helpers.
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/InfrastructureReflection.cs`: remove or narrow generic reflection helpers.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/*`: update the specific suites that currently invoke non-public application behavior by string.
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/*`: update the specific suites that currently invoke non-public infrastructure behavior by string.
    - `src/TNC.Trading.Platform.Application/*`: add only the smallest enabling seams required for typed tests.
    - `src/TNC.Trading.Platform.Infrastructure/*`: add only the smallest enabling seams required for typed tests.
    - `docs/wiki/testing-and-quality.md`: update if the documented unit-test strategy changes materially.
  - **Work Item Dependencies**: Keep the seam changes behavior-preserving and prefer local, low-risk refactors that do not widen product API exposure unnecessarily.
  - **User Instructions**: Expect small compile-time seam changes in application or infrastructure code if the current private shape blocks maintainable testing.

### Work Item 3 details

- [x] Work Item 3: Rebalance expensive distributed auth coverage
  - [x] Build and test baseline established
  - [x] Task 1: Confirm overlap before trimming scenarios
    - [x] Step 1: Map the existing functional and E2E auth suites to the lower-level coverage added in Work Item 1.
    - [x] Step 2: Identify duplicated flows that assert the same route-first challenge, role-boundary, sign-in, sign-out, or recovery behavior at multiple higher levels.
    - [x] Step 3: Define the minimum retained real-runtime smoke matrix for sign-in, sign-out, and role-boundary coverage.
  - [x] Task 2: Consolidate runtime setup and retained smoke flows
    - [x] Step 1: Introduce or extend shared collection fixtures so related functional or E2E auth tests can reuse AppHost startup and authenticated session setup.
    - [x] Step 2: Trim duplicated high-level cases only after equivalent lower-level assertions are present and green.
    - [x] Step 3: Keep one browser-driven sign-in smoke, one sign-out smoke, and one role-boundary smoke against the real AppHost-plus-Keycloak path.
    - [x] Step 4: Update test comments so the remaining high-level tests explain why they still exist and what lower-level gaps they intentionally do not cover.
  - [x] Task 3: Validate determinism and runtime cost
    - [x] Step 1: Re-run the retained functional and E2E suites to confirm fixture reuse does not break isolation.
    - [x] Step 2: Confirm no new arbitrary waits were introduced as the primary stabilization mechanism.
    - [x] Step 3: Record any remaining justified high-cost cases in the wiki guidance if they remain intentional.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/*`: consolidate functional auth setup and trim duplicated scenarios.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/*`: consolidate E2E auth setup and trim duplicated scenarios.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs`: likely fixture touch point.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs`: likely fixture touch point.
    - `docs/wiki/testing-and-quality.md`: document the final suite ownership and retained smoke purpose.
    - `docs/wiki/local-development.md`: update only if the local validation workflow changes materially.
  - **Work Item Dependencies**: Depends on Work Item 1 so lower-level coverage exists before higher-level duplication is reduced.
  - **User Instructions**: Keep the retained real-runtime cases narrow and evidence-driven; do not remove the last smoke path for any protected auth journey.

### Work Item 4 details

- [x] Work Item 4: Restore repository-wide traceability and testing guidance
  - [x] Build and test baseline established
  - [x] Task 1: Normalize traceability in affected tests
    - [x] Step 1: Update new or modified tests from Work Items 1 to 3 so comments capture requirement traceability, expected behavior, and why the behavior matters.
    - [x] Step 2: Replace `regression`-only intent markers in touched suites with explicit requirement, risk, or feature-area references where the mapping is known.
    - [x] Step 3: Ensure test names remain in `MethodName_StateUnderTest_ExpectedResult` style where new or renamed tests are introduced.
  - [x] Task 2: Update wiki guidance and coverage mapping
    - [x] Step 1: Update `docs/wiki/testing-and-quality.md` so it reflects current suite ownership across work packages 003, 004, and 005 rather than only older traceability references.
    - [x] Step 2: Add or refresh a project-wide coverage map in the wiki that explains which suite covers which risks and where intentional smoke-only coverage remains.
    - [x] Step 3: Update `docs/wiki/local-development.md` if the documented local validation flow or runtime expectations changed during the mitigation work.
    - [x] Step 4: Verify affected wiki links still resolve after the updates.
  - [x] Task 3: Final review readiness
    - [x] Step 1: Confirm every finding `F1` to `F4` is explicitly closed or reduced by at least one delivered work item.
    - [x] Step 2: Confirm the mitigation plan status can be advanced only after the wiki and test-comment updates are complete.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `docs/wiki/testing-and-quality.md`: primary documentation touch point for suite ownership and traceability.
    - `docs/wiki/local-development.md`: secondary documentation touch point if local validation guidance changes.
    - `test/TNC.Trading.Platform.Web/*`: update comments and traceability in touched test files.
    - `test/TNC.Trading.Platform.Api/*`: update comments and traceability in touched test files.
    - `test/TNC.Trading.Platform.Application/*`: update comments and traceability in touched test files.
    - `test/TNC.Trading.Platform.Infrastructure/*`: update comments and traceability in touched test files.
  - **Work Item Dependencies**: Run after the earlier work items so the wiki describes the delivered state rather than the intended state.
  - **User Instructions**: Review the updated wiki content as implementation documentation, not as a work-package note, before considering the mitigation complete.

### Work Item N details (copy/paste)

Copy the **Work Item 1 details** section for each additional work item.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**:
  - `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
- **Integration tests**:
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
- **Functional tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`
- **E2E tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`
- **Manual checks**:
  - Start the AppHost and confirm the Web UI, API, Keycloak, Mailpit, and Scalar surfaces still resolve through the delivered local topology.
  - Validate one retained real-runtime sign-in flow, one sign-out flow, and one role-boundary flow after the distributed-suite consolidation work.
  - Review refreshed Blazor UI surfaces manually for theme behavior, remembered preference, and narrower-width layout behavior if those remain intentionally outside automated coverage.
- **Security checks**:
  - Confirm no new secrets, machine-specific defaults, or checked-in credentials were introduced while enabling tests.
  - Confirm protected API and Web tests still fail closed for unauthorized and insufficient-role paths.
  - Confirm any retained real-runtime auth smoke continues to use the real AppHost-plus-Keycloak path rather than a substitute runtime.

## Acceptance checklist

- [x] Every planned mitigation maps back to one or more findings in `001-project-test-review-report.md`.
- [x] High-priority missing or weak coverage is addressed before lower-priority improvements.
- [x] The plan prefers lower-level automated tests before higher-level tests where practical.
- [x] Required validation steps are defined for each work item.
- [x] Relevant `docs/wiki/` pages are updated to reflect the delivered testing or implementation changes.
- [x] Affected wiki links resolve after documentation updates.
- [x] Rollback/backout plan documented for each work item.

## Notes

- Confirmed evidence used for this plan comes from `docs/005-refactor-app-host/001-project-test-review-report.md`, `docs/005-refactor-app-host/requirements.md`, `docs/005-refactor-app-host/technical-specification.md`, `docs/005-refactor-app-host/plans/001-delivery-plan.md`, `docs/wiki/testing-and-quality.md`, `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`, `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`, `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ApplicationReflection.cs`, and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/InfrastructureReflection.cs`.
- Assumption: the repository can continue to use repo-root `dotnet build` and the listed project-level `dotnet test` commands as the standard validation baseline for this mitigation work.
- Assumption: adding Blazor component tests may require a minimal test harness dependency in the existing Web unit-test project because the current project file only references xUnit and the ASP.NET Core shared framework.
- Assumption: some `F2` mitigation may require small enabling production-code seams, but the plan intentionally avoids broad API-surface expansion unless a narrower option is unavailable.
