# Work Package Test Mitigation Plan

This plan converts the findings in `docs/005-refactor-app-host/002-project-test-review-report.md` into sequenced mitigation work so the AppHost refactor gains direct lower-level evidence, distributed tests become cheaper and more deterministic, and the documented coverage story matches the repository's real test assets.

## Summary

- **Source review**: `../002-project-test-review-report.md`
- **Work package**: `./docs/005-refactor-app-host/`
- **Status**: `completed`
- **Inputs**:
  - `../002-project-test-review-report.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - `001-delivery-plan.md`
  - `002-work-package-test-mitigation-plan.md`

## Description of work

This mitigation plan addresses all confirmed issues in the latest AppHost work-package test review by prioritizing low-cost, high-signal tests before adding or retaining more expensive distributed validation. The plan focuses on five outcomes: add direct AppHost coverage for the refactored support units (`F1`, `F6`), reduce repeated AppHost startup cost in distributed suites (`F2`), strengthen API and operational-path regression coverage while removing brittle reflection seams (`F3`), reconcile Web component coverage and test hygiene with the wiki (`F4`, `F5`), and refresh quantitative quality evidence so the repository can confirm that the mitigation work materially improved coverage and mutation resistance.

The work stays scoped to the issues identified in the review. It may require small behavior-preserving supporting changes that make code easier to test, but it must prefer narrow seams, shared fixtures, compile-time-safe tests, and documentation updates over broad product refactoring.

## Mitigation approach

- **Delivery model**: `phased hardening`
- **Branching**: continue on `005-refactor-app-host` and deliver each work item in sequence so every slice can be validated independently.
- **Dependencies**:
  - `src/TNC.Trading.Platform.AppHost/`
  - `src/TNC.Trading.Platform.Api/`
  - `src/TNC.Trading.Platform.Web/`
  - `test/TNC.Trading.Platform.Api/`
  - `test/TNC.Trading.Platform.Web/`
  - `test/TNC.Trading.Platform.Application/`
  - `test/TNC.Trading.Platform.Infrastructure/`
  - `docs/wiki/testing-and-quality.md`
  - `docs/wiki/local-development.md`
- **Key risks**:
  - Adding AppHost-focused tests may require new test seams or a new test project.
    - **Mitigation**: keep seams internal and behavior-preserving, and prefer direct tests of existing support types before introducing broader integration surfaces.
  - Reusing AppHost startup across distributed suites could leak shared state or hide isolation problems.
    - **Mitigation**: introduce collection-level fixtures deliberately, keep retained real-runtime smokes narrow, and preserve per-test state cleanup where route or session behavior depends on it.
  - Replacing reflection-heavy tests may expose areas that currently have no compile-time-safe seam.
    - **Mitigation**: prefer existing public or internal contracts first, then add the smallest internal seam needed for typed access.
  - Updating wiki guidance before the mitigation is complete could create another documentation drift.
    - **Mitigation**: update `docs/wiki/` only after the corresponding test or implementation slice is green and validated.

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | AppHost refactor coverage for support units and preserved topology (`RA1`) | Missing / Partial | `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs`; `AppHostInfrastructureRegistration.cs`; `AppHostProjectRegistration.cs`; `AppHostEnvironmentWiring.cs`; review found no focused lower-level tests | Add direct AppHost unit tests for configuration parsing and environment wiring, plus one focused composition smoke that asserts resource registration, waits, and exposed links remain intact after refactors. |
| `F2` | Repeated high-cost distributed startup and inconsistent fixture reuse (`RA3`, `RA7`) | Weak / Expensive | API integration tests and several Web functional tests repeatedly create and start AppHost per test | Introduce shared API and Web distributed fixtures where isolation is not required, keep only the smallest real-runtime smoke matrix, and move simple challenge or redirect checks to cheaper suites when equivalent coverage exists. |
| `F3` | Uneven lower-level coverage, brittle reflection, and weak fast-feedback API coverage (`RA5`, `RA8`) | Weak / Partial | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/ApiReflection.cs`; low Coverlet and Stryker signal for API unit tests; narrow operational-path evidence | Replace reflection-heavy tests with typed seams, expand API unit or integration coverage for endpoint registration, auth configuration, auth-audit payload handling, and operational event or notification-path assumptions, and improve mutation resistance in the cheapest suites first. |
| `F4` | Web component coverage claim does not clearly match reviewed test assets (`RA6`) | Partial / Stale | `docs/wiki/testing-and-quality.md` claims bUnit coverage for `MainLayout`, `Home`, `Status`, and `Configuration` | Confirm the current component tests cover the documented behavior, add missing component tests where gaps remain, and update the wiki to reflect the actual delivered coverage boundaries. |
| `F5` | Test naming, XML comments, and maintainability drift (`RA8`) | Partial / Fragile | Duplicate XML summary in `PlatformAuthenticationFunctionalTests.cs`; naming drift in `PlatformThemeStateTests.cs`; reflection helpers weaken compile-time safety | Normalize test names and comments to repository standards, remove malformed XML comment structure, and ensure new or revised tests explain traceability, expected outcomes, and rationale. |
| `F6` | Provider-parity and auth-provider branch coverage remain narrow (`RA1`, `RA6`) | Partial | `AppHostEnvironmentWiring.ConfigureAuthenticationProvider` still contains provider branching with limited focused tests | Add direct provider-branch tests for Keycloak and Test API provider decisions, plus focused AppHost validation that proves the default delivered runtime remains Keycloak-backed while the isolated synthetic API negatives remain intentionally scoped. |

