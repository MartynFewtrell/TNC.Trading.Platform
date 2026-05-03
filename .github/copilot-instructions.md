---
description: 'Defines repo-wide Copilot guidance for technology choices, workflow boundaries, and where scoped repository standards live so contributions stay consistent with this repo’s .NET conventions.'
applyTo: '**/*'
---

# Copilot Instructions

## Overview

These instructions standardize technology choices and contribution patterns across the repository. They are intended for any change that touches product code, tests, documentation, infrastructure, or build configuration.

## Scope

Applies to: `**/*`

## General Guidelines

- Encourage established refactoring and design principles such as SOLID, DRY, separation of concerns, and explicit dependencies.
- For this repository's local development environment, Docker is now required because Keycloak is part of the local auth stack, and the local in-memory SQL option is considered redundant.
- To speed up local validation and test runs, prefer keeping Docker-backed infrastructure such as SQL Server and Keycloak running between application runs when supported by Aspire.

## Instructions

### MUST

- When choosing a .NET target version (if `global.json` does not define it), determine the latest .NET LTS version via Microsoft Learn and target that by default.
- Ground .NET best-practice guidance in Microsoft Learn when possible, and periodically re-validate repository instruction files against Microsoft Learn.
- For C# source files, keep one top-level class, interface, record, struct, enum, or delegate per file and name the file to match the top-level type. Prefer file-scoped namespaces in new C# files when all types in the file belong to the same namespace.
- Follow Microsoft Learn C# naming conventions for top-level types: use PascalCase for classes, records, structs, enums, and delegates; prefix interfaces with `I`; use singular enum names unless the enum is a flags enum.
- For .NET Aspire guidance, use `https://aspire.dev/` as the primary reference site and include it in documentation/rules so guidance can be re-researched as Aspire evolves.
- For local authentication, use Keycloak running in a container orchestrated by Aspire.
- For Azure authentication, use Microsoft Entra ID.
- Ensure authentication guidance and implementations are compatible with OIDC, OAuth 2.0, and SAML 2.0.
- Project-wide developer run documentation (e.g., how to build/start/validate locally) should live at the top level under `docs/` for reuse across work packages, with work packages referencing it as needed.
- Implement requested Copilot artifacts as Agent Skills (in `.github/skills` with `SKILL.md`), not as `.prompt.md` files.
- Keep `Program.cs` focused on startup orchestration when using Minimal APIs, and place endpoint mappings in dedicated registration extensions grouped by route or feature area.
- Do not draft `docs/00x-work/` work packages unless explicitly requested.
- Use automated test method names in the MethodName_StateUnderTest_ExpectedResult style, e.g., `CalculateTotal_ShouldReturnZero_WhenCartIsEmpty`, because it reads naturally and enhances clarity.
- Fully document automated tests with comments that capture requirement traceability, explain what each test verifies, the expected outcome, and why the behavior matters.
- Store plans in a `plans` subfolder within each work package, using `plans/001-delivery-plan.md` for the initial delivery plan and prefixes like `001-`, `002-`, `003-` for all subsequent plan files to show application order. For additional work package plans, number them in the sequence they are applied in the work package; for this auth package, the refactoring mitigation plan should be 004 rather than 002.
- Treat `docs/wiki/` as the implementation documentation source of truth. Before any numbered plan is considered complete, update the affected wiki pages to reflect implemented changes in behavior, architecture, API surface, runtime behavior, operator guidance, local development, or testing approach. When plan work is completed, the wiki documentation under `docs/wiki` should be updated as needed to reflect the implemented changes before the work is considered complete.
- When generating review reports in this repository, create a new report file instead of updating the existing report so prior report history remains visible.

### SHOULD

- Prefer C# with the latest .NET LTS, Minimal APIs for HTTP services, and Blazor for front ends when applicable.
- For Blazor UI development, prefer Radzen Blazor free components as the default UI component choice and follow `./.github/instructions/radzen-blazor.instructions.md`.
- Prefer .NET Aspire for local desktop/distributed development orchestration.
- Prefer Azure Container Apps for deployments when containerized hosting is appropriate.
- Prefer scalable service-based architecture with messaging when the problem domain benefits from it.

### MUST NOT

- MUST NOT select an arbitrary .NET version without first checking `global.json` and (when absent) confirming the current .NET LTS version via Microsoft Learn.
- MUST NOT provide Aspire guidance without using `https://aspire.dev/` as the primary reference.
- MUST NOT introduce a local authentication approach that bypasses Keycloak-in-Aspire unless the repository explicitly changes that standard.

## Output and Validation (optional)

- If changing runtime/authentication behavior, validate OIDC/OAuth2/SAML compatibility for the relevant environment (local vs Azure).

## References (optional)

- `https://learn.microsoft.com/dotnet/`
- `https://aspire.dev/`

- `./.github/instructions/architecture.instructions.md`
- `./.github/instructions/aspire.instructions.md`
- `./.github/instructions/aspire-tests.instructions.md`
- `./.github/instructions/auth.instructions.md`
- `./.github/instructions/configuration.instructions.md`
- `./.github/instructions/csharp.instructions.md`
- `./.github/instructions/docs.instructions.md`
- `./.github/instructions/dotnet-stack.instructions.md`
- `./.github/instructions/folders.instructions.md`
- `./.github/instructions/playwright.instructions.md`
- `./.github/instructions/radzen-blazor.instructions.md`
- `./.github/instructions/scalar.instructions.md`
- `./.github/instructions/tests.instructions.md`
- `./.github/instructions/work-packages.instructions.md`
