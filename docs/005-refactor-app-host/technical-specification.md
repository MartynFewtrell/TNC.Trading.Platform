# Refactor AppHost Technical Specification

This document describes how work package `005-refactor-app-host` will be implemented so the platform's Aspire AppHost becomes easier to understand and distributed tests validate the real Aspire-managed topology instead of substitute runtime branches.

## 1. Summary

- **Source**: See `requirements.md` for canonical work metadata, requirement identifiers, and acceptance criteria. See `../business-requirements.md` for project-level business context.
- **Status**: draft
- **Input**: `requirements.md`, `../business-requirements.md`, and `../systems-analysis.md`
- **Output**: `plans/001-delivery-plan.md`

## 2. Problem and Context

### 2.1 Problem statement

The current AppHost composition concentrates multiple responsibilities in a single file, including infrastructure resource modeling, application-project wiring, environment configuration, and synthetic runtime branching used by distributed tests. That makes the composition harder to review and increases the risk that distributed tests validate a different topology from the one used in real local startup.

### 2.2 Assumptions

- The AppHost remains the correct Aspire composition root for local development.
- The current resources and local runtime topology remain the intended baseline.
- AppHost-backed distributed tests should validate the same real topology used by normal AppHost startup.
- Smaller isolated unit tests can remain unit tests where infrastructure is not part of the contract under test.

### 2.3 Constraints

- The AppHost must remain composition-only and must not take on business logic responsibilities.
- Existing resource names, service relationships, and exposed capabilities should remain stable unless a direct compatibility issue requires change.
- Distributed AppHost-backed validation must use Aspire-managed integrations and not in-memory runtime substitute behavior.
- Documentation must remain self-contained inside `docs/005-refactor-app-host/` with any affected wiki updates completed when implementation finishes.

## 3. Proposed Solution

### 3.1 Approach

Refactor the AppHost in two layers. First, simplify the composition root by separating infrastructure modeling, application-project registration, and shared environment wiring into smaller AppHost support files. Second, remove the synthetic runtime branch used by AppHost-backed distributed tests and replace it with real Aspire-managed integration flows for API, Web, and authentication validation.

The top-level AppHost entry point should become a thin orchestrator that:

1. creates the distributed application builder
2. reads supported AppHost configuration
3. composes infrastructure resources
4. composes the API and Web projects
5. applies shared environment wiring
6. builds and runs the distributed application

This keeps the AppHost aligned with current Aspire guidance that the AppHost is the code-first place to declare services and relationships. The distributed test changes align with Aspire testing guidance that AppHost-backed tests should be closed-box and should validate the real distributed application rather than an in-process or substitute topology.

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| ------ | ------- | ---- | ---- | ------------------ |
| A | Keep the current AppHost structure and only add comments | Lowest implementation cost | Leaves complexity, branching, and test-topology drift in place | Rejected because it does not materially improve maintainability or change safety |
| B | Extract cohesive AppHost support units and remove synthetic distributed runtime paths | Preserves behavior while improving clarity and keeping distributed validation realistic | Requires coordinated changes across AppHost and tests | Accepted because it addresses both the complexity and the test drift at the root |
| C | Replace most AppHost-backed tests with smaller isolated tests | Faster tests in some areas | Weakens confidence in the real distributed topology | Rejected because the requirement is that AppHost-backed distributed tests run against Aspire integrations |

### 3.3 Architecture

- **Components**:
  - `TNC.Trading.Platform.AppHost` as the composition root
  - focused AppHost support files for infrastructure composition, project composition, and shared environment wiring
  - API and Web distributed test suites that start the real AppHost through Aspire testing
- **Data flows**:
  - AppHost starts and composes infrastructure and projects through explicit resource references and waits
  - distributed test suites start the AppHost as a separate process graph and validate external runtime behavior
  - authentication and protected-route validation use the real runtime topology instead of substitute configuration branches
- **Dependencies**:
  - Aspire AppHost composition APIs
  - Aspire.Hosting.Testing and `DistributedApplicationTestingBuilder`
  - existing Keycloak, SQL Server, Mailpit, API, and Web runtime topology

## 4. Requirements Traceability