## Mitigation Plan

### Execution gates (required)

Before starting *any* mitigation work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build or tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (review findings) | Traceability (requirements) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Add direct AppHost coverage for refactored support units | Add focused lower-level coverage for settings parsing, infrastructure and project registration, provider branching, and preserved topology so AppHost regressions are caught before a broad distributed run. | `F1, F6` | `FR1, FR2, FR3, FR4, NF1, NF2, NF3, NF5, SR1, SR3, IR1, IR2, TR1, TR2, TR3, OR1` | Baseline item; should land before fixture consolidation so the core AppHost behavior has fast evidence. | `dotnet build`; repo-root `dotnet test`; focused AppHost test runs once the new tests exist; manual verification that preserved resource names and links still match the delivered topology. | Revert the new AppHost tests and any small enabling seams together if they prove coupled to implementation details rather than observable composition behavior. | Review the added AppHost tests for provider-parity coverage, preserved resource names, and proof that only the API test-provider override remains synthetic. |
| Work Item 2: Consolidate distributed startup and narrow retained high-cost auth coverage | Reuse AppHost startup across API integration and Web functional auth suites where isolation is not required, and keep only the real-runtime checks that still add unique value above lower-level coverage. | `F2` | `FR3, FR4, NF2, NF5, SR1, SR3, IR2, TR1, TR2, TR3, OR2` | Depends on Work Item 1 so reduced distributed scope is backed by stronger lower-level AppHost coverage. | `dotnet build`; repo-root `dotnet test`; targeted API integration, Web functional, and Web E2E runs; manual check that retained sign-in, sign-out, and insufficient-role smokes still pass against the real AppHost-plus-Keycloak path. | Restore the removed scenario or fixture-sharing change if collection reuse creates hidden state bleed or if a required sign-in, sign-out, or role-boundary contract loses real-runtime coverage. | Confirm at least one real-runtime sign-in smoke, one post-sign-out fail-closed smoke, and one insufficient-role smoke remain after consolidation. |
| Work Item 3: Strengthen API and operational-path regression coverage while removing brittle reflection | Expand fast API coverage, reduce string-based reflection, and add focused tests for auth-audit, validation-problem, and notification or operational-path assumptions that currently rely on expensive or indirect evidence. | `F3, F5` | `FR2, FR3, FR4, NF1, NF5, SR1, SR3, TR1, TR2, OR1` | Can begin after Work Item 1; complete before quality-metric reruns so the new fast-feedback coverage is included. | `dotnet build`; repo-root `dotnet test`; targeted API unit and integration runs; focused Application and Infrastructure unit-test runs where typed seams or operational-path helpers change. | Revert the seam changes and associated tests together if they widen product API surface unnecessarily or weaken encapsulation without meaningful regression-value gain. | Prefer compile-time-safe tests over generic helpers, and keep any new seam `internal` unless external consumption already exists. |
| Work Item 4: Reconcile Web component coverage, naming, and traceability with the wiki | Confirm or add the documented Blazor component tests, fix malformed comments and naming drift, and align `docs/wiki/testing-and-quality.md` with the delivered Web coverage boundaries. | `F4, F5` | `FR4, NF3, OR1, OR2, TR3` | Depends on Work Item 2 for final distributed-suite boundaries and on Work Item 3 for any shared traceability or naming updates. | `dotnet build`; repo-root `dotnet test`; targeted Web unit, functional, and E2E runs for touched files; markdown link review for updated wiki pages. | Revert the documentation and test-comment updates with the related test changes if the delivered suite boundaries differ from the documented guidance. | Read the updated wiki as implementation documentation and confirm it names only discoverable tests and currently retained distributed smokes. |
| Work Item 5: Re-run coverage and mutation evidence and record the hardening outcome | Re-measure the strengthened suites with Coverlet and Stryker so the repository can confirm that the mitigation work materially improved the weakest areas identified by the review. | `F1, F3, F4, F6` | `NF5, TR1, TR2, TR3, OR1` | Run after Work Items 1 to 4 so the measurements reflect the delivered mitigation rather than the baseline. | `dotnet build`; repo-root `dotnet test`; focused Coverlet reruns for the strengthened unit-test projects; focused Stryker reruns for the same projects; wiki update if the documented coverage or mutation story changes materially. | If the rerun metrics expose unstable or low-value mitigation work, revert the most recent test changes that caused regressions and keep the last green validated slice. | Treat coverage and mutation results as quality evidence, not as a reason to add arbitrary or brittle tests; follow up only on surviving high-value gaps. |

