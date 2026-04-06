---
description: 'Standardize the repository test approach (unit-first with xUnit, plus Aspire closed-box integration/E2E and requirement-driven functional tests) so test projects are consistent, resilient, and CI-friendly.'
applyTo: 'test/**/*.cs, test/**/*.csproj'
---

# Test approach (unit, integration, E2E, functional)

## Overview

These instructions define how automated tests are structured and authored in this repository. Prefer fast, isolated unit tests. Add Aspire-based closed-box integration/E2E tests for distributed behavior. Add functional tests to validate documented requirements.

## Scope

Applies to: `test/**/*.cs, test/**/*.csproj`

- Applies when creating or updating any test projects or test code under `test/`.
- These rules are additive to:
  - `/.github/instructions/folders.instructions.md`
  - `/.github/instructions/aspire-tests.instructions.md`
  - `/.github/instructions/playwright.instructions.md`
  - `/.github/instructions/work-packages.instructions.md`

## Instructions

### MUST

- Keep all automated tests under `test/`.
- Use xUnit for unit test projects.
- Prefer unit tests over higher-level tests when a behavior can be validated without infrastructure.

- Standardize test project structure as: `test/<Service>/<Service>.<TestType>/...`
  - Use separate projects per test type so suites can be run independently:
    - `test/<Service>/<Service>.UnitTests`
    - `test/<Service>/<Service>.IntegrationTests`
    - `test/<Service>/<Service>.E2ETests`
    - `test/<Service>/<Service>.FunctionalTests`

- Use descriptive test file names that match the contained test class; avoid generic names like `UnitTest1.cs`.
- Automated test method names MUST follow the `MethodName_StateUnderTest_ExpectedResult` convention.
  - Prefer names that read as natural English, typically using `Should...` for the expected result and `When...` for the state under test.
  - Example: `CalculateTotal_ShouldReturnZero_WhenCartIsEmpty`
- Automated tests MUST be fully documented with comments immediately above each test method.
  - Test comments MUST capture requirement traceability when applicable, using identifiers from the relevant work package or requirement source (for example `FR1`, `NF2`, `SR3`, `TR1`).
  - Test comments MUST explain what the test is verifying, the expected outcome, and why the behavior matters or regression risk it is guarding against.
  - When the scenario is not obvious from the method name alone, test comments MUST also explain important setup assumptions or boundary conditions.
  - Prefer XML documentation comments (`///`) when the language and test framework support them cleanly.

- Keep test boundaries clear:
  - **Unit tests** MUST run in-process and in-memory.
    - Unit tests MUST NOT depend on real infrastructure (network, containers, databases, file system).
  - **Integration tests** MUST validate a single service boundary with real dependencies when needed.
    - If the test launches an Aspire `AppHost`, it MUST follow `/.github/instructions/aspire-tests.instructions.md`.
  - **E2E tests** MUST validate cross-service behavior through external boundaries using Aspire testing.
    - Aspire-based E2E tests MUST follow `/.github/instructions/aspire-tests.instructions.md` and treat the system as closed-box.
  - **Functional tests** MUST validate documented requirements/acceptance criteria.
    - If a functional test is UI-driven, it MUST follow `/.github/instructions/playwright.instructions.md`.

- Ensure tests are deterministic and repeatable:
  - Tests MUST NOT depend on execution order.
  - Tests MUST avoid shared mutable state across test runs.
  - Tests that create external state MUST either clean up, or use unique identifiers to prevent cross-test pollution.

#### Requirement traceability (functional tests)

- Functional tests MUST be organized per iterative work package.
  - The source of truth for work packages is `docs/<00x-work>/` as defined in `/.github/instructions/work-packages.instructions.md`.
  - Functional tests for a work package SHOULD be placed under a matching folder name under the functional test project:
    - `test/<Service>/<Service>.FunctionalTests/<00x-work>/...`
    - Example work package folder: `001-add-order-endpoint`

- Functional test method names MUST use the same `MethodName_StateUnderTest_ExpectedResult` convention as the rest of the suite.
  - Example: `RetryStatus_ShouldShowBlockedReason_WhenAuthenticationIsDegraded`
  - Functional test method names MUST NOT use numeric-only traceability names such as `<001>_<FR1>_point_of_test`

- Work package traceability MUST remain explicit through the work package folder name (`<00x-work>`).
  - Example: `001` from `001-add-order-endpoint`

- Requirement traceability MUST remain explicit by referencing the requirement identifier from the work package requirements document (`docs/<00x-work>/requirements.md`) in the test method comments.
  - The comments SHOULD explain how the test scenario proves the requirement and why the requirement is important.
  - Example identifiers: `FR1`, `FR2`, ...

### SHOULD

- Keep the overall suite aligned to a testing pyramid:
  - Many unit tests
  - Fewer integration tests
  - Fewest E2E/functional tests

### MUST NOT

- MUST NOT mix unit tests and Aspire-based integration/E2E tests in the same test project.
- MUST NOT add time-based sleeps/waits as a primary flake mitigation strategy.

## Output and Validation (optional)

- Validate success:
  - `dotnet test`

## References (optional)

- `/.github/instructions/folders.instructions.md`
- `/.github/instructions/work-packages.instructions.md`
- `/.github/instructions/aspire-tests.instructions.md`
- `/.github/instructions/playwright.instructions.md`
- https://learn.microsoft.com/dotnet/core/testing/
- https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices
- https://aspire.dev/testing/overview/
