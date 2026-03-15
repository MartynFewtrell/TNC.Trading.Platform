---
description: 'Make Scalar UI the default interactive API documentation experience for ASP.NET Core API projects in this repository.'
applyTo: 'src/**/*.cs, src/**/*.csproj'
---

# Scalar UI defaults for APIs

## Overview

These instructions standardize Scalar UI as the default interactive API documentation experience for HTTP APIs in this repository. They are intended for contributors creating or modifying ASP.NET Core API projects so OpenAPI is exposed consistently, health endpoints are visible in the UI, and Aspire dashboard links can open Scalar directly.

## Scope

Applies to: `src/**/*.cs, src/**/*.csproj`

- Applies when creating or updating ASP.NET Core API projects under `src/`.
- Applies when configuring OpenAPI endpoints, development-time API documentation, health endpoint visibility, and Aspire dashboard links for API resources.
- Does not apply to non-HTTP projects or to test-only projects under `test/`.

## Instructions

### MUST

- Add and configure Scalar UI for every ASP.NET Core API project created in this repository.
- Add and configure `Scalar.AspNetCore` and `Microsoft.AspNetCore.OpenApi` for API projects that expose HTTP endpoints.
- Configure each API to expose an OpenAPI document that Scalar UI can consume.
- Use the default route conventions:
  - OpenAPI document at `/openapi/v1.json`
  - Scalar UI at `/scalar/v1`
- Map OpenAPI and Scalar UI in the Development environment by default.
- Use `app.MapOpenApi()` and `app.MapScalarApiReference()` in the API startup entry point unless a shared abstraction is already established for the same behavior.
- Ensure health endpoints intended for operational use are included in the generated OpenAPI document when they should be visible in Scalar UI.
- When an API is orchestrated by .NET Aspire, add a dashboard link so the API resource can open Scalar UI directly.
- Keep API documentation configuration minimal and repo-consistent.
- Validate that the API still builds and that existing tests continue to pass after adding or changing Scalar UI.

### SHOULD

- Keep OpenAPI and Scalar configuration in the API startup entry point so it is easy to find and maintain.
- Prefer shared defaults or reusable extensions when multiple APIs need the same documentation configuration.
- Keep health endpoint OpenAPI metadata concise and operationally focused.
- Prefer configuring Aspire dashboard links on the HTTPS endpoint when Scalar UI is exposed over HTTPS.

### MUST NOT

- MUST NOT create a new API project without also enabling Scalar UI.
- MUST NOT expose Scalar UI in non-development environments unless explicitly required and documented.
- MUST NOT configure Scalar UI without also exposing a valid OpenAPI document.
- MUST NOT use inconsistent routes or naming conventions across API projects without a documented reason.
- MUST NOT rely on health check middleware alone when the requirement is for health endpoints to appear in the generated OpenAPI document; use endpoint mappings that contribute OpenAPI metadata.

## Output and Validation (optional)

- Expected artifacts:
  - API project package references for OpenAPI and Scalar UI support
  - API startup configuration that maps OpenAPI and Scalar UI
  - OpenAPI-visible health endpoints when required
  - Aspire AppHost dashboard link to Scalar UI when the API is orchestrated by Aspire

- Validate success:
  - `dotnet build`
  - `dotnet test`
  - Run the API locally and verify Scalar UI loads successfully at `/scalar/v1`
  - Verify the OpenAPI document is available at `/openapi/v1.json`
  - Verify the expected endpoints appear in Scalar UI

## References (optional)

- `./.github/copilot-instructions.md`
- `./.github/instructions/aspire-best-practices.instructions.md`
- `./.github/instructions/csharp.instructions.md`
- `./.github/instructions/microsoft-stack-dotnet.instructions.md`

## Notes (optional)

- This repository targets modern .NET and uses Minimal APIs by default for HTTP services.
- This file makes Scalar UI the default API documentation option for future API projects.