### Work Item 1 details

- [ ] Work Item 1: Add direct AppHost coverage for refactored support units
  - [x] Build and test baseline established
  - [x] Task 1: Add low-cost AppHost settings and wiring tests
    - [x] Step 1: Add focused tests for `AppHostSettings.FromConfiguration` covering default values, explicit `Authentication:ApiProvider`, interactive sign-in parsing, and ACS setting passthrough.
    - [x] Step 2: Add focused tests for `AppHostEnvironmentWiring.ConfigureAuthenticationProvider` covering the default Keycloak path, the explicit `Test` API provider path, and the Web project's fixed Keycloak runtime behavior.
    - [x] Step 3: Add focused tests for `ConfigureApiProject` and `ConfigureWebProject` covering required environment keys, Mailpit wiring, and ACS environment inclusion only when values are present.
  - [x] Task 2: Add focused composition tests for preserved topology
    - [x] Step 1: Add a focused AppHost composition smoke that asserts expected resources, waits, and exposed links remain present after refactors.
    - [x] Step 2: Add focused tests for `AppHostInfrastructureRegistration` and `AppHostProjectRegistration` so resource-model and project-reference drift is caught without a full manual walkthrough.
    - [x] Step 3: Add provider-parity checks proving the default delivered runtime remains Keycloak-backed while the isolated API synthetic-negative path remains deliberately scoped.
  - [x] Task 3: Preserve traceability and docs
    - [x] Step 1: Add or update test comments so new AppHost tests explain the requirement traceability, expected behavior, and why the behavior matters.
    - [x] Step 2: Update `docs/wiki/testing-and-quality.md` if the AppHost-focused coverage map changes materially.
    - [x] Step 3: Update `docs/wiki/local-development.md` only if the preserved-topology validation workflow changes.
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs`: target for focused configuration-parsing tests.
    - `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs`: target for infrastructure registration coverage.
    - `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs`: target for project registration coverage.
    - `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs`: target for provider-branch and environment-wiring coverage.
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: target only if a small enabling seam is needed for composition assertions.
    - `test/TNC.Trading.Platform.AppHost/*` or the closest existing test location adopted for new AppHost-focused tests: add direct AppHost unit or composition tests.
    - `docs/wiki/testing-and-quality.md`: update coverage ownership if the AppHost coverage story changes.
    - `docs/wiki/local-development.md`: update only if local validation steps change.
  - **Work Item Dependencies**: Complete before reducing distributed-suite scope so fast AppHost evidence exists first.
  - **User Instructions**: Prefer one AppHost-focused unit or integration test project over scattering AppHost assertions across unrelated suites.

### Work Item 2 details

- [ ] Work Item 2: Consolidate distributed startup and narrow retained high-cost auth coverage
  - [x] Build and test baseline established
  - [x] Task 1: Introduce or extend shared distributed fixtures where isolation is unnecessary
    - [x] Step 1: Add an API integration collection fixture so `PlatformAuthenticationIntegrationTests` and related distributed auth tests can reuse one AppHost runtime where request isolation is sufficient.
    - [x] Step 2: Consolidate remaining Web functional anonymous redirect tests onto the shared auth fixture rather than starting AppHost per test.
    - [x] Step 3: Confirm fixture reuse does not hide state coupling by preserving explicit session setup and cleanup per test where required.
  - [x] Task 2: Move simple checks to cheaper suites when equivalent evidence exists
    - [x] Step 1: Move or duplicate simple anonymous redirect and policy-shape checks into lower-level unit or lightweight HTTP tests before trimming redundant distributed cases.
    - [x] Step 2: Keep only the real-runtime distributed checks that still prove a unique sign-in, sign-out, or insufficient-role contract.
    - [x] Step 3: Remove duplicated high-cost scenarios only after the lower-level replacement tests are green and traceable.
  - [x] Task 3: Preserve traceability and docs
    - [x] Step 1: Update retained distributed-test comments so they explain why each remaining high-cost case still exists.
    - [x] Step 2: Update `docs/wiki/testing-and-quality.md` to describe the final retained distributed-smoke matrix.
    - [x] Step 3: Reviewed `docs/wiki/local-development.md`; no update was required because the recommended local validation flow already matches the delivered shared-runtime auth validation flow.
  - [x] Build and test validation
    - Validation note: the focused retained distributed suites are now green in isolation: `TNC.Trading.Platform.Api.IntegrationTests` (23/23), `TNC.Trading.Platform.Web.FunctionalTests` (6/6), and `TNC.Trading.Platform.Web.E2ETests` (1/1). The previously observed repo-root instability did not reproduce in the isolated AppHost-backed projects.

  - **Files**:
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`: likely fixture-reuse and scenario-scope touch point.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/*Collection*.cs`: likely new or updated API distributed fixture location.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`: reduce repeated per-test AppHost startup where it no longer adds value.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`: reduce repeated per-test AppHost startup for anonymous protected-route checks.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs`: likely shared-fixture touch point.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/*`: confirm the retained browser smoke set remains narrow and intentional.
    - `docs/wiki/testing-and-quality.md`: update the documented high-cost suite boundaries.
    - `docs/wiki/local-development.md`: update only if local validation guidance changes.
  - **Work Item Dependencies**: Requires Work Item 1 so AppHost behavior still has strong lower-level coverage after distributed consolidation.
  - **User Instructions**: Do not remove the last real-runtime proof of any required sign-in, sign-out, or role-boundary journey.

### Work Item 3 details

- [ ] Work Item 3: Strengthen API and operational-path regression coverage while removing brittle reflection
  - [x] Build and test baseline established
  - [x] Task 1: Replace reflection-heavy API test access with typed seams
    - [x] Step 1: Inventory the current `ApiReflection` usages and group them by constructor access, non-public method invocation, enum parsing, property inspection, and in-memory persistence setup.
    - [x] Step 2: Move tests to existing public or internal compile-time-safe contracts where those contracts already exist.
    - [x] Step 3: Add the smallest internal seam needed for any remaining high-value API tests that cannot be expressed without string-based reflection.
    - [x] Step 4: Remove or narrow `ApiReflection.cs` once the targeted tests no longer depend on generic string-based access.
  - [x] Task 2: Expand low-cost API and operational-path coverage
    - [x] Step 1: Add focused coverage for endpoint registration, auth-configuration behavior, and protected-route contract shape where current API unit coverage is weakest.
    - [x] Step 2: Add targeted tests for auth-audit validation-problem payloads, display-name fallback, and operational event-shape expectations that currently rely on indirect evidence.
    - [x] Step 3: Add focused tests for notification-path or runtime notification assumptions where the review identified narrower confidence than storage-level coverage alone.
    - [x] Step 4: Ensure new or revised API tests assert specific payloads, status codes, and requirement-driven outcomes rather than broad success conditions.
  - [x] Task 3: Preserve traceability and docs
    - [x] Step 1: Update test comments in touched API, application, or infrastructure suites so they capture traceability, expected behavior, and rationale.
    - [x] Step 2: Update `docs/wiki/testing-and-quality.md` if the API-unit versus integration coverage ownership changes materially.
    - [x] Step 3: Record any deliberate remaining synthetic-only negatives in the wiki so the boundary stays explicit.
  - [x] Build and test validation
    - Validation note: `dotnet build`, `TNC.Trading.Platform.Api.UnitTests` (14/14), `TNC.Trading.Platform.Application.UnitTests` (27/27), `TNC.Trading.Platform.Infrastructure.UnitTests` (20/20), and `TNC.Trading.Platform.Api.IntegrationTests` (23/23) are green. The earlier repo-root instability did not reproduce in the focused validation projects used for this work item.

  - **Files**:
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/*`: continue expanding low-cost API coverage with typed tests after removing the former `ApiReflection.cs` helper.
    - `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs`: focused internal seam for cheap auth-audit summary and display-name fallback coverage.
    - `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditRecord.cs`: typed auth-audit record shape shared by the resolver and tests.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: extend focused API-boundary coverage where runtime evidence is still required.
    - `src/TNC.Trading.Platform.Api/*`: add only the smallest internal seam required for typed tests.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/*`: update if operational-path behavior is better exercised here than at the API layer.
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs`: notification runtime branch target for recipient, provider-registration, and handled-exception coverage.
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/*`: update if notification-path assumptions are better exercised here than at the API layer.
    - `docs/wiki/testing-and-quality.md`: update coverage ownership and remaining deliberate gaps.
  - **Work Item Dependencies**: Finish before metric reruns so the improved fast-feedback tests are included in the new quality evidence.
  - **User Instructions**: Favor compile-time failure for renamed members over runtime reflection failure, and keep the production API surface narrow.

### Work Item 4 details

- [ ] Work Item 4: Reconcile Web component coverage, naming, and traceability with the wiki
  - [x] Build and test baseline established
  - [x] Task 1: Confirm the real Web component coverage inventory
    - [x] Step 1: Review the existing `MainLayoutTests`, `HomeTests`, `StatusTests`, and `ConfigurationTests` files to confirm which documented behaviors are already covered.
    - [x] Step 2: Add or strengthen bUnit tests where the wiki claims coverage but the reviewed assertions are incomplete or missing.
    - [x] Step 3: Keep component tests focused on rendered behavior, authorization-state presentation, and operator-facing affordances rather than implementation details.
  - [x] Task 2: Fix test hygiene and maintainability drift
    - [x] Step 1: Remove malformed or duplicated XML comment structure in the touched functional and unit test files.
    - [x] Step 2: Rename outlier tests such as the `PlatformThemeStateTests` cases so they follow the required `MethodName_StateUnderTest_ExpectedResult` style.
    - [x] Step 3: Ensure all new or renamed tests include comments that explain requirement traceability, expected outcomes, and why the behavior matters.
  - [x] Task 3: Reconcile documentation with delivered coverage
    - [x] Step 1: Update `docs/wiki/testing-and-quality.md` so it names only the Blazor component tests and coverage boundaries that now exist.
    - [x] Step 2: Update any affected local-development or testing wiki guidance if the recommended validation mix changes.
    - [x] Step 3: Verify affected wiki links still resolve after the updates.
  - [x] Build and test validation
    - Validation note: `dotnet build`, `TNC.Trading.Platform.Web.UnitTests` (75/75), `TNC.Trading.Platform.Web.FunctionalTests` (6/6), and `TNC.Trading.Platform.Web.E2ETests` (1/1) all passed after the Web reconciliation changes.

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs`: confirm or extend shell coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/HomeTests.cs`: confirm or extend landing-page coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs`: confirm or extend status-page coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs`: confirm or extend configuration-page coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformThemeStateTests.cs`: normalize naming and comments.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`: fix malformed XML comment structure in touched tests.
    - `docs/wiki/testing-and-quality.md`: reconcile documented Web coverage with the delivered test assets.
    - `docs/wiki/local-development.md`: update only if the documented validation mix changes.
  - **Work Item Dependencies**: Run after Work Items 2 and 3 so the wiki reflects the final distributed and lower-level coverage split.
  - **User Instructions**: Keep the wiki evidence-backed; if a test class is not discoverable in the repository, do not document that coverage as delivered.

### Work Item 5 details

- [x] Work Item 5: Re-run coverage and mutation evidence and record the hardening outcome
  - [x] Build and test baseline established
  - [x] Task 1: Re-measure focused line and branch coverage
    - [x] Step 1: Re-run Coverlet for the strengthened unit-test projects, starting with the weakest baseline areas first.
    - [x] Step 2: Compare the resulting coverage outputs with the review baseline so the mitigation can show whether AppHost, API, and Web signal improved materially.
    - [x] Step 3: Record any remaining low-signal areas that still require follow-up rather than inflating coverage with low-value tests.
  - [x] Task 2: Re-measure mutation resistance
    - [x] Step 1: Re-run Stryker for the strengthened unit-test projects, starting with API and any new AppHost-focused unit-test project if one exists.
    - [x] Step 2: Investigate surviving high-value mutants and fix only the gaps that represent meaningful behavioral blind spots.
    - [x] Step 3: Record any remaining intentionally deferred mutation gaps with explicit rationale.
  - [x] Task 3: Publish the refreshed quality picture
    - [x] Step 1: Update `docs/wiki/testing-and-quality.md` if the delivered coverage or mutation story changed materially.
    - [x] Step 2: Create a new follow-up review or evidence note only if the work package requires a fresh report after mitigation.
    - [x] Step 3: Verify the mitigation plan closure references the refreshed quality evidence rather than the original baseline alone.
  - [x] Build and test validation
    - Validation note: `dotnet build`, repo-root `dotnet test`, and the focused Application, Infrastructure, API unit, API integration, Web unit, Web functional, and Web E2E project reruns all passed after the quality-evidence documentation updates. Coverlet reruns produced fresh AppHost, Application, Infrastructure, API, and Web reports, while Stryker reruns succeeded for AppHost, Application, Infrastructure, and API and remained blocked for Web by the documented `Program.cs` / `App` compile issue.

  - **Files**:
    - `docs/wiki/testing-and-quality.md`: update only if the measured quality story changes materially.
    - `docs/005-refactor-app-host/002-project-test-review-report.md`: leave unchanged as the baseline review reference.
    - `docs/005-refactor-app-host/*`: add a new follow-up evidence file only if a new report is explicitly required.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TestResults/*`: expected Coverlet output area.
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TestResults/*`: expected Coverlet output area.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TestResults/*`: expected Coverlet output area.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TestResults/*`: expected Coverlet output area.
  - **Work Item Dependencies**: Run last so the quality metrics describe the delivered mitigation state.
  - **User Instructions**: Use the metric reruns to prioritize any follow-up work; do not keep adding brittle tests only to move a percentage.

## Cross-cutting validation

- **Build**:
  - `dotnet build`
- **Unit tests**:
  - `dotnet test`
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
  - Start the AppHost and confirm the API, Web UI, Keycloak, Mailpit, and Scalar surfaces still resolve through the delivered local topology.
  - Validate one retained real-runtime sign-in path, one post-sign-out fail-closed path, and one insufficient-role path against the real AppHost-plus-Keycloak runtime.
  - Confirm the AppHost composition still exposes the expected resource links and waits after the focused AppHost tests are added.
  - Re-run focused coverage collection after the hardening work:
    - `Set-Location test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests; dotnet test --collect:"XPlat Code Coverage"`
    - `Set-Location test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests; dotnet test --collect:"XPlat Code Coverage"`
    - `Set-Location test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests; dotnet test --collect:"XPlat Code Coverage"`
    - `Set-Location test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests; dotnet test --collect:"XPlat Code Coverage"`
  - Re-run focused mutation testing after the hardening work:
    - `Set-Location test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests; dotnet stryker`
    - `Set-Location test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests; dotnet stryker`
    - `Set-Location test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests; dotnet stryker`
    - `Set-Location test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests; dotnet stryker`
- **Security checks**:
  - Confirm no new secrets, connection strings, or machine-specific defaults are introduced while enabling tests.
  - Confirm AppHost-backed distributed tests still default to the real Keycloak-backed topology and that any remaining synthetic API negatives stay isolated and explicit.
  - Confirm protected API and Web coverage still fails closed for anonymous, insufficient-role, malformed-token, and CSRF-negative paths.
  - Confirm auth-audit, notification, and operational-path assertions continue to avoid sensitive-value leakage.

## Acceptance checklist

- [x] Every planned mitigation maps back to one or more findings in `002-project-test-review-report.md`.
- [x] High-priority missing or weak coverage is addressed before lower-priority improvements.
- [x] The plan prefers lower-level automated tests before higher-level tests where practical.
- [x] Required validation steps are defined for each work item.
- [x] Relevant `docs/wiki/` pages are updated to reflect the delivered testing or implementation changes.
- [x] Affected wiki links resolve after documentation updates.
- [x] Rollback or backout guidance is documented for each work item.

## Notes

- **Confirmed evidence**:
  - The review report explicitly identifies direct AppHost coverage gaps for `AppHostSettings`, infrastructure registration, project registration, and environment wiring.
  - `AppHostSettings.FromConfiguration` and `AppHostEnvironmentWiring` currently expose concrete low-level behaviors that can be validated without relying on a broad distributed runtime.
  - `PlatformAuthenticationIntegrationTests` still show repeated AppHost startup per test, and the review identifies similar repeated startup in the Web functional auth suites.
  - `ApiReflection.cs` is a concrete example of brittle string-based reflection still present in the fast-feedback API suite.
  - The repository already contains `MainLayoutTests.cs`, `HomeTests.cs`, `StatusTests.cs`, `ConfigurationTests.cs`, and `PlatformThemeStateTests.cs`, so the immediate mitigation should reconcile actual assertions and documentation before inventing new Web coverage claims.
  - The current wiki already documents explicit build, unit, integration, functional, and E2E validation commands that can be reused as the mitigation baseline.
- **Assumptions and missing information**:
  - A dedicated AppHost test project does not appear in the current reviewed file inventory, so Work Item 1 may need to introduce one in `test/` unless an existing suite is chosen as the better home for AppHost-focused tests.
  - Exact future test-project paths for any new AppHost-focused tests cannot be inferred yet, so repo-root `dotnet test` is the minimum default command until those paths are finalized.
- A dedicated AppHost unit-test project now exists and was included in the final Coverlet and Stryker evidence pass.
  - This plan assumes the repository will continue to treat `docs/wiki/` as the implementation documentation source of truth and will keep the baseline review report unchanged while adding any follow-up evidence as new files if needed.