| Requirement ID | Requirement | Implementation notes | Validation approach |
| -------------- | ----------- | -------------------- | ------------------- |
| FR1 | AppHost remains the composition root | Keep AppHost as the startup entry point while simplifying its structure | AppHost startup validation and distributed tests |
| FR2 | AppHost becomes easier to understand and change | Extract cohesive composition responsibilities into small support files | Code review plus build and distributed validation |
| FR3 | AppHost-backed distributed tests use Aspire-managed integrations | Remove synthetic substitute runtime branching from AppHost-backed distributed tests | Integration, functional, and E2E runs against the real topology |
| FR4 | Local runtime topology and capabilities remain preserved | Keep resource names, links, waits, and auth behavior stable | Manual AppHost walkthrough and automated validation |
| NF1 | Improve cohesion and reviewability | Thin top-level AppHost file and explicit supporting units | Code review |
| NF2 | Preserve startup behavior | Keep waits, references, and environment keys stable | Build plus manual and automated startup validation |
| NF3 | Stay aligned with repo Aspire guidance | Keep AppHost composition-only and keep distributed tests closed-box | Architecture review plus test review |
| NF5 | Keep distributed validation realistic | Eliminate in-memory substitute runtime behavior from AppHost-backed suites | Test audit and automated test execution |
| SR1 | Preserve auth behavior | Keep current protected API and Web behavior unchanged | Auth-focused integration and functional tests |
| SR3 | Avoid bypassing runtime protections in distributed tests | Remove substitute runtime branches from AppHost-backed distributed validation | Test review and automated execution |
| IR2 | Use Aspire testing patterns | Start the AppHost with `DistributedApplicationTestingBuilder` and validate through external boundaries | Distributed test execution |
| TR1 | Cover refactored AppHost startup | Validate AppHost startup and exposed topology after refactor | Build and distributed validation |
| TR2 | Cover removal of synthetic distributed runtime paths | Ensure AppHost-backed suites no longer depend on the synthetic path | Test audit and execution |
| TR3 | Cover real Aspire-managed local validation | Walk through the real AppHost runtime after refactor | Manual validation |

## 5. Detailed Design

### 5.1 Public APIs / Contracts

| Area | Contract | Example | Notes |
| ---- | -------- | ------- | ----- |
| AppHost composition | AppHost remains the startup entry point | `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` | No new startup entry point is introduced |
| Distributed tests | AppHost-backed suites start the real distributed application | `DistributedApplicationTestingBuilder.CreateAsync<Projects.TNC_Trading_Platform_AppHost>()` | Validation must reflect real runtime topology |

### 5.2 Data Model

No new persistent domain data model is introduced by this work package.

### 5.3 Implementation Plan (technical steps)

| Step | Change | Files/Modules | Notes |
| ---- | ------ | ------------- | ----- |
| 1 | Extract infrastructure resource registration from AppHost | `src/TNC.Trading.Platform.AppHost/*` | Keep resource modeling cohesive and explicit |
| 2 | Extract API and Web project composition | `src/TNC.Trading.Platform.AppHost/*` | Keep project registration separate from infrastructure modeling |
| 3 | Centralize shared environment wiring | `src/TNC.Trading.Platform.AppHost/*` | Reduce duplication while preserving service-specific configuration |
| 4 | Remove synthetic AppHost runtime settings and branch | `src/TNC.Trading.Platform.AppHost/AppHost.cs`, `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs` | Only after real-runtime test helpers are in place |
| 5 | Migrate AppHost-backed integration, functional, and E2E tests to real Aspire-managed runtime validation | `test/TNC.Trading.Platform.Api/*`, `test/TNC.Trading.Platform.Web/*` | Keep tests closed-box and aligned with Aspire testing guidance |
| 6 | Update docs and validation guidance | `docs/wiki/*`, `docs/005-refactor-app-host/*` | Required before the work package is considered complete |

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| -------- | ----------------- | --------------- |
| AppHost extraction changes a wait or reference unexpectedly | Build or runtime validation should fail clearly before completion | Existing Aspire startup output and test failures |
| Real-runtime test migration exposes hidden synthetic-path assumptions | Failing distributed tests identify the remaining mismatch explicitly | Test failures and runtime logs |
| Refactor accidentally changes auth or endpoint behavior | Functional, integration, or manual validation should detect the regression | Existing runtime logs and auth test coverage |

### 5.5 Configuration

| Setting | Purpose | Default | Location |
| ------ | ------- | ------- | -------- |
| `Authentication:Test:EnableInteractiveSignIn` | Supports explicit test sign-in behavior where still required by non-AppHost-specific test harnesses | `false` | External configuration |
| `NotificationTransports:AzureCommunicationServices:*` | Supplies ACS configuration to the API project | None | External configuration |

The synthetic AppHost runtime switch should be removed from the delivered AppHost composition as part of this work package.

## 6. Security Design

- **AuthN/AuthZ**: Preserve the current Keycloak-backed local auth behavior and protected API/UI boundaries.
- **Secrets**: Do not add new checked-in secrets or machine-specific defaults.
- **Threat model notes**:
  - substitute runtime branches in distributed tests can hide real-runtime auth and topology regressions
  - this work package reduces that risk by moving AppHost-backed distributed validation onto the real Aspire-managed path

## 7. Observability

| Signal | What | Where | Notes |
| ------ | ---- | ----- | ----- |
| AppHost startup output | Resource creation, endpoint exposure, dependency readiness | AppHost console and dashboard | Used to validate preserved topology after refactor |
| Health endpoints | API readiness and liveness | Runtime endpoints | Must remain stable |
| Auth and runtime behavior | Protected route and endpoint outcomes | Existing logs and distributed test results | Used to confirm behavior preservation |

## 8. Testing Strategy

- Keep isolated unit tests where infrastructure is not part of the contract under test.
- Require AppHost-backed distributed validation to use real Aspire-managed integrations.
- Validate the refactor through build, integration, functional, E2E, and one manual AppHost walkthrough.