---
description: 'Standardize operator-managed configuration and secret handling so settings live in SQL Server, IG credentials stay protected, and configuration updates flow through the Blazor UI safely.'
applyTo: 'src/**/*.cs, src/**/*.csproj, src/**/*.json, src/**/*.razor, infra/**/*'
---

# Configuration management

## Overview

These instructions define how operator-managed configuration and secrets are handled in this repository. The goal is to keep runtime settings centralized, protect sensitive values such as IG credentials, and ensure configuration changes flow through the intended Blazor UI experience.

## Scope

Applies to: `src/**/*.cs, src/**/*.csproj, src/**/*.json, src/**/*.razor, infra/**/*`

- Applies when adding or changing configuration storage, configuration editing flows, secret handling, or admin/operator UI features.
- Applies to backend services, Blazor UI components, and infrastructure that support operator-managed configuration.

## Instructions

### MUST

- Store operator-managed configuration in Microsoft SQL Server.
- Keep IG credentials and similar third-party secrets securely protected at rest and in transit.
- Manage operator-facing configuration updates through the Blazor UI when this repository exposes configuration management features.
- Use write-only secret handling for operator-managed secrets:
  - Operators MAY set or replace a secret.
  - Stored secret values MUST NOT be displayed back to the operator after save.
  - APIs and UI models MUST avoid returning populated secret values once persisted.
- Keep secrets and connection details out of source control.
- Use explicit application services or repositories for configuration persistence rather than scattering configuration writes across unrelated UI or endpoint code.

### SHOULD

- Separate secret metadata from secret material when it improves maintainability or auditing.
- Prefer audit-friendly configuration update flows so operational changes can be traced when the application supports it.
- Keep Blazor configuration forms focused on editing current settings without exposing existing secret values.

### MUST NOT

- MUST NOT render existing secret values in Blazor forms, API responses, logs, exceptions, or diagnostics.
- MUST NOT store plaintext secrets in source control or sample configuration files.
- MUST NOT bypass the Blazor UI with ad hoc operator-only configuration workflows unless an explicit requirement justifies it.

## Output and Validation (optional)

- Validate success:
  - `dotnet build`
  - `dotnet test`
  - Verify operator configuration flows can update settings without revealing persisted secret values

## References (optional)

- `/.github/instructions/csharp.instructions.md`
- `/.github/instructions/dotnet-stack.instructions.md`
- `/.github/instructions/auth.instructions.md`
