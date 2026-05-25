# Refactor AppHost Requirements

This document defines the work-package requirements for refactoring the Aspire AppHost so the composition remains easier to understand, safer to change, and aligned with the repository's Aspire and testing conventions.

## 1. Summary

- **Work item**: Refactor AppHost
- **Work folder**: `./docs/005-refactor-app-host/`
- **Business requirements**: `../business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-05-18
- **Status**: draft
- **Outputs**:
  - `technical-specification.md`
  - `plans/001-delivery-plan.md`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `../business-requirements.md` |
| Systems analysis | `../systems-analysis.md` |
| Requirements | `requirements.md` |
| Technical specification | `technical-specification.md` |
| Initial delivery plan | `plans/001-delivery-plan.md` |

## 2. Context

### 2.1 Background

The current AppHost composition has accumulated multiple responsibilities in one place, including infrastructure registration, application project registration, shared environment wiring, and a synthetic test runtime branch. That makes the composition harder to reason about and creates drift between the distributed runtime used in local development and the topology used by many AppHost-backed tests.

This work package creates a dedicated refactoring slice to simplify the AppHost structure and ensure that distributed tests exercising the AppHost run against real Aspire-managed integrations instead of in-memory substitute runtime paths.

## 3. Scope

### 3.1 In scope

- Refactoring the AppHost composition into smaller, cohesive responsibilities.
- Preserving AppHost as the single composition root for local distributed startup.
- Removing synthetic or in-memory substitute runtime behavior from AppHost-backed distributed tests.
- Updating AppHost-backed integration, functional, and E2E validation to use real Aspire-managed integrations.
- Updating documentation affected by the AppHost refactor or the distributed testing model.

### 3.2 Out of scope

- New runtime capabilities or new infrastructure resources.
- Changes to the intended operator authentication or authorization behavior.
- Broad refactoring outside the AppHost and directly related distributed test harnesses unless required to preserve behavior.
- Converting isolated unit tests into distributed integration tests where infrastructure is not part of the contract under test.

## 4. Functional Requirements

| ID | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | --- | --- | --- | --- |
| FR1 | The AppHost must remain the composition root for the local distributed application. | The repository standard is that local orchestration is modeled through Aspire AppHost composition. | The application can still be started from the AppHost entry point after the refactor, and the same core services and resources are composed successfully. | The AppHost should remain composition-only. |
| FR2 | The AppHost composition must be easier to understand and change. | Large mixed-responsibility composition files increase change risk and review difficulty. | The delivered AppHost structure separates infrastructure registration, project registration, and shared environment wiring into smaller cohesive units without changing intended behavior. | The refactor should prefer the smallest safe structural change. |
| FR3 | AppHost-backed distributed tests must run against Aspire-managed integrations rather than in-memory substitute runtime paths. | Distributed validation should reflect the real runtime topology and reduce false confidence created by synthetic substitutes. | AppHost-backed distributed test suites no longer rely on synthetic runtime or in-memory substitute branches in AppHost composition. | This requirement applies to AppHost-backed distributed tests, not isolated unit tests. |
| FR4 | The refactor must preserve the current local runtime topology and exposed capabilities. | The refactor is intended to improve maintainability, not to change the delivered operator/runtime behavior. | Existing service/resource relationships, exposed endpoints, and local authentication flow remain functionally equivalent after the refactor. | Includes API, Web, Keycloak, Mailpit, and Scalar exposure where currently delivered. |

## 5. Non-Functional Requirements

| ID | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | --- | ----------- | -------------- | ------------------- |
| NF1 | Maintainability/Supportability | The AppHost composition must be more cohesive and easier to review. | Reduced responsibility mixing in the top-level AppHost entry point. | The top-level AppHost file becomes a thin orchestration entry point with clearer supporting composition units. |
| NF2 | Reliability/Availability | The refactor must preserve current runtime startup behavior. | No intentional regression in startup ordering, dependency waits, or exposed service links. | Validation confirms that the distributed application still starts and reaches the expected runtime surfaces. |
| NF3 | Standards Compatibility | The refactor must remain aligned with repository Aspire guidance. | AppHost remains composition-only and distributed tests use Aspire-managed closed-box validation. | The delivered implementation follows the repository's Aspire and Aspire-testing instructions. |
| NF4 | Observability | Existing observable runtime endpoints and diagnostics must remain available. | Health endpoints, dashboard-driven discovery, and service links remain usable. | Manual validation confirms the runtime remains observable after refactoring. |
| NF5 | Testability/Supportability | Distributed test coverage must reflect the real AppHost topology. | No AppHost-backed distributed tests depend on in-memory substitute runtime behavior. | Integration, functional, and E2E suites validate the real Aspire-managed path. |

## 6. Security Requirements

| ID | Category | Requirement | Acceptance criteria |
| --- | --- | ----------- | ------------------- |
| SR1 | Authentication/Authorization | The refactor must not weaken the current authentication and authorization behavior. | Protected API and Web behaviors remain unchanged from the operator perspective after the refactor. |
| SR2 | Secrets/Key Management | The refactor must not introduce new checked-in secrets or machine-specific defaults. | No new secrets, connection strings, or machine-specific paths are introduced in code or docs. |
| SR3 | Threats/Abuse Cases | Distributed tests must not bypass intended runtime protections through AppHost substitute branches. | AppHost-backed distributed validation exercises the intended runtime protections through the real Aspire-managed topology. |

## 7. Interfaces and Integration Requirements

| ID | Requirement | System | Contract | Acceptance criteria | Notes |
| --- | --- | ------ | -------- | ------------------- | ----- |
| IR1 | The AppHost must continue to compose the existing local infrastructure and application projects. | Aspire AppHost | Existing local composition model | API, Web, SQL Server, Keycloak, Mailpit, and related references continue to compose successfully after refactoring. | No new required resources are introduced by this package. |
| IR2 | AppHost-backed distributed validation must use Aspire testing patterns. | Aspire test harness | `DistributedApplicationTestingBuilder`-based closed-box validation | Distributed tests start the AppHost and validate runtime behavior through external boundaries rather than in-memory substitution. | Aligns with repo test rules. |

## 8. Testing Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | ------------------- | ----- |
| TR1 | Automated tests must cover the refactored AppHost startup and preserved topology. | Validation demonstrates the AppHost still starts the intended services and resources successfully. | Include focused distributed validation for the touched composition. |
| TR2 | Automated tests must cover the removal of synthetic AppHost-backed distributed runtime paths. | AppHost-backed integration, functional, and E2E suites no longer depend on substitute runtime behavior. | This does not require converting isolated unit tests to distributed tests. |
| TR3 | Local validation must cover the real Aspire-managed runtime path after refactoring. | A local validation flow demonstrates successful AppHost startup, real sign-in flow, and protected runtime behavior through the real topology. | Supports safe iteration during local development. |

## 9. Operational Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | ------------------- | ----- |
| OR1 | AppHost refactoring guidance and the resulting test model must be documented. | Relevant documentation reflects the delivered AppHost structure and distributed testing approach before the work package is considered complete. | Applies to affected `docs/wiki/` pages. |
| OR2 | Local development guidance must remain accurate after the AppHost refactor. | Documentation still describes how to start the AppHost and validate the local runtime after the refactor. | Applies when AppHost structure or validation guidance changes. |

## 10. Assumptions, Risks, and Dependencies

### 10.1 Assumptions

- The existing AppHost remains the correct composition root for the platform.
- The current resource set remains sufficient for this refactoring work package.
- AppHost-backed distributed tests should validate the real Aspire-managed topology rather than substitute runtime branches.

### 10.2 Risks

- Structural AppHost refactoring could accidentally change startup ordering or environment wiring.
  - **Mitigation**: keep the refactor behavior-preserving and validate the real runtime path after each slice.
- Removing synthetic runtime support from distributed tests could surface hidden dependencies in the current suites.
  - **Mitigation**: migrate tests in stages and strengthen real-runtime helpers before removing the branch.

### 10.3 Dependencies

- `../business-requirements.md`
- `../systems-analysis.md`
- `src/TNC.Trading.Platform.AppHost/`
- `test/TNC.Trading.Platform.Api/`
- `test/TNC.Trading.Platform.Web/`

## 11. Appendix

- Microsoft Learn: [AppHost overview](https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview)
- Aspire docs: [What is the AppHost?](https://aspire.dev/get-started/app-host/)
- Aspire docs: [Testing overview](https://aspire.dev/testing/overview/)