---
description: 'Define enforceable best practices for using .NET Aspire in this repo so distributed apps are easy to run locally, observable, and ready for container-based deployment.'
applyTo: 'src/**/AppHost/**/*, src/**/ServiceDefaults/**/*, src/**/*AppHost*.csproj, src/**/*ServiceDefaults*.csproj, src/**/*.sln'
---

# .NET Aspire best practices

## Overview

These instructions standardize how .NET Aspire is used to compose and run distributed applications locally, and to ensure services are configured consistently for observability and container-based deployment.

## Scope

Applies to: `src/**/AppHost/**/*, src/**/ServiceDefaults/**/*, src/**/*AppHost*.csproj, src/**/*ServiceDefaults*.csproj, src/**/*.sln`

- Applies when creating or modifying an Aspire `AppHost` project.
- Applies when introducing new resources (databases, caches, message brokers) wired through Aspire.
- Applies when adding new services that will be orchestrated via Aspire.

## Instructions

### MUST

- Use an Aspire `AppHost` project as the composition root for local development of distributed applications.
- Keep the `AppHost` focused on composition only (resource definitions, references, configuration wiring). Business logic MUST NOT be implemented in the `AppHost`.

- Ensure the full system can be started locally via the `AppHost` entry point.
- Represent all required local dependencies as Aspire resources (for example: SQL Server, messaging, caches) instead of requiring manual setup steps.

- Centralize cross-cutting service configuration via a shared defaults project (commonly named `ServiceDefaults` in Aspire-based repos) when the repo uses that pattern.
  - All services orchestrated by Aspire MUST reference and use the shared defaults to keep health checks, telemetry, and resilience consistent.

- Treat observability as required for services wired into Aspire.
  - Services MUST emit structured logs.
  - Services MUST expose health checks suitable for local orchestration and readiness checks.

- Use stable, deterministic resource names in the `AppHost`.
  - Names MUST be consistent across machines.
  - Names MUST be safe for containerized environments (avoid whitespace and special characters).

- Keep configuration externalized.
  - Connection strings, keys, and credentials MUST NOT be checked into source control.
  - Local secrets SHOULD use user-secrets or a developer secret store.

- Before using a new Aspire feature, integration, or CLI/API surface (especially preview capabilities), check `https://aspire.dev/docs/` (primary) and Microsoft Learn for the current recommended approach and supported packages.

### SHOULD

- Prefer adding resources and service references through the `AppHost` rather than hard-coding endpoints/ports in services.
- Prefer using the Aspire CLI for local orchestration and deployment artifacts:
  - Local: `aspire run`
  - Deployment artifacts / environments: `aspire deploy`
- Prefer readiness/liveness patterns that work in both local orchestration and container platforms.
- Prefer explicit dependency wiring (service-to-resource references) over implicit configuration.

- Keep local developer experience fast:
  - Avoid unnecessary heavyweight resources.
  - Keep `AppHost` changes small and reviewable.

### MUST NOT

- MUST NOT rely on developers manually starting infrastructure outside Aspire when it can be represented as an Aspire resource.
- MUST NOT hard-code secrets, connection strings, or credentials in code, `appsettings*.json`, or project files.
- MUST NOT introduce machine-specific configuration (absolute paths, user-specific ports) as defaults in Aspire composition.
- MUST NOT use prerelease CLI channels (staging/dev) for production.

## Output and Validation (optional)

- Expected artifacts (as applicable):
  - An Aspire `AppHost` project under `src/`.
  - A shared defaults project (for example `ServiceDefaults`) referenced by orchestrated services.

- Validate success (as applicable):
  - The system starts from the `AppHost`.
  - All required resources are created and reachable.
  - Health checks report healthy for all services.

## References (optional)

- `https://aspire.dev/`
- `https://aspire.dev/docs/`
- `https://aspire.dev/get-started/app-host/`
- `https://aspire.dev/reference/cli/commands/aspire-run/`
- `https://aspire.dev/reference/cli/commands/aspire-deploy/`
- `https://aspire.dev/dashboard/overview/`
- https://learn.microsoft.com/dotnet/aspire/
- https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview
- https://learn.microsoft.com/dotnet/aspire/get-started/upgrade-to-aspire-13
- `/.github/instructions/dotnet-stack.instructions.md`
- `/.github/instructions/folders.instructions.md`
