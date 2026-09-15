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
- Access resources by their declared name and endpoint with `CreateHttpClient(resourceName)` or `CreateHttpClient(resourceName, endpointName)` and the corresponding endpoint APIs. Do not infer resource identity from process output or local sockets.

- Treat Aspire tests as closed-box integration tests.
  - Interact with the system via external boundaries (HTTP endpoints, exposed ports, resource endpoints).
  - Influence behavior via configuration/environment variables.
  - Do not depend on in-process access to services hosted inside the `AppHost`.

- Ensure resources are cleaned up.
  - Make the fixture own the testing builder, distributed application, and any controlled dependency it creates.
  - Use `await using` or `IAsyncLifetime` and await application and builder disposal so containers and resources are torn down.
- Keep readiness bounded.
  - Use `WaitForResourceHealthyAsync(resourceName, cancellationToken)` or an equivalent bounded health/readiness check before issuing assertions.
  - Do not use hard-coded sleeps as readiness checks.
- Keep test configuration scoped to the fixture or testing builder. Do not mutate process-wide environment variables, launch profiles, or shared configuration as a test setup shortcut.

- Prefer running multiple Aspire test instances concurrently.
  - Keep Aspire's randomized proxy ports enabled and pass fixture-provided endpoint URIs to clients and browsers.

### SHOULD

- Keep port randomization enabled. Do not disable it in tests or CI.
- Keep the Aspire dashboard disabled for automated tests. Use named resource endpoints for assertions; the dashboard is a manual diagnostic surface, not an application endpoint.

- Prefer end-to-end assertions that validate service interactions, not only single-service behavior.

### MUST NOT

- MUST NOT attempt to mock, substitute, or replace dependency injection services inside the launched application processes.
- MUST NOT add hard-coded sleeps/time-based waits as the primary readiness strategy.
- MUST NOT parse AppHost output, scan TCP listeners, infer endpoints from launch profiles, or use dashboard endpoints as application endpoints.
- MUST NOT use fixed-port leases or process-wide environment overrides to coordinate test resources.
- MUST NOT disable port randomization in CI.

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
