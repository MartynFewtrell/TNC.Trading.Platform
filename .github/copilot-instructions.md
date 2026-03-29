---
description: 'Defines repo-wide Copilot guidance for technology choices, references, authentication, and test naming so contributions stay consistent with the team’s .NET standards.'
applyTo: '**/*'
---

# Copilot Instructions

## Overview

These instructions standardize technology choices and contribution patterns across the repository. They are intended for any change that touches product code, tests, documentation, infrastructure, or build configuration.

## Scope

Applies to: `**/*`

## Instructions

### MUST

- When choosing a .NET target version (if `global.json` does not define it), determine the latest .NET LTS version via Microsoft Learn and target that by default.
- Ground .NET best-practice guidance in Microsoft Learn when possible, and periodically re-validate repository instruction files against Microsoft Learn.
- For .NET Aspire guidance, use `https://aspire.dev/` as the primary reference site and include it in documentation/rules so guidance can be re-researched as Aspire evolves.
- For local authentication, use Keycloak running in a container orchestrated by Aspire.
- For Azure authentication, use Microsoft Entra ID.
- Ensure authentication guidance and implementations are compatible with OIDC, OAuth 2.0, and SAML 2.0.
- Use the functional test naming convention: `<001>_<FR1>_point_of_test`, where `001` is the work package number (from the subfolder) and `FR1/FR2/...` comes from the requirements document.
- Use descriptive test file names that match the contained test class; avoid generic names like UnitTest1.cs.
- Project-wide developer run documentation (e.g., how to build/start/validate locally) should live at the top level under `docs/` for reuse across work packages, with work packages referencing it as needed.
- Implement requested Copilot artifacts as Agent Skills (in `.github/skills` with `SKILL.md`), not as `.prompt.md` files.
- Requirements documents should remain implementation-agnostic and refer to a data store for configuration rather than naming SQL Server directly.

### SHOULD

- Prefer C# with the latest .NET LTS, Minimal APIs for HTTP services, and Blazor for front ends when applicable.
- Prefer a data store for configuration rather than naming SQL Server directly when a relational database is required.
- Prefer .NET Aspire for local desktop/distributed development orchestration.
- Prefer Azure Container Apps for deployments when containerized hosting is appropriate.
- Prefer scalable service-based architecture with messaging when the problem domain benefits from it.

### MUST NOT

- MUST NOT select an arbitrary .NET version without first checking `global.json` and (when absent) confirming the current .NET LTS version via Microsoft Learn.
- MUST NOT provide Aspire guidance without using `https://aspire.dev/` as the primary reference.
- MUST NOT introduce a local authentication approach that bypasses Keycloak-in-Aspire unless the repository explicitly changes that standard.

## Output and Validation (optional)

- If adding or renaming functional tests, validate that names follow `<001>_<FRx>_point_of_test`.
- If changing runtime/authentication behavior, validate OIDC/OAuth2/SAML compatibility for the relevant environment (local vs Azure).

## References (optional)

- `https://learn.microsoft.com/dotnet/`
- `https://aspire.dev/`

- `./.github/instructions/architecture-guidelines.instructions.md`
- `./.github/instructions/aspire-best-practices.instructions.md`
- `./.github/instructions/aspire-testing.instructions.md`
- `./.github/instructions/authentication.instructions.md`
- `./.github/instructions/csharp.instructions.md`
- `./.github/instructions/docs-authoring.instructions.md`
- `./.github/instructions/folder-structure.instructions.md`
- `./.github/instructions/iterative-work-docs.instructions.md`
- `./.github/instructions/microsoft-stack-dotnet.instructions.md`
- `./.github/instructions/playwright-dotnet.instructions.md`
- `./.github/instructions/test-approach.instructions.md`
