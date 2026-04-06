---
description: 'Standardize repository contributions around the Microsoft stack: modern C#/.NET, Minimal APIs, Blazor, SQL Server, Aspire, and Azure Container Apps.'
applyTo: 'src/**/*.cs, src/**/*.csproj, src/**/*.sln, src/**/*.razor, src/**/*.json, infra/**/*'
---

# Microsoft stack .NET architecture guidelines

## Overview

These instructions define the default technical choices and architectural expectations for work in this repository. They are intended for contributors building new services, UIs, and supporting infrastructure on the Microsoft stack.

## Scope

Applies to: `src/**/*.cs, src/**/*.csproj, src/**/*.sln, src/**/*.razor, src/**/*.json, infra/**/*`

- Applies when creating new services, APIs, Blazor front ends, or distributed app wiring.
- Applies when selecting a database or local orchestration approach for a work item.

## Instructions

### MUST

- Use C# and the repository’s configured .NET SDK/runtime version (follow `global.json` if present; otherwise check Microsoft Learn to confirm the latest .NET LTS version and target it by default).
- Enable nullable reference types for new .NET projects (`<Nullable>enable</Nullable>`) and treat new nullable warnings as errors in touched projects when feasible.

- For new HTTP services, default to Minimal APIs (endpoint mapping on `WebApplication`) unless a concrete requirement needs an alternative.
- When exposing HTTP endpoints, provide OpenAPI output (for example via `Microsoft.AspNetCore.OpenApi`) and include health endpoints.
  - OpenAPI SHOULD be mapped only in the `Development` environment unless explicitly required.
  - Health endpoints SHOULD include separate readiness and liveness probes when the service has dependencies.

- For new UIs, default to Blazor for frontend work (create `*.razor` components and follow the repo’s established Blazor hosting model).

- Default database choice is Microsoft SQL Server.
- Use a migration-based schema workflow for SQL Server (for example EF Core migrations if the repo uses EF Core).
- Keep all connection strings and credentials out of source control; use configuration + secret injection (development user-secrets; production secret store/managed identity where applicable).
- Follow `/.github/instructions/configuration.instructions.md` for operator-managed configuration storage and secret-handling flows.

- Use .NET Aspire for desktop/local development for distributed applications.
  - Prefer the established Aspire patterns in the repo (for example an `AppHost` project and shared service defaults if present).
  - Ensure the full system can be started locally via the Aspire app host entry point.

- Assume services will be deployed as containers to Azure Container Apps.
  - New services MUST be containerizable (provide a repo-consistent `Dockerfile` or use .NET container publishing if that is the repo standard).
  - Prefer configuration via environment variables and external configuration sources over hard-coded values.
  - For Azure Container Apps, store sensitive values as platform secrets and reference them via environment variables.
  - Prefer managed identity for accessing Azure resources (for example Key Vault and Azure SQL) instead of client secrets.

- Design for scalability by default.
  - Prefer service-based boundaries over tightly coupled modules when adding new major capabilities.
  - When cross-service communication is needed, use an explicit contract and consider asynchronous messaging for decoupling.

### SHOULD

- Prefer pinning the SDK with `global.json` for repeatable local and CI builds, and keep it updated.
- Prefer .NET SDK container publishing (`dotnet publish` with `/t:PublishContainer`) for standard services unless a custom `Dockerfile` is required.

- Prefer Azure-native building blocks when introducing new infrastructure:
  - Messaging: prefer Azure Service Bus (queues/topics) unless a specific alternative is justified.
  - Identity: prefer managed identity for service-to-service and service-to-Azure authentication.

- When using messaging, design consumers/handlers to be idempotent and tolerant of duplicate deliveries.

- Use structured logging and distributed tracing for services (OpenTelemetry conventions if present in the repo).
- Use resilience patterns for cross-service calls (timeouts, retries with backoff, idempotency where applicable).

- Keep solution/project structure aligned with `/.github/instructions/folders.instructions.md`.
- For iterative work items, keep docs aligned with `/.github/instructions/work-packages.instructions.md`.

### MUST NOT

- MUST NOT introduce non-Microsoft primary stacks for new product code (for example Node/Express or non-.NET services) without an explicit, documented reason.
- MUST NOT create new HTTP APIs using controller-based MVC by default when Minimal APIs are sufficient.
- MUST NOT embed secrets in `appsettings*.json`, source code, or Dockerfiles.
- MUST NOT couple services through shared databases as an integration mechanism without a documented reason and constraints.

## Output and Validation (optional)

- Expected artifacts (as applicable):
  - Minimal API service project(s) under `src/`
  - Blazor UI project(s) under `src/`
  - SQL Server-backed persistence with migrations
  - Aspire app host wiring for local orchestration
  - Container build/publish path suitable for Azure Container Apps

- Validate success (as applicable):
  - `dotnet build`
  - `dotnet test`
  - Start the system locally through the Aspire app host

## References (optional)

- `/.github/instructions/configuration.instructions.md`
- `/.github/instructions/folders.instructions.md`
- `/.github/instructions/work-packages.instructions.md`
- https://learn.microsoft.com/dotnet/core/releases-and-support
- https://learn.microsoft.com/dotnet/core/tools/global-json
- https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview
- https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks
- https://learn.microsoft.com/aspnet/core/security/app-secrets
- https://learn.microsoft.com/aspnet/core/security/key-vault-configuration
- https://learn.microsoft.com/dotnet/core/containers/sdk-publish
- https://learn.microsoft.com/azure/container-apps/dotnet-overview
- https://learn.microsoft.com/azure/container-apps/managed-identity
- https://learn.microsoft.com/azure/well-architected/service-guides/azure-service-bus
