---
description: 'Standardize .NET Aspire closed-box integration tests so distributed apps are exercised end-to-end with repeatable setup and cleanup.'
applyTo: 'test/**/*.cs, test/**/*.csproj'
---

# .NET Aspire testing guidelines

## Overview

These instructions define how to write automated tests for distributed applications orchestrated by a .NET Aspire `AppHost`. Aspire testing is intended for closed-box integration tests that launch the `AppHost` and its resources as separate processes.

## Scope

Applies to: `test/**/*.cs, test/**/*.csproj`

- Applies when creating or updating Aspire-based test projects.
- These rules are additive to `/.github/instructions/aspire.instructions.md` and `/.github/instructions/folders.instructions.md`.

## Instructions

### MUST

- Keep Aspire test projects and test code under `test/`.
- Use the `Aspire.Hosting.Testing` package when writing Aspire tests.
- Use `DistributedApplicationTestingBuilder` to launch the `AppHost` in tests.

- Treat Aspire tests as closed-box integration tests.
  - Interact with the system via external boundaries (HTTP endpoints, exposed ports, resource endpoints).
  - Influence behavior via configuration/environment variables.
  - Do not depend on in-process access to services hosted inside the `AppHost`.

- Ensure resources are cleaned up.
  - Dispose the testing builder and/or application instance at the end of each test (or test fixture) so containers/resources are torn down.

- Prefer running multiple Aspire test instances concurrently.
  - Do not rely on fixed ports by default.

### SHOULD

- Keep port randomization enabled (default).
  - Only disable port randomization when a concrete requirement needs stable ports.
  - If port randomization must be disabled, pass `"DcpPublisher:RandomizePorts=false"` as an argument when creating the testing builder.

- Keep the dashboard disabled (default).
  - Enable the dashboard only for local debugging scenarios.
  - If the dashboard must be enabled, set `DisableDashboard = false` when creating the testing builder.

- Prefer end-to-end assertions that validate service interactions, not only single-service behavior.

### MUST NOT

- MUST NOT attempt to mock, substitute, or replace dependency injection services inside the launched application processes.
- MUST NOT add hard-coded sleeps/time-based waits as the primary readiness strategy.
- MUST NOT disable port randomization in CI unless required.

## Output and Validation (optional)

- Expected artifacts:
  - Aspire test projects under `test/`.
  - Tests that start the `AppHost` using `DistributedApplicationTestingBuilder`.

- Validate success:
  - `dotnet test`
  - No leaked containers/resources after tests complete (resources should be cleaned up via disposal).

## References (optional)

- https://aspire.dev/testing/overview/
- https://aspire.dev/testing/write-your-first-test/
- https://aspire.dev/testing/manage-app-host/
- https://aspire.dev/testing/accessing-resources/
- https://www.nuget.org/packages/Aspire.Hosting.Testing
- `/.github/instructions/aspire.instructions.md`
- `/.github/instructions/folders.instructions.md`
